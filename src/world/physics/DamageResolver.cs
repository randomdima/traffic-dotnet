using TrafficSimulation.Core.Config;

namespace TrafficSimulation.World.Physics;

/// <summary>What a body is to the damage arithmetic: the tolerances it carries, and nothing else.</summary>
internal enum Participant : byte
{
    /// <summary>Buildings, props and furniture. Immovable, unbreakable, and never affected by anything.</summary>
    Static,

    Person,
    Car,
}

/// <summary>
/// What a contact did to one participant. Damage is binary and one tolerance decides it, whichever kind
/// of body it is: a car is intact or broken, a person is on their feet or down in the road.
/// </summary>
internal enum DamageOutcome : byte
{
    None,

    /// <summary>Struck by a vehicle hard enough to put them down the road (PER-23): off their feet, and a casualty until an ambulance has been.</summary>
    Wounded,

    Broken,
}

/// <summary>One side of a contact, as the arithmetic sees it.</summary>
/// <remarks>
/// <para>
/// A static body's mass is infinite rather than large: that is what makes the reduced mass of a car
/// against a wall the car's own mass, with no case in the formula and no figure anybody chose.
/// </para>
/// <para>
/// <see cref="Unbreakable"/> is not <see cref="Spent"/> reached early (PHY-4b). A spent body is already
/// down and contributes nothing to the other side; an unbreakable one is a full participant that the
/// outcome is simply never written for.
/// </para>
/// </remarks>
/// <param name="Spent">
/// Whether this body is already what a contact could make of it — a wreck, or somebody already lying in
/// the road (PHY-5a). It is not the same as being <em>terminal</em>: a casualty gets up again once a
/// hospital has had them (PER-18), and until then a car passing over them breaks on nothing.
/// </param>
internal readonly record struct DamageSubject(Participant Kind, float MassKg, bool Spent, bool Unbreakable = false)
{
    public static DamageSubject Static => new(Participant.Static, float.PositiveInfinity, Spent: false);

    public static DamageSubject Person(float massKg, bool down) => new(Participant.Person, massKg, down);

    public static DamageSubject Car(float massKg, bool broken, bool unbreakable = false) =>
        new(Participant.Car, massKg, broken, unbreakable);
}

/// <summary>What one contact did to both of its participants, and the energy that decided it.</summary>
internal readonly record struct DamageVerdict(DamageOutcome ToFirst, DamageOutcome ToSecond, float EnergyKj);

/// <summary>
/// The only place damage is decided: half the pair's reduced mass times the square of their closing
/// speed along the contact normal, measured against each participant's own tolerance.
/// </summary>
/// <remarks>
/// One energy per contact and one tolerance per kind of body covers every pairing without a special
/// case — the speed that puts a pedestrian in the road barely marks the car, because the pedestrian
/// weighs a seventeenth of it. Nothing here touches a body, a roster or a solver: two masses, two kinds
/// and a closing speed in, two outcomes out, so every ordered pair against every band is checkable as
/// arithmetic.
/// </remarks>
internal static class DamageResolver
{
    /// <summary>
    /// <c>m₁m₂/(m₁+m₂)</c> — and the mass of whichever one is not static when the other is, which is
    /// the same expression's limit and not a case bolted onto it.
    /// </summary>
    public static float ReducedMassKg(float firstKg, float secondKg)
    {
        if (float.IsPositiveInfinity(firstKg)) return secondKg;
        if (float.IsPositiveInfinity(secondKg)) return firstKg;

        var total = firstKg + secondKg;
        return total > 0f ? firstKg * secondKg / total : 0f;
    }

    /// <summary>
    /// <c>E = ½ · μ · v²</c> in kilojoules. A negative closing speed is two bodies separating, never
    /// energy in the other direction.
    /// </summary>
    public static float EnergyKj(float reducedMassKg, float closingMps)
    {
        if (closingMps <= 0f) return 0f;

        return 0.5f * reducedMassKg * closingMps * closingMps / 1000f;
    }

    /// <param name="closingMps">
    /// Closing speed along the contact normal, taken from the velocities the pair carried <em>into</em>
    /// the tick they began touching in — never from what the solver left them with, which is the
    /// response and not the cause.
    /// </param>
    public static DamageVerdict Resolve(SimConfig config, in DamageSubject first, in DamageSubject second, float closingMps)
    {
        var energyKj = EnergyKj(ReducedMassKg(first.MassKg, second.MassKg), closingMps);

        return new DamageVerdict(
            OutcomeFor(config, first, second, energyKj), OutcomeFor(config, second, first, energyKj), energyKj);
    }

    static DamageOutcome OutcomeFor(SimConfig config, in DamageSubject subject, in DamageSubject other, float energyKj)
    {
        if (subject.Kind == Participant.Static) return DamageOutcome.None;

        // A spent body cannot be made spent again and contributes nothing to the other participant,
        // which is what lets a car pass over somebody already lying in the road.
        if (subject.Spent || other.Spent) return DamageOutcome.None;

        if (subject.Kind == Participant.Person)
        {
            // Person against person and person against static geometry are harmless at any energy, so a
            // vehicle is the only thing that can harm a person. Who was moving carries no weight — only
            // the closing speed and the two masses are in the arithmetic.
            if (other.Kind != Participant.Car) return DamageOutcome.None;

            return energyKj >= config.PersonCasualtyKj ? DamageOutcome.Wounded : DamageOutcome.None;
        }

        // PHY-4b: a vehicle built not to break takes the contact and keeps its shape. The other side's
        // outcome is decided above from the same energy, so what hits an evacuator is judged as it would
        // be against any other car.
        if (subject.Unbreakable) return DamageOutcome.None;

        return energyKj >= config.Damage.CarWreckKj ? DamageOutcome.Broken : DamageOutcome.None;
    }
}
