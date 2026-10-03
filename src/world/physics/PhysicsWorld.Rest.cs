namespace TrafficSimulation.World.Physics;

/// <summary>
/// <b>Freezing</b> (SOL-37): a body at rest is left out of the step until an impulse reaches it or a body the step
/// is moving meets it, and its motion is exactly zero for as long as it is out.
/// </summary>
/// <remarks>
/// <para>
/// <b>What a frozen body saves is the broad phase, not the solve.</b> A town's contacts are few; what it pays for
/// is every standing body asking the grids, every tick, what stands round it — and a parked car is surrounded by
/// buildings and trees that have not moved either.
/// </para>
/// <para>
/// <b>Rest is read off the motion and never off the impulses.</b> A parked car's tyres spend a correction every
/// tick against what the last one left over, and a count reset by impulses would never let it freeze; once it is
/// frozen its motion is exactly zero, its tyres have nothing to do, and no impulse is spent to wake it. What a
/// count read off the motion costs is a body that an actor pushes by less than the rest speed a tick, every tick
/// from standing, for the whole of the rest time — and then loses that push each tick for as long as it stays
/// under the figure.
/// </para>
/// </remarks>
internal sealed partial class PhysicsWorld
{
    readonly CellGrid _frozenGrid = new();

    /// <summary>The bodies the frozen index was last laid over: each one still in it while it is <see cref="BodyFlags.FiledFrozen"/>.</summary>
    int[] _filedFrozen = new int[Room];
    int _filedFrozenCount;

    /// <summary>
    /// Bodies frozen since the frozen index was laid, which the moving index files meanwhile — each once
    /// (<see cref="BodyFlags.Unfiled"/>), and kept here until the frozen index takes them in or they wake.
    /// </summary>
    int[] _unfiled = new int[Room];
    int _unfiledCount;

    /// <summary>
    /// Bodies to be taken in among the awake at the next step — woken, released, added, or woken by the last step
    /// and still moving — each once (<see cref="BodyFlags.Joining"/>).
    /// </summary>
    int[] _joining = new int[Room];
    int _joiningCount;

    /// <summary>Where the awake roster is merged with what joins it.</summary>
    int[] _merged = new int[Room];

    /// <summary>Whether an awake body has gone into a container since the last step, and the roster still names it.</summary>
    bool _awakeLeft;

    /// <summary>
    /// How many bodies the moving index files on the frozen index's behalf before that is laid again: never fewer
    /// than this, and otherwise an eighth of what the frozen index holds — so a body is filed in the frozen index
    /// at most a few times for every one it holds, and laying it again is paid for by every freeze since.
    /// </summary>
    /// <remarks>Which index a frozen body is filed in decides what a step costs and nothing it does.</remarks>
    const int LeastUnfiled = 256;

    const int UnfiledShare = 8;

    /// <summary>A frozen body taken back into the steps from the next one on: an impulse, a new layer, a container.</summary>
    void Wake(int body)
    {
        if (!Thaw(body)) return;

        Join(body);
    }

    /// <summary>
    /// A frozen body a moving one has met, <b>woken into the step that met it</b> (SOL-37a): an owner from here,
    /// and solved, damped and integrated with everything else this step moves.
    /// </summary>
    /// <remarks>
    /// Its rest count is kept: a body met by something passing it at a hand's width is not moved by that, and
    /// is frozen again at the end of this step if nothing it touches is still moving (<see cref="FreezeWhatHasSettled"/>).
    /// </remarks>
    void WakeInStep(int body)
    {
        Thaw(body);
        _overlapM[body] = 0f;
        _moving[_movingCount++] = body;
    }

    bool Thaw(int body)
    {
        if ((_flags[body] & BodyFlags.Frozen) == 0) return false;

        _flags[body] &= ~BodyFlags.Frozen;
        FrozenBodyCount--;
        return true;
    }

    void Join(int body)
    {
        if ((_flags[body] & BodyFlags.Joining) != 0) return;

        _flags[body] |= BodyFlags.Joining;
        _joining[_joiningCount++] = body;
    }

    /// <summary>
    /// <b>The awake roster as the next step finds it</b>: whatever went into a container dropped, and whatever joined
    /// since merged in, in index order — the order every step's owners are walked in. Nothing frozen is looked at.
    /// </summary>
    void TakeInTheJoining()
    {
        if (_awakeLeft)
        {
            _movingCount = KeepOnly(_moving.AsSpan(0, _movingCount), BodyFlags.Enabled | BodyFlags.Frozen, BodyFlags.Enabled);
            _awakeLeft = false;
        }

        if (_joiningCount == 0) return;

        var joining = _joining.AsSpan(0, _joiningCount);
        foreach (var body in joining) _flags[body] &= ~BodyFlags.Joining;

        joining = joining[..KeepOnly(joining, BodyFlags.Enabled | BodyFlags.Frozen, BodyFlags.Enabled)];
        joining.Sort();
        _joiningCount = 0;

        // A body can be named by both — contained and released again before the step — and is taken once.
        var count = 0;
        var awake = 0;
        var join = 0;
        while (awake < _movingCount || join < joining.Length)
        {
            int next;
            if (join == joining.Length || (awake < _movingCount && _moving[awake] < joining[join])) next = _moving[awake++];
            else if (awake == _movingCount || joining[join] < _moving[awake]) next = joining[join++];
            else
            {
                next = _moving[awake++];
                join++;
            }

            if (count > 0 && _merged[count - 1] == next) continue;

            _merged[count++] = next;
        }

        (_moving, _merged) = (_merged, _moving);
        _movingCount = count;
    }

    /// <summary>
    /// <b>The frozen index laid again</b> once the bodies waiting for it outnumber what it is worth to wait: over
    /// everything frozen and standing where it was filed, and everything frozen since. A body woken and not yet
    /// moved is let go of here, and the moving index files it from now on.
    /// </summary>
    void LayTheFrozenIndexWhenDue()
    {
        _unfiledCount = KeepTheUnfiledInPlace();
        if (_unfiledCount <= Math.Max(LeastUnfiled, _filedFrozenCount / UnfiledShare)) return;

        var kept = 0;
        for (var slot = 0; slot < _filedFrozenCount; slot++)
        {
            var body = _filedFrozen[slot];
            if ((_flags[body] & (BodyFlags.Frozen | BodyFlags.FiledFrozen)) == (BodyFlags.Frozen | BodyFlags.FiledFrozen))
            {
                _filedFrozen[kept++] = body;
            }
            else
            {
                _flags[body] &= ~BodyFlags.FiledFrozen;
            }
        }

        for (var slot = 0; slot < _unfiledCount; slot++)
        {
            var body = _unfiled[slot];
            _flags[body] = (_flags[body] & ~BodyFlags.Unfiled) | BodyFlags.FiledFrozen;
            _filedFrozen[kept++] = body;
        }

        _filedFrozenCount = kept;
        _unfiledCount = 0;
        _frozenGrid.Rebuild(_filedFrozen.AsSpan(0, _filedFrozenCount), _leastM, _mostM, _config.Grid.Main);
    }

    /// <summary>The bodies waiting for the frozen index that still are: in the world, frozen and not filed there.</summary>
    int KeepTheUnfiledInPlace()
    {
        var kept = 0;
        for (var slot = 0; slot < _unfiledCount; slot++)
        {
            var body = _unfiled[slot];
            if ((_flags[body] & (BodyFlags.Enabled | BodyFlags.Frozen | BodyFlags.FiledFrozen)) == (BodyFlags.Enabled | BodyFlags.Frozen))
            {
                _unfiled[kept++] = body;
            }
            else
            {
                _flags[body] &= ~BodyFlags.Unfiled;
            }
        }

        return kept;
    }

    /// <summary>The bodies waiting for the frozen index copied out for the moving index to file, and how many there are.</summary>
    int KeepTheUnfiled(Span<int> into)
    {
        _unfiledCount = KeepTheUnfiledInPlace();
        _unfiled.AsSpan(0, _unfiledCount).CopyTo(into);
        return _unfiledCount;
    }

    /// <summary>A roster kept to the bodies whose <paramref name="mask"/> bits read <paramref name="want"/>, in its own order.</summary>
    int KeepOnly(Span<int> bodies, BodyFlags mask, BodyFlags want)
    {
        var kept = 0;
        foreach (var body in bodies)
        {
            if ((_flags[body] & mask) == want) bodies[kept++] = body;
        }

        return kept;
    }

    void ResizeRosters(int room)
    {
        Array.Resize(ref _moving, room);
        Array.Resize(ref _merged, room);
        Array.Resize(ref _filed, room);
        Array.Resize(ref _filedFrozen, room);
        Array.Resize(ref _unfiled, room);
        Array.Resize(ref _joining, room);
    }

    /// <summary>
    /// <b>Every body this step moved that has stood at rest for the rest time freezes</b> — unless it touches one
    /// that has not, which keeps it awake, and so on along whatever is leaning on what (SOL-37b).
    /// </summary>
    /// <remarks>
    /// <para>
    /// At rest is three things: the centre slower than the rest speed, the furthest point of the body swung no
    /// faster, and no overlap left for the push to take out — a body being pushed out of another is moving on an
    /// accumulator the velocities never see.
    /// </para>
    /// <para>
    /// <b>A pair settles together or not at all.</b> A body pushed slowly by an actor's car is at rest by its own
    /// motion and must not be frozen out from under the push: that would be a body the push can only ever move by
    /// one tick's worth. The contacts are few and a row of bodies leaning on each other is short, so the
    /// unsettling is passed along them until nothing changes.
    /// </para>
    /// </remarks>
    void FreezeWhatHasSettled()
    {
        var restMps = _config.Solver.RestSpeedMps;
        var restSquared = restMps * restMps;
        var restTicks = _config.SolverRestTicks;
        var allowedM = _config.Solver.AllowedPenetrationM;

        for (var slot = 0; slot < _movingCount; slot++)
        {
            var body = _moving[slot];
            var swingMps = _yawRateRadPerS[body] * _reachM[body];
            var atRest = _velocityMps[body].LengthSquared() <= restSquared && swingMps * swingMps <= restSquared
                         && _overlapM[body] <= allowedM;

            _restTicks[body] = atRest ? Math.Min(_restTicks[body] + 1, restTicks) : 0;
            _settling[body] = _restTicks[body] >= restTicks;
        }

        bool unsettled;
        do
        {
            unsettled = false;
            for (var contact = 0; contact < _contactCount; contact++)
            {
                var first = _contactA[contact];
                var second = _contactB[contact];
                if ((_flags[second] & BodyFlags.Static) != 0 || _settling[first] == _settling[second]) continue;

                _settling[first] = _settling[second] = false;
                unsettled = true;
            }
        }
        while (unsettled);

        for (var slot = 0; slot < _movingCount; slot++)
        {
            var body = _moving[slot];
            if (!_settling[body]) continue;

            _velocityMps[body] = default;
            _yawRateRadPerS[body] = 0f;
            _flags[body] |= BodyFlags.Frozen;
            _frozeAtStep[body] = _stepStamp;
            FrozenBodyCount++;

            // Standing where the frozen index filed it, it is already there; anywhere else it waits for it.
            if ((_flags[body] & (BodyFlags.FiledFrozen | BodyFlags.Unfiled)) == 0)
            {
                _flags[body] |= BodyFlags.Unfiled;
                _unfiled[_unfiledCount++] = body;
            }
        }

        LeaveTheFrozenBehind();
    }

    /// <summary>
    /// <b>The awake roster without what froze</b>: the owners the step began with keep their order, and the ones it
    /// woke and that are still moving join the next step's in theirs.
    /// </summary>
    void LeaveTheFrozenBehind()
    {
        var owners = _moving.AsSpan(0, _ownersAtStart);
        var kept = KeepOnly(owners, BodyFlags.Frozen, BodyFlags.None);
        for (var slot = _ownersAtStart; slot < _movingCount; slot++)
        {
            var body = _moving[slot];
            if ((_flags[body] & BodyFlags.Frozen) == 0) Join(body);
        }

        _movingCount = kept;
    }
}
