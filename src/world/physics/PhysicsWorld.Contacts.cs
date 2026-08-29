using System.Numerics;

namespace TrafficSimulation.World.Physics;

/// <summary>The contact roster across steps — what began touching this step, and the room the manifolds are kept in.</summary>
internal sealed partial class PhysicsWorld
{
    /// <summary>
    /// Which pairs are touching now that were not touching last step: a linear merge over two sorted
    /// lists, and no table of pairs kept anywhere. The sortedness is a consequence of how the pairs were
    /// gathered, so the bookkeeping costs one pass and no memory beyond the two lists.
    /// </summary>
    void ReportBegun()
    {
        _beganCount = 0;

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
                was++;
            }
            else
            {
                now++;
                was++;
            }
        }

        while (now < _touchingCount) Begin(now++);

        (_previousKey, _touchingKey) = (_touchingKey, _previousKey);
        _previousCount = _touchingCount;
    }

    void Begin(int touching)
    {
        var key = _touchingKey[touching];
        _beganA[_beganCount] = (int)(key >> 32);
        _beganB[_beganCount] = (int)(key & 0xFFFFFFFF);
        _beganNormal[_beganCount] = _touchingNormal[touching];
        _beganCount++;
    }

    void Sweep(CellGrid grid, Vector2 fromM, Vector2 travelM, int ignore, ref float nearest, ref int found)
    {
        var walk = grid.Walk(fromM, travelM);
        while (walk.MoveNext())
        {
            foreach (var body in walk.Items)
            {
                if (body == ignore) continue;
                if ((_flags[body] & BodyFlags.Enabled) == 0) continue;
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
    /// All six lists grow together and to the same length. The two key buffers change places every step —
    /// this step's touching list is next step's list of what was touching — so buffers of different sizes
    /// would be resized back and forth once a tick for the rest of the run.
    /// </summary>
    void RoomForTouching()
    {
        if (_touchingCount < _touchingKey.Length) return;

        var room = Math.Max(256, _touchingKey.Length * 2);
        Array.Resize(ref _touchingKey, room);
        Array.Resize(ref _touchingNormal, room);
        Array.Resize(ref _previousKey, room);
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
