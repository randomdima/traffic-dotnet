using System.Numerics;
using System.Runtime.CompilerServices;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>A walker getting past somebody standing on its way</b> (PER-28, TER-4c.6): the car's pass said of a
/// walker — asked for off the grant a body ended, laid as a body over the ground it will cover, kept or
/// withdrawn once, and walked as a step across onto the lane beside, a walk down it and a step back.
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
internal sealed partial class TownWorld
{
    /// <summary>Walkers' passes asked for since the town was laid (PER-28).</summary>
    public long SidestepsAsked { get; private set; }

    /// <summary>And withdrawn in the rebuild after, because a body or another pass had the ground by then.</summary>
    public long SidestepsWithdrawn { get; private set; }

    /// <summary>And walked to the end, the walker back on its own route.</summary>
    public long SidestepsMade { get; private set; }

    /// <summary>
    /// <b>The ground this walker's pass will cover, laid as a body</b> (TER-4c.6): swept from where it stands to
    /// where it is back on its route, on the pavement (<see cref="IsPavement"/>).
    /// </summary>
    [SkipLocalsInit]
    void LayTheWalkersPass(int person)
    {
        if (!People.Pass[person].Any) return;
        if (!WhereOnItsSidestep(person, out var place))
        {
            People.Pass[person] = Sidestep.None;
            return;
        }

        var pass = People.Pass[person];
        Span<WayCover> under = stackalloc WayCover[MostWaysUnderABody];
        for (var piece = 0; piece < PiecesOfTheSidestep(person, pass, place); piece++)
        {
            var count = UnderThePieceOfTheSidestep(person, pass, place, piece, under);
            for (var at = 0; at < count; at++)
            {
                if (!IsPavement(under[at].Way)) continue;

                _occupancy.LayPass(under[at].Way, under[at].FromM, under[at].ToM, 0f, person, LaneRoster.Walking);
            }
        }
    }

    /// <summary>
    /// <b>This rebuild's word on a walker's pass</b>, read once its grant is: over once it is back on its route
    /// past the one it passed, begun or withdrawn in the rebuild after it was asked for, and asked for where the
    /// grant was ended by somebody it may get past.
    /// </summary>
    void ConsiderASidestep(int person)
    {
        var pass = People.Pass[person];
        if (pass.Begun)
        {
            if (!WhereOnItsSidestep(person, out var place) || place.Leg != SidestepLeg.Over) return;

            People.Pass[person] = Sidestep.None;
            SidestepsMade++;
            return;
        }

        if (pass.Any)
        {
            KeepOrWithdrawTheSidestep(person, pass);
            return;
        }

        AskForASidestep(person);
    }

    /// <summary>A walker's pass laid in this rebuild, kept where it still has its ground to itself and withdrawn otherwise.</summary>
    [SkipLocalsInit]
    void KeepOrWithdrawTheSidestep(int person, in Sidestep pass)
    {
        if (!WhereOnItsSidestep(person, out var place)) return;

        Span<WayCover> under = stackalloc WayCover[MostWaysUnderABody];
        for (var piece = 0; piece < PiecesOfTheSidestep(person, pass, place); piece++)
        {
            var count = UnderThePieceOfTheSidestep(person, pass, place, piece, under);
            for (var at = 0; at < count; at++)
            {
                if (!IsPavement(under[at].Way)
                    || _occupancy.KeepsItsPass(under[at].Way, under[at].FromM, under[at].ToM, person, LaneRoster.Walking))
                {
                    continue;
                }

                People.Pass[person] = Sidestep.None;
                SidestepsWithdrawn++;
                return;
            }
        }

        People.Pass[person] = pass with { Begun = true };
    }

    /// <summary>
    /// <b>A walker's pass asked for</b> (PER-28): where its grant was ended on its walk by somebody it may get past,
    /// a lane runs back beside the way it is on, its route runs on far enough past them to step back onto, and
    /// nobody has any of that ground.
    /// </summary>
    /// <remarks>
    /// <b>As short as a walker can make it</b>: straight across from where it stands, along the lane beside until
    /// its back is the gap it keeps past them, and straight back (<see cref="Sidestep"/>).
    /// </remarks>
    [SkipLocalsInit]
    void AskForASidestep(int person)
    {
        var hold = _walkerHold[person];
        var code = People.CurrentRouteWay(person);
        if (hold == LaneOccupancy.NoHold || code == PersonFleet.NoWay || WalkingNetwork.IsACorner(code)) return;
        if (!People.Walking[person] || !People.IsOnItsFeet(person) || IsHopping(person)) return;

        _occupancy.HoldEndsAtM(hold, out _, out var cutBy);
        var cutOn = _occupancy.HoldCutOn(hold);
        if (!cutBy.Found || !cutBy.MayBePassedBy(OnwardAlongTheWalk(person, cutOn))) return;

        var placeM = People.OnWayM[person];
        var asideM = Walking.AsideM(code, placeM);
        if (float.IsNaN(asideM)) return;

        var radiusM = People.RadiusM[person];
        Span<LineWay> walk = stackalloc LineWay[MostWaysAlongAWalk];
        var count = WaysAlongTheWalk(person, PlansAheadM + (radiusM * 2f), walk);
        if (!OnTheLine(walk[..count], cutOn, cutBy.ToM, out var farM)) return;

        var pass = new Sidestep(People.RouteAt(person), placeM, farM + _config.PersonStandstillGapM + radiusM, asideM, false);
        Span<LaneClaim> passed = stackalloc LaneClaim[1];
        passed[0] = cutBy;
        if (!IsTheSidestepFree(person, pass, passed)) return;

        People.Pass[person] = pass;
        SidestepsAsked++;
    }

    /// <summary>
    /// <b>Whether the ground a walker's pass would cover is free</b> (TER-4c.6): all of it on the walk — a step over
    /// ground the traffic drives is a pass stepped off the kerb — with nobody on any of it but the walker, and
    /// nobody planning it but whoever it passes, the paint of a zebra it only skirts aside; and the gap it keeps
    /// past where it steps back free too, so it never steps back in front of somebody.
    /// </summary>
    [SkipLocalsInit]
    bool IsTheSidestepFree(int person, in Sidestep pass, ReadOnlySpan<LaneClaim> passed)
    {
        var place = new SidestepPlace(0f, 0f, SidestepLeg.Across);
        var pieces = PiecesOfTheSidestep(person, pass, place);
        Span<WayCover> under = stackalloc WayCover[MostWaysUnderABody];
        for (var piece = 0; piece <= pieces; piece++)
        {
            var count = piece < pieces
                ? UnderThePieceOfTheSidestep(person, pass, place, piece, under)
                : UnderTheRoomPastTheSidestep(person, pass, under);
            if (count < 0) return false;

            for (var at = 0; at < count; at++)
            {
                ref readonly var cover = ref under[at];
                if (_ways.IsDriven(cover.Way)) return false;
                if (!_occupancy.IsFreeForAPass(
                        cover.Way, cover.FromM, cover.ToM, person, LaneRoster.Walking, passed, !IsTheCrossing(cover.Way)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// <b>Whether somebody stands inside what is left of a walker's pass</b> — the one thing that holds a walker
    /// on a pass where it is, since nothing planned can be laid over the ground it covers (TER-4c.1).
    /// </summary>
    [SkipLocalsInit]
    bool TheBodyInTheSidestep(int person, out LaneClaim body, out int on)
    {
        body = LaneClaim.Nothing;
        on = LaneOccupancy.NoHold;
        if (!WhereOnItsSidestep(person, out var place)) return false;

        var pass = People.Pass[person];
        Span<WayCover> under = stackalloc WayCover[MostWaysUnderABody];
        for (var piece = 0; piece < PiecesOfTheSidestep(person, pass, place); piece++)
        {
            var count = UnderThePieceOfTheSidestep(person, pass, place, piece, under);
            for (var at = 0; at < count; at++)
            {
                ref readonly var cover = ref under[at];
                if (!IsPavement(cover.Way)
                    || !_occupancy.AheadBody(cover.Way, cover.FromM, cover.ToM, person, out body, LaneRoster.Walking))
                {
                    continue;
                }

                on = cover.Way;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <b>Where a walker on a pass aims</b> (PER-28): straight across onto the lane beside, a stride down it — never
    /// past where it is clear of the one it passes — and straight back onto its route. False once the pass is
    /// over, when it aims down its route as any walker does.
    /// </summary>
    bool AimTheSidestep(int person)
    {
        var pass = People.Pass[person];
        if (!pass.Begun || !WhereOnItsSidestep(person, out var place)) return false;

        switch (place.Leg)
        {
            case SidestepLeg.Across:
                if (!OnTheWalkAt(person, 0f, out var here)) return false;

                People.DestinationM[person] = here.PositionM + (here.Right * pass.AsideM);
                return true;

            case SidestepLeg.Along:
                var strideM = MathF.Min(
                    MathF.Min(_config.PersonWalkAheadM, People.GrantM[person]), pass.ClearM - place.WalkedM);
                if (!OnTheWalkAt(person, MathF.Max(0f, strideM), out var ahead)) return false;

                People.DestinationM[person] = ahead.PositionM + (ahead.Right * pass.AsideM);
                return true;

            case SidestepLeg.Back:
                if (!OnTheWalkAt(person, 0f, out var onItsRoute)) return false;

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
    public int SidestepPathM(int person, Span<Vector2> into)
    {
        var pass = People.Pass[person];
        if (!pass.Any || into.IsEmpty || !WhereOnItsSidestep(person, out var place)) return 0;

        var written = 0;
        into[written++] = People.PositionM[person];
        var clearM = MathF.Max(0f, pass.ClearM - place.WalkedM);
        if (place.Leg <= SidestepLeg.Along)
        {
            var strideM = People.RadiusM[person] * 2f;
            for (var aheadM = 0f; written < into.Length - 1 && OnTheWalkAt(person, MathF.Min(aheadM, clearM), out var at);
                 aheadM += strideM)
            {
                into[written++] = at.PositionM + (at.Right * pass.AsideM);
                if (aheadM >= clearM) break;
            }
        }

        if (place.Leg <= SidestepLeg.Back && written < into.Length && OnTheWalkAt(person, clearM, out var back))
        {
            into[written++] = back.PositionM;
        }

        return written;
    }

    /// <summary>
    /// <b>How far down its walk a walker's plan begins</b> (PER-26): the front of its body, or past the end of its
    /// pass — the ground up to there being the pass's own (TER-4c.6).
    /// </summary>
    float WalkPlannedFromM(int person)
    {
        var frontM = People.RadiusM[person];
        var pass = People.Pass[person];
        if (!pass.Begun || !IsStillOnItsSidestep(person, out var walkedM)) return frontM;

        return MathF.Max(frontM, pass.ClearM - walkedM + frontM);
    }

    /// <summary>
    /// <b>Where a walker stands on its pass</b>: still walking the route it asked on, how far down it and how far
    /// across it — and so which leg it is on.
    /// </summary>
    bool WhereOnItsSidestep(int person, out SidestepPlace place)
    {
        place = default;
        if (!IsStillOnItsSidestep(person, out var walkedM) || !OnTheWalkAt(person, 0f, out var here)) return false;

        var acrossM = Vector2.Dot(People.PositionM[person] - here.PositionM, here.Right);
        var leg = People.Pass[person].LegAt(walkedM, acrossM, _config.PersonStepM, People.RadiusM[person]);
        place = new SidestepPlace(walkedM, acrossM, leg);
        return true;
    }

    /// <summary>
    /// <b>Whether a walker is still walking the route its pass was asked for on</b>, and how far down it it has come
    /// since — on its feet, out of doors and under nobody's hand.
    /// </summary>
    bool IsStillOnItsSidestep(int person, out float walkedM)
    {
        walkedM = 0f;
        var pass = People.Pass[person];
        if (!HasAWalkToPlan(person) || People.Inside[person].Any || People.RouteAt(person) < pass.Slot) return false;

        walkedM = WalkedSince(person, pass.Slot, pass.FromM);
        return true;
    }

    /// <summary>
    /// <b>How far down its route a walker has come from a place on it</b> — every way between taken over the stretch
    /// of it the route covers, which is the walk's own measure (<see cref="WaysAlongTheWalk"/>).
    /// </summary>
    float WalkedSince(int person, int slot, float fromM)
    {
        var at = People.RouteAt(person);
        if (at == slot) return People.OnWayM[person] - fromM;

        var walking = Walking;
        var route = People.RouteOf(person);
        var count = People.RouteCount[person];
        var walkedM = -fromM;
        for (var way = slot; way < at; way++)
        {
            walking.SpanOfWay(
                way > 0 ? route[way - 1] : WalkingNetwork.NoLane, route[way],
                way + 1 < count ? route[way + 1] : WalkingNetwork.NoLane, out var spanFromM, out var spanToM);
            walkedM += spanToM - (way == slot ? 0f : spanFromM);
        }

        walking.SpanOfWay(
            route[at - 1], route[at], at + 1 < count ? route[at + 1] : WalkingNetwork.NoLane, out var onFromM, out _);
        return walkedM + People.OnWayM[person] - onFromM;
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
            if (WayOf(route[slot]) != way) continue;

            return slot + 1 < People.RouteCount[person] ? WayOf(route[slot + 1]) : LaneOccupancy.NoWay;
        }

        return LaneOccupancy.NoWay;
    }

    /// <summary>
    /// <b>Whether a walker's pass is laid on a way</b>: the pavement's lanes and its corners, and never the carriageway
    /// or a zebra's paint — held there, somebody crossing towards the kerb would be held in the road.
    /// </summary>
    bool IsPavement(int way) => !_ways.IsDriven(way) && !IsTheCrossing(way);

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
            if (!OnTheWalkAt(person, 0f, out var here)) return -1;

            return UnderTheStretch(
                here.PositionM + (here.Right * place.AcrossM), here.PositionM + (here.Right * pass.AsideM), radiusM, under);
        }

        if (piece == last)
        {
            if (place.Leg > SidestepLeg.Back) return 0;
            if (!OnTheWalkAt(person, clearM, out var back)) return -1;

            var fromAcrossM = place.Leg == SidestepLeg.Back ? place.AcrossM : pass.AsideM;
            return UnderTheStretch(back.PositionM + (back.Right * fromAcrossM), back.PositionM, radiusM, under);
        }

        if (place.Leg > SidestepLeg.Along) return 0;

        var fromM = (piece - 1) * radiusM * 2f;
        var toM = MathF.Min(clearM, fromM + (radiusM * 2f));
        if (!OnTheWalkAt(person, fromM, out var from) || !OnTheWalkAt(person, toM, out var to)) return -1;

        return UnderTheStretch(
            from.PositionM + (from.Right * pass.AsideM), to.PositionM + (to.Right * pass.AsideM), radiusM, under);
    }

    /// <summary>
    /// The ways under the gap a walker keeps in front of where it steps back onto its route — asked free with the
    /// pass and never laid, since it is the room the walk goes on into.
    /// </summary>
    int UnderTheRoomPastTheSidestep(int person, in Sidestep pass, Span<WayCover> under)
    {
        var roomM = People.RadiusM[person] + _config.PersonStandstillGapM;
        if (!OnTheWalkAt(person, pass.ClearM, out var from) || !OnTheWalkAt(person, pass.ClearM + roomM, out var to)) return -1;

        return UnderTheStretch(from.PositionM, to.PositionM, People.RadiusM[person], under);
    }

    /// <summary>
    /// <b>Where the walk is a distance down it</b> from the body's own place, over the pavement's lanes and the
    /// mitres at its corners alike. False where the route does not reach that far.
    /// </summary>
    [SkipLocalsInit]
    bool OnTheWalkAt(int person, float aheadM, out SplineSample at)
    {
        at = default;
        Span<LineWay> pieces = stackalloc LineWay[MostWaysAlongAWalk];

        // A tick's walk past it, so the place the body stands on is on a piece of the walk too.
        var count = WaysAlongTheWalk(person, aheadM + _config.PersonStepM, pieces);
        for (var index = 0; index < count; index++)
        {
            ref readonly var piece = ref pieces[index];
            if (aheadM > piece.LineFromM + (piece.ToM - piece.FromM)) continue;

            at = Spline.SampleAt(LineOfWay(piece.Way, out _), piece.FromM + MathF.Max(0f, aheadM - piece.LineFromM));
            return true;
        }

        return false;
    }
}

/// <summary>
/// Where a walker stands on its pass: how far down its route it has come since it asked, how far across its route
/// it stands, and so which leg of the pass it is on.
/// </summary>
internal readonly record struct SidestepPlace(float WalkedM, float AcrossM, SidestepLeg Leg);
