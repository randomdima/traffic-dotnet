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

    /// <summary>Nothing, a brush too light to move a body, one that puts them in the road, and a wreck-making one.</summary>
    const float NoneKj = 0f;

    const float BrushKj = 1f;
    const float KnockKj = 10f;
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
        "wounded" => DamageOutcome.Wounded,
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

    static DamageSubject SubjectOf(Participant kind, bool spent = false) => kind switch
    {
        Participant.Person => DamageSubject.Person(Config.Person.MassKg, spent),
        Participant.Car => DamageSubject.Car(Config.Car.MassKg, spent),
        _ => DamageSubject.Static,
    };

    static DamageVerdict Judge(Participant first, Participant second, float energyKj, bool firstIsSpent = false) =>
        DamageResolver.Resolve(
            Config, SubjectOf(first, firstIsSpent), SubjectOf(second),
            ClosingMps(energyKj, MassOf(first), MassOf(second)));

    /// <summary>
    /// The whole matrix, spelled out rather than derived: a table that re-computed its own expectations
    /// would be the resolver checked against itself.
    /// </summary>
    [Theory]
    // A person against a person is harmless at any energy (PHY-4a).
    [InlineData("person", "person", NoneKj, "none")]
    [InlineData("person", "person", BrushKj, "none")]
    [InlineData("person", "person", KnockKj, "none")]
    [InlineData("person", "person", WreckKj, "none")]
    // A person against static geometry is harmless at any energy (PHY-4a).
    [InlineData("person", "static", NoneKj, "none")]
    [InlineData("person", "static", BrushKj, "none")]
    [InlineData("person", "static", KnockKj, "none")]
    [InlineData("person", "static", WreckKj, "none")]
    // A person against a car, and one band: down in the road is the whole of what a contact makes of
    // somebody, so the energy that breaks a car does no more to them than the energy that moves them.
    [InlineData("person", "car", NoneKj, "none")]
    [InlineData("person", "car", BrushKj, "none")]
    [InlineData("person", "car", KnockKj, "wounded")]
    [InlineData("person", "car", WreckKj, "wounded")]
    // A car breaks at its own tolerance and against anything, which is the point of having one rule.
    [InlineData("car", "person", NoneKj, "none")]
    [InlineData("car", "person", BrushKj, "none")]
    [InlineData("car", "person", KnockKj, "none")]
    [InlineData("car", "person", WreckKj, "broken")]
    [InlineData("car", "car", NoneKj, "none")]
    [InlineData("car", "car", BrushKj, "none")]
    [InlineData("car", "car", KnockKj, "none")]
    [InlineData("car", "car", WreckKj, "broken")]
    [InlineData("car", "static", NoneKj, "none")]
    [InlineData("car", "static", BrushKj, "none")]
    [InlineData("car", "static", KnockKj, "none")]
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
        string[] outcomes = ["none", "wounded", "broken"];

        Assert.Equal(Enum.GetValues<Participant>(), Array.ConvertAll(kinds, KindNamed));
        Assert.Equal(Enum.GetValues<DamageOutcome>(), Array.ConvertAll(outcomes, OutcomeNamed));
    }

    /// <summary>
    /// The same contact may break one participant and not the other, and this is the pairing where it
    /// is most visible: the pedestrian weighs a seventeenth of the car, so the speed that puts them in
    /// the road barely marks it.
    /// </summary>
    [Fact]
    public void OneContactCanBreakOneParticipantAndNotTheOther()
    {
        var verdict = Judge(Participant.Person, Participant.Car, KnockKj);

        Assert.Equal(DamageOutcome.Wounded, verdict.ToFirst);
        Assert.Equal(DamageOutcome.None, verdict.ToSecond);
    }

    /// <summary>
    /// PER-23 as arithmetic: <b>the tolerance is the work of sliding a body the authored distance</b>, so a
    /// person struck at exactly it has exactly that much ground left in them. The reduced mass is what
    /// makes it a little under half a metre rather than exactly half — a car is heavy, not infinite.
    /// </summary>
    [Fact]
    public void ThePersonsToleranceIsTheAuthoredSlide()
    {
        var closingMps = ClosingMps(Config.PersonCasualtyKj, Config.Person.MassKg, Config.Car.MassKg);
        var carriedMps = closingMps * Config.Car.MassKg / (Config.Car.MassKg + Config.Person.MassKg);
        var slideM = carriedMps * carriedMps / (2f * Config.PersonSlidingGripMps2);

        // A band and not a fingerprint: what has to hold is that a casualty costs a car meeting a standing
        // body at about the speed this town's traffic runs at, and the digits of it move whenever a raw
        // term under the sliding grip does.
        Assert.InRange(closingMps, 9.5f, 11f);
        Assert.Equal(Config.Damage.SlideToCasualtyM, slideM, 1);
    }

    /// <summary>
    /// <b>The band sits above the town's own walking pace</b>, which is what makes a knock-down an impact
    /// rather than a contact. Nothing in PER-23 asks who was carrying the closing speed, so a band under
    /// <see cref="PersonFigures.WalkSpeedMps"/> is one a walker meets by arriving at a car that never moved.
    /// </summary>
    [Fact]
    public void AWalkerArrivingAtAParkedCarAtItsOwnPaceStaysOnItsFeet()
    {
        var verdict = DamageResolver.Resolve(
            Config, DamageSubject.Person(Config.Person.MassKg, down: false),
            DamageSubject.Car(Config.Car.MassKg, broken: false), Config.PersonWalkSpeedMps);

        Assert.Equal(DamageOutcome.None, verdict.ToFirst);
        Assert.Equal(DamageOutcome.None, verdict.ToSecond);
    }

    /// <summary>
    /// The table's bands are literals because <c>[InlineData]</c> may hold nothing else, so this is what
    /// keeps them bands: a tolerance moved in the figures without them has cases on the wrong side of it.
    /// </summary>
    [Fact]
    public void TheTableBandsStraddleTheShippedTolerances()
    {
        Assert.True(BrushKj < Config.PersonCasualtyKj);
        Assert.True(KnockKj > Config.PersonCasualtyKj && KnockKj < Config.Damage.CarWreckKj);
        Assert.True(WreckKj > Config.Damage.CarWreckKj);
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

    /// <summary>PHY-5a: a body already down cannot be put down again, and contributes nothing to the other participant.</summary>
    [Theory]
    [InlineData("person", "car")]
    [InlineData("car", "car")]
    [InlineData("car", "person")]
    public void ASpentBodyNeitherGainsAStateNorGivesOne(string spent, string other)
    {
        var verdict = Judge(KindNamed(spent), KindNamed(other), WreckKj, firstIsSpent: true);

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
    /// PHY-4b: <b>an unbreakable car is never broken and is never spent</b>. The first half is the
    /// exemption; the second is what separates it from PHY-5a, and it is the half worth a case — whatever
    /// runs into an evacuator is wrecked by exactly the energy that would have wrecked it against any
    /// other car.
    /// </summary>
    [Fact]
    public void AnUnbreakableCarTakesNoStateAndDeniesTheOtherSideNothing()
    {
        var closingMps = ClosingMps(WreckKj, Config.Car.MassKg, Config.Car.MassKg);
        var ordinary = DamageSubject.Car(Config.Car.MassKg, broken: false);
        var unbreakable = DamageSubject.Car(Config.Car.MassKg, broken: false, unbreakable: true);

        var verdict = DamageResolver.Resolve(Config, unbreakable, ordinary, closingMps);

        Assert.Equal(DamageOutcome.None, verdict.ToFirst);
        Assert.Equal(DamageOutcome.Broken, verdict.ToSecond);
        Assert.Equal(DamageResolver.Resolve(Config, ordinary, ordinary, closingMps).ToSecond, verdict.ToSecond);
    }

    /// <summary>
    /// And a person is not made safe by what they were struck by: the arithmetic is the two masses and the
    /// closing speed, and being unbreakable is an outcome the car does not take rather than energy it does
    /// not carry.
    /// </summary>
    [Fact]
    public void AnUnbreakableCarIsAsDangerousToAPersonAsAnyOther()
    {
        var closingMps = ClosingMps(KnockKj, Config.Person.MassKg, Config.Car.MassKg);
        var person = DamageSubject.Person(Config.Person.MassKg, down: false);

        var verdict = DamageResolver.Resolve(
            Config, person, DamageSubject.Car(Config.Car.MassKg, broken: false, unbreakable: true), closingMps);

        Assert.Equal(DamageOutcome.Wounded, verdict.ToFirst);
        Assert.Equal(DamageOutcome.None, verdict.ToSecond);
    }

    /// <summary>
    /// The thresholds are read from the injected figures and are nowhere in the code: a town whose bodies
    /// slide further before they are counted as knocked over must give a different outcome at the same
    /// energy the shipped one wounds at.
    /// </summary>
    [Fact]
    public void TheTolerancesComeFromTheFiguresAndNotFromTheResolver()
    {
        var sturdy = new SimConfig { Damage = new DamageFigures { SlideToCasualtyM = 50f, CarWreckKj = 500f } };
        var closingMps = ClosingMps(KnockKj, sturdy.Person.MassKg, sturdy.Car.MassKg);

        var verdict = DamageResolver.Resolve(
            sturdy, DamageSubject.Person(sturdy.Person.MassKg, down: false),
            DamageSubject.Car(sturdy.Car.MassKg, broken: false), closingMps);

        Assert.Equal(DamageOutcome.None, verdict.ToFirst);
        Assert.Equal(DamageOutcome.None, verdict.ToSecond);
        Assert.Equal(DamageOutcome.Wounded, Judge(Participant.Person, Participant.Car, KnockKj).ToFirst);
    }
}
