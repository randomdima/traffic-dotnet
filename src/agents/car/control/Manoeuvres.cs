using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.Agents.Car.Control;

/// <summary>What a car's manoeuvre at a bay is for (GEN-4f).</summary>
internal enum ManoeuvreKind : byte
{
    None,

    /// <summary>Into a bay off the street.</summary>
    Park,

    /// <summary>Out of a bay onto the street.</summary>
    Leave,
}

/// <summary>
/// <b>How far a manoeuvre has got with the ground it needs</b> (GEN-4f, TER-4c.6): shaped and waiting for its
/// ground, laid for the first time and to be kept or withdrawn, or begun and held until it is driven.
/// </summary>
internal enum ManoeuvreStage : byte
{
    None,
    Shaped,
    Asked,
    Begun,
}

/// <summary>
/// <b>Every car's manoeuvre at a bay</b>, one array per field (GEN-4f): the shape it laid for itself off its own
/// circle, which piece of it it is on, how far it has got with its ground, and what it is for.
/// </summary>
/// <remarks>
/// <b>The driver's own, and read by nobody else</b> (TER-4c.5): what the town sees of it is the ground it covers,
/// laid as a pass's is. The shape is written once, where the car decides on it, and driven a piece at a time as
/// the car's line.
/// </remarks>
internal sealed class Manoeuvres
{
    readonly ArcSeg[] _arcs;

    public Manoeuvres(int cars)
    {
        _arcs = new ArcSeg[cars * BayManoeuvre.MostArcs];
        Shape = new BayManoeuvre.Shape[cars];
        Kind = new ManoeuvreKind[cars];
        Stage = new ManoeuvreStage[cars];
        Bay = new int[cars];
        Lane = new int[cars];
        Piece = new int[cars];
        StreetM = new float[cars];
        Array.Fill(Bay, NoBay);
        Array.Fill(Lane, NoLane);
    }

    public const int NoBay = -1;

    public const int NoLane = -1;

    public BayManoeuvre.Shape[] Shape { get; }

    public ManoeuvreKind[] Kind { get; }

    public ManoeuvreStage[] Stage { get; }

    /// <summary>The bay the manoeuvre is into or out of.</summary>
    public int[] Bay { get; }

    /// <summary>
    /// The street lane it is made from, for one into a bay, or lands on, for one out of it — the lane a line is
    /// taken up on again once it is driven.
    /// </summary>
    public int[] Lane { get; }

    /// <summary>Which of the shape's pieces is the car's line.</summary>
    public int[] Piece { get; }

    /// <summary>
    /// <b>How much street the shape takes</b>, in metres of the street's own ways summed — what it was chosen by,
    /// the least of the shapes the car could make from where it stood.
    /// </summary>
    public float[] StreetM { get; }

    /// <summary>Room for a shape's arcs, written by whoever lays one for this car.</summary>
    public Span<ArcSeg> RoomOf(int car) => _arcs.AsSpan(car * BayManoeuvre.MostArcs, BayManoeuvre.MostArcs);

    /// <summary>One piece of the shape in hand, in the direction the axle travels along it.</summary>
    public ReadOnlySpan<ArcSeg> PieceOf(int car, int piece) =>
        _arcs.AsSpan((car * BayManoeuvre.MostArcs) + Shape[car].FirstArcOf(piece), Shape[car].ArcsOf(piece));

    /// <summary>Whether this car has a manoeuvre in hand at all.</summary>
    public bool Any(int car) => Kind[car] != ManoeuvreKind.None;

    /// <summary>Whether it has begun it, and so holds its ground until it is driven.</summary>
    public bool IsBegun(int car) => Stage[car] == ManoeuvreStage.Begun;

    /// <summary>Whether the piece it is on is its last.</summary>
    public bool OnTheLastPiece(int car) => Piece[car] >= Shape[car].Pieces - 1;

    /// <summary>A shape laid into <see cref="RoomOf"/>, taken as the one the car means to drive.</summary>
    public void Shaped(int car, ManoeuvreKind kind, int bay, int lane, in BayManoeuvre.Shape shape, float streetM)
    {
        Shape[car] = shape;
        Kind[car] = kind;
        Stage[car] = ManoeuvreStage.Shaped;
        Bay[car] = bay;
        Lane[car] = lane;
        Piece[car] = 0;
        StreetM[car] = streetM;
    }

    public void Clear(int car)
    {
        Shape[car] = BayManoeuvre.Shape.None;
        Kind[car] = ManoeuvreKind.None;
        Stage[car] = ManoeuvreStage.None;
        Bay[car] = NoBay;
        Lane[car] = NoLane;
        Piece[car] = 0;
    }
}
