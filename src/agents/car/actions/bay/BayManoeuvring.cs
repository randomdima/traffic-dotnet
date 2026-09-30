using System.Numerics;
using System.Runtime.CompilerServices;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.Car.Actions;

/// <summary>
/// <b>A car's manoeuvre at a bay</b> (GEN-4f), whichever way it is made — into one (<see cref="ParkingIn"/>) or out
/// of one (<see cref="PullingOut"/>): a shape the car lays for itself off its own circle from where it stands, asked
/// for where all of its ground is free, laid as a body over that ground, kept or withdrawn once, and driven a piece at
/// a time in each piece's gear.
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
/// </remarks>
internal sealed class BayManoeuvring(DrivingGround ground, CarActions actions, Manoeuvres manoeuvres)
{
    /// <summary>
    /// How many of the street's ways one shape's ground is totalled over. A bound on a stack span and not a figure
    /// behaviour reads: a way past it is still totalled, as ground of its own.
    /// </summary>
    const int MostStreetWaysTaken = 16;

    CarFleet Cars => ground.Cars;

    LaneOccupancy Occupancy => ground.Occupancy;

    SimConfig Config => ground.Config;

    /// <summary>Every car's manoeuvre — its shape, its stage and its piece.</summary>
    public Manoeuvres Manoeuvres => manoeuvres;

    /// <summary>Manoeuvres whose ground was asked for since the town was laid (GEN-4f).</summary>
    public long Asked { get; private set; }

    /// <summary>And withdrawn in the rebuild after, because a body or another pass had the ground by then.</summary>
    public long Withdrawn { get; private set; }

    /// <summary>And begun: ground the car then held until it had driven it.</summary>
    public long Begun { get; private set; }

    /// <summary>How many of those begun were into a bay nose first, and how many backwards.</summary>
    public long ParkedNoseIn { get; private set; }

    /// <inheritdoc cref="ParkedNoseIn"/>
    public long ParkedBackedIn { get; private set; }

    /// <summary>
    /// <b>The street every manoeuvre begun took</b>, in metres of the street's own ways — the figure each car's
    /// choice of shape is the least of.
    /// </summary>
    public double StreetM { get; private set; }

    /// <summary>A shaped manoeuvre asked for where every metre of its ground is free (TER-4c.6), to be laid in the next rebuild.</summary>
    public void Ask(int car)
    {
        if (!OverTheGround(car, GroundAsk.Free)) return;

        manoeuvres.Stage[car] = ManoeuvreStage.Asked;
        Asked++;
    }

    /// <summary>
    /// <b>A manoeuvre laid in this rebuild, kept or withdrawn</b> before the car moves on it — kept where nothing but
    /// the car itself is on its ground, as a pass is (<see cref="LaneOccupancy.KeepsItsPass(int, float, float, float, float, int, LaneRoster, in PassTerms)"/>).
    /// <b>Kept, the whole of its ground is the car's from this tick</b>, and a car getting into a bay takes up its
    /// first piece in place of the route's line.
    /// </summary>
    public bool KeepOrWithdraw(int car)
    {
        if (!OverTheGround(car, GroundAsk.Keep))
        {
            manoeuvres.Stage[car] = ManoeuvreStage.Shaped;
            Withdrawn++;
            return false;
        }

        manoeuvres.Stage[car] = ManoeuvreStage.Begun;
        Begun++;
        StreetM += manoeuvres.StreetM[car];
        if (manoeuvres.Kind[car] == ManoeuvreKind.Park)
        {
            if (manoeuvres.Shape[car].IsReverse(manoeuvres.Shape[car].Pieces - 1)) ParkedBackedIn++;
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
    /// manoeuvre is kept (<see cref="KeepOrWithdraw"/>): the grant the last rebuild read was down another line.
    /// </remarks>
    public void TakeThePiece(int car, int piece)
    {
        var arcs = manoeuvres.PieceOf(car, piece);
        arcs.CopyTo(Cars.LineArcsOf(car));
        CornerLimits.Lay(arcs, Cars.LineEntriesOf(car), Config);
        Cars.Line[car] = new DrivenLine(arcs.Length, 0, Spline.TotalLengthM(arcs));
        Cars.LineIsReverse[car] = manoeuvres.Shape[car].IsReverse(piece);
        manoeuvres.Piece[car] = piece;

        Cars.AuthorityM[car] = 0f;
        Cars.HorizonM[car] = float.PositiveInfinity;
        Cars.GrantMarginM[car] = 0f;

        ref readonly var build = ref Cars.BuildOf(car);
        var axleM = CarFollower.RearAxleM(build, Cars.PositionM[car], Heading.Unit(Cars.HeadingRad[car]));
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
        var coveredM = MathF.Abs(progressM - Cars.ProgressM[car]);

        Cars.ProgressM[car] = progressM;
        Cars.AlongMps[car] = alongMps;
        Cars.OffLineM[car] = CarFollower.OffLineM(line, rearAxleM, progressM);
        Cars.GroundCoefficient[car] = town.GroundCoefficientAt(pose.PositionM);
        Cars.CommittedToTheBox[car] = false;

        var context = new DriveContext(
            Cars.GroundCoefficient[car], Cars.AuthorityM[car] - coveredM, Cars.GrantCutBy[car],
            MarginM: Cars.GrantMarginM[car], HorizonM: Cars.HorizonM[car] - coveredM);

        Cars.Context[car] = context;
        town.Drive(car, build, pose, line, progressM, lengthM, context, travel, alongMps, reverse);
    }

    /// <summary>
    /// <b>The ground a car's manoeuvre will cover, laid as a body</b> (TER-4c.6) — once asked and every rebuild after
    /// it is begun, from where the car stands on its piece to the end of its last. <b>A manoeuvre into a bay the line
    /// stops for no longer is given up here</b>, for the road.
    /// </summary>
    public void Lay(int car)
    {
        if (!CarActions.IsAtABay(Cars.Action[car])) return;

        if (Cars.Action[car] == CarAction.Park && !manoeuvres.IsBegun(car)
            && Cars.StopsForBayOf(car) != manoeuvres.Bay[car])
        {
            actions.Enter(car, CarAction.Follow);
            return;
        }

        if (manoeuvres.Stage[car] is ManoeuvreStage.None or ManoeuvreStage.Shaped) return;

        OverTheGround(car, GroundAsk.Lay);
    }

    /// <summary>
    /// <b>What a car's manoeuvre grants it</b>: the whole of its ground, but short of a body standing inside what is
    /// left of it — the one thing that ends its ground short of its end, since nothing planned can be laid over it
    /// (TER-4c.1): where it stood at the last station of this piece with nobody on its ground. And a manoeuvre not
    /// yet begun is ground the car does not have, so it stands where it is.
    /// </summary>
    public void Hold(int car)
    {
        if (!Cars.Driven[car] || Cars.Broken[car]) return;

        var standOffM = Config.Driving.StandOffM;
        if (!manoeuvres.IsBegun(car))
        {
            var waiting = Occupancy.BeginHold(standOffM);
            ground.PlanHold[car] = waiting;
            Occupancy.EndHold(waiting, Cars.ClaimFromM[car], 0f, LaneClaim.Nothing);
            return;
        }

        if (!TheBodyInTheManoeuvre(car, out var inTheWayM, out var body, out var on))
        {
            Cars.AuthorityM[car] = float.PositiveInfinity;
            return;
        }

        var held = Occupancy.BeginHold(standOffM);
        ground.PlanHold[car] = held;
        Occupancy.EndHold(held, inTheWayM, standOffM, body, on);
    }

    [SkipLocalsInit]
    bool TheBodyInTheManoeuvre(int car, out float inTheWayM, out LaneClaim body, out int on)
    {
        var piece = manoeuvres.PieceOf(car, manoeuvres.Piece[car]);
        var reverse = Cars.LineIsReverse[car];
        var fromM = Cars.ProgressM[car];
        var toM = Cars.Line[car].LengthM;
        var clearM = fromM;
        Span<WayCover> under = stackalloc WayCover[RibbonAtlas.MostWaysUnderABody];
        for (var station = 0; station < ground.StationsOfTheSweep(car, fromM, toM); station++)
        {
            var count = UnderTheCarOnThePiece(car, piece, reverse, fromM, toM, station, 0f, under, out var atM);
            for (var at = 0; at < count; at++)
            {
                ref readonly var swept = ref under[at];
                if (!ground.IsCarriageway(swept.Way)) continue;
                if (!Occupancy.AheadBody(swept.Way, swept.FromM, swept.ToM, car, out body)) continue;

                on = swept.Way;
                inTheWayM = clearM + ground.LeadingEdgeAheadOfTheAxleM(car);
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
        var shape = manoeuvres.Shape[car];
        var begun = manoeuvres.IsBegun(car) && actions.IsManoeuvring(car);
        var firstPiece = begun ? manoeuvres.Piece[car] : 0;
        var spareM = ask == GroundAsk.Free ? Config.Driving.PassSpareM : 0f;
        Span<WayCover> under = stackalloc WayCover[RibbonAtlas.MostWaysUnderABody];
        for (var piece = firstPiece; piece < shape.Pieces; piece++)
        {
            var line = manoeuvres.PieceOf(car, piece);
            var fromM = begun && piece == firstPiece ? Cars.ProgressM[car] : 0f;
            var toM = Spline.TotalLengthM(line);
            var reverse = shape.IsReverse(piece);
            for (var station = 0; station < ground.StationsOfTheSweep(car, fromM, toM); station++)
            {
                var count = UnderTheCarOnThePiece(car, line, reverse, fromM, toM, station, spareM, under, out _);
                for (var at = 0; at < count; at++)
                {
                    ref readonly var swept = ref under[at];
                    if (!ground.IsCarriageway(swept.Way)) continue;

                    var held = ground.HeldAsABody(swept);
                    switch (ask)
                    {
                        case GroundAsk.Lay:
                            Occupancy.LayPass(held.Way, held.FromM, held.ToM, 0f, car, LaneRoster.Driving);
                            break;

                        case GroundAsk.Free when !Occupancy.IsFreeForAPass(
                            held.Way, swept.FromM, swept.ToM, held.FromM, held.ToM, car, LaneRoster.Driving, [],
                            PassTerms.Plain):
                        case GroundAsk.Keep when !Occupancy.KeepsItsPass(
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
    public bool WhatTheShapeTakes(int car, ReadOnlySpan<ArcSeg> arcs, in BayManoeuvre.Shape shape, out float streetM)
    {
        streetM = 0f;
        Span<WayCover> under = stackalloc WayCover[RibbonAtlas.MostWaysUnderABody];
        Span<WayCover> taken = stackalloc WayCover[MostStreetWaysTaken];
        var takenCount = 0;
        for (var piece = 0; piece < shape.Pieces; piece++)
        {
            var line = arcs.Slice(shape.FirstArcOf(piece), shape.ArcsOf(piece));
            var toM = Spline.TotalLengthM(line);
            for (var station = 0; station < ground.StationsOfTheSweep(car, 0f, toM); station++)
            {
                var count = UnderTheCarOnThePiece(car, line, shape.IsReverse(piece), 0f, toM, station, 0f, under, out _);
                for (var at = 0; at < count; at++)
                {
                    ref readonly var cover = ref under[at];
                    if (!ground.IsCarriageway(cover.Way) || ground.CrossesAZebra(cover.Way, cover.FromM, cover.ToM)) return false;
                    if (ground.Ways.KindOf(cover.Way) == WayKind.Lane && ground.Roads.IsABayArm(ground.Ways.RoadLaneOf(cover.Way))) continue;

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
        return ground.Atlas.UnderBox(centreM, forward, halfM.X + spareM, halfM.Y + spareM, under);
    }
}
