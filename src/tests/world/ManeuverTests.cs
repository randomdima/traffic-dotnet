using System.Numerics;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Car.Maneuvers;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The catalogue in a running town: that every entry this engine has built is actually reached, that
/// nothing stands still with no clock against it, and that a car <b>slows for a crossing and stops
/// short of one somebody is on</b> (CAR-7b, TER-4c.1) — which no entry of the catalogue does, so what is
/// asserted here is the profile these tests can only see through the bodies.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class ManeuverTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// Long enough for the fixture map to turn a whole trip over, which is where the parking entries
    /// come from. <b>Two minutes and not one</b>: the ground took a rolling resistance when the tyres
    /// did, which costs a car about a fifth of its acceleration, and a small lit map is stop-start
    /// enough that a whole leg no longer fits in a minute.
    /// </summary>
    const int MeasuredTicks = 7_200;

    static TownWorld Open(string map) => new(Towns.Of(map), Config);

    /// <summary>
    /// <b>An entry nothing can reach is a finding.</b> These are the ones this engine has built, and a
    /// change that quietly stops naming one of them is a manoeuvre that has become unenterable —
    /// exactly the fault the trace exists for.
    /// </summary>
    /// <remarks>
    /// <b>The fixture first and the city after it, because reachability is the engine's property and not
    /// the map's.</b> The fixture is a dozen junctions and a handful of cars: whether one of them is ever
    /// refused a box in two minutes turns on the phase offsets, so an entry that is only reached there by a
    /// single event is one a re-timed junction can silently take away. What the claim is about is that
    /// something in the town still names each of them, and a town nothing reaches an entry in is the finding.
    /// </remarks>
    [Fact]
    public void EveryEntryThisEngineBuildsIsReached()
    {
        using var fixture = Open(Towns.Fixture);
        new SimLoop<TownWorld>(fixture, Config).Advance(MeasuredTicks);

        using var city = Open("Odesa");
        new SimLoop<TownWorld>(city, Config).Advance(MeasuredTicks);

        Maneuver[] built =
        [
            Maneuver.LeaveTheBay, Maneuver.RunTheLine, Maneuver.HoldAtALine,
            Maneuver.TakeTheJunction, Maneuver.ParkInTheBay, Maneuver.StandParked,
        ];

        foreach (var entry in built)
        {
            Assert.True(
                fixture.Trace.EverEntered(entry) || city.Trace.EverEntered(entry),
                $"{Maneuvers.Code(entry)} was never entered in two minutes of either town: nothing reaches it");
        }
    }

    /// <summary>
    /// <b>There is no state a car can stand still in that nothing is running for.</b> The watchdog has
    /// every driven car and the light has the ones queueing at one — so this counter is zero or the wiring
    /// has a hole in it.
    /// </summary>
    [Theory]
    [InlineData("Test")]
    [InlineData("River")]
    public void NoCarStandsStillWithNothingRunningForIt(string map)
    {
        using var world = Open(map);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(1_200);

        Assert.Equal(0, world.Trace.StoodUnclocked);
    }

    /// <summary>
    /// CAR-7b: <b>a car reduces its pace over a crossing whether or not anybody is visible.</b>
    /// Measured on the bodies rather than on the intent — what is asserted is the speed of cars whose
    /// own body is on the paint.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The margin is for the two ways a car on the paint can be going faster than it asked to: it may
    /// be being pushed, and the pace is a target the tyres have to deliver on the ground under them.
    /// A wreck is not driving and is not asserted about.
    /// </para>
    /// <para>
    /// <b>A crossing showing its kerbs a red is exempt and is skipped here for the same reason the
    /// driver skips it</b>: the car has the priority, and crawling over it would spend the green the
    /// whole queue behind is waiting to use. <b>The exemption outlives the change by the amber tail</b>,
    /// because the cycle has no all-red in it (TLT-4) and that tail is what clears the ground: a car a
    /// stride short of the paint at nineteen metres a second when the phase turns cannot stop inside its
    /// own length, and asserting against it asserts about the timetable rather than about the driver.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("Test")]
    [InlineData("River")]
    public void CarsCrossTheirZebrasAtCrossingPace(string map)
    {
        var plan = Towns.Of(map);
        using var world = new TownWorld(plan, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(600);

        var onThePaint = 0;

        // When each crossing last held its own kerbs. A phase that has just turned cannot bind a car that
        // is already on the paint: the cycle has no all-red in it (TLT-4), so the ground is cleared by the
        // amber tail, and a body still on it that long after the change is clearing rather than speeding.
        var heldItsKerbsS = new float[plan.Crosswalks.Count];
        Array.Fill(heldItsKerbsS, float.NegativeInfinity);
        for (var tick = 0; tick < 1_800; tick++)
        {
            loop.Advance();
            for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
            {
                if (KerbsAreRed(world, crossing)) heldItsKerbsS[crossing] = world.ElapsedS;
            }

            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (!world.Cars.Driven[car] || world.Cars.Broken[car]) continue;

                var atM = world.Cars.PositionM[car];
                var crossing = CrossingUnder(plan, atM);
                if (crossing < 0) continue;

                // The kerbs' own red is the driver's exemption, read off the same table they read — and it
                // outlives the change by the tail that clears the ground.
                if (world.ElapsedS - heldItsKerbsS[crossing] <= Config.Signals.AmberTailS) continue;

                onThePaint++;
                var speedMps = world.Cars.VelocityMps[car].Length();
                var context = world.Cars.Context[car];

                // <b>The pace this car owes, and not the nominal car's</b> (CAR-11): a crossing is
                // approached at a creep sized by the body doing the creeping, so a long car is allowed the
                // fraction more it is longer by. The half again is what a body still clearing the paint has.
                var capMps = world.Cars.BuildOf(car).CrossingPaceMps * 1.5f;
                Assert.True(
                    speedMps <= capMps,
                    $"{map}: car {car} crossed the paint at {speedMps:F1} m/s against a pace of " +
                    $"{world.Cars.BuildOf(car).CrossingPaceMps:F1} m/s " +
                    $"— doing {Maneuvers.Code(world.Cars.Doing[car])}, held by {world.Cars.Hold[car]}, " +
                    $"crossing at {context.CrossingAtM:F1} m at {context.CrossingPaceMps:F1} m/s, " +
                    $"lanes {world.Cars.Line[car].LaneCount}");
            }
        }

        Assert.True(onThePaint > 0, $"{map}: no car drove over a crossing at all, so nothing was asserted");
    }

    /// <summary>
    /// The yield (TER-5e), staged rather than waited for: a body standing on the paint in front of a car
    /// is a stop point on that car's own line, and the driver holds short of it.
    /// </summary>
    [Fact]
    public void ACarSlowsForSomebodyStandingOnTheCrossing()
    {
        var plan = Towns.Of(Towns.Fixture);
        using var world = new TownWorld(plan, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(600);

        var held = 0;
        for (var tick = 0; tick < 1_800; tick++)
        {
            loop.Advance();
            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (world.Cars.Hold[car] == DrivingHold.Crossing) held++;
            }
        }

        Assert.True(held > 0, "no car was ever limited by a crossing on a map that has five of them");
    }

    /// <summary>Whether this crossing is holding its own kerbs, which is the driver's exemption from the pace.</summary>
    static bool KerbsAreRed(TownWorld world, int crossing) =>
        world.Signals.CrossingIsLit(crossing)
        && world.Signals.ForCrossing(crossing, world.ElapsedS) != SignalColour.Green;

    /// <summary>
    /// Which crossing's paint a point stands on, or −1. <b>The depth runs with the traffic and the span
    /// across it</b>, which is the one thing about the crossing register that is easy to get backwards.
    /// </summary>
    static int CrossingUnder(TrafficSimulation.CityGen.CityPlan plan, Vector2 pointM)
    {
        var crossings = plan.Crosswalks;
        for (var crossing = 0; crossing < crossings.Count; crossing++)
        {
            var axis = crossings.Axis[crossing];
            if (axis.LengthSquared() <= 0f) continue;

            axis = Vector2.Normalize(axis);
            var offset = pointM - crossings.CentreM[crossing];
            if (MathF.Abs(Vector2.Dot(offset, axis)) > crossings.DepthM[crossing] * 0.5f) continue;
            if (MathF.Abs((offset.X * -axis.Y) + (offset.Y * axis.X)) > crossings.SpanM[crossing] * 0.5f) continue;

            return crossing;
        }

        return -1;
    }
}
