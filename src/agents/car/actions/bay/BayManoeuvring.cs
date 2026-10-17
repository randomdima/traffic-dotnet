using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.Car.Actions;

/// <summary>
/// <b>A car's manoeuvre at a bay</b> (GEN-4f), whichever way it is made — into one (<see cref="ParkingIn"/>) or out
/// of one (<see cref="PullingOut"/>): a shape the car lays for itself off its own circle from where it stands, asked
/// for where no body stands on its ground, laid as a plan of its own over that ground, kept or withdrawn once, and
/// driven a piece at a time in each piece's gear.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its ground is a pass's</b> (TER-4c.6): the stretch of each lane the bay is worked off that the car's body stands
/// over somewhere down the shape (<see cref="StreetUnder"/>), asked whole — at <see cref="ClaimPriority.Committed"/> once
/// begun, since a car half across a street cannot give the street back — and given back as it is driven. So the traffic
/// is held off a car manoeuvring, and a car manoeuvring off the traffic, by the ground each holds and by nothing else
/// (SIM-7).
/// </para>
/// <para>
/// <b>Nothing here reads another agent</b> (TER-4c.5): whether the ground is free is what is laid on the lanes under
/// it.
/// </para>
/// </remarks>
internal sealed class BayManoeuvring(DrivingGround ground, CarActions actions, Manoeuvres manoeuvres, BayStreets streets)
{
    /// <summary>
    /// How many of the street's ways one shape's ground is totalled over. A bound on a stack span and not a figure
    /// behaviour reads: a way past it is still totalled, as ground of its own.
    /// </summary>
    const int MostStreetWaysTaken = 16;

    /// <summary>
    /// Which ask each car's manoeuvre is now, counted per car so that an ask writes nothing another car reads — none
    /// before its first.
    /// </summary>
    readonly long[] _askedAs = new long[manoeuvres.Piece.Length];

    /// <summary>
    /// And which ask the claims were last laid for — what an ask is answered after, kept or withdrawn, and not before:
    /// the claims are laid once a decision interval.
    /// </summary>
    readonly long[] _laidFor = new long[manoeuvres.Piece.Length];

    /// <summary>The hold each car's manoeuvre was asked in at its last turn, or <see cref="LaneOccupancy.NoHold"/>.</summary>
    readonly int[] _holds = LaneOccupancy.NoHolds(manoeuvres.Piece.Length);

    /// <summary>
    /// <b>The street under each piece of a car's manoeuvre, whole</b> (<see cref="StreetUnder"/> from its start, no room
    /// to spare): a shape's own until it is shaped again, which is only ever followed by an ask — so it is swept once an
    /// ask (<see cref="_sweptFor"/>), not at every turn it is laid at. <see cref="BayStreets.MostLanes"/> runs a piece.
    /// </summary>
    readonly StreetRun[] _swept = new StreetRun[manoeuvres.Piece.Length * BayManoeuvre.MostPieces * BayStreets.MostLanes];

    readonly byte[] _sweptRuns = new byte[manoeuvres.Piece.Length * BayManoeuvre.MostPieces];

    /// <summary>The ask each car's <see cref="_swept"/> was swept for; none before its first.</summary>
    readonly long[] _sweptFor = new long[manoeuvres.Piece.Length];

    /// <summary>
    /// <b>The street under each piece of a car's shape, with room to spare</b> (<see cref="OverTheGround"/>): the shape's
    /// own until it is shaped again (<see cref="Manoeuvres.ShapedAs"/>), so a car waiting in its bay to pull out, asking the
    /// same shape every <see cref="DrivingFigures.ManoeuvreAskEveryS"/>, sweeps it once.
    /// </summary>
    readonly StreetRun[] _spared = new StreetRun[manoeuvres.Piece.Length * BayManoeuvre.MostPieces * BayStreets.MostLanes];

    readonly byte[] _sparedRuns = new byte[manoeuvres.Piece.Length * BayManoeuvre.MostPieces];

    /// <summary>The shape each car's <see cref="_spared"/> was swept for; none before its first.</summary>
    readonly uint[] _sparedFor = new uint[manoeuvres.Piece.Length];

    CarFleet Cars => ground.Cars;

    LaneOccupancy Occupancy => ground.Occupancy;

    SimConfig Config => ground.Config;

    /// <summary>Every car's manoeuvre — its shape, its stage and its piece.</summary>
    public Manoeuvres Manoeuvres => manoeuvres;

    /// <summary>Manoeuvres whose ground was asked for since the town was laid (GEN-4f).</summary>
    public long Asked { get; private set; }

    /// <summary>And withdrawn once laid, because a body or another pass had the ground by then.</summary>
    public long Withdrawn { get; private set; }

    /// <summary>And begun: ground the car then held until it had driven it.</summary>
    public long Begun { get; private set; }

    /// <summary>How many of those begun were into a bay nose first, and how many backwards.</summary>
    public long ParkedNoseIn { get; private set; }

    /// <inheritdoc cref="ParkedNoseIn"/>
    public long ParkedBackedIn { get; private set; }

    /// <summary>How many of those were alongside a kerb (GEN-4n) rather than into a painted bay.</summary>
    public long ParkedAtTheKerb { get; private set; }

    /// <summary>
    /// <b>The street every manoeuvre begun took</b>, in metres of the street's own ways — the figure each car's
    /// choice of shape is the least of.
    /// </summary>
    public double StreetM { get; private set; }

    /// <summary>How long until a car waiting on its manoeuvre shapes it and asks for it again (<see cref="DrivingFigures.ManoeuvreAskEveryS"/>).</summary>
    readonly float[] _asksInS = new float[manoeuvres.Piece.Length];

    /// <summary>
    /// <b>Whether a car waiting on its manoeuvre shapes it and asks for it at this decision</b> — no sooner than
    /// <see cref="DrivingFigures.ManoeuvreAskEveryS"/> after it last did: each is a sweep of the whole shape.
    /// </summary>
    /// <param name="sinceLastDecisionS">How much of the town's time this decision answers for, which the clock runs by.</param>
    public bool IsTimeToAsk(int car, float sinceLastDecisionS)
    {
        ref var inS = ref _asksInS[car];
        inS -= sinceLastDecisionS;
        if (inS > 0f) return false;

        inS = Config.Driving.ManoeuvreAskEveryS;
        return true;
    }

    /// <summary>
    /// A shaped manoeuvre asked for where every metre of its ground is free (TER-4c.6), to be laid when the car next
    /// claims.
    /// </summary>
    public void Ask(int car)
    {
        _asksInS[car] = Config.Driving.ManoeuvreAskEveryS;
        if (!OverTheGround(car)) return;

        manoeuvres.Stage[car] = ManoeuvreStage.Asked;
        Asked++;
        _askedAs[car]++;
    }

    /// <summary>
    /// <b>A manoeuvre laid at the car's last turn, kept or withdrawn</b> before the car moves on it — kept where the
    /// whole of its ground was had, as a pass is (<see cref="DrivingGround.IsKept"/>). <b>Kept, the whole of its ground
    /// is the car's from this tick</b>, and a car getting into a bay takes up its first piece in place of the route's
    /// line. One the claims have not been laid for since it was asked waits for them.
    /// </summary>
    public bool KeepOrWithdraw(int car)
    {
        if (_laidFor[car] != _askedAs[car]) return false;

        if (!ground.IsKept(_holds[car]))
        {
            manoeuvres.Stage[car] = ManoeuvreStage.Shaped;
            Withdrawn++;
            return false;
        }

        manoeuvres.Stage[car] = ManoeuvreStage.Begun;
        Begun++;

        // The next manoeuvre this car waits on is asked for at its first decision.
        _asksInS[car] = 0f;
        StreetM += manoeuvres.StreetM[car];
        if (manoeuvres.Kind[car] == ManoeuvreKind.Park)
        {
            if (manoeuvres.Shape[car].IsReverse(manoeuvres.Shape[car].Pieces - 1)) ParkedBackedIn++;
            else ParkedNoseIn++;

            if (streets.KerbRoadsideOf(manoeuvres.Bay[car]) >= 0) ParkedAtTheKerb++;

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
    /// <b>Taken, it grants nothing</b> until its ground is read again against it — at the car's next turn, or where the
    /// manoeuvre is kept (<see cref="KeepOrWithdraw"/>): the grant its last turn read was down another line.
    /// </remarks>
    public void TakeThePiece(int car, int piece)
    {
        var arcs = manoeuvres.PieceOf(car, piece);
        arcs.CopyTo(Cars.LineArcsOf(car));
        CornerLimits.Lay(arcs, Cars.LineEntriesOf(car), Config);
        Cars.SetLine(car, new DrivenLine(arcs.Length, 0, Spline.TotalLengthM(arcs)));
        Cars.LineIsReverse[car] = manoeuvres.Shape[car].IsReverse(piece);
        manoeuvres.Piece[car] = piece;

        Cars.AuthorityM[car] = 0f;
        Cars.CoveredSinceClaimM[car] = 0f;
        Cars.HorizonM[car] = float.PositiveInfinity;
        Cars.GrantMarginM[car] = 0f;

        ref readonly var build = ref Cars.BuildOf(car);
        var axleM = CarFollower.RearAxleM(build, Cars.PositionM[car], Cars.Forward[car]);
        Cars.ProgressM[car] = CarFollower.ProgressM(build, arcs, axleM, 0f);
    }

    /// <summary>
    /// <b>The next piece of a manoeuvre, taken where the one driven is not its last</b> — false where it is, and the
    /// manoeuvre is done.
    /// </summary>
    public bool TakeTheNextPiece(int car)
    {
        if (manoeuvres.OnTheLastPiece(car)) return false;

        TakeThePiece(car, manoeuvres.Piece[car] + 1);
        return true;
    }

    /// <summary>
    /// <b>A car whose line is a piece of its manoeuvre at a bay</b> (GEN-4f), driven in the piece's own gear. The
    /// same wheel and the same profile as a route, and the same reservations underneath: the ground it drives is
    /// its own, held as a body, and the grant is cut only by a body standing in it.
    /// </summary>
    public void DriveThePiece<TTown>(ref TTown town, int car, in CarPose pose)
        where TTown : struct, ICarTown
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var reverse = Cars.LineIsReverse[car];
        var forward = pose.Forward;
        var travel = reverse ? -forward : forward;
        var rearAxleM = CarFollower.RearAxleM(build, pose.PositionM, forward);
        var line = Cars.LineOf(car);
        var lengthM = Cars.Line[car].LengthM;
        var progressM = CarFollower.ProgressM(build, line, rearAxleM, Cars.ProgressM[car]);
        var alongMps = Vector2.Dot(pose.VelocityMps, travel);
        Cars.CoveredSinceClaimM[car] += MathF.Abs(progressM - Cars.ProgressM[car]);
        Cars.ProgressM[car] = progressM;
        Cars.AlongMps[car] = alongMps;
        Cars.OffLineM[car] = CarFollower.OffLineM(line, rearAxleM, progressM);

        var context = new DriveContext(
            Cars.GrantLeftM(car), Cars.GrantCutBy[car], MarginM: Cars.GrantMarginM[car],
            HorizonM: Cars.HorizonM[car] - Cars.CoveredSinceClaimM[car]);

        Cars.Context[car] = context;
        town.Drive(car, build, pose, line, progressM, lengthM, context, travel, alongMps, reverse);
    }

    /// <summary>
    /// <b>The ground a car's manoeuvre will cover, asked as a plan of its own</b> (TER-4c.6) — once asked and every
    /// turn after it is begun, from where the car stands on its piece to the end of its last. <b>A manoeuvre into a bay
    /// the line stops for no longer is given up here</b>, for the road.
    /// </summary>
    public void Lay(int car)
    {
        Occupancy.Release(_holds[car]);
        _holds[car] = LaneOccupancy.NoHold;
        if (!CarActions.IsAtABay(Cars.Action[car])) return;

        if (Cars.Action[car] == CarAction.Park && !manoeuvres.IsBegun(car)
            && Cars.StopsForBayOf(car) != manoeuvres.Bay[car])
        {
            actions.Enter(car, CarAction.Follow);
            return;
        }

        if (manoeuvres.Stage[car] is ManoeuvreStage.None or ManoeuvreStage.Shaped) return;

        var begun = manoeuvres.IsBegun(car);
        if (!begun) _laidFor[car] = _askedAs[car];

        var hold = _holds[car] = Occupancy.BeginHold(car, LaneRoster.Driving, whole: !begun);
        var rung = begun ? ClaimPriority.Committed : ClaimPriority.Firm;
        var progressM = Cars.ProgressM[car];
        var firstPiece = begun ? manoeuvres.Piece[car] : 0;
        var shape = manoeuvres.Shape[car];

        // A body in a piece past this one is met where this one ends, which the car is at rest at before it takes the next.
        var endsM = MathF.Max(progressM, Spline.TotalLengthM(manoeuvres.PieceOf(car, firstPiece)))
                    + ground.LeadingEdgeAheadOfTheAxleM(car);
        Span<StreetRun> runs = stackalloc StreetRun[BayStreets.MostLanes];
        for (var piece = firstPiece; piece < shape.Pieces; piece++)
        {
            var under = begun && piece == firstPiece
                ? runs[..StreetUnder(
                    car, manoeuvres.Bay[car], manoeuvres.PieceOf(car, piece), shape.IsReverse(piece), progressM, 0f, runs)]
                : SweptWhole(car, piece);
            foreach (ref readonly var run in under)
            {
                var stopsAtM = piece > firstPiece ? endsM : ground.StopsShortOfM(car, run.FirstAtM);
                Occupancy.AskSwept(hold, run.Way, run.FromM, run.ToM, stopsAtM, rung);
            }
        }
    }

    /// <summary>The street under one piece of a car's manoeuvre from its start (<see cref="_swept"/>), swept at the first ask that needs it.</summary>
    ReadOnlySpan<StreetRun> SweptWhole(int car, int piece)
    {
        if (_sweptFor[car] != _askedAs[car])
        {
            var shape = manoeuvres.Shape[car];
            for (var each = 0; each < shape.Pieces; each++)
            {
                _sweptRuns[(car * BayManoeuvre.MostPieces) + each] = (byte)StreetUnder(
                    car, manoeuvres.Bay[car], manoeuvres.PieceOf(car, each), shape.IsReverse(each), 0f, 0f, SweptOf(car, each));
            }

            _sweptFor[car] = _askedAs[car];
        }

        return SweptOf(car, piece)[.._sweptRuns[(car * BayManoeuvre.MostPieces) + piece]];
    }

    Span<StreetRun> SweptOf(int car, int piece) => RunsOf(_swept, car, piece);

    static Span<StreetRun> RunsOf(StreetRun[] table, int car, int piece) =>
        table.AsSpan(((car * BayManoeuvre.MostPieces) + piece) * BayStreets.MostLanes, BayStreets.MostLanes);

    /// <summary>
    /// <b>What a car's manoeuvre grants it</b>: once begun, the whole of its ground short of whatever refuses its hold
    /// (<see cref="Lay"/>) — where it stood at the last station of this piece with nobody on its ground. A manoeuvre not
    /// yet begun is ground the car does not have, so it stands where it is.
    /// </summary>
    public void Hold(int car)
    {
        if (!HoldsWhereItStands(car) || !manoeuvres.IsBegun(car)) return;

        ground.BeginThePlan(car, Config.Driving.StandOffM, _holds[car]);
    }

    /// <summary>Whether a car manoeuvring at a bay holds anything where it stands (<see cref="Hold"/>): one driven and whole.</summary>
    bool HoldsWhereItStands(int car) => Cars.Driven[car] && !Cars.Broken[car];

    /// <summary>
    /// <b>Whether nobody stands on any stretch of a shaped manoeuvre's ground</b>, grown by room to spare — asked before
    /// it is laid, as a pass is (<see cref="DrivingFigures.PassSpareM"/>), so a manoeuvre clearing something by a hair is
    /// not asked only to be withdrawn once laid. False at the first stretch that is not. Whether a plan holds any of it
    /// is answered when it is laid, after every plan of its rung.
    /// </summary>
    bool OverTheGround(int car)
    {
        var shape = manoeuvres.Shape[car];
        if (_sparedFor[car] != manoeuvres.ShapedAs[car])
        {
            for (var piece = 0; piece < shape.Pieces; piece++)
            {
                _sparedRuns[(car * BayManoeuvre.MostPieces) + piece] = (byte)StreetUnder(
                    car, manoeuvres.Bay[car], manoeuvres.PieceOf(car, piece), shape.IsReverse(piece), 0f,
                    Config.Driving.PassSpareM, RunsOf(_spared, car, piece));
            }

            _sparedFor[car] = manoeuvres.ShapedAs[car];
        }

        for (var piece = 0; piece < shape.Pieces; piece++)
        {
            foreach (ref readonly var run in RunsOf(_spared, car, piece)[.._sparedRuns[(car * BayManoeuvre.MostPieces) + piece]])
            {
                if (Occupancy.AnyBodyOver(run.Way, run.FromM, run.ToM, car, LaneRoster.Driving)) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// <b>How much street a shape takes</b> — the metres of the lanes the bay is worked off that its body stands over,
    /// each lane's stretch once however many pieces lie on it, and an aisle of a car park left out, since that is what
    /// the car is getting into or out of — <b>and whether a body stands on any of it</b>. <b>False where it lies over a
    /// zebra's paint</b> (TER-4c.6): a shape run through a crossing is not one the car makes.
    /// </summary>
    public bool WhatTheShapeTakes(
        int car, int bay, ReadOnlySpan<ArcSeg> arcs, in BayManoeuvre.Shape shape, out float streetM, out bool stoodOn)
    {
        streetM = 0f;
        stoodOn = false;
        Span<StreetRun> runs = stackalloc StreetRun[BayStreets.MostLanes];
        Span<WayCover> taken = stackalloc WayCover[MostStreetWaysTaken];
        var takenCount = 0;
        for (var piece = 0; piece < shape.Pieces; piece++)
        {
            var line = arcs.Slice(shape.FirstArcOf(piece), shape.ArcsOf(piece));
            foreach (ref readonly var run in runs[..StreetUnder(car, bay, line, shape.IsReverse(piece), 0f, 0f, runs)])
            {
                if (ground.CrossesAZebra(run.Way, run.FromM, run.ToM)) return false;

                stoodOn = stoodOn || Occupancy.AnyBodyOver(run.Way, run.FromM, run.ToM, car, LaneRoster.Driving);
                if (ground.Roads.IsABayArm(ground.Ways.RoadLaneOf(run.Way))) continue;

                TakenOnce(taken, ref takenCount, new WayCover(run.Way, run.FromM, run.ToM), ref streetM);
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
    /// <b>The street a piece of a manoeuvre is driven over</b> (TER-4c.6): for each lane the bay is worked off, the
    /// stretch of it the car's body stands over at some station of the piece from <paramref name="fromM"/> on — where
    /// the body's middle stands within half the lane and the body's half reach of the lane's line, its half reach
    /// either side along it — grown by <paramref name="spareM"/>, with the piece's metre it first stands on the lane at.
    /// </summary>
    /// <remarks>
    /// <b>Read off the lanes the bay is worked off and no others</b> — and, for a place at the kerb, the roadside it stands
    /// on (GEN-4n), since that is ground other bodies stand on and a bay's own lane is not: those are what a manoeuvre
    /// comes from or goes to, and a body swung past them is the world's to meet. Each station is found on a lane from the
    /// bay's own metre on it, which is where the shape was drawn from.
    /// </remarks>
    int StreetUnder(
        int car, int bay, ReadOnlySpan<ArcSeg> piece, bool reverse, float fromM, float spareM, Span<StreetRun> into)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var reachM = (build.CollisionSizeM * 0.5f).Length() + spareM;
        var toM = Spline.TotalLengthM(piece);
        var stations = ground.StationsOfTheSweep(car, fromM, toM);
        var count = 0;
        var worked = streets.LanesOf(bay);
        var roadside = streets.KerbRoadsideOf(bay);
        var lanes = worked.Length + (roadside >= 0 ? 1 : 0);
        for (var each = 0; each < lanes; each++)
        {
            var lane = each < worked.Length ? worked[each] : roadside;
            var arcs = ground.Roads.ArcsOf(lane);
            var lengthM = ground.Roads.LaneLengthM[lane];
            var aroundM = each < worked.Length ? streets.AtLaneM(bay, lane) : streets.KerbRoadsideAtM(bay);
            var windowM = toM + build.LengthM;
            var halfLaneM = ground.Roads.LaneWidthM[lane] * 0.5f;
            var acrossM = halfLaneM + reachM;
            var atTheKerb = each >= worked.Length;
            var leastM = float.PositiveInfinity;
            var mostM = float.NegativeInfinity;
            var firstAtM = float.PositiveInfinity;
            var lastAtM = float.NegativeInfinity;
            for (var station = 0; station < stations; station++)
            {
                var atM = MathF.Min(toM, fromM + (station * build.FlankM));
                var on = Spline.SampleAt(piece, atM);
                var centreM = on.PositionM + ((reverse ? -on.Direction : on.Direction) * build.CentreAheadOfAxleM);
                var alongM = Spline.ProjectM(arcs, centreM, aroundM, windowM, out var offSq);
                if (offSq > acrossM * acrossM) continue;

                // <b>The kerb is read by the body as it is turned</b> (GEN-4n): it runs beside the very lane the car drives up,
                // nearer than a body's half-diagonal, so read round the diagonal every station of the car going by stood
                // over it, and the cars parked either side of a gap refused the slide into it.
                var stationReachM = reachM;
                if (atTheKerb)
                {
                    var laneAlong = Spline.SampleAt(arcs, alongM).Direction;
                    var (sin, cos) = (MathF.Abs(Spline.Cross(on.Direction, laneAlong)), MathF.Abs(Vector2.Dot(on.Direction, laneAlong)));
                    var (halfLengthM, halfWidthM) = (build.CollisionSizeM.X * 0.5f, build.CollisionSizeM.Y * 0.5f);
                    var outM = (halfLengthM * sin) + (halfWidthM * cos) + spareM;
                    if (offSq > (halfLaneM + outM) * (halfLaneM + outM)) continue;

                    stationReachM = (halfLengthM * cos) + (halfWidthM * sin) + spareM;
                }

                leastM = MathF.Min(leastM, alongM - stationReachM);
                mostM = MathF.Max(mostM, alongM + stationReachM);
                firstAtM = MathF.Min(firstAtM, atM);
                lastAtM = MathF.Max(lastAtM, atM);
            }

            leastM = MathF.Max(0f, leastM);
            mostM = MathF.Min(lengthM, mostM);
            if (mostM > leastM) into[count++] = new StreetRun(ground.Ways.OfRoadLane(lane), leastM, mostM, firstAtM, lastAtM);
        }

        return count;
    }
}

/// <summary>
/// One lane under a piece of a manoeuvre (<see cref="BayManoeuvring"/>): the stretch of it, in the lane's own metres, that
/// the body stands over at any station from <paramref name="FirstAtM"/> to <paramref name="LastAtM"/> along the piece.
/// </summary>
internal readonly record struct StreetRun(int Way, float FromM, float ToM, float FirstAtM, float LastAtM);
