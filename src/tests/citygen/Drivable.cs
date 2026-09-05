using System.Numerics;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>Whether a town can be driven round</b> (GEN-18): from every lane there is, every junction the town
/// has is reachable. <b>Turning round in the road is not a movement</b> (TER-5f), so this is not the
/// undirected connectivity GEN-5 asks for — a block whose one-way streets all ran inwards keeps that one
/// and is still somewhere a car drives into and never leaves.
/// </summary>
/// <remarks>
/// Asked of the lane graph a town is actually driven on, so it answers for the turns the drawn shapes left
/// room for and not for the chords the generator settled its one-way streets against. <b>A node no lane
/// arrives at is not a place the town drives to</b> and is passed over: two junctions whose discs overlap
/// leave the stretch between them with no lane on it at all.
/// </remarks>
internal static class Drivable
{
    /// <summary>What is wrong, or <c>null</c> where every junction can be driven to from every lane.</summary>
    public static string? Offence(RoadGraph roads)
    {
        var into = new List<int>[roads.LaneCount];
        for (var lane = 0; lane < roads.LaneCount; lane++) into[lane] = [];
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            foreach (var onto in roads.LanesFrom(lane)) into[onto].Add(lane);
        }

        var reaches = new int[roads.LaneCount];
        var walked = new Queue<int>();

        for (var junction = 0; junction < roads.JunctionCount; junction++)
        {
            if (roads.LanesIntoJunction(junction).Length == 0) continue;

            // Walked backwards from the lanes arriving at it, so one walk says which lanes reach this
            // junction rather than one walk a lane saying which junctions it reaches.
            var found = 0;
            foreach (var lane in roads.LanesIntoJunction(junction))
            {
                if (reaches[lane] == junction + 1) continue;

                reaches[lane] = junction + 1;
                walked.Enqueue(lane);
                found++;
            }

            while (walked.Count > 0)
            {
                foreach (var earlier in into[walked.Dequeue()])
                {
                    if (reaches[earlier] == junction + 1) continue;

                    reaches[earlier] = junction + 1;
                    walked.Enqueue(earlier);
                    found++;
                }
            }

            if (found < roads.LaneCount)
            {
                return $"{roads.LaneCount - found} of {roads.LaneCount} lanes cannot be driven to junction " +
                       $"{junction} at {AtJunction(roads, junction)}";
            }
        }

        return null;
    }

    /// <summary>
    /// What dangles, or <c>null</c> where every lane is both driven onto and driven off (GEN-18a).
    /// </summary>
    public static string? Dangling(RoadGraph roads)
    {
        var into = new int[roads.LaneCount];
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            foreach (var onto in roads.LanesFrom(lane)) into[onto]++;
        }

        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            if (into[lane] > 0 && roads.LanesFrom(lane).Length > 0) continue;

            var what = into[lane] == 0 ? "is driven onto by nothing" : "is driven off onto nothing";
            return $"lane {lane} of road {roads.LaneRoad[lane]} {what}: from {roads.StartOf(lane).PositionM} "
                   + $"to {roads.EndOf(lane).PositionM}";
        }

        return null;
    }

    /// <summary>Where a junction stands, for a message: the end of any one of the lanes that arrive at it.</summary>
    static Vector2 AtJunction(RoadGraph roads, int junction)
    {
        var arms = roads.LanesIntoJunction(junction);
        return arms.IsEmpty ? Vector2.Zero : roads.EndOf(arms[0]).PositionM;
    }
}
