using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Terrain;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// The shallow bar every map is held to, and <b>nothing more</b>: it validates, its junctions are
/// junctions, no lit junction can show two conflicting greens, it is furnished, and nothing is laid
/// on its water.
/// </summary>
/// <remarks>
/// A whole map is the wrong place to ask a detailed question — "every bridge carries a footway",
/// asked of whatever a city happens to contain, is a different question every time somebody edits the
/// city and is vacuous on a map with no bridge. Detailed geometry is asked of named places on the
/// fixture map, and this engine owes those the day it carries the named places
/// as a generated town would be.
/// <para>
/// One clause of the bar is only half asked here, and it is said rather than left out: <b>the greens
/// themselves</b> need the cycle table and the signal agent, which arrive with the lit town. What a
/// plan alone can answer — that a lit junction carries a phase offset inside the cycle it is staggered
/// against — is what is asserted, and the conflicting-greens half lands with the traffic lights.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
public class MapConformanceTests
{
    public static TheoryData<string> Maps => Towns.EveryShippedMap();

    /// <summary>Everything a record points at exists: an index into a run that is not there is a town nobody can build.</summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ItValidates(string map)
    {
        var plan = Towns.Of(map);

        Assert.True(plan.CellSizeM > 0f);
        Assert.True(plan.WorldSizeM.X > 0f && plan.WorldSizeM.Y > 0f);

        for (var road = 0; road < plan.Roads.Count; road++)
        {
            Assert.InRange(plan.Roads.FromJunction[road], 0, plan.Junctions.Count - 1);
            Assert.InRange(plan.Roads.ToJunction[road], 0, plan.Junctions.Count - 1);
            Assert.True(plan.Roads.WidthM[road] > 0f, $"road {road} has no width");
            Assert.True(plan.Roads.SegmentsOf(road).Length > 0, $"road {road} has no shape");
        }

        // A crossing struck mid-block belongs to no junction, which is a record pointing at nothing
        // and not a broken reference.
        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            Assert.InRange(plan.Crosswalks.Junction[crossing], CityPlan.NoRecord, plan.Junctions.Count - 1);
        }

        for (var bar = 0; bar < plan.StopLines.Count; bar++)
        {
            Assert.InRange(plan.StopLines.Junction[bar], 0, plan.Junctions.Count - 1);
            Assert.InRange(plan.StopLines.Road[bar], 0, plan.Roads.Count - 1);
        }

        for (var bridge = 0; bridge < plan.Bridges.Count; bridge++)
        {
            Assert.InRange(plan.Bridges.Road[bridge], 0, plan.Roads.Count - 1);
            Assert.True(plan.Bridges.ToM[bridge] > plan.Bridges.FromM[bridge], $"bridge {bridge} spans nothing");
            Assert.True(plan.Bridges.DeckWidthM[bridge] > plan.Roads.WidthM[plan.Bridges.Road[bridge]],
                $"bridge {bridge}'s deck is no wider than the carriageway it carries");
        }
    }

    /// <summary>A junction is where roads meet, so a junction no road is an arm of is not one.</summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ItsJunctionsAreJunctions(string map)
    {
        var plan = Towns.Of(map);
        var arms = new int[plan.Junctions.Count];
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            arms[plan.Roads.FromJunction[road]]++;
            arms[plan.Roads.ToJunction[road]]++;
        }

        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            Assert.True(arms[junction] > 0, $"{map}: junction {junction} has no road running into it");
            Assert.True(plan.Junctions.RadiusM[junction] > 0f, $"{map}: junction {junction} has no reach");
        }
    }

    [Theory]
    [MemberData(nameof(Maps))]
    public void ALitJunctionIsStaggeredInsideItsOwnCycle(string map)
    {
        var plan = Towns.Of(map);
        var cycleS = SimConfig.Shipped().Signals.CycleS;

        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            if (!plan.Junctions.Lit[junction]) continue;

            Assert.InRange(plan.Junctions.PhaseOffsetS[junction], 0f, cycleS);
        }
    }

    /// <summary>
    /// A town is furnished — buildings to go to, props on its verges, somewhere to park and a roster
    /// to do it. A map with nobody living on it is a <em>scenario</em>: one of each thing rather than
    /// a population, laid to put one behaviour under a microscope, and it still carries the thing it
    /// is for.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ItIsFurnished(string map)
    {
        var plan = Towns.Of(map);

        Assert.True(plan.Roads.Count > 0, $"{map} has no streets");
        Assert.True(plan.Spawns.Count > 0, $"{map} has nobody on it");

        if (plan.Buildings.Count == 0)
        {
            // What a scenario carries is the thing it was laid for: paint to be watched crossing, or
            // road that is a shape rather than a line. A scenario of neither is a bare grid.
            Assert.True(
                plan.Crosswalks.Count > 0 || Bends(plan),
                $"{map} is a scenario with nothing on it to watch");
            return;
        }

        Assert.True(plan.Props.Count > 0, $"{map} has buildings and bare verges");
        Assert.True(plan.ParkingLots.SpaceCount > 0, $"{map} has buildings and nowhere to park");
        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            Assert.True(plan.Buildings.Capacity[building] > 0, $"{map}: building {building} holds nobody");
        }
    }

    /// <summary>Whether any road on the map is a curve, which is what a map about the shape of roads carries.</summary>
    static bool Bends(CityPlan plan)
    {
        foreach (var segment in plan.Roads.Segments)
        {
            if (segment.Curvature != 0f) return true;
        }

        return false;
    }

    /// <summary>
    /// Nothing stands on ground legal to nobody. A carriageway may cross it — that is what a bridge
    /// is — but a building, a prop, a bay or a body placed on water is a town that cannot be left.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NothingIsLaidOnItsWater(string map)
    {
        var plan = Towns.Of(map);
        var grid = new TerrainGrid(plan, SimConfig.Shipped());

        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            AssertOffTheWater(grid, plan.Buildings.CentreM[building], $"{map}: building {building}");
        }

        for (var prop = 0; prop < plan.Props.Count; prop++)
        {
            AssertOffTheWater(grid, plan.Props.CentreM[prop], $"{map}: prop {prop}");
        }

        for (var space = 0; space < plan.ParkingLots.SpaceCount; space++)
        {
            AssertOffTheWater(grid, plan.ParkingLots.SpacePositionM[space], $"{map}: bay {space}");
        }

        for (var spawn = 0; spawn < plan.Spawns.Count; spawn++)
        {
            AssertOffTheWater(grid, plan.Spawns.PositionM[spawn], $"{map}: spawn {spawn}");
        }
    }

    static void AssertOffTheWater(TerrainGrid grid, System.Numerics.Vector2 pointM, string what)
    {
        var ground = grid.At(pointM);
        Assert.True(ground.Walkable || ground.Drivable, $"{what} stands at {pointM} on ground permitted to nobody");
    }
}
