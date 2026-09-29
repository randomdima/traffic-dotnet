using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.World.Foot;
using TrafficSimulation.Bench;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>A car getting past what stands in its lane</b> (CAR-46, TER-4c.6), staged on the fixture: a wreck put down
/// ahead of a car driving a quiet two-way street, and what the car makes of it.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P2)]
public class OvertakeInATownTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>A car gets past a wreck in its lane over the lane beside</b>, and is back in its own lane past it having
    /// touched nothing: the lane beside was free for the whole of the pass, so nothing was ever there to touch.
    /// </summary>
    [Fact]
    public void ACarGetsPastAWreckInItsLaneWithoutTouchingAnything()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WarmUpTicks);

        var (driver, lane, clearsM, _, _) = StageAWreckAhead(world, alsoInTheLaneBeside: false);

        var furthestM = float.NegativeInfinity;
        var overTheLaneBeside = false;
        var touched = false;
        for (var tick = 0; tick < WatchedTicks && world.Cars.LaneOf(driver) == lane; tick++)
        {
            loop.Advance(1);
            if (world.Cars.LaneOf(driver) == lane) furthestM = MathF.Max(furthestM, world.Cars.ProgressM[driver]);
            overTheLaneBeside |= world.Cars.Pass[driver].Begun;
            touched |= world.PhysicsForInstruments.OverlapOf(world.Cars.Body[driver]) > SoakProbe.OverlapAllowanceM;
        }

        var tailPastM = furthestM - world.Cars.BuildOf(driver).TailBehindAxleM;
        Assert.True(
            overTheLaneBeside,
            $"car {driver} never went over the lane beside lane {lane}; it was held by {world.Cars.GrantCutBy[driver]}");
        Assert.True(
            tailPastM > clearsM,
            $"car {driver} got its tail to {tailPastM:F1} m of lane {lane}, and the wreck ends at {clearsM:F1} m");
        Assert.False(touched, $"car {driver} touched something on its way past");
    }

    /// <summary>
    /// <b>A pass holds all of the ground it covers from where it starts</b> (TER-4c.6): every tick of it, the car's
    /// body stands on nothing but what its pass held the tick it began — to within the spare it was asked for with,
    /// which is as far as a car driving it is let stray.
    /// </summary>
    [Fact]
    public void ACarOnAPassStandsOnlyOnTheGroundItsPassHeldWhenItBegan()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WarmUpTicks);

        var (driver, _, _, _, _) = StageAWreckAhead(world, alsoInTheLaneBeside: false);
        var cars = world.Cars;

        List<(int Way, float FromM, float ToM)>? held = null;
        Span<WayCover> under = stackalloc WayCover[16];
        for (var tick = 0; tick < WatchedTicks && (held == null || cars.Pass[driver].Begun); tick++)
        {
            loop.Advance(1);
            if (!cars.Pass[driver].Begun) continue;

            held ??= TheGroundItsPassHolds(world, driver, LaneRoster.Driving);
            var halfM = cars.BuildOf(driver).CollisionSizeM * 0.5f;
            var count = world.Atlas.UnderBox(
                cars.PositionM[driver], Heading.Unit(cars.HeadingRad[driver]), halfM.X, halfM.Y, under);
            var off = OffTheGround(held, under[..count], Config.Driving.PassSpareM);
            Assert.True(off == null, $"car {driver} on tick {tick} of its pass stood on {off}");
        }

        Assert.True(held != null, $"car {driver} never began a pass; it was held by {cars.GrantCutBy[driver]}");
    }

    /// <summary>
    /// And a walker's: every tick of its pass, its body stands on nothing but what its pass held the tick it began,
    /// to within a tick's walk.
    /// </summary>
    [Fact]
    public void AWalkerOnAPassStandsOnlyOnTheGroundItsPassHeldWhenItBegan()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WalkersOutTicks);

        var (walker, _, _) = StandSomebodyAhead(world);
        var people = world.People;

        List<(int Way, float FromM, float ToM)>? held = null;
        Span<WayCover> under = stackalloc WayCover[16];
        for (var tick = 0; tick < WatchedTicks && (held == null || people.Pass[walker].Begun); tick++)
        {
            loop.Advance(1);
            if (!people.Pass[walker].Begun) continue;

            held ??= TheGroundItsPassHolds(world, walker, LaneRoster.Walking);
            var count = world.Atlas.UnderDisc(people.PositionM[walker], people.RadiusM[walker], under);
            var off = OffTheGround(held, under[..count], Config.PersonStepM);
            Assert.True(off == null, $"walker {walker} on tick {tick} of its pass stood on {off}");
        }

        Assert.True(held != null, $"walker {walker} never began a pass");
    }

    /// <summary>Every stretch of every way the town holds under one holder's pass this tick.</summary>
    static List<(int Way, float FromM, float ToM)> TheGroundItsPassHolds(TownWorld world, int holder, LaneRoster of)
    {
        var held = new List<(int Way, float FromM, float ToM)>();
        var bodies = new LaneClaim[64];
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            var count = world.Occupancy.CopyBodiesTo(way, bodies);
            for (var at = 0; at < count; at++)
            {
                if (bodies[at].Passing && bodies[at].Occupant == holder && bodies[at].Of == of)
                {
                    held.Add((way, bodies[at].FromM, bodies[at].ToM));
                }
            }
        }

        return held;
    }

    /// <summary>The first stretch of ground a body stands on that none of what was held covers, to within a tolerance — or none.</summary>
    static string? OffTheGround(List<(int Way, float FromM, float ToM)> held, ReadOnlySpan<WayCover> under, float withinM)
    {
        foreach (var cover in under)
        {
            var covered = false;
            foreach (var stretch in held)
            {
                covered |= stretch.Way == cover.Way
                           && stretch.FromM - withinM <= cover.FromM && cover.ToM <= stretch.ToM + withinM;
            }

            if (!covered) return $"way {cover.Way} from {cover.FromM:F2} m to {cover.ToM:F2} m";
        }

        return null;
    }

    /// <summary>
    /// <b>A junction is no end to a pass</b>: a wreck standing just short of a box with no zebra on it — a car
    /// park's, on the town that lays them — is got past through the box, the car coming back onto its own line in
    /// the lane beyond it.
    /// </summary>
    [Fact]
    public void ACarGetsPastAWreckShortOfAJunctionThroughTheBox()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WarmUpTicks);

        var staged = StageAWreckShortOfABox(world);
        for (var tick = 0; tick < WatchedAtAJunctionTicks && staged.Driver < 0; tick += WarmUpTicks)
        {
            loop.Advance(WarmUpTicks);
            staged = StageAWreckShortOfABox(world);
        }

        var (driver, lane, beyond) = staged;
        Assert.True(driver >= 0, "the town had no car on a quiet two-way street going straight on through a box with no zebra");

        var overTheLaneBeside = false;
        var touched = false;
        for (var tick = 0; tick < WatchedAtAJunctionTicks && world.Cars.LaneOf(driver) == lane; tick++)
        {
            loop.Advance(1);
            overTheLaneBeside |= world.Cars.Pass[driver].Begun;
            touched |= world.PhysicsForInstruments.OverlapOf(world.Cars.Body[driver]) > SoakProbe.OverlapAllowanceM;
        }

        Assert.True(
            overTheLaneBeside,
            $"car {driver} never went over the lane beside lane {lane}; it was held by {world.Cars.GrantCutBy[driver]}");
        Assert.True(
            world.Cars.LaneOf(driver) == beyond,
            $"car {driver} left lane {lane} for lane {world.Cars.LaneOf(driver)} and not {beyond}, "
            + $"doing {world.Cars.Hold[driver]} {world.Cars.OffLineM[driver]:F2} m off its line");
        Assert.False(touched, $"car {driver} touched something on its way past");
    }

    /// <summary>
    /// A driven car on a quiet two-way street whose line goes straight on through the box at its end onto another
    /// two-way street, and a wreck put down in its lane <see cref="ShortOfTheBoxM"/> short of that box.
    /// </summary>
    /// <returns>The car, the lane it is on, and the lane beyond the box — or a driver of −1 where there is none yet.</returns>
    static (int Driver, int Lane, int Beyond) StageAWreckShortOfABox(TownWorld world)
    {
        var cars = world.Cars;
        var roads = world.Roads;
        for (var driver = 0; driver < cars.Count; driver++)
        {
            var lane = cars.LaneOf(driver);
            if (!cars.Driven[driver] || cars.Broken[driver] || lane < 0 || cars.Line[driver].LaneCount < 2) continue;

            var beyond = cars.ChainOf(driver)[1];
            var join = roads.ConnectorBetween(lane, beyond);
            if (join == RoadGraph.NoConnector || roads.KindOf(join) != LaneTurn.Straight) continue;

            var back = roads.LaneReverse[lane];
            var beyondBack = roads.LaneReverse[beyond];
            var atM = roads.LaneLengthM[lane] - ShortOfTheBoxM;
            var fromM = cars.ProgressM[driver];
            if (back < 0 || beyondBack < 0 || atM - fromM < WreckAheadM) continue;

            // Quiet over what a pass through the box covers: the end of this street both ways, and the start of the next.
            var backM = roads.LaneLengthM[back];
            var beyondBackM = roads.LaneLengthM[beyondBack];
            if (!IsQuiet(world, driver, lane, fromM) || !IsQuiet(world, driver, back, 0f, backM - fromM)) continue;
            if (!IsQuiet(world, driver, beyond, 0f, PastTheBoxM)) continue;
            if (!IsQuiet(world, driver, beyondBack, beyondBackM - PastTheBoxM)) continue;

            // A box with no zebra on its arms, as a car park's has none (WLK-10): a pass is never taken over the paint.
            var ways = world.Ways;
            if (HasAZebra(world, ways.OfRoadLane(lane), fromM, roads.LaneLengthM[lane])) continue;
            if (HasAZebra(world, ways.OfRoadLane(back), 0f, backM - fromM)) continue;
            if (HasAZebra(world, ways.OfRoadLane(beyond), 0f, PastTheBoxM)) continue;
            if (HasAZebra(world, ways.OfRoadLane(beyondBack), beyondBackM - PastTheBoxM, beyondBackM)) continue;
            if (HasAZebra(world, roads.WayOfConnector(join), 0f, roads.ConnectorLengthM(join))) continue;

            for (var wreck = 0; wreck < cars.Count; wreck++)
            {
                if (wreck == driver) continue;

                var at = Spline.SampleAt(roads.ArcsOf(lane), atM);
                PutDown(world, wreck, at.PositionM, at.HeadingRad);
                return (driver, lane, beyond);
            }
        }

        return (-1, -1, -1);
    }

    /// <summary>
    /// <b>The lane beside must be free</b>: with a second wreck standing in it alongside the first, a car never
    /// asks for a pass, and waits behind what is in its lane.
    /// </summary>
    [Fact]
    public void ACarAsksForNoPassWhileTheLaneBesideIsHeld()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WarmUpTicks);

        var (driver, _, _, _, _) = StageAWreckAhead(world, alsoInTheLaneBeside: true);

        for (var tick = 0; tick < WatchedTicks; tick++)
        {
            loop.Advance(1);
            Assert.False(world.Cars.Pass[driver].Any, $"car {driver} asked for a pass on tick {tick} over a held lane");
        }
    }

    /// <summary>
    /// <b>A car that finds the lane beside free gets past without stopping</b>: its pass is drawn for the speed it is
    /// doing and asked for before it would begin to slow, so it never comes to a stand behind the wreck.
    /// </summary>
    [Fact]
    public void ACarWithTheLaneBesideFreeGetsPastWithoutStopping()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WarmUpTicks);

        var (driver, _, _, _, _) = StageAWreckAhead(world, alsoInTheLaneBeside: false);
        var cars = world.Cars;

        var (moving, began, stoodAtM) = (false, false, float.NaN);
        for (var tick = 0; tick < WatchedTicks && !(began && !cars.Pass[driver].Begun); tick++)
        {
            loop.Advance(1);
            began |= cars.Pass[driver].Begun;
            if (cars.AlongMps[driver] > Config.Driving.StopSpeedMps) moving = true;
            else if (moving && float.IsNaN(stoodAtM)) stoodAtM = cars.ProgressM[driver];
        }

        Assert.True(began, $"car {driver} never began a pass; it was held by {cars.GrantCutBy[driver]}");
        Assert.True(float.IsNaN(stoodAtM), $"car {driver} came to a stand at {stoodAtM:F1} m with the lane beside free");
    }

    /// <summary>
    /// <b>A car held behind a wreck stands where it can step out round it</b>, and no further back: once the lane
    /// beside clears it begins its pass from where it stands, and what it passes is less than a step in front of it —
    /// its step out runs on alongside what it passes rather than being done behind it.
    /// </summary>
    [Fact]
    public void ACarHeldBehindAWreckStepsOutRoundItFromWhereItStands()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WarmUpTicks);

        var (driver, _, clearsM, wreck, beside) = StageAWreckAhead(world, alsoInTheLaneBeside: true);
        var cars = world.Cars;

        var (moving, stoodFor) = (false, 0);
        for (var tick = 0; tick < WatchedTicks && stoodFor < StandsForTicks; tick++)
        {
            loop.Advance(1);
            if (cars.AlongMps[driver] > Config.Driving.StopSpeedMps) (moving, stoodFor) = (true, 0);
            else if (moving) stoodFor++;
        }

        Assert.True(stoodFor >= StandsForTicks, $"car {driver} never stood behind the wreck; it was held by {cars.GrantCutBy[driver]}");

        var stoodAtM = cars.ProgressM[driver];
        PutDown(world, beside, FarOffTheTownM, 0f);
        for (var tick = 0; tick < WatchedTicks && !cars.Pass[driver].Begun; tick++) loop.Advance(1);

        var pass = cars.Pass[driver];
        var wreckTailM = clearsM - cars.BuildOf(wreck).LengthM;
        var noseM = stoodAtM + cars.BuildOf(driver).NoseAheadOfAxleM;
        Assert.True(
            pass.Begun,
            $"car {driver} never began a pass once the lane beside was clear: standing {wreckTailM - noseM:F2} m short of the wreck, "
            + $"at {cars.ProgressM[driver]:F2} m, held by {cars.GrantCutBy[driver]} {cars.AuthorityM[driver]:F2} m on, "
            + $"margin {cars.GrantMarginM[driver]:F2} m, {cars.Hold[driver]}");
        Assert.True(
            pass.OutM - stoodAtM <= Config.Driving.PassSpareM,
            $"car {driver} stood at {stoodAtM:F2} m and stepped out from {pass.OutM:F2} m");
        Assert.True(
            wreckTailM - noseM < pass.StepM,
            $"car {driver} stood {wreckTailM - noseM:F2} m short of the wreck with a step of {pass.StepM:F2} m");
    }

    /// <summary>
    /// <b>A walker gets past somebody standing on its way</b> (PER-28): over onto the lane beside and back onto
    /// its own way past them, having touched nobody.
    /// </summary>
    [Fact]
    public void AWalkerGetsPastSomebodyStandingOnItsWayWithoutTouchingThem()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WalkersOutTicks);

        var (walker, code, clearsM) = StandSomebodyAhead(world);

        var overTheLaneBeside = false;
        var touched = false;
        for (var tick = 0; tick < WatchedTicks && world.People.CurrentRouteWay(walker) == code; tick++)
        {
            loop.Advance(1);
            overTheLaneBeside |= world.People.Pass[walker].Begun;
            touched |= world.PhysicsForInstruments.OverlapOf(world.People.Body[walker]) > SoakProbe.OverlapAllowanceM;
            if (world.People.OnWayM[walker] - world.People.RadiusM[walker] > clearsM && !world.People.Pass[walker].Any) break;
        }

        Assert.True(overTheLaneBeside, $"walker {walker} never stepped over onto the lane beside its way");
        Assert.True(
            world.People.CurrentRouteWay(walker) != code
            || world.People.OnWayM[walker] - world.People.RadiusM[walker] > clearsM,
            $"walker {walker} got to {world.People.OnWayM[walker]:F1} m of its way, and who it passes ends at {clearsM:F1} m");
        Assert.False(touched, $"walker {walker} touched somebody on its way past");
    }

    /// <summary>
    /// A walker out on a stretch of pavement with a lane beside it and room ahead, and somebody else stood still on
    /// its way <see cref="StandsAheadM"/> in front of it, going nowhere.
    /// </summary>
    /// <returns>The walker, the way of its route it is on, and where along that the one standing ends.</returns>
    static (int Walker, int Code, float ClearsM) StandSomebodyAhead(TownWorld world)
    {
        var people = world.People;
        for (var walker = 0; walker < people.Count; walker++)
        {
            var code = people.CurrentRouteWay(walker);
            if (!people.Walking[walker] || people.Inside[walker].Any || people.OnWay[walker] < 0) continue;
            if (WalkingNetwork.IsACorner(code) || world.IsTheCrossing(people.OnWay[walker])) continue;

            var atM = people.OnWayM[walker] + StandsAheadM;
            var endM = people.OnTheLastWay(walker) ? people.RouteToM[walker] : world.Walking.WayLengthM(code);
            if (endM - atM < RoomPastM || float.IsNaN(world.Walking.AsideM(code, people.OnWayM[walker]))) continue;

            for (var standing = 0; standing < people.Count; standing++)
            {
                if (standing == walker || people.Inside[standing].Any || people.Wounded[standing]) continue;

                var at = Spline.SampleAt(world.Walking.WayArcs(code), atM);
                StandStill(world, standing, at.PositionM, at.HeadingRad);
                return (walker, code, atM + people.RadiusM[standing]);
            }
        }

        Assert.Fail("the town had no walker on a stretch of pavement with a lane beside it and room ahead");
        return default;
    }

    /// <summary>Somebody put down and told to stand there: under orders with none left, which is a walker idling.</summary>
    static void StandStill(TownWorld world, int person, Vector2 atM, float headingRad)
    {
        var people = world.People;
        people.ClearRoute(person);
        people.Walking[person] = false;
        people.Manual[person] = true;
        people.Stage[person] = TripStage.UnderOrders;
        people.PositionM[person] = atM;
        people.DestinationM[person] = atM;
        people.GoalM[person] = atM;
        people.VelocityMps[person] = Vector2.Zero;
        people.DeclaredMps[person] = Vector2.Zero;
        people.HeadingRad[person] = headingRad;
        world.PhysicsForInstruments.Release(people.Body[person], atM, headingRad);
    }

    /// <summary>
    /// A driven car on a quiet two-way street with room ahead, and a wreck put down in its lane
    /// <see cref="WreckAheadM"/> in front of it — and, where asked, a second one level with it in the lane beside.
    /// </summary>
    /// <returns>The car, the lane, where along it the wreck ends, the wreck, and the one beside it or −1.</returns>
    static (int Driver, int Lane, float ClearsM, int Wreck, int Beside) StageAWreckAhead(
        TownWorld world, bool alsoInTheLaneBeside)
    {
        var cars = world.Cars;
        var roads = world.Roads;
        for (var driver = 0; driver < cars.Count; driver++)
        {
            var lane = cars.LaneOf(driver);
            if (!cars.Driven[driver] || cars.Broken[driver] || lane < 0 || cars.LineWayOf(driver) >= 0) continue;

            var back = roads.LaneReverse[lane];
            var fromM = cars.ProgressM[driver];
            if (back < 0 || roads.LaneOverOneLine[lane] || roads.LaneLengthM[lane] - fromM < RoomAheadM) continue;
            if (!IsQuiet(world, driver, lane, fromM) || !IsQuiet(world, driver, back, 0f)) continue;

            var atM = fromM + cars.BuildOf(driver).NoseAheadOfAxleM + WreckAheadM;
            var clearsM = float.NaN;
            var (inTheLane, beside) = (-1, -1);
            for (var wreck = 0; wreck < cars.Count && (inTheLane < 0 || (alsoInTheLaneBeside && beside < 0)); wreck++)
            {
                if (wreck == driver) continue;

                var first = inTheLane < 0;
                var on = first ? lane : back;
                var alongM = first ? atM : roads.LaneLengthM[back] - atM;
                var at = Spline.SampleAt(roads.ArcsOf(on), alongM);
                PutDown(world, wreck, at.PositionM, at.HeadingRad);
                if (!first)
                {
                    beside = wreck;
                    continue;
                }

                inTheLane = wreck;
                clearsM = atM + (cars.BuildOf(wreck).LengthM * 0.5f);
            }

            return (driver, lane, clearsM, inTheLane, beside);
        }

        Assert.Fail("the fixture had no car on a quiet two-way street with room ahead of it");
        return default;
    }

    /// <summary>Whether nobody but this car stands on or plans a stretch of a lane — from a place on it to the end, unless told where.</summary>
    static bool IsQuiet(TownWorld world, int driver, int lane, float fromM, float toM = float.PositiveInfinity)
    {
        Span<LaneClaim> slots = stackalloc LaneClaim[64];
        var count = world.Occupancy.CopyTo(world.Ways.OfRoadLane(lane), slots);
        for (var slot = 0; slot < count; slot++)
        {
            if (slots[slot].Occupant != driver && slots[slot].ToM > fromM && slots[slot].FromM < toM) return false;
        }

        return true;
    }

    /// <summary>Whether a zebra's paint lies over a stretch of one way, read off its marks.</summary>
    static bool HasAZebra(TownWorld world, int way, float fromM, float toM)
    {
        foreach (var mark in world.Occupancy.Marks.Of(way))
        {
            if (mark.MineToM > fromM && mark.MineFromM < toM && world.IsTheCrossing(mark.OnWay)) return true;
        }

        return false;
    }

    /// <summary>A car broken where it is put down: nothing drives it, and it holds its ground as a body (TER-4c.2).</summary>
    static void PutDown(TownWorld world, int car, Vector2 atM, float headingRad)
    {
        world.Cars.Driven[car] = false;
        world.Cars.Broken[car] = true;
        world.Cars.Command[car] = DriveCommand.Locked;
        world.Cars.WheelSpinOf(car).Clear();
        world.Cars.PositionM[car] = atM;
        world.Cars.HeadingRad[car] = headingRad;
        world.Cars.VelocityMps[car] = Vector2.Zero;
        world.PhysicsForInstruments.Release(world.Cars.Body[car], atM, headingRad);
    }

    /// <summary>Long enough for every car the fixture stands to have a line.</summary>
    const int WarmUpTicks = 120;

    /// <summary>Twenty seconds: time to come up to a wreck, wait for the lane beside and get past it.</summary>
    const int WatchedTicks = 1_200;

    /// <summary>A second at rest: the car is standing behind what it waits for, and not stopping for a tick.</summary>
    const int StandsForTicks = 60;

    /// <summary>Somewhere no way of any town reaches, to take a wreck out of the road.</summary>
    static readonly Vector2 FarOffTheTownM = new(-10_000f, -10_000f);

    /// <summary>How far in front of the car's nose the wreck is put down: a town speed's stop and more.</summary>
    const float WreckAheadM = 45f;

    /// <summary>How much of its lane a car must have ahead of it for a wreck and the whole of a pass round it.</summary>
    const float RoomAheadM = 140f;

    /// <summary>How far short of the box at a lane's end a wreck is put down: less than a pass needs to come back in.</summary>
    const float ShortOfTheBoxM = 8f;

    /// <summary>How much of the street past the box a pass round a wreck that close to it runs on into.</summary>
    const float PastTheBoxM = 60f;

    /// <summary>A minute: a signalled box is held against a pass through it until its light lets the pass have it.</summary>
    const int WatchedAtAJunctionTicks = 3_600;

    /// <summary>Long enough that the town's people are out of their first dwell and walking.</summary>
    static int WalkersOutTicks => (int)(Config.Building.DwellMaxS * Config.Sim.TickRateHz) + 120;

    /// <summary>How far in front of a walker's middle somebody is stood: a few strides, so it walks up to them first.</summary>
    const float StandsAheadM = 3f;

    /// <summary>How much of its way a walker must have past them for the whole of a pass round them.</summary>
    const float RoomPastM = 4f;
}
