using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The pavement, laid once as the pieces it is made of</b> (TER-3c): a band along every carriageway, a
/// ring round every junction, a wrap round every lot, and the fillet that turns each inner corner where two
/// of those run into one another (TER-3c.4).
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a step and not a derivation two readers each do for themselves.</b> The shape of the pavement
/// used to be written down twice — once as the draw calls that lay it (<c>GroundMesh.Build</c>) and once as
/// the coverage tests that answer what the ground is at a point (<see cref="GroundShapes"/>) — in two
/// slices, at two tiers, kept in step by nobody but whoever remembered. That is the figure that exists in
/// two places and eventually disagrees with itself: a band widened in the picture and not in the answer is
/// a walker refused ground it can see it is standing on. Laid here, the two read one list.
/// </para>
/// <para>
/// <b>Every piece is offset from what it runs beside, at the walk's own width</b> (TER-3c.3), so the
/// pavement keeps its width round whatever curve the thing it wraps takes. Nothing here is a shape in its
/// own right: a ribbon is a road's own arcs, a ring is a junction's own disc and a wrap is a lot's own
/// rectangle, each grown by the one figure.
/// </para>
/// <para>
/// <b>The pieces are a union and the order among them says nothing.</b> Where two overlap the ground is
/// pavement once; where they leave a re-entrant spike between them a corner turns it (TER-3c.4). What
/// order they are <em>drawn</em> in is the mesh's business, and it draws each of them twice for its rim.
/// </para>
/// </remarks>
internal sealed class Paving
{
    Paving(
        float walkM, float[] ribbonHalfM, float[] ringRadiusM, Vector2[] wrapHalfM, float wrapCornerM,
        IReadOnlyList<PavementCorner> corners, GroundPieces pieces)
    {
        WalkM = walkM;
        RibbonHalfM = ribbonHalfM;
        RingRadiusM = ringRadiusM;
        WrapHalfM = wrapHalfM;
        WrapCornerM = wrapCornerM;
        Corners = corners;
        Of = pieces;
    }

    /// <summary>The shapes the pavement was laid off, for a reader that wants the road a ribbon belongs to.</summary>
    public GroundPieces Of { get; }

    /// <summary>
    /// How wide the band is. <b>The map's own figure where it has one</b>, and the town's where it does not,
    /// so a map laid without a pavement of its own is walked at the same width it is drawn.
    /// </summary>
    public float WalkM { get; }

    /// <summary>How far each road's band reaches from its own centreline: half the carriageway and a walk.</summary>
    public float[] RibbonHalfM { get; }

    /// <summary>And how far each junction's ring reaches from its centre.</summary>
    public float[] RingRadiusM { get; }

    /// <summary>Each lot's wrap, as the half extent it reaches to.</summary>
    public Vector2[] WrapHalfM { get; }

    /// <summary>
    /// The radius a wrap turns its own right angles on — <b>half the walk</b> (TER-3c.3): rounded on the
    /// full width the band reads pinched, and square takes a bite of verge.
    /// </summary>
    public float WrapCornerM { get; }

    /// <summary>
    /// The inner corners, solved against the finished ground rather than enumerated per kind of neighbour
    /// (TER-3c.4). <b>Nothing reads a list of these from a map</b>: a pair the generator has never put
    /// together before is rounded the first time it appears.
    /// </summary>
    public IReadOnlyList<PavementCorner> Corners { get; }

    public static Paving Lay(GroundPieces pieces, SimConfig config)
    {
        var walkM = pieces.PavementWidthM > 0f ? pieces.PavementWidthM : config.PavementWidthM;

        var ribbonHalfM = new float[pieces.Roads.Count];
        for (var road = 0; road < ribbonHalfM.Length; road++)
        {
            ribbonHalfM[road] = (pieces.Roads.WidthM[road] * 0.5f) + walkM;
        }

        var ringRadiusM = new float[pieces.Junctions.Count];
        for (var junction = 0; junction < ringRadiusM.Length; junction++)
        {
            ringRadiusM[junction] = pieces.Junctions.RadiusM[junction] + walkM;
        }

        var wrapHalfM = new Vector2[pieces.ParkingLots.Count];
        for (var lot = 0; lot < wrapHalfM.Length; lot++)
        {
            wrapHalfM[lot] = pieces.ParkingLots.HalfExtentM[lot] + new Vector2(walkM);
        }

        return new Paving(
            walkM, ribbonHalfM, ringRadiusM, wrapHalfM, config.PavementCornerRadiusM,
            PavementCorners.Solve(pieces, config), pieces);
    }
}
