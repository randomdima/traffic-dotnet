using System.Numerics;
using TrafficSimulation.CityGen;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>Where a town's one-way streets stand</b> (GEN-18): never two of them at one junction, and never two
/// of them nearer than the spacing that scatters them over the town.
/// </summary>
/// <remarks>
/// <para>
/// Asked of the roads and their junctions rather than of the lanes, because it is a question about which
/// streets were chosen and not about what the drawn shapes left room for — a one-way street is measured on
/// the chord between the two junctions it joins, which is the same chord the choice was made on.
/// </para>
/// <para>
/// <b>A roundabout's ring is one one-way road and not several</b> (GEN-19): the whole of a circle is one
/// direction laid at one place, so its own pieces are not two of them meeting and are not two of them
/// crowding each other — but a street the scatter took at one of its nodes still is.
/// </para>
/// </remarks>
internal static class OneWays
{
    /// <summary>Which pair meets, or <c>null</c> where no junction has two one-way roads at it.</summary>
    public static string? Meeting(CityPlan plan)
    {
        var atJunction = new int[plan.Junctions.Count];
        Array.Fill(atJunction, -1);

        var circulating = Circulating(plan);
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            if (!circulating[road]) continue;

            atJunction[plan.Roads.FromJunction[road]] = road;
            atJunction[plan.Roads.ToJunction[road]] = road;
        }

        for (var road = 0; road < plan.Roads.Count; road++)
        {
            if (plan.Roads.Flow[road] == RoadFlow.BothWays || circulating[road]) continue;

            foreach (var junction in (ReadOnlySpan<int>)
                     [plan.Roads.FromJunction[road], plan.Roads.ToJunction[road]])
            {
                if (atJunction[junction] >= 0)
                {
                    return $"one-way roads {atJunction[junction]} and {road} meet at junction {junction}, "
                           + $"at {plan.Junctions.CentreM[junction]}";
                }

                atJunction[junction] = road;
            }
        }

        return null;
    }

    /// <summary>Which pair stands too near, or <c>null</c> where every two of them are the spacing apart.</summary>
    public static string? Crowding(CityPlan plan, float apartM)
    {
        var middleM = new List<Vector2>();
        var road = new List<int>();
        var circulating = Circulating(plan);

        for (var at = 0; at < plan.Roads.Count; at++)
        {
            if (plan.Roads.Flow[at] == RoadFlow.BothWays || circulating[at]) continue;

            var atM = (plan.Junctions.CentreM[plan.Roads.FromJunction[at]]
                       + plan.Junctions.CentreM[plan.Roads.ToJunction[at]]) * 0.5f;

            for (var earlier = 0; earlier < middleM.Count; earlier++)
            {
                var apartHereM = Vector2.Distance(middleM[earlier], atM);
                if (apartHereM < apartM)
                {
                    return $"one-way roads {road[earlier]} and {at} stand {apartHereM:F1} m apart, "
                           + $"inside the {apartM:F1} m that scatters them";
                }
            }

            middleM.Add(atM);
            road.Add(at);
        }

        return null;
    }

    /// <summary>Which of the town's roads are a roundabout's circulating carriageway (GEN-19).</summary>
    public static bool[] Circulating(CityPlan plan)
    {
        var found = new bool[plan.Roads.Count];
        for (var ring = 0; ring < plan.Roundabouts.Count; ring++)
        {
            foreach (var road in plan.Roundabouts.RoadsOf(ring)) found[road] = true;
        }

        return found;
    }
}
