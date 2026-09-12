using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Config;
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
/// the whole world, then the pavement — every piece of the town grown by a walk — then the water and the
/// decks, then the carriageway at its own size, then the paint. It is one list of shapes read at four
/// sizes, since the pavement and the carriageway are each laid twice for the line between them. A union is
/// stated by drawing its pieces over one another, so nothing here trims a shape against its neighbour and
/// no piece knows what is beside it. There is no depth buffer and nothing to sort — one indexed draw in one
/// pass — and the order the triangles are laid in is the whole of the answer.
/// </para>
/// <para>
/// <b>It is <c>GroundShapes.At</c>'s order, forwards.</b> That method walks this list from the end and
/// takes the first shape that covers the point, so the picture and the answer are one list read in two
/// directions and the question of whether they agree cannot be asked (TER-7). A shape added to one is
/// added to the other, at the same place in the order.
/// </para>
/// <para>
/// <b>A rim, a kerb line and an edge line are what a layer leaves of the one under it.</b> Each layer is
/// laid twice, a line's width apart — the outer pass in the shade the line is to be, the inner in the
/// surface's own — so what survives is a stroke on the union's outer boundary and nothing where two of its
/// pieces meet. It is the same trick the shore is drawn by, and it is why no line here is a shape.
/// <b>Which of the two passes is the surface's own size is the line's to say</b>: an edge shade is struck
/// inside what it rims, so the pavement's outer pass is the band's true width; a kerb line is struck
/// outside (TER-3d), so the carriageway's inner pass is the lane's.
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
        var walkM = paving.WalkM;
        var edgeM = config.Road.EdgeLineWidthM;
        var kerbM = config.Road.PaintLineWidthM;

        // An edge is the surface darkened and paint is the surface brightened. Two measurements, not one
        // relation: the inverse of the edge shade is 1.72 and lays a dash two and a half times too dark.
        // Nothing else in the town is drawn in a colour of its own.
        var edge = Shade(0.58f, 0.58f, 0.62f);
        var paint = Shade(2.6f, 2.6f, 2.5f);

        mesh.Rect(Vector2.Zero, plan.WorldSizeM, Surface.Grass, Plain, periods);

        // <b>Whether anything off the kerb is drawn at all</b> (<c>RoadFigures.LinesOffTheKerbLaid</c>):
        // temporary, and the whole of what it takes to leave a town its driven ground and nothing else.
        // Every layer it holds back is one distance off one boundary, which is why there is one switch.
        var offTheKerb = config.Road.LinesOffTheKerbLaid;

        // <b>The pavement: the ground within a walk of the kerb</b> (TER-3c.3) — one region of the town's
        // own boundary (<see cref="Region"/>), which is the same distance off the same line the answer
        // compares against and the walking lane is laid down the middle of.
        //
        // <b>Twice, and the whole layer each time</b>: at full size in the edge shade, then a line's width
        // smaller in the surface's own, so what survives is a rim on the region's boundary and nothing
        // inside it.
        var rings = GroundRings.Of(paving, config);
        if (offTheKerb)
        {
            Region(mesh, rings, walkM, Surface.Pavement, edge, periods);
            Region(mesh, rings, walkM - edgeM, Surface.Pavement, Plain, periods);
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

        // A deck is drawn out to its own half-width, with an edge line laid the way the pavement's is on
        // land: the piece at full size in the edge shade, then a line's width smaller in its own.
        // <b>And nothing but the deck</b> — the pavement that used to be carried across one at the width it
        // has on land was the last line beside a road struck by arithmetic of its own (TER-3c.3), so it is
        // gone and the margin outside the carriageway is deck all the way out.
        for (var bridge = 0; bridge < plan.Bridges.Count; bridge++)
        {
            var road = plan.Bridges.Road[bridge];
            if (road < 0) continue;

            var span = plan.Roads.SegmentsOf(road);
            var deckHalfM = plan.Bridges.DeckWidthM[bridge] * 0.5f;
            mesh.Ribbon(span, deckHalfM, Surface.Deck, edge, periods);
            mesh.Ribbon(span, deckHalfM - edgeM, Surface.Deck, Plain, periods);
        }

        // <b>The kerb line, struck outside the carriageway</b> (TER-3d): a stroke on the tarmac's own offset
        // curve, which the tarmac at its own size is then drawn back over. Struck inside — the way an edge
        // shade is struck inside the surface it rims — the line takes its own width off the lane it marks,
        // and every lane measured off the picture comes out short of the figure the rest of the build
        // quotes.
        if (offTheKerb) Region(mesh, rings, kerbM, Surface.Tarmac, paint, periods);

        // <b>Whether the carriageway's own surface is drawn at all</b>
        // (<c>RoadFigures.CarriagewayDrawn</c>): temporary, and a drawing switch rather than a laying one —
        // the answer goes on saying the ground is driven over, because every car on it is held up by that.
        var carriageway = config.Road.CarriagewayDrawn;

        // <b>Between the stroke and the carriageway, the tarmac that is not a road</b>: a slab. It is where
        // the answer puts it (<c>GroundShapes.At</c>).
        if (carriageway)
        {
            for (var slab = 0; slab < plan.PavedAreas.Count; slab++)
            {
                mesh.Rect(
                    plan.PavedAreas.MinM[slab], plan.PavedAreas.SizeM[slab], Surface.Tarmac, Plain, periods);
            }
        }

        // And the driven ground at its own size, last of the ground: the region the boundary bounds, kerb
        // to kerb. <b>A junction is the union of the movements that cross in it</b> (TER-5) and has no shape
        // of its own to draw; a car park is a union of the ways that reach into it (<c>BayLines</c>) and has
        // none either — and neither has a road, a bridge or a dead end, because none of them is the edge of
        // anything: the boundary is. Being last is what leaves the kerb line as the line's width the layer
        // before it kept.
        if (carriageway) Region(mesh, rings, 0f, Surface.Tarmac, Plain, periods);

        // And what the blocks the town encloses take back, outwards from their own kerbs
        // (<see cref="Encloses"/>). With nothing struck off the kerb there is one of them and it is the
        // grass, which is what a block is once the lines beside its roads are held back — and with no
        // carriageway drawn there is nothing for a block to take back at all, the grass already being what
        // was laid there.
        if (offTheKerb)
        {
            Encloses(mesh, rings, 0f, Surface.Tarmac, paint, periods);
            Encloses(mesh, rings, kerbM, Surface.Pavement, Plain, periods);
            Encloses(mesh, rings, walkM - edgeM, Surface.Pavement, edge, periods);
            Encloses(mesh, rings, walkM, Surface.Grass, Plain, periods);
        }
        else if (carriageway)
        {
            Encloses(mesh, rings, 0f, Surface.Grass, Plain, periods);
        }

        // <b>The paint, which is one layer and so one switch</b> (<c>RoadFigures.PaintDrawn</c>): temporary,
        // and the last of the stage's three. The mark still says what it said — a crossing is a stretch of
        // the road it is painted across (TER-6) whether or not the stripes are drawn — so this hides the
        // stripes and nothing else.
        mesh.FirstMarkVertex = mesh._vertices.Count;
        mesh._welding = false;
        mesh._welds = [];
        if (!config.Road.PaintDrawn) return mesh;

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
    /// <b>The ground within <paramref name="outM"/> of the kerb, laid as the one shape it is</b>
    /// (<see cref="GroundRings"/>) — the town's boundary moved that far and filled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A region and not a union of pieces.</b> The ground the town is driven over has a boundary and the
    /// shell computes it, so every layer here is one shape per ring of it rather than a heap of roads,
    /// movements, wedges and car parks laid over one another until their outlines happen to agree. What that
    /// buys is the thing overdraw could never state: the edge of each layer is a line the build can hand to
    /// anybody, and the band between two of them is exactly the difference between the distances that struck
    /// them.
    /// </para>
    /// <para>
    /// <b>A ring that encloses ground is filled, and a ring that encloses a block is the hole in it</b> — and
    /// the hole is painted in the layer beneath, which is what leaves each layer a line's width of the one
    /// under it round the inside of a block exactly as over-painting leaves it round the outside of the town.
    /// Which a ring is, is its own hand: a ring walks with the ground on its right throughout, so the sign of
    /// the area it encloses says which side of it the town is on.
    /// </para>
    /// <para>
    /// <b>The order is still the whole of the answer</b> (TER-7b): the layers are laid outermost distance
    /// first and the piece laid last is the piece that shows, so <c>GroundShapes.At</c> comparing the same
    /// distance against the same table walks this list in the other direction.
    /// </para>
    /// </remarks>
    static void Region(
        GroundMesh mesh, GroundRings rings, float outM, Surface surface, Vector3 tint, float[] periods)
    {
        foreach (var ring in rings.At(outM))
        {
            if (Encloses(ring)) mesh.Ring(ring, surface, tint, periods);
        }
    }

    /// <summary>
    /// <b>What a block takes back</b>: the ground the town encloses rather than lays, painted in the surface
    /// that begins at <paramref name="outM"/>.
    /// </summary>
    /// <remarks>
    /// <b>Laid after every region and in the other order.</b> Outside the town the layers nest inwards —
    /// each distance encloses less than the one before it — and inside a block they nest the other way,
    /// since a ring nearer the kerb leaves <em>more</em> of the block beyond it. Painted in the regions'
    /// own order, the innermost distance's block covered the whole of it and every town came back with its
    /// blocks paved kerb to kerb. Run in increasing distance after them, what a block shows is the same
    /// sequence read outwards from its own kerb: the line, the walk, its rim, and then the grass.
    /// </remarks>
    static void Encloses(
        GroundMesh mesh, GroundRings rings, float outM, Surface surface, Vector3 tint, float[] periods)
    {
        foreach (var ring in rings.At(outM))
        {
            if (!Encloses(ring)) mesh.Ring(ring, surface, tint, periods);
        }
    }

    /// <summary>
    /// Whether a ring encloses the town's ground rather than a block of it: twice the area it covers,
    /// signed, is positive where it is walked with what it encloses on its right.
    /// </summary>
    static bool Encloses(ReadOnlySpan<ArcSeg> ring)
    {
        var twiceM = 0f;
        foreach (var arc in ring) twiceM += (arc.StartM.X * arc.EndM.Y) - (arc.EndM.X * arc.StartM.Y);

        return twiceM > 0f;
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
