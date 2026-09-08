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
[Trait(Priority.Key, Priority.P3)]
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
    /// <b>The band is closed wherever it stops.</b> A run is drawn as its own band, square across both
    /// ends, and what closes it is the round the answer measures past that end
    /// (<c>GroundShapes.Paved</c>) — the round itself where the run really stops (<c>Paving.Caps</c>), and
    /// the sector of it between the two bands where the run hands over to another
    /// (<c>Paving.Corners</c>). So every metre the answer calls pavement stands beside a run, inside a
    /// round, or inside a turn's wedge. A round dropped where the band really stops is a bite out of the
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
                        // wrong — and not within a place of the end itself, since two runs hand over at two
                        // places that far apart (<see cref="Kerbs.OnePlaceM"/>) and which of them a point
                        // that close is beside is the same toss.
                        var pointM = endM + (new Vector2(x, y) * stepM) + new Vector2(0.013f, 0.017f);
                        if (Vector2.Distance(pointM, endM) <= Kerbs.OnePlaceM) continue;
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

    /// <summary>
    /// <b>An arm's end reaches the town's outline only where nothing stands across it</b>
    /// (<see cref="Paving.OpenEnds"/>): at a crossroads every arm's end lies inside the street across it and
    /// is no run at all, and at a dead end the line turned round the road's head is the pavement's own run —
    /// unless something else stands there, as the fixture's slab does at one of its dead ends.
    /// </summary>
    [Fact]
    public void ACrossroadsArmIsBuriedAndADeadEndIsOpen()
    {
        var plan = Towns.Of(Towns.Fixture);
        var paving = plan.Paving(SimConfig.Shipped());
        var arms = RoadCuts.ArmsPerJunction(plan.Ground);
        var openDeadEnds = 0;
        var crossroads = 0;

        for (var road = 0; road < plan.Roads.Count; road++)
        {
            foreach (var (junction, end) in (ReadOnlySpan<(int, int)>)[(plan.Roads.FromJunction[road], 0), (plan.Roads.ToJunction[road], 1)])
            {
                var open = paving.OpenEnds[(road * 2) + end];
                if (arms[junction] == 1 && open) openDeadEnds++;
                if (arms[junction] != 4) continue;

                crossroads++;
                Assert.False(open, $"road {road}'s end at crossroads {junction} reaches the outline");
            }
        }

        Assert.True(crossroads > 0, "no crossroads arm to ask about");
        Assert.True(openDeadEnds > 0, "no dead end reaches the outline");
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

        // A turn's arc is struck about the place a run stopped at, half a walk in on its across side, and
        // the wedge it closes is the sector of that round between the arc's two ends — or, where two runs
        // lie along one another and the turn is the straight step between their kerbs, the band half a
        // walk deep on that side of it.
        foreach (var corner in paving.Corners)
        {
            foreach (var arc in corner)
            {
                if (arc.Curvature == 0f)
                {
                    var alongM = Vector2.Dot(pointM - arc.StartM, arc.StartUnit);
                    var inM = Vector2.Dot(pointM - arc.StartM, Heading.RightOf(arc.StartUnit));
                    if (alongM >= 0f && alongM <= arc.LengthM && inM >= 0f && inM <= halfWalkM) return true;

                    continue;
                }

                var centreM = arc.StartM + (Heading.RightOf(arc.StartUnit) * halfWalkM);
                var fromM = pointM - centreM;
                if (fromM.Length() > halfWalkM) continue;

                var startRad = MathF.Atan2(arc.StartM.Y - centreM.Y, arc.StartM.X - centreM.X);
                var roundRad = MathF.Atan2(fromM.Y, fromM.X) - startRad;
                while (roundRad < 0f) roundRad += MathF.Tau;
                if (roundRad <= arc.LengthM * arc.Curvature) return true;
            }
        }

        return false;
    }

    static float OffTheRunM(in PavedRun run, Vector2 pointM, out float alongM)
    {
        alongM = Spline.ProjectM(run.Line, pointM, run.LengthM * 0.5f, run.LengthM);
        return (Spline.SampleAt(run.Line, alongM).PositionM - pointM).Length();
    }
}
