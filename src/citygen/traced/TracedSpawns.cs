using System.Numerics;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// <b>Who a traced town stands when it is opened</b> (GEN-7, GEN-57): the people its map asks for, one a door, and its
/// cars, one a lane, beside the few its bridges stand (<see cref="TracedBridgeCars"/>) — as many of each as the map asks
/// (<see cref="TracedMap.Population"/>) and the town has room for (GEN-8).
/// </summary>
/// <remarks>
/// <para>
/// <b>Laid by rule and not drawn</b> (GEN-57): every nth door and every nth lane from the first, so the same map stands
/// the same roster every time it is opened. A lane is one a generated town with no car park stands a car on
/// (<see cref="SpawnStage.TheLanes"/>), and the middle of it is where.
/// </para>
/// <para>
/// <b>Its cars are nobody's</b>: a traced town cuts no car park, so nobody living in it has a bay to be handed a car in
/// (PER-29), and every car it stands tours (CAR-8).
/// </para>
/// </remarks>
internal static class TracedSpawns
{
    public static CityPlan.SpawnArrays Lay(
        TracedPopulation population, CityPlan.SpawnArrays bridgeCars, LaneLines lanes, CityPlan.BuildingArrays buildings,
        SimConfig config)
    {
        var standable = Clear(SpawnStage.TheLanes(lanes, config), bridgeCars, lanes, config);
        var cars = SpawnStage.Spread(standable.Count, Math.Min(population.Cars, standable.Count), from: 0);
        var doors = buildings.EntryPointM.Length;
        var people = SpawnStage.Spread(doors, Math.Min(population.People, doors), from: 0);

        var count = bridgeCars.Count + cars.Length + people.Length;
        var kind = new byte[count];
        var positionM = new Vector2[count];
        var headingRad = new float[count];
        bridgeCars.Kind.CopyTo(kind, 0);
        bridgeCars.PositionM.CopyTo(positionM, 0);
        bridgeCars.HeadingRad.CopyTo(headingRad, 0);

        var taken = bridgeCars.Count;
        foreach (var at in cars)
        {
            var lane = standable[at];
            var on = Spline.SampleAt(lanes.ArcsOf(lane), SpawnStage.MiddleOfThePlaceM(lanes, lane, config));
            (kind[taken], positionM[taken], headingRad[taken]) = (SpawnStage.Car, on.PositionM, on.HeadingRad);
            taken++;
        }

        foreach (var entry in people)
        {
            var (doorM, facingRad) = SpawnStage.AtTheDoor(buildings, entry);
            (kind[taken], positionM[taken], headingRad[taken]) = (SpawnStage.Person, doorM, facingRad);
            taken++;
        }

        return new CityPlan.SpawnArrays { Kind = kind, PositionM = positionM, HeadingRad = headingRad };
    }

    /// <summary>
    /// The lanes whose middle no bridge's car stands within a car's room of (<see cref="SpawnStage.RoomM"/>), so no car
    /// is stood on top of another.
    /// </summary>
    static List<int> Clear(List<int> lanes, CityPlan.SpawnArrays bridgeCars, LaneLines lines, SimConfig config)
    {
        var roomM = SpawnStage.RoomM(config);
        return lanes.FindAll(lane =>
        {
            var middleM = Spline.SampleAt(lines.ArcsOf(lane), SpawnStage.MiddleOfThePlaceM(lines, lane, config)).PositionM;
            foreach (var carM in bridgeCars.PositionM)
            {
                if (Vector2.Distance(carM, middleM) < roomM) return false;
            }

            return true;
        });
    }
}
