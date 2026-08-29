using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Bench;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Physics;
using TrafficSimulation.Core.Simulation;
using Xunit;

namespace TrafficSimulation.Tests.Physics;

/// <summary>
/// The staged half of stage 3: the arithmetic is exhaustively checked next door with no solver in the
/// room, and what these check is <em>everything between it and a town</em> — the filter that let the
/// pair touch at all, the normal the closing speed is measured along, the snapshot taken before the
/// step, and the terminal state that comes out the other side.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class CrashCaseTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    public static TheoryData<string> EveryStagedCase()
    {
        var names = new TheoryData<string>();
        foreach (var row in CrashProbe.Cases(Config)) names.Add(row.Name);

        return names;
    }

    /// <summary>
    /// Every case the crash sandbox stages, one test each, so a failure names the case rather than the
    /// table. The rows carry their own expectation — <c>--bench crash</c> prints exactly this.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryStagedCase))]
    public void TheStagedCaseReadsAsTheArithmeticSaysItMust(string name)
    {
        var row = Array.Find(CrashProbe.Cases(Config), staged => staged.Name == name);

        Assert.Equal(row.Expected, row.Read);
    }

    /// <summary>
    /// PHY-5: a wreck is never removed from the world. It keeps its body, keeps its shape, stays dynamic
    /// — and takes no actions, which is what makes it a wreck rather than a car with nobody in it.
    /// </summary>
    [Fact]
    public void AWreckKeepsItsBodyAndTakesNoActions()
    {
        using var rig = new CrashSandbox(Config);
        var car = rig.AddCar(Vector2.Zero, 0f);
        rig.Apply(new BodyTag(BodyKind.Car, car), DamageOutcome.Broken);

        Assert.True(rig.IsTerminal(rig.People.Count + car));
        Assert.False(rig.Cars.Driven[car]);
        Assert.True(rig.Cars.Broken[car]);

        new SimLoop<CrashSandbox>(rig, Config).Advance(60);

        // Still there, still a body, still exactly where nothing pushed it.
        Assert.Equal(1, rig.Cars.Count);
        Assert.True((rig.Cars.PositionM[car] - Vector2.Zero).Length() < 0.01f);
    }

    /// <summary>
    /// PHY-5: <b>a wreck's wheels lock where the crash left them pointing.</b> A car broken mid-corner has
    /// its rack wound over and nothing afterwards is turning it back, so the angle is carried rather than
    /// zeroed — it is what the four patches skid along and what the four tyres are drawn at.
    /// </summary>
    [Fact]
    public void AWrecksWheelsLockAtTheAngleTheCrashLeftThemAt()
    {
        using var rig = new CrashSandbox(Config);
        var car = rig.AddCar(Vector2.Zero, 0f);

        const float OnTheLockRad = 0.37f;
        rig.Cars.Command[car] = new DriveCommand(OnTheLockRad, 0f, 0f, false, false);
        rig.Apply(new BodyTag(BodyKind.Car, car), DamageOutcome.Broken);

        Assert.Equal(OnTheLockRad, rig.Cars.Command[car].SteerRad);
        Assert.True(rig.Cars.Command[car].LocksEveryWheel, "a wreck rolls on a wheel the crash did not lock");
    }

    /// <summary>
    /// A casualty is slowed by the ground it is lying on and not by a manoeuvre: it takes no actions, and
    /// the sliding grip still stops it. A body nothing acted on would slide until the damping caught it.
    /// </summary>
    [Fact]
    public void ACasualtySlidesToAStopOnTheGroundAndNotOnIntent()
    {
        const float LaunchedMps = 10f;

        using var rig = new CrashSandbox(Config);
        var person = rig.AddPerson(Vector2.Zero);
        rig.Apply(new BodyTag(BodyKind.Person, person), DamageOutcome.Wounded);
        rig.Launch(new BodyTag(BodyKind.Person, person), new Vector2(LaunchedMps, 0f));

        var stoppingFromM = rig.People.PositionM[person].X;
        new SimLoop<CrashSandbox>(rig, Config).Advance(600);

        Assert.True(rig.People.Wounded[person]);
        Assert.False(rig.People.IsOnItsFeet(person));
        Assert.Equal(0f, rig.People.VelocityMps[person].Length(), 2);

        // On the sliding grip, which is the whole of what stops it: nothing else acted on the body.
        var onTheGripM = LaunchedMps * LaunchedMps / (2f * Config.PersonSlidingGripMps2);
        var slidM = rig.People.PositionM[person].X - stoppingFromM;
        Assert.InRange(slidM, onTheGripM * 0.8f, onTheGripM * 1.3f);
    }

    /// <summary>
    /// <b>A body off its feet is sent down the road, and it is the strike that sends it</b> — not a ground
    /// that gives way under it. The two grips are close, because a sole and a body along the same asphalt
    /// stop in much the same distance; what carries a casualty clear of where it was hit is the speed it
    /// was handed and the fact that it declares nothing to spend on stopping.
    /// </summary>
    [Fact]
    public void ACasualtyStruckWellOverTheBandIsSentFurtherThanItsOwnBody()
    {
        var carriedMps = ToleranceClosingMps(9f) * Config.Car.MassKg / (Config.Car.MassKg + Config.Person.MassKg);

        using var rig = new CrashSandbox(Config);
        var person = rig.AddPerson(Vector2.Zero);
        rig.Apply(new BodyTag(BodyKind.Person, person), DamageOutcome.Wounded);
        rig.Launch(new BodyTag(BodyKind.Person, person), new Vector2(carriedMps, 0f));

        var sentFromM = rig.People.PositionM[person].X;
        new SimLoop<CrashSandbox>(rig, Config).Advance(600);

        var sentM = rig.People.PositionM[person].X - sentFromM;
        Assert.True(sentM > Config.PersonDiameterM,
            $"a body carried {sentM:F2} m against its own {Config.PersonDiameterM:F2} m is not being sent anywhere");
    }

    /// <summary>
    /// PER-23 through the solver rather than asserted of the arithmetic: a car meeting a standing body
    /// over the tolerance puts it down, and one under the tolerance leaves it on its feet.
    /// </summary>
    /// <remarks>
    /// <b>Only the outcome is asked, and deliberately not the distance.</b> A car below the tolerance
    /// still <em>pushes</em> — a braced walker weighs a seventeenth of what is leaning on it and gets
    /// shoved several metres without ever going down, which is the foot model's own stated wrongness and
    /// not this band's business. What the half metre sizes is the energy, and
    /// <see cref="ABodyPutDownAtTheToleranceSlidesTheAuthoredDistance"/> is where that is measured.
    /// </remarks>
    [Theory]
    [InlineData(1.5f, true)]
    [InlineData(0.5f, false)]
    public void TheBandIsWhereTheAuthoredSlideIs(float ofTheTolerance, bool goesDown)
    {
        var closingMps = ToleranceClosingMps(ofTheTolerance);

        using var rig = new CrashSandbox(Config);
        var car = rig.AddCar(Vector2.Zero, 0f);
        var person = rig.AddPerson(
            new Vector2(
                (Config.Car.LengthM * 0.5f) + (closingMps * 0.05f) + (Config.PersonDiameterM * 0.5f), 0f));

        rig.Launch(new BodyTag(BodyKind.Car, car), new Vector2(closingMps, 0f));
        new SimLoop<CrashSandbox>(rig, Config).Advance(240);

        Assert.Equal(goesDown, rig.People.Wounded[person]);
    }

    /// <summary>
    /// And the other half of PER-23: <b>the tolerance is the authored distance</b>. A body put down
    /// carrying exactly what a contact at the tolerance hands it slides about that far and stops.
    /// </summary>
    [Fact]
    public void ABodyPutDownAtTheToleranceSlidesTheAuthoredDistance()
    {
        // What the contact leaves in the body: the pair's common speed, which for a car of seventeen
        // times the mass is nearly all of the closing speed.
        var carriedMps = ToleranceClosingMps(1f) * Config.Car.MassKg / (Config.Car.MassKg + Config.Person.MassKg);

        using var rig = new CrashSandbox(Config);
        var person = rig.AddPerson(Vector2.Zero);
        rig.Apply(new BodyTag(BodyKind.Person, person), DamageOutcome.Wounded);
        rig.Launch(new BodyTag(BodyKind.Person, person), new Vector2(carriedMps, 0f));

        var slidFromM = rig.People.PositionM[person].X;
        new SimLoop<CrashSandbox>(rig, Config).Advance(600);

        var slidM = rig.People.PositionM[person].X - slidFromM;
        Assert.InRange(slidM, Config.Damage.SlideToCasualtyM * 0.6f, Config.Damage.SlideToCasualtyM * 1.2f);
    }

    /// <summary>The closing speed a car and a standing body meet at, at a given share of the person's own tolerance.</summary>
    static float ToleranceClosingMps(float ofTheTolerance) =>
        MathF.Sqrt(
            2f * Config.PersonCasualtyKj * ofTheTolerance * 1_000f
            / DamageResolver.ReducedMassKg(Config.Car.MassKg, Config.Person.MassKg));
}
