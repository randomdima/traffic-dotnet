
using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.World.Physics;

/// <summary>
/// What the arbiter needs of whatever holds the bodies: what a tag is to the arithmetic, how fast it
/// came into the tick, and where to put the outcome.
/// </summary>
/// <remarks>
/// It exists so the town and a crash rig with no town in it are arbitrated by the <em>same</em> code —
/// which is what "one component owns this arithmetic" has to mean if a staged case is to prove anything
/// about the running town.
/// </remarks>
internal interface IDamageRoster
{
    DamageSubject SubjectOf(BodyTag tag);

    void Apply(BodyTag tag, DamageOutcome outcome);
}

/// <summary>A roster whose solver is stepped in front of it, so the motion each body carried into the step is its to say.</summary>
internal interface IStepRoster : IDamageRoster
{
    /// <summary>The velocity this body carried <em>into</em> the tick, which is the cause; what the step left it with is the response.</summary>
    Vector2 VelocityIntoTickMps(BodyTag tag);
}

/// <summary>
/// Phase 5: every pair that began touching in the step just taken, judged once, and the outcomes handed
/// back to the roster.
/// </summary>
/// <remarks>
/// The solver's begin-touch event <em>is</em> the pair table: a contact list re-read every tick would
/// judge a queue sixty times a second, and keeping a second table beside the solver's would be two
/// answers to one question. What no solver can supply is the speed — by the time a contact is reported
/// the step has already answered it, and the velocities in the bodies are the response. The cause is
/// the pair's motion carried into the tick, which is why the roster is asked for that and not the body.
/// </remarks>
internal static class ContactArbiter
{
    /// <param name="roster">
    /// A type parameter and never a field of interface type, so the calls below are direct: this runs
    /// inside a tick, and an allocation-free tick does not survive a dispatch the JIT cannot see through.
    /// </param>
    /// <returns>How many pairs were judged, which is the count of touches that began this tick.</returns>
    public static int Resolve<TRoster>(PhysicsWorld physics, SimConfig config, TRoster roster)
        where TRoster : IStepRoster
    {
        var judged = 0;
        foreach (var touch in physics.BeganTouchingThisStep())
        {
            var into = touch.Into ?? Approach.Of(
                roster.VelocityIntoTickMps(touch.First), roster.VelocityIntoTickMps(touch.Second), touch.Normal);
            if (Judge(touch, into, config, roster)) judged++;
        }

        return judged;
    }

    /// <summary>The touches a world stepped elsewhere began, each carrying how its bodies came into it (<see cref="Touch.Into"/>).</summary>
    public static int Resolve<TRoster>(ReadOnlySpan<Touch> touches, SimConfig config, TRoster roster)
        where TRoster : IDamageRoster
    {
        var judged = 0;
        foreach (ref readonly var touch in touches)
        {
            if (Judge(touch, touch.Into ?? default, config, roster)) judged++;
        }

        return judged;
    }

    static bool Judge<TRoster>(in Touch touch, in Approach into, SimConfig config, TRoster roster)
        where TRoster : IDamageRoster
    {
        var first = roster.SubjectOf(touch.First);
        var second = roster.SubjectOf(touch.Second);
        if (first.Kind == Participant.Static && second.Kind == Participant.Static) return false;

        var verdict = DamageResolver.Resolve(config, first, second, into);
        if (verdict.ToFirst != DamageOutcome.None) roster.Apply(touch.First, verdict.ToFirst);
        if (verdict.ToSecond != DamageOutcome.None) roster.Apply(touch.Second, verdict.ToSecond);
        return true;
    }
}
