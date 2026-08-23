using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.World.Physics;

/// <summary>Which roster a body belongs to. Static geometry is on none, and is what <see cref="BodyTag.None"/> means.</summary>
internal enum BodyKind : byte
{
    Ground,
    Person,
    Car,
}

/// <summary>A body's identity outside the solver: the roster it is on and its index on it.</summary>
internal readonly record struct BodyTag(BodyKind Kind, int Index)
{
    public static BodyTag None => new(BodyKind.Ground, -1);

    public int Packed => ((int)Kind << 24) | ((Index + 1) & 0xFFFFFF);

    public static BodyTag Unpack(int packed) => new((BodyKind)(packed >>> 24), (packed & 0xFFFFFF) - 1);
}

/// <summary>What a ray found: what it is, where it was met, how fast it is going, and how far along the ray it lay.</summary>
internal readonly record struct RayHit(BodyTag Tag, Vector2 PointM, Vector2 VelocityMps, float DistanceM);

/// <summary>
/// Two bodies that <b>began</b> touching in the step just taken, and the contact normal, which points
/// from the first towards the second.
/// </summary>
/// <remarks>
/// The beginning is reported and never the touching, which is the whole of "once per touch": a pair
/// resting against each other in a queue is one of these and never sixty a second.
/// </remarks>
internal readonly record struct Touch(BodyTag First, BodyTag Second, Vector2 Normal);

/// <summary>What is true of a body that is not a number: three bits, and nothing that needs a branch to read.</summary>
[Flags]
internal enum BodyFlags : byte
{
    None = 0,

    /// <summary>Immovable, undamageable, never integrated. Its zero inverse mass is the whole of the arithmetic.</summary>
    Static = 1 << 0,

    /// <summary>Off while a body is inside a container: no shape, no layer, no integration, and its slot kept.</summary>
    Enabled = 1 << 1,

    /// <summary>A person is never spun: one zero in the inverse inertia, and no case anywhere else.</summary>
    RotationLocked = 1 << 2,
}

/// <summary>
/// The solver, and the one wall between the simulation and it. Everything that has a body goes through
/// here, so what the simulation may ask of the physics is exactly this list: rigid bodies, contacts,
/// impulses, static geometry, layers, rotation lock and no gravity.
/// </summary>
/// <remarks>
/// <para>
/// The body table is a structure of arrays of blittable values — no object per body, per shape or per
/// contact, and nothing in a step the JIT cannot see through.
/// </para>
/// <para>
/// A person's heading is never told to the solver: a walker's shape is a circle with its rotation
/// locked, so a rotation on the body would describe something perfectly symmetrical. Heading is intent,
/// it belongs to the agent, and the only thing that reads it is what draws.
/// </para>
/// <para>
/// The damping figures reach every body and are three orders under the foot grip, so they do not decide
/// how far a walker glides; the grip does.
/// </para>
/// </remarks>
internal sealed partial class PhysicsWorld
{
    readonly SimConfig _config;

    Vector2[] _positionM = new Vector2[Room];
    Vector2[] _rotation = new Vector2[Room];
    float[] _headingRad = new float[Room];
    Vector2[] _velocityMps = new Vector2[Room];
    float[] _yawRateRadPerS = new float[Room];
    float[] _massKg = new float[Room];
    float[] _inverseMass = new float[Room];
    float[] _inverseInertia = new float[Room];
    Vector2[] _extentM = new Vector2[Room];
    ShapeKind[] _kind = new ShapeKind[Room];
    BodyFlags[] _flags = new BodyFlags[Room];
    ulong[] _category = new ulong[Room];
    ulong[] _mask = new ulong[Room];
    int[] _tag = new int[Room];
    Vector2[] _leastM = new Vector2[Room];
    Vector2[] _mostM = new Vector2[Room];
    float[] _overlapM = new float[Room];
    Vector2[] _pushMps = new Vector2[Room];
    float[] _pushYawRadPerS = new float[Room];
    long[] _seen = new long[Room];

    int[] _dynamic = new int[Room];
    int[] _static = new int[Room];

    int _count;
    int _dynamicCount;
    int _staticCount;

    /// <summary>Where a body table starts. It doubles from here, and a town's statics are what make it grow.</summary>
    const int Room = 1024;

    public PhysicsWorld(SimConfig config) => _config = config;

    public int StaticBodyCount => _staticCount;

    /// <summary>Every body with a mass, whether or not it is in the world this instant: a contained walker is still one of these.</summary>
    public int DynamicBodyCount => _dynamicCount;

    /// <summary>
    /// The bodies the last step actually integrated. Nothing sleeps here — islands and sleeping are
    /// deliberately not provided — so this is the dynamic roster less whatever is inside something.
    /// </summary>
    public int IntegratedBodyCount { get; private set; }

    /// <summary>Contact points the last step solved, which is the census every figure this solver reports has to be quoted against.</summary>
    public int ContactPointCount { get; private set; }

    /// <summary>
    /// A person: a circle, rotation locked and gravity off.
    /// </summary>
    public BodyId AddPerson(Vector2 positionM)
    {
        var body = Add(
            ShapeKind.Circle, positionM, 0f, new Vector2(_config.PersonDiameterM * 0.5f), CollisionLayer.Person,
            BodyFlags.Enabled | BodyFlags.RotationLocked, _config.Person.MassKg);

        _dynamic[_dynamicCount++] = body.Index;
        return body;
    }

    /// <summary>
    /// A car: a box, free to turn — a car's heading is solver output, unlike a walker's, because a car
    /// turns when its tyres turn it.
    /// </summary>
    public BodyId AddCar(Vector2 positionM, float headingRad)
    {
        var body = Add(
            ShapeKind.Box, positionM, headingRad, new Vector2(_config.Car.LengthM * 0.5f, _config.Car.WidthM * 0.5f),
            CollisionLayer.Car, BodyFlags.Enabled, _config.Car.MassKg);

        _dynamic[_dynamicCount++] = body.Index;
        return body;
    }

    /// <summary>A prop: one static circle, which is the whole of its shape.</summary>
    public BodyId AddStaticCircle(Vector2 centreM, float radiusM)
    {
        var body = Add(
            ShapeKind.Circle, centreM, 0f, new Vector2(radiusM), CollisionLayer.Static,
            BodyFlags.Enabled | BodyFlags.Static | BodyFlags.RotationLocked, massKg: 0f);

        _static[_staticCount++] = body.Index;
        _staticIndexStale = true;
        return body;
    }

    /// <summary>A building: one static box, turned the way the plan turned it.</summary>
    public BodyId AddStaticBox(Vector2 centreM, Vector2 sizeM, float headingRad)
    {
        var body = Add(
            ShapeKind.Box, centreM, headingRad, sizeM * 0.5f, CollisionLayer.Static,
            BodyFlags.Enabled | BodyFlags.Static | BodyFlags.RotationLocked, massKg: 0f);

        _static[_staticCount++] = body.Index;
        _staticIndexStale = true;
        return body;
    }

    /// <summary>
    /// A body that has gone inside a container. It is not simulated and has no collision shape while it
    /// is in there — the container is the only thing in the world — and it keeps its own slot rather than
    /// being destroyed and remade, so what comes back out is the same walker with the same index and tag.
    /// </summary>
    public void Contain(BodyId body)
    {
        var index = body.Index;
        _velocityMps[index] = Vector2.Zero;
        _yawRateRadPerS[index] = 0f;

        // Nothing can be inside a body that is not in the world, and the step will not clear this for it.
        _overlapM[index] = 0f;

        // The index is deliberately *not* marked stale. Going into a container only clears a bit that
        // every reader of the index already tests — the broad phase, the ray sweep and the moving roster
        // all skip a body that is not enabled — so the entry left behind is filtered rather than found.
        // Marking it would rebuild the whole moving grid inside phase 3, on the next headway ray any car
        // cast: a walker getting into a car would cost the town its own broad phase over again.
        // Coming back out is the other case and does mark it (Release), because a released body stands
        // somewhere new and the grid would otherwise answer about where it went in.
        //
        // What the rebuild also used to do was retake the census, so that is done here instead.
        if ((_flags[index] & BodyFlags.Enabled) != 0) IntegratedBodyCount--;

        _flags[index] &= ~BodyFlags.Enabled;
    }

    /// <summary>
    /// And back out, at the place its container put it down. The pose is written before the body is
    /// enabled, or it re-enters the broad phase where it went in and the solver sees a body teleport
    /// across the town.
    /// </summary>
    public void Release(BodyId body, Vector2 atM, float headingRad)
    {
        var index = body.Index;
        Place(index, atM, headingRad);
        _velocityMps[index] = Vector2.Zero;
        _yawRateRadPerS[index] = 0f;
        _flags[index] |= BodyFlags.Enabled;
        _movingIndexStale = true;
    }

    /// <summary>
    /// Lay the grid over the town's furniture, once all of it is standing. Call it after the last static
    /// body and never in a tick: the grid is built once and read for the rest of the run by every ray,
    /// every clearance query and every dynamic body's broad phase.
    /// </summary>
    /// <remarks>
    /// Forgetting it cannot lose a building — adding static geometry marks the grid stale and the next
    /// question lays it again, so this call is where the cost is <em>paid</em> rather than what makes the
    /// geometry real. Before the stale flag existed a missed call silently turned the town's walls into
    /// air.
    /// </remarks>
    public void SettleStatics()
    {
        _staticGrid.Rebuild(_static.AsSpan(0, _staticCount), _leastM, _mostM, _config.SolverCellSizeM);
        _staticIndexStale = false;
    }

    public Vector2 PositionOf(BodyId body) => _positionM[body.Index];

    /// <summary>
    /// Half the body's axis-aligned bounds as it now stands — <b>a box that holds the shape whichever way
    /// it is turned</b>, and therefore the support a caller measuring room around it may lean on without
    /// asking the shape itself.
    /// </summary>
    public Vector2 HalfExtentOf(BodyId body) => (_mostM[body.Index] - _leastM[body.Index]) * 0.5f;

    public Vector2 VelocityOf(BodyId body) => _velocityMps[body.Index];

    public float MassOf(BodyId body) => _massKg[body.Index];

    public float HeadingOf(BodyId body) => _headingRad[body.Index];

    /// <summary>
    /// The same heading as the unit vector the step already reduced it to. A caller that wants the
    /// direction takes it from here rather than turning the angle back into one: the integration writes
    /// this pair for every body it moves, so asking for it costs a load and asking for the angle instead
    /// costs a <c>sincos</c>.
    /// </summary>
    public Vector2 RotationOf(BodyId body) => _rotation[body.Index];

    public float YawRateOf(BodyId body) => _yawRateRadPerS[body.Index];

    /// <summary>
    /// What a body is, to whoever finds it: which roster it is on and where on it. Kept in the table
    /// beside the pose rather than in a user-data field, because it is read off a ray hit in the middle
    /// of a tick and a lookup by index is a load where a boxed field would be a cast.
    /// </summary>
    public void Tag(BodyId body, BodyTag tag) => _tag[body.Index] = tag.Packed;

    public BodyTag TagOf(BodyId body) => BodyTag.Unpack(_tag[body.Index]);

    /// <summary>
    /// The one thing an agent actuates. An impulse of nothing is never applied.
    /// </summary>
    public void ApplyCentralImpulse(BodyId body, Vector2 impulseNs)
    {
        if (impulseNs == Vector2.Zero) return;

        var index = body.Index;
        _velocityMps[index] += impulseNs * _inverseMass[index];
    }

    /// <summary>
    /// An impulse spent at a point rather than at the centre, which is the whole of how a car moves:
    /// four wheels, four points, and the yaw that falls out of the difference between them.
    /// </summary>
    public void ApplyImpulseAt(BodyId body, Vector2 impulseNs, Vector2 atM)
    {
        if (impulseNs == Vector2.Zero) return;

        var index = body.Index;
        _velocityMps[index] += impulseNs * _inverseMass[index];
        _yawRateRadPerS[index] += _inverseInertia[index] * Shape.Cross(atM - _positionM[index], impulseNs);
    }

    /// <summary>
    /// How deep this body was into everything touching it when the last step was taken, in metres. An
    /// instrument, never called from a tick.
    /// </summary>
    /// <remarks>
    /// The step's own manifold separations and not a sweep taken afterwards, so it answers about the
    /// geometry the solver acted on rather than giving a second opinion about it. The figure belongs to
    /// the poses the step began at and not to the ones it left behind, which is why the soak counts
    /// <em>runs</em> of ticks rather than instants.
    /// </remarks>
    public float OverlapOf(BodyId body) => _overlapM[body.Index];

    /// <summary>
    /// The one place a layer becomes a body's filter. The mask is the <em>symmetrised</em> one, so the
    /// solver's <c>&amp;&amp;</c> over the two masks answers "either scans the other"
    /// (<see cref="CollisionLayers"/>).
    /// </summary>
    static ulong MaskOf(CollisionLayer layer) => (ulong)CollisionLayers.MaskOf(layer);

    /// <summary>
    /// A query from something that is itself on the road: everything a car or a walker would scan. Its
    /// mask is the layer table's, so what a ray finds is decided by the same matrix that decides what a
    /// body hits, and not by a filter of the query's own.
    /// </summary>
    /// <remarks>
    /// Whether the town's furniture is in a ray's question is decided by <em>which grids the ray enters</em>
    /// (<see cref="CastRay"/>) and not by striking a bit out of this. A mask can only reject what has
    /// already been reached; not entering the static grid is not reaching it.
    /// </remarks>
    const CollisionLayer LookingAs = CollisionLayer.Car;

    static readonly ulong LookingMask = MaskOf(LookingAs);

    static readonly ulong StaticsOnlyMask = (ulong)CollisionLayer.Static;

    BodyId Add(ShapeKind kind, Vector2 positionM, float headingRad, Vector2 extentM, CollisionLayer layer, BodyFlags flags, float massKg)
    {
        if (_count == _positionM.Length) Grow();

        var index = _count++;
        _kind[index] = kind;
        _extentM[index] = extentM;
        _flags[index] = flags;
        _category[index] = (ulong)layer;
        _mask[index] = MaskOf(layer);
        _tag[index] = BodyTag.None.Packed;
        _velocityMps[index] = Vector2.Zero;
        _yawRateRadPerS[index] = 0f;
        _overlapM[index] = 0f;

        var immovable = (flags & BodyFlags.Static) != 0;
        _massKg[index] = immovable ? 0f : massKg;
        _inverseMass[index] = immovable || massKg <= 0f ? 0f : 1f / massKg;
        _inverseInertia[index] = immovable || (flags & BodyFlags.RotationLocked) != 0 || massKg <= 0f
            ? 0f
            : 1f / (massKg * Shape.InertiaPerKg(kind, extentM));

        Place(index, positionM, headingRad);
        _movingIndexStale = true;
        return new BodyId(index + 1);
    }

    void Place(int index, Vector2 positionM, float headingRad)
    {
        _positionM[index] = positionM;
        _headingRad[index] = headingRad;
        _rotation[index] = Shape.Rotation(headingRad);
        Bound(index);
    }

    /// <summary>The body's own axis-aligned bounds, which are what the broad phase is laid over.</summary>
    void Bound(int index)
    {
        var half = Shape.HalfBoundsM(_kind[index], _rotation[index], _extentM[index]);
        _leastM[index] = _positionM[index] - half;
        _mostM[index] = _positionM[index] + half;
    }

    void Grow()
    {
        var room = _positionM.Length * 2;
        Array.Resize(ref _positionM, room);
        Array.Resize(ref _rotation, room);
        Array.Resize(ref _headingRad, room);
        Array.Resize(ref _velocityMps, room);
        Array.Resize(ref _yawRateRadPerS, room);
        Array.Resize(ref _massKg, room);
        Array.Resize(ref _inverseMass, room);
        Array.Resize(ref _inverseInertia, room);
        Array.Resize(ref _extentM, room);
        Array.Resize(ref _kind, room);
        Array.Resize(ref _flags, room);
        Array.Resize(ref _category, room);
        Array.Resize(ref _mask, room);
        Array.Resize(ref _tag, room);
        Array.Resize(ref _leastM, room);
        Array.Resize(ref _mostM, room);
        Array.Resize(ref _overlapM, room);
        Array.Resize(ref _pushMps, room);
        Array.Resize(ref _pushYawRadPerS, room);
        Array.Resize(ref _seen, room);
        Array.Resize(ref _dynamic, room);
        Array.Resize(ref _static, room);
    }
}
