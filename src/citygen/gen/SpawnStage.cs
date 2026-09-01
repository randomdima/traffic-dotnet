using System.Numerics;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>Where the roster stands at the first tick</b>: a car in a bay and a person at the door of the
/// building it lives in (GEN-7).
/// </summary>
/// <remarks>
/// <b>Both are places an earlier stage has already made legal.</b> A bay was laid inside a car park's own
/// rectangle and a door on the pavement outside a building, so nothing here has to test the ground — which
/// is the whole shape of this generator: the stage that places something is the stage that knows it fits.
/// <para>
/// <b>A count the town cannot afford is clamped rather than retried</b>: a brief asking for more cars than
/// the frontages left bays for stands as many as there are bays, and the census is what says so.
/// </para>
/// </remarks>
internal static class SpawnStage
{
    const byte Person = 0;

    const byte Car = 1;

    public static CityPlan.SpawnArrays Lay(
        TownBrief brief, CityPlan.BuildingArrays buildings, CityPlan.ParkingLotArrays lots, ref Rng draw)
    {
        var kind = new List<byte>();
        var positionM = new List<Vector2>();
        var headingRad = new List<float>();

        var cars = Math.Min(brief.Cars, lots.SpaceCount);
        foreach (var bay in Spread(lots.SpaceCount, cars, ref draw))
        {
            kind.Add(Car);
            positionM.Add(lots.SpacePositionM[bay]);
            headingRad.Add(lots.SpaceHeadingRad[bay]);
        }

        var people = Math.Min(brief.People, buildings.Count * MostPerBuilding(buildings));
        for (var person = 0; person < people; person++)
        {
            // Round the buildings rather than drawn per person, so a town's people are spread over its
            // doors instead of piling up behind whichever ones the draw happened to favour.
            var building = person % buildings.Count;
            var doorM = buildings.EntryPointM[buildings.EntryOffsets[building]];
            kind.Add(Person);
            positionM.Add(doorM);
            headingRad.Add(RoadStage.Facing(Vector2.Normalize(buildings.CentreM[building] - doorM)));
        }

        return new CityPlan.SpawnArrays
        {
            Kind = [.. kind], PositionM = [.. positionM], HeadingRad = [.. headingRad],
        };
    }

    static int MostPerBuilding(CityPlan.BuildingArrays buildings) =>
        buildings.Count == 0 ? 0 : buildings.Capacity[0];

    /// <summary>
    /// Which of the bays are taken: every <c>n</c>th one from a drawn start, so the parked cars are spread
    /// over the town's car parks rather than filling the first of them.
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
