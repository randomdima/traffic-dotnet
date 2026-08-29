using TrafficSimulation.CityGen;

namespace TrafficSimulation.World.Statics;

/// <summary>
/// <b>Which of a town's buildings serve some use</b> — the hospitals a casualty is delivered to (AMB-1),
/// the police stations and the depots a service vehicle waits at (SRV-1). A roster is read off the map
/// when it is opened and is never a decision anything takes at run time.
/// </summary>
/// <remarks>
/// <para>
/// <b>Authored rather than drawn</b> (GEN-9). Which building is the hospital is a fact about the town it
/// stands in — it is the one on a corner with the parking outside it — and a shuffle taken at load could
/// only ever be told which buildings <em>exist</em>. The placement itself is
/// <see cref="ServicePlacement"/>, run when a map is authored; what is here is the answer read back.
/// </para>
/// <para>
/// <b>A building serves one use at most</b> (SRV-1), which the file settles rather than the order the
/// rosters are read in: one byte a building cannot say two things.
/// </para>
/// </remarks>
internal sealed class BuildingRoster
{
    readonly int[] _buildings;

    BuildingRoster(int[] buildings) => _buildings = buildings;

    /// <summary>The roster a map with none of this use has, so nothing has to hold a null to say so.</summary>
    public static BuildingRoster Empty { get; } = new([]);

    /// <summary>The buildings on this roster, in ascending order and each of them once.</summary>
    public ReadOnlySpan<int> Buildings => _buildings;

    public int Count => _buildings.Length;

    /// <summary>The building the entry at this index is.</summary>
    public int BuildingOf(int entry) => _buildings[entry];

    /// <summary>Whether this building is one of them — a walk of a handful of numbers, asked once a delivery.</summary>
    public bool Holds(int building) => Array.IndexOf(_buildings, building) >= 0;

    /// <summary>The buildings this map declares for one use, in the plan's own order.</summary>
    public static BuildingRoster Of(CityPlan plan, BuildingUse use)
    {
        var found = 0;
        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            if (plan.Buildings.Use[building] == use) found++;
        }

        if (found == 0) return Empty;

        var buildings = new int[found];
        var slot = 0;
        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            if (plan.Buildings.Use[building] == use) buildings[slot++] = building;
        }

        return new BuildingRoster(buildings);
    }

    /// <summary>
    /// How many of a use a plan is worth, without placing them. <b>The fleets are laid before the town
    /// is</b> and a service vehicle is a car and a crew apiece, so the count has to be answerable from
    /// the plan alone.
    /// </summary>
    public static int CountIn(CityPlan plan, float perBuilding, int most)
    {
        if (plan.Buildings.Count == 0) return 0;

        var wanted = (int)MathF.Round(plan.Buildings.Count * perBuilding);
        return Math.Clamp(wanted, 1, Math.Min(most, plan.Buildings.Count));
    }
}
