using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.Agents.Person.Control;

/// <summary>
/// Where a walker goes when nothing has told it. A destination is drawn from the walker's own stream,
/// on ground it is permitted to stand on, and it walks at it in a straight line.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is not PER-9 and does not pretend to be.</b> PER-9's walk is building to building over a
/// route, and both halves of that — the buildings as containers and the foot graph the route is
/// searched over — arrive later. What this is, is the smallest thing that puts a body on the ground
/// and makes what the ground does to it visible: it walks, the terrain scales its pace, it collides
/// with what is in the way and stops, and when it cannot make progress it goes somewhere else.
/// </para>
/// <para>
/// <b>It names no terrain type</b> (TER-2a): a destination has to be <em>walkable</em>, and is
/// preferred where the ground declares itself preferred, so the day the town gains a boardwalk this
/// keeps working.
/// </para>
/// </remarks>
internal static class Wander
{
    /// <summary>
    /// A guard on a loop rather than a figure the simulation is parameterised by: a draw that finds
    /// nowhere is simply redrawn on the next decision, so all this bounds is how much of one tick a
    /// walker may spend looking.
    /// </summary>
    const int MostAttempts = 32;

    /// <summary>
    /// A walkable point somewhere in the town, preferring ground that declares itself preferred.
    /// False means this walker found nowhere to go this time and should stand.
    /// </summary>
    public static bool DrawDestination(CityPlan plan, TerrainGrid terrain, ref Rng draw, out Vector2 destinationM)
    {
        var fallback = Vector2.Zero;
        var haveFallback = false;

        for (var attempt = 0; attempt < MostAttempts; attempt++)
        {
            var point = new Vector2(draw.NextFloat(0f, plan.WorldSizeM.X), draw.NextFloat(0f, plan.WorldSizeM.Y));
            var ground = terrain.At(point);
            if (!ground.Walkable) continue;
            if (ground.Preferred)
            {
                destinationM = point;
                return true;
            }

            if (haveFallback) continue;

            fallback = point;
            haveFallback = true;
        }

        destinationM = fallback;
        return haveFallback;
    }
}
