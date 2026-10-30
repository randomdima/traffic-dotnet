using System.Numerics;

namespace TrafficSimulation.Multiplayer.Wire;

/// <summary>
/// <b>How wide every field is on the wire</b>, derived from the quanta and the town and never authored: a server and a
/// player lay it from the same welcome (<see cref="Welcome"/>), so both read a field the width the other wrote it.
/// </summary>
/// <remarks>
/// <b>A position is clamped to the town's own square</b>: a body is never sent past the ground it is laid on, and one
/// pushed over the edge is drawn at it. <b>A small angle is bound to a right angle either way</b> — a road wheel's lock
/// and a body's lean are both well inside it — and one past it is sent at it. <b>A heading and a small angle have
/// quanta of their own</b>: a heading turns a whole body on the screen, a lean or a road wheel only a sliver of it.
/// </remarks>
internal readonly struct WireShape
{
    const float RightAngleDeg = 90f;

    public WireShape(
        float positionQuantumM, float angleQuantumDeg, float smallAngleQuantumDeg, Vector2 worldSizeM, MoverIds movers,
        float smallMoveM, int seats)
    {
        PositionQuantumM = positionQuantumM;
        AngleQuantumRad = angleQuantumDeg * MathF.PI / 180f;
        SmallAngleQuantumRad = smallAngleQuantumDeg * MathF.PI / 180f;
        Movers = movers;
        MostPosition = (int)MathF.Ceiling(MathF.Max(worldSizeM.X, worldSizeM.Y) / positionQuantumM);
        PositionBits = BitsFor((uint)MostPosition);
        HeadingSteps = (int)MathF.Ceiling(360f / angleQuantumDeg);
        HeadingBits = BitsFor((uint)HeadingSteps - 1);
        MostSmallAngle = (int)MathF.Ceiling(RightAngleDeg / smallAngleQuantumDeg);
        SmallAngleBits = BitsFor(Zigzag.Encode(MostSmallAngle));
        IdBits = BitsFor((uint)Math.Max(1, movers.Count));
        MostSmallMove = (int)MathF.Ceiling(smallMoveM / positionQuantumM);
        SmallMoveBits = BitsFor(Zigzag.Encode(MostSmallMove));
        DriverBits = BitsFor((uint)Math.Max(0, seats));
    }

    /// <summary>Enough for a mover's driver: a seat plus one, or nought (<see cref="MoverState.Driver"/>).</summary>
    public int DriverBits { get; }

    public float PositionQuantumM { get; }

    public float AngleQuantumRad { get; }

    /// <summary>The step a road wheel's angle and a body's lean are sent in.</summary>
    public float SmallAngleQuantumRad { get; }

    public MoverIds Movers { get; }

    public int MostPosition { get; }

    public int PositionBits { get; }

    public int HeadingSteps { get; }

    public int HeadingBits { get; }

    public int MostSmallAngle { get; }

    public int SmallAngleBits { get; }

    /// <summary>Enough for any mover's number and for a count of all of them.</summary>
    public int IdBits { get; }

    /// <summary>The furthest a mover may have gone since its baseline and be sent as a move rather than a place.</summary>
    public int MostSmallMove { get; }

    public int SmallMoveBits { get; }

    public int ToPosition(float m) => Math.Clamp((int)MathF.Round(m / PositionQuantumM), 0, MostPosition);

    public float FromPosition(int quanta) => quanta * PositionQuantumM;

    public Vector2 FromPosition(int x, int y) => new(FromPosition(x), FromPosition(y));

    public ushort ToHeading(float rad)
    {
        var steps = (int)MathF.Round(rad / AngleQuantumRad) % HeadingSteps;
        return (ushort)(steps < 0 ? steps + HeadingSteps : steps);
    }

    public float FromHeading(int steps) => steps * AngleQuantumRad;

    public short ToSmallAngle(float rad) =>
        (short)Math.Clamp((int)MathF.Round(rad / SmallAngleQuantumRad), -MostSmallAngle, MostSmallAngle);

    public float FromSmallAngle(int quanta) => quanta * SmallAngleQuantumRad;

    public static int BitsFor(uint most) => most == 0 ? 1 : 32 - BitOperations.LeadingZeroCount(most);
}
