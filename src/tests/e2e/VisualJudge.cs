using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.Tests.E2E;

/// <summary>
/// The judge: it hands one scenario's frames, its claims and its reference frame to a Claude Code
/// agent and takes the verdict back as the test's own.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the only part of the suite whose answer is not arithmetic</b>, and it is here because
/// the questions this tier asks have no threshold in them: whether a dashed line is evenly pitched,
/// whether a pavement sweeps round a corner or kinks, whether traffic looks like traffic. The agent
/// is given the same brief a person would be given (<see cref="JudgeBrief"/>), reads the images with
/// its own Read tool, and answers one claim at a time.
/// </para>
/// <para>
/// <b>It costs money and about ten seconds a scenario</b>, so the whole judged set is a few dollars
/// and a few minutes. <c>TRAFFIC_E2E_JUDGE=off</c> takes the frames and skips the judging, which is
/// what to set while iterating on a scenario's framing; <c>TRAFFIC_E2E_MODEL</c> picks the model.
/// </para>
/// <para>
/// A verdict is not perfectly repeatable — two runs can disagree on a marginal claim. That is the
/// price of the tier, and it is the reason the agent is asked for its reasoning per claim: a red run
/// is read, not just counted.
/// </para>
/// </remarks>
internal static class VisualJudge
{
    /// <summary>Whether the judge runs at all. Off takes the frames and asserts only the harness.</summary>
    public static bool Enabled =>
        !string.Equals(Environment.GetEnvironmentVariable("TRAFFIC_E2E_JUDGE"), "off",
            StringComparison.OrdinalIgnoreCase);

    /// <summary>Which model judges. An alias the CLI knows, or a full model name.</summary>
    static string Model =>
        Environment.GetEnvironmentVariable("TRAFFIC_E2E_MODEL") is { Length: > 0 } named ? named : "sonnet";

    /// <summary>How long one scenario may take before the run is a failure rather than a hang.</summary>
    static TimeSpan Patience =>
        int.TryParse(Environment.GetEnvironmentVariable("TRAFFIC_E2E_JUDGE_TIMEOUT_S"), out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.FromMinutes(5);

    /// <summary>
    /// Judge one scenario's frames and write the verdict beside them. Throws rather than returning a
    /// default when the agent cannot be reached or does not answer in the shape it was asked for: a
    /// verdict nobody produced must never read as a pass.
    /// </summary>
    public static JudgeVerdict Judge(VisualScenario scenario, string brief)
    {
        var answer = Ask(brief);
        var verdict = Parse(answer);
        File.WriteAllText(
            Path.Combine(VisualScenarios.Frames, $"{scenario.Name}.verdict.md"), Sheet(scenario, verdict));
        return verdict;
    }

    /// <summary>Run the CLI on the brief and return what the agent said. The brief goes in on stdin so
    /// that no part of it has to survive a command line.</summary>
    static string Ask(string brief)
    {
        var claude = new ProcessStartInfo("claude")
        {
            WorkingDirectory = ProjectPaths.Root,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        claude.ArgumentList.Add("--print");
        claude.ArgumentList.Add("--output-format");
        claude.ArgumentList.Add("json");
        claude.ArgumentList.Add("--model");
        claude.ArgumentList.Add(Model);

        // Read and nothing else, and no prompting for the rest: a judge that can edit the tree could
        // answer a claim by changing what it is about.
        claude.ArgumentList.Add("--allowedTools");
        claude.ArgumentList.Add("Read");
        claude.ArgumentList.Add("--permission-mode");
        claude.ArgumentList.Add("dontAsk");

        using var process = Start(claude);
        process.StandardInput.Write(brief);
        process.StandardInput.Close();

        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit((int)Patience.TotalMilliseconds))
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw new JudgeUnreachableException($"the judging agent did not answer within {Patience.TotalSeconds:0} s");
        }

        var said = output.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
            throw new JudgeUnreachableException(
                $"claude exited {process.ExitCode}: {Tail(errors.GetAwaiter().GetResult(), said)}");

        // The CLI's own envelope, whose `result` is what the agent finally said.
        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(said, Lenient);
            if (envelope?.Result is { Length: > 0 } result) return result;
        }
        catch (JsonException broken)
        {
            throw new JudgeUnreachableException($"claude did not print the JSON envelope: {broken.Message}");
        }

        throw new JudgeUnreachableException("claude answered with an empty result");
    }

    static Process Start(ProcessStartInfo claude)
    {
        try
        {
            return Process.Start(claude) ?? throw new JudgeUnreachableException("claude did not start");
        }
        catch (System.ComponentModel.Win32Exception missing)
        {
            throw new JudgeUnreachableException(
                "the `claude` CLI is not on PATH — this tier is judged by an agent. Install it, or set "
                + "TRAFFIC_E2E_JUDGE=off to take the frames without judging them. " + missing.Message);
        }
    }

    /// <summary>The agent's JSON, taken out of whatever it was wrapped in. A fence or a sentence in
    /// front of it is a formatting slip and not a reason to lose a verdict.</summary>
    static JudgeVerdict Parse(string answer)
    {
        var opens = answer.IndexOf('{');
        var closes = answer.LastIndexOf('}');
        if (opens < 0 || closes <= opens)
            throw new JudgeUnreachableException($"the agent answered with no JSON object: {Short(answer)}");

        JudgeVerdict? verdict;
        try
        {
            verdict = JsonSerializer.Deserialize<JudgeVerdict>(answer[opens..(closes + 1)], Lenient);
        }
        catch (JsonException broken)
        {
            throw new JudgeUnreachableException(
                $"the agent's JSON did not parse ({broken.Message}): {Short(answer)}");
        }

        if (verdict is null || string.IsNullOrWhiteSpace(verdict.Verdict))
            throw new JudgeUnreachableException($"the agent's answer carries no verdict: {Short(answer)}");

        return verdict;
    }

    /// <summary>The verdict as it is kept beside the frames — the reasoning, not just the answer, so a
    /// red run can be read rather than re-run.</summary>
    static string Sheet(VisualScenario scenario, JudgeVerdict verdict)
    {
        var sheet = new StringBuilder();
        sheet.Append($"# {scenario.Name} — {verdict.Verdict}\n\n{verdict.Summary}\n\n");
        sheet.Append("| claim | verdict | why |\n|---|---|---|\n");
        foreach (var claim in verdict.Claims ?? [])
            sheet.Append($"| {claim.N} | {claim.Verdict} | {claim.Why?.Replace("|", "\\|")} |\n");

        if (verdict.Differences is { Length: > 0 })
        {
            sheet.Append("\n## Differences from the reference frame\n\n");
            foreach (var difference in verdict.Differences) sheet.Append($"- {difference}\n");
        }

        sheet.Append("\n## The claims as they were written\n\n");
        for (var claim = 0; claim < scenario.Expect.Length; claim++)
            sheet.Append($"{claim + 1}. {scenario.Expect[claim]}\n");

        return sheet.ToString();
    }

    static string Tail(string errors, string output) =>
        Short(string.IsNullOrWhiteSpace(errors) ? output : errors);

    static string Short(string text) =>
        text.Length <= 400 ? text.Trim() : text[..400].Trim() + " …";

    static readonly JsonSerializerOptions Lenient = new() { PropertyNameCaseInsensitive = true };

    sealed record Envelope([property: JsonPropertyName("result")] string? Result);
}

/// <summary>What the agent answered. <see cref="Verdict"/> is PASS or FAIL for the scenario as a
/// whole; a claim may also be UNCLEAR, which says the frame cannot answer it.</summary>
internal sealed record JudgeVerdict(
    string Verdict,
    string? Summary,
    ClaimVerdict[]? Claims,
    string[]? Differences)
{
    public bool Passed => string.Equals(Verdict, "PASS", StringComparison.OrdinalIgnoreCase);

    /// <summary>The failure message a red test carries: the agent's own words, claim by claim.</summary>
    public string Complaint()
    {
        var said = new StringBuilder(Summary ?? "the agent did not pass this scenario");
        foreach (var claim in Claims ?? [])
            if (!string.Equals(claim.Verdict, "PASS", StringComparison.OrdinalIgnoreCase))
                said.Append($"\n  claim {claim.N} {claim.Verdict}: {claim.Why}");

        return said.ToString();
    }
}

internal sealed record ClaimVerdict(int N, string Verdict, string? Why);

/// <summary>The judge could not be reached or did not answer in the shape it was asked for. Its own
/// exception type because it is not a verdict: the town was never judged.</summary>
internal sealed class JudgeUnreachableException(string message) : Exception(message);
