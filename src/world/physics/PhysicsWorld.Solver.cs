
using System.Numerics;

namespace TrafficSimulation.World.Physics;

/// <summary>
/// The step itself: the broad phase over the town, the narrow phase for the two shapes it has, the
/// contact solver, and the diff that says which pairs <em>began</em> touching.
/// </summary>
/// <remarks>
/// <para>
/// There is no gravity, and that is the single largest simplifier: with nothing pulling bodies into each
/// other there are no resting stacks and no gravity-driven penetration, so none of the sub-stepped
/// soft-constraint machinery a general 2D solver carries is wanted. One narrow phase a tick, a
/// sequential-impulse velocity solve, and a positional pass that pushes an overlap out <em>without the
/// push becoming motion</em> — the correction is solved on a second accumulator, spent on the
/// integration and thrown away, so a resting pair does not breathe and a queue does not jitter. Folding
/// it into the body's real velocity is the classic way to get a town that shivers.
/// </para>
/// <para>
/// Nothing is swept: a fast body can pass through a thin one. <b>A body at rest is frozen</b> (SOL-37) and
/// left out of the step until something reaches it; every contact of every body the step moves is run.
/// </para>
/// <para>
/// Every order in here derives from body indices. Pairs are gathered by the owners in index order with
/// their candidates sorted, so the touching list comes out ascending by key unless a body was woken —
/// which lets the begin-touch report be a linear merge against the last step's list rather than a hash
/// table.
/// </para>
/// </remarks>
internal sealed partial class PhysicsWorld
{
    readonly CellGrid _staticGrid = new();
    readonly CellGrid _dynamicGrid = new();

    /// <summary>
    /// <b>The awake roster</b>: every body in the world and not frozen, in index order between steps — the owners of
    /// the next one. Inside a step the bodies it wakes are added at the end (<see cref="WakeInStep"/>).
    /// </summary>
    int[] _moving = new int[Room];
    int _movingCount;

    /// <summary>How many owners the step began with: the sorted head of <see cref="_moving"/>, the woken after it.</summary>
    int _ownersAtStart;

    int[] _filed = new int[Room];
    int _filedCount;
    bool _staticIndexStale;

    int[] _candidate = [];
    int _candidateCount;
    int _stepStamp;

    int[] _contactA = [];
    int[] _contactB = [];
    Vector2[] _contactNormal = [];
    int[] _contactPoints = [];
    int _contactCount;

    // Two points per contact, laid flat: point p of contact c is at c * 2 + p in every one of these.
    Vector2[] _pointM = [];
    float[] _separationM = [];
    Vector2[] _armA = [];
    Vector2[] _armB = [];
    float[] _normalMass = [];
    float[] _tangentMass = [];
    float[] _normalImpulseNs = [];
    float[] _tangentImpulseNs = [];
    float[] _pushImpulseNs = [];

    // One key a contact, in the contacts' own order: the pair, lower body first.
    ulong[] _touchingKey = [];
    int _touchingCount;

    ulong[] _previousKey = [];
    int _previousCount;

    ulong[] _heldKey = [];
    int _heldCount;

    int[] _beganA = [];
    int[] _beganB = [];
    Vector2[] _beganNormal = [];
    int _beganCount;

    /// <summary>
    /// One tick of the world: what is touching what, what that does to the velocities, and where
    /// everything ends up.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The moving index is laid once a step, at the poses the step begins at</b>, which is the only index the
    /// step reads. Nothing outside a step asks it anything that decides: the town's clearance queries are of the
    /// static grid alone, so a step's own is the one lay it pays for, however many walkers came out of doors
    /// since the last.
    /// </para>
    /// <para>
    /// The census is taken after the contacts, because the bodies they woke are integrated too.
    /// </para>
    /// </remarks>
    public void Step(float dtS)
    {
        if (_staticIndexStale) SettleStatics();

        RestTheLinkedAfresh();
        LayTheIndexes();
        FindContacts();
        IntegratedBodyCount = _movingCount;
        DampAndEmptyThePush(dtS);
        Prepare();
        PrepareLinks();
        SolveVelocities(dtS);
        SolvePush(dtS);
        IntegrateAndCountTheRest(dtS);
        FreezeWhatHasSettled();
        ReportBegun();
    }

    /// <summary>
    /// Every pair that began touching in the step just taken. Read after the step and before the next.
    /// </summary>
    public BeganTouching BeganTouchingThisStep() => new(this);

    /// <summary>
    /// The damping that reaches every body laid damped, applied once a step — and the push accumulator emptied on the same
    /// pass, since nothing between here and <see cref="SolvePush"/> reads it.
    /// </summary>
    void DampAndEmptyThePush(float dtS)
    {
        var linear = 1f / (1f + dtS * _config.Solver.LinearDamping);
        var angular = 1f / (1f + dtS * _config.Solver.AngularDamping);
        for (var slot = 0; slot < _movingCount; slot++)
        {
            var body = _moving[slot];
            if ((_flags[body] & BodyFlags.Undamped) == 0)
            {
                _velocityMps[body] *= linear;
                _yawRateRadPerS[body] *= angular;
            }

            _pushMps[body] = Vector2.Zero;
            _pushYawRadPerS[body] = 0f;
        }
    }

    /// <summary>
    /// The arms and the effective masses each contact point is solved against. Taken once for the whole
    /// step, because the geometry is the narrow phase's and does not move while the velocities are being
    /// solved.
    /// </summary>
    /// <remarks>
    /// Impulses start from nothing every step and are not carried across ticks. Warm starting is what
    /// makes a tall stack converge under gravity, and there is no gravity and no stack here; what it
    /// would buy is unmeasured.
    /// </remarks>
    void Prepare()
    {
        for (var contact = 0; contact < _contactCount; contact++)
        {
            var first = _contactA[contact];
            var second = _contactB[contact];
            var normal = _contactNormal[contact];
            var tangent = Shape.LeftPerpendicular(normal);

            for (var point = 0; point < _contactPoints[contact]; point++)
            {
                var at = contact * 2 + point;
                var armA = _pointM[at] - _positionM[first];
                var armB = _pointM[at] - _positionM[second];
                _armA[at] = armA;
                _armB[at] = armB;

                _normalMass[at] = EffectiveMass(first, second, armA, armB, normal);
                _tangentMass[at] = EffectiveMass(first, second, armA, armB, tangent);
                _normalImpulseNs[at] = 0f;
                _tangentImpulseNs[at] = 0f;
                _pushImpulseNs[at] = 0f;
            }
        }
    }

    float EffectiveMass(int first, int second, Vector2 armA, Vector2 armB, Vector2 direction)
    {
        var turnA = Shape.Cross(armA, direction);
        var turnB = Shape.Cross(armB, direction);
        var resistance = _inverseMass[first] + _inverseMass[second]
                         + _inverseInertia[first] * turnA * turnA + _inverseInertia[second] * turnB * turnB;
        return resistance > 0f ? 1f / resistance : 0f;
    }

    /// <summary>
    /// The collision response: no bounce, Coulomb friction, and a speculative term that stops an approach
    /// still short of touching in the tick it would otherwise have crossed — one within the speculative
    /// distance (<see cref="SpeculativeM"/>). Restitution is not modelled at all — a collision in
    /// this town is a crash rather than a break shot, so there is no coefficient to set to zero.
    /// </summary>
    void SolveVelocities(float dtS)
    {
        var friction = _config.Solver.Friction;
        var inverseStep = 1f / dtS;

        for (var pass = 0; pass < _config.Solver.VelocityIterations; pass++)
        {
            SolveLinkVelocities();
            for (var contact = 0; contact < _contactCount; contact++)
            {
                var first = _contactA[contact];
                var second = _contactB[contact];
                var normal = _contactNormal[contact];
                var tangent = Shape.LeftPerpendicular(normal);

                for (var point = 0; point < _contactPoints[contact]; point++)
                {
                    var at = contact * 2 + point;
                    var armA = _armA[at];
                    var armB = _armB[at];

                    var closing = Closing(first, second, armA, armB);

                    // A pair that has not met yet may only be slowed by as much as would close the gap:
                    // anything more would stop a body short of something it has not reached.
                    var separationM = _separationM[at];
                    var speculative = separationM > 0f ? separationM * inverseStep : 0f;

                    var along = Vector2.Dot(closing, normal);
                    var wanted = -_normalMass[at] * (along + speculative);
                    var total = MathF.Max(_normalImpulseNs[at] + wanted, 0f);
                    var spend = total - _normalImpulseNs[at];
                    _normalImpulseNs[at] = total;
                    Push(first, second, armA, armB, normal * spend);

                    // Friction is spent against whatever the normal has already taken, which is what
                    // makes it Coulomb rather than a fixed drag.
                    closing = Closing(first, second, armA, armB);
                    var across = Vector2.Dot(closing, tangent);
                    var slide = -_tangentMass[at] * across;
                    var most = friction * _normalImpulseNs[at];
                    var held = Math.Clamp(_tangentImpulseNs[at] + slide, -most, most);
                    spend = held - _tangentImpulseNs[at];
                    _tangentImpulseNs[at] = held;
                    Push(first, second, armA, armB, tangent * spend);
                }
            }
        }
    }

    /// <summary>
    /// The overlap pushed out on a second set of velocities, which the integration spends and throws away.
    /// </summary>
    /// <remarks>
    /// The push must never reach the body's own motion: a correction folded into the real velocity is
    /// energy the collision did not have — a resting pair breathes, a queue jitters, and the damage
    /// arbiter reads a closing speed that nothing caused. Its own accumulator is the whole of the fix,
    /// and it costs one more pass over the same contacts.
    /// </remarks>
    void SolvePush(float dtS)
    {
        var allowedM = _config.Solver.AllowedPenetrationM;
        var bias = _config.Solver.ContactBias / dtS;
        var mostMps = _config.Solver.MaxPushOutMps;

        for (var pass = 0; pass < _config.Solver.PositionIterations; pass++)
        {
            SolveLinkPush(bias, mostMps);
            for (var contact = 0; contact < _contactCount; contact++)
            {
                var first = _contactA[contact];
                var second = _contactB[contact];
                var normal = _contactNormal[contact];

                for (var point = 0; point < _contactPoints[contact]; point++)
                {
                    var at = contact * 2 + point;

                    // The allowance is what a solver is permitted to leave: correcting it away is what
                    // makes two resting bodies chatter against each other for the rest of the run.
                    var depthM = -(_separationM[at] + allowedM);
                    if (depthM <= 0f) continue;

                    var armA = _armA[at];
                    var armB = _armB[at];
                    var wantedMps = MathF.Min(depthM * bias, mostMps);
                    var along = Vector2.Dot(Separating(first, second, armA, armB), normal);
                    var wanted = _normalMass[at] * (wantedMps - along);
                    var total = MathF.Max(_pushImpulseNs[at] + wanted, 0f);
                    var spend = total - _pushImpulseNs[at];
                    _pushImpulseNs[at] = total;

                    var impulse = normal * spend;
                    _pushMps[first] -= impulse * _inverseMass[first];
                    _pushYawRadPerS[first] -= _inverseInertia[first] * Shape.Cross(armA, impulse);
                    _pushMps[second] += impulse * _inverseMass[second];
                    _pushYawRadPerS[second] += _inverseInertia[second] * Shape.Cross(armB, impulse);
                }
            }
        }
    }

    /// <summary>How fast the second body's surface is moving towards the first's at this point.</summary>
    Vector2 Closing(int first, int second, Vector2 armA, Vector2 armB) =>
        _velocityMps[second] + Shape.CrossYaw(_yawRateRadPerS[second], armB)
        - _velocityMps[first] - Shape.CrossYaw(_yawRateRadPerS[first], armA);

    /// <summary>The same question asked of the push accumulator, which is the one the overlap is solved on.</summary>
    Vector2 Separating(int first, int second, Vector2 armA, Vector2 armB) =>
        _pushMps[second] + Shape.CrossYaw(_pushYawRadPerS[second], armB)
        - _pushMps[first] - Shape.CrossYaw(_pushYawRadPerS[first], armA);

    void Push(int first, int second, Vector2 armA, Vector2 armB, Vector2 impulseNs)
    {
        _velocityMps[first] -= impulseNs * _inverseMass[first];
        _yawRateRadPerS[first] -= _inverseInertia[first] * Shape.Cross(armA, impulseNs);
        _velocityMps[second] += impulseNs * _inverseMass[second];
        _yawRateRadPerS[second] += _inverseInertia[second] * Shape.Cross(armB, impulseNs);
    }

    /// <summary>
    /// Where the step leaves everything: the body's own motion plus the push it was given, and the push
    /// discarded on the way out. A body that moves stops being where the frozen index filed it. Each body's rest
    /// is counted on the same pass, off the motion the step left it (<see cref="FreezeWhatHasSettled"/>).
    /// </summary>
    /// <remarks>
    /// <b>A heading the step left alone to the bit keeps its rotation, and a pose left alone keeps its bounds</b> — each
    /// is a function of those bits alone (<see cref="Place"/> is the only other writer), so what is skipped is the same
    /// answer and not one near it: a walker never turns and a car standing still never moves. The comparison is of bits
    /// and not of values, so a heading gone from −0 to +0 is turned again (integrate.comp's own skip compares values).
    /// </remarks>
    void IntegrateAndCountTheRest(float dtS)
    {
        var restMps = _config.Solver.RestSpeedMps;
        var restSquared = restMps * restMps;
        var restTicks = _config.SolverRestTicks;
        var allowedM = _config.Solver.AllowedPenetrationM;

        for (var slot = 0; slot < _movingCount; slot++)
        {
            var body = _moving[slot];
            var travelMps = _velocityMps[body] + _pushMps[body];
            var turnRadPerS = _yawRateRadPerS[body] + _pushYawRadPerS[body];
            if (travelMps != Vector2.Zero || turnRadPerS != 0f) _flags[body] &= ~BodyFlags.FiledFrozen;

            var wasM = _positionM[body];
            var positionM = wasM + travelMps * dtS;
            var wasRad = _headingRad[body];
            var headingRad = Wrap(wasRad + turnRadPerS * dtS);
            var turned = BitConverter.SingleToInt32Bits(headingRad) != BitConverter.SingleToInt32Bits(wasRad);
            _positionM[body] = positionM;
            _headingRad[body] = headingRad;
            if (turned) _rotation[body] = Shape.Rotation(headingRad);
            if (turned || !SameBits(positionM, wasM)) Bound(body);

            var swingMps = _yawRateRadPerS[body] * _reachM[body];
            var atRest = _velocityMps[body].LengthSquared() <= restSquared && swingMps * swingMps <= restSquared
                         && _overlapM[body] <= allowedM;
            _restTicks[body] = atRest ? Math.Min(_restTicks[body] + 1, restTicks) : 0;
            _settling[body] = _restTicks[body] >= restTicks;
        }
    }

    static bool SameBits(Vector2 a, Vector2 b) =>
        BitConverter.SingleToInt32Bits(a.X) == BitConverter.SingleToInt32Bits(b.X)
        && BitConverter.SingleToInt32Bits(a.Y) == BitConverter.SingleToInt32Bits(b.Y);

    /// <summary>A heading is reported in the half-open turn the rest of the engine reads.</summary>
    static float Wrap(float headingRad)
    {
        const float Turn = MathF.PI * 2f;
        while (headingRad > MathF.PI) headingRad -= Turn;
        while (headingRad <= -MathF.PI) headingRad += Turn;
        return headingRad;
    }
}
