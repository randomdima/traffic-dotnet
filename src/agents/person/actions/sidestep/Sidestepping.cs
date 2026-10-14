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
/// walker: asked for off the grant a body ended, laid as a plan of its own over the ground it will cover, kept or
/// withdrawn once, and walked as a step across onto the lane beside, a walk down it and a step back. Done once it is
/// back on its route past the one it passed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing decides a pass</b> (<see cref="DrawThePass"/>): whether what ended the grant is somebody standing still is
/// what that body is doing, and the claims say only where it is (TER-4c.5).
/// </para>
/// <para>
/// <b>Its ground is its own route's</b> (<see cref="GroundOfThePass"/>), from where it stands to the gap past where it
/// steps back — the lane beside it walks down being ground the world reads its body on as it goes (TER-4c.2) — so a pass
/// runs round a corner and across the joint where a pavement is parted for a zebra as along a stretch.
/// </para>
/// <para>
/// <b>It may run on past the end of the way it began on</b>, which is the whole of getting round somebody
/// waiting at a kerb: they stand at the end of their way, where the pavement is parted for the zebra they are
/// waiting to cross.
/// </para>
/// </remarks>
internal sealed class Sidestepping(WalkingGround ground, PersonActions actions)
{
    /// <summary>The pass each walker decided on, committed to while it waits for it and walks it.</summary>
    readonly Sidestep[] _decided = new Sidestep[ground.People.Capacity];

    /// <summary>The body each decided pass gets past, where it stood.</summary>
    readonly BodySeen[] _passed = new BodySeen[ground.People.Capacity];

    /// <summary>The hold each walker's pass was asked in this rebuild, or <see cref="LaneOccupancy.NoHold"/>.</summary>
    readonly int[] _holds = LaneOccupancy.NoHolds(ground.People.Capacity);

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
    /// <b>The ground this walker's pass will cover, asked as a plan of its own</b> (TER-4c.6): what is left of its
    /// route's ground (<see cref="GroundStillToCover"/>) — at a walker's rung, and at <see cref="ClaimPriority.Committed"/>
    /// once begun.
    /// </summary>
    [SkipLocalsInit]
    public void Lay(int person)
    {
        Occupancy.Release(_holds[person]);
        _holds[person] = LaneOccupancy.NoHold;
        Span<LineWay> left = stackalloc LineWay[WalkingGround.MostWaysAlongAWalk];
        var count = GroundStillToCover(person, left);
        if (count < 0)
        {
            actions.Enter(person, PersonAction.Walk);
            return;
        }

        if (count == 0) return;

        // Begun, the walker stands where it is short of whatever refuses the pass: nought on its walk is its centre.
        var begun = People.Pass[person].Begun;
        var occupant = ground.HeldAs(person, out var roster);
        var hold = _holds[person] = Occupancy.BeginHold(occupant, roster, whole: !begun);
        var rung = begun ? ClaimPriority.Committed : ClaimPriority.Afoot;
        foreach (ref readonly var way in left[..count]) Occupancy.Ask(hold, way.Way, way.FromM, way.ToM, way.LineFromM, rung);
    }

    /// <summary>
    /// The hold of the pass a walker has begun, which its walk carries on from (<see cref="WalkingGround.Claim"/>) — or
    /// <see cref="LaneOccupancy.NoHold"/>.
    /// </summary>
    public int HoldOf(int person) => People.Pass[person].Begun ? _holds[person] : LaneOccupancy.NoHold;

    /// <summary>
    /// <b>What is left of the ground a walker's pass covers</b> (<see cref="Lay"/>): its route's from where it stands to
    /// where it steps back.
    /// </summary>
    /// <param name="into">Room for <see cref="WalkingGround.MostWaysAlongAWalk"/>.</param>
    /// <returns>How many: none for a walker on no pass, and −1 for one no longer on the sidestep it decided.</returns>
    public int GroundStillToCover(int person, Span<LineWay> into)
    {
        var pass = People.Pass[person];
        if (People.Action[person] != PersonAction.Sidestep || !pass.Any) return 0;
        if (!WhereOnItsSidestep(person, pass, out var place)) return -1;

        return GroundOfThePass(person, pass, place.WalkedM, 0f, into);
    }

    /// <summary>
    /// <b>A pass's ground</b> (TER-4c.6): the ways of the walker's route from where it stands to where it steps back
    /// onto it, <paramref name="walkedM"/> down the pass, and <paramref name="roomM"/> past that.
    /// </summary>
    int GroundOfThePass(int person, in Sidestep pass, float walkedM, float roomM, Span<LineWay> into) =>
        ground.WaysAlongTheWalk(person, MathF.Max(0f, pass.ClearM - walkedM) + People.RadiusM[person] + roomM, into);

    /// <summary>
    /// <b>This rebuild's word on a walker's pass</b>, read once its grant is: begun or withdrawn in the rebuild after it
    /// was asked for, and over once the walker is back on its route past the one it passed — in the rebuild it gets
    /// there, since past there is ground the pass never held (TER-4c.8). Everything else about the pass is the walker's
    /// decision (<see cref="Decide"/>).
    /// </summary>
    public void Consider(int person)
    {
        if (People.Action[person] != PersonAction.Sidestep) return;

        var pass = People.Pass[person];
        if (pass.Begun)
        {
            if (!WhereOnItsSidestep(person, pass, out var place) || place.Leg != SidestepLeg.Over) return;

            Made++;
            actions.Enter(person, PersonAction.Walk);
            return;
        }

        if (pass.Any) KeepOrWithdrawTheSidestep(person, pass);
    }

    /// <summary>
    /// <b>A walker's pass, on its own clock</b>: while it waits, looked round every
    /// <see cref="PersonFigures.SidestepAskEveryS"/> — asked for, or let go where the one it passes has gone or it has
    /// waited its patience out.
    /// </summary>
    /// <param name="sinceLastDecisionS">How much of the town's time this decision answers for, which its clocks run by.</param>
    public void Decide(int person, float sinceLastDecisionS)
    {
        if (People.Action[person] != PersonAction.Sidestep || People.Pass[person].Any) return;

        _waitedS[person] += sinceLastDecisionS;
        if (!IsTimeToLook(person, sinceLastDecisionS)) return;

        if (_waitedS[person] > ground.Config.Person.SidestepPatienceS)
        {
            actions.Enter(person, PersonAction.Walk);
            return;
        }

        if (!IsStillThere(person))
        {
            actions.Enter(person, PersonAction.Walk);
            return;
        }

        if (!IsTheSidestepFree(person)) return;

        People.Pass[person] = _decided[person];
        Asked++;
    }

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
    /// <b>Whether the one the walker decided to get past is still there to be got past</b>: a body on the way it was met
    /// on, standing where it stood to within a tick's walk — where the pass was drawn round it.
    /// </summary>
    bool IsStillThere(int person)
    {
        ref readonly var was = ref _passed[person];
        var occupant = ground.HeldAs(person, out var roster);
        var withinM = ground.Config.PersonStepM;
        return Occupancy.FirstBodySeen(was.Way, was.FromM, was.ToM, occupant, roster, out var body)
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
        if (Occupancy.CutOf(_holds[person]).Refused)
        {
            Withdrawn++;
            People.Pass[person] = Sidestep.None;
            return;
        }

        People.Pass[person] = pass with { Begun = true };
    }

    /// <summary>
    /// <b>A walker's pass decided on, once</b> (PER-28): where its grant was ended on its walk by a body on
    /// <paramref name="cutOn"/> at <paramref name="cutAtM"/> it means to get past, walking a way of its route on its
    /// feet, a lane runs back beside the way it is on, and its route runs on far enough past them to step back onto —
    /// drawn and swept then, and never again. False where the pavement refuses it, which is not asked again before the
    /// walker would next look round.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>As short as a walker can make it</b>: straight across from where it stands, along the lane beside until
    /// its back is the gap it keeps past them, and straight back (<see cref="Sidestep"/>).
    /// </para>
    /// <para>
    /// <b>Nothing calls it</b>: which body is one to get past is what the body is doing, which the claims do not say
    /// (TER-4c.5) — <c>docs/tech-debt.md</c>.
    /// </para>
    /// </remarks>
    [SkipLocalsInit]
    public bool DrawThePass(int person, float sinceLastDecisionS, int cutOn, float cutAtM)
    {
        var code = People.CurrentRouteWay(person);
        if (code == PersonFleet.NoWay || WalkingNetwork.IsACorner(code)) return false;
        if (!People.Walking[person] || !People.IsOnItsFeet(person) || ground.IsHopping(person)) return false;

        var occupant = ground.HeldAs(person, out var roster);
        if (!Occupancy.FirstBodySeen(cutOn, cutAtM, Occupancy.WayLengthM(cutOn), occupant, roster, out var cutBy)) return false;

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
        if (!ground.OnTheWalkAt(person, pass.ClearM + RoomPastM(person), out _)) return false;

        _decided[person] = pass;
        _passed[person] = cutBy;
        inS = 0f;
        _looksInS[person] = 0f;
        _waitedS[person] = 0f;
        return true;
    }

    /// <summary>The gap a walker keeps in front of where it steps back onto its route.</summary>
    float RoomPastM(int person) => People.RadiusM[person] + ground.Config.PersonStandstillGapM;

    /// <summary>
    /// <b>Whether the ground a walker's decided pass would cover is free</b> (TER-4c.6): nobody standing on any of it but
    /// the walker and the one it passes, the gap it keeps past where it steps back included, so it never steps back in
    /// front of somebody. Whether a plan holds any of it is answered when the pass is laid.
    /// </summary>
    [SkipLocalsInit]
    bool IsTheSidestepFree(int person)
    {
        var passed = new ReadOnlySpan<BodySeen>(in _passed[person]);
        var occupant = ground.HeldAs(person, out var roster);
        Span<LineWay> ways = stackalloc LineWay[WalkingGround.MostWaysAlongAWalk];
        var count = GroundOfThePass(person, _decided[person], 0f, RoomPastM(person), ways);
        foreach (ref readonly var run in ways[..count])
        {
            var fromM = run.FromM;
            while (Occupancy.FirstBodySeen(run.Way, fromM, run.ToM, occupant, roster, out var body))
            {
                if (!body.IsUnderAny(passed)) return false;

                fromM = body.ToM;
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
}

/// <summary>
/// Where a walker stands on its pass: how far down its route it has come since it asked, how far across its route
/// it stands, and so which leg of the pass it is on.
/// </summary>
internal readonly record struct SidestepPlace(float WalkedM, float AcrossM, SidestepLeg Leg);
