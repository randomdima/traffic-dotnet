using System.Numerics;

namespace TrafficSimulation.World.Physics;

/// <summary>Which pairs are worth testing: the three cell grids, what is moving, and the narrow test each surviving pair is given.</summary>
internal sealed partial class PhysicsWorld
{
    /// <summary>
    /// <b>The moving index laid over what is not in the frozen one</b>: every awake body but one woken and not yet
    /// moved, which the frozen index still has where it stands, and every body frozen since the frozen index was
    /// last laid. Nothing frozen and filed there is walked.
    /// </summary>
    /// <remarks>
    /// No bound is retaken here: a body's box is written wherever its pose is (<see cref="Place"/> and the
    /// integration), and nothing moves a body between steps.
    /// </remarks>
    void IndexMoving()
    {
        _filedCount = 0;
        for (var slot = 0; slot < _movingCount; slot++)
        {
            var body = _moving[slot];
            if ((_flags[body] & BodyFlags.FiledFrozen) == 0) _filed[_filedCount++] = body;
        }

        _filedCount += KeepTheUnfiled(_filed.AsSpan(_filedCount));
        _dynamicGrid.Rebuild(_filed.AsSpan(0, _filedCount), _leastM, _mostM, _config.Grid.Main);
        _movingIndexStale = false;
    }

    /// <summary>
    /// The moving index brought up to date for a query asked outside a step — after a body has been
    /// contained, released or added since the last one.
    /// </summary>
    void EnsureIndex()
    {
        if (_staticIndexStale) SettleStatics();
        if (!_movingIndexStale) return;

        TakeInTheJoining();
        LayTheFrozenIndexWhenDue();
        IndexMoving();
        IntegratedBodyCount = _movingCount;
    }

    /// <summary>
    /// <b>The three grids the broad phase asks</b> — the town's furniture, the bodies frozen in place, and the
    /// rest of the roster at the poses the last step began from — for the layer that draws them (OBS-2x) and for
    /// nothing that decides anything.
    /// </summary>
    /// <remarks>
    /// <b>As they stand, and never brought up to date first.</b> A query outside a step reindexes the moving
    /// set before answering (<see cref="EnsureIndex"/>) and a picture may not: that call rewrites
    /// <see cref="IntegratedBodyCount"/>, which the panel beside the picture is reading, and the lattice a step
    /// was actually priced on is the one it began from — a tick behind the bodies drawn over it. <b>An entry of the
    /// frozen index is only the truth while its body is</b> <see cref="BodyFlags.FiledFrozen"/>: one woken and
    /// moved since is left standing there until the index is laid again.
    /// </remarks>
    public CellGrid StaticIndex => _staticGrid;

    public CellGrid MovingIndex => _dynamicGrid;

    public CellGrid FrozenIndex => _frozenGrid;

    /// <summary>
    /// <b>Every enabled body of one roster whose bounds meet a box</b>, by its index on that roster, read off the moving
    /// and the frozen index as they stand — what a picture of the box draws, and nothing that decides anything.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The bounds are this moment's and the filing a tick old</b>, so the box is widened by a cell before the grids
    /// are asked: no body crosses one in a tick. A frozen entry is taken whether or not its body is still
    /// <see cref="BodyFlags.FiledFrozen"/>, because one woken in the last step is in no moving index yet. A body made
    /// or let out of a container since the last step is in neither, and is answered from the next.
    /// </para>
    /// <para>
    /// <b>A body may be written more than once</b> — once a cell its filing touches, in both grids — and in no
    /// particular order; the caller sorts and drops the repeats, which a picture wants done anyway. Keeping it in the
    /// first cell it shares (<see cref="CellGrid.FirstShared"/>) would judge that cell by bounds a tick newer than the
    /// filing, and a car whose corner had just crossed into a cell it is not filed in yet would be kept in none.
    /// </para>
    /// </remarks>
    /// <returns>
    /// How many were written, or −1 where the box spans more than <paramref name="mostCells"/> cells of either grid —
    /// which is cheaper walked as the roster — or where <paramref name="into"/> filled.
    /// </returns>
    public int InBox(Vector2 leastM, Vector2 mostM, BodyKind kind, int mostCells, Span<int> into)
    {
        var written = InBox(_dynamicGrid, leastM, mostM, kind, mostCells, into);
        if (written < 0) return -1;

        var frozen = InBox(_frozenGrid, leastM, mostM, kind, mostCells, into[written..]);
        return frozen < 0 ? -1 : written + frozen;
    }

    int InBox(CellGrid grid, Vector2 leastM, Vector2 mostM, BodyKind kind, int mostCells, Span<int> into)
    {
        var cellM = new Vector2(_config.Grid.Main.CellM);
        if (!grid.TryRange(leastM - cellM, mostM + cellM, out var range)) return 0;
        if ((long)(range.ToX - range.FromX + 1) * (range.ToY - range.FromY + 1) > mostCells) return -1;

        var written = 0;
        for (var y = range.FromY; y <= range.ToY; y++)
        {
            for (var x = range.FromX; x <= range.ToX; x++)
            {
                foreach (var body in grid.Items(x, y))
                {
                    if ((_flags[body] & BodyFlags.Enabled) == 0 || Apart(body, leastM, mostM)) continue;

                    var tag = BodyTag.Unpack(_tag[body]);
                    if (tag.Kind != kind) continue;
                    if (written == into.Length) return -1;

                    into[written++] = tag.Index;
                }
            }
        }

        return written;
    }

    /// <summary>
    /// Every pair worth a manifold, and the manifold. The owners are the outer loop and neither the static
    /// population nor the frozen one is ever walked: a body asks the two grids what is near it, and ninety-five
    /// thousand props and every parked car are in the answer without being in the price.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The roster of owners grows while it is walked</b>: a frozen body met with a manifold is woken onto the
    /// end of it (SOL-37a), and is an owner of its own pairs in the same step — so whatever it is about to be
    /// pushed into is in the solve that pushes it, and so on down a row of parked cars.
    /// </para>
    /// <para>
    /// <b>A pair is gathered once, by whichever of its two owns first</b>, and is always given to the narrow phase
    /// lower index first, which is the order the solver and the begin-touch report read it in. The contacts are
    /// put in that order once they are all found (<see cref="OrderContacts"/>); a step nobody was woken in found
    /// them in it.
    /// </para>
    /// </remarks>
    void FindContacts()
    {
        _contactCount = 0;
        _touchingCount = 0;
        ContactPointCount = 0;
        _stepStamp++;
        _ownersAtStart = _movingCount;

        for (var slot = 0; slot < _movingCount; slot++) _overlapM[_moving[slot]] = 0f;

        for (var slot = 0; slot < _movingCount; slot++)
        {
            var body = _moving[slot];
            _ownedInStep[body] = _stepStamp;
            _candidateCount = 0;
            Gather(_dynamicGrid, body, BodyFlags.Enabled);
            Gather(_frozenGrid, body, BodyFlags.Enabled | BodyFlags.FiledFrozen);
            Gather(_staticGrid, body, BodyFlags.Enabled);
            Order(_candidate.AsSpan(0, _candidateCount));

            for (var candidate = 0; candidate < _candidateCount; candidate++) Narrow(body, _candidate[candidate]);
        }

        OrderContacts();
    }

    /// <summary>
    /// Whatever this grid holds that could reach the body, carries <paramref name="live"/>, and has not owned its
    /// own pairs yet this step, once each.
    /// </summary>
    /// <remarks>
    /// <b>The bounds are asked first and everything else only of what they pass</b>: a cell holds mostly what this
    /// body's box does not reach, and the bounds are the one thing the narrow phase would read of it anyway. A
    /// body met in several cells is kept in the first of them (<see cref="CellGrid.FirstShared"/>). An entry
    /// without <paramref name="live"/> is one whose body is filed elsewhere now.
    /// </remarks>
    void Gather(CellGrid grid, int body, BodyFlags live)
    {
        var leastM = _leastM[body];
        var mostM = _mostM[body];
        if (!grid.TryRange(leastM, mostM, out var range)) return;

        for (var y = range.FromY; y <= range.ToY; y++)
        {
            for (var x = range.FromX; x <= range.ToX; x++)
            {
                foreach (var other in grid.Items(x, y))
                {
                    if (Apart(other, leastM, mostM)) continue;
                    if (!grid.FirstShared(range, x, y, _leastM[other])) continue;
                    if (_ownedInStep[other] == _stepStamp) continue;
                    if ((_flags[other] & live) != live) continue;
                    if ((_category[body] & _mask[other]) == 0 || (_category[other] & _mask[body]) == 0) continue;
                    if ((_channels[body] & _channels[other]) == 0) continue;

                    if (_candidateCount == _candidate.Length) Array.Resize(ref _candidate, Math.Max(64, _candidate.Length * 2));

                    _candidate[_candidateCount++] = other;
                }
            }
        }
    }

    /// <summary>Whether a body's bounds miss the box entirely.</summary>
    bool Apart(int body, Vector2 leastM, Vector2 mostM) =>
        _mostM[body].X < leastM.X || _leastM[body].X > mostM.X ||
        _mostM[body].Y < leastM.Y || _leastM[body].Y > mostM.Y;

    /// <summary>
    /// A handful of candidates put in index order. An insertion sort because that is what the list
    /// actually is — a car reaches a few props and one or two other cars — and because the order it
    /// leaves behind is what keeps a step's contacts in the order they are solved in.
    /// </summary>
    static void Order(Span<int> candidates)
    {
        for (var at = 1; at < candidates.Length; at++)
        {
            var body = candidates[at];
            var slot = at - 1;
            while (slot >= 0 && candidates[slot] > body)
            {
                candidates[slot + 1] = candidates[slot];
                slot--;
            }

            candidates[slot + 1] = body;
        }
    }

    /// <summary>
    /// One pair's manifold, taken lower index first whichever of the two owns it — <b>the same question asked the
    /// same way round</b>, so a pair's answer does not depend on which end of it the step reached first. The
    /// town's furniture is always second: it owns nothing.
    /// </summary>
    void Narrow(int owner, int other)
    {
        var otherStatic = (_flags[other] & BodyFlags.Static) != 0;
        var (first, second) = otherStatic || owner < other ? (owner, other) : (other, owner);
        if (!Shape.Collide(
                _positionM[first], _rotation[first], _extentM[first], _cornerRadiusM[first],
                _positionM[second], _rotation[second], _extentM[second], _cornerRadiusM[second],
                _config.SolverSpeculativeM, out var manifold))
        {
            return;
        }

        if ((_flags[other] & BodyFlags.Frozen) != 0) WakeInStep(other);

        RoomForContact();
        var contact = _contactCount++;
        _contactA[contact] = first;
        _contactB[contact] = second;
        _contactNormal[contact] = manifold.Normal;
        _contactPoints[contact] = manifold.PointCount;
        _pointM[contact * 2] = manifold.Point0;
        _separationM[contact * 2] = manifold.Separation0;
        _pointM[contact * 2 + 1] = manifold.Point1;
        _separationM[contact * 2 + 1] = manifold.Separation1;
        ContactPointCount += manifold.PointCount;

        var deepestM = -manifold.Separation0;
        if (manifold.PointCount > 1) deepestM = MathF.Max(deepestM, -manifold.Separation1);
        if (deepestM > 0f)
        {
            _overlapM[first] = MathF.Max(_overlapM[first], deepestM);
            if (!otherStatic) _overlapM[second] = MathF.Max(_overlapM[second], deepestM);
        }

        RoomForTouching(_touchingCount + 1);
        _touchingKey[_touchingCount] = ((ulong)(uint)first << 32) | (uint)second;
        _touchingCount++;
    }

    /// <summary>
    /// The step's contacts put in the order of their pairs: by the lower body, then the higher — the order every
    /// step found them in before anything froze, and the one the solver's answer is reproducible in.
    /// </summary>
    /// <remarks>
    /// An insertion sort, because the list is already in order unless somebody was woken, and then only the
    /// woken body's own pairs are out of it.
    /// </remarks>
    void OrderContacts()
    {
        for (var at = 1; at < _contactCount; at++)
        {
            if (_touchingKey[at - 1] < _touchingKey[at]) continue;

            var key = _touchingKey[at];
            var a = _contactA[at];
            var b = _contactB[at];
            var normal = _contactNormal[at];
            var points = _contactPoints[at];
            var point0 = _pointM[at * 2];
            var point1 = _pointM[at * 2 + 1];
            var separation0 = _separationM[at * 2];
            var separation1 = _separationM[at * 2 + 1];

            var slot = at - 1;
            while (slot >= 0 && _touchingKey[slot] > key)
            {
                MoveContact(slot, slot + 1);
                slot--;
            }

            var to = slot + 1;
            _touchingKey[to] = key;
            _contactA[to] = a;
            _contactB[to] = b;
            _contactNormal[to] = normal;
            _contactPoints[to] = points;
            _pointM[to * 2] = point0;
            _pointM[to * 2 + 1] = point1;
            _separationM[to * 2] = separation0;
            _separationM[to * 2 + 1] = separation1;
        }
    }

    void MoveContact(int from, int to)
    {
        _touchingKey[to] = _touchingKey[from];
        _contactA[to] = _contactA[from];
        _contactB[to] = _contactB[from];
        _contactNormal[to] = _contactNormal[from];
        _contactPoints[to] = _contactPoints[from];
        _pointM[to * 2] = _pointM[from * 2];
        _pointM[to * 2 + 1] = _pointM[from * 2 + 1];
        _separationM[to * 2] = _separationM[from * 2];
        _separationM[to * 2 + 1] = _separationM[from * 2 + 1];
    }
}
