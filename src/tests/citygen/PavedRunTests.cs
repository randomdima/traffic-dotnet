using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>The runs the pavement is cut into, and the rounds that close them.</b> The band is everything within
/// half a walk of the line the town's outline was cut at (TER-3c.3), so what a run is and where one stops
/// decides both the concrete drawn and the ground answered.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class PavedRunTests
{
    public static TheoryData<string> Maps => Towns.EveryMapWithAFootway();

    /// <summary>
    /// <b>No run is shorter than its own two ends can be told apart</b> (<see cref="Kerbs.OnePlaceM"/>).
    /// The outline is cut where nothing stands nearer than the offset, asked with a rounding's grace — and
    /// a wrapping line that grazes another runs that far past the point they cross before the grace runs
    /// out. Kept, each of those spans is a run whose two ends are one place: a walk-wide round of pavement
    /// struck off a few centimetres of line, answered as ground and drawn as a circle standing in the
    /// middle of the band.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoRunIsShorterThanItsOwnEndsStandApart(string map)
    {
        var paving = Towns.Of(map).Paving(SimConfig.Shipped());

        foreach (var run in paving.Walk)
        {
            Assert.True(
                run.LengthM > Kerbs.OnePlaceM,
                $"{map}: a run of {run.LengthM:F3} m stands at {run.Line[0].StartM}");
        }
    }

    /// <summary>
    /// <b>The band is closed wherever it stops.</b> A run is drawn as its own skirt, square across both
    /// ends, and what closes it is the round the answer measures past that end
    /// (<c>GroundShapes.Paved</c>) — so every metre the answer calls pavement stands either beside a run or
    /// inside one of those rounds. A round dropped where the band really stops is a bite out of the
    /// concrete; the whole of it is the picture and the answer being one construction (TER-7).
    /// </summary>
    /// <remarks>
    /// Asked about the ground round every end of every run on the fixture map, at a fifth of a walk — where
    /// the two disagree they disagree at an end, and one point in five hundred thousand of a city says the
    /// same as the fixture does at a hundredth of the cost.
    /// </remarks>
    [Fact]
    public void EveryMetreOfPavementNearAnEndIsClosedByARound()
    {
        var paving = Towns.Of(Towns.Fixture).Paving(SimConfig.Shipped());
        var halfWalkM = paving.WalkM * 0.5f;
        var stepM = paving.WalkM * 0.2f;
        var asked = 0;

        foreach (var run in paving.Walk)
        {
            foreach (var endM in (ReadOnlySpan<Vector2>)[run.Line[0].StartM, run.Line[^1].EndM])
            {
                for (var x = -6; x <= 6; x++)
                {
                    for (var y = -6; y <= 6; y++)
                    {
                        // Off the grid the ends themselves stand on, so that no point lands exactly on the
                        // seam between two runs — where the answer is a coin toss and neither reading is
                        // wrong.
                        var pointM = endM + (new Vector2(x, y) * stepM) + new Vector2(0.013f, 0.017f);
                        if (!Answered(paving, pointM, halfWalkM)) continue;

                        asked++;
                        Assert.True(
                            Drawn(paving, pointM, halfWalkM),
                            $"{Towns.Fixture}: {pointM} is pavement the band does not close, off the end at "
                            + $"{endM}");
                    }
                }
            }
        }

        Assert.True(asked > 0, $"{Towns.Fixture}: no pavement stands near an end to ask about");
    }

    /// <summary>Within half a walk of a run, which is the whole of what makes ground pavement.</summary>
    static bool Answered(Paving paving, Vector2 pointM, float halfWalkM)
    {
        foreach (var run in paving.Walk)
        {
            if (OffTheRunM(run, pointM, out _) <= halfWalkM) return true;
        }

        return false;
    }

    /// <summary>Beside a run, or inside the round that closes an end.</summary>
    static bool Drawn(Paving paving, Vector2 pointM, float halfWalkM)
    {
        foreach (var run in paving.Walk)
        {
            var offM = OffTheRunM(run, pointM, out var alongM);
            if (alongM > 0f && alongM < run.LengthM && offM <= halfWalkM) return true;
        }

        foreach (var cap in paving.Caps)
        {
            var fromM = pointM - cap.PlaceM;
            if (fromM.Length() <= halfWalkM && Vector2.Dot(fromM, cap.OutwardM) >= 0f) return true;
        }

        return false;
    }

    static float OffTheRunM(in PavedRun run, Vector2 pointM, out float alongM)
    {
        alongM = Spline.ProjectM(run.Line, pointM, run.LengthM * 0.5f, run.LengthM);
        return (Spline.SampleAt(run.Line, alongM).PositionM - pointM).Length();
    }
}
