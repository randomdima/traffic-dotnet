namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The bodies</b> (TER-4c.2): every body with a collider on every way the atlas finds it over, laid before any plan,
/// each threaded into its way's run by near edge as it is laid. A record of real things and not of anybody's plan, so
/// never compared, cut or taken. <b>Those that overlap are one claim</b> — read as the stretches running into one another
/// (<see cref="OwnBodyOver"/>, <see cref="Bodies"/>), never stored merged.
/// </summary>
internal sealed partial class LaneOccupancy
{
    int _laidCount;

    bool _bodiesClosed;

    /// <summary>One stretch of a holder's own body, linked to the holder's next laid this rebuild.</summary>
    record struct OwnStretch(int Row, ushort FromDm, ushort ToDm, int Next);

    /// <summary>
    /// <b>Every driver's body stretch by its holder</b>, in the order laid — so a driver's own are read off it and not off
    /// a way's run, which is long where a queue stands.
    /// </summary>
    OwnStretch[] _own;

    int _ownCount;

    /// <summary>A holder's last stretch laid, where <see cref="Laying"/> is this rebuild's.</summary>
    record struct OwnHead(int Laying, int Last);

    /// <summary>One a driver, by its index in its roster (<see cref="OccupantOf"/>).</summary>
    OwnHead[] _ownHeads;

    /// <summary>Which rebuild this is, so a holder's head need not be cleared.</summary>
    int _laying = 1;

    /// <summary>
    /// <b>A body on one way</b> (TER-4c.2), over the stretch its collider covers of it. One holder laying two stretches of
    /// one way stands over both.
    /// </summary>
    public void LayBody(int way, float fromM, float toM, int occupant, LaneRoster of)
    {
        if (_bodiesClosed) throw new InvalidOperationException("a body laid after the plans began to be asked");

        var fromDm = DmOf(fromM);
        var toDm = DmOf(toM);
        if (toDm <= fromDm) return;

        var row = Touch(way);
        var key = KeyOf(occupant, of);
        InsertTheBody(ref _rows[row].Bodies, fromDm, toDm, key);
        if (key != NoKey && of == LaneRoster.Driving)
        {
            if (occupant >= _ownHeads.Length) Array.Resize(ref _ownHeads, Math.Max(occupant + 1, 2 * _ownHeads.Length));
            if (_ownCount == _own.Length) Array.Resize(ref _own, 2 * _own.Length);

            ref var head = ref _ownHeads[occupant];
            _own[_ownCount] = new OwnStretch(row, fromDm, toDm, head.Laying == _laying ? head.Last : NoSlot);
            head = new OwnHead(_laying, _ownCount++);
        }

        _laidCount++;
    }

    /// <summary>No body is laid once the first question is asked of them, which every plan is.</summary>
    void CloseTheBodies() => _bodiesClosed = true;

    /// <summary>How many stretches of bodies this rebuild laid, before any were merged, for an instrument.</summary>
    public int BodiesLaid => _laidCount;

    int BodiesOn(int row)
    {
        CloseTheBodies();
        return row == NoRow ? NoSlot : _rows[row].Bodies;
    }

    /// <summary>Where one holder's own body stands on a way, as the least and most of every stretch it laid there.</summary>
    bool OwnBody(int way, int key, out ushort fromDm, out ushort toDm)
    {
        fromDm = ushort.MaxValue;
        toDm = 0;
        if (key == NoKey) return false;

        var row = RowOf(way);
        if (RosterOf(key) != LaneRoster.Driving)
        {
            for (var at = BodiesOn(row); at != NoSlot; at = _bodies[at].Next)
            {
                ref readonly var body = ref _bodies[at];
                if (body.Key != key) continue;

                fromDm = Math.Min(fromDm, body.FromDm);
                toDm = Math.Max(toDm, body.ToDm);
            }

            return toDm > fromDm;
        }

        for (var at = OwnStretches(key); at != NoSlot; at = _own[at].Next)
        {
            ref readonly var own = ref _own[at];
            if (own.Row != row) continue;

            fromDm = Math.Min(fromDm, own.FromDm);
            toDm = Math.Max(toDm, own.ToDm);
        }

        return toDm > fromDm;
    }

    /// <summary>A driver's last stretch laid this rebuild, its others linked from it.</summary>
    int OwnStretches(int key)
    {
        CloseTheBodies();
        var driver = OccupantOf(key);
        return driver >= _ownHeads.Length || _ownHeads[driver].Laying != _laying ? NoSlot : _ownHeads[driver].Last;
    }

    /// <summary>
    /// <see cref="OwnBodyOver"/> read off the holder's own stretches: <b>one stretch of it on the way stands over the
    /// decimetre or not, whatever runs into it</b>, so only two or more there are read off the way's run.
    /// </summary>
    bool OwnBodyAt(int row, int key, ushort atDm, out ushort fromDm, out ushort toDm)
    {
        if (key == NoKey || RosterOf(key) != LaneRoster.Driving) return OwnBodyOver(row, key, atDm, out fromDm, out toDm);

        (fromDm, toDm) = (ushort.MaxValue, (ushort)0);
        var (count, begins, ends) = (0, false, false);
        for (var at = OwnStretches(key); at != NoSlot; at = _own[at].Next)
        {
            ref readonly var own = ref _own[at];
            if (own.Row != row) continue;

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
        for (var at = BodiesOn(row); at != NoSlot; at = _bodies[at].Next)
        {
            ref readonly var body = ref _bodies[at];
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
        for (var at = BodiesOn(row); at != NoSlot; at = _bodies[at].Next)
        {
            ref readonly var body = ref _bodies[at];
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
