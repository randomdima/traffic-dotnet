using System.Numerics;
using System.Runtime.CompilerServices;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>A car getting into a bay and out of one</b> (GEN-4f): a manoeuvre it lays for itself off its own circle
/// from where it stands, <b>the shape of the few it could make that takes the least street</b>, asked for where
/// all of its ground is free, laid as a body over that ground, kept or withdrawn once, and driven a piece at a time
/// in each piece's gear.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its ground is a pass's</b> (TER-4c.6): the car's own body swept down the shape and read off the atlas, laid at
/// p0 where nothing compares it, cuts it or takes it — a car half across a street cannot give the street back —
/// and given back as it is driven. So the traffic is held off a car manoeuvring, and a car manoeuvring off the
/// traffic, by the ground each holds and by nothing else (SIM-7).
/// </para>
/// <para>
/// <b>Nothing here reads another agent</b> (TER-4c.5): whether the ground is free is what is laid on the ways
/// under it, and the bays either side are ways like any other.
/// </para>
/// <para>
/// <b>Which shape is the car's</b>: its own circle (<see cref="CarBuild.ParkingTemplateRadiusM"/>), its own body
/// and its own straightening (<see cref="CarBuild.ParkingStraightensUpM"/>), so a long vehicle with a wide circle
/// makes a different manoeuvre into the same bay than a small car does, and takes more street doing it.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>
    /// How many of the street's ways one shape's ground is totalled over. A bound on a stack span and not a figure
    /// behaviour reads: a way past it is still totalled, as ground of its own.
    /// </summary>
    const int MostStreetWaysTaken = 16;

    readonly Manoeuvres _manoeuvres;

    /// <summary>Manoeuvres whose ground was asked for since the town was laid (GEN-4f).</summary>
    public long ManoeuvresAsked { get; private set; }

    /// <summary>And withdrawn in the rebuild after, because a body or another pass had the ground by then.</summary>
    public long ManoeuvresWithdrawn { get; private set; }

    /// <summary>And begun: ground the car then held until it had driven it.</summary>
    public long ManoeuvresBegun { get; private set; }

    /// <summary>How many of those begun were into a bay nose first, and how many backwards.</summary>
    public long ParkedNoseIn { get; private set; }

    /// <inheritdoc cref="ParkedNoseIn"/>
    public long ParkedBackedIn { get; private set; }

    /// <summary>
    /// <b>The street every manoeuvre begun took</b>, in metres of the street's own ways — the figure each car's
    /// choice of shape is the least of.
    /// </summary>
    public double ManoeuvreStreetM { get; private set; }

    /// <summary>The manoeuvres, for an instrument or the debug layer; the driver's own and read by nothing else.</summary>
    public Manoeuvres Manoeuvres => _manoeuvres;

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
    float StopForTheBayM(int car, int bay, int lane)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var mouthM = _bayStreets.AtLaneM(bay, lane);
        var radiusM = build.ParkingTemplateRadiusM;
        var fallbackM = Math.Clamp(mouthM - radiusM - build.LengthM, 0f, _roads.LaneLengthM[lane]);

        var mouth = Spline.SampleAt(_roads.ArcsOf(lane), mouthM);
        Span<ArcSeg> room = stackalloc ArcSeg[BayManoeuvre.MostArcs];
        var shape = BayManoeuvre.NoseIn(
            new BayManoeuvre.Pose(mouth.PositionM - (mouth.Direction * mouthM), mouth.HeadingRad), NoseInPose(car, bay),
            radiusM, build.ParkingStraightensUpM, _config.ParkingSwingMostRad, room);
        if (!shape.Exists) return fallbackM;

        return room[0].Curvature == 0f ? MathF.Min(room[0].LengthM, _roads.LaneLengthM[lane]) : 0f;
    }

    /// <summary>The axle's pose in a bay standing nose in, heading into it (GEN-4i).</summary>
    BayManoeuvre.Pose NoseInPose(int car, int bay)
    {
        var into = Heading.Unit(_parking.HeadingRad(bay));
        return new BayManoeuvre.Pose(
            _parking.CentreM(bay) - (into * Cars.BuildOf(car).CentreAheadOfAxleM), _parking.HeadingRad(bay));
    }

    /// <summary>And standing backed in — the axle at the deep end, travelling into the bay as it backs in.</summary>
    BayManoeuvre.Pose BackedInPose(int car, int bay)
    {
        var into = Heading.Unit(_parking.HeadingRad(bay));
        return new BayManoeuvre.Pose(
            _parking.CentreM(bay) + (into * Cars.BuildOf(car).CentreAheadOfAxleM), _parking.HeadingRad(bay));
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
        var forward = ForwardOf(car);
        var from = new BayManoeuvre.Pose(CarFollower.RearAxleM(build, Cars.PositionM[car], forward), Cars.HeadingRad[car]);
        var radiusM = build.ParkingTemplateRadiusM;

        Span<ArcSeg> noseIn = stackalloc ArcSeg[BayManoeuvre.MostArcs];
        Span<ArcSeg> backIn = stackalloc ArcSeg[BayManoeuvre.MostArcs];
        var noseShape = BayManoeuvre.NoseIn(
            from, NoseInPose(car, bay), radiusM, build.ParkingStraightensUpM, _config.ParkingSwingMostRad, noseIn);
        var backShape = BayManoeuvre.BackIn(from, BackedInPose(car, bay), radiusM, build.ParkingStraightensUpM, backIn);

        var noseM = float.PositiveInfinity;
        var backM = float.PositiveInfinity;
        if (noseShape.Exists && !WhatTheShapeTakes(car, noseIn, noseShape, out noseM)) noseM = float.PositiveInfinity;
        if (backShape.Exists && !WhatTheShapeTakes(car, backIn, backShape, out backM)) backM = float.PositiveInfinity;
        if (float.IsPositiveInfinity(noseM) && float.IsPositiveInfinity(backM)) return false;

        var backs = backM < noseM || (backM == noseM && Cars.BacksIntoBays[car]);
        var chosen = backs ? backIn : noseIn;
        chosen.CopyTo(_manoeuvres.RoomOf(car));
        _manoeuvres.Shaped(car, ManoeuvreKind.Park, bay, lane, backs ? backShape : noseShape, backs ? backM : noseM);
        return true;
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
    bool ShapeTheWayOut(int car, int bay)
    {
        var turnsOnto = _parking.TurnOf(car) == bay ? Cars.TurnsBackOn[car] : CarFleet.NoLane;

        ref readonly var build = ref Cars.BuildOf(car);
        var forward = ForwardOf(car);
        var reverse = BayTemplate.StandsNoseIn(_parking.HeadingRad(bay), Cars.HeadingRad[car]);
        var from = new BayManoeuvre.Pose(
            CarFollower.RearAxleM(build, Cars.PositionM[car], forward),
            reverse ? Cars.HeadingRad[car] + MathF.PI : Cars.HeadingRad[car]);

        Span<ArcSeg> room = stackalloc ArcSeg[BayManoeuvre.MostArcs];
        var bestLane = CarFleet.NoLane;
        var bestShape = BayManoeuvre.Shape.None;
        var bestTowardsM = float.NegativeInfinity;
        var bestM = float.PositiveInfinity;
        foreach (var lane in _bayStreets.LanesOf(bay))
        {
            if (turnsOnto != CarFleet.NoLane && lane != turnsOnto) continue;

            // A car backing out travels up the street and ends facing down it, the way the lane runs.
            var at = Spline.SampleAt(_roads.ArcsOf(lane), _bayStreets.AtLaneM(bay, lane));
            var shape = BayManoeuvre.OutOfTheBay(
                from, reverse, at.PositionM, reverse ? at.HeadingRad + MathF.PI : at.HeadingRad,
                build.ParkingTemplateRadiusM, build.ParkingStraightensUpM, _config.LaneWidthM, room);
            if (!shape.Exists || !WhatTheShapeTakes(car, room, shape, out var streetM)) continue;

            var towardsM = Cars.HasDestination[car] ? Vector2.Dot(Cars.DestinationM[car] - at.PositionM, at.Direction) : 0f;
            if (towardsM < bestTowardsM || (towardsM == bestTowardsM && streetM >= bestM)) continue;

            bestLane = lane;
            bestShape = shape;
            bestTowardsM = towardsM;
            bestM = streetM;
            room[..shape.ArcCount].CopyTo(_manoeuvres.RoomOf(car));
        }

        if (!bestShape.Exists) return false;

        _manoeuvres.Shaped(car, ManoeuvreKind.Leave, bay, bestLane, bestShape, bestM);
        TakeThePiece(car, 0);
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
    bool TakeUpTheBay(int car, int bay, float progressM, float alongMps)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var brakingMps2 = CarFollower.BrakingMps2(_config, build, Cars.GroundCoefficient[car]);
        var toTheStopM = Cars.Line[car].LengthM - progressM;
        if (toTheStopM > StoppingM(MathF.Max(0f, alongMps), brakingMps2) + build.LengthM) return false;

        var lane = Cars.ChainOf(car)[Cars.Line[car].LaneCount - 1];
        if (!ShapeTheWayIn(car, bay, lane)) return false;

        AskForTheManoeuvre(car);
        return true;
    }

    /// <summary>
    /// <b>This tick of a car's manoeuvre out of a bay</b>, standing in it with the first piece as its line: asked
    /// for, or kept or withdrawn in the tick after it was laid.
    /// </summary>
    void ConsiderLeaving(int car)
    {
        if (_manoeuvres.Stage[car] == ManoeuvreStage.Asked) KeepOrWithdrawTheManoeuvre(car);
        else AskForTheManoeuvre(car);
    }

    /// <summary>A shaped manoeuvre asked for where every metre of its ground is free (TER-4c.6), to be laid in the next rebuild.</summary>
    void AskForTheManoeuvre(int car)
    {
        if (!OverTheGround(car, GroundAsk.Free)) return;

        _manoeuvres.Stage[car] = ManoeuvreStage.Asked;
        ManoeuvresAsked++;
    }

    /// <summary>
    /// <b>A manoeuvre laid in this rebuild, kept or withdrawn</b> before the car moves on it — kept where nothing but
    /// the car itself is on its ground, as a pass is (<see cref="LaneOccupancy.KeepsItsPass(int, float, float, float, float, int, LaneRoster, in PassTerms)"/>).
    /// <b>Kept, the whole of its ground is the car's from this tick</b>, and a car getting into a bay takes up its
    /// first piece in place of the route's line.
    /// </summary>
    bool KeepOrWithdrawTheManoeuvre(int car)
    {
        if (!OverTheGround(car, GroundAsk.Keep))
        {
            _manoeuvres.Stage[car] = ManoeuvreStage.Shaped;
            ManoeuvresWithdrawn++;
            return false;
        }

        _manoeuvres.Stage[car] = ManoeuvreStage.Begun;
        ManoeuvresBegun++;
        ManoeuvreStreetM += _manoeuvres.StreetM[car];
        if (_manoeuvres.Kind[car] == ManoeuvreKind.Park)
        {
            if (_manoeuvres.Shape[car].IsReverse(_manoeuvres.Shape[car].Pieces - 1)) ParkedBackedIn++;
            else ParkedNoseIn++;

            TakeThePiece(car, 0);
        }

        Cars.AuthorityM[car] = float.PositiveInfinity;
        return true;
    }

    /// <summary>
    /// <b>One piece of the manoeuvre taken as the car's line</b>, driven in its own gear. It is the one place a line
    /// that is not the route's is written, and the piece it is is written with it.
    /// </summary>
    /// <remarks>
    /// <b>Taken, it grants nothing</b> until its ground is read again against it — in the rebuild after, or where the
    /// manoeuvre is kept (<see cref="KeepOrWithdrawTheManoeuvre"/>): the grant the last rebuild read was down another
    /// line.
    /// </remarks>
    void TakeThePiece(int car, int piece)
    {
        var arcs = _manoeuvres.PieceOf(car, piece);
        arcs.CopyTo(Cars.LineArcsOf(car));
        CornerLimits.Lay(arcs, Cars.LineEntriesOf(car), _config);
        Cars.Line[car] = new DrivenLine(arcs.Length, 0, Spline.TotalLengthM(arcs));
        Cars.LineIsReverse[car] = _manoeuvres.Shape[car].IsReverse(piece);
        _manoeuvres.Piece[car] = piece;

        Cars.AuthorityM[car] = 0f;
        Cars.HorizonM[car] = float.PositiveInfinity;
        Cars.GrantMarginM[car] = 0f;

        ref readonly var build = ref Cars.BuildOf(car);
        var axleM = CarFollower.RearAxleM(build, Cars.PositionM[car], ForwardOf(car));
        Cars.ProgressM[car] = CarFollower.ProgressM(build, arcs, axleM, 0f);
    }

    /// <summary>
    /// <b>A piece of a manoeuvre driven to its end</b>: the next piece, or — at the end of the last — the car parked,
    /// or out on its lane with the bay the town's again (GEN-4l's turn with it).
    /// </summary>
    /// <remarks>
    /// <b>A bay turned in is not a bay parked in</b> (GEN-4l): the leg keeps the place it is going to and the car
    /// comes straight back out, onto the lane running towards it, holding the turning bay until it is out.
    /// </remarks>
    bool TheManoeuvreIsDriven(int car)
    {
        if (!_manoeuvres.OnTheLastPiece(car))
        {
            TakeThePiece(car, _manoeuvres.Piece[car] + 1);
            return true;
        }

        var kind = _manoeuvres.Kind[car];
        var bay = _manoeuvres.Bay[car];
        _manoeuvres.Clear(car);
        if (kind == ManoeuvreKind.Park)
        {
            if (bay == _parking.TurnOf(car) && ShapeTheWayOut(car, bay))
            {
                Enter(car, CarAction.Unpark);
                return true;
            }

            ParkIt(car, bay);
            return true;
        }

        _parking.Vacate(car);
        _parking.LeaveTheTurn(car);
        return TakeTheRoad(car);
    }

    /// <summary>Whether the car's line is a piece of its manoeuvre rather than the route's chain.</summary>
    bool IsManoeuvring(int car) =>
        Cars.Action[car] == CarAction.Unpark || (Cars.Action[car] == CarAction.Park && _manoeuvres.IsBegun(car));

    /// <summary>
    /// <b>The ground a car's manoeuvre will cover, laid as a body</b> (TER-4c.6) — once asked and every rebuild after
    /// it is begun, from where the car stands on its piece to the end of its last. <b>A manoeuvre into a bay the line
    /// stops for no longer is given up here</b>, for the road.
    /// </summary>
    void LayTheCarsManoeuvre(int car)
    {
        if (!IsAtABay(Cars.Action[car])) return;

        if (Cars.Action[car] == CarAction.Park && !_manoeuvres.IsBegun(car)
            && Cars.StopsForBayOf(car) != _manoeuvres.Bay[car])
        {
            Enter(car, CarAction.Follow);
            return;
        }

        if (_manoeuvres.Stage[car] is ManoeuvreStage.None or ManoeuvreStage.Shaped) return;

        OverTheGround(car, GroundAsk.Lay);
    }

    /// <summary>
    /// <b>What a car's manoeuvre grants it</b>: the whole of its ground, but short of a body standing inside what is
    /// left of it — the one thing that ends its ground short of its end, since nothing planned can be laid over it
    /// (TER-4c.1): where it stood at the last station of this piece with nobody on its ground. And a manoeuvre not
    /// yet begun is ground the car does not have, so it stands where it is.
    /// </summary>
    void HoldTheManoeuvre(int car)
    {
        if (!Cars.Driven[car] || Cars.Broken[car]) return;

        var standOffM = _config.Driving.StandOffM;
        if (!_manoeuvres.IsBegun(car))
        {
            var waiting = _occupancy.BeginHold(standOffM);
            _carHold[car] = waiting;
            _occupancy.EndHold(waiting, Cars.ClaimFromM[car], 0f, LaneClaim.Nothing);
            return;
        }

        if (!TheBodyInTheManoeuvre(car, out var inTheWayM, out var body, out var on))
        {
            Cars.AuthorityM[car] = float.PositiveInfinity;
            return;
        }

        var held = _occupancy.BeginHold(standOffM);
        _carHold[car] = held;
        _occupancy.EndHold(held, inTheWayM, standOffM, body, on);
    }

    [SkipLocalsInit]
    bool TheBodyInTheManoeuvre(int car, out float inTheWayM, out LaneClaim body, out int on)
    {
        var piece = _manoeuvres.PieceOf(car, _manoeuvres.Piece[car]);
        var reverse = Cars.LineIsReverse[car];
        var fromM = Cars.ProgressM[car];
        var toM = Cars.Line[car].LengthM;
        var clearM = fromM;
        Span<WayCover> under = stackalloc WayCover[MostWaysUnderABody];
        for (var station = 0; station < StationsOfThePass(car, fromM, toM); station++)
        {
            var count = UnderTheCarOnThePiece(car, piece, reverse, fromM, toM, station, 0f, under, out var atM);
            for (var at = 0; at < count; at++)
            {
                ref readonly var swept = ref under[at];
                if (!IsCarriageway(swept.Way)) continue;
                if (!_occupancy.AheadBody(swept.Way, swept.FromM, swept.ToM, car, out body)) continue;

                on = swept.Way;
                inTheWayM = clearM + LeadingEdgeAheadOfTheAxleM(car);
                return true;
            }

            clearM = atM;
        }

        inTheWayM = float.PositiveInfinity;
        body = LaneClaim.Nothing;
        on = LaneOccupancy.NoHold;
        return false;
    }

    /// <summary>What <see cref="OverTheGround"/> does with each stretch of a manoeuvre's ground.</summary>
    enum GroundAsk : byte
    {
        /// <summary>Whether nobody has it, with room to spare — asked before it is laid.</summary>
        Free,

        /// <summary>Whether it is still the car's once laid — asked once, in the rebuild after.</summary>
        Keep,

        /// <summary>Laid, as a body.</summary>
        Lay,
    }

    /// <summary>
    /// <b>Every stretch of the ground a manoeuvre has still to cover</b>, from where the car stands on the piece it is
    /// driving — or from the start of its first, where it has not begun — to the end of its last, asked or laid as
    /// <paramref name="ask"/> says. False at the first stretch a question refuses.
    /// </summary>
    /// <remarks>
    /// <b>Asked for with room to spare and laid and kept without it</b>, as a pass is
    /// (<see cref="DrivingFigures.PassSpareM"/>), so a manoeuvre clearing something by a hair is not asked one rebuild
    /// and withdrawn the next.
    /// </remarks>
    [SkipLocalsInit]
    bool OverTheGround(int car, GroundAsk ask)
    {
        var shape = _manoeuvres.Shape[car];
        var begun = _manoeuvres.IsBegun(car) && IsManoeuvring(car);
        var firstPiece = begun ? _manoeuvres.Piece[car] : 0;
        var spareM = ask == GroundAsk.Free ? _config.Driving.PassSpareM : 0f;
        Span<WayCover> under = stackalloc WayCover[MostWaysUnderABody];
        for (var piece = firstPiece; piece < shape.Pieces; piece++)
        {
            var line = _manoeuvres.PieceOf(car, piece);
            var fromM = begun && piece == firstPiece ? Cars.ProgressM[car] : 0f;
            var toM = Spline.TotalLengthM(line);
            var reverse = shape.IsReverse(piece);
            for (var station = 0; station < StationsOfThePass(car, fromM, toM); station++)
            {
                var count = UnderTheCarOnThePiece(car, line, reverse, fromM, toM, station, spareM, under, out _);
                for (var at = 0; at < count; at++)
                {
                    ref readonly var swept = ref under[at];
                    if (!IsCarriageway(swept.Way)) continue;

                    var held = HeldByThePass(swept);
                    switch (ask)
                    {
                        case GroundAsk.Lay:
                            _occupancy.LayPass(held.Way, held.FromM, held.ToM, 0f, car, LaneRoster.Driving);
                            break;

                        case GroundAsk.Free when !_occupancy.IsFreeForAPass(
                            held.Way, swept.FromM, swept.ToM, held.FromM, held.ToM, car, LaneRoster.Driving, [],
                            PassTerms.Plain):
                        case GroundAsk.Keep when !_occupancy.KeepsItsPass(
                            held.Way, swept.FromM, swept.ToM, held.FromM, held.ToM, car, LaneRoster.Driving,
                            PassTerms.Plain):
                            return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// <b>How much street a shape takes</b> — the metres of the street's own ways its body is swept over, each way's
    /// stretch once however many stations lie on it, and a bay's own ground left out, since that is what the car is
    /// getting into or out of. <b>False where its body leaves the driven ground or lies over a zebra's paint</b>
    /// (TER-4c.6): a shape run off the road, or through a crossing, is not one the car makes.
    /// </summary>
    [SkipLocalsInit]
    bool WhatTheShapeTakes(int car, ReadOnlySpan<ArcSeg> arcs, in BayManoeuvre.Shape shape, out float streetM)
    {
        streetM = 0f;
        Span<WayCover> under = stackalloc WayCover[MostWaysUnderABody];
        Span<WayCover> taken = stackalloc WayCover[MostStreetWaysTaken];
        var takenCount = 0;
        for (var piece = 0; piece < shape.Pieces; piece++)
        {
            var line = arcs.Slice(shape.FirstArcOf(piece), shape.ArcsOf(piece));
            var toM = Spline.TotalLengthM(line);
            for (var station = 0; station < StationsOfThePass(car, 0f, toM); station++)
            {
                var count = UnderTheCarOnThePiece(car, line, shape.IsReverse(piece), 0f, toM, station, 0f, under, out _);
                for (var at = 0; at < count; at++)
                {
                    ref readonly var cover = ref under[at];
                    if (!IsCarriageway(cover.Way) || CrossesAZebra(cover.Way, cover.FromM, cover.ToM)) return false;
                    if (_ways.KindOf(cover.Way) == WayKind.Lane && _roads.IsABayArm(_ways.RoadLaneOf(cover.Way))) continue;

                    TakenOnce(taken, ref takenCount, cover, ref streetM);
                }
            }
        }

        for (var at = 0; at < takenCount; at++) streetM += taken[at].ToM - taken[at].FromM;

        return true;
    }

    /// <summary>
    /// One stretch of one way a shape takes, grown into what it already takes of that way — or, past
    /// <see cref="MostStreetWaysTaken"/> ways, counted as ground of its own.
    /// </summary>
    static void TakenOnce(Span<WayCover> taken, ref int takenCount, in WayCover cover, ref float overflowM)
    {
        for (var at = 0; at < takenCount; at++)
        {
            if (taken[at].Way != cover.Way) continue;

            taken[at] = taken[at] with
            {
                FromM = MathF.Min(taken[at].FromM, cover.FromM), ToM = MathF.Max(taken[at].ToM, cover.ToM),
            };
            return;
        }

        if (takenCount < taken.Length) taken[takenCount++] = cover;
        else overflowM += cover.ToM - cover.FromM;
    }

    /// <summary>
    /// <b>The ways under the car at one station of a piece</b>: its collider stood where the piece puts the rear axle
    /// and pointed the way the car points there — along the piece, or back along it on a piece driven in reverse —
    /// grown by <paramref name="spareM"/> all round.
    /// </summary>
    int UnderTheCarOnThePiece(
        int car, ReadOnlySpan<ArcSeg> piece, bool reverse, float fromM, float toM, int station, float spareM,
        Span<WayCover> under, out float atM)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        atM = MathF.Min(toM, fromM + (station * build.FlankM));
        var on = Spline.SampleAt(piece, atM);
        var forward = reverse ? -on.Direction : on.Direction;

        var halfM = build.CollisionSizeM * 0.5f;
        var centreM = on.PositionM + (forward * build.CentreAheadOfAxleM);
        return _atlas.UnderBox(centreM, forward, halfM.X + spareM, halfM.Y + spareM, under);
    }
}
