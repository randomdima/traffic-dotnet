using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Terrain;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// Which side of a junction's kerb fillet is road, pinned against the ground the town was laid with. The
/// format carries a corner, an arc centre, a radius and two tangent points and says nothing about it
/// — and <b>the fillet, not the junction disc, is where a walker's ground ends</b>,
/// so the walking network's band round a junction is laid off whichever side this says.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class JunctionCornerGeometryTests
{
    public static TheoryData<string> Maps => Towns.EveryTown();

    /// <summary>
    /// The arc centre stands in the block, the ground inside the arc is walkable and the ground outside it
    /// is carriageway. Read the other way round, a band laid half a walk off the fillet lands in the road.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void TheGroundInsideAFilletIsWalkableAndOutsideItIsRoad(string map)
    {
        var plan = Towns.Of(map);
        var terrain = new TerrainGrid(plan, SimConfig.Shipped());
        var corners = plan.JunctionCorners;
        if (corners.Count == 0) return;

        var strideM = plan.CellSizeM;
        for (var corner = 0; corner < corners.Count; corner++)
        {
            var arcCentreM = corners.ArcCentreM[corner];
            var radiusM = corners.RadiusM[corner];
            var toCorner = Vector2.Normalize(corners.CornerM[corner] - arcCentreM);

            var insideM = arcCentreM + toCorner * (radiusM - strideM);
            var outsideM = arcCentreM + toCorner * (radiusM + strideM);

            Assert.True(
                terrain.At(insideM).Walkable,
                $"{map}: corner {corner} is {terrain.GroundAt(insideM)} a stride inside its own arc, not somewhere to walk");
            Assert.True(
                terrain.At(outsideM).Drivable,
                $"{map}: corner {corner} is {terrain.GroundAt(outsideM)} a stride outside its own arc, not the road it rounds");
        }
    }

    /// <summary>
    /// <b>A junction disc is not its kerb</b> (TER-5). The disc is the ground its arms share, so it stops
    /// inside the pavement of every road that meets it — anything laid off a disc that reached past one is
    /// laid on the walk. Asked of every junction, because the arithmetic that gets it wrong looks perfectly
    /// reasonable and gets it wrong everywhere at once.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryJunctionDiscStopsInsideTheRoadsThatMeetAtIt(string map)
    {
        var plan = Towns.Of(map);
        var roads = plan.Roads;

        for (var road = 0; road < roads.Count; road++)
        {
            foreach (var junction in (ReadOnlySpan<int>)[roads.FromJunction[road], roads.ToJunction[road]])
            {
                var reachM = (roads.WidthM[road] * 0.5f) + plan.PavementWidthM;
                Assert.True(
                    plan.Junctions.RadiusM[junction] <= reachM,
                    $"{map}: junction {junction} is {plan.Junctions.RadiusM[junction]:F2} m across where road " +
                    $"{road} reaches {reachM:F2} m, so its disc is laid over the walk");
            }
        }
    }
}
