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
/// <b>Three actions and no others</b> (PER-3): stand, turn on the spot, or walk straight at the aim at its pace.
/// A walker whose aim is off the heading by more than a tick's turn stands and turns; one facing it walks, and
/// the last of the way — anything short of a tick's walk — it steps onto whichever way it faces. It never walks
/// past its aim and never walks at anything it is not facing, so there is no aim it can circle.
/// </para>
/// <para>
/// <b>What it chose is what it gets, and what was done to it is taken back at the grip.</b> The velocity it
/// declares is delivered whole against the one it declared a tick ago, so a walker has no acceleration of its
/// own; the difference between that and the velocity the solver left it with is what a contact did to it, and
/// the feet spend no more than <c>grip · m · dt</c> taking it back. So a walker is shoved and thrown like a
/// body and does not brace against a car, which it would do spending the whole correction every tick.
/// </para>
/// <para>
/// <b>Off its feet nothing is its own</b>: the whole correction is at the sliding grip, which is what a
/// casualty slides to a stop on.
/// </para>
/// <para>
/// <b>The ground is not asked.</b> A walker's pace and grip are its own and the same on every surface: the
/// terrain is a wheel's (TER-2), and a person reads nothing of it.
/// </para>
/// <para>
/// <b>Nothing here touches a body.</b> It takes a pose and returns numbers, so the rules can be
/// checked against a fake walker with no solver in the room — which is the reason this is a function
/// and not a method on the fleet.
/// </para>
/// </remarks>
internal static class WalkerFollower
{
    /// <summary>
    /// <b>A walker's own tick</b> (phase 3), by what the walker is: <see cref="Step"/> asked of a body on its feet at
    /// its aim, of a paused one at where it stands, and of nothing at all for a body inside something or down.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Paused, it holds its stance where it stands</b>, on whatever feet it has, before anything else is asked of
    /// it — the same thing a car with nobody deciding for it is given.
    /// </para>
    /// <para>
    /// <b>Inside a container there is no body in the world to move</b> (PHY-7), so it declares and spends nothing; the
    /// only actions are the container's own, which are the trip's and are taken on the decision clock.
    /// </para>
    /// <para>
    /// <b>A casualty declares nothing</b> (PER-18). What still acts on it is the ground it is lying on, and that is the
    /// body phase's, asked of the body rather than of the walker it was — so its tick writes nothing.
    /// </para>
    /// <para>
    /// <b>The aim is a point along the way in front of it and nothing else</b> (PER-25), no further than the walker
    /// was granted, settled once the grant was read on the claims' clock. Here it is only walked at.
    /// </para>
    /// </remarks>
    /// <param name="aimM">Where it is to get to — <see cref="Body.PersonFleet.DestinationM"/>, the hand's already where one has it.</param>
    /// <param name="walking">Whether it is asked to move at all (<see cref="Body.PersonFleet.Walking"/>).</param>
    /// <param name="paused">Its controller is paused: nobody is deciding for it, and nobody has its hand.</param>
    /// <returns>Whether the tick writes <paramref name="step"/> back — false for a casualty, which it leaves as it lies.</returns>
    public static bool Tick(
        SimConfig config, float headingRad, Vector2 positionM, Vector2 velocityMps, Vector2 declaredMps, Vector2 aimM,
        bool walking, bool wounded, bool inside, bool paused, float massKg, float dtS, out WalkerStep step)
    {
        if (paused)
        {
            step = Step(config, headingRad, positionM, velocityMps, declaredMps, positionM, moving: false, onFeet: !wounded, massKg, dtS);
            return true;
        }

        if (inside)
        {
            step = new WalkerStep(headingRad, Vector2.Zero, Vector2.Zero);
            return true;
        }

        if (wounded)
        {
            step = default;
            return false;
        }

        step = Step(config, headingRad, positionM, velocityMps, declaredMps, aimM, walking, onFeet: true, massKg, dtS);
        return true;
    }

    /// <summary>
    /// One tick of one walker.
    /// </summary>
    /// <param name="declaredMps">What it declared the tick before — its own velocity, as against <paramref name="velocityMps"/>, which is what the solver made of it.</param>
    /// <param name="aimM">Where its body is to get to. Its own position means "stand", and so does <paramref name="moving"/> false.</param>
    /// <param name="onFeet">False while dead or inside the stumble window, which is the difference between being knocked over and being sent down the road.</param>
    public static WalkerStep Step(
        SimConfig config, float headingRad, Vector2 positionM, Vector2 velocityMps, Vector2 declaredMps, Vector2 aimM,
        bool moving, bool onFeet, float massKg, float dtS)
    {
        var heading = headingRad;
        var desired = Vector2.Zero;

        // <b>An aim under the body is a stand</b>, the same as being asked for none: a walker with nowhere
        // to be does not carry on the way it was last pointed until something else stops it.
        var toAim = aimM - positionM;
        var distanceM = toAim.Length();
        if (distanceM * distanceM > config.Person.AimUnderfootM2)
        {
            var paceMps = config.PersonWalkSpeedMps;

            // Inside a tick's walk it is a shuffle onto the point and not a turn: the aim of a queue creeping
            // forward, or of a body a hair off its line, swings round it a centimetre away, and a walker
            // turning to face every one of those spins on the spot.
            if (distanceM <= paceMps * dtS)
            {
                if (moving) desired = toAim / dtS;
            }
            else
            {
                var bearingRad = Trig.Atan2(toAim.Y, toAim.X);
                var mostRad = config.PersonTurnRateDegPerS * MathF.PI / 180f * dtS;
                if (MathF.Abs(Wrap(bearingRad - headingRad)) > mostRad)
                {
                    heading = TurnToward(headingRad, bearingRad, mostRad);
                }
                else
                {
                    heading = bearingRad;
                    if (moving) desired = toAim / distanceM * paceMps;
                }
            }
        }

        var correctionMps = onFeet
            ? desired - declaredMps + AtMost(declaredMps - velocityMps, config.PersonFootGripMps2 * dtS)
            : AtMost(desired - velocityMps, config.PersonSlidingGripMps2 * dtS);

        return new WalkerStep(heading, desired, correctionMps * massKg);
    }

    /// <summary>A change of velocity cut down to what a grip affords in one tick.</summary>
    static Vector2 AtMost(Vector2 changeMps, float mostMps)
    {
        var lengthMps = changeMps.Length();
        return lengthMps > mostMps ? changeMps * (mostMps / lengthMps) : changeMps;
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
    /// <remarks>
    /// The remainder is a call into the C library, and an angle already inside a turn is its own remainder to
    /// the bit — so only one outside it is sent there.
    /// </remarks>
    public static float Wrap(float angleRad)
    {
        const float Tau = MathF.PI * 2f;

        if (!(MathF.Abs(angleRad) < Tau)) angleRad %= Tau;
        if (angleRad > MathF.PI) angleRad -= Tau;
        else if (angleRad <= -MathF.PI) angleRad += Tau;

        return angleRad;
    }
}
