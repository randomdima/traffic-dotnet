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
    /// <summary>
    /// A junction is where roads meet, so a junction no road is an arm of is not one — and it stands a box round it,
    /// but for the end of a road running off the map, which is no box (<see cref="CityPlan.JunctionArrays.RunsOffTheMap"/>).
    /// </summary>
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
            Assert.True(plan.Junctions.RadiusM[junction] > 0f || plan.Junctions.RunsOff(junction), $"{map}: junction {junction} has no reach");
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

        // <b>Somewhere to park is a car park and not a space</b> (GEN-53): a car park is the junction its
        // bays are the arms of, and the spaces on them are one of the known gaps
        // ([docs/index.md](../../../docs/index.md#known-gaps)). What is asked is what a town with buildings
        // on it owes — that somewhere was cut to park at — and it reads the bays the day they land.
        Assert.True(plan.CarParks.Road.Length > 0, $"{map} has buildings and nowhere to park");
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

    /// <summary>
    /// <b>Nothing a town lays stands in its water but a deck</b> (GEN-5, GEN-14, GEN-14a): no road off a bridge's span, no
    /// junction, no track, no car park, no paved slab, no building and no prop. Asked of the water itself
    /// (<see cref="GroundShapes.Wet"/>), because the ground answers what is on top and a road laid into the river is road
    /// there. A road and a track are asked along their middle and both edges, a building round its footprint.
    /// </summary>
    public static void NothingStandsInItsWater(string map)
    {
        var plan = Towns.Of(map);
        var config = SimConfig.Shipped();
        var ground = new GroundShapes(plan.Paving(config), config);
        var wet = new Dictionary<string, (int Count, Vector2 FirstM)>();
        void Asked(string what, Vector2 atM)
        {
            if (!ground.Wet(atM)) return;

            wet[what] = wet.TryGetValue(what, out var seen) ? (seen.Count + 1, seen.FirstM) : (1, atM);
        }

        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var chain = plan.Roads.SegmentsOf(road);
            var lengthM = Spline.TotalLengthM(chain);
            var halfM = plan.Roads.WidthM[road] * 0.5f;
            for (var alongM = 0f; alongM <= lengthM; alongM += WaterStepM)
            {
                if (OnADeck(plan, road, alongM)) continue;

                var sample = Spline.SampleAt(chain, alongM);
                var acrossM = Heading.RightOf(Heading.Unit(sample.HeadingRad)) * halfM;
                foreach (var atM in (ReadOnlySpan<Vector2>)[sample.PositionM, sample.PositionM + acrossM, sample.PositionM - acrossM])
                {
                    Asked(plan.Roads.IsABay(road) ? "bay" : "road", atM);
                }
            }
        }

        for (var junction = 0; junction < plan.Junctions.Count; junction++) Asked("junction", plan.Junctions.CentreM[junction]);

        for (var track = 0; track < plan.Rails.Count; track++)
        {
            var line = plan.Rails.TrackOf(track);
            var levels = plan.Rails.Level.AsSpan(plan.Rails.PointOffsets[track], line.Length);
            for (var point = 0; point + 1 < line.Length; point++)
            {
                if (levels[point] != 0) continue;

                var runM = Vector2.Distance(line[point], line[point + 1]);
                for (var alongM = 0f; alongM < runM; alongM += WaterStepM) Asked("track", Blend.Lerp(line[point], line[point + 1], alongM / runM));
            }
        }

        for (var lot = 0; lot < plan.ParkingLots.Count; lot++)
        {
            var (centreM, axis, halfM) = (plan.ParkingLots.CentreM[lot], plan.ParkingLots.Axis[lot], plan.ParkingLots.HalfExtentM[lot]);
            foreach (var cornerM in Corners(centreM, axis, halfM)) Asked("car park", cornerM);
        }

        for (var slab = 0; slab < plan.PavedAreas.Count; slab++)
        {
            var (minM, sizeM) = (plan.PavedAreas.MinM[slab], plan.PavedAreas.SizeM[slab]);
            foreach (var cornerM in (ReadOnlySpan<Vector2>)[minM, minM + sizeM, minM with { X = minM.X + sizeM.X }, minM with { Y = minM.Y + sizeM.Y }])
            {
                Asked("slab", cornerM);
            }
        }

        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            var axis = Heading.Unit(plan.Buildings.HeadingRad[building]);
            foreach (var cornerM in Corners(plan.Buildings.CentreM[building], axis, plan.Buildings.SizeM[building] * 0.5f)) Asked("building", cornerM);
        }

        for (var prop = 0; prop < plan.Props.Count; prop++) Asked("prop", plan.Props.CentreM[prop]);

        Assert.True(wet.Count == 0, $"{map} stands in its water: " + string.Join("; ", wet.Select(kind => $"{kind.Value.Count} {kind.Key} places, the first at {kind.Value.FirstM}")));
    }

    /// <summary>How finely a road or a track is walked for the water under it.</summary>
    const float WaterStepM = 1f;

    static bool OnADeck(CityPlan plan, int road, float alongM)
    {
        for (var bridge = 0; bridge < plan.Bridges.Count; bridge++)
        {
            if (plan.Bridges.Road[bridge] == road && alongM >= plan.Bridges.FromM[bridge] && alongM <= plan.Bridges.ToM[bridge]) return true;
        }

        return false;
    }

    static Vector2[] Corners(Vector2 centreM, Vector2 axis, Vector2 halfM)
    {
        var across = Heading.RightOf(axis);
        return
        [
            centreM + (axis * halfM.X) + (across * halfM.Y), centreM + (axis * halfM.X) - (across * halfM.Y),
            centreM - (axis * halfM.X) - (across * halfM.Y), centreM - (axis * halfM.X) + (across * halfM.Y),
        ];
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

        // A building is a box on its own bearing, so what stands on the map or off it is its four corners.
        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            var halfM = plan.Buildings.SizeM[building] * 0.5f;
            var along = Heading.Unit(plan.Buildings.HeadingRad[building]) * halfM.X;
            var across = Heading.RightOf(Heading.Unit(plan.Buildings.HeadingRad[building])) * halfM.Y;
            foreach (var corner in (ReadOnlySpan<Vector2>)[along + across, along - across, -along + across, -along - across])
            {
                AssertOnTheMap(plan, plan.Buildings.CentreM[building] + corner, $"{map}: building {building}");
            }
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
    /// carries, and no lane of it dangles (GEN-50). <b>Asked of cities alone</b> — a map laid to measure one
    /// thing is deliberately in pieces, and the ends it is made of are what it is for.
    /// </summary>
    public static void ACityCanBeDrivenRound(string map)
    {
        var plan = Towns.Of(map);
        var roads = RoadGraph.Build(plan, SimConfig.Shipped());

        // <b>Except the lanes joined to nothing</b> — a car park's bays (GEN-53) and a road's roadsides (GEN-57)
        // (<see cref="Drivable.JoinedToNothing"/>) — and a road's that runs off the map (GEN-2b).
        var passedOver = Drivable.OffTheMap(plan, roads, Drivable.JoinedToNothing(plan, roads));

        Assert.Null(Drivable.Offence(roads, passedOver));
        Assert.Null(Drivable.Dangling(roads, passedOver));
    }

    /// <summary>
    /// <b>The boundary of a union of closed bands is closed</b>, so every stretch the merge keeps is in a
    /// ring and none is left with two ends (<see cref="BandShell.Loose"/>). A run that does not shut is a
    /// crossing the merge did not find, and it is a length of the town's edge nothing accounts for.
    /// </summary>
    /// <remarks>
    /// <b>Asked of a whole town because that is the only size the question has.</b> What breaks it is one
    /// arrangement of bands out of a hundred thousand — a junction whose movements fan off a lane end leaving
    /// a piece of ribbon a few centimetres long — so a named place on a fixture map cannot be the sample, and
    /// a town that happens not to hold one says nothing either way. The reading is the whole boundary or it
    /// is nothing.
    /// </remarks>
    public static void ItsBoundaryCloses(string map)
    {
        var config = SimConfig.Shipped();
        var shell = Towns.Of(map).Paving(config).Perimeter(config);
        var lengthM = 0f;
        foreach (var run in shell.Loose) lengthM += Spline.TotalLengthM(run);

        Assert.True(
            shell.Loose.Length == 0,
            $"{map} left {shell.Loose.Length} runs of boundary open, {lengthM:F3} m in all, the first of "
            + $"them from {(shell.Loose.Length > 0 ? shell.Loose[0][0].StartM : Vector2.Zero)}");
    }
}
