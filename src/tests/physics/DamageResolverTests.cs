using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Physics;
using Xunit;

namespace TrafficSimulation.Tests.Physics;

/// <summary>
/// VER-6, and the cheapest place in the project to be exhaustive: every ordered pair of participant
/// kinds against every band of contact energy, and a person against every vehicle at every band of its
/// speed, with no solver in the room.
/// </summary>
/// <remarks>
/// <para>
/// The bands are named by the tolerances that divide them, and the speeds each case is run at are
/// computed <em>backwards</em> from the energy or the speed it is meant to land against — so a threshold
/// changed in the config moves the cases with it and none of them can pass because a speed happened to be
/// on the right side of a number nobody rechecked.
/// </para>
/// <para>
/// The table names its kinds and outcomes as text because both enums are <c>internal</c> and a public
/// <c>[Theory]</c> may not take one. <see cref="EveryKindAndOutcomeIsNamedExactlyOnce"/> is what keeps
/// that from becoming a way to spell a case wrong.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P1)]
public class DamageResolverTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Nothing, a brush too light to break anything, a harder knock, and a wreck-making one.</summary>
    const float NoneKj = 0f;

    const float BrushKj = 1f;
    const float KnockKj = 10f;
    const float WreckKj = 30f;

    static Participant KindNamed(string name) => name switch
    {
        "static" => Participant.Static,
        "person" => Participant.Person,
        "car" => Participant.Car,
        "train" => Participant.Train,
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

    /// <summary>Head on, half the closing speed each, so neither one is "the one that was moving".</summary>
    static Approach HeadOn(float closingMps) => new(closingMps * 0.5f, closingMps * 0.5f);

    static float MassOf(Participant kind) => kind switch
    {
        Participant.Person => Config.Person.MassKg,
        Participant.Car => Config.Car.MassKg,
        Participant.Train => TrainKg,
        _ => float.PositiveInfinity,
    };

    /// <summary>A coach's weight: a train is a real, heavy mass (TRN-3).</summary>
    const float TrainKg = 58_000f;

    static DamageSubject SubjectOf(Participant kind, bool spent = false) => kind switch
    {
        Participant.Person => DamageSubject.Person(Config.Person.MassKg, spent),
        Participant.Car => DamageSubject.Car(Config.Car.MassKg, spent),
        Participant.Train => DamageSubject.Train(TrainKg),
        _ => DamageSubject.Static,
    };

    static DamageVerdict Judge(Participant first, Participant second, float energyKj, bool firstIsSpent = false) =>
        DamageResolver.Resolve(
            Config, SubjectOf(first, firstIsSpent), SubjectOf(second),
            HeadOn(ClosingMps(energyKj, MassOf(first), MassOf(second))));

    /// <summary>
    /// The whole matrix of what an energy does, spelled out rather than derived: a table that re-computed
    /// its own expectations would be the resolver checked against itself. A person against a vehicle is
    /// judged by the vehicle's speed and not the energy, and is <see cref="APersonGoesDownToAVehicleMovingIntoThemFasterThanAPush"/>.
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
    // A train is a vehicle of unbounded mass (TRN-3): what it meets takes the contact at its own tolerance, as against a
    // car, and it is never harmed.
    [InlineData("car", "train", KnockKj, "none")]
    [InlineData("car", "train", WreckKj, "broken")]
    [InlineData("train", "car", WreckKj, "none")]
    [InlineData("train", "person", WreckKj, "none")]
    public void EveryOrderedPairAndEveryBandGivesTheArithmeticsOutcome(
        string first, string second, float energyKj, string expected)
    {
        Assert.Equal(OutcomeNamed(expected), Judge(KindNamed(first), KindNamed(second), energyKj).ToFirst);
    }

    /// <summary>
    /// PER-23: <b>a person goes down to a vehicle moving into them faster than a push, and to nothing else</b>
    /// — the vehicle's own motion along the normal, whatever the person was doing, and one band: down in the
    /// road is the whole of what a contact makes of somebody.
    /// </summary>
    /// <param name="vehicleInBands">How fast the vehicle was moving into the person, in <c>KnockDownAboveMps</c>; negative is away.</param>
    /// <param name="personInBands">And the person into the vehicle.</param>
    [Theory]
    [InlineData("car", 0f, 0f, "none")]
    [InlineData("car", 0.5f, 0f, "none")]
    [InlineData("car", 1.5f, 0f, "wounded")]
    [InlineData("car", 20f, 0f, "wounded")]
    [InlineData("car", 1.5f, -1f, "wounded")]
    [InlineData("car", 0.5f, 10f, "none")]
    [InlineData("car", -2f, 10f, "none")]
    [InlineData("car", 0f, 20f, "none")]
    [InlineData("train", 0.5f, 0f, "none")]
    [InlineData("train", 1.5f, 0f, "wounded")]
    public void APersonGoesDownToAVehicleMovingIntoThemFasterThanAPush(
        string vehicle, float vehicleInBands, float personInBands, string expected)
    {
        var bandMps = Config.Damage.KnockDownAboveMps;
        var verdict = DamageResolver.Resolve(
            Config, SubjectOf(Participant.Person), SubjectOf(KindNamed(vehicle)),
            new Approach(personInBands * bandMps, vehicleInBands * bandMps));

        Assert.Equal(OutcomeNamed(expected), verdict.ToFirst);
    }

    /// <summary>Guarded in both directions, so the table above cannot name a kind or an outcome that is not one.</summary>
    [Fact]
    public void EveryKindAndOutcomeIsNamedExactlyOnce()
    {
        string[] kinds = ["static", "person", "car", "train"];
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
    /// <b>Who was moving is the whole of PER-23</b>: a walker arriving at a parked car at the town's own pace
    /// is a contact and not an impact, however fast the pace is.
    /// </summary>
    [Fact]
    public void AWalkerArrivingAtAParkedCarAtItsOwnPaceStaysOnItsFeet()
    {
        var verdict = DamageResolver.Resolve(
            Config, DamageSubject.Person(Config.Person.MassKg, down: false),
            DamageSubject.Car(Config.Car.MassKg, broken: false), new Approach(Config.PersonWalkSpeedMps, 0f));

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
        Assert.True(KnockKj < Config.Damage.CarWreckKj);
        Assert.True(WreckKj > Config.Damage.CarWreckKj);
    }

    /// <summary>Which side is passed first cannot matter, so long as each keeps the speed it came in at.</summary>
    [Theory]
    [InlineData("person", "car")]
    [InlineData("car", "static")]
    [InlineData("car", "car")]
    [InlineData("person", "static")]
    public void TheVerdictDoesNotDependOnWhichSideIsGivenFirst(string first, string second)
    {
        var one = KindNamed(first);
        var other = KindNamed(second);
        var into = new Approach(ClosingMps(WreckKj, MassOf(one), MassOf(other)), Config.Damage.KnockDownAboveMps * 2f);

        var forward = DamageResolver.Resolve(Config, SubjectOf(one), SubjectOf(other), into);
        var backward = DamageResolver.Resolve(Config, SubjectOf(other), SubjectOf(one), new Approach(into.SecondMps, into.FirstMps));

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
            Config, SubjectOf(Participant.Car), SubjectOf(Participant.Car), HeadOn(separatingMps));

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
        var into = HeadOn(ClosingMps(WreckKj, Config.Car.MassKg, Config.Car.MassKg));
        var ordinary = DamageSubject.Car(Config.Car.MassKg, broken: false);
        var unbreakable = DamageSubject.Car(Config.Car.MassKg, broken: false, unbreakable: true);

        var verdict = DamageResolver.Resolve(Config, unbreakable, ordinary, into);

        Assert.Equal(DamageOutcome.None, verdict.ToFirst);
        Assert.Equal(DamageOutcome.Broken, verdict.ToSecond);
        Assert.Equal(DamageResolver.Resolve(Config, ordinary, ordinary, into).ToSecond, verdict.ToSecond);
    }

    /// <summary>
    /// And a person is not made safe by what they were struck by: being unbreakable is an outcome the car
    /// does not take, and not a speed it does not carry.
    /// </summary>
    [Fact]
    public void AnUnbreakableCarIsAsDangerousToAPersonAsAnyOther()
    {
        var person = DamageSubject.Person(Config.Person.MassKg, down: false);

        var verdict = DamageResolver.Resolve(
            Config, person, DamageSubject.Car(Config.Car.MassKg, broken: false, unbreakable: true),
            new Approach(0f, Config.Damage.KnockDownAboveMps * 2f));

        Assert.Equal(DamageOutcome.Wounded, verdict.ToFirst);
        Assert.Equal(DamageOutcome.None, verdict.ToSecond);
    }

    /// <summary>
    /// The thresholds are read from the injected figures and are nowhere in the code: a town whose band is
    /// faster must leave on their feet somebody the shipped one puts down at the same speed.
    /// </summary>
    [Fact]
    public void TheTolerancesComeFromTheFiguresAndNotFromTheResolver()
    {
        var into = new Approach(0f, Config.Damage.KnockDownAboveMps * 2f);
        var sturdy = new SimConfig
        {
            Damage = new DamageFigures { KnockDownAboveMps = Config.Damage.KnockDownAboveMps * 10f, CarWreckKj = 500f },
        };

        var verdict = DamageResolver.Resolve(
            sturdy, DamageSubject.Person(sturdy.Person.MassKg, down: false),
            DamageSubject.Car(sturdy.Car.MassKg, broken: false), into);

        Assert.Equal(DamageOutcome.None, verdict.ToFirst);
        Assert.Equal(DamageOutcome.None, verdict.ToSecond);
        Assert.Equal(
            DamageOutcome.Wounded,
            DamageResolver.Resolve(Config, SubjectOf(Participant.Person), SubjectOf(Participant.Car), into).ToFirst);
    }
}
