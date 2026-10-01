using System.Collections.Concurrent;
using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The reservations asked of a running town: that the physical layer puts every body on the ways its
/// collider stands over, and that the planned layer reaches as far as its holders mean to go and no further.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P1)]
public class LaneOccupancyInATownTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>A body on its line is a car that has a line</b> (TER-4c.2). What tells a queue from an obstruction
    /// is the holder saying it is travelling the way, so a body that says so with no line under it is a
    /// reading taken from nothing — which is how a queue starts being read as something to drive round.
    /// </summary>
    [Fact]
    public void EveryBodyOnItsLineIsACarWithALine()
    {
        var world = Run(Towns.City);

        Span<LaneClaim> bodies = stackalloc LaneClaim[64];
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            var count = world.Occupancy.CopyBodiesTo(way, bodies);
            for (var slot = 0; slot < count; slot++)
            {
                if (bodies[slot].Of != LaneRoster.Driving || !bodies[slot].OnItsLine) continue;

                var car = bodies[slot].Occupant;
                Assert.True(
                    world.Cars.Line[car].LaneCount > 0,
                    $"car {car} is a body on its line on way {way} and has no line");
            }
        }
    }

    /// <summary>
    /// <b>A car on the last lane of its line is going on down it until it is at the line's end</b> (TER-4c.2). A
    /// line is laid a sight distance ahead, so on a long lane the lane under a car is often the last its line has
    /// yet — and read as its line ending there, a queue on it was a car going nowhere to everybody behind.
    /// </summary>
    /// <remarks>
    /// Read on every laying of the claims over a minute of the town, since whether a car is on the last lane of its line
    /// at any one of them is the town's own business.
    /// </remarks>
    [Fact]
    public void ACarOnTheLastLaneOfItsLineIsGoingOnDownIt()
    {
        using var world = new TownWorld(Towns.Of(Towns.City), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var cars = world.Cars;

        Span<LaneClaim> bodies = stackalloc LaneClaim[64];
        var read = 0;
        for (var tick = 0; tick < TicksWatched; tick++)
        {
            loop.Advance(1);
            if (!world.ClaimsLaidThisTick) continue;

            for (var car = 0; car < cars.Count; car++)
            {
                var lanes = cars.Line[car].LaneCount;
                if (lanes == 0 || cars.StopsForBayOf(car) != CarFleet.NoBay) continue;

                // A car's length of line left past its nose, so the tick driven since the bodies were laid cannot
                // have carried it to the end.
                ref readonly var build = ref cars.BuildOf(car);
                if (cars.Line[car].LengthM - cars.ProgressM[car] - build.NoseAheadOfAxleM < build.LengthM) continue;

                var lastWay = world.Ways.OfRoadLane(cars.ChainOf(car)[lanes - 1]);
                var count = world.Occupancy.CopyBodiesTo(lastWay, bodies);
                for (var slot = 0; slot < count; slot++)
                {
                    if (bodies[slot].Occupant != car || bodies[slot].Of != LaneRoster.Driving || !bodies[slot].OnItsLine) continue;

                    read++;
                    Assert.True(
                        bodies[slot].Onward == LaneOccupancy.RunsOn,
                        $"car {car} is on the last lane of its line with {cars.Line[car].LengthM - cars.ProgressM[car]:F1} m "
                        + $"of it to run at tick {tick}, and was laid going on to {bodies[slot].Onward}");
                }
            }
        }

        if (read == 0) Assert.Fail("the town had no car on the last lane of its line with the line running on");
    }

    /// <summary>
    /// <b>A body holds the ground it stands on whatever it is doing</b> (TER-4c.2) — including a car with a
    /// hand at its wheel, which is a driver by every field the fleet carries and is on no line the town laid.
    /// </summary>
    [Fact]
    public void ACarUnderAHandHoldsTheGroundItIsStandingOn()
    {
        using var world = new TownWorld(Towns.Of(Towns.City), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        var driver = -1;
        var box = CarFleet.NoWay;
        for (var tick = 0; tick < TicksWatched && driver < 0; tick++)
        {
            loop.Advance(1);
            for (var car = 0; car < world.Cars.Count && driver < 0; car++)
            {
                if (!world.Cars.Driven[car] || world.Cars.Broken[car] || !world.Cars.InsideTheBox[car]) continue;

                box = TheJoinItStandsOn(world, car);
                if (box != CarFleet.NoWay) driver = car;
            }
        }

        Assert.True(driver >= 0, "nobody in a busy town was inside a junction on a movement of its own");

        // A hand at the wheel, on the handbrake: the car stands where the route left it — in the box.
        world.Select(new Selection(SelectionKind.Car, driver));
        world.Hands(new HandInput(Held: true, Throttle: 0f, Steer: 0f, Handbrake: true, WalkDirection: Vector2.Zero));
        Claims.UntilLaid(loop);

        Assert.True(world.HandsOn, "the hand never reached the wheel");
        Assert.True(
            LengthHeldOn(world, box, driver) > 0f,
            $"car {driver} stands in the box on way {box} under a hand and is no body there");
    }

    /// <summary>
    /// <b>A body holds what its collider covers of a way and no more</b> (TER-4c.2): lying across a lane it
    /// takes its own width of it, where lying along the lane it takes its length — each to within the
    /// half step either side a lattice point stands for.
    /// </summary>
    [Fact]
    public void ABodyAcrossALaneTakesItsWidthOfItAndNotItsLength()
    {
        using var world = new TownWorld(Towns.Of(Towns.City), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(1);

        var lane = AQuietLane(world);
        var way = world.Ways.OfRoadLane(lane);
        var collider = world.Cars.BuildOf(TheBodyToStand).CollisionSizeM;

        StandTheBodyOn(world, lane, 0f, out _);
        var alongTheLaneM = LengthHeldOn(world, way, TheBodyToStand);

        StandTheBodyOn(world, lane, 0f, out _, MathF.PI * 0.5f);
        var acrossTheLaneM = LengthHeldOn(world, way, TheBodyToStand);

        Assert.InRange(alongTheLaneM, collider.X, collider.X + Config.RibbonLatticeStepM);
        Assert.InRange(acrossTheLaneM, collider.Y, collider.Y + Config.RibbonLatticeStepM);
    }

    /// <summary>
    /// <b>A body over the line between two lanes is on both of them</b> (TER-4c.2): touching a ribbon is
    /// being on its way, and how much of it the body has taken is not the question the write asks.
    /// </summary>
    [Fact]
    public void ABodyOverTheLineBetweenTwoLanesIsOnBothOfThem()
    {
        using var world = new TownWorld(Towns.Of(Towns.City), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(1);

        var lane = AQuietLane(world, needsTheLaneBack: true);
        var back = world.Roads.LaneReverse[lane];

        // Half a lane over, which is the paint: the lane running back is on the offside (TER-4a), so the
        // step towards it is against the way this lane's own right hand points.
        var car = StandTheBodyOn(world, lane, -Config.LaneOffsetM * Config.RoadSideSign, out _);

        Assert.True(
            LengthHeldOn(world, world.Ways.OfRoadLane(lane), car) > 0f,
            $"a body on the line between lanes {lane} and {back} holds none of lane {lane}");
        Assert.True(
            LengthHeldOn(world, world.Ways.OfRoadLane(back), car) > 0f,
            $"a body on the line between lanes {lane} and {back} holds none of lane {back}");
    }

    /// <summary>
    /// <b>And a body up to the paint is on the lane it is in and on no other</b> (TER-4c.2): a lane's
    /// ribbon ends at the paint, so a body has to reach into the lane running back — and not merely to its
    /// edge — to be on it.
    /// </summary>
    [Fact]
    public void ABodyUpToThePaintIsOnOnlyTheLaneItIsIn()
    {
        using var world = new TownWorld(Towns.Of(Towns.City), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(1);

        var lane = AQuietLane(world, needsTheLaneBack: true);
        var back = world.Roads.LaneReverse[lane];
        var halfWidthM = world.Cars.BuildOf(TheBodyToStand).CollisionSizeM.Y * 0.5f;

        var car = StandTheBodyOn(world, lane, -(Config.LaneOffsetM - halfWidthM) * Config.RoadSideSign, out _);

        Assert.True(
            LengthHeldOn(world, world.Ways.OfRoadLane(lane), car) > 0f,
            $"a body up to lane {lane}'s paint holds none of the lane it is standing in");
        Assert.Equal(0f, LengthHeldOn(world, world.Ways.OfRoadLane(back), car));
    }

    /// <summary>
    /// <b>And of a lane it only clips it holds the clip</b> (TER-4c.2): what a body covers of a way is the
    /// part of its collider over that way's ribbon, never the shadow the whole box casts down the line.
    /// </summary>
    [Fact]
    public void ABodyClippingALanesCornerHoldsTheCornerAndNotItsOwnShadow()
    {
        using var world = new TownWorld(Towns.Of(Towns.City), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(1);

        var lane = AQuietLane(world, needsTheLaneBack: true);
        var back = world.Ways.OfRoadLane(world.Roads.LaneReverse[lane]);

        // Turned across its own line and leaning a quarter of a lane towards the lane back, so that one
        // corner is over that lane's ribbon and the rest of the body is not.
        StandTheBodyOn(world, lane, -Config.LaneOffsetM * 0.5f * Config.RoadSideSign, out _, MathF.PI / 3f);
        var clippedM = LengthHeldOn(world, back, TheBodyToStand);
        var standingInM = LengthHeldOn(world, world.Ways.OfRoadLane(lane), TheBodyToStand);

        Assert.True(clippedM > 0f, "a body reaching into the lane back holds none of it");
        Assert.True(
            clippedM < standingInM,
            $"a body reaching lane {world.Roads.LaneReverse[lane]} by a corner holds {clippedM:0.00} m of it, "
            + $"as much as the {standingInM:0.00} m it holds of lane {lane}, which it is standing in");
    }

    /// <summary>
    /// <b>A car on the hook holds the ground it is dragged over, under the vehicle pulling it</b> (EVA-5,
    /// TER-4c.2): a coupled pair is one movement and so one occupant, which is what keeps a truck's own plan
    /// off the trailer behind it.
    /// </summary>
    [Fact]
    public void ACarOnTheHookHoldsItsGroundUnderTheVehiclePullingIt()
    {
        using var world = new TownWorld(Towns.Of(Towns.City), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(1);

        Assert.True(world.Cars.Count >= 2, "a tow needs two vehicles and the town stood fewer");
        const int hauler = 0;
        const int wreck = 1;

        var lane = AQuietLane(world);
        var alongM = world.Roads.LaneLengthM[lane] * 0.5f;
        var on = Spline.SampleAt(world.Roads.ArcsOf(lane), alongM);
        world.Recovery.OnTheHookOf[wreck] = hauler;
        world.Recovery.Towing[hauler] = wreck;

        world.Cars.Driven[hauler] = false;
        world.Cars.VelocityMps[hauler] = Vector2.Zero;
        world.Cars.PositionM[hauler] =
            Spline.SampleAt(world.Roads.ArcsOf(lane), alongM + world.Cars.BuildOf(hauler).LengthM).PositionM;
        world.Cars.HeadingRad[hauler] = MathF.Atan2(on.Direction.Y, on.Direction.X);
        world.Cars.Driven[wreck] = false;
        world.Cars.Broken[wreck] = true;
        world.Cars.VelocityMps[wreck] = Vector2.Zero;
        world.Cars.PositionM[wreck] = on.PositionM;
        world.Cars.HeadingRad[wreck] = MathF.Atan2(on.Direction.Y, on.Direction.X) + (MathF.PI * 0.5f);
        world.LayTheClaims();

        var way = world.Ways.OfRoadLane(lane);
        Assert.Equal(hauler, BodyOn(world, way, alongM));
        Assert.Equal(0f, LengthHeldOn(world, way, wreck));
    }

    /// <summary>
    /// <b>A body whose nose is over the metre its lane is left at is on the movement beyond it</b>
    /// (TER-4c.2, TER-5d): past that metre the ground is the join's, so a body reaching past it is standing
    /// in the junction whatever its middle is doing.
    /// </summary>
    [Fact]
    public void ABodyWithItsNoseOverTheEndOfItsLaneIsOnTheMovementBeyondIt()
    {
        using var world = new TownWorld(Towns.Of(Towns.City), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(1);

        var roads = world.Roads;
        var lane = AQuietLaneOntoAJunction(world);
        Assert.True(lane >= 0, "the town has no quiet lane running into a junction");

        // A metre short of the lane's own end, so that the body is on the lane and its nose is not.
        var on = Spline.SampleAt(roads.ArcsOf(lane), roads.LaneLengthM[lane] - 1f);
        var car = StandTheBodyAt(world, on.PositionM, MathF.Atan2(on.Direction.Y, on.Direction.X));
        Assert.True(
            world.Cars.BuildOf(car).CollisionSizeM.X * 0.5f > 1f + Config.RibbonLatticeStepM,
            "a body shorter than the metre it stands back is no test");

        var way = roads.WayOfConnector(roads.ConnectorsFrom(lane)[0]);
        Assert.True(LengthHeldOn(world, way, car) > 0f, $"a body with its nose over the end of lane {lane} is not on join {way}");
    }

    /// <summary>
    /// <b>A car standing on a zebra is on the crossing way and the lane under it alike</b> (TER-4c.2): the
    /// paint is ground two networks name, and a body on two ways is on two ways.
    /// </summary>
    [Fact]
    public void ACarOnAZebraIsOnTheCrossingWayUnderIt()
    {
        using var world = new TownWorld(Towns.Of(Towns.City), Config);

        var crossing = ACrossingWay(world);
        Assert.True(crossing >= 0, "the town painted no zebra to stand a car on");

        // Square across the paint, which is how a car meets one: along the road the zebra is painted over.
        var alongM = world.Ways.LengthM(crossing) * 0.5f;
        var on = Spline.SampleAt(world.LineOfWay(crossing, out _), alongM);
        var across = Heading.RightOf(on.Direction);
        var car = StandTheBodyAt(world, on.PositionM, MathF.Atan2(across.Y, across.X));

        Assert.Equal(car, BodyOn(world, crossing, alongM));
        Assert.True(
            HoldsAWayOfKind(world, car, WayKind.Lane),
            $"car {car} stands on crossing way {crossing} and is on no lane under it");
    }

    /// <summary>
    /// <b>Nobody is two stretches of one way</b> (TER-5c.2): a body is one stretch of each way it is on, and
    /// a holder's own pieces of one way never lie over each other — or every walk of the way counts one
    /// holder twice.
    /// </summary>
    /// <remarks>
    /// A holder's secondary claim may lie over a main claim of its own: it is the same holder's ground reached
    /// two ways, and nothing is ever held against its own holder.
    /// </remarks>
    [Fact]
    public void NobodyIsTwoStretchesOfOneWay()
    {
        var world = Run(Towns.City);

        Span<LaneClaim> slots = stackalloc LaneClaim[128];
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            OnceEach(world.Occupancy.CopyBodiesTo(way, slots), slots, way, "body");

            var count = world.Occupancy.CopyPlannedTo(way, slots);
            var pieces = 0;
            for (var slot = 0; slot < count; slot++)
            {
                if (!slots[slot].Secondary) slots[pieces++] = slots[slot];
            }

            OnceEach(pieces, slots, way, "plan");
        }

        static void OnceEach(int count, Span<LaneClaim> slots, int way, string layer)
        {
            for (var one = 0; one < count; one++)
            {
                for (var other = one + 1; other < count; other++)
                {
                    if (slots[one].Occupant != slots[other].Occupant || slots[one].Of != slots[other].Of) continue;

                    Assert.False(
                        slots[one].ToM > slots[other].FromM && slots[one].FromM < slots[other].ToM,
                        $"{slots[one].Of} {slots[one].Occupant} is two {layer} stretches of way {way}: "
                        + $"{slots[one].FromM:0.00}–{slots[one].ToM:0.00} m and "
                        + $"{slots[other].FromM:0.00}–{slots[other].ToM:0.00} m");
                }
            }
        }
    }

    /// <summary>
    /// <b>Nobody plans further than it could get in the planned run</b> (TER-4c.1): what a car means to use is
    /// as far as it reaches pulling up to the speed it is planning for over that run, and a stop from there —
    /// so however empty the street, no plan is longer than the car's own top speed held for the run, a stop
    /// from it and the margin it keeps.
    /// </summary>
    /// <remarks>
    /// <b>The ceiling is the car's own figures</b>, because every term of the plan can only lower the ask. A
    /// plan past it is a street shut to everybody crossing it by a car that could never have been there.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NobodyPlansFurtherThanThePlannedRunTakesIt(string map)
    {
        var world = Run(map);

        for (var car = 0; car < world.Cars.Count; car++)
        {
            var plannedM = world.Cars.ClaimToM[car] - world.Cars.ClaimFromM[car];
            if (plannedM <= 0f) continue;

            ref readonly var build = ref world.Cars.BuildOf(car);
            var brakingMps2 = CarFollower.BrakingMps2(Config, build, world.Cars.GroundCoefficient[car]);
            var topMps = build.MaxSpeedMps;
            var ceilingM =
                (topMps * Config.Driving.PlannedRunS) + (topMps * topMps / (2f * brakingMps2)) + Config.Driving.StandOffM;

            Assert.True(
                plannedM <= ceilingM + Tolerance,
                $"{map}: car {car} plans {plannedM:0.0} m of road, past the {ceilingM:0.0} m its top speed takes it");
        }
    }

    /// <summary>
    /// <b>A car at rest plans no more than the room to pull away</b> (TER-5g): a reaction interval of its own
    /// full throttle, a stop from there and what it keeps off what cut it — its stand-off, or the room to step
    /// out it keeps while it stands (CAR-46) — so a queue waiting at a junction plans none of the box it is
    /// waiting for.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ACarAtRestPlansOnlyTheRoomToPullAway(string map)
    {
        var world = Run(map);

        for (var car = 0; car < world.Cars.Count; car++)
        {
            var plannedM = world.Cars.ClaimToM[car] - world.Cars.ClaimFromM[car];
            if (plannedM <= 0f || NoseInABox(world, car)) continue;

            // The speed at the rebuild and not the speed now: the plans were laid at the top of this tick and
            // the body has been driven since, so a tick of its own braking is the whole of the difference.
            ref readonly var build = ref world.Cars.BuildOf(car);
            var askedAtMps = world.Cars.AlongMps[car] + (build.BrakingMps2 * Config.TickSeconds);
            if (askedAtMps > Config.Driving.StopSpeedMps) continue;

            var brakingMps2 = CarFollower.BrakingMps2(Config, build, world.Cars.GroundCoefficient[car]);
            var pulledToMps = MathF.Max(0f, askedAtMps) + (build.AccelerationMps2 * Config.CarReactionS);
            var roomM = (pulledToMps * Config.CarReactionS) + (pulledToMps * pulledToMps / (2f * brakingMps2))
                        + MathF.Max(Config.Driving.StandOffM, world.Cars.GrantMarginM[car]);

            Assert.True(
                plannedM <= roomM + Tolerance,
                $"{map}: car {car} is at rest and plans {plannedM:0.0} m of road, past the {roomM:0.0} m it needs to "
                + "pull away");
        }
    }

    /// <summary>
    /// <b>And a moving car with the road to itself plans past what it is committed to</b> (TER-5g): planning
    /// for at least the speed it is doing, with nothing holding it short and line still to run over, it says
    /// where it means to be able to stop and not only where it no longer can.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void AMovingCarWithTheRoadToItselfPlansPastWhatItIsCommittedTo(string map)
    {
        var world = Run(map);

        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Cars.ClaimToM[car] <= world.Cars.ClaimFromM[car]) continue;
            if (world.Cars.ClaimToM[car] >= world.Cars.Line[car].LengthM - Tolerance) continue;
            if (world.Cars.CommittedToTheBox[car] || NoseInABox(world, car)) continue;

            ref readonly var build = ref world.Cars.BuildOf(car);
            var alongMps = world.Cars.AlongMps[car];
            if (alongMps <= Config.Driving.StopSpeedMps + (build.AccelerationMps2 * Config.TickSeconds)) continue;
            if (world.Cars.PlannedMps[car] < alongMps) continue;

            Assert.True(
                world.Cars.ClaimToM[car] > world.Cars.CommittedToM[car],
                $"{map}: car {car} is doing {alongMps:0.0} m/s with nothing stopping it and plans only the "
                + $"{world.Cars.CommittedToM[car] - world.Cars.ClaimFromM[car]:0.0} m it is committed to");
        }
    }

    /// <summary>
    /// <b>No plan reaches further than a plan may</b> (TER-4c.1): however empty the street, no further than any plan
    /// reaches, nor than the car could stop from its own top speed — and since the ground a car can no longer stop
    /// short of is never cut, a car that holds no more than that is one that drove to stop by the end of its plan.
    /// </summary>
    /// <remarks>
    /// A car in a box plans its way out of it whatever this says, and one on a pass plans from where the pass ends.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoPlanReachesFurtherThanAPlanMay(string map)
    {
        var world = Run(map);

        for (var car = 0; car < world.Cars.Count; car++)
        {
            var plannedM = world.Cars.ClaimToM[car] - world.Cars.ClaimFromM[car];
            if (plannedM <= 0f || NoseInABox(world, car) || world.Cars.Pass[car].Begun) continue;

            var mostM = MathF.Min(Config.Driving.PlanMostM, world.Cars.BuildOf(car).SightM);
            Assert.True(
                plannedM <= mostM + Tolerance,
                $"{map}: car {car} plans {plannedM:0.0} m of road, past the {mostM:0.0} m a plan may reach");
        }
    }

    /// <summary>
    /// <b>No plan passes more joins that break its line than a plan may</b> (TER-4c.1): a turn, or a join that bends
    /// the road on, is somewhere the road ahead stops being the road the car is on, and a plan runs through no more
    /// of them than <see cref="DrivingFigures.PlanMostJoins"/>.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoPlanPassesMoreJoinsThatBreakItsLineThanAPlanMay(string map)
    {
        var world = Run(map);

        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Cars.ClaimToM[car] <= world.Cars.ClaimFromM[car] || NoseInABox(world, car)) continue;

            var ends = world.Cars.LaneEndsOf(car);
            var breaks = world.Cars.JoinBreaksOf(car);
            var passed = 0;
            for (var slot = 0; slot + 1 < world.Cars.Line[car].LaneCount; slot++)
            {
                if (breaks[slot] && ends[slot] > world.Cars.ClaimFromM[car] && ends[slot] < world.Cars.ClaimToM[car]) passed++;
            }

            Assert.True(
                passed <= Config.Driving.PlanMostJoins,
                $"{map}: car {car} plans through {passed} joins that break its line");
        }
    }

    /// <summary>Whether a car's nose is past the mouth of the box ahead of it, where its plan runs to the far side of the join.</summary>
    static bool NoseInABox(TownWorld world, int car) => world.Cars.InsideTheBox[car] || world.Cars.ToTheBoxM[car] <= 0f;

    /// <summary>The car these place, moved out of whatever the town had it doing and stood where the test wants it.</summary>
    const int TheBodyToStand = 1;

    /// <summary>
    /// One body stood still on a lane, <paramref name="acrossM"/> to the right of that lane's own line and
    /// square to it, with the reservations rebuilt around it.
    /// </summary>
    static int StandTheBodyOn(TownWorld world, int lane, float acrossM, out float alongM, float turnedRad = 0f)
    {
        alongM = world.Roads.LaneLengthM[lane] * 0.5f;
        var on = Spline.SampleAt(world.Roads.ArcsOf(lane), alongM);

        return StandTheBodyAt(
            world, on.PositionM + (Heading.RightOf(on.Direction) * acrossM),
            MathF.Atan2(on.Direction.Y, on.Direction.X) + turnedRad);
    }

    /// <summary>The same body stood still at a pose of its own, with the reservations rebuilt around it.</summary>
    static int StandTheBodyAt(TownWorld world, Vector2 atM, float headingRad)
    {
        world.Cars.Driven[TheBodyToStand] = false;
        world.Cars.Broken[TheBodyToStand] = true;
        world.Cars.VelocityMps[TheBodyToStand] = Vector2.Zero;
        world.Cars.PositionM[TheBodyToStand] = atM;
        world.Cars.HeadingRad[TheBodyToStand] = headingRad;
        world.LayTheClaims();
        return TheBodyToStand;
    }

    /// <summary>One of the ways a town's zebras are walked, or <c>-1</c> where it painted none.</summary>
    static int ACrossingWay(TownWorld world)
    {
        for (var way = 0; way < world.Ways.Count; way++)
        {
            if (world.Ways.KindOf(way) != WayKind.Footway) continue;
            if (world.Foot.KindOf(world.Ways.FootwayOf(way)) != FootEdgeKind.Crossing) continue;

            return way;
        }

        return -1;
    }

    /// <summary>Whether this body is on any way of that kind.</summary>
    static bool HoldsAWayOfKind(TownWorld world, int car, WayKind kind)
    {
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            if (world.Ways.KindOf(way) != kind) continue;
            if (LengthHeldOn(world, way, car) > 0f) return true;
        }

        return false;
    }

    /// <summary>A lane nobody is on that runs into a junction its first movement has a line of its own over.</summary>
    static int AQuietLaneOntoAJunction(TownWorld world)
    {
        var roads = world.Roads;

        Span<LaneClaim> slots = stackalloc LaneClaim[1];
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            if (roads.LaneLengthM[lane] < 60f || roads.LanesFrom(lane).Length == 0) continue;

            var slot = roads.ConnectorsFrom(lane)[0];
            if (roads.ConnectorArcs(slot).Length == 0) continue;

            var next = roads.ConnectorTo(slot);
            if (world.Occupancy.CopyTo(world.Ways.OfRoadLane(lane), slots) != 0) continue;
            if (world.Occupancy.CopyTo(world.Ways.OfRoadLane(next), slots) != 0) continue;
            if (world.Occupancy.CopyTo(roads.WayOfConnector(slot), slots) != 0) continue;

            return lane;
        }

        return -1;
    }

    /// <summary>A straight-enough lane of the town nobody is on, so that one body put there is the only answer it can give.</summary>
    static int AQuietLane(TownWorld world, bool needsTheLaneBack = false)
    {
        Span<LaneClaim> slots = stackalloc LaneClaim[1];
        for (var lane = 0; lane < world.Roads.LaneCount; lane++)
        {
            var back = world.Roads.LaneReverse[lane];
            if (world.Roads.LaneLengthM[lane] < 60f || (needsTheLaneBack && back < 0)) continue;
            if (world.Occupancy.CopyTo(world.Ways.OfRoadLane(lane), slots) != 0) continue;
            if (needsTheLaneBack && world.Occupancy.CopyTo(world.Ways.OfRoadLane(back), slots) != 0) continue;
            if (!StraightAtItsMiddle(world.Roads.ArcsOf(lane), world.Roads.LaneLengthM[lane] * 0.5f)) continue;

            return lane;
        }

        Assert.Fail("the town has no quiet lane straight at its middle to stand a body on");
        return -1;
    }

    /// <summary>
    /// Whether the arc under the middle of a lane runs straight for a car's length either side — a body stood
    /// on a bend covers more of the way round it than its own length, which is another reading.
    /// </summary>
    static bool StraightAtItsMiddle(ReadOnlySpan<ArcSeg> arcs, float middleM)
    {
        var startM = 0f;
        foreach (var arc in arcs)
        {
            var endM = startM + arc.LengthM;
            if (middleM >= startM && middleM <= endM)
            {
                return MathF.Abs(arc.Curvature) < 1e-6f && middleM - startM >= 5f && endM - middleM >= 5f;
            }

            startM = endM;
        }

        return false;
    }

    /// <summary>Which car's body is on one place of one way, or <see cref="LaneOccupancy.Nobody"/>.</summary>
    static int BodyOn(TownWorld world, int way, float atM)
    {
        Span<LaneClaim> slots = stackalloc LaneClaim[64];
        var count = world.Occupancy.CopyBodiesTo(way, slots);
        for (var slot = 0; slot < count; slot++)
        {
            if (slots[slot].Of != LaneRoster.Driving) continue;
            if (slots[slot].FromM <= atM && slots[slot].ToM >= atM) return slots[slot].Occupant;
        }

        return LaneOccupancy.Nobody;
    }

    /// <summary>How much of one way a car's body covers, which for one body is one stretch (TER-5c.2).</summary>
    /// <summary>A join of a box this car's body stands on, or <see cref="CarFleet.NoWay"/>.</summary>
    static int TheJoinItStandsOn(TownWorld world, int car)
    {
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            if (world.Ways.KindOf(way) == WayKind.Connector && LengthHeldOn(world, way, car) > 0f) return way;
        }

        return CarFleet.NoWay;
    }

    static float LengthHeldOn(TownWorld world, int way, int car)
    {
        Span<LaneClaim> slots = stackalloc LaneClaim[64];
        var count = world.Occupancy.CopyBodiesTo(way, slots);
        for (var slot = 0; slot < count; slot++)
        {
            if (slots[slot].Occupant == car && slots[slot].Of == LaneRoster.Driving) return slots[slot].ToM - slots[slot].FromM;
        }

        return 0f;
    }

    /// <summary>Ground on a way is metres, and a plan is arithmetic on floats: a centimetre is not a finding.</summary>
    const float Tolerance = 1e-2f;

    public static TheoryData<string> Maps => Towns.EveryTown();

    static readonly ConcurrentDictionary<string, TownWorld> Ran = new();

    /// <summary>
    /// <b>The town a minute in, taken once per map and read by every question that asks about the same
    /// moment.</b> Nothing here writes to the world it is handed.
    /// </summary>
    static TownWorld Run(string map) => Ran.GetOrAdd(map, opened =>
    {
        var world = new TownWorld(Towns.Of(opened), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(TicksWatched);
        Claims.UntilLaid(loop);
        return world;
    });

    /// <summary>A minute of town, which is long enough for every kind of hold to have happened on every map.</summary>
    const int TicksWatched = 3_600;
}
