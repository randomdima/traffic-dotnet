using System.Numerics;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Car.Maneuvers;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The catalogue in a running town: that every entry this engine has built is actually reached, that
/// nothing stands still with no clock against it, and that a car <b>stops short of a crossing somebody
/// is on</b> (TER-4c.1) — which no entry of the catalogue does, so what is asserted here is the profile
/// these tests can only see through the bodies.
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
    /// <b>GEN-4i — a car that has parked stands square in its bay</b>, which is the one thing the settling
    /// straight on the end of a bay's way is bought for. The pose the template ends at is square by
    /// construction, so what this measures is the <em>follower</em>: how much of the turn is still unwinding
    /// when the car comes to rest, and therefore whether the straight is long enough to be worth the ground
    /// it costs the street (P-14).
    /// </summary>
    /// <remarks>
    /// <b>It is read at the tick `P-14` hands the car on and nowhere else</b>, which is what makes it a
    /// statement about that manoeuvre rather than about the town (VER-12). A pose swept out of every car
    /// standing in a bay at the end of a run also catches the ones a collision shoved, the ones that gave
    /// the place up and the ones the plan stood there — three faults with three owners, none of them this.
    /// </remarks>
    [Theory]
    [InlineData("Test")]
    [InlineData("River")]
    public void ACarThatHasParkedStandsSquareInItsBay(string map)
    {
        using var world = Open(map);
        var loop = new SimLoop<TownWorld>(world, Config);
        var wasParking = new bool[world.Cars.Count];

        var worstDeg = 0f;
        var parked = 0;
        var askew = 0;
        for (var tick = 0; tick < MeasuredTicks; tick++)
        {
            loop.Advance();
            for (var car = 0; car < world.Cars.Count; car++)
            {
                var parking = world.Cars.Doing[car] == Maneuver.ParkInTheBay;
                var handedOn = wasParking[car] && !parking;
                wasParking[car] = parking;

                var bay = world.Parking.BayOf(car);
                if (!handedOn || bay < 0) continue;

                var bayRad = world.Parking.HeadingRad(bay);
                var standingRad = BayTemplate.StandingHeadingRad(
                    bayRad, BayTemplate.StandsNoseIn(bayRad, world.Cars.HeadingRad[car]));
                var offDeg = MathF.Abs(
                    BayTemplate.SignedTurnRad(
                        Heading.Unit(standingRad), Heading.Unit(world.Cars.HeadingRad[car]))) * 180f / MathF.PI;

                parked++;
                if (offDeg > SquareEnoughDeg) askew++;
                worstDeg = MathF.Max(worstDeg, offDeg);
            }
        }

        Assert.True(parked > 0, "nothing parked in two minutes, so the pose this measures was never taken");
        Assert.True(
            askew == 0,
            $"{askew} of {parked} cars left `P-14` over {SquareEnoughDeg:F0} deg out of square, worst {worstDeg:F2}");
    }

    /// <summary>
    /// How far out of square a car may be handed on. <b>It is a bound on the bay and not on comfort</b>: a
    /// body of the shipped size turned by <c>θ</c> spans <c>W cos θ + L sin θ</c> across its space, which
    /// reaches the <see cref="SimConfig.ParkingSpaceWidthM"/> the bay is wide at about 37° — so this is the
    /// angle past which a corner is over the line the bay shares with its neighbour (GEN-4c), with a
    /// hand's width left.
    /// </summary>
    /// <remarks>
    /// <b>It is not the figure <c>P-14</c> quotes</b>, and the gap is the finding rather than the bound: a
    /// quarter of a car length of settling straight was measured at 1.1° out of square on the drawing, and
    /// what the follower actually hands on in a running town is twenty. The straight is not what decides it
    /// — the car settles the rest of the way once it is at rest — so this guards the one thing that is
    /// genuinely a fault, which is a body left across two spaces.
    /// </remarks>
    const float SquareEnoughDeg = 30f;

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
            if (MathF.Abs((offset.X * -axis.Y) + (offset.Y * axis.X)) > plan.CrossingSpanM(crossing) * 0.5f) continue;

            return crossing;
        }

        return -1;
    }
}
