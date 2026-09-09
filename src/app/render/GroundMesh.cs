using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.App.Render;

/// <summary>Which of the five surfaces a triangle wears, or <see cref="Paint"/> for a flat colour.</summary>
internal enum Surface : uint
{
    Grass = 0,
    Tarmac = 1,
    Pavement = 2,
    Deck = 3,
    Water = 4,

    /// <summary>Not a surface: the tint alone, for anything that is not ground.</summary>
    Paint = 255,
}

/// <summary>
/// One corner of the ground. The texture coordinate is computed here, at load, from the world
/// position alone — which is what anchors every surface's texture to the world origin rather than to
/// the shape being painted, and what makes the triangulation invisible (TER-7's drawn half).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct GroundVertex(Vector2 PositionM, Vector2 Uv, Vector3 Tint, Surface Surface);

/// <summary>
/// The town's standing ground, triangulated once at load from the plan's <b>shapes</b> — road ribbons
/// bent along their own splines, the lines a car is turned through a box on, corner fillets, decks, slabs,
/// lots and water outlines. Never from the cell grid: no arrangement of metre squares is a kerb running
/// at 40°.
/// </summary>
/// <remarks>
/// <para>
/// <b>The ground is a stack of layers and each is the union of the shapes in it</b> (TER-7b): grass over
/// the whole world, then every piece of the town grown by a walk, then the water and the decks, then every
/// piece at its own size, then the paint. A union is stated by drawing its pieces over one another, so
/// nothing here trims a shape against its neighbour and no piece knows what is beside it. There is no
/// depth buffer and nothing to sort — one indexed draw in one pass — and the order the triangles are laid
/// in is the whole of the answer.
/// </para>
/// <para>
/// <b>It is <c>GroundShapes.At</c>'s order, forwards.</b> That method walks this list from the end and
/// takes the first shape that covers the point, so the picture and the answer are one list read in two
/// directions and the question of whether they agree cannot be asked (TER-7). A shape added to one is
/// added to the other, at the same place in the order.
/// </para>
/// <para>
/// <b>A rim, a kerb line and an edge line are what a layer leaves of the one under it.</b> Each layer is
/// laid twice — once at full size in the shade the line is to be, then again a line's width smaller in the
/// surface's own — so what survives is a stroke on the union's own outer boundary and nowhere two of its
/// pieces meet. It is the same trick the shore is drawn by, and it is why no line here is a shape.
/// </para>
/// <para>
/// What is <b>not</b> here is anything that is not ground: buildings, props, agents and their sprites
/// are the second pipeline.
/// </para>
/// </remarks>
internal sealed partial class GroundMesh
{
    /// <summary>How far a drawn chord is allowed to bow off the arc it stands for.</summary>
    const float ChordSagM = 0.02f;

    /// <summary>
    /// How far apart two corners may stand and still be one point: a millimetre, which is the rounding the
    /// town's own outline is cut at (<see cref="Kerbs.RoundingM"/>) and well under anything a frame shows.
    /// </summary>
    const float OnePointM = Kerbs.RoundingM;

    /// <summary>White: a surface drawn as itself.</summary>
    static readonly Vector3 Plain = Vector3.One;

    readonly List<GroundVertex> _vertices = [];
    readonly List<uint> _indices = [];

    /// <summary>
    /// Which corner already stands at a place, wearing a surface and a shade (<see cref="Vertex"/>), so a
    /// shape laid at a size and the same shape laid a line's width inside it do not each carry their own
    /// copy of the stations they agree on. It is laying scratch and is dropped once the ground is laid.
    /// </summary>
    Dictionary<(int X, int Y, Surface Surface, int R, int G, int B), int> _welds = [];

    /// <summary>
    /// Whether a corner asked for is shared with whatever already stands at it. <b>The ground is welded and
    /// the marks are not</b>: a mark is four corners read back as four corners.
    /// </summary>
    bool _welding = true;

    GroundMesh()
    {
    }

    public ReadOnlySpan<GroundVertex> Vertices => CollectionsMarshal.AsSpan(_vertices);

    public ReadOnlySpan<uint> Indices => CollectionsMarshal.AsSpan(_indices);

    /// <summary>
    /// Where the marks start, as an index into <see cref="Vertices"/>: everything from here on is a
    /// dash, a bar, a zebra's stripe or a bay stroke, four corners at a time.
    /// </summary>
    /// <remarks>
    /// <b>The kerb line is not among them.</b> It is what the carriageway leaves of the stroke struck
    /// outside it rather than a quad of its own, so the tint alone does not tell a mark from the ground it
    /// is on and anything asking what was <em>painted</em> asks this instead.
    /// </remarks>
    public int FirstMarkVertex { get; private set; }

    /// <summary>
    /// No ground at all: one degenerate triangle, which is what the start menu is drawn over.
    /// </summary>
    /// <remarks>
    /// <b>GEN-1b — nothing is built until a map is picked</b>, and a menu still has to be drawn by
    /// something. A renderer whose ground pass covers no pixels is the honest shape of that: the
    /// menu's own quads are the third pipeline, exactly as they are over a town, and the picture says
    /// plainly that no town exists.
    /// </remarks>
    public static GroundMesh Nothing()
    {
        var mesh = new GroundMesh();
        for (var corner = 0; corner < 3; corner++)
        {
            mesh._vertices.Add(new GroundVertex(Vector2.Zero, Vector2.Zero, Vector3.Zero, Surface.Grass));
            mesh._indices.Add((uint)corner);
        }

        return mesh;
    }

    public static GroundMesh Build(CityPlan plan, SimConfig config)
    {
        var mesh = new GroundMesh();
        var periods = Periods(config);

        // <b>The pavement is a step and not a shape this pass works out for itself</b> (TER-3c): the walk
        // and the lines a car is driven on are the town's own (<see cref="Paving"/>). Derived again here,
        // the picture and the answer are two readings that have to be kept in step by whoever remembers.
        var paving = plan.Paving(config);
        var lanes = paving.Lanes;
        var walkM = paving.WalkM;
        var edgeM = config.Road.EdgeLineWidthM;
        var kerbM = config.Road.PaintLineWidthM;

        // An edge is the surface darkened and paint is the surface brightened. Two measurements, not one
        // relation: the inverse of the edge shade is 1.72 and lays a dash two and a half times too dark.
        // Nothing else in the town is drawn in a colour of its own.
        var edge = Shade(0.58f, 0.58f, 0.62f);
        var paint = Shade(2.6f, 2.6f, 2.5f);

        mesh.Rect(Vector2.Zero, plan.WorldSizeM, Surface.Grass, Plain, periods);

        // <b>The pavement: every piece of the town grown by a walk</b> (TER-3c.3). Not a band worked out
        // from the tarmac's outline — the outline is what a union of these pieces <em>has</em>, and a union
        // is stated by laying them over one another. What a walk out from every piece covers and what the
        // answer calls pavement are one construction, since the answer asks these same shapes at this same
        // size (<c>GroundShapes.At</c>, <c>GroundShapes.Grown</c>).
        //
        // <b>Twice, and the whole layer each time</b>: at full size in the edge shade, then a line's width
        // smaller in the surface's own. Since every fill follows every rim, what survives is a rim on the
        // union's own outer boundary and nothing where two of its pieces meet.
        foreach (var insetM in (ReadOnlySpan<float>)[0f, edgeM])
        {
            var tint = insetM == 0f ? edge : Plain;
            var outM = walkM - insetM;

            // A car park turns a right angle of its own, so its wrap turns on the walk — which is the
            // radius the answer turns it on too (<c>GroundShapes.InRoundedRect</c>).
            //
            // <b>A slab has no wrap</b> (<c>Kerbs.Lay</c>, <c>WalkedPast.Never</c>): it offers the town's
            // outline no line to walk, so nothing pavements round one and nothing here draws it.
            for (var lot = 0; lot < plan.ParkingLots.Count; lot++)
            {
                mesh.RoundedRect(plan.ParkingLots.CentreM[lot], plan.ParkingLots.Axis[lot],
                    plan.ParkingLots.HalfExtentM[lot] + new Vector2(outM), outM, Surface.Pavement, tint,
                    periods);
            }

            Grown(mesh, plan, lanes, outM, Surface.Pavement, tint, periods);
        }

        // The water and the shore it is set in, largest ring first (GEN-2c). Each fill leaves a line's width
        // of the one under it showing, which is the same trick every other line here is drawn by: what
        // survives is one line where the shore meets the grass and another where it meets the water. <b>Each
        // takes the colour of the ground it meets</b> — green against the grass and blue against the water —
        // and each is drawn darker than that ground, so the edge reads as the shore's own shadow on it
        // rather than as a highlight laid over it.
        Water(mesh, plan.Water.Shore, Surface.Pavement, Shade(0.3f, 0.48f, 0.22f), periods);
        Water(mesh, plan.Water.ShoreEdge, Surface.Pavement, Plain, periods);
        Water(mesh, plan.Water.WaterEdge, Surface.Pavement, Shade(0.08f, 0.2f, 0.3f), periods);
        Water(mesh, plan.Water.Outline, Surface.Water, Plain, periods);

        // A deck is drawn like the section TER-3b.1 draws: the deck itself out to its own half-width,
        // then the town's pavement carried across it at the width it has on land, which leaves the
        // margin — the ground a parapet stands on — as the strip of deck outside the walk. Both carry
        // an edge line, and each is laid the way the pavement's is on land: the piece at full size in
        // the edge shade, then a line's width smaller in its own.
        for (var bridge = 0; bridge < plan.Bridges.Count; bridge++)
        {
            var road = plan.Bridges.Road[bridge];
            if (road < 0) continue;

            var span = plan.Roads.SegmentsOf(road);
            var deckPavementM = plan.Bridges.PavementWidthM[bridge] > 0f ? plan.Bridges.PavementWidthM[bridge] : walkM;
            var deckHalfM = plan.Bridges.DeckWidthM[bridge] * 0.5f;
            var walkHalfM = (plan.Roads.WidthM[road] * 0.5f) + deckPavementM;
            mesh.Ribbon(span, deckHalfM, Surface.Deck, edge, periods);
            mesh.Ribbon(span, deckHalfM - edgeM, Surface.Deck, Plain, periods);
            mesh.Ribbon(span, walkHalfM, Surface.Pavement, edge, periods);
            mesh.Ribbon(span, walkHalfM - edgeM, Surface.Pavement, Plain, periods);
        }

        // <b>The kerb line, struck outside the carriageway</b> (TER-3d): a stroke on the tarmac's own offset
        // curve, which the tarmac at its own size is then drawn back over. Struck inside — the way an edge
        // shade is struck inside the surface it rims — the line takes its own width off the lane it marks,
        // and every lane measured off the picture comes out short of the figure the rest of the build
        // quotes.
        Grown(mesh, plan, lanes, kerbM, Surface.Tarmac, paint, periods);

        // <b>Between the stroke and the carriageway, the tarmac that is not a road</b>: a slab, and a car
        // park at its own size. It is where the answer puts them (<c>GroundShapes.At</c>) and it is what
        // breaks the kerb line over a car park's mouth — a line painted there is one every car entering the
        // lot drives across, and what takes it away is the lot's own tarmac rather than a stretch of kerb
        // worked out and left unstruck.
        for (var slab = 0; slab < plan.PavedAreas.Count; slab++)
        {
            mesh.Rect(plan.PavedAreas.MinM[slab], plan.PavedAreas.SizeM[slab], Surface.Tarmac, Plain, periods);
        }

        for (var lot = 0; lot < plan.ParkingLots.Count; lot++)
        {
            mesh.OrientedRect(plan.ParkingLots.CentreM[lot], plan.ParkingLots.Axis[lot],
                plan.ParkingLots.HalfExtentM[lot], Surface.Tarmac, Plain, periods);
        }

        // And the carriageway at its own size, last of the ground: every road, every line a car is turned
        // through a box on, and the wedge its kerbs turn on. <b>A junction is the union of the movements
        // that cross in it</b> (TER-5) and has no shape of its own to draw.
        Grown(mesh, plan, lanes, 0f, Surface.Tarmac, Plain, periods);

        mesh.FirstMarkVertex = mesh._vertices.Count;
        mesh._welding = false;
        mesh._welds = [];
        mesh.LaneDashes(plan, config, paint, periods);

        // A zebra spans the whole carriageway kerb to kerb — the width of the road it is painted on and
        // never a span of its own (TER-6) — where a stop bar covers the approaching lane only.
        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            mesh.Zebra(plan.Crosswalks.CentreM[crossing], plan.Crosswalks.Axis[crossing],
                plan.Crosswalks.DepthM[crossing], plan.CrossingSpanM(crossing), config, paint, periods);
        }

        // The bars that were painted, in the arm's own frame: the plan carries where each one landed,
        // so nothing here re-derives a coordinate somebody else owns.
        for (var bar = 0; bar < plan.StopLines.Count; bar++)
        {
            var approach = plan.StopLines.Approach[bar];
            mesh.OrientedRect(plan.StopLines.CentreM[bar], new Vector2(-approach.Y, approach.X),
                new Vector2(plan.StopLines.SpanM[bar] * 0.5f, plan.StopLines.ThicknessM[bar] * 0.5f),
                Surface.Tarmac, paint, periods);
        }

        mesh.BayStrokes(plan, config, paint, periods);

        return mesh;
    }

    /// <summary>
    /// <b>The road network at <paramref name="outM"/> beyond its own size</b> — every road, every line a
    /// car is turned through a box on, and every wedge their kerbs turn on. One call is one layer of the
    /// stack, and the three the ground has are this at a walk, at a line's width and at nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The union is the drawing and not a shape.</b> Two pieces that overlap overlap; the boundary the
    /// layer has is whatever their outlines leave, and no piece is cut against another (TER-7b). This is
    /// what makes a junction, a car park's mouth and a bridge cost nothing that a straight does not.
    /// </para>
    /// <para>
    /// <b>A corner grows by moving its arc in and its apex out</b> (<see cref="Fillet"/>), which is one
    /// fillet of a smaller radius about the same centre — the construction <c>GroundShapes.Grown</c> uses
    /// to answer the same ground, down to dropping a corner tighter than the growth, where there is no
    /// reading the arc in that far and what stands round it is the two arms' own bands.
    /// </para>
    /// <para>
    /// <b>A road's ends are square</b>, where the answer swings a quarter circle round each of its two far
    /// corners (<c>GroundShapes.OffTheBandM</c>). It costs a bite out of the walk at a dead end and nowhere
    /// else, since every other end of every road stands inside a junction.
    /// </para>
    /// </remarks>
    static void Grown(
        GroundMesh mesh, CityPlan plan, LaneLines lanes, float outM, Surface surface, Vector3 tint,
        float[] periods)
    {
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var arcs = plan.Roads.SegmentsOf(road);
            var halfM = plan.Roads.WidthM[road] * 0.5f;
            mesh.Ribbon(arcs, halfM + outM, surface, tint, periods);
            if (outM <= 0f) continue;

            // <b>A band's own end is square and what grows off it is round</b> (TER-7a,
            // <c>GroundShapes.OffTheBandM</c>): the ground within a walk of a road that stops is within a
            // walk of its last cross-section, which is that segment swung round. The ribbon has no way to
            // say that, so each end carries the offset of its own end segment — a rectangle a growth deep
            // whose corners turn about the road's two end corners on the growth itself. At every end that
            // stands inside a junction it is under the junction, and at a dead end it is the head.
            var lengthM = Spline.TotalLengthM(arcs);
            foreach (var atM in (ReadOnlySpan<float>)[0f, lengthM])
            {
                var end = Spline.SampleAt(arcs, atM);
                mesh.RoundedRect(
                    end.PositionM, end.Direction, new Vector2(outM, halfM + outM), outM, surface, tint,
                    periods);
            }
        }

        for (var connector = 0; connector < lanes.ConnectorCount; connector++)
        {
            var line = lanes.ArcsOfConnector(connector);
            if (line.Length == 0) continue;

            mesh.Ribbon(line, (lanes.ConnectorWidthM(connector) * 0.5f) + outM, surface, tint, periods);
        }

        var kerbs = plan.JunctionCorners;
        for (var corner = 0; corner < kerbs.Count; corner++)
        {
            mesh.Fillet(kerbs.CornerM[corner], kerbs.ArcCentreM[corner], kerbs.RadiusM[corner],
                kerbs.TangentAM[corner], kerbs.TangentBM[corner], -outM, surface, tint, periods);
        }
    }

    /// <summary>Every ring of one of the water's own sets, laid as the one shape it is.</summary>
    static void Water(
        GroundMesh mesh, CityPlan.RingArrays rings, Surface surface, Vector3 tint, float[] periods)
    {
        for (var ring = 0; ring < rings.Count; ring++) mesh.Polygon(rings.RingOf(ring), surface, tint, periods);
    }

    /// <summary>The period each surface's texture repeats over, in metres, from the figures config carries.</summary>
    public static float[] Periods(SimConfig config) =>
    [
        config.View.GroundPeriodGrassM,
        config.View.GroundPeriodTarmacM,
        config.View.GroundPeriodPavementM,
        config.View.GroundPeriodDeckM,
        config.View.GroundPeriodWaterM,
    ];

    /// <summary>
    /// A multiplier and not a colour, which is why it is three floats and not four bytes: paint is
    /// the surface <em>brighter</em>, so the factor is above one and an eight-bit tint could only
    /// have clamped it back to the ground it was meant to stand out from.
    /// </summary>
    static Vector3 Shade(float r, float g, float b) => new(r, g, b);
}
