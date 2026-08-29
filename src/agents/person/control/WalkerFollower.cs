using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.Agents.Person.Control;

/// <summary>What one tick asks of one walker: where it now faces, what it declared, and what that costs in impulse.</summary>
internal readonly record struct WalkerStep(float HeadingRad, Vector2 DesiredMps, Vector2 ImpulseNs);

/// <summary>
/// The whole of a person's movement model, as a pure function of the pose it is given.
/// </summary>
/// <remarks>
/// <para>
/// <b>Foot friction is the entire acceleration model.</b> A manoeuvre declares one desired velocity
/// for the tick; this asks for the full correction <c>(desired − v) · m</c> and spends no more than
/// <c>grip · m · dt</c> of it. There is no acceleration curve anywhere above it, which is what makes
/// PER-3's "a walker's pace is a cap, never a profile" honest rather than a description.
/// </para>
/// <para>
/// <b>Nothing here touches a body.</b> It takes a pose and returns numbers, so the rules can be
/// checked against a fake walker with no solver in the room — which is the reason this is a function
/// and not a method on the fleet.
/// </para>
/// <para>
/// <b>The follower turns first and steps second</b>, so the velocity declared is along the heading
/// this tick ends at and not the one it began at. Heading is intent and never solver output: rotation
/// is locked, so a walker may turn on the spot at its turn rate regardless of where it is travelling.
/// </para>
/// </remarks>
internal static class WalkerFollower
{
    /// <summary>
    /// One tick of one walker.
    /// </summary>
    /// <param name="aimM">Where it is walking. Its own position means "stand", and so does <paramref name="moving"/> false.</param>
    /// <param name="terrainCoefficient">The ground's own factor, which scales the pace <em>and</em> the grip (TER-2).</param>
    /// <param name="onFeet">False while dead or inside the stumble window, which is the difference between being knocked over and being sent down the road.</param>
    public static WalkerStep Step(
        SimConfig config, float headingRad, Vector2 positionM, Vector2 velocityMps, Vector2 aimM, bool moving,
        float terrainCoefficient, bool onFeet, float massKg, float dtS)
    {
        var toAim = aimM - positionM;
        var heading = toAim.LengthSquared() > 1e-8f
            ? TurnToward(headingRad, MathF.Atan2(toAim.Y, toAim.X), config.Person.TurnRateDegPerS * MathF.PI / 180f * dtS)
            : headingRad;

        var desired = moving
            ? Heading.Unit(heading) * config.Person.WalkSpeedMps * terrainCoefficient
            : Vector2.Zero;

        var gripMps2 = (onFeet ? config.Person.FootGripMps2 : config.PersonSlidingGripMps2) * terrainCoefficient;
        var wanted = (desired - velocityMps) * massKg;
        var affordable = gripMps2 * massKg * dtS;
        var wantedLength = wanted.Length();
        var impulse = wantedLength > affordable ? wanted * (affordable / wantedLength) : wanted;

        return new WalkerStep(heading, desired, impulse);
    }

    /// <summary>The shortest way round, capped at what the turn rate affords this tick.</summary>
    public static float TurnToward(float fromRad, float toRad, float mostRad)
    {
        var difference = Wrap(toRad - fromRad);
        if (difference > mostRad) difference = mostRad;
        else if (difference < -mostRad) difference = -mostRad;

        return Wrap(fromRad + difference);
    }

    /// <summary>Into (−π, π].</summary>
    public static float Wrap(float angleRad)
    {
        const float Tau = MathF.PI * 2f;

        angleRad %= Tau;
        if (angleRad > MathF.PI) angleRad -= Tau;
        else if (angleRad <= -MathF.PI) angleRad += Tau;

        return angleRad;
    }
}
