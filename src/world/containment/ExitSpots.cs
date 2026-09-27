using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Physics;

namespace TrafficSimulation.World.Containment;

/// <summary>
/// <b>PHY-7a, and it is one rule for both container kinds</b>: a person coming out of a container is
/// placed at the nearest unoccupied position on the map within the exit search radius of the way out,
/// and while there is no such position <em>the exit action is unavailable</em>.
/// </summary>
/// <remarks>
/// <para>
/// <b>A container places its occupant; the occupant never places itself.</b> Refused is not a stall
/// and not a failure — it means every position round the door is taken, so the person stays inside and
/// asks again next tick, and the doorway empties as soon as whoever is standing in it walks off.
/// </para>
/// <para>
/// <b>Nearest, by construction rather than by comparison.</b> The rings widen outward from the way out
/// and the first spot that answers all three questions is taken, so nothing is scored and nothing is
/// sorted. The three questions are the rule's own: is it on the map, is anything standing there, and is
/// the body's own footprint clear of the town's furniture.
/// </para>
/// <para>
/// <b>The ground is not asked</b> (TER-2): what a person stands on is nothing to a person, and a spot on
/// the carriageway beside a door is a spot like any other.
/// </para>
/// </remarks>
internal static class ExitSpots
{
    /// <summary>How many places are tried on each ring — every 45°, which at a person's diameter leaves no gap a body would fit in.</summary>
    const int PlacesPerRing = 8;

    /// <summary>
    /// The standing bodies a spot has to be clear of, as the three columns this rule reads and no more.
    /// <b>It is spans rather than the fleet itself</b> so that this slice stays a fact about containers:
    /// what is standing about is the caller's to hand over, and a container never learns what an agent is.
    /// </summary>
    internal readonly ref struct Standing(
        ReadOnlySpan<Vector2> atM, ReadOnlySpan<float> radiusM, ReadOnlySpan<Contained> inside)
    {
        public readonly ReadOnlySpan<Vector2> AtM = atM;
        public readonly ReadOnlySpan<float> RadiusM = radiusM;
        public readonly ReadOnlySpan<Contained> Inside = inside;
    }

    /// <summary>
    /// The nearest place a person may be put down outside <paramref name="wayOutM"/>, or false where
    /// there is none. <paramref name="towardsM"/> is where the ring starts from, so the first spot tried
    /// is the one the container faces.
    /// </summary>
    /// <param name="worldSizeM">The town's own box, which a spot has to be inside.</param>
    public static bool TryFind(
        SimConfig config, Vector2 worldSizeM, PhysicsWorld physics, BucketGrid nearby, Standing standing,
        Vector2 wayOutM, Vector2 towardsM, Span<int> scratch, out Vector2 spotM)
    {
        var bodyM = config.PersonDiameterM;
        var reachM = config.PersonExitSearchRadiusM;
        var firstRad = MathF.Atan2(towardsM.Y - wayOutM.Y, towardsM.X - wayOutM.X);

        for (var ringM = 0f; ringM <= reachM + 1e-3f; ringM += bodyM * 0.5f)
        {
            var places = ringM <= 1e-3f ? 1 : PlacesPerRing;
            for (var place = 0; place < places; place++)
            {
                // The ring is walked outward from the direction handed in and alternately either side of
                // it, so "nearest" is nearest to the way the container faces as well as to the door.
                var turnRad = firstRad + (place % 2 == 0 ? 1f : -1f) * ((place + 1) / 2) * (MathF.Tau / PlacesPerRing);
                var atM = wayOutM + Heading.Unit(turnRad) * ringM;
                if (!IsFree(config, worldSizeM, physics, nearby, standing, atM, scratch)) continue;

                spotM = atM;
                return true;
            }
        }

        spotM = wayOutM;
        return false;
    }

    /// <summary>On the map, nobody standing on it, and nothing immovable inside the body's own footprint.</summary>
    static bool IsFree(
        SimConfig config, Vector2 worldSizeM, PhysicsWorld physics, BucketGrid nearby, Standing standing,
        Vector2 atM, Span<int> scratch)
    {
        if (atM.X < 0f || atM.Y < 0f || atM.X >= worldSizeM.X || atM.Y >= worldSizeM.Y) return false;

        var bodyM = config.PersonDiameterM;
        var half = new Vector2(bodyM * 0.5f);
        if (physics.StaticInBox(atM - half, atM + half)) return false;

        var found = nearby.Query(atM, bodyM, scratch);
        for (var slot = 0; slot < Math.Min(found, scratch.Length); slot++)
        {
            var person = scratch[slot];

            // A contained person is not in the town (PHY-7), and its position is only where its
            // container stands — so it can neither be stood on nor stand in anybody's way.
            if (standing.Inside[person].Any) continue;
            var clearM = (bodyM * 0.5f) + standing.RadiusM[person];
            if ((standing.AtM[person] - atM).LengthSquared() < clearM * clearM)
            {
                return false;
            }
        }

        // A superset that would not fit in the scratch has been read as a subset, and a place judged
        // empty because the list was full is a body put down on top of somebody.
        return found <= scratch.Length;
    }
}
