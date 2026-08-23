using System.Numerics;

namespace TrafficSimulation.World.Physics;

/// <summary>Which pairs are worth testing: the two cell grids, what is moving, and the narrow test each surviving pair is given.</summary>
internal sealed partial class PhysicsWorld
{
    /// <summary>The bodies this step will integrate, in index order, with their bounds brought up to date.</summary>
    void ListMoving()
    {
        if (_moving.Length < _dynamicCount) _moving = new int[Math.Max(_dynamicCount, Room)];

        _movingCount = 0;
        for (var slot = 0; slot < _dynamicCount; slot++)
        {
            var body = _dynamic[slot];
            if ((_flags[body] & BodyFlags.Enabled) == 0) continue;

            Bound(body);
            _moving[_movingCount++] = body;
        }

        IntegratedBodyCount = _movingCount;
    }

    void IndexMoving()
    {
        _dynamicGrid.Rebuild(_moving.AsSpan(0, _movingCount), _leastM, _mostM, _config.SolverCellSizeM);
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

        ListMoving();
        IndexMoving();
    }

    /// <summary>
    /// Every pair worth a manifold, and the manifold. The moving roster is the outer loop and the static
    /// population is never walked: a body asks the two grids what is near it, and ninety-five thousand
    /// props are in the answer without being in the price.
    /// </summary>
    void FindContacts()
    {
        _contactCount = 0;
        _touchingCount = 0;
        ContactPointCount = 0;
        _stepStamp++;

        for (var slot = 0; slot < _movingCount; slot++) _overlapM[_moving[slot]] = 0f;

        for (var slot = 0; slot < _movingCount; slot++)
        {
            var body = _moving[slot];
            _candidateCount = 0;
            Gather(_dynamicGrid, body, movingOnly: true);
            Gather(_staticGrid, body, movingOnly: false);
            Order(_candidate.AsSpan(0, _candidateCount));

            for (var candidate = 0; candidate < _candidateCount; candidate++) Narrow(body, _candidate[candidate]);
        }
    }

    /// <summary>
    /// Whatever this grid holds that could reach the body, once each. A pair is owned by the
    /// <em>lower-indexed</em> moving body, which is what makes it appear exactly once and what keeps the
    /// touching list ascending.
    /// </summary>
    void Gather(CellGrid grid, int body, bool movingOnly)
    {
        if (!grid.TryRange(_leastM[body], _mostM[body], out var fromX, out var fromY, out var toX, out var toY)) return;

        var token = ((long)_stepStamp << 32) | (uint)body;
        for (var y = fromY; y <= toY; y++)
        {
            for (var x = fromX; x <= toX; x++)
            {
                foreach (var other in grid.Items(x, y))
                {
                    if (movingOnly && other <= body) continue;
                    if (_seen[other] == token) continue;

                    _seen[other] = token;
                    if ((_flags[other] & BodyFlags.Enabled) == 0) continue;
                    if ((_category[body] & _mask[other]) == 0 || (_category[other] & _mask[body]) == 0) continue;
                    if (Apart(other, _leastM[body], _mostM[body])) continue;

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
    /// leaves behind is the whole of what makes a step reproducible.
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

    void Narrow(int first, int second)
    {
        if (!Shape.Collide(
                _kind[first], _positionM[first], _rotation[first], _extentM[first],
                _kind[second], _positionM[second], _rotation[second], _extentM[second],
                _config.SolverSpeculativeM, out var manifold))
        {
            return;
        }

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
            if ((_flags[second] & BodyFlags.Static) == 0) _overlapM[second] = MathF.Max(_overlapM[second], deepestM);
        }

        RoomForTouching();
        _touchingKey[_touchingCount] = ((ulong)(uint)first << 32) | (uint)second;
        _touchingNormal[_touchingCount] = manifold.Normal;
        _touchingCount++;
    }
}
