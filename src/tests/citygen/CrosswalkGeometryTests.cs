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
    /// <b>A crossing stands clear of every bend the road takes.</b> The paint has to lie on straight kerb
    /// rather than on a corner's own arc (<see cref="RoadFigures.CrossingSetbackM"/>), so past the paint's
    /// own edge the road holds the bearing it has under it for the bar behind the crossing and that stride
    /// again.
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
    public void EveryCrossingStandsClearOfTheBendsOfItsOwnRoad(string map)
    {
        var plan = Towns.Of(map);
        var config = SimConfig.Shipped();
        var wantedM = config.Road.StopBarSetbackM + config.Road.StopBarThicknessM + config.Road.CrossingSetbackM;

        var tooNear = new List<string>();
        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            var arcs = plan.Roads.SegmentsOf(plan.Crosswalks.Road[crossing]);
            if (arcs.Length == 0) continue;

            var lengthM = Spline.TotalLengthM(arcs);
            var alongM = Spline.ProjectM(arcs, plan.Crosswalks.CentreM[crossing], lengthM * 0.5f, lengthM);
            var squareRad = Spline.SampleAt(arcs, alongM).HeadingRad;
            var edgeM = config.Road.CrossingDepthM * 0.5f;

            foreach (var way in (ReadOnlySpan<float>)[1f, -1f])
            {
                var clearM = SquareOnM(arcs, lengthM, alongM + (edgeM * way), squareRad, way, wantedM);
                if (clearM >= wantedM - Kerbs.RoundingM) continue;

                tooNear.Add($"crossing {crossing} at {plan.Crosswalks.CentreM[crossing]} has {clearM:F2} m");
            }
        }

        Assert.True(
            tooNear.Count == 0,
            $"{map}: {tooNear.Count} of {plan.Crosswalks.Count} crossings have less than {wantedM:F2} m of "
            + $"square road past their paint — {string.Join("; ", tooNear.Take(5))}");
    }

    /// <summary>
    /// How far past a place the road still holds a bearing, one way, up to what the caller needs. <b>A road
    /// leaves a straight on the straight's own bearing</b>, so an arc's first metres read square: what is
    /// asked is where the line has turned off it by more than the paint's own line is wide.
    /// </summary>
    static float SquareOnM(
        ReadOnlySpan<ArcSeg> arcs, float lengthM, float fromM, float squareRad, float way, float mostM)
    {
        var turnedRad = SimConfig.Shipped().Road.PaintLineWidthM / SimConfig.Shipped().RoadWidthM;
        for (var stepM = 0f; stepM <= mostM; stepM += Kerbs.RoundingM * 10f)
        {
            // Past the end of its own road there is no bend of this road's to stand clear of, and what is
            // beyond is the next one's ground.
            var atM = fromM + (stepM * way);
            if (atM < 0f || atM > lengthM) return mostM;

            if (MathF.Abs(Spline.WrapRad(Spline.SampleAt(arcs, atM).HeadingRad - squareRad)) > turnedRad)
            {
                return stepM;
            }
        }

        return mostM;
    }
}
