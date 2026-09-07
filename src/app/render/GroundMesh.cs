using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
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
/// bent along their own splines, junction discs, corner fillets, decks, slabs, lots and water
/// outlines. Never from the cell grid: no arrangement of metre squares is a kerb running at 40°.
/// </summary>
/// <remarks>
/// <para>
/// The order the triangles are laid in is the order they are painted in: grass over the whole world;
/// the pavement, which is every piece of the tarmac grown by a walk and drawn twice for its edge line;
/// the water; the decks; the paved slabs; the lots; the carriageway; the lines through the boxes; the
/// corner fillets. There is no depth buffer and nothing to sort — one indexed draw in one pass, and the
/// pavement band falls out as what is left showing round what is drawn over it, without anything having
/// to know where a kerb is.
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

    /// <summary>White: a surface drawn as itself.</summary>
    static readonly Vector3 Plain = Vector3.One;

    readonly List<GroundVertex> _vertices = [];
    readonly List<uint> _indices = [];

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
    /// <b>The kerb line is paint too</b>, and it is a rim on a ribbon, a disc and a fillet rather than
    /// a quad — so the tint alone no longer tells a mark from the ground it is on, and anything asking
    /// what was <em>painted</em> asks this instead.
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
        // and the lines a car is driven on are the town's own (<see cref="Paving"/>), and what is drawn
        // beside them is the tarmac at the size it is drawn, grown by that one figure — which is the same
        // construction the ground is answered off (<see cref="GroundShapes"/>) and the same one the walking
        // lanes are cut from. Derived again here, the picture and the answer are two readings that have to
        // be kept in step by whoever remembers.
        var paving = plan.Paving(config);
        var lanes = paving.Lanes;
        var walkM = paving.WalkM;
        var edgeM = config.Road.EdgeLineWidthM;

        // An edge is the surface darkened and paint is the surface brightened. Two measurements, not one
        // relation: the inverse of the edge shade is 1.72 and lays a dash two and a half times too dark.
        // Nothing else in the town is drawn in a colour of its own.
        var edge = Shade(0.58f, 0.58f, 0.62f);
        var paint = Shade(2.6f, 2.6f, 2.5f);
        var kerbM = config.Road.PaintLineWidthM;

        mesh.Rect(Vector2.Zero, plan.WorldSizeM, Surface.Grass, Plain, periods);

        // <b>The town out to a walk beyond its own tarmac, as tarmac</b>, piece by piece: growing a union
        // one piece at a time is growing the union, so this one pass covers every square metre the town
        // paves — carriageway, junction, car park, and the pockets the pieces leave between them.
        //
        // The pavement is the band laid over it below, so what is left showing here is exactly the ground
        // inside the kerb (TER-3c.7). Drawn the other way round — the tarmac at its own size, the pavement
        // filling out to a walk — the kerb was the tarmac's own outline, and it stepped and chamfered its
        // way round every mouth where a movement is narrower than the arm it leaves while the shell and the
        // walking lane beside it ran straight past.
        //
        // Twice: once at full size in the pavement's edge shade, then a line's width smaller as tarmac over
        // the top. What survives of the first is a rim on the union's own outer boundary — the shell's
        // shadow on the grass. <b>Drawn off the union and not off the runs the band is drawn off</b>: a run
        // knows whether it reaches the outside of the town only by asking, and asked run by run the shadow
        // came out with a stub up the middle of the pavement wherever the answer was wrong and a gap at the
        // corner wherever two runs gave way to one another.
        foreach (var inset in (ReadOnlySpan<float>)[0f, edgeM])
        {
            var tint = inset == 0f ? edge : Plain;
            var surface = inset == 0f ? Surface.Pavement : Surface.Tarmac;
            mesh.Grown(plan, lanes, walkM - inset, surface, tint, periods);
        }

        // <b>The pavement is the band the walk runs down</b> (TER-3c.3): everything within half a walk of
        // <c>Paving.Walk</c> — the town's outline at half a walk, cut to the runs of it that are really the
        // outside. Both its edges are offsets of that one curve, so the kerb turns a corner the way the
        // shell against the grass does and the way the lane between them does; and a run's end is closed
        // with the half-round the answer measures there (<c>GroundShapes.Paved</c>), which is what fills the
        // wedge where two runs give way to one another.
        //
        // It stops a line's width short of the outside, which is what leaves the shell's shadow standing.
        var halfWalkM = walkM * 0.5f;
        foreach (var run in paving.Walk)
        {
            var outerM = run.Outline ? halfWalkM - edgeM : halfWalkM;
            mesh.Skirt(run.Line, -run.RoadSide * outerM, run.RoadSide * halfWalkM, Surface.Pavement, Plain, periods);
        }

        // And the rounds that close the runs that really stop (<see cref="Paving.Caps"/>). <b>Half a round
        // apiece and facing out</b>: the other half stands where the skirt above already laid concrete, and
        // struck as whole circles they were a fan of triangles buried inside the band at every seam in the
        // town.
        foreach (var cap in paving.Caps)
        {
            var run = paving.Walk[cap.Run];
            mesh.HalfRound(cap.PlaceM, run.Outline ? halfWalkM - edgeM : halfWalkM, cap.OutwardM,
                Surface.Pavement, Plain, periods);
        }

        // The round is one radius and the band is not centred, so on a run that reaches the outside of the
        // town the round stops a line's width inside the kerb it is supposed to reach. That much of the
        // corner is band and not shadow: it is within half a walk of the line, which is the whole of what
        // makes ground pavement (<c>GroundShapes.Paved</c>). It reaches a chord's own sag past the round it
        // meets, because two arcs of one circle struck at different phases stand that far apart at worst and
        // a line of tarmac left showing between them reads as a crack in the pavement.
        foreach (var corner in paving.Corners)
        {
            mesh.Skirt(corner, edgeM + ChordSagM, 0f, Surface.Pavement, Plain, periods);
        }

        // The kerb line, after every fill. <b>It stands on the kerb and not in the lane</b> (TER-3d) — it is
        // the innermost stroke of the pavement, so every lane keeps the whole width of asphalt the town was
        // laid at. Struck along every run and not only the ones that reach the outside of the town: a run
        // that gives way to another at a corner has tarmac on its inner side all the same, and gated on the
        // outside the line broke at every corner in the town.
        foreach (var run in paving.Walk)
        {
            mesh.Skirt(run.Line, run.RoadSide * (halfWalkM - kerbM), run.RoadSide * halfWalkM, Surface.Tarmac,
                paint, periods);
        }

        // <b>And round the corner, where one run gives way to another.</b> A kerb is the band's inner edge and
        // an edge is an offset, so at a place two runs hand over at an angle each one's stops half a walk
        // short of the corner along its own arm — an L of missing kerb two metres on a side at every car park
        // in the town. What carries it round is the rim of the half-round the band is closed with
        // (<see cref="Paving.Corners"/>), and a turn starts where the last kerb ended.
        foreach (var corner in paving.Corners)
        {
            mesh.Skirt(corner, kerbM, 0f, Surface.Tarmac, paint, periods);
        }

        // The water and the shore it is set in, largest ring first (GEN-2c). Each fill leaves a line's width
        // of the one under it showing, which is the same trick the pavement's own rim is drawn by: what
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

        for (var slab = 0; slab < plan.PavedAreas.Count; slab++)
        {
            mesh.Rect(plan.PavedAreas.MinM[slab], plan.PavedAreas.SizeM[slab], Surface.Tarmac, Plain, periods);
        }

        for (var lot = 0; lot < plan.ParkingLots.Count; lot++)
        {
            mesh.OrientedRect(plan.ParkingLots.CentreM[lot], plan.ParkingLots.Axis[lot],
                plan.ParkingLots.HalfExtentM[lot], Surface.Tarmac, Plain, periods);
        }

        // The carriageway at its own size, last of the ground. <b>It wears no kerb line of its own</b> — the
        // kerb is the pavement's inner rim now — but it is still what keeps that line out of the lane
        // (TER-3d): a line half a walk outside one piece of tarmac can stand half a walk outside nothing
        // else and still run up the middle of a junction, and the stroke such a run carries is over asphalt
        // a car drives on. Drawn back over it, the only strokes left standing are the ones on a kerb.
        //
        // It is also the road over the water a bridge carries it across, and over the deck laid there.
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            mesh.Ribbon(plan.Roads.SegmentsOf(road), plan.Roads.WidthM[road] * 0.5f, Surface.Tarmac, Plain,
                periods);
        }

        for (var turn = 0; turn < lanes.ConnectorCount; turn++)
        {
            var line = lanes.ArcsOfConnector(turn);
            if (line.Length == 0) continue;

            mesh.Ribbon(line, lanes.ConnectorWidthM(turn) * 0.5f, Surface.Tarmac, Plain, periods);
        }

        for (var corner = 0; corner < plan.JunctionCorners.Count; corner++)
        {
            mesh.Fillet(plan.JunctionCorners.CornerM[corner], plan.JunctionCorners.ArcCentreM[corner],
                plan.JunctionCorners.RadiusM[corner], plan.JunctionCorners.TangentAM[corner],
                plan.JunctionCorners.TangentBM[corner], 0f, Surface.Tarmac, Plain, periods);
        }


        mesh.FirstMarkVertex = mesh._vertices.Count;
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
    /// <b>Every piece of the town's tarmac grown by one distance</b> — a band by its ends as well as its
    /// sides (TER-3c.6), a kerb fillet by reading its arc in, a car park by turning its box's corners on
    /// the distance itself. Growing a union one piece at a time is growing the union, so this covers every
    /// square metre within that distance of the tarmac and nothing else.
    /// </summary>
    /// <remarks>
    /// <b>A junction has no piece here, because a junction has no shape</b> (TER-5): what a box is on the
    /// ground is the lines cars are turned through it on and the fillets that round the wedges between its
    /// arms, and the band beside the movements is not the arms' own because a movement swings wider than
    /// either arm it runs between (<see cref="CityGen.GroundShapes.Turns"/>). Under the distance a fillet
    /// cannot be read in at all and its corner is left to the arms — the one call <c>Kerbs.Wrapping</c>
    /// makes about the same shape.
    /// </remarks>
    void Grown(
        CityPlan plan, LaneLines lanes, float outM, Surface surface, Vector3 tint, float[] periods)
    {
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            Band(plan.Roads.SegmentsOf(road), plan.Roads.WidthM[road] * 0.5f, outM, surface, tint, periods);
        }

        for (var connector = 0; connector < lanes.ConnectorCount; connector++)
        {
            Band(
                lanes.ArcsOfConnector(connector), lanes.ConnectorWidthM(connector) * 0.5f, outM, surface,
                tint, periods);
        }

        for (var corner = 0; corner < plan.JunctionCorners.Count; corner++)
        {
            if (plan.JunctionCorners.RadiusM[corner] <= outM) continue;

            Fillet(plan.JunctionCorners.CornerM[corner], plan.JunctionCorners.ArcCentreM[corner],
                plan.JunctionCorners.RadiusM[corner], plan.JunctionCorners.TangentAM[corner],
                plan.JunctionCorners.TangentBM[corner], -outM, surface, tint, periods);
        }

        for (var lot = 0; lot < plan.ParkingLots.Count; lot++)
        {
            RoundedRect(plan.ParkingLots.CentreM[lot], plan.ParkingLots.Axis[lot],
                plan.ParkingLots.HalfExtentM[lot] + new Vector2(outM), outM, surface, tint, periods);
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
