using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Hud;

/// <summary>
/// <b>CTL-1a: where the selected unit is going, drawn whole</b> — the lanes or the pavement it is holding,
/// from under its own body to the end of what it has planned, as one chevronned line with a mark on the
/// goal at the end of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the interface asking about one unit, and not a layer</b> (OBS-2h): the layers answer <em>what
/// is this body doing now</em> for every body at once, and drawing a whole route for each of them would
/// bury the town under its own plans. One unit, because somebody clicked on it.
/// </para>
/// <para>
/// <b>What the unit is holding is read off it.</b> A car's is the line the assembler wove plus the lanes
/// its own route has left in it; a walker's is the points its own line was laid as. A second opinion
/// drawn beside the body would be the one thing in the frame that is not the simulation.
/// </para>
/// <para>
/// <b>And where that runs out, the rest is asked of the town</b> (<see cref="TownWorld.RouteBeyond"/>): a
/// body holds a bounded run of its route and plans the next one when it runs out, so what it is carrying
/// is the near end of a long trip. The far end is planned over the same network by the same planner, from
/// the end of what the body holds, which is the answer the body will get when it asks.
/// </para>
/// </remarks>
internal static class SelectionPath
{
    /// <summary>
    /// The goal mark's arms and its stroke, on screen — a size in pixels rather than in metres because it
    /// stands on a <em>place</em> and not on a body, so there is nothing under it to be drawn to the scale
    /// of. Bolder than the brackets: it is the one mark here that says a thing has ended.
    /// </summary>
    const float CrossArmPx = 10f;

    const float CrossStrokePx = 3f;

    public static void Draw(ref ScreenDraw draw, TownWorld world, SimConfig config, float pixelsPerMetre)
    {
        if (pixelsPerMetre <= 0f) return;

        // CTL-5: a hand at the wheel has substituted the behaviour wholesale, so there is no goal under the
        // unit and no route to it — the picture says so by having nothing to draw.
        if (world.HandsOn) return;

        // The selection's plans and no others (CTL-1a), however many units it holds: what bounds the
        // picture is that somebody asked for these ones, and the set is bounded itself (CTL-1b). Which
        // slot of it a unit stands in is where the town keeps the rest of that unit's way.
        var units = world.Selected;
        for (var slot = 0; slot < units.Length; slot++)
        {
            if (units[slot].Kind == SelectionKind.Car)
            {
                CarPath(ref draw, world, config, slot, units[slot].Index, pixelsPerMetre);
            }
            else
            {
                WalkerPath(ref draw, world, slot, units[slot].Index, pixelsPerMetre);
            }
        }
    }

    /// <summary>
    /// The car: the rest of the line it is driving, then the route that line has not been grown onto yet —
    /// each junction join and the lane past it, between the setbacks that junction's own movements arrive
    /// and leave at (TER-5d), which is the same ground the nodes layer draws.
    /// </summary>
    /// <remarks>
    /// <b>The route runs out rather than ends</b> (<see cref="CarFleet.RouteLanesPerCar"/>): a long trip is
    /// planned again from where the car has got to, so what the car holds stops at the last lane planned so
    /// far and the rest of the way is asked of the town (<see cref="TownWorld.RouteBeyond"/>). The last lane
    /// is drawn whole — where a car leaves it for its bay is a manoeuvre's geometry and not the route's.
    /// </remarks>
    static void CarPath(
        ref ScreenDraw draw, TownWorld world, SimConfig config, int slot, int car, float pixelsPerMetre)
    {
        var cars = world.Cars;
        var roads = world.Roads;
        var pitchM = PathMarks.MarkPitchAt(pixelsPerMetre);
        var sagM = PathMarks.SagPx / pixelsPerMetre;
        var colour = Theme.SelectionPath;

        var line = cars.LineOf(car);
        var lanes = cars.Line[car].LaneCount;
        var lastLane = lanes > 0 ? cars.ChainOf(car)[lanes - 1] : CarFleet.NoLane;
        if (line.Length > 0)
        {
            // From under the middle of the body: the ground already covered is where the car has been, and
            // the progress itself is the rear axle's, a car's length of line behind it.
            ref readonly var build = ref cars.BuildOf(car);
            var totalM = cars.Line[car].LengthM;
            var underTheCarM = MathF.Min(Math.Clamp(cars.ProgressM[car], 0f, totalM) + build.CentreAheadOfAxleM, totalM);
            PathMarks.Chained(ref draw, line, underTheCarM, totalM, pitchM, bothWays: false, sagM, colour);
        }

        var route = cars.RouteOf(car);
        var planned = cars.RouteCount[car] - cars.RouteTaken[car];
        lastLane = Stretches(
            ref draw, roads, route[cars.RouteTaken[car]..cars.RouteCount[car]], lastLane, pitchM, sagM, colour);

        // CTL-8c: a car it is following is a thing and not a place, and it is a thing that moves — so it
        // is wrapped wherever it has got to, and the line above stops at wherever the route was last
        // drawn to. This one is marked whether or not there is a route in hand: the order stands while
        // the leg under it is being laid again.
        //
        // <b>And nothing is planned past it</b>: where the car is going is a car, so a route drawn on to
        // where that car was standing this frame is a picture that is wrong by however far it has moved.
        var lead = world.OrderOf(car) == PlayerOrder.FollowThatCar ? world.OrderedAfter(car) : -1;
        if (lead >= 0)
        {
            ref readonly var leadBuild = ref cars.BuildOf(lead);
            SelectionMark.Brackets(
                ref draw, cars.PositionM[lead], new Vector2(leadBuild.LengthM, leadBuild.WidthM),
                cars.HeadingRad[lead], pixelsPerMetre, Theme.SelectionGoal);
            return;
        }

        // The rest of the way, where what the car is carrying stops short of where it is going.
        if (cars.RouteRunsOut[car])
        {
            Stretches(ref draw, roads, world.RouteBeyond(slot, car, lastLane), lastLane, pitchM, sagM, colour);
        }

        if (line.Length == 0 && planned == 0) return;

        // What a leg is aimed at is a booking (GEN-4g), and a bay is a thing the car is going *into* — so it
        // is wrapped rather than crossed. A leg aimed at a place in the road instead (AMB-5, EVA-3, CTL-8a)
        // has no bay and gets the cross.
        var bay = world.Parking.BookingOf(car);
        if (bay >= 0)
        {
            SelectionMark.Brackets(
                ref draw, world.Parking.CentreM(bay),
                new Vector2(config.ParkingSpaceLengthM, config.ParkingSpaceWidthM), world.Parking.HeadingRad(bay),
                pixelsPerMetre, Theme.SelectionGoal);
        }
        else if (cars.HasDestination[car])
        {
            Cross(ref draw, cars.DestinationM[car], pixelsPerMetre);
        }
    }

    /// <summary>
    /// A run of lanes drawn as the ground a car covers over them: each junction join and the lane past it,
    /// between the setbacks that junction's own movements arrive and leave at (TER-5d). Answers the last
    /// lane drawn, which is what the next run is joined on from.
    /// </summary>
    static int Stretches(
        ref ScreenDraw draw, RoadGraph roads, ReadOnlySpan<int> lanes, int fromLane, float pitchM, float sagM,
        Vector4 colour)
    {
        var lastLane = fromLane;
        foreach (var lane in lanes)
        {
            var turn = lastLane >= 0 ? roads.TurnSlot(lastLane, lane) : RoadGraph.NoTurn;
            if (turn != RoadGraph.NoTurn)
            {
                var join = roads.JoinArcs(turn);
                PathMarks.Chained(
                    ref draw, join, 0f, Spline.TotalLengthM(join), pitchM, bothWays: false, sagM, colour);
            }

            PathMarks.Chained(
                ref draw, roads.ArcsOf(lane), roads.JoinedAtM(lane), roads.LaneLengthM[lane] - roads.LeftAtM(lane),
                pitchM, bothWays: false, sagM, colour);

            lastLane = lane;
        }

        return lastLane;
    }

    /// <summary>
    /// The walker: every point of the line it was given that it has not walked yet, run from the body
    /// itself rather than from where the leg began, and on past it where the line stops short of the goal.
    /// </summary>
    /// <remarks>
    /// No dot is put where one stretch of pavement becomes the next. The whole route is on screen here,
    /// and a dot at every kerb of it is a row of beads over the thing they are punctuating — which is a
    /// reading the walker layer offers under its own switch, on the two stretches it draws.
    /// </remarks>
    static void WalkerPath(ref ScreenDraw draw, TownWorld world, int slot, int person, float pixelsPerMetre)
    {
        var people = world.People;

        // PHY-7: somebody inside a building or a car is not in the town, and the walk they will take when
        // they come out has not been laid yet.
        if (people.Inside[person].Any || !people.Walking[person]) return;

        var count = people.WalkedCount[person];
        var at = people.WalkedAt(person);
        if (at < 0 || at >= count) return;

        var pitchM = PathMarks.MarkPitchAt(pixelsPerMetre);
        var colour = Theme.SelectionPath;
        var points = people.WalkedLineOf(person);
        var fromM = people.PositionM[person];

        // A body standing on the point it is walking at has finished that stretch and is about to be handed
        // the next one, so the stretch in hand is the one starting at the first point it is not on already.
        while (at < count - 1 && (points[at] - fromM).Length() <= people.RadiusM[person]) at++;

        for (var point = at; point < count; point++)
        {
            PathMarks.Chevroned(ref draw, fromM, points[point], pitchM, colour);
            fromM = points[point];
        }

        // And the rest of the walk, where the line the walker holds stops short of where it is going.
        if (people.WalkedRunsOut[person])
        {
            foreach (var pointM in world.WalkBeyond(slot, person, fromM))
            {
                PathMarks.Chevroned(ref draw, fromM, pointM, pitchM, colour);
                fromM = pointM;
            }
        }

        Goal(ref draw, world, person, pixelsPerMetre);
    }

    /// <summary>
    /// The end of a walk, marked as what it is: a thing that is entered is wrapped in the same brackets the
    /// unit itself wears (CTL-3), and a place on the ground is crossed.
    /// </summary>
    static void Goal(ref ScreenDraw draw, TownWorld world, int person, float pixelsPerMetre)
    {
        var people = world.People;
        var car = people.TripCar[person];
        if (people.Stage[person] == TripStage.WalkingToTheCar && car >= 0)
        {
            ref readonly var build = ref world.Cars.BuildOf(car);
            SelectionMark.Brackets(
                ref draw, world.Cars.PositionM[car], new Vector2(build.LengthM, build.WidthM),
                world.Cars.HeadingRad[car], pixelsPerMetre, Theme.SelectionGoal);
            return;
        }

        var building = people.DestinationBuilding[person];
        if (building != PersonFleet.NoBuilding)
        {
            var buildings = world.Plan.Buildings;
            SelectionMark.Brackets(
                ref draw, buildings.CentreM[building], buildings.SizeM[building], buildings.HeadingRad[building],
                pixelsPerMetre, Theme.SelectionGoal);
            return;
        }

        Cross(ref draw, people.GoalM[person], pixelsPerMetre);
    }

    /// <summary>The goal as a place and not as a thing: two bars across each other, standing on the point itself.</summary>
    static void Cross(ref ScreenDraw draw, Vector2 atM, float pixelsPerMetre)
    {
        var armM = CrossArmPx / pixelsPerMetre;
        var strokeM = CrossStrokePx / pixelsPerMetre;
        var arm = new Vector2(armM, armM);
        draw.LineM(atM - arm, atM + arm, strokeM, Theme.SelectionGoal);
        draw.LineM(atM - new Vector2(armM, -armM), atM + new Vector2(armM, -armM), strokeM, Theme.SelectionGoal);
    }
}
