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
    /// <summary>Which pair meets, or <c>null</c> where no junction has two one-way streets at it.</summary>
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
                var met = atJunction[junction];
                if (met >= 0 && met != road)
                {
                    return $"one-way roads {met} and {road} meet at junction {junction}, "
                           + $"at {plan.Junctions.CentreM[junction]}";
                }

                atJunction[junction] = road;
            }
        }

        return null;
    }

    /// <summary>
    /// Which pair stands too near, or <c>null</c> where every two of them are the spacing apart. <b>Measured
    /// between the middles of the chords they are laid on.</b>
    /// </summary>
    public static string? Crowding(CityPlan plan, float apartM)
    {
        var circulating = Circulating(plan);

        var endsM = new Dictionary<int, Vector2>();
        var ends = new Dictionary<int, int>();
        for (var street = 0; street < plan.Roads.Count; street++)
        {
            if (plan.Roads.Flow[street] == RoadFlow.BothWays || circulating[street]) continue;

            foreach (var junction in (ReadOnlySpan<int>)
                     [plan.Roads.FromJunction[street], plan.Roads.ToJunction[street]])
            {
                endsM[street] = endsM.GetValueOrDefault(street) + plan.Junctions.CentreM[junction];
                ends[street] = ends.GetValueOrDefault(street) + 1;
            }
        }

        var middleM = new List<Vector2>();
        var street0 = new List<int>();
        // In the streets' own order, so a town that breaks this names the same pair every time it is asked.
        foreach (var street in endsM.Keys.Order())
        {
            var atM = endsM[street] / ends[street];
            for (var earlier = 0; earlier < middleM.Count; earlier++)
            {
                var apartHereM = Vector2.Distance(middleM[earlier], atM);
                if (apartHereM < apartM)
                {
                    return $"one-way streets {street0[earlier]} and {street} stand {apartHereM:F1} m apart, "
                           + $"inside the {apartM:F1} m that scatters them";
                }
            }

            middleM.Add(atM);
            street0.Add(street);
        }

        return null;
    }

    /// <summary>
    /// <b>Which one-way street arrives somewhere it takes the last choice away</b>, or <c>null</c> where every
    /// one of them ends at a junction of four arms or more (GEN-18).
    /// </summary>
    /// <remarks>
    /// A street arriving at a node takes one way out away from every approach there, and a three-armed node
    /// has only one left to give — so an approach would be driven through a junction it decides nothing at.
    /// <b>Asked of the arrival end alone</b>: a street leaving takes nothing away from anybody.
    /// </remarks>
    public static string? Starving(CityPlan plan)
    {
        var arms = plan.Ground.ArmsPerJunction();
        var circulating = Circulating(plan);

        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var flow = plan.Roads.Flow[road];
            if (flow == RoadFlow.BothWays || circulating[road]) continue;

            var arrivesAt = flow == RoadFlow.WithTheRoad
                ? plan.Roads.ToJunction[road]
                : plan.Roads.FromJunction[road];

            if (arms[arrivesAt] < 4)
            {
                return $"one-way road {road} arrives at junction {arrivesAt} of {arms[arrivesAt]} arms, "
                       + $"at {plan.Junctions.CentreM[arrivesAt]}";
            }
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
