using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>Where the roster stands at the first tick</b>: a person at a door, and — where nobody has a bay of their own —
/// a car in a bay or on a lane (GEN-7), beside the cars already stood on the town's bridges.
/// </summary>
/// <remarks>
/// <para>
/// <b>A town with people in it and car parks stands no car here.</b> Every car there is somebody's own, and which
/// bay it stands in is the bay nearest its owner's door that is still free once the services have their aprons — a
/// question about the parking registry, which is the town's (<c>TownWorld.HandThemTheirCar</c>, PER-29).
/// </para>
/// <para>
/// <b>A car is stood in the middle of a bay of one of the town's own car parks</b>, pointing into it, and
/// the town turns it round to whichever way its driver parks (<c>TownWorld.StandCar</c>, GEN-4j). A yard is
/// a service's (GEN-55) and nobody else's, so none is stood in one. <b>How many is what the map asks, up
/// to the bays there are</b> — spread over the town rather than filling whichever car park was cut first.
/// </para>
/// <para>
/// <b>A town that cut no car park stands its cars on its lanes</b>, one a lane and none within a car's room of a
/// bridge's — the fixture, which asks for no buildings and so is owed no parking, and a city whose roads are its own
/// (GEN-8). Otherwise the town would not move at all, and a picture of a perfect road fabric with no traffic on it
/// passes every static test while making every dynamic reading vacuous at once. Such a car tours (CAR-8).
/// </para>
/// </remarks>
internal static class SpawnStage
{
    public const byte Car = 1;

    /// <summary>Which is also the value an unwritten slot holds, so a person is the kind a spawn is by default.</summary>
    public const byte Person = 0;

    /// <param name="wantPeople">How many people the map asks for (<see cref="Zones.ZoneParam.People"/>).</param>
    /// <param name="wantCars">How many cars it asks for where nobody has a bay of their own (<see cref="Zones.ZoneParam.Cars"/>).</param>
    /// <param name="bridgeCars">The cars already stood on its bridges over its roads, which no car is stood on top of.</param>
    public static CityPlan.SpawnArrays Lay(
        int wantPeople, int wantCars, CityPlan.SpawnArrays bridgeCars, Paving paving, CarParks.Laid carParks,
        CityPlan.BuildingArrays buildings, SimConfig config, ref Rng draw)
    {
        var lanes = paving.Lanes;
        var places = TheBays(lanes, carParks);
        var bays = places.Count > 0;
        if (!bays) places = Clear(TheLanes(lanes, config), bridgeCars, lanes, config);

        var doors = buildings.EntryPointM.Length;
        var people = Math.Min(wantPeople, doors);
        var cars = people > 0 && bays ? 0 : Math.Min(wantCars, places.Count);
        var count = bridgeCars.Count + cars + people;
        var kind = new byte[count];
        var positionM = new Vector2[count];
        var headingRad = new float[count];
        bridgeCars.Kind.CopyTo(kind, 0);
        bridgeCars.PositionM.CopyTo(positionM, 0);
        bridgeCars.HeadingRad.CopyTo(headingRad, 0);

        var taken = bridgeCars.Count;
        foreach (var place in Spread(places.Count, cars, ref draw))
        {
            var on = places[place];
            var at = Spline.SampleAt(lanes.ArcsOf(on), MiddleOfThePlaceM(lanes, on, config));

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
            var (doorM, facingRad) = AtTheDoor(buildings, slot);
            kind[taken] = Person;
            positionM[taken] = doorM;
            headingRad[taken] = facingRad;
            taken++;
        }

        return new CityPlan.SpawnArrays { Kind = kind, PositionM = positionM, HeadingRad = headingRad };
    }

    /// <summary>
    /// <b>A person stood at one way in</b>: at the door, facing out of it — the way a body that has just come through
    /// one is pointing.
    /// </summary>
    public static (Vector2 DoorM, float FacingRad) AtTheDoor(CityPlan.BuildingArrays buildings, int entry)
    {
        var doorM = buildings.EntryPointM[entry];
        return (doorM, Bearing(doorM - buildings.CentreM[BuildingOfEntry(buildings, entry)]));
    }

    /// <summary>
    /// <b>Where along a place's lane a car is stood</b>: the middle of the space for a bay, which is the deepest
    /// bay-length of its lane (<see cref="SimConfig.CarParkBayDepthM"/>), and the middle of the lane otherwise.
    /// </summary>
    public static float MiddleOfThePlaceM(LaneLines lanes, int lane, SimConfig config) =>
        lanes.LaneIsBay[lane]
            ? lanes.LaneLengthM[lane] - (config.CarParkBayLengthM * 0.5f)
            : lanes.LaneLengthM[lane] * 0.5f;

    /// <summary>
    /// <b>Every bay of the town's own car parks, as the lane driven into it</b> (GEN-53): the middle of the
    /// space is on that lane (<see cref="MiddleOfThePlaceM"/>), and its bearing is the way a car nosed in points.
    /// </summary>
    static List<int> TheBays(LaneLines lanes, CarParks.Laid carParks)
    {
        var ordinary = new HashSet<int>();
        for (var park = 0; park < carParks.Count; park++)
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
    public static List<int> TheLanes(LaneLines lanes, SimConfig config)
    {
        var roomM = RoomM(config);
        var standable = new List<int>();
        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            if (lanes.LaneLengthM[lane] < roomM) continue;
            if (lanes.ConnectorAt[lane + 1] == lanes.ConnectorAt[lane]) continue;

            standable.Add(lane);
        }

        return standable;
    }

    /// <summary>The room a car is stood in clear of a lane's ends: its length and a width either side.</summary>
    public static float RoomM(SimConfig config) => config.Car.LengthM + (config.Car.WidthM * 2f);

    /// <summary>
    /// The lanes whose middle no bridge's car stands within a car's room of (<see cref="RoomM"/>), so no car is stood on
    /// top of another.
    /// </summary>
    static List<int> Clear(List<int> lanes, CityPlan.SpawnArrays bridgeCars, LaneLines lines, SimConfig config)
    {
        if (bridgeCars.Count == 0) return lanes;

        var roomM = RoomM(config);
        return lanes.FindAll(lane =>
        {
            var middleM = Spline.SampleAt(lines.ArcsOf(lane), MiddleOfThePlaceM(lines, lane, config)).PositionM;
            foreach (var carM in bridgeCars.PositionM)
            {
                if (Vector2.Distance(carM, middleM) < roomM) return false;
            }

            return true;
        });
    }

    /// <summary>
    /// Which building a way in belongs to: the first whose offsets run past it, searched for — a city's worth of
    /// people stood one a door would otherwise walk every building for each of them.
    /// </summary>
    static int BuildingOfEntry(CityPlan.BuildingArrays buildings, int entry)
    {
        var (low, high) = (0, buildings.Count - 1);
        while (low < high)
        {
            var middle = (low + high) >>> 1;
            if (entry < buildings.EntryOffsets[middle + 1]) high = middle;
            else low = middle + 1;
        }

        return low;
    }

    static float Bearing(Vector2 outwardM) =>
        outwardM.LengthSquared() > 1e-6f ? MathF.Atan2(outwardM.Y, outwardM.X) : 0f;

    /// <summary>
    /// Which of the lanes are stood on: every <c>n</c>th one from a drawn start, so the traffic is spread
    /// over the town rather than filling whichever corner of it was laid first.
    /// </summary>
    static int[] Spread(int have, int want, ref Rng draw)
    {
        if (have <= 0 || want <= 0) return [];

        var from = draw.NextInt(have);
        var step = MathF.Max(1f, have / (float)want);
        var taken = new int[want];
        for (var at = 0; at < want; at++) taken[at] = (from + (int)(at * step)) % have;
        return taken;
    }
}
