using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.World.Statics;

/// <summary>How many buildings one service wants, and how far from its own door its vehicles may stand.</summary>
internal readonly record struct ServiceApron(BuildingUse Use, int Wanted, float WithinM);

/// <summary>
/// <b>Which buildings a town's services stand at</b> (GEN-9) — the placement itself, run when a map is
/// authored and written into the file it produces. Nothing calls it while a town is running.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is an authoring step, so it may look at the town.</b> Two things decide a place, and neither is
/// affordable on every load: a service building has to have somewhere for its vehicles to stand, and the
/// services have to be spread over the town rather than shuffled into it. A shuffle costs nothing and
/// puts the only hospital in the corner of the map next door to the police station about as often as it
/// does anything else; a sweep over every pair of buildings costs a fraction of a second, once, and
/// never again.
/// </para>
/// <para>
/// <b>A building with nowhere to park stands nothing</b> (SRV-2), so one is not eligible to be a service
/// building at all: an apron that finds no bay is a hospital with no ambulances, which is a slice of the
/// town doing nothing and no way for anybody watching to tell why.
/// </para>
/// <para>
/// <b>Spread is measured against every service already placed and not against its own kind</b>: a depot
/// across the road from a hospital is the same clump to somebody watching as two hospitals would be.
/// The first is drawn from the world seed, so a map has the same services every time it is authored and
/// different ones from its neighbour.
/// </para>
/// </remarks>
internal static class ServicePlacement
{
    /// <summary>The stream of the world seed the first place is drawn from, which belongs to nothing else.</summary>
    const ulong PlacementStream = 0x53525643;

    /// <summary>
    /// Places every service on the plan, ordinary buildings first: the file's uses are written from
    /// nothing each time, so authoring a map twice from one seed produces the same map.
    /// </summary>
    public static void Place(CityPlan plan, ulong worldSeed, ReadOnlySpan<ServiceApron> aprons)
    {
        var buildings = plan.Buildings;
        Array.Fill(buildings.Use, BuildingUse.Ordinary);
        if (buildings.Count == 0) return;

        var draw = new Rng(worldSeed, PlacementStream);
        var placedM = new List<Vector2>();
        var eligible = new List<int>();

        foreach (var apron in aprons)
        {
            eligible.Clear();
            for (var building = 0; building < buildings.Count; building++)
            {
                if (buildings.Use[building] != BuildingUse.Ordinary) continue;
                if (BaysNear(plan, buildings.CentreM[building], apron.WithinM) == 0) continue;

                eligible.Add(building);
            }

            for (var placed = 0; placed < apron.Wanted && eligible.Count > 0; placed++)
            {
                var slot = placedM.Count == 0 ? draw.NextInt(eligible.Count) : FurthestFrom(buildings, eligible, placedM);
                var building = eligible[slot];
                eligible.RemoveAt(slot);

                buildings.Use[building] = apron.Use;
                placedM.Add(buildings.CentreM[building]);
            }
        }
    }

    /// <summary>
    /// The candidate furthest from the nearest service already placed — the farthest-point traversal,
    /// which is what "spread over the town" is when the count is a dozen and the choices are a thousand.
    /// </summary>
    static int FurthestFrom(CityPlan.BuildingArrays buildings, List<int> eligible, List<Vector2> placedM)
    {
        var best = 0;
        var bestM = -1f;
        for (var slot = 0; slot < eligible.Count; slot++)
        {
            var centreM = buildings.CentreM[eligible[slot]];
            var nearestM = float.MaxValue;
            foreach (var takenM in placedM) nearestM = MathF.Min(nearestM, (takenM - centreM).LengthSquared());

            if (nearestM <= bestM) continue;

            best = slot;
            bestM = nearestM;
        }

        return best;
    }

    /// <summary>
    /// How many of the map's parking spaces are within reach of this building. <b>The plan's spaces and
    /// not the registry's bays</b>: this runs over a plan on its own, before anything has been stood up,
    /// and what it is asked is whether there is anywhere to park at all.
    /// </summary>
    static int BaysNear(CityPlan plan, Vector2 centreM, float withinM)
    {
        var spaces = plan.ParkingLots.SpacePositionM;
        var near = 0;
        foreach (var spaceM in spaces)
        {
            if ((spaceM - centreM).LengthSquared() <= withinM * withinM) near++;
        }

        return near;
    }
}
