using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Terrain;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>The shallow bar a town is held to, as one machine with two readers.</b> The town tier asks it of the
/// maps this build lays (<see cref="Towns.EveryLaidMap"/>); <see cref="Tier.Maps"/> asks the same bar of the
/// cities somebody ships, deliberately and by name.
/// </summary>
/// <remarks>
/// A second implementation of the bar would be a second answer, and a city passing one and failing the
/// other is not something anybody could settle by looking at the town.
/// </remarks>
internal static class Conformance
{
    /// <summary>A junction is where roads meet, so a junction no road is an arm of is not one.</summary>
    public static void ItsJunctionsAreJunctions(string map)
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

    public static void ALitJunctionIsStaggeredInsideItsOwnCycle(string map)
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
    public static void ItIsFurnished(string map)
    {
        var plan = Towns.Of(map);

        Assert.True(plan.Roads.Count > 0, $"{map} has no streets");
        Assert.True(plan.Spawns.Count > 0, $"{map} has nobody on it");

        if (plan.Buildings.Count == 0)
        {
            // What a scenario carries is the thing it was laid for: paint to be watched crossing, road
            // that is a shape rather than a line, or a fleet standing on it where the road is only
            // somewhere to do the thing. A scenario of none of the three is a bare grid.
            Assert.True(
                plan.Crosswalks.Count > 0 || Bends(plan) || StandsAFleet(plan),
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

    /// <summary>
    /// Whether the map stands a whole fleet of cars, which is what a map <em>about the cars</em> carries —
    /// the skidpad, where the road is plain on purpose and every question is about what is standing on it.
    /// A town would never put this many cars in one place with nowhere for any of them to go.
    /// </summary>
    static bool StandsAFleet(CityPlan plan)
    {
        var cars = 0;
        foreach (var kind in plan.Spawns.Kind)
        {
            if (kind == SpawnKindCar) cars++;
        }

        return cars >= CarCatalog.Shared.Count;
    }

    /// <summary>The spawn kind the format carries for a car.</summary>
    const byte SpawnKindCar = 1;

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
    public static void NothingIsLaidOnItsWater(string map)
    {
        var plan = Towns.Of(map);
        var grid = new GroundLocator(plan, SimConfig.Shipped());

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

    static void AssertOffTheWater(GroundLocator grid, Vector2 pointM, string what)
    {
        var ground = grid.At(pointM);
        Assert.True(ground.Walkable || ground.Drivable, $"{what} stands at {pointM} on ground permitted to nobody");
    }

    /// <summary>
    /// <b>A map ends at its own edge</b> (GEN-2b). The extent is the whole of the world, so a shape laid past
    /// it stands on ground nothing can classify, nobody can reach and the ground mesh draws over the void —
    /// which is what a sea painted to its own horizon did.
    /// </summary>
    public static void NothingItCarriesStandsOffIt(string map)
    {
        var plan = Towns.Of(map);

        foreach (var (what, rings) in Towns.WaterRingsOf(plan.Water))
        {
            for (var ring = 0; ring < rings.Count; ring++)
            {
                foreach (var pointM in rings.RingOf(ring)) AssertOnTheMap(plan, pointM, $"{map}: {what} {ring}");
            }
        }

        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            AssertOnTheMap(plan, plan.Junctions.CentreM[junction], $"{map}: junction {junction}");
        }

        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var chain = plan.Roads.SegmentsOf(road);
            var lengthM = Spline.TotalLengthM(chain);
            for (var alongM = 0f; alongM <= lengthM; alongM += 1f)
            {
                AssertOnTheMap(plan, Spline.SampleAt(chain, alongM).PositionM, $"{map}: road {road}");
            }
        }

        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            var halfM = plan.Buildings.SizeM[building] * 0.5f;
            AssertOnTheMap(
                plan, plan.Buildings.CentreM[building], $"{map}: building {building}", MathF.Max(halfM.X, halfM.Y));
        }

        for (var space = 0; space < plan.ParkingLots.SpaceCount; space++)
        {
            AssertOnTheMap(plan, plan.ParkingLots.SpacePositionM[space], $"{map}: bay {space}");
        }

        for (var prop = 0; prop < plan.Props.Count; prop++)
        {
            AssertOnTheMap(plan, plan.Props.CentreM[prop], $"{map}: prop {prop}", plan.Props.RadiusM[prop]);
        }

        for (var spawn = 0; spawn < plan.Spawns.Count; spawn++)
        {
            AssertOnTheMap(plan, plan.Spawns.PositionM[spawn], $"{map}: spawn {spawn}");
        }
    }

    /// <summary>
    /// On the map with its own girth on it, which for a point is the point itself. <b>The message is built
    /// only when it is needed</b>: this is asked once a metre of every road of every town, and an
    /// interpolated string per call was most of what the check cost.
    /// </summary>
    static void AssertOnTheMap(CityPlan plan, Vector2 pointM, string what, float reachM = 0f)
    {
        if (pointM.X >= reachM && pointM.Y >= reachM
            && pointM.X <= plan.WorldSizeM.X - reachM && pointM.Y <= plan.WorldSizeM.Y - reachM)
        {
            return;
        }

        Assert.Fail($"{what} stands at {pointM}, off a map of {plan.WorldSizeM}");
    }

    /// <summary>
    /// <b>A city can be driven round</b> (GEN-18): every junction it has is reachable from every lane it
    /// carries, and no lane of it dangles (GEN-18a). <b>Asked of cities alone</b> — a map laid to measure one
    /// thing is deliberately in pieces, and the ends it is made of are what it is for.
    /// </summary>
    public static void ACityCanBeDrivenRound(string map)
    {
        var roads = RoadGraph.Build(Towns.Of(map), SimConfig.Shipped());

        Assert.Null(Drivable.Offence(roads));
        Assert.Null(Drivable.Dangling(roads));
    }
}
