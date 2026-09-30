using System.Numerics;
using System.Runtime.CompilerServices;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Parking;

namespace TrafficSimulation.Agents.Car.Actions;

/// <summary>
/// <b>Unpark</b> (GEN-4f): out of the bay the car stands in, by a manoeuvre shaped from where it stands and asked for
/// whole (<see cref="BayManoeuvring"/>) — standing in the bay until it has all of that ground, then driving it a piece
/// at a time. Done on the street, with the bay given back and the lane under it taken as its route's.
/// </summary>
/// <remarks>
/// <b>A manoeuvre out of a bay is not driven until its ground is had</b>: the car stands in the bay with the first
/// piece as its line, asking for it, and its grant holds it where it is until then.
/// </remarks>
internal sealed class PullingOut(
    DrivingGround ground, BayManoeuvring bays, ParkingRegistry parking, BayStreets bayStreets)
{
    CarFleet Cars => ground.Cars;

    Manoeuvres Manoeuvres => bays.Manoeuvres;

    /// <summary>
    /// <b>This tick of a car getting out of a bay</b>: its manoeuvre asked for, or kept or withdrawn in the tick after it
    /// was laid, until it is begun — and the piece it is on driven, which a car without its ground is held still on.
    /// </summary>
    public void Tick<TTown>(ref TTown town, int car, in CarPose pose)
        where TTown : struct, ICarTown
    {
        if (!Manoeuvres.IsBegun(car))
        {
            if (Manoeuvres.Stage[car] == ManoeuvreStage.Asked) bays.KeepOrWithdraw(car);
            else bays.Ask(car);
        }

        bays.DriveThePiece(ref town, car, pose);
    }

    /// <summary>
    /// <b>The manoeuvre out of a bay driven to its end</b>: the bay given back — the turn with it (GEN-4l) — and the
    /// lane under the car taken as its route's.
    /// </summary>
    public bool Arrive<TTown>(ref TTown town, int car)
        where TTown : struct, ICarTown
    {
        Manoeuvres.Clear(car);
        parking.Vacate(car);
        parking.LeaveTheTurn(car);
        return town.TakeTheRoad(car);
    }

    /// <summary>
    /// <b>The shape out of a bay the car is standing in</b>, taken as its line: forwards where it stands backed in
    /// and in reverse where it stands nose in (GEN-4j), onto the lane of the street running the way it is going —
    /// and of two that run it no nearer, the one taking less street. False where it can make none, which is a
    /// leg with nothing to drive.
    /// </summary>
    /// <remarks>
    /// <b>Read off the pose and never off the register</b> (GEN-4j): which way round the body stands is where it
    /// points. Setting off into the stream running the other way is a leg that starts by driving round the block,
    /// so where the car is going ranks the lanes first — <b>but for a car turning in this bay</b> (GEN-4l), whose
    /// route comes back down the lane it turned for and lands on that one whichever way the place lies.
    /// </remarks>
    [SkipLocalsInit]
    public bool ShapeTheWayOut(int car, int bay)
    {
        var turnsOnto = parking.TurnOf(car) == bay ? Cars.TurnsBackOn[car] : CarFleet.NoLane;

        ref readonly var build = ref Cars.BuildOf(car);
        var forward = Heading.Unit(Cars.HeadingRad[car]);
        var reverse = BayTemplate.StandsNoseIn(parking.HeadingRad(bay), Cars.HeadingRad[car]);
        var from = new BayManoeuvre.Pose(
            CarFollower.RearAxleM(build, Cars.PositionM[car], forward),
            reverse ? Cars.HeadingRad[car] + MathF.PI : Cars.HeadingRad[car]);

        Span<ArcSeg> room = stackalloc ArcSeg[BayManoeuvre.MostArcs];
        var bestLane = CarFleet.NoLane;
        var bestShape = BayManoeuvre.Shape.None;
        var bestTowardsM = float.NegativeInfinity;
        var bestM = float.PositiveInfinity;
        foreach (var lane in bayStreets.LanesOf(bay))
        {
            if (turnsOnto != CarFleet.NoLane && lane != turnsOnto) continue;

            // A car backing out travels up the street and ends facing down it, the way the lane runs.
            var at = Spline.SampleAt(ground.Roads.ArcsOf(lane), bayStreets.AtLaneM(bay, lane));
            var shape = BayManoeuvre.OutOfTheBay(
                from, reverse, at.PositionM, reverse ? at.HeadingRad + MathF.PI : at.HeadingRad,
                build.ParkingTemplateRadiusM, build.ParkingStraightensUpM, ground.Config.LaneWidthM, room);
            if (!shape.Exists || !bays.WhatTheShapeTakes(car, room, shape, out var streetM)) continue;

            var towardsM = Cars.HasDestination[car] ? Vector2.Dot(Cars.DestinationM[car] - at.PositionM, at.Direction) : 0f;
            if (towardsM < bestTowardsM || (towardsM == bestTowardsM && streetM >= bestM)) continue;

            bestLane = lane;
            bestShape = shape;
            bestTowardsM = towardsM;
            bestM = streetM;
            room[..shape.ArcCount].CopyTo(Manoeuvres.RoomOf(car));
        }

        if (!bestShape.Exists) return false;

        Manoeuvres.Shaped(car, ManoeuvreKind.Leave, bay, bestLane, bestShape, bestM);
        bays.TakeThePiece(car, 0);
        return true;
    }
}
