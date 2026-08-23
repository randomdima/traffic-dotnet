using System.Text;
using TrafficSimulation.App.Shot;

namespace TrafficSimulation.Tests.E2E;

/// <summary>
/// What the judging agent is asked. One place, because the brief is the test's real assertion: every
/// word here is part of what "the town looks right" means.
/// </summary>
/// <remarks>
/// <para>
/// <b>The traps are the load-bearing half.</b> A reviewer who assumes the road conventions of
/// somewhere else reports a stop bar that covers one lane as a fault, and a corner that carries no
/// lights as a missing signal. Each line below was a wrong verdict before it was a rule.
/// </para>
/// <para>
/// The reference frame is handed over as a reference and never as a gate: it is the same scenario as
/// drawn by the godot-dotnet build, and the two engines are allowed to differ. A difference is
/// reported, not failed — otherwise this tier becomes a pixel-diff against another engine's art, which
/// is a check nobody wants and which the render tier could not pass either.
/// </para>
/// </remarks>
internal static class JudgeBrief
{
    /// <summary>
    /// Compose the brief and keep it beside the frames, whether or not it is going to be asked. It is
    /// the written half of this tier: a frame nobody can see the question for is not reviewable, and
    /// this is the file to read first when a scenario answers something nobody asked.
    /// </summary>
    public static string Write(VisualScenario scenario)
    {
        var brief = Compose(scenario);
        File.WriteAllText(Path.Combine(VisualScenarios.Frames, $"{scenario.Name}.brief.md"), brief);
        return brief;
    }

    /// <summary>The prompt for one scenario: what it is, how big a metre is on it, which files to open,
    /// the claims, the traps, and the exact JSON to answer with.</summary>
    public static string Compose(VisualScenario scenario)
    {
        var (widthPx, heightPx) = scenario.SizeFor();
        var brief = new StringBuilder();

        brief.Append("You are judging a rendered frame of a top-down 2D traffic simulation of a small "
                     + "town against claims written down before the frame was taken.\n\n");

        brief.Append("## Open these first\n\n");
        brief.Append("Read EVERY image below with the Read tool before you answer anything. A frame you "
                     + "did not open is a frame you cannot judge.\n\n");
        brief.Append($"- the frame under judgement: `{Rooted(scenario.Frame)}`\n");
        if (scenario.Expected is not null)
            brief.Append($"- for reference only: `{Rooted(scenario.Expected, reference: true)}`\n");
        brief.Append('\n');

        brief.Append("## What this is\n\n");
        brief.Append($"{scenario.Subject}\n\n");
        brief.Append($"- map: {scenario.Map}, {scenario.Seconds:0.#} s into a seeded run\n");
        brief.Append($"- interface: {(scenario.Ui.Length == 0 ? "the ordinary interface" : string.Join(", ", scenario.Ui))}\n");
        brief.Append($"- {(scenario.Cells is null ? "frame" : "each cell")}: {widthPx} x {heightPx} px");
        if (scenario.FrameWidthM > 0f)
            brief.Append($", {scenario.FrameWidthM:0.#} m across, **{scenario.PxPerM:0.#} px per metre** "
                         + $"— the finest thing the claims name is {scenario.FinestFeatureM:0.###} m, "
                         + $"which is {scenario.FinestFeatureM * scenario.PxPerM:0.#} px here");
        brief.Append("\n\n");

        if (scenario.Cells is { Length: > 1 })
        {
            var columns = Sheet.Columns(scenario.Cells.Length);
            brief.Append($"**This frame is a contact sheet**: {scenario.Cells.Length} separate subjects "
                         + $"tiled {columns} across in reading order — left to right, then down — "
                         + "separated by magenta gutters. The magenta is never part of a picture. The "
                         + "cells, in that order, are: "
                         + string.Join(", ", scenario.Cells.Select((cell, at) => $"{at + 1} {cell.Label}"))
                         + ".\n\n**Every claim is asked of EVERY cell.** Name the cell a verdict is "
                         + "about.\n\n");
        }

        brief.Append("## The claims\n\n");
        for (var claim = 0; claim < scenario.Expect.Length; claim++)
            brief.Append($"{claim + 1}. {scenario.Expect[claim]}\n");
        brief.Append('\n');

        if (scenario.Expected is not null)
        {
            brief.Append("## The reference frame\n\n");
            brief.Append("It is the same scenario as drawn by another implementation of this same "
                         + "simulation. **It is a reference, not a gate.** Answer every claim against "
                         + "the frame under judgement, never against the reference. A difference "
                         + "between them — art, colour, sprite size, how much of a debug layer is "
                         + "drawn — goes in `differences` and NEVER makes a claim fail.\n");
            if (scenario.ExpectedNote is not null)
                brief.Append($"\n{scenario.ExpectedNote}\n");
            brief.Append('\n');
        }

        brief.Append(Rules);

        brief.Append("\n## Answer with\n\n");
        brief.Append("ONE JSON object, no prose around it, no markdown fence:\n\n");
        brief.Append("""
                     {"verdict":"PASS"|"FAIL",
                      "claims":[{"n":1,"verdict":"PASS"|"FAIL"|"UNCLEAR","why":"one sentence naming where in the frame you looked, and for a failure by how much"}],
                      "differences":["one sentence each, or empty"],
                      "summary":"one sentence"}
                     """);
        brief.Append("\n\n`verdict` is FAIL if any claim is FAIL, and PASS otherwise. An UNCLEAR claim "
                     + "does not fail the scenario, but say plainly what you could not see.\n");
        return brief.ToString();
    }

    /// <summary>How to judge, and the doctrine a reviewer would otherwise invent. Each trap was a wrong
    /// verdict somebody reached before it was written down.</summary>
    const string Rules = """
        ## How to judge

        - Answer the claim that was written, not "does this look nice". Each claim is one falsifiable
          sentence; work through them one at a time and in order.
        - The px per metre above is your ruler. At 34 px/m a 0.15 m road marking is 5 px and a 4 m car
          is 136 px. "That line looks thin" is not a finding; "that line is 2 px where a marking is 5"
          is. Crop or zoom mentally before you call something wrong.
        - UNCLEAR is a real verdict and the honest one when the frame does not show it. A claim about
          something outside the frame, or too small to resolve, is UNCLEAR — never a PASS.
        - A body may cover a marking: a car parked over a bay line hides it. That is UNCLEAR, not FAIL.
        - Do NOT report a violation of doctrine you have invented. This town's rules are its own:
          - A stop bar covers ONE LANE — half the carriageway, centreline to kerb. A bar across the
            whole road would be the fault.
          - A zebra crossing spans the WHOLE carriageway, kerb to kerb, unlike a bar.
          - A corner is a road that turns, not a junction: it is supposed to carry no lights, no stop
            bar and no crossing. A lit bend would be the fault.
          - An inline junction is a two-arm junction on a straight road carrying one lit mid-block
            crossing. It has no side road, and that is correct.
          - Pedestrians cross only at crossings. A person on the carriageway away from one is a fault;
            a person on a pavement, verge, footway or crossing is not.
          - A parking lot may reach under the carriageway, so a road with a lot beside it carries no
            kerb line on that side. That is not a missing marking.
          - Magenta lines in a reference frame are gutters between tiled cells, never part of a picture.
          - Traffic signals are photographed at whatever moment their cycle had reached. Two frames
            showing different colours is not a difference in behaviour.
        """;

    static string Rooted(string file, bool reference = false) =>
        reference
            ? Path.Combine("src", "tests", "e2e", "expected", file).Replace('\\', '/')
            : Path.Combine(".tmp", "e2e", file).Replace('\\', '/');
}
