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
/// sorted. The three questions are the rule's own: is it on the map, does anybody have it, and is the
/// body's own footprint clear of the town's furniture.
/// </para>
/// <para>
/// <b>Whether anybody has it is the town's to answer</b> (<see cref="IStandingGround"/>), which it does off its
/// reservations: somebody standing there, or ground somebody can no longer stop short of. So a person is put
/// down neither on top of a walker nor in front of a car that could not stop for them, and this slice never
/// learns what an agent is.
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
    /// The nearest place a person may be put down outside <paramref name="wayOutM"/>, or false where
    /// there is none. <paramref name="towardsM"/> is where the ring starts from, so the first spot tried
    /// is the one the container faces.
    /// </summary>
    /// <param name="worldSizeM">The town's own box, which a spot has to be inside.</param>
    public static bool TryFind<TGround>(
        SimConfig config, Vector2 worldSizeM, PhysicsWorld physics, in TGround ground, Vector2 wayOutM,
        Vector2 towardsM, out Vector2 spotM)
        where TGround : struct, IStandingGround
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
                if (!IsFree(config, worldSizeM, physics, ground, atM)) continue;

                spotM = atM;
                return true;
            }
        }

        spotM = wayOutM;
        return false;
    }

    /// <summary>On the map, nobody having it, and nothing immovable inside the body's own footprint.</summary>
    static bool IsFree<TGround>(SimConfig config, Vector2 worldSizeM, PhysicsWorld physics, in TGround ground, Vector2 atM)
        where TGround : struct, IStandingGround
    {
        if (atM.X < 0f || atM.Y < 0f || atM.X >= worldSizeM.X || atM.Y >= worldSizeM.Y) return false;

        var halfM = config.PersonDiameterM * 0.5f;
        var half = new Vector2(halfM);
        return !physics.StaticInBox(atM - half, atM + half) && !ground.IsTaken(atM, halfM);
    }
}

/// <summary>
/// <b>What a place to put a body down is asked of the town round it</b> (PHY-7a): whether anybody has the ground
/// a disc there would stand on. The town answers it; a container never learns who.
/// </summary>
internal interface IStandingGround
{
    bool IsTaken(Vector2 atM, float radiusM);
}
