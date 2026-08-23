using System.Numerics;
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
[Collection(Simulation.SolverCollection.Name)]
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
    /// A corpse is slowed by the ground it is lying on and not by a manoeuvre: it takes no actions, and
    /// the sliding grip still stops it. A body nothing acted on would slide until the damping caught it.
    /// </summary>
    [Fact]
    public void ACorpseSlidesToAStopOnTheGroundAndNotOnIntent()
    {
        using var rig = new CrashSandbox(Config);
        var person = rig.AddPerson(Vector2.Zero);
        rig.Apply(new BodyTag(BodyKind.Person, person), DamageOutcome.Dead);
        rig.Launch(new BodyTag(BodyKind.Person, person), new Vector2(10f, 0f));

        var loop = new SimLoop<CrashSandbox>(rig, Config);
        loop.Advance(1);
        var stoppingFromM = rig.People.PositionM[person].X;
        loop.Advance(600);

        Assert.True(rig.People.Dead[person]);
        Assert.Equal(0f, rig.People.VelocityMps[person].Length(), 2);

        // At the sliding grip and not at the foot grip: 10 m/s against 4 m/s² is some twelve metres,
        // where a body still on its feet would have stopped inside half of one.
        var slidM = rig.People.PositionM[person].X - stoppingFromM;
        Assert.InRange(slidM, 10f, 14f);
    }

    /// <summary>
    /// The survivable band leaves the walker off its feet for the stumble window and no longer
    /// (PER-12a), which is what makes the impulse of the impact visible after the impact is over.
    /// </summary>
    [Fact]
    public void AShakenWalkerIsOffItsFeetForTheStumbleWindowAndThenBackOnThem()
    {
        using var rig = new CrashSandbox(Config);
        var person = rig.AddPerson(Vector2.Zero);
        rig.Apply(new BodyTag(BodyKind.Person, person), DamageOutcome.Shaken);

        Assert.False(rig.People.IsOnItsFeet(person));

        var loop = new SimLoop<CrashSandbox>(rig, Config);
        loop.Advance((int)(Config.Person.StumbleWindowS * Config.Sim.TickRateHz) - 1);
        Assert.False(rig.People.IsOnItsFeet(person));

        loop.Advance(2);
        Assert.True(rig.People.IsOnItsFeet(person));
    }
}
