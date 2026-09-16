using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>The road laid to its two ends and not the other way round</b> (TER-5d): every road in a generated town
/// leaves on the bearing its arm was drawn with, arrives on the one the far arm was drawn with, and bends no
/// tighter than its class's design speed affords on the way.
/// </summary>
/// <remarks>
/// <b>This is the inversion, asked of the town.</b> The bearing used to be whatever the chord happened to
/// leave; here it is drawn first and the road is the thing that has to satisfy it, so what these cases guard
/// is that the satisfying really happened rather than being approached.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P2)]
public class RoadSplineTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    public static TheoryData<ulong> Seeds() => Towns.Seeds();

    static CityPlan Lay(ulong seed) => Towns.LaidFrom(seed);

    /// <summary>
    /// <b>A road's end tangents are the bearings it was given</b>, to the angle a joint may be open by and
    /// still be one line (<c>RoadStage.CreaseRad</c>) — which is a figure and not a taste: it is the
    /// rounding two computations of one distance disagree by, taken at the offset the furthest line off a
    /// carriageway is laid at. <b>Twice it, because two bearings are being compared</b>: the arm's own and
    /// the chain's, each carried through a reduction of its own.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryRoadArrivesOnTheBearingsItsArmsWereDrawnWith(ulong seed)
    {
        var plan = Lay(seed);
        var ground = plan.Ground;
        var openRad = RoadStage.CreaseRad(Config);
        var onARing = RingRoads(plan);

        for (var road = 0; road < ground.Roads.Count; road++)
        {
            var arcs = ground.Roads.SegmentsOf(road);

            // A ring piece takes its bearings rather than being laid to them: the circle is the layout's
            // and GEN-19 sizes it, so what it owes its nodes is <c>EveryRoundaboutRunsThroughItsOwnJunctions</c>'.
            if (arcs.Length == 0 || onARing[road]) continue;

            var from = ConnectionPoints.ArmOf(ground, Config, road, atFrom: true);
            var to = ConnectionPoints.ArmOf(ground, Config, road, atFrom: false);

            Assert.Equal(0f, Apart(arcs[0].StartUnit, from.StandUnit), openRad * 2f);
            Assert.Equal(
                0f, Apart(Heading.Unit(arcs[^1].HeadingAtRad(arcs[^1].LengthM)), -to.StandUnit), openRad * 2f);
        }
    }

    /// <summary>Which of a town's roads circulate on one of its roundabouts (GEN-19).</summary>
    static bool[] RingRoads(CityPlan plan)
    {
        var onARing = new bool[plan.Roads.Count];
        for (var ring = 0; ring < plan.Roundabouts.Count; ring++)
        {
            foreach (var road in plan.Roundabouts.RoadsOf(ring)) onARing[road] = true;
        }

        return onARing;
    }

    /// <summary>How far apart two bearings are, in radians, which is nil where they are the same bearing.</summary>
    static float Apart(Vector2 one, Vector2 other) =>
        MathF.Acos(Math.Clamp(Vector2.Dot(one, other), -1f, 1f));

    /// <summary>
    /// <b>Nothing bends tighter than its own floor</b>: what its design speed affords on tarmac, derived
    /// from a speed and a grip and never authored as a radius. A ring is the exception it always was — its
    /// circle is the layout's and is sized by GEN-19.
    /// </summary>
    /// <remarks>
    /// <b>A road that passes places has a junction's floor</b> (GEN-47, GEN-51): every place it passes was
    /// a junction, and what a car held there was the movement across it. The figure is the junction's own
    /// and not a second one, so what this asks of a joined road is the bound GEN-48 already sets.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NothingBendsTighterThanItsOwnClassAffords(ulong seed)
    {
        var plan = Lay(seed);
        var onARing = RingRoads(plan);

        var bends = 0;
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            if (onARing[road]) continue;

            // The loosest floor a road of the class this one could be has, because the plan does not carry
            // a road's class: a road inside the arterial's floor is inside every class's, and what a street
            // may take is asked of the arm's own bound instead (<see cref="ConnectionPoints"/>).
            var floorM = plan.Roads.ThroughOf(road).Length > 0
                ? Config.JunctionCorneringRadiusM
                : RoadLines.FloorRadiusM(Config, RoadClass.Street);

            foreach (var arc in plan.Roads.SegmentsOf(road))
            {
                if (arc.Curvature == 0f) continue;

                bends++;
                var radiusM = 1f / MathF.Abs(arc.Curvature);
                Assert.True(
                    radiusM >= floorM - 0.01f,
                    $"road {road} bends to {radiusM:F1} m against a floor of {floorM:F1} m");
            }
        }

        Assert.True(bends > 0, "not one road in the town bends at all, so nothing was asked about a bend");
    }

    /// <summary>
    /// <b>Every lane's two ends are its road's own two connection points</b> (TER-5d, TER-5i) — the exit
    /// point of the arm it sets off on and the entry point of the arm it arrives at. Each road was laid to the
    /// points its arms were drawn with, so the offset to a lane's own share of the carriageway lands on them:
    /// there is nothing to cut back, and no figure a reader has to add to a lane's metres.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryLanesTwoEndsAreItsTwoConnectionPoints(ulong seed)
    {
        var plan = Lay(seed);
        var ground = plan.Ground;
        var lanes = plan.Paving(Config).Lanes;

        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            var road = lanes.LaneRoad[lane];
            var arcs = lanes.ArcsOf(lane);
            var leavesFrom = lanes.LaneFromJunction[lane] == ground.Roads.FromJunction[road];

            var offM = Vector2.Distance(arcs[0].StartM, PointOn(ground, road, leavesFrom, LaneEnd.Exit));
            Assert.True(
                offM < 0.01f,
                $"lane {lane} of road {road} ({ground.Roads.Flow[road]}, {ground.Roads.LanesOn(road)} lanes, "
                + $"{ground.Roads.WidthM[road]:F2} m wide) starts {offM:F3} m off its own connection point");
            Assert.Equal(
                0f, Vector2.Distance(arcs[^1].EndM, PointOn(ground, road, !leavesFrom, LaneEnd.Enter)), 2);
        }
    }

    /// <summary>One arm's point of the kind asked for, which every arm a road is driven onto carries.</summary>
    static Vector2 PointOn(GroundPieces ground, int road, bool atFrom, LaneEnd end)
    {
        Span<ConnectionPoint> points = stackalloc ConnectionPoint[ConnectionPoints.MostPerArm];
        var found = ConnectionPoints.At(ground, Config, road, atFrom, points);
        for (var at = 0; at < found; at++)
        {
            if (points[at].End == end) return points[at].AtM;
        }

        Assert.Fail($"the arm of road {road} at its {(atFrom ? "from" : "to")} end drew no {end} point");
        return Vector2.Zero;
    }

    /// <summary>
    /// <b>A strict district lays straighter roads than a loose one</b>: how much a street wanders
    /// is drawn from the district it runs in, so a town laid all strict and the same town laid all loose
    /// read differently.
    /// </summary>
    /// <remarks>
    /// <b>One reading against the parameter that drives it</b>, and not a bound on either. It is asked of
    /// two whole towns rather than of the districts inside one, because which district a road runs in is
    /// not something the plan carries — and how far a town's roads run past their own chords is the reading
    /// the parameter moves.
    /// </remarks>
    [Fact]
    public void AStrictDistrictLaysStraighterRoadsThanALooseOne()
    {
        var strict = StraightShare(Towns.LayFresh(Towns.Brief(4242, gridDistrictShare: 1f)));
        var loose = StraightShare(Towns.LayFresh(Towns.Brief(4242, gridDistrictShare: 0f)));

        Assert.True(
            strict < loose,
            $"a town of strict districts ran {strict:P2} past its own chords against {loose:P2} for a town "
            + "of loose ones");
    }

    /// <summary>
    /// How far a town's roads run past the straight line between their own two ends, as a share: nought
    /// where every road is a straight and larger the more they wander.
    /// </summary>
    static float StraightShare(CityPlan plan)
    {
        var overM = 0f;
        var chordM = 0f;
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var arcs = plan.Roads.SegmentsOf(road);
            if (arcs.Length == 0) continue;

            var runM = Vector2.Distance(arcs[0].StartM, arcs[^1].EndM);
            overM += Spline.TotalLengthM(arcs) - runM;
            chordM += runM;
        }

        return chordM == 0f ? 0f : overM / chordM;
    }
}
