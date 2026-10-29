using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>An intercity road has no walk beside it, and nothing stands in its verge</b> (GEN-75): asked of
/// <see cref="Towns.Built"/>, whose brief lays intercity roads and stands buildings and props.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P5)]
[Collection(TownGeometryCollection.Name)]
public class UnwalkedRoadsTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static CityPlan Plan => Towns.Built;

    const float StepM = 5f;

    /// <summary>
    /// <b>No walk runs beside a road with no walk</b> (GEN-75): halfway across where a walk would stand either side
    /// of its carriageway the ground is not walk, anywhere along it clear of the junctions at its two ends — whose
    /// corners a walked road turns round.
    /// </summary>
    [Fact]
    public void NoWalkRunsBesideARoadWithNoWalk()
    {
        var plan = Plan;
        var ground = new GroundShapes(plan.Paving(Config), Config);
        var besideM = Config.WalkInnerM + (Config.PavementWidthM * 0.5f);
        var junctions = plan.Junctions;

        for (var road = 0; road < plan.Roads.Count; road++)
        {
            if (plan.Roads.IsWalked(road)) continue;

            var arcs = plan.Roads.SegmentsOf(road);
            var lengthM = Spline.TotalLengthM(arcs);
            var halfM = plan.Roads.WidthM[road] * 0.5f;
            var (from, to) = (plan.Roads.FromJunction[road], plan.Roads.ToJunction[road]);
            for (var atM = 0f; atM <= lengthM; atM += StepM)
            {
                var on = Spline.SampleAt(arcs, atM);
                if (NearAnEnd(junctions, from, on.PositionM) || NearAnEnd(junctions, to, on.PositionM)) continue;

                foreach (var side in (ReadOnlySpan<float>)[-1f, 1f])
                {
                    var atSideM = on.PositionM + (on.Right * side * (halfM + besideM));
                    if (!Towns.OnTheMap(plan, atSideM)) continue;

                    Assert.True(
                        ground.At(atSideM) != Ground.Sidewalk,
                        $"road {road} has walk beside it at {atSideM.X:F0},{atSideM.Y:F0}");
                }
            }
        }
    }

    /// <summary>
    /// <b>A zebra across a road with no walk lands on the walk at both ends</b> (GEN-75), over its whole depth: it is
    /// painted at a crossroads on it and never where its far side has nothing walked.
    /// </summary>
    [Fact]
    public void AZebraAcrossARoadWithNoWalkLandsOnTheWalkAtBothEnds()
    {
        var plan = Plan;
        var paving = plan.Paving(Config);
        var ground = new GroundShapes(paving, Config);
        var pastTheKerbM = Config.WalkInnerM + (Config.PavementWidthM * 0.5f);
        var halfDepthM = Config.Road.CrossingDepthM * 0.5f;

        foreach (var station in paving.RoadEnds(Config).CrossedM)
        {
            if (station.Road < 0 || plan.Roads.IsWalked(station.Road)) continue;

            var across = Vector2.Normalize(station.FarM - station.NearM);
            var along = new Vector2(-across.Y, across.X);
            foreach (var edge in (ReadOnlySpan<float>)[-1f, 0f, 1f])
            {
                var nearM = station.NearM - (across * pastTheKerbM) + (along * edge * halfDepthM);
                var farM = station.FarM + (across * pastTheKerbM) + (along * edge * halfDepthM);
                Assert.True(
                    ground.At(nearM) == Ground.Sidewalk && ground.At(farM) == Ground.Sidewalk,
                    $"a zebra across road {station.Road} at {station.NearM.X:F0},{station.NearM.Y:F0} lands on " +
                    $"{ground.At(nearM)} and {ground.At(farM)} at its {(edge == 0f ? "middle" : "edge")}");
            }
        }
    }

    /// <summary>
    /// <b>No building stands in the verge of a road with no walk</b> (GEN-75): every corner of every building is further
    /// off its line than its half and <see cref="CityGenFigures.UnwalkedVergeM"/>.
    /// </summary>
    [Fact]
    public void NoBuildingStandsInAVerge()
    {
        var plan = Plan;
        var lines = Unwalked(plan);
        var buildings = plan.Buildings;
        for (var building = 0; building < buildings.Count; building++)
        {
            var axis = Heading.Unit(buildings.HeadingRad[building]);
            var side = Heading.RightOf(axis);
            var halfM = buildings.SizeM[building] * 0.5f;
            foreach (var (along, across) in (ReadOnlySpan<(float, float)>)[(-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f)])
            {
                var cornerM = buildings.CentreM[building] + (axis * along * halfM.X) + (side * across * halfM.Y);
                var (road, offM) = Nearest(lines, cornerM);
                Assert.True(
                    road < 0 || offM > (plan.Roads.WidthM[road] * 0.5f) + Config.CityGen.UnwalkedVergeM,
                    $"building {building} stands {offM:F1} m off road {road}'s line at {cornerM.X:F0},{cornerM.Y:F0}");
            }
        }
    }

    /// <summary>
    /// <b>No prop the ground scatters stands in the verge of a road with no walk</b> (GEN-75): its whole reach clear of
    /// the road's half and the verge.
    /// </summary>
    [Fact]
    public void NoScatteredPropStandsInAVerge()
    {
        var plan = Plan;
        var lines = Unwalked(plan);
        var props = plan.Props;
        for (var prop = 0; prop < props.CentreM.Length; prop++)
        {
            var (road, offM) = Nearest(lines, props.CentreM[prop]);
            Assert.True(
                road < 0 || offM - props.RadiusM[prop] > (plan.Roads.WidthM[road] * 0.5f) + Config.CityGen.UnwalkedVergeM,
                $"prop {prop} stands {offM:F1} m off road {road}'s line at {props.CentreM[prop].X:F0},{props.CentreM[prop].Y:F0}");
        }
    }

    /// <summary>
    /// Whether a place is within a walked corner's reach of a junction: its disc, the run on past the crossing at its
    /// mouth (<see cref="RoadFigures.CrossingDepthM"/>) and a walk, and that walk's rounded end and the corner's radius.
    /// </summary>
    static bool NearAnEnd(CityPlan.JunctionArrays junctions, int junction, Vector2 atM) =>
        Vector2.Distance(atM, junctions.CentreM[junction])
        < junctions.RadiusM[junction] + Config.Road.CrossingDepthM + (2f * Config.WalkOuterM)
        + Config.IntersectionCornerRadiusM;

    static ChainIndex Unwalked(CityPlan plan)
    {
        var building = new ChainIndex.Builder();
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            if (plan.Roads.IsWalked(road)) continue;

            var arcs = plan.Roads.SegmentsOf(road);
            building.Add(road, arcs, Spline.TotalLengthM(arcs));
        }

        return building.Seal(Config.Grid.Main);
    }

    static (int Road, float OffM) Nearest(ChainIndex lines, Vector2 atM)
    {
        if (lines.ChainCount == 0) return (-1, 0f);

        var road = lines.Nearest(lines.NewScan(), atM, float.MaxValue, out _, out var offM);
        return (road, offM);
    }
}
