using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Physics;
using Xunit;

namespace TrafficSimulation.Tests.Physics;

/// <summary>
/// VER-6, and the cheapest place in the project to be exhaustive: every ordered pair of participant
/// kinds against every band of contact energy, with no solver in the room.
/// </summary>
/// <remarks>
/// <para>
/// The bands are named by the tolerances that divide them, and the closing speed each case is run at is
/// computed <em>backwards</em> from the energy it is meant to carry — so a threshold changed in the config
/// moves the cases with it and none of them can pass because a speed happened to be on the right side
/// of a number nobody rechecked.
/// </para>
/// <para>
/// The table names its kinds and outcomes as text because both enums are <c>internal</c> and a public
/// <c>[Theory]</c> may not take one. <see cref="EveryKindAndOutcomeIsNamedExactlyOnce"/> is what keeps
/// that from becoming a way to spell a case wrong.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
public class DamageResolverTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Nothing, a bump, a survivable hit for a person, a fatal one, and a wreck-making one.</summary>
    const float NoneKj = 0f;

    const float BumpKj = 1f;
    const float ShakeKj = 4f;
    const float FatalKj = 9f;
    const float WreckKj = 30f;

    static Participant KindNamed(string name) => name switch
    {
        "static" => Participant.Static,
        "person" => Participant.Person,
        "car" => Participant.Car,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Not a participant this table knows."),
    };

    static DamageOutcome OutcomeNamed(string name) => name switch
    {
        "none" => DamageOutcome.None,
        "shaken" => DamageOutcome.Shaken,
        "dead" => DamageOutcome.Dead,
        "broken" => DamageOutcome.Broken,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Not an outcome this table knows."),
    };

    /// <summary>The closing speed that carries a given energy between two masses — the arithmetic's own formula, inverted.</summary>
    static float ClosingMps(float energyKj, float firstKg, float secondKg)
    {
        var reducedKg = float.IsPositiveInfinity(firstKg) ? secondKg
            : float.IsPositiveInfinity(secondKg) ? firstKg
            : firstKg * secondKg / (firstKg + secondKg);

        return MathF.Sqrt(2f * energyKj * 1_000f / reducedKg);
    }

    static float MassOf(Participant kind) => kind switch
    {
        Participant.Person => Config.Person.MassKg,
        Participant.Car => Config.Car.MassKg,
        _ => float.PositiveInfinity,
    };

    static DamageSubject SubjectOf(Participant kind, bool terminal = false) => kind switch
    {
        Participant.Person => DamageSubject.Person(Config.Person.MassKg, terminal),
        Participant.Car => DamageSubject.Car(Config.Car.MassKg, terminal),
        _ => DamageSubject.Static,
    };

    static DamageVerdict Judge(Participant first, Participant second, float energyKj, bool firstIsTerminal = false) =>
        DamageResolver.Resolve(
            Config, SubjectOf(first, firstIsTerminal), SubjectOf(second),
            ClosingMps(energyKj, MassOf(first), MassOf(second)));

    /// <summary>
    /// The whole matrix, spelled out rather than derived: a table that re-computed its own expectations
    /// would be the resolver checked against itself.
    /// </summary>
    [Theory]
    // A person against a person is harmless at any energy (PHY-4a).
    [InlineData("person", "person", NoneKj, "none")]
    [InlineData("person", "person", BumpKj, "none")]
    [InlineData("person", "person", ShakeKj, "none")]
    [InlineData("person", "person", FatalKj, "none")]
    [InlineData("person", "person", WreckKj, "none")]
    // A person against static geometry is harmless at any energy (PHY-4a).
    [InlineData("person", "static", NoneKj, "none")]
    [InlineData("person", "static", BumpKj, "none")]
    [InlineData("person", "static", ShakeKj, "none")]
    [InlineData("person", "static", FatalKj, "none")]
    [InlineData("person", "static", WreckKj, "none")]
    // A person against a car sees all three bands, which no other pairing does.
    [InlineData("person", "car", NoneKj, "none")]
    [InlineData("person", "car", BumpKj, "none")]
    [InlineData("person", "car", ShakeKj, "shaken")]
    [InlineData("person", "car", FatalKj, "dead")]
    [InlineData("person", "car", WreckKj, "dead")]
    // A car breaks at its own tolerance and against anything, which is the point of having one rule.
    [InlineData("car", "person", NoneKj, "none")]
    [InlineData("car", "person", BumpKj, "none")]
    [InlineData("car", "person", ShakeKj, "none")]
    [InlineData("car", "person", FatalKj, "none")]
    [InlineData("car", "person", WreckKj, "broken")]
    [InlineData("car", "car", NoneKj, "none")]
    [InlineData("car", "car", BumpKj, "none")]
    [InlineData("car", "car", ShakeKj, "none")]
    [InlineData("car", "car", FatalKj, "none")]
    [InlineData("car", "car", WreckKj, "broken")]
    [InlineData("car", "static", NoneKj, "none")]
    [InlineData("car", "static", BumpKj, "none")]
    [InlineData("car", "static", ShakeKj, "none")]
    [InlineData("car", "static", FatalKj, "none")]
    [InlineData("car", "static", WreckKj, "broken")]
    // A static object is never affected by anything (PHY-2, PHY-4a).
    [InlineData("static", "person", WreckKj, "none")]
    [InlineData("static", "car", WreckKj, "none")]
    public void EveryOrderedPairAndEveryBandGivesTheArithmeticsOutcome(
        string first, string second, float energyKj, string expected)
    {
        Assert.Equal(OutcomeNamed(expected), Judge(KindNamed(first), KindNamed(second), energyKj).ToFirst);
    }

    /// <summary>Guarded in both directions, so the table above cannot name a kind or an outcome that is not one.</summary>
    [Fact]
    public void EveryKindAndOutcomeIsNamedExactlyOnce()
    {
        string[] kinds = ["static", "person", "car"];
        string[] outcomes = ["none", "shaken", "dead", "broken"];

        Assert.Equal(Enum.GetValues<Participant>(), Array.ConvertAll(kinds, KindNamed));
        Assert.Equal(Enum.GetValues<DamageOutcome>(), Array.ConvertAll(outcomes, OutcomeNamed));
    }

    /// <summary>
    /// The same contact may break one participant and not the other, and this is the pairing where it
    /// is most visible: the pedestrian weighs a seventeenth of the car, so the speed that kills them
    /// barely marks it.
    /// </summary>
    [Fact]
    public void OneContactCanBreakOneParticipantAndNotTheOther()
    {
        var verdict = Judge(Participant.Person, Participant.Car, FatalKj);

        Assert.Equal(DamageOutcome.Dead, verdict.ToFirst);
        Assert.Equal(DamageOutcome.None, verdict.ToSecond);
    }

    /// <summary>Only the closing speed and the two masses are in the arithmetic, so which side is passed first cannot matter.</summary>
    [Theory]
    [InlineData("person", "car")]
    [InlineData("car", "static")]
    [InlineData("car", "car")]
    [InlineData("person", "static")]
    public void TheVerdictDoesNotDependOnWhichSideIsGivenFirst(string first, string second)
    {
        var forward = Judge(KindNamed(first), KindNamed(second), WreckKj);
        var backward = Judge(KindNamed(second), KindNamed(first), WreckKj);

        Assert.Equal(forward.ToFirst, backward.ToSecond);
        Assert.Equal(forward.ToSecond, backward.ToFirst);
        Assert.Equal(forward.EnergyKj, backward.EnergyKj);
    }

    /// <summary>PHY-5a: a terminal body cannot enter another state, and contributes nothing to the other participant.</summary>
    [Theory]
    [InlineData("person", "car")]
    [InlineData("car", "car")]
    [InlineData("car", "person")]
    public void ATerminalBodyNeitherGainsAStateNorGivesOne(string terminal, string other)
    {
        var verdict = Judge(KindNamed(terminal), KindNamed(other), WreckKj, firstIsTerminal: true);

        Assert.Equal(DamageOutcome.None, verdict.ToFirst);
        Assert.Equal(DamageOutcome.None, verdict.ToSecond);
    }

    /// <summary>Two bodies moving apart carry no energy — a negative speed along the normal is separation, never energy the other way.</summary>
    [Fact]
    public void BodiesMovingApartCarryNoEnergy()
    {
        var separatingMps = -ClosingMps(WreckKj, Config.Car.MassKg, Config.Car.MassKg);
        var verdict = DamageResolver.Resolve(
            Config, SubjectOf(Participant.Car), SubjectOf(Participant.Car), separatingMps);

        Assert.Equal(0f, verdict.EnergyKj);
        Assert.Equal(DamageOutcome.None, verdict.ToFirst);
        Assert.Equal(DamageOutcome.None, verdict.ToSecond);
    }

    /// <summary>
    /// The formula itself, against figures worked by hand: <c>½ · m₁m₂/(m₁+m₂) · v²</c>, and the mass
    /// of whichever body is not static when the other is.
    /// </summary>
    [Fact]
    public void TheEnergyIsHalfTheReducedMassTimesTheClosingSpeedSquared()
    {
        Assert.Equal(75.6757f, DamageResolver.ReducedMassKg(80f, 1_400f), 3);
        Assert.Equal(700f, DamageResolver.ReducedMassKg(1_400f, 1_400f), 3);
        Assert.Equal(1_400f, DamageResolver.ReducedMassKg(1_400f, float.PositiveInfinity), 3);
        Assert.Equal(1_400f, DamageResolver.ReducedMassKg(float.PositiveInfinity, 1_400f), 3);

        Assert.Equal(3.7838f, DamageResolver.EnergyKj(75.6757f, 10f), 3);
        Assert.Equal(35f, DamageResolver.EnergyKj(700f, 10f), 3);
        Assert.Equal(70f, DamageResolver.EnergyKj(1_400f, 10f), 3);
    }

    /// <summary>
    /// The thresholds are read from the injected figures and are nowhere in the code: a town tuned to
    /// different tolerances must give different outcomes at the same energy.
    /// </summary>
    [Fact]
    public void TheTolerancesComeFromTheFiguresAndNotFromTheResolver()
    {
        var fragile = new SimConfig { Damage = new DamageFigures { PersonFatalKj = 0.5f, PersonShakeKj = 0.25f, CarWreckKj = 0.5f } };
        var closingMps = ClosingMps(BumpKj, fragile.Person.MassKg, fragile.Car.MassKg);

        var verdict = DamageResolver.Resolve(
            fragile, DamageSubject.Person(fragile.Person.MassKg, dead: false),
            DamageSubject.Car(fragile.Car.MassKg, broken: false), closingMps);

        Assert.Equal(DamageOutcome.Dead, verdict.ToFirst);
        Assert.Equal(DamageOutcome.Broken, verdict.ToSecond);
    }
}
