using System.Numerics;
using System.Runtime.CompilerServices;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;
using static TrafficSimulation.World.Road.LineWays;

namespace TrafficSimulation.Agents.Person.Actions;

/// <summary>
/// <b>Sidestep</b> (PER-28, TER-4c.6): a walker getting past somebody standing on its way — the car's pass said of a
/// walker: asked for off the grant a body ended, laid as a body over the ground it will cover, kept or withdrawn once,
/// and walked as a step across onto the lane beside, a walk down it and a step back. Done once it is back on its route
/// past the one it passed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its ground is the walker's own disc swept down its three legs and read off the atlas</b>, as a body's is
/// (TER-4c.2): across from where it stands, along the lane beside a stride at a time, and back — so a pass runs
/// round a corner and across the joint where a pavement is parted for a zebra as along a stretch.
/// </para>
/// <para>
/// <b>It may run on past the end of the way it began on</b>, which is the whole of getting round somebody
/// waiting at a kerb: they stand at the end of their way, where the pavement is parted for the zebra they are
/// waiting to cross.
/// </para>
/// </remarks>
internal sealed class Sidestepping(WalkingGround ground, PersonActions actions)
{
    /// <summary>
    /// How many runs one walker's pass is kept in (<see cref="SweptGround"/>). A bound on the table and not a figure
    /// behaviour reads: a pass is a step across, a few strides and a step back.
    /// </summary>
    const int MostRunsOfAPass = 32;

    /// <summary>The pass each walker decided on, committed to while it waits for it and walks it.</summary>
    readonly Sidestep[] _decided = new Sidestep[ground.People.Capacity];

    /// <summary>The pavement each decided pass's body sweeps — what is laid, kept, and looked for a body in.</summary>
    readonly SweptGround _swept = new(ground.People.Capacity, MostRunsOfAPass);

    /// <summary>And everything it sweeps, with the room past where it steps back — what is asked for.</summary>
    readonly SweptGround _asked = new(ground.People.Capacity, MostRunsOfAPass);

    /// <summary>The body each decided pass gets past, and the way it was met on.</summary>
    readonly LaneClaim[] _passed = new LaneClaim[ground.People.Capacity];

    readonly int[] _passedOn = new int[ground.People.Capacity];

    /// <summary>How long until a walker waiting on its pass looks round again (<see cref="PersonFigures.SidestepAskEveryS"/>).</summary>
    readonly float[] _looksInS = new float[ground.People.Capacity];

    /// <summary>How long a walker has waited on the pass it decided (<see cref="PersonFigures.SidestepPatienceS"/>).</summary>
    readonly float[] _waitedS = new float[ground.People.Capacity];

    /// <summary>How long until a walker refused a pass by the ground decides on one again.</summary>
    readonly float[] _decidesInS = new float[ground.People.Capacity];

    PersonFleet People => ground.People;

    LaneOccupancy Occupancy => ground.Occupancy;

    /// <summary>Walkers' passes asked for since the town was laid (PER-28).</summary>
    public long Asked { get; private set; }

    /// <summary>And withdrawn in the rebuild after, because a body or another pass had the ground by then.</summary>
    public long Withdrawn { get; private set; }

    /// <summary>And walked to the end, the walker back on its own route.</summary>
    public long Made { get; private set; }

    /// <summary>
    /// <b>The ground this walker's pass will cover, laid as a body</b> (TER-4c.6): the pavement it was swept over
    /// (<see cref="IsPavement"/>), given back a stride behind the walker as it goes.
    /// </summary>
    public void Lay(int person)
    {
        var pass = People.Pass[person];
        if (People.Action[person] != PersonAction.Sidestep || !pass.Any) return;
        if (!WhereOnItsSidestep(person, pass, out var place))
        {
            actions.Enter(person, PersonAction.Walk);
            return;
        }

        foreach (ref readonly var run in _swept.Of(person))
        {
            if (run.LastAtM < place.WalkedM - AStrideM(person)) continue;

            Occupancy.LayPass(run.Way, run.FromM, run.ToM, 0f, person, LaneRoster.Walking);
        }
    }

    /// <summary>
    /// <b>This rebuild's word on a walker's pass</b>, read once its grant is: begun or withdrawn in the rebuild after it
    /// was asked for. Everything else about the pass is the walker's decision (<see cref="Decide"/>).
    /// </summary>
    public void Consider(int person)
    {
        var pass = People.Pass[person];
        if (People.Action[person] == PersonAction.Sidestep && pass.Any && !pass.Begun) KeepOrWithdrawTheSidestep(person, pass);
    }

    /// <summary>
    /// <b>A walker's pass, on its own clock</b>: decided where the grant was ended by somebody it may get past; while it
    /// waits, looked round every <see cref="PersonFigures.SidestepAskEveryS"/> — asked for, or let go where the one it
    /// passes has gone or it has waited its patience out; and over once it is back on its route past the one it passed.
    /// </summary>
    /// <param name="sinceLastDecisionS">How much of the town's time this decision answers for, which its clocks run by.</param>
    public void Decide(int person, float sinceLastDecisionS)
    {
        if (People.Action[person] == PersonAction.Walk)
        {
            if (DrawThePass(person, sinceLastDecisionS)) actions.Enter(person, PersonAction.Sidestep);
            return;
        }

        if (People.Action[person] != PersonAction.Sidestep) return;

        var pass = People.Pass[person];
        if (pass.Begun)
        {
            if (!WhereOnItsSidestep(person, pass, out var place) || place.Leg != SidestepLeg.Over) return;

            Made++;
            actions.Enter(person, PersonAction.Walk);
            return;
        }

        if (pass.Any) return;

        _waitedS[person] += sinceLastDecisionS;
        if (!IsTimeToLook(person, sinceLastDecisionS)) return;

        if (_waitedS[person] > ground.Config.Person.SidestepPatienceS || !IsStillThere(person))
        {
            actions.Enter(person, PersonAction.Walk);
            return;
        }

        if (!IsTheSidestepFree(person)) return;

        People.Pass[person] = _decided[person];
        Asked++;
    }

    /// <summary>
    /// <b>Whether somebody stands inside what is left of a walker's pass</b> — the one thing that holds a walker
    /// on a pass where it is, since nothing planned can be laid over the ground it covers (TER-4c.1).
    /// </summary>
    public bool TheBodyInTheSidestep(int person, out LaneClaim body, out int on)
    {
        body = LaneClaim.Nothing;
        on = LaneOccupancy.NoHold;
        var pass = People.Pass[person];
        if (!WhereOnItsSidestep(person, pass, out var place)) return false;

        foreach (ref readonly var run in _swept.Of(person))
        {
            if (run.LastAtM < place.WalkedM - AStrideM(person)
                || !Occupancy.AheadBody(run.Way, run.FromM, run.ToM, person, out body, LaneRoster.Walking))
            {
                continue;
            }

            on = run.Way;
            return true;
        }

        return false;
    }

    /// <summary>A stride of the walker's own: what it has walked past a run by before that run is given back.</summary>
    float AStrideM(int person) => People.RadiusM[person] * 2f;

    /// <summary>
    /// <b>Whether a walker waiting on its pass looks round at this decision</b> — every
    /// <see cref="PersonFigures.SidestepAskEveryS"/> from when it decided.
    /// </summary>
    bool IsTimeToLook(int person, float sinceLastDecisionS)
    {
        ref var inS = ref _looksInS[person];
        inS -= sinceLastDecisionS;
        if (inS > 0f) return false;

        inS = ground.Config.Person.SidestepAskEveryS;
        return true;
    }

    /// <summary>
    /// <b>Whether the one the walker decided to get past is still there to be got past</b>: on the way it was met
    /// on, still somebody it may pass, and standing where it stood to within a tick's walk — where the pass was
    /// drawn round it.
    /// </summary>
    bool IsStillThere(int person)
    {
        ref readonly var was = ref _passed[person];
        var on = _passedOn[person];
        var withinM = ground.Config.PersonStepM;
        return Occupancy.TheBodyOf(on, was.Occupant, was.Of, out var body)
               && body.MayBePassedBy(OnwardAlongTheWalk(person, on))
               && MathF.Abs(body.FromM - was.FromM) <= withinM && MathF.Abs(body.ToM - was.ToM) <= withinM;
    }

    /// <summary>
    /// <b>Where a walker on a pass aims</b> (PER-28): straight across onto the lane beside, a stride down it — never
    /// past where it is clear of the one it passes — and straight back onto its route. False once the pass is
    /// over, when it aims down its route as any walker does.
    /// </summary>
    public bool Aim(int person)
    {
        var pass = People.Pass[person];
        if (!pass.Begun || !WhereOnItsSidestep(person, pass, out var place)) return false;

        switch (place.Leg)
        {
            case SidestepLeg.Across:
                if (!ground.OnTheWalkAt(person, 0f, out var here)) return false;

                People.DestinationM[person] = here.PositionM + (here.Right * pass.AsideM);
                return true;

            case SidestepLeg.Along:
                var strideM = MathF.Min(
                    MathF.Min(ground.Config.PersonWalkAheadM, People.GrantM[person]), pass.ClearM - place.WalkedM);
                if (!ground.OnTheWalkAt(person, MathF.Max(0f, strideM), out var ahead)) return false;

                People.DestinationM[person] = ahead.PositionM + (ahead.Right * pass.AsideM);
                return true;

            case SidestepLeg.Back:
                if (!ground.OnTheWalkAt(person, 0f, out var onItsRoute)) return false;

                People.DestinationM[person] = onItsRoute.PositionM;
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// <b>What is left of a walker's pass, as the places it walks through</b> (PER-28) — where it stands, onto the
    /// lane beside, down it a stride at a time and back onto its route — for whoever draws it.
    /// </summary>
    /// <returns>How many places were written: none for a walker on no pass, and as many as fit.</returns>
    public int PathM(int person, Span<Vector2> into)
    {
        var pass = People.Pass[person];
        if (!pass.Any || into.IsEmpty || !WhereOnItsSidestep(person, pass, out var place)) return 0;

        var written = 0;
        into[written++] = People.PositionM[person];
        var clearM = MathF.Max(0f, pass.ClearM - place.WalkedM);
        if (place.Leg <= SidestepLeg.Along)
        {
            var strideM = People.RadiusM[person] * 2f;
            for (var aheadM = 0f;
                 written < into.Length - 1 && ground.OnTheWalkAt(person, MathF.Min(aheadM, clearM), out var at);
                 aheadM += strideM)
            {
                into[written++] = at.PositionM + (at.Right * pass.AsideM);
                if (aheadM >= clearM) break;
            }
        }

        if (place.Leg <= SidestepLeg.Back && written < into.Length && ground.OnTheWalkAt(person, clearM, out var back))
        {
            into[written++] = back.PositionM;
        }

        return written;
    }

    /// <summary>
    /// <b>How far down its walk a walker's plan begins</b> (PER-26): the front of its body, or past the end of its
    /// pass — the ground up to there being the pass's own (TER-4c.6).
    /// </summary>
    public float WalkPlannedFromM(int person)
    {
        var frontM = People.RadiusM[person];
        var pass = People.Pass[person];
        if (!pass.Begun || !IsStillOnItsSidestep(person, pass, out var walkedM)) return frontM;

        return MathF.Max(frontM, pass.ClearM - walkedM + frontM);
    }

    /// <summary>
    /// <b>A walker's pass laid in this rebuild, kept where it still has its ground to itself</b>, and withdrawn
    /// otherwise — to be asked for again when the walker next looks round.
    /// </summary>
    void KeepOrWithdrawTheSidestep(int person, in Sidestep pass)
    {
        foreach (ref readonly var run in _swept.Of(person))
        {
            if (Occupancy.KeepsItsPass(run.Way, run.FromM, run.ToM, person, LaneRoster.Walking)) continue;

            Withdrawn++;
            People.Pass[person] = Sidestep.None;
            return;
        }

        People.Pass[person] = pass with { Begun = true };
    }

    /// <summary>
    /// <b>A walker's pass decided on, once</b> (PER-28): where its grant was ended on its walk by somebody it may get
    /// past, a lane runs back beside the way it is on, and its route runs on far enough past them to step back onto —
    /// drawn and swept then, and never again. False where the pavement refuses it, which is not asked again before the
    /// walker would next look round.
    /// </summary>
    /// <remarks>
    /// <b>As short as a walker can make it</b>: straight across from where it stands, along the lane beside until
    /// its back is the gap it keeps past them, and straight back (<see cref="Sidestep"/>).
    /// </remarks>
    [SkipLocalsInit]
    bool DrawThePass(int person, float sinceLastDecisionS)
    {
        var hold = ground.WalkHold[person];
        var code = People.CurrentRouteWay(person);
        if (hold == LaneOccupancy.NoHold || code == PersonFleet.NoWay || WalkingNetwork.IsACorner(code)) return false;
        if (!People.Walking[person] || !People.IsOnItsFeet(person) || ground.IsHopping(person)) return false;

        Occupancy.HoldEndsAtM(hold, out _, out var cutBy);
        var cutOn = Occupancy.HoldCutOn(hold);
        if (!cutBy.Found || !cutBy.MayBePassedBy(OnwardAlongTheWalk(person, cutOn))) return false;

        ref var inS = ref _decidesInS[person];
        inS -= sinceLastDecisionS;
        if (inS > 0f) return false;

        // Refused, it is not drawn again before the walker would next look round.
        inS = ground.Config.Person.SidestepAskEveryS;
        var placeM = People.OnWayM[person];
        var asideM = ground.Walking.AsideM(code, placeM);
        if (float.IsNaN(asideM)) return false;

        var radiusM = People.RadiusM[person];
        Span<LineWay> walk = stackalloc LineWay[WalkingGround.MostWaysAlongAWalk];
        var count = ground.WaysAlongTheWalk(person, ground.PlansAheadM + (radiusM * 2f), walk);
        if (!OnTheLine(walk[..count], cutOn, cutBy.ToM, out var farM)) return false;

        var pass = new Sidestep(
            People.RouteAt(person), placeM, farM + ground.Config.PersonStandstillGapM + radiusM, asideM, false);
        if (!Sweep(person, pass)) return false;

        _decided[person] = pass;
        _passed[person] = cutBy;
        _passedOn[person] = cutOn;
        inS = 0f;
        _looksInS[person] = 0f;
        _waitedS[person] = 0f;
        return true;
    }

    /// <summary>
    /// <b>A decided pass's ground, swept once</b> (<see cref="SweptGround"/>) from where the walker stands, each piece
    /// at how far down its route it is walked — the step across at its start, each stride where it ends, the step back
    /// where the walker is clear: the pavement of it for laying and keeping, and all of it with the room past where it
    /// steps back for asking. <b>False where any of it is ground the traffic drives</b> — a pass stepped off the kerb —
    /// or the route does not run that far, or it does not fit.
    /// </summary>
    [SkipLocalsInit]
    bool Sweep(int person, in Sidestep pass)
    {
        _swept.Clear(person);
        _asked.Clear(person);
        var place = new SidestepPlace(0f, 0f, SidestepLeg.Across);
        var pieces = PiecesOfTheSidestep(person, pass, place);
        var strideM = AStrideM(person);
        Span<WayCover> under = stackalloc WayCover[RibbonAtlas.MostWaysUnderABody];
        for (var piece = 0; piece <= pieces; piece++)
        {
            var count = piece < pieces
                ? UnderThePieceOfTheSidestep(person, pass, place, piece, under)
                : UnderTheRoomPastTheSidestep(person, pass, under);
            if (count < 0) return false;

            var atM = piece == 0 ? 0f
                : piece < pieces - 1 ? MathF.Min(pass.ClearM, piece * strideM)
                : pass.ClearM;
            var pavement = 0;
            for (var at = 0; at < count; at++)
            {
                if (ground.Ways.IsDriven(under[at].Way)) return false;
                if (!IsPavement(under[at].Way)) continue;

                (under[pavement], under[at]) = (under[at], under[pavement]);
                pavement++;
            }

            if (!_asked.Station(person, atM, strideM, under[..count])) return false;
            if (piece < pieces && !_swept.Station(person, atM, strideM, under[..pavement])) return false;
        }

        return true;
    }

    /// <summary>
    /// <b>Whether the ground a walker's decided pass would cover is free</b> (TER-4c.6): nobody on any of it but the
    /// walker, and nobody planning it but the one it passes, the paint of a zebra it only skirts aside; and the gap it
    /// keeps past where it steps back free too, so it never steps back in front of somebody.
    /// </summary>
    bool IsTheSidestepFree(int person)
    {
        var passed = new ReadOnlySpan<LaneClaim>(in _passed[person]);
        foreach (ref readonly var run in _asked.Of(person))
        {
            if (!Occupancy.IsFreeForAPass(
                    run.Way, run.FromM, run.ToM, person, LaneRoster.Walking, passed, !ground.Lines.IsTheCrossing(run.Way)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// <b>Where a walker stands on its pass</b>: still walking the route it asked on, how far down it and how far
    /// across it — and so which leg it is on.
    /// </summary>
    bool WhereOnItsSidestep(int person, in Sidestep pass, out SidestepPlace place)
    {
        place = default;
        if (!IsStillOnItsSidestep(person, pass, out var walkedM) || !ground.OnTheWalkAt(person, 0f, out var here))
        {
            return false;
        }

        var acrossM = Vector2.Dot(People.PositionM[person] - here.PositionM, here.Right);
        var leg = pass.LegAt(walkedM, acrossM, ground.Config.PersonStepM, People.RadiusM[person]);
        place = new SidestepPlace(walkedM, acrossM, leg);
        return true;
    }

    /// <summary>
    /// <b>Whether a walker is still walking the route its pass was asked for on</b>, and how far down it it has come
    /// since — on its feet, out of doors and under nobody's hand.
    /// </summary>
    bool IsStillOnItsSidestep(int person, in Sidestep pass, out float walkedM)
    {
        walkedM = 0f;
        if (!ground.HasAWalkToPlan(person) || People.Inside[person].Any || People.RouteAt(person) < pass.Slot) return false;

        walkedM = ground.WalkedSince(person, pass.Slot, pass.FromM);
        return true;
    }

    /// <summary>
    /// <b>Which way a walker's route takes after one of its ways</b> — the movement a body in its way is weighed
    /// against (TER-4c.6) — or <see cref="LaneOccupancy.NoWay"/> where the way is not ahead on it or is its last.
    /// </summary>
    int OnwardAlongTheWalk(int person, int way)
    {
        var route = People.RouteOf(person);
        for (var slot = Math.Max(0, People.RouteAt(person)); slot < People.RouteCount[person]; slot++)
        {
            if (ground.WayOf(route[slot]) != way) continue;

            return slot + 1 < People.RouteCount[person] ? ground.WayOf(route[slot + 1]) : LaneOccupancy.NoWay;
        }

        return LaneOccupancy.NoWay;
    }

    /// <summary>
    /// <b>Whether a walker's pass is laid on a way</b>: the pavement's lanes and its corners, and never the carriageway
    /// or a zebra's paint — held there, somebody crossing towards the kerb would be held in the road.
    /// </summary>
    bool IsPavement(int way) => !ground.Ways.IsDriven(way) && !ground.Lines.IsTheCrossing(way);

    /// <summary>
    /// <b>How many pieces what is left of a walker's pass is swept in</b>: the step across, each stride down the lane
    /// beside — a body's width, so no stride is wider than the walker taking it — and the step back.
    /// </summary>
    int PiecesOfTheSidestep(int person, in Sidestep pass, in SidestepPlace place)
    {
        var alongM = MathF.Max(0f, pass.ClearM - place.WalkedM);
        return 2 + (int)MathF.Ceiling(alongM / (People.RadiusM[person] * 2f));
    }

    /// <summary>
    /// <b>The ways under one piece of what is left of a walker's pass</b>, as wide as the walker: the step across
    /// from where it stands while it is still stepping across, a stride down the lane beside while it has still to
    /// clear what it passes, and the step back from wherever it has got to across — or −1 where the route does not
    /// run that far.
    /// </summary>
    int UnderThePieceOfTheSidestep(int person, in Sidestep pass, in SidestepPlace place, int piece, Span<WayCover> under)
    {
        var radiusM = People.RadiusM[person];
        var clearM = MathF.Max(0f, pass.ClearM - place.WalkedM);
        var last = PiecesOfTheSidestep(person, pass, place) - 1;
        if (piece == 0)
        {
            if (place.Leg != SidestepLeg.Across) return 0;
            if (!ground.OnTheWalkAt(person, 0f, out var here)) return -1;

            return ground.UnderTheStretch(
                here.PositionM + (here.Right * place.AcrossM), here.PositionM + (here.Right * pass.AsideM), radiusM, under);
        }

        if (piece == last)
        {
            if (place.Leg > SidestepLeg.Back) return 0;
            if (!ground.OnTheWalkAt(person, clearM, out var back)) return -1;

            var fromAcrossM = place.Leg == SidestepLeg.Back ? place.AcrossM : pass.AsideM;
            return ground.UnderTheStretch(back.PositionM + (back.Right * fromAcrossM), back.PositionM, radiusM, under);
        }

        if (place.Leg > SidestepLeg.Along) return 0;

        var fromM = (piece - 1) * radiusM * 2f;
        var toM = MathF.Min(clearM, fromM + (radiusM * 2f));
        if (!ground.OnTheWalkAt(person, fromM, out var from) || !ground.OnTheWalkAt(person, toM, out var to)) return -1;

        return ground.UnderTheStretch(
            from.PositionM + (from.Right * pass.AsideM), to.PositionM + (to.Right * pass.AsideM), radiusM, under);
    }

    /// <summary>
    /// The ways under the gap a walker keeps in front of where it steps back onto its route — asked free with the
    /// pass and never laid, since it is the room the walk goes on into.
    /// </summary>
    int UnderTheRoomPastTheSidestep(int person, in Sidestep pass, Span<WayCover> under)
    {
        var roomM = People.RadiusM[person] + ground.Config.PersonStandstillGapM;
        if (!ground.OnTheWalkAt(person, pass.ClearM, out var from) || !ground.OnTheWalkAt(person, pass.ClearM + roomM, out var to))
        {
            return -1;
        }

        return ground.UnderTheStretch(from.PositionM, to.PositionM, People.RadiusM[person], under);
    }
}

/// <summary>
/// Where a walker stands on its pass: how far down its route it has come since it asked, how far across its route
/// it stands, and so which leg of the pass it is on.
/// </summary>
internal readonly record struct SidestepPlace(float WalkedM, float AcrossM, SidestepLeg Leg);
