using System.Numerics;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Agents.Service;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Service;

/// <summary>
/// A wreck staged in a street of the suite's built city, and the police closing the road round it (SRV-8…SRV-11):
/// a car at the mouth of each lane the wreck lies across, its officer out and standing there, and those lanes out
/// of every route.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P5)]
[Collection(nameof(TownGeometryCollection))]
public class RoadClosureInATownTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>A wreck lying in its lane closes that lane</b> (SRV-9): one police car stands in its entrance, and its
    /// officer is out of the car and standing on the road at the mouth (SRV-11).
    /// </summary>
    [Fact]
    public void AWreckInItsLaneIsClosedAtThatLanesEntranceByAnOfficerStandingThere()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WarmUpTicks);

        var (wreck, lane) = StageAWreck(world, acrossTheRoad: false);
        var patrol = WaitForAClosure(world, loop, wreck, holding: 1);
        loop.Advance(OfficerWalksTicks);

        var closed = world.Beat.ClosedLanesOf(patrol);
        Assert.Equal(lane, world.Beat.SceneLane[patrol]);
        Assert.Contains(lane, closed.ToArray());
        foreach (var closedLane in closed) Assert.True(world.ClosedLanes[closedLane], $"lane {closedLane} is held but not closed");

        var officer = world.Beat.Officer[patrol];
        Assert.True(officer >= 0, "the police car carries no officer");
        Assert.False(world.People.Inside[officer].Any, "the officer never got out of the car");
        Assert.Equal(TripStage.OnDuty, world.People.Stage[officer]);

        // At the mouth, and on the road: where they stand for as long as no call is coming down the lane, which is
        // what they step aside for (SRV-6).
        var entrance = closed[0];
        var postM = Spline.SampleAt(world.Roads.ArcsOf(entrance), Config.Service.OfficerIntoTheLaneM).PositionM;
        var nearestM = float.PositiveInfinity;
        for (var tick = 0; tick < WatchedTicks && world.Beat.Closes(patrol) && nearestM > Config.Service.CrewReachM; tick++)
        {
            nearestM = MathF.Min(nearestM, (world.People.PositionM[officer] - postM).Length());
            loop.Advance(1);
        }

        var calls = "";
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (!world.Cars.BlueLight[car]) continue;

            calls += $"\n  call {car} lane {world.Cars.LaneOf(car)} hold {world.Cars.Hold[car]} cut {world.Cars.GrantCutBy[car]} " +
                     $"pass {world.Cars.Pass[car].Any}/{world.Cars.Pass[car].Begun} aside {world.Cars.Context[car].PassAsideM:F2} " +
                     $"at {world.Cars.PositionM[car]} chain {string.Join(",", world.Cars.ChainOf(car)[..world.Cars.Line[car].LaneCount].ToArray())}";
        }

        Assert.True(
            nearestM <= Config.Service.CrewReachM,
            $"the officer came no nearer the mouth of lane {entrance} than {nearestM:F2} m; closure of {closed.Length} lanes, " +
            $"police car at {world.Cars.PositionM[patrol]}{calls}");
    }

    /// <summary>
    /// <b>A wreck lying across the road is closed from both ends, a police car sent to each</b> (SRV-9): two patrols
    /// take it, one for each of its lanes.
    /// </summary>
    [Fact]
    public void AWreckAcrossTheRoadSendsAPoliceCarToEachEnd()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WarmUpTicks);

        var (wreck, lane) = StageAWreck(world, acrossTheRoad: true);
        var back = world.Roads.LaneReverse[lane];
        var toLane = -1;
        var toBack = -1;
        for (var tick = 0; tick < DispatchedWithinTicks && (toLane < 0 || toBack < 0); tick++)
        {
            loop.Advance(1);
            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (world.Beat.Wreck[car] != wreck || world.Beat.ClosedCount[car] == 0) continue;

                var closes = world.Beat.SceneLane[car];
                if (closes == lane) toLane = car;
                else if (closes == back) toBack = car;
            }
        }

        Assert.True(toLane >= 0, $"no police car was sent to close lane {lane}, which the wreck lies across");
        Assert.True(toBack >= 0, $"no police car was sent to close lane {back}, which the wreck lies across");
        Assert.NotEqual(toLane, toBack);
    }

    /// <summary>
    /// <b>A closed lane is out of every route</b> (SRV-10): no car without a call turns into its entrance once the
    /// officer is standing there.
    /// </summary>
    [Fact]
    public void NoCarWithoutACallDrivesIntoAClosedLane()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WarmUpTicks);

        var (wreck, _) = StageAWreck(world, acrossTheRoad: false);
        var patrol = WaitForAClosure(world, loop, wreck, holding: 1);
        loop.Advance(OfficerWalksTicks);

        // A car already in the lane is inside the closure and drives out of it; one carrying a call is let in
        // (SRV-6), and is the same car after it has hitched the wreck and put its light out.
        var entrance = world.Beat.EntranceOf(patrol);
        var letIn = new bool[world.Cars.Count];
        var was = new int[world.Cars.Count];
        for (var car = 0; car < world.Cars.Count; car++)
        {
            letIn[car] = world.Cars.LaneOf(car) == entrance;
            was[car] = world.Cars.LaneOf(car);
        }

        for (var tick = 0; tick < WatchedTicks && world.Beat.Closes(patrol); tick++)
        {
            loop.Advance(1);
            for (var car = 0; car < world.Cars.Count; car++)
            {
                letIn[car] |= world.Cars.BlueLight[car];
                if (car == patrol || letIn[car]) continue;

                Assert.False(
                    world.Cars.LaneOf(car) == entrance,
                    $"car {car} turned into closed lane {entrance} at tick {tick} of the closure: " +
                    $"patrol {world.Beat.Stage[car]} station {world.Beat.Station[car]}, came off lane {was[car]}, " +
                    $"chain {string.Join(",", world.Cars.ChainOf(car)[..world.Cars.Line[car].LaneCount].ToArray())}, " +
                    $"route {string.Join(",", world.Cars.RouteOf(car)[..world.Cars.RouteCount[car]].ToArray())}, " +
                    $"destination {world.Cars.DestinationM[car]}, committed {world.Cars.CommittedToTheBox[car]}");
                was[car] = world.Cars.LaneOf(car);
            }
        }
    }

    /// <summary>
    /// A parked car of the town's own, put down in the middle of a long street lane — along it, or across the whole
    /// road — and wrecked there the way a crash wrecks one, so the town is told of it (EVA-1).
    /// </summary>
    static (int Wreck, int Lane) StageAWreck(TownWorld world, bool acrossTheRoad)
    {
        var roads = world.Roads;
        var lane = -1;
        for (var candidate = 0; candidate < roads.LaneCount; candidate++)
        {
            if (roads.IsABayArm(candidate) || roads.LaneReverse[candidate] < 0) continue;
            if (lane < 0 || roads.LaneLengthM[candidate] > roads.LaneLengthM[lane]) lane = candidate;
        }

        var victim = -1;
        for (var car = 0; car < world.Cars.Count && victim < 0; car++)
        {
            if (!world.Cars.Driven[car] && !world.Cars.Broken[car] && world.Beat.Station[car] == PatrolDuty.NoBuilding
                && world.Recovery.Depot[car] < 0 && !world.Cars.Ambulance[car])
            {
                victim = car;
            }
        }

        Assert.True(lane >= 0 && victim >= 0, "the built city has no street lane or no parked car to wreck on it");

        var at = Spline.SampleAt(roads.ArcsOf(lane), roads.LaneLengthM[lane] * 0.5f);
        var positionM = acrossTheRoad
            ? at.PositionM + (at.Right * Config.RoadSideSign * -(roads.LaneWidthM[lane] * 0.5f))
            : at.PositionM;
        var headingRad = acrossTheRoad ? at.HeadingRad + (MathF.PI * 0.5f) : at.HeadingRad;

        world.Cars.PositionM[victim] = positionM;
        world.Cars.HeadingRad[victim] = headingRad;
        world.Cars.VelocityMps[victim] = Vector2.Zero;
        world.PhysicsForInstruments.Release(world.Cars.Body[victim], positionM, headingRad);
        world.Apply(new BodyTag(BodyKind.Car, victim), DamageOutcome.Broken);
        return (victim, lane);
    }

    /// <summary>
    /// Advances until as many patrols as asked stand closing a lane of this wreck, and hands back the first of them.
    /// </summary>
    static int WaitForAClosure(TownWorld world, SimLoop<TownWorld> loop, int wreck, int holding)
    {
        for (var tick = 0; tick < ArrivesWithinTicks; tick++)
        {
            loop.Advance(1);

            var first = -1;
            var closing = 0;
            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (world.Beat.Wreck[car] != wreck || !world.Beat.Closes(car)) continue;

                closing++;
                if (first < 0) first = car;
            }

            if (closing >= holding) return first;
        }

        var sent = 0;
        var patrols = "";
        for (var car = 0; car < world.Cars.Count; car++)
        {
            sent += world.Beat.Wreck[car] == wreck ? 1 : 0;
            if (world.Beat.Station[car] == PatrolDuty.NoBuilding) continue;

            var officer = world.Beat.Officer[car];
            patrols += $"\n  car {car} {world.Beat.Stage[car]} wreck {world.Beat.Wreck[car]} closes {world.Beat.ClosedCount[car]} " +
                       $"officer {officer} inside {(officer >= 0 ? world.People.Inside[officer].ToString() : "-")} " +
                       $"{(world.Cars.PositionM[car] - world.Cars.PositionM[wreck]).Length():F0} m off";
        }

        Assert.Fail(
            $"{holding} patrol(s) never stood closing wreck {wreck} in {ArrivesWithinTicks} ticks; " +
            $"{sent} are on it, of {world.PoliceCars} police cars; {world.ClosuresTaken} taken, " +
            $"{world.ClosuresGivenUp} given up, {world.ClosuresStood} stood{patrols}");
        return -1;
    }

    const int WarmUpTicks = 600;

    /// <summary>Four minutes: a patrol crosses the suite's city in less.</summary>
    const int ArrivesWithinTicks = 14_400;

    /// <summary>Long enough for an officer to walk from the door to the mouth of the lane.</summary>
    const int OfficerWalksTicks = 300;

    const int WatchedTicks = 3_600;

    /// <summary>A few decisions of every patrol, which is all a call takes to be answered.</summary>
    const int DispatchedWithinTicks = 600;
}
