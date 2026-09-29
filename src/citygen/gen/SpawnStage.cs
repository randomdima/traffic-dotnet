using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>Where the roster stands at the first tick</b>: a car in a bay, and a person at a door (GEN-7).
/// </summary>
/// <remarks>
/// <para>
/// <b>A car is stood in the middle of a bay of one of the town's own car parks</b>, pointing into it, and
/// the town turns it round to whichever way its driver parks (<c>TownWorld.StandCar</c>, GEN-4j). A yard is
/// a service's (GEN-55) and nobody else's, so none is stood in one. <b>How many is what the brief asks, up
/// to the bays there are</b> — spread over the town rather than filling whichever car park was cut first.
/// </para>
/// <para>
/// <b>A town that cut no car park stands its cars on its lanes</b>, one a lane — the fixture, which asks for
/// no buildings and so is owed no parking (GEN-8). Otherwise the town would not move at all, and a picture
/// of a perfect road fabric with no traffic on it passes every static test while making every dynamic
/// reading vacuous at once. Such a car tours (CAR-8).
/// </para>
/// </remarks>
internal static class SpawnStage
{
    const byte Car = 1;

    /// <summary>Which is also the value an unwritten slot holds, so a person is the kind a spawn is by default.</summary>
    const byte Person = 0;

    public static CityPlan.SpawnArrays Lay(
        TownBrief brief, Paving paving, CarParks.Laid carParks, CityPlan.BuildingArrays buildings,
        SimConfig config, ref Rng draw)
    {
        var lanes = paving.Lanes;
        var places = TheBays(lanes, carParks);
        if (places.Count == 0) places = TheLanes(lanes, config);

        var cars = Math.Min(brief.Cars, places.Count);
        var doors = buildings.EntryPointM.Length;
        var people = Math.Min(brief.People, doors);
        var kind = new byte[cars + people];
        var positionM = new Vector2[cars + people];
        var headingRad = new float[cars + people];

        var taken = 0;
        foreach (var place in Spread(places.Count, cars, ref draw))
        {
            var on = places[place];
            var at = Spline.SampleAt(lanes.ArcsOf(on), lanes.LaneLengthM[on] * 0.5f);

            kind[taken] = Car;
            positionM[taken] = at.PositionM;
            headingRad[taken] = at.HeadingRad;
            taken++;
        }

        // <b>One person a door, spread over the whole town's worth of them</b> (GEN-7). A body stood at a
        // way in is a body inside that building before the first tick, so what the count is bounded by is
        // how many doors the buildings were laid with — and a door already taken would be two people
        // walking out of one doorway onto one another.
        foreach (var slot in Spread(doors, people, ref draw))
        {
            var doorM = buildings.EntryPointM[slot];
            var building = BuildingOfEntry(buildings, slot);

            kind[taken] = Person;
            positionM[taken] = doorM;
            // Facing out of the door it is standing at, which is the way a body that has just come
            // through one is pointing.
            headingRad[taken] = Bearing(doorM - buildings.CentreM[building]);
            taken++;
        }

        return new CityPlan.SpawnArrays { Kind = kind, PositionM = positionM, HeadingRad = headingRad };
    }

    /// <summary>
    /// <b>Every bay of the town's own car parks, as the lane driven into it</b> (GEN-53): the middle of that
    /// lane is the middle of the space, and its bearing is the way a car nosed in points.
    /// </summary>
    static List<int> TheBays(LaneLines lanes, CarParks.Laid carParks)
    {
        var ordinary = new HashSet<int>();
        for (var park = 0; park < carParks.Junction.Length; park++)
        {
            if (carParks.For[park] != BuildingUse.Ordinary) continue;

            for (var bay = carParks.BayOffsets[park]; bay < carParks.BayOffsets[park + 1]; bay++)
            {
                ordinary.Add(carParks.Road[bay]);
            }
        }

        var bays = new List<int>();
        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            if (lanes.LaneForward[lane] && ordinary.Contains(lanes.LaneRoad[lane])) bays.Add(lane);
        }

        return bays;
    }

    /// <summary>
    /// <b>The lanes a car can be stood on where a town has no bay</b>: long enough to stand one clear of
    /// both ends — a body over a lane's own end is in the box beyond it before the town has ticked once —
    /// and with a movement off the end, or the car is a body with nowhere to go.
    /// </summary>
    static List<int> TheLanes(LaneLines lanes, SimConfig config)
    {
        var roomM = config.Car.LengthM + (config.Car.WidthM * 2f);
        var standable = new List<int>();
        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            if (lanes.LaneLengthM[lane] < roomM) continue;
            if (lanes.ConnectorAt[lane + 1] == lanes.ConnectorAt[lane]) continue;

            standable.Add(lane);
        }

        return standable;
    }

    /// <summary>Which building a way in belongs to, walked from the offsets that index them.</summary>
    static int BuildingOfEntry(CityPlan.BuildingArrays buildings, int entry)
    {
        for (var building = 0; building < buildings.Count; building++)
        {
            if (entry < buildings.EntryOffsets[building + 1]) return building;
        }

        return buildings.Count - 1;
    }

    static float Bearing(Vector2 outwardM) =>
        outwardM.LengthSquared() > 1e-6f ? MathF.Atan2(outwardM.Y, outwardM.X) : 0f;

    /// <summary>
    /// Which of the lanes are stood on: every <c>n</c>th one from a drawn start, so the traffic is spread
    /// over the town rather than filling whichever corner of it was laid first.
    /// </summary>
    static IEnumerable<int> Spread(int have, int want, ref Rng draw)
    {
        if (have <= 0 || want <= 0) return [];

        var step = MathF.Max(1f, have / (float)want);
        var from = draw.NextInt(have);
        var taken = new int[want];
        for (var at = 0; at < want; at++) taken[at] = (from + (int)(at * step)) % have;
        return taken;
    }
}
