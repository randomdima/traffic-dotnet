namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The bodies</b> (TER-4c.2): every body with a collider on every way the atlas finds it over, held by its holder as
/// its own ground and laid again by it as it moves (root decision log, 2026-10-14). A record of real things and not of
/// anybody's plan, so never compared, cut or taken — and <b>a body laid on another holder's plan cuts it there</b>, as a
/// plan asked against it would have been refused. <b>Those that overlap are one claim</b> — read as the stretches
/// running into one another (<see cref="OwnBodyOver"/>, <see cref="Bodies"/>), never stored merged.
/// </summary>
internal sealed partial class LaneOccupancy
{
    /// <summary>
    /// Each body's hold, by the body's own key (<see cref="KeyOf"/>), or <see cref="NoHold"/> — its stretches held as
    /// whoever its ground is held as, which for a walker going to its own car is the car and for a car on a bar the truck.
    /// </summary>
    int[] _bodyHoldOf;

    int _bodiesLaid;

    /// <summary>
    /// <b>A body on one way</b> (TER-4c.2), over the stretch its collider covers of it, added to what its holder stands
    /// on: every main claim of another holder under it is cut where it begins. One holder laying two stretches of one
    /// way stands over both.
    /// </summary>
    public void LayBody(int way, float fromM, float toM, int occupant, LaneRoster of) =>
        LayBody(KeyOf(occupant, of), way, fromM, toM, occupant, of);

    /// <param name="body">The body's own key, which its hold is kept by.</param>
    void LayBody(int body, int way, float fromM, float toM, int occupant, LaneRoster of)
    {
        var fromDm = DmOf(fromM);
        var toDm = DmOf(toM);
        var key = KeyOf(occupant, of);
        if (toDm <= fromDm || key == NoKey || body == NoKey) return;

        var hold = BodyHoldOf(body, occupant, of);
        var row = Touch(way);
        var slot = NewSlot(fromDm, toDm, ClaimPriority.Hard, key);
        _slots[slot].Turn = 0;
        _owners[slot] = new SlotOwner
        {
            Hold = hold, HoldNext = NoSlot, Prev = NoSlot, Row = row, Kind = ClaimKind.Body, Line = WishLine.Fixed,
            Planned = ClaimPriority.Hard, Main = NoSlot,
        };
        Thread(slot);
        OnTheHold(hold, slot);
        _bodiesLaid++;
        StandOn(row, fromDm, toDm, key);
    }

    /// <summary>Everything one body stands on, let go of — a body gone from the world, or to be laid again.</summary>
    public void LiftBody(int body, LaneRoster bodyOf)
    {
        var key = KeyOf(body, bodyOf);
        if (key == NoKey || key >= _bodyHoldOf.Length || _bodyHoldOf[key] == NoHold) return;

        Release(_bodyHoldOf[key]);
        _bodyHoldOf[key] = NoHold;
    }

    /// <summary>
    /// <b>One body laid where it now stands</b>, over every way under it (<paramref name="covers"/>) and held as
    /// <paramref name="occupant"/>: nothing done where it stands where it stood, as the same holder, and what it stood on
    /// let go of and laid again where it does not.
    /// </summary>
    public void PlaceBody(int body, LaneRoster bodyOf, int occupant, LaneRoster of, ReadOnlySpan<WayCover> covers)
    {
        var bodyKey = KeyOf(body, bodyOf);
        if (OnTheSameWays(bodyKey, KeyOf(occupant, of), covers))
        {
            MoveOnTheSameWays(bodyKey, covers);
            return;
        }

        LiftBody(body, bodyOf);
        foreach (ref readonly var cover in covers) LayBody(bodyKey, cover.Way, cover.FromM, cover.ToM, occupant, of);
    }

    /// <summary>Whether a body is laid over these ways, a stretch each in this order, held as this holder.</summary>
    bool OnTheSameWays(int body, int key, ReadOnlySpan<WayCover> covers)
    {
        var hold = body == NoKey || body >= _bodyHoldOf.Length ? NoHold : _bodyHoldOf[body];
        if (hold == NoHold || _holdStates[hold].Key != key) return false;

        var at = _holds[hold].FirstClaim;
        foreach (ref readonly var cover in covers)
        {
            if (DmOf(cover.ToM) <= DmOf(cover.FromM)) continue;
            if (at == NoSlot || _touched[_owners[at].Row] != cover.Way) return false;

            at = _owners[at].HoldNext;
        }

        return at == NoSlot;
    }

    /// <summary>
    /// A body on the ways it stood on moved over each to where it now stands, and every main claim of another holder on
    /// the ground it has come to cut there — what it stood on already being clear of them.
    /// </summary>
    void MoveOnTheSameWays(int body, ReadOnlySpan<WayCover> covers)
    {
        var at = _holds[_bodyHoldOf[body]].FirstClaim;
        foreach (ref readonly var cover in covers)
        {
            var fromDm = DmOf(cover.FromM);
            var toDm = DmOf(cover.ToM);
            if (toDm <= fromDm) continue;

            ref var stretch = ref _slots[at];
            var (wasFromDm, wasToDm) = (stretch.FromDm, stretch.ToDm);
            var next = _owners[at].HoldNext;
            if (fromDm == wasFromDm && toDm == wasToDm)
            {
                at = next;
                continue;
            }

            var row = _owners[at].Row;
            _claimsChangedAt[row] = _answers;
            stretch.ToDm = toDm;
            if (fromDm != wasFromDm) MoveTheNearEdge(at, fromDm);

            if (fromDm < wasFromDm) StandOn(row, fromDm, Math.Min(wasFromDm, toDm), _slots[at].Key);
            if (toDm > wasToDm) StandOn(row, Math.Max(wasToDm, fromDm), toDm, _slots[at].Key);

            at = next;
        }
    }

    /// <summary>Every main claim of another holder over <c>[fromDm, toDm)</c> of a row cut where a body has come to stand on it.</summary>
    void StandOn(int row, ushort fromDm, ushort toDm, int key)
    {
        while (LaterIn(_rows[row].Mains, fromDm, toDm, key, 0) is var main && main != NoSlot)
        {
            CutHeld(main, fromDm, toDm, ClaimPriority.Hard, ClaimKind.Body);
        }
    }

    /// <summary>A held claim begun at <paramref name="fromDm"/>, moved in its run only where that passes another's near edge.</summary>
    void MoveTheNearEdge(int slot, ushort fromDm)
    {
        ref var claim = ref _slots[slot];
        var prev = _owners[slot].Prev;
        if (fromDm >= claim.FromDm ? claim.Next == NoSlot || _slots[claim.Next].FromDm >= fromDm
                                   : prev == NoSlot || _slots[prev].FromDm <= fromDm)
        {
            claim.FromDm = fromDm;
            return;
        }

        while (prev != NoSlot && _slots[prev].FromDm > fromDm) prev = _owners[prev].Prev;

        Unthread(slot);
        claim.FromDm = fromDm;
        ThreadAfter(slot, prev);
    }

    /// <summary>A body's hold, begun at the first stretch it lays, as the holder its ground is held as.</summary>
    int BodyHoldOf(int body, int occupant, LaneRoster of)
    {
        if (body >= _bodyHoldOf.Length)
        {
            var was = _bodyHoldOf.Length;
            Array.Resize(ref _bodyHoldOf, Math.Max(body + 1, 2 * was));
            _bodyHoldOf.AsSpan(was).Fill(NoHold);
        }

        ref var hold = ref _bodyHoldOf[body];
        if (hold == NoHold) hold = NewHold(occupant, of, whole: false, byLine: false);

        return hold;
    }

    /// <summary>
    /// The first stretch of a holder's own body — the body whose own key it is — its others following on its hold, or
    /// <see cref="NoSlot"/>.
    /// </summary>
    int OwnStretches(int key)
    {
        var hold = key == NoKey || key >= _bodyHoldOf.Length ? NoHold : _bodyHoldOf[key];
        return hold == NoHold || _holdStates[hold].Key != key ? NoSlot : _holds[hold].FirstClaim;
    }

    /// <summary>How many stretches of bodies are laid, for an instrument.</summary>
    public int BodiesLaid => _bodiesLaid;

    int BodiesOn(int row) => row == NoRow ? NoSlot : _rows[row].Bodies;

    /// <summary>Where one holder's own body stands on a way, as the least and most of every stretch it laid there.</summary>
    bool OwnBody(int way, int key, out ushort fromDm, out ushort toDm)
    {
        fromDm = ushort.MaxValue;
        toDm = 0;
        var row = RowOf(way);
        if (row == NoRow) return false;

        for (var at = OwnStretches(key); at != NoSlot; at = _owners[at].HoldNext)
        {
            if (_owners[at].Row != row) continue;

            fromDm = Math.Min(fromDm, _slots[at].FromDm);
            toDm = Math.Max(toDm, _slots[at].ToDm);
        }

        return toDm > fromDm;
    }

    /// <summary>
    /// <see cref="OwnBodyOver"/> read off the holder's own stretches: <b>one stretch of it on the way stands over the
    /// decimetre or not, whatever runs into it</b>, so only two or more there are read off the way's run.
    /// </summary>
    bool OwnBodyAt(int row, int key, ushort atDm, out ushort fromDm, out ushort toDm)
    {
        (fromDm, toDm) = (ushort.MaxValue, (ushort)0);
        if (row == NoRow) return false;

        var (count, begins, ends) = (0, false, false);
        for (var at = OwnStretches(key); at != NoSlot; at = _owners[at].HoldNext)
        {
            if (_owners[at].Row != row) continue;

            ref readonly var own = ref _slots[at];
            (fromDm, toDm) = (own.FromDm, own.ToDm);
            begins |= own.FromDm <= atDm;
            ends |= own.ToDm > atDm;
            count++;
        }

        if (!begins || !ends) return false;

        return count == 1 || OwnBodyOver(row, key, atDm, out fromDm, out toDm);
    }

    /// <summary>
    /// <b>Where one holder's own body stands over one decimetre of a way</b>, as the least and most of its stretches in
    /// the body claim over it — the bodies running into one another there.
    /// </summary>
    bool OwnBodyOver(int row, int key, ushort atDm, out ushort fromDm, out ushort toDm)
    {
        fromDm = ushort.MaxValue;
        toDm = 0;
        if (key == NoKey) return false;

        var reachDm = (ushort)0;
        var begun = false;
        for (var at = BodiesOn(row); at != NoSlot; at = _slots[at].Next)
        {
            ref readonly var body = ref _slots[at];
            if (!begun || body.FromDm >= reachDm)
            {
                if (begun && reachDm > atDm) break;
                if (body.FromDm > atDm) return false;

                begun = true;
                fromDm = ushort.MaxValue;
                toDm = 0;
            }

            reachDm = Math.Max(reachDm, body.ToDm);
            if (body.Key != key) continue;

            fromDm = Math.Min(fromDm, body.FromDm);
            toDm = Math.Max(toDm, body.ToDm);
        }

        return reachDm > atDm && fromDm <= atDm && toDm > atDm;
    }

    /// <summary>
    /// <b>The first body but the holder's own over <c>[fromDm, toDm)</c></b>, and the first decimetre of it it stands on,
    /// travelling up the way — or down it, <paramref name="against"/>, the last.
    /// </summary>
    bool BodyIn(int row, ushort fromDm, ushort toDm, int key, bool against, out int found, out ushort atDm)
    {
        found = NoSlot;
        atDm = 0;
        for (var at = BodiesOn(row); at != NoSlot; at = _slots[at].Next)
        {
            ref readonly var body = ref _slots[at];
            if (body.FromDm >= toDm) break;
            if (body.Key == key || body.ToDm <= fromDm) continue;

            if (!against)
            {
                found = at;
                atDm = Math.Max(fromDm, body.FromDm);
                return true;
            }

            var reachDm = Math.Min(toDm, body.ToDm);
            if (found != NoSlot && reachDm <= atDm) continue;

            found = at;
            atDm = reachDm;
        }

        return found != NoSlot;
    }
}
