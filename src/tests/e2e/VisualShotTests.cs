using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TrafficSimulation.App.Shot;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.E2E;

/// <summary>
/// The end-to-end visual tier (VER-10): it stages each scenario, photographs it through the game's
/// own shot path, and has the frame judged against the claims written down beside it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The verdict is an agent's.</b> The claims in <see cref="VisualScenario.Expect"/> have no
/// threshold in them — whether a dashed line is evenly pitched, whether a pavement sweeps round a
/// corner or kinks, whether traffic looks like traffic — so the test hands the frames, the claims and
/// the reference frame to a Claude Code agent (<see cref="VisualJudge"/>) and is green only if that
/// agent answers PASS. A red test carries the agent's own reasoning, claim by claim.
/// </para>
/// <para>
/// The harness is asserted first and cheaply: a frame written, at the size the scenario asked for,
/// from the place it was pinned to, carrying more than one flat colour. A frame that fails any of
/// those is never sent to be judged — it would be judging the harness, at the price of a judgement.
/// </para>
/// <para>
/// Every case opens its own rendering device and builds its own town, which is what makes it an
/// end-to-end test rather than a unit one. It needs no display: the frame is drawn offscreen, exactly
/// at the size asked for, with no compositor and nothing to steal focus. Judging adds about ten
/// seconds and a few cents a scenario — <c>TRAFFIC_E2E_JUDGE=off</c> takes the frames without it,
/// which is what to set while iterating on a scenario's framing.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.E2E)]
public class VisualShotTests
{
    static readonly SimConfig Config = SimConfig.Load();

    /// <summary>How many distinct colours a frame has to carry before it is a picture of anything. A
    /// frame that came back as one flat colour is the failure that reads as a pass everywhere else:
    /// the file is there, of the right name and the right size, containing nothing.</summary>
    const int LeastColours = 32;

    public static TheoryData<string> Core => [.. VisualScenarios.Named("core")];

    public static TheoryData<string> Wider => [.. VisualScenarios.Named("wider")];

    /// <summary>The fixture map's own set: the detail, on places drawn on purpose, and what a change
    /// to anything that draws is answered by.</summary>
    [Theory]
    [MemberData(nameof(Core))]
    public void ACoreScenarioPhotographsAndSaysWhatItMustShow(string name) => Photograph(name);

    /// <summary>What the fixture cannot answer: a whole city, the skewed crossing, the debug layers
    /// and the interface.</summary>
    [Theory]
    [MemberData(nameof(Wider))]
    public void AWiderScenarioPhotographsAndSaysWhatItMustShow(string name) => Photograph(name);

    static void Photograph(string name)
    {
        var scenario = VisualScenarios.ByName(name);
        var (widthPx, heightPx) = scenario.SizeFor();
        Directory.CreateDirectory(VisualScenarios.Frames);

        // A scenario owns every file that carries its name, and clears them before it starts. Without
        // this, a cell that was renamed or a scenario that stopped tiling leaves a picture behind, and
        // the next review opens it as though this run had taken it.
        foreach (var stale in Directory.GetFiles(VisualScenarios.Frames, $"{name}.*"))
            File.Delete(stale);
        foreach (var stale in Directory.GetFiles(VisualScenarios.Frames, $"{name}-*"))
            File.Delete(stale);

        var taken = new List<string>();
        foreach (var (file, atM) in scenario.Exposures())
        {
            var path = Path.Combine(VisualScenarios.Frames, file);
            var shot = ShotRun.Take(
                new ShotRequest(
                    Map: scenario.Map,
                    Path: path,
                    WidthPx: widthPx,
                    HeightPx: heightPx,
                    ViewM: scenario.ViewM,
                    AtM: atM,
                    Ui: scenario.Ui,
                    Seconds: scenario.Seconds,
                    RulerPointsM: scenario.RulerPointsM),
                Config);

            Assert.True(File.Exists(path), $"{file} was not written");
            Assert.Equal((widthPx, heightPx), SizeOf(path));

            // The camera was pinned where the scenario says, and the frame covers the ground it
            // claims to: a review that quotes px/m off the sheet is quoting this.
            if (atM is { } wanted) Assert.Equal(wanted, shot.CentreM);
            if (scenario.FrameWidthM > 0f) Assert.Equal(scenario.FrameWidthM, shot.SpanM.X, 0.5f);

            Assert.True(Colours(path) >= LeastColours,
                $"{file} carries fewer than {LeastColours} distinct colours — it is a picture of nothing");
            taken.Add(path);
        }

        // Several subjects become one contact sheet, as the reference implementation tiles them: the
        // claims are asked once, of every cell, and the cells are seen side by side.
        var frame = Path.Combine(VisualScenarios.Frames, scenario.Frame);
        if (scenario.Cells is not null)
        {
            Sheet.Tile(taken, frame);
            Assert.Equal(Sheet.SizeOf(taken.Count, widthPx, heightPx), SizeOf(frame));
        }

        if (scenario.Expected is not null)
        {
            var reference = Path.Combine(VisualScenarios.Expected, scenario.Expected);
            Assert.True(File.Exists(reference),
                $"{name} names the reference frame {scenario.Expected}, which is not in tests/e2e/expected/");
        }

        var brief = JudgeBrief.Write(scenario);
        if (!VisualJudge.Enabled) return;

        var verdict = VisualJudge.Judge(scenario, brief);
        Assert.True(verdict.Passed, $"{name} — the agent did not pass it:\n{verdict.Complaint()}\n"
                                    + $"Its reasoning is beside the frames, in .tmp/e2e/{name}.verdict.md");
    }

    static (int WidthPx, int HeightPx) SizeOf(string path)
    {
        var info = Image.Identify(path);
        return (info.Width, info.Height);
    }

    /// <summary>How many distinct colours a frame carries, sampled on a coarse lattice — enough to
    /// tell a picture from a flat fill, and cheap enough to run on every frame of the set.</summary>
    static int Colours(string path)
    {
        using var image = Image.Load<Rgba32>(path);
        var seen = new HashSet<uint>();
        for (var y = 0; y < image.Height; y += 8)
            for (var x = 0; x < image.Width; x += 8)
                seen.Add(image[x, y].PackedValue);

        return seen.Count;
    }
}
