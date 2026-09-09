using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Terrain;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// Which way round a crossing's record is, pinned against the ground the town was laid with. A record
/// carries an axis, a depth and the road it is painted across, and says nothing about what any of them is
/// across — a reader that guessed would lay every foot crossing along the road it was meant to cross.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P3)]
public class CrosswalkGeometryTests
{
    public static TheoryData<string> Maps => Towns.EveryTown();

    /// <summary>
    /// <b>The axis runs along the road, not across it</b> — a walker crosses square to the axis, over the
    /// span, and the depth is how much of the road's own length the paint takes up. Past either end of the
    /// span is ground to step off onto; a stride off either flank of the depth is still the road the
    /// crossing is painted across.
    /// </summary>
    /// <remarks>
    /// <b>The step off is taken into the walk and not a stride past the kerb.</b> The span is the
    /// carriageway's own width, and the ground is classified cell by cell — so the road's edge stands
    /// wherever its half-width rounded to, and the crossing's own band is swept past that to reach it
    /// (<c>GroundPainter.Crossing</c>). A cell either way is inside that rounding and says nothing about
    /// which way round the record is, which is the whole of what this asks.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void TheAxisIsTheWayAcrossAndTheSpanIsHowFar(string map)
    {
        var plan = Towns.Of(map);
        var terrain = new GroundLocator(plan, SimConfig.Shipped());
        var crossings = plan.Crosswalks;
        if (crossings.Count == 0) return;

        var strideM = SimConfig.Shipped().Terrain.GroundStepM;
        for (var crossing = 0; crossing < crossings.Count; crossing++)
        {
            var centreM = crossings.CentreM[crossing];
            var axis = Vector2.Normalize(crossings.Axis[crossing]);
            var across = new Vector2(-axis.Y, axis.X);

            Assert.Equal(Ground.Crosswalk, terrain.GroundAt(centreM));

            var beyond = plan.CrossingSpanM(crossing) * 0.5f + (plan.PavementWidthM * 0.5f);
            foreach (var end in (ReadOnlySpan<Vector2>)[centreM + across * beyond, centreM - across * beyond])
            {
                Assert.True(
                    terrain.At(end).Walkable && !terrain.At(end).Drivable,
                    $"{map}: crossing {crossing} ends on {terrain.GroundAt(end)}, which is not somewhere to step off onto");
            }

            var flank = crossings.DepthM[crossing] * 0.5f + strideM;
            foreach (var side in (ReadOnlySpan<Vector2>)[centreM + axis * flank, centreM - axis * flank])
            {
                Assert.True(
                    terrain.At(side).Drivable,
                    $"{map}: crossing {crossing} has {terrain.GroundAt(side)} beside it, not the road it crosses");
            }
        }
    }

    /// <summary>
    /// <b>A crossing is painted on the road it names</b> (TER-6), which is what makes that road's width the
    /// span it is drawn, walked and stopped for at: the road's own line runs under the middle of the zebra,
    /// and near enough along its axis for the skew to be what lengthens the paint rather than a sign it is
    /// across some other road.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryCrossingIsLaidOnTheRoadItNames(string map)
    {
        var plan = Towns.Of(map);
        var crossings = plan.Crosswalks;
        for (var crossing = 0; crossing < crossings.Count; crossing++)
        {
            var road = crossings.Road[crossing];
            Assert.InRange(road, 0, plan.Roads.Count - 1);

            var centreM = crossings.CentreM[crossing];
            var arcs = plan.Roads.SegmentsOf(road);
            var lengthM = Spline.TotalLengthM(arcs);
            var at = Spline.SampleAt(arcs, Spline.ProjectM(arcs, centreM, lengthM * 0.5f, lengthM));

            var offM = (at.PositionM - centreM).Length();
            Assert.True(
                offM <= plan.Roads.WidthM[road] * 0.5f,
                $"{map}: crossing {crossing} stands {offM:F2} m off road {road}, whose width it is drawn to span");

            var alongItsRoad = MathF.Abs(Vector2.Dot(at.Direction, Vector2.Normalize(crossings.Axis[crossing])));
            Assert.True(
                alongItsRoad >= MathF.Cos(MathF.PI / 4f),
                $"{map}: crossing {crossing} lies at {MathF.Acos(alongItsRoad) * 180f / MathF.PI:F0} deg to road {road}");
        }
    }

    /// <summary>
    /// <b>A crossing reads square on the road it stands on.</b> Its bars are drawn straight across its own
    /// axis, so the road's line stands off that axis by no more than a paint line's width anywhere under the
    /// paint or under the bar behind it — which keeps a zebra off a corner's own fillet
    /// (<see cref="RoadFigures.CrossingSetbackM"/>) and lets one stand on the gentle arc a ring road's arm is.
    /// </summary>
    /// <remarks>
    /// <b>Asked of the road's own line and not of where the paint was put.</b> Placed by a setback off
    /// whatever ground the node reaches, a crossing at a fork stands a stride past flat junction ground and
    /// one at a node with no fork stood the same stride past an <em>arc</em> — which curves away under it.
    /// The tightest zebras on both shipped towns were those: a metre and a half of square road behind the
    /// paint, where the rest of the town had three.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryCrossingReadsSquareOnItsRoad(string map)
    {
        var plan = Towns.Of(map);
        var config = SimConfig.Shipped();
        var bundleM = (config.Road.CrossingDepthM * 0.5f) + config.Road.StopBarSetbackM + config.Road.StopBarThicknessM;
        var allowedM = config.Road.PaintLineWidthM + Kerbs.RoundingM;

        var skewed = new List<string>();
        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            var arcs = plan.Roads.SegmentsOf(plan.Crosswalks.Road[crossing]);
            if (arcs.Length == 0) continue;

            var centreM = plan.Crosswalks.CentreM[crossing];
            var axis = Vector2.Normalize(plan.Crosswalks.Axis[crossing]);
            var lengthM = Spline.TotalLengthM(arcs);
            var alongM = Spline.ProjectM(arcs, centreM, lengthM * 0.5f, lengthM);

            foreach (var way in (ReadOnlySpan<float>)[1f, -1f])
            {
                foreach (var outM in (ReadOnlySpan<float>)[config.Road.CrossingDepthM * 0.5f, bundleM])
                {
                    // Past the end of its own road there is no line of this road's to read off, and what is
                    // beyond is the next one's ground.
                    var atM = alongM + (outM * way);
                    if (atM < 0f || atM > lengthM) continue;

                    var offM = atM - alongM;
                    var lineM = Spline.SampleAt(arcs, atM).PositionM - centreM;
                    var offTheAxisM = MathF.Abs((axis.X * lineM.Y) - (axis.Y * lineM.X));
                    if (offTheAxisM <= allowedM) continue;

                    skewed.Add($"crossing {crossing} at {centreM} stands {offTheAxisM:F2} m off its road {offM:+0.0;-0.0} m out");
                }
            }
        }

        Assert.True(
            skewed.Count == 0,
            $"{map}: {skewed.Count} of {plan.Crosswalks.Count} crossings do not read square on their road — "
            + string.Join("; ", skewed.Take(5)));
    }
}
