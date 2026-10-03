using System.Numerics;

namespace TrafficSimulation.World.Physics;

/// <summary>The contact roster across steps — what began touching this step, and the room the manifolds are kept in.</summary>
internal sealed partial class PhysicsWorld
{
    /// <summary>
    /// Which pairs are touching now that were not touching last step: a linear merge over two sorted
    /// lists, and no table of pairs kept anywhere. The sortedness is a consequence of how the contacts were
    /// put in order, so the bookkeeping costs one pass and no memory beyond the lists.
    /// </summary>
    /// <remarks>
    /// <b>A pair nobody owned this step is still touching</b> (SOL-37c): both its bodies were frozen through the
    /// step, or one was and the other is the town's furniture, so neither has moved since the step that last
    /// asked. It is held rather than asked again — and rather than dropped, which would have it begin again on
    /// whichever step one of the two is woken, and be judged twice for one touch.
    /// </remarks>
    void ReportBegun()
    {
        _beganCount = 0;
        _heldCount = 0;

        var now = 0;
        var was = 0;
        while (now < _touchingCount && was < _previousCount)
        {
            if (_touchingKey[now] < _previousKey[was])
            {
                Begin(now++);
            }
            else if (_touchingKey[now] > _previousKey[was])
            {
                Hold(_previousKey[was++]);
            }
            else
            {
                now++;
                was++;
            }
        }

        while (now < _touchingCount) Begin(now++);
        while (was < _previousCount) Hold(_previousKey[was++]);

        // What is touching from here on — this step's pairs and the held ones, one sorted list — written over
        // the last step's, which nothing reads again.
        RoomForTouching(_touchingCount + _heldCount);
        var into = 0;
        var held = 0;
        now = 0;
        while (now < _touchingCount || held < _heldCount)
        {
            _previousKey[into++] = held == _heldCount || (now < _touchingCount && _touchingKey[now] < _heldKey[held])
                ? _touchingKey[now++]
                : _heldKey[held++];
        }

        _previousCount = into;
    }

    void Begin(int touching)
    {
        var key = _touchingKey[touching];
        _beganA[_beganCount] = (int)(key >> 32);
        _beganB[_beganCount] = (int)(key & 0xFFFFFFFF);
        _beganNormal[_beganCount] = _contactNormal[touching];
        _beganCount++;
    }

    /// <summary>A pair the last step had and this one did not find, kept where neither of its bodies was asked.</summary>
    void Hold(ulong key)
    {
        if (OwnedThisStep((int)(key >> 32)) || OwnedThisStep((int)(key & 0xFFFFFFFF))) return;

        _heldKey[_heldCount++] = key;
    }

    /// <summary>
    /// Whether this step asked what a body touches: it was an owner, or it is out of the world, where it touches
    /// nothing. Static and frozen bodies are neither.
    /// </summary>
    bool OwnedThisStep(int body) =>
        _ownedInStep[body] == _stepStamp || (_flags[body] & BodyFlags.Enabled) == 0;

    /// <summary>One grid's part of a cast, over the entries carrying <paramref name="live"/> (<see cref="Gather"/>).</summary>
    void Sweep(CellGrid grid, BodyFlags live, Vector2 fromM, Vector2 travelM, int ignore, ref float nearest, ref int found)
    {
        var walk = grid.Walk(fromM, travelM);
        while (walk.MoveNext())
        {
            foreach (var body in walk.Items)
            {
                if (body == ignore) continue;
                if ((_flags[body] & live) != live) continue;
                if ((_category[body] & LookingMask) == 0 || (_mask[body] & (ulong)LookingAs) == 0) continue;
                if (!Shape.CastSegment(
                        fromM, travelM, _positionM[body], _rotation[body], _extentM[body],
                        _cornerRadiusM[body], out var met))
                {
                    continue;
                }

                if (met >= nearest) continue;

                nearest = met;
                found = body;
            }

            // Nothing in a later cell can be nearer than what has already been met inside this one.
            if (nearest <= walk.ExitFraction) return;
        }
    }

    void RoomForContact()
    {
        if (_contactCount < _contactA.Length) return;

        var room = Math.Max(256, _contactA.Length * 2);
        Array.Resize(ref _contactA, room);
        Array.Resize(ref _contactB, room);
        Array.Resize(ref _contactNormal, room);
        Array.Resize(ref _contactPoints, room);
        Array.Resize(ref _pointM, room * 2);
        Array.Resize(ref _separationM, room * 2);
        Array.Resize(ref _armA, room * 2);
        Array.Resize(ref _armB, room * 2);
        Array.Resize(ref _normalMass, room * 2);
        Array.Resize(ref _tangentMass, room * 2);
        Array.Resize(ref _normalImpulseNs, room * 2);
        Array.Resize(ref _tangentImpulseNs, room * 2);
        Array.Resize(ref _pushImpulseNs, room * 2);
    }

    /// <summary>
    /// All six lists grow together and to the same length, <b>to hold <paramref name="pairs"/></b>: the list of
    /// what was touching is this step's pairs and the held ones together, so it is the one that outgrows the rest.
    /// </summary>
    void RoomForTouching(int pairs)
    {
        if (pairs <= _touchingKey.Length) return;

        var room = Math.Max(Math.Max(256, pairs), _touchingKey.Length * 2);
        Array.Resize(ref _touchingKey, room);
        Array.Resize(ref _previousKey, room);
        Array.Resize(ref _heldKey, room);
        Array.Resize(ref _beganA, room);
        Array.Resize(ref _beganB, room);
        Array.Resize(ref _beganNormal, room);
    }

    /// <summary>The walk over one step's begin-touch events. Not a snapshot — it reads the world's own arrays in place.</summary>
    internal struct BeganTouching
    {
        readonly PhysicsWorld _physics;
        int _next;

        public BeganTouching(PhysicsWorld physics)
        {
            _physics = physics;
            _next = 0;
            Current = default;
        }

        public Touch Current { get; private set; }

        public readonly BeganTouching GetEnumerator() => this;

        public bool MoveNext()
        {
            if (_next >= _physics._beganCount) return false;

            var began = _next++;
            Current = new Touch(
                BodyTag.Unpack(_physics._tag[_physics._beganA[began]]),
                BodyTag.Unpack(_physics._tag[_physics._beganB[began]]),
                _physics._beganNormal[began]);
            return true;
        }
    }
}
