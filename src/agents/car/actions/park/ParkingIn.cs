using System.Runtime.CompilerServices;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Parking;

namespace TrafficSimulation.Agents.Car.Actions;

/// <summary>
/// <b>Park</b> (GEN-4f): up the route's line to where the car waits for its manoeuvre into its bay, the manoeuvre
/// shaped afresh from where it stands and asked for whole (<see cref="BayManoeuvring"/>) — and, had, the manoeuvre
/// itself, a piece at a time. Done in the bay, parked; or, where the leg only turns there (GEN-4l), straight back out.
/// </summary>
/// <remarks>
/// <b>Which shape is the car's</b>: its own circle (<see cref="CarBuild.ParkingTemplateRadiusM"/>), its own body
/// and its own straightening (<see cref="CarBuild.ParkingStraightensUpM"/>), so a long vehicle with a wide circle
/// makes a different manoeuvre into the same bay than a small car does, and takes more street doing it — and of the
/// two it could make, the one taking less street.
/// </remarks>
internal sealed class ParkingIn(
    DrivingGround ground, CarActions actions, BayManoeuvring bays, ParkingRegistry parking, BayStreets bayStreets)
{
    CarFleet Cars => ground.Cars;

    Manoeuvres Manoeuvres => bays.Manoeuvres;

    /// <summary>
    /// <b>This tick of a car getting into a bay</b>: the manoeuvre, where it is begun; otherwise up the route's line to
    /// where it waits, the manoeuvre kept where it was asked for last tick, or shaped afresh and asked for.
    /// </summary>
    public void Tick<TTown>(ref TTown town, int car, in CarPose pose)
        where TTown : struct, ICarTown
    {
        if (Manoeuvres.IsBegun(car))
        {
            bays.DriveThePiece(ref town, car, pose);
            return;
        }

        if (!town.ReadTheLine(car, pose, out var progressM, out var alongMps, out var coveredM)) return;

        if (Manoeuvres.Stage[car] == ManoeuvreStage.Asked)
        {
            if (bays.KeepOrWithdraw(car))
            {
                bays.DriveThePiece(ref town, car, pose);
                return;
            }
        }
        else
        {
            TakeUpTheBay(car, Manoeuvres.Bay[car], progressM, alongMps);
        }

        town.DriveOnTheLine(car, pose, progressM, alongMps, coveredM);
    }

    /// <summary>
    /// <b>The manoeuvre into a bay driven to its end</b>: the car parked — or, where the leg turns in this bay (GEN-4l),
    /// straight back out of it, onto the lane its route out of the bay sets off down, holding the bay until it is out.
    /// </summary>
    public bool Arrive<TTown>(ref TTown town, int car)
        where TTown : struct, ICarTown
    {
        var bay = Manoeuvres.Bay[car];
        Manoeuvres.Clear(car);
        if (bay == parking.TurnOf(car) && town.LeaveTheBay(car, bay))
        {
            actions.Enter(car, CarAction.Unpark);
            return true;
        }

        town.ParkIt(car, bay);
        return true;
    }

    /// <summary>
    /// <b>A car's manoeuvre into a bay, shaped afresh from where it stands</b> on its way to the place it waits for it,
    /// and asked for where all of its ground is free. True where it was shaped.
    /// </summary>
    /// <remarks>
    /// <b>Only once the car is near enough to stop for it</b>: a shape laid from further off is one the car would
    /// drive a street of before it began, holding that street as it went.
    /// </remarks>
    public bool TakeUpTheBay(int car, int bay, float progressM, float alongMps)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var brakingMps2 = CarFollower.BrakingMps2(ground.Config, build, Cars.GroundCoefficient[car]);
        var toTheStopM = Cars.Line[car].LengthM - progressM;
        if (toTheStopM > DrivingGround.StoppingM(MathF.Max(0f, alongMps), brakingMps2) + build.LengthM) return false;

        var lane = Cars.ChainOf(car)[Cars.Line[car].LaneCount - 1];
        if (!ShapeTheWayIn(car, bay, lane)) return false;

        bays.Ask(car);
        return true;
    }

    /// <summary>
    /// <b>Where on a lane a car waits for its manoeuvre into a bay</b> — the metre its own turn in would begin
    /// at, off the lane's own line, which is short of the bay by as much as its circle and its swing take. A bay
    /// it could not nose into from there is waited for a car's length and a circle short of its mouth.
    /// </summary>
    /// <remarks>
    /// <b>The car's own and not the bay's</b>: a wide circle turns in further back. It is a place to stop and not
    /// a promise of the shape — which one the car makes is chosen when it gets there (<see cref="ShapeTheWayIn"/>),
    /// and backing in from there is pulling on past the bay first. <b>Read off the lane's own line at the bay</b>,
    /// run straight back from there to the lane's start, since a car park stands on a street straight enough to
    /// square a rank to (GEN-53).
    /// </remarks>
    [SkipLocalsInit]
    public float StopForTheBayM(int car, int bay, int lane)
    {
        var roads = ground.Roads;
        ref readonly var build = ref Cars.BuildOf(car);
        var mouthM = bayStreets.AtLaneM(bay, lane);
        var radiusM = build.ParkingTemplateRadiusM;
        var fallbackM = Math.Clamp(mouthM - radiusM - build.LengthM, 0f, roads.LaneLengthM[lane]);

        var mouth = Spline.SampleAt(roads.ArcsOf(lane), mouthM);
        Span<ArcSeg> room = stackalloc ArcSeg[BayManoeuvre.MostArcs];
        var shape = BayManoeuvre.NoseIn(
            new BayManoeuvre.Pose(mouth.PositionM - (mouth.Direction * mouthM), mouth.HeadingRad), NoseInPose(car, bay),
            radiusM, build.ParkingStraightensUpM, ground.Config.ParkingSwingMostRad, room);
        if (!shape.Exists) return fallbackM;

        return room[0].Curvature == 0f ? MathF.Min(room[0].LengthM, roads.LaneLengthM[lane]) : 0f;
    }

    /// <summary>The axle's pose in a bay standing nose in, heading into it (GEN-4i).</summary>
    BayManoeuvre.Pose NoseInPose(int car, int bay)
    {
        var into = Heading.Unit(parking.HeadingRad(bay));
        return new BayManoeuvre.Pose(
            parking.CentreM(bay) - (into * Cars.BuildOf(car).CentreAheadOfAxleM), parking.HeadingRad(bay));
    }

    /// <summary>And standing backed in — the axle at the deep end, travelling into the bay as it backs in.</summary>
    BayManoeuvre.Pose BackedInPose(int car, int bay)
    {
        var into = Heading.Unit(parking.HeadingRad(bay));
        return new BayManoeuvre.Pose(
            parking.CentreM(bay) + (into * Cars.BuildOf(car).CentreAheadOfAxleM), parking.HeadingRad(bay));
    }

    /// <summary>
    /// <b>The shape into a bay from where the car stands on its lane</b>: nose first, or on past it and backwards —
    /// whichever of the two it can make takes less street, and its driver's habit where they take the same.
    /// False where it can make neither from here.
    /// </summary>
    [SkipLocalsInit]
    bool ShapeTheWayIn(int car, int bay, int lane)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var forward = Heading.Unit(Cars.HeadingRad[car]);
        var from = new BayManoeuvre.Pose(CarFollower.RearAxleM(build, Cars.PositionM[car], forward), Cars.HeadingRad[car]);
        var radiusM = build.ParkingTemplateRadiusM;

        Span<ArcSeg> noseIn = stackalloc ArcSeg[BayManoeuvre.MostArcs];
        Span<ArcSeg> backIn = stackalloc ArcSeg[BayManoeuvre.MostArcs];
        var noseShape = BayManoeuvre.NoseIn(
            from, NoseInPose(car, bay), radiusM, build.ParkingStraightensUpM, ground.Config.ParkingSwingMostRad, noseIn);
        var backShape = BayManoeuvre.BackIn(from, BackedInPose(car, bay), radiusM, build.ParkingStraightensUpM, backIn);

        var noseM = float.PositiveInfinity;
        var backM = float.PositiveInfinity;
        if (noseShape.Exists && !bays.WhatTheShapeTakes(car, noseIn, noseShape, out noseM)) noseM = float.PositiveInfinity;
        if (backShape.Exists && !bays.WhatTheShapeTakes(car, backIn, backShape, out backM)) backM = float.PositiveInfinity;
        if (float.IsPositiveInfinity(noseM) && float.IsPositiveInfinity(backM)) return false;

        var backs = backM < noseM || (backM == noseM && Cars.BacksIntoBays[car]);
        var chosen = backs ? backIn : noseIn;
        chosen.CopyTo(Manoeuvres.RoomOf(car));
        Manoeuvres.Shaped(car, ManoeuvreKind.Park, bay, lane, backs ? backShape : noseShape, backs ? backM : noseM);
        return true;
    }
}
