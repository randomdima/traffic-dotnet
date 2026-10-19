using System.Numerics;

namespace TrafficSimulation.World.Physics;

/// <summary>
/// <b>The body table as it stands</b>, row for row: what a device lays into its own buffers, and nothing that steps it.
/// Read between steps; a step rewrites every span it holds.
/// </summary>
internal readonly ref struct BodyTable
{
    public required int Count { get; init; }
    public required ReadOnlySpan<Vector2> PositionM { get; init; }
    public required ReadOnlySpan<Vector2> Rotation { get; init; }
    public required ReadOnlySpan<float> HeadingRad { get; init; }
    public required ReadOnlySpan<Vector2> VelocityMps { get; init; }
    public required ReadOnlySpan<float> YawRateRadPerS { get; init; }
    public required ReadOnlySpan<float> InverseMass { get; init; }
    public required ReadOnlySpan<float> InverseInertia { get; init; }
    public required ReadOnlySpan<Vector2> ExtentM { get; init; }
    public required ReadOnlySpan<float> CornerRadiusM { get; init; }

    /// <summary>How far each body's furthest point stands from its centre: its core's half-diagonal and its corner radius.</summary>
    public required ReadOnlySpan<float> ReachM { get; init; }

    /// <summary>How many steps in a row each body has ended at rest, up to the steps it takes to freeze (SOL-37).</summary>
    public required ReadOnlySpan<int> RestTicks { get; init; }

    public required ReadOnlySpan<BodyFlags> Flags { get; init; }
    public required ReadOnlySpan<ulong> Category { get; init; }
    public required ReadOnlySpan<ulong> Mask { get; init; }
    public required ReadOnlySpan<byte> Channels { get; init; }
    public required ReadOnlySpan<Vector2> LeastM { get; init; }
    public required ReadOnlySpan<Vector2> MostM { get; init; }

    /// <summary>The awake roster in index order, as the next step will own it (SOL-37): every enabled body not frozen.</summary>
    public required ReadOnlySpan<int> Moving { get; init; }

    /// <summary>The town's furniture, in the order it was added.</summary>
    public required ReadOnlySpan<int> Statics { get; init; }

    /// <summary>The links held (SOL-38), in the order a step solves them.</summary>
    public required ReadOnlySpan<LinkRow> Links { get; init; }
}

/// <summary>
/// <b>The last step's contacts</b>, in the order it solved them — by the lower body, then the higher, the furniture
/// always second — each with its normal and one or two points (two a contact, laid flat: point p of contact c at 2c + p).
/// </summary>
internal readonly ref struct ContactTable
{
    public required int Count { get; init; }
    public required ReadOnlySpan<int> First { get; init; }
    public required ReadOnlySpan<int> Second { get; init; }
    public required ReadOnlySpan<Vector2> Normal { get; init; }
    public required ReadOnlySpan<int> Points { get; init; }
    public required ReadOnlySpan<Vector2> PointM { get; init; }
    public required ReadOnlySpan<float> SeparationM { get; init; }
}

internal sealed partial class PhysicsWorld
{
    /// <inheritdoc cref="BodyTable"/>
    public BodyTable Table => new()
    {
        Count = _count,
        PositionM = _positionM.AsSpan(0, _count),
        Rotation = _rotation.AsSpan(0, _count),
        HeadingRad = _headingRad.AsSpan(0, _count),
        VelocityMps = _velocityMps.AsSpan(0, _count),
        YawRateRadPerS = _yawRateRadPerS.AsSpan(0, _count),
        InverseMass = _inverseMass.AsSpan(0, _count),
        InverseInertia = _inverseInertia.AsSpan(0, _count),
        ExtentM = _extentM.AsSpan(0, _count),
        CornerRadiusM = _cornerRadiusM.AsSpan(0, _count),
        ReachM = _reachM.AsSpan(0, _count),
        RestTicks = _restTicks.AsSpan(0, _count),
        Flags = _flags.AsSpan(0, _count),
        Category = _category.AsSpan(0, _count),
        Mask = _mask.AsSpan(0, _count),
        Channels = _channels.AsSpan(0, _count),
        LeastM = _leastM.AsSpan(0, _count),
        MostM = _mostM.AsSpan(0, _count),
        Moving = _moving.AsSpan(0, _movingCount),
        Statics = _static.AsSpan(0, _staticCount),
        Links = _links.Live,
    };

    /// <inheritdoc cref="ContactTable"/>
    public ContactTable Contacts => new()
    {
        Count = _contactCount,
        First = _contactA.AsSpan(0, _contactCount),
        Second = _contactB.AsSpan(0, _contactCount),
        Normal = _contactNormal.AsSpan(0, _contactCount),
        Points = _contactPoints.AsSpan(0, _contactCount),
        PointM = _pointM.AsSpan(0, _contactCount * 2),
        SeparationM = _separationM.AsSpan(0, _contactCount * 2),
    };

    /// <summary>The gap the narrow phase still gives a pair a manifold at — the speculative distance it was laid with.</summary>
    public float SpeculativeM => _config.SolverSpeculativeM;
}
