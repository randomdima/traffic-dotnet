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
/// the pockets of asphalt beside a road with something against its kerb; the pavement, run by run with
/// the rounds and turns that close it; the water; the decks; the paved slabs; the lots; the roads' own
/// cross-sections to their cuts; and the boxes between them. There is no depth buffer and nothing to sort
/// — one indexed draw in one pass — and nearly all of it is a partition of the ground rather than a stack
/// (TER-7b).
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
    /// The stations one call of <see cref="Strips"/> is struck on, kept between its bands: where each
    /// stands, which way across the curve is there, and how sharply the curve turns — the centre of the
    /// turn stands that far along <c>Across</c>. It is scratch and is cleared by every use.
    /// </summary>
    readonly List<(Vector2 AtM, Vector2 Across, float Curvature)> _stations = [];

    /// <summary>
    /// Which corner already stands at a place, wearing a surface and a shade (<see cref="Vertex"/>): what
    /// makes two shapes that meet share the seam between them rather than each carrying a copy of it. It is
    /// laying scratch and is dropped once the ground is laid.
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

        // <b>Nothing is grown.</b> Every square metre of the town's ground is laid as itself, once: a
        // road as its own cross-section, a car park as its box and the band round it, a junction as the box
        // between its arms' cuts, and the pavement as the runs that wrap all of them with the rounds and
        // turns that close them (TER-7b). A dead end's head is the runs that turn round it, its rim
        // included, and the road's own section runs to the end inside them.
        //
        // <b>The pocket beside a road with something standing against its kerb</b> (TER-3c.7) — the
        // ground between a street and the car park set back off it, which is asphalt and not concrete — on
        // the road's own stations so that it meets the carriageway laid last exactly. <b>Under the pavement
        // and not over it</b>: at a car park's mouth the rounds that close the runs stand on this ground,
        // and laid with the rest of the section they were painted out to a square notch.
        var shades = new SectionShades(walkM, kerbM, edgeM, paint, edge);
        foreach (var section in paving.Sections)
        {
            if (!Clipped(plan, paving, section, out var stretch)) continue;

            mesh.Section(
                plan.Roads.SegmentsOf(section.Road), plan.Roads.WidthM[section.Road] * 0.5f, stretch,
                shades, periods, pockets: true);
        }

        // <b>The pavement is the band the walk runs down</b> (TER-3c.3): everything within half a walk of
        // <c>Paving.Walk</c> — the town's outline at half a walk, cut to the runs of it that are really the
        // outside. Both its edges are offsets of that one curve, so the kerb turns a corner the way the
        // shell against the grass does and the way the lane between them does; and a run's end is closed
        // with the half-round the answer measures there (<c>GroundShapes.Paved</c>), which is what fills the
        // wedge where two runs give way to one another.
        //
        // It stops a line's width short of the outside, which is what leaves the shell's shadow standing.
        //
        // <b>Except where the run wraps a carriageway</b>, which is most of the town: that band is the
        // road's own cross-section above, struck on the road's stations rather than on the offset's, so
        // that the concrete and the asphalt it meets share their seam instead of standing a sag apart.
        //
        // <b>Struck as one cross-section on one set of stations</b>, the way a road's is (TER-7b): the walk
        // and the kerb line on the band's inner edge (TER-3d) as bands between offsets of the run's own
        // line, so the kerb line and the concrete it stands on share their seam rather than the one being
        // painted over the other. The rim where the run is the town's outline is a strip on the same
        // stations, laid after the roads (<see cref="GroundMesh.Rim"/>).
        foreach (var run in paving.Walk)
        {
            if (run.AlongARoad) continue;

            mesh.Run(run, shades, periods);
        }

        // And the rounds that close the runs that really stop (<see cref="Paving.Caps"/>). <b>Half a round
        // apiece and facing out</b>: the other half stands where the run above already laid concrete, and
        // struck as whole circles they were a fan of triangles buried inside the band at every seam in the
        // town. Each carries its own rim where its run is the outline, on the same stations as its disc.
        foreach (var cap in paving.Caps)
        {
            mesh.Round(cap, paving.Walk[cap.Run].Outline, shades, periods);
        }

        // The round is one radius and the band is not centred, so on a run that reaches the outside of the
        // town the round stops a line's width inside the kerb it is supposed to reach. That much of the
        // corner is band and not shadow: it is within half a walk of the line, which is the whole of what
        // makes ground pavement (<c>GroundShapes.Paved</c>). It reaches a chord's own sag past the round it
        // meets, because two arcs of one circle struck at different phases stand that far apart at worst and
        // a line of tarmac left showing between them reads as a crack in the pavement.
        //
        // <b>And the kerb line round the corner, where one run gives way to another.</b> A kerb is the
        // band's inner edge and an edge is an offset, so at a place two runs hand over at an angle each one's
        // stops half a walk short of the corner along its own arm — an L of missing kerb two metres on a side
        // at every car park in the town. What carries it round is the rim of the half-round the band is
        // closed with (<see cref="Paving.Corners"/>), and a turn starts where the last kerb ended. The two
        // are one cross-section on the turn's own stations: the concrete outside the stroke, then the stroke.
        for (var corner = 0; corner < paving.Corners.Length; corner++)
        {
            // A hand-over between two kerbs that lie along one another is a step and not a wedge, and what
            // covers it is the whole cross-section struck across it (<see cref="GroundMesh.Bridge"/>). The
            // wedge a turn lays reaches the place its arc turns about — the band's road half — so laid on a
            // step it covered that half twice and left the outer half open.
            if (paving.Straight[corner]) continue;

            mesh.Turn(paving.Corners[corner], shades, periods);
        }

        Bridges(mesh, paving, shades, rim: false, periods);

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
        //
        // <b>Every road's whole cross-section, struck off that road's own arcs at one set of stations</b>
        // (TER-7b): the carriageway, the kerb line either side of it, the two bands of pavement and the two
        // rims, as bands between consecutive offsets of one curve. <b>No two of them overlap and no seam
        // between them can open</b> — a seam is one offset evaluated once, so the two bands that meet on it
        // stand at the same points rather than a chord's sag apart. Over a road away from a box this is the
        // whole of what covers the ground, and the ground is covered once.
        //
        // What each side carries over each stretch is the town's own answer (<see cref="Paving.Sections"/>),
        // read off the same predicate the pavement's runs are cut by. A side with nothing beside it but the
        // verge carries the rim; one with more town beyond carries none; one with something standing against
        // the kerb carries the tarmac that is really there (TER-3c.7) and no concrete at all.
        //
        // <b>Laid where the carriageway used to be, and last of the ground for the same reason</b>: a run's
        // corner turns a kerb stroke round the place two runs hand over, and such a turn crosses the lane
        // where the arms it stands between are of different widths. What keeps a stroke out of the lane is
        // still the road drawn back over it (TER-3d).
        //
        // <b>And only as far as its box</b> (<see cref="Paving.EnterM"/>, <see cref="Paving.ExitM"/>): an
        // arm stops where the box takes over from it, and the box is one shape laid once below. Run to the
        // node, four arms' sections crossed one another in every box in the town.
        foreach (var section in paving.Sections)
        {
            if (!Clipped(plan, paving, section, out var stretch)) continue;

            mesh.Section(
                plan.Roads.SegmentsOf(section.Road), plan.Roads.WidthM[section.Road] * 0.5f, stretch,
                shades, periods, pockets: false);
        }

        // Where one of an arm's sides runs on past the cut into the box, that side's own concrete beside it
        // (<see cref="Paving.Stubs"/>): the same bands the section would have laid there, on the same
        // stations, with the carriageway and the other side left to the box.
        foreach (var stub in paving.Stubs)
        {
            if (stub.ToM <= stub.FromM || !SectionAtTheCut(plan, paving, stub, out var beside)) continue;

            mesh.Stub(
                plan.Roads.SegmentsOf(stub.Road), plan.Roads.WidthM[stub.Road] * 0.5f, stub, beside,
                shades, periods);
        }

        // <b>The box itself, once</b> (<see cref="Paving.Boxes"/>, TER-7b): the ground between the arms'
        // cuts, their kerbs and the fillets that round their wedges, as one outline cut into triangles.
        foreach (var box in paving.Boxes)
        {
            mesh.Box(box.Outline, periods);
        }

        // <b>The runs' rims, and the rim round every corner the shell turns</b>, after the roads
        // (<see cref="GroundMesh.Rim"/>, <see cref="Paving.ShellCorners"/>): where a run hands over to a
        // road, the last of its rim stands inside the road's band, and the road's bare side laid over it
        // painted it out to the road's own edge. Each run's rim stops where its outer edge stops being the
        // outline, the road's stops where its own does, and the sector between the two square ends is the
        // corner's.
        foreach (var run in paving.Walk)
        {
            if (run.AlongARoad) continue;

            mesh.Rim(run, shades, periods);
        }

        Bridges(mesh, paving, shades, rim: true, periods);

        foreach (var corner in paving.ShellCorners)
        {
            mesh.ShellCorner(corner, shades, periods);
        }

        // <b>No movement is drawn at its own size.</b> One that lies inside the arms it joins is the box's
        // ground; one that swings out past a fillet's arc, or a hair past a kinked road's kerb, takes a
        // sliver beyond the box's edge — and that sliver is the movement grown by a walk in the union pass
        // above, asphalt to a line's width short of the walk, which is the one place a junction is still
        // painted over its own box (<see href="../../../../docs/index.md">known gaps</see>).


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
    /// <b>Every hand-over the pavement makes</b> (<see cref="Paving.Next"/>), laid once each: the band
    /// struck across it off the two runs' own end stations (<see cref="GroundMesh.Bridge"/>), so what
    /// stands between two ends is the corners those two ends already stand on.
    /// </summary>
    /// <remarks>
    /// <b>Walked over the ends and not over the turns</b>, because it is the two ends the band is read off
    /// and a turn carries neither. A pair is reached from both of its ends, so it is taken from the lower of
    /// the two. <b>A hand-over a wedge covers gets the outer half of the cross-section and no more</b>: the
    /// wedge reaches the run's own line and the two would otherwise cover the road half twice.
    /// </remarks>
    static void Bridges(GroundMesh mesh, Paving paving, in SectionShades shades, bool rim, float[] periods)
    {
        for (var end = 0; end < paving.Next.Length; end++)
        {
            var onto = paving.Next[end];
            if (onto <= end) continue;

            var turn = paving.TurnFrom[end] != CityPlan.NoRecord ? paving.TurnFrom[end] : paving.TurnFrom[onto];
            var wedged = turn != CityPlan.NoRecord && !paving.Straight[turn];
            mesh.Bridge(
                paving.Walk[end / 2], end % 2 == 0, paving.Walk[onto / 2], onto % 2 == 0, shades, rim, wedged,
                periods);
        }
    }

    /// <summary>
    /// <b>What the road carries beside a stub</b>: the section the road's own stretch ends with at the cut
    /// the stub runs on past (<see cref="Paving.EnterM"/>, <see cref="Paving.ExitM"/>).
    /// </summary>
    /// <remarks>
    /// <b>Read at the cut and not over the stub's own stretch.</b> A stub stands inside the box, and the
    /// sections there answer for a road with a junction beside it, which is nothing on either side — so a
    /// stub read off the sections it overlaps laid no concrete at all and the side that ran on stopped at
    /// the cut with the other.
    /// </remarks>
    static bool SectionAtTheCut(CityPlan plan, Paving paving, in PavedStub stub, out PavedSection beside)
    {
        // A box stands at one end of a road or the other, so which end this stub runs into is which end it
        // is nearer, and the road's own stations are the ones the other way off its inner end. <b>The
        // nearest section that side still carries something</b>: a road answers None for the last few
        // centimetres before its cut as well, since the ground beside it there is already the junction's,
        // and a stub read off one of those laid nothing.
        var lengthM = Spline.TotalLengthM(plan.Roads.SegmentsOf(stub.Road));
        var atStart = stub.FromM < lengthM - stub.ToM;
        var atM = atStart ? stub.ToM : stub.FromM;
        var nearestM = float.PositiveInfinity;
        beside = default;
        foreach (var section in paving.Sections)
        {
            if (section.Road != stub.Road) continue;
            if ((stub.Side > 0f ? section.Right : section.Left) == PavedEdge.None) continue;
            if (atStart ? section.ToM <= atM : section.FromM >= atM) continue;

            var awayM = MathF.Max(0f, MathF.Max(section.FromM - atM, atM - section.ToM));
            if (awayM >= nearestM) continue;

            nearestM = awayM;
            beside = section;
        }

        return nearestM < float.PositiveInfinity;
    }

    /// <summary>
    /// A section clipped to the stretch of its road that is the road's own and not a box's
    /// (<see cref="Paving.EnterM"/>, <see cref="Paving.ExitM"/>), or nothing where none of it is.
    /// </summary>
    static bool Clipped(CityPlan plan, Paving paving, in PavedSection section, out PavedSection stretch)
    {
        var lengthM = Spline.TotalLengthM(plan.Roads.SegmentsOf(section.Road));
        var fromM = MathF.Max(section.FromM, paving.EnterM[section.Road]);
        var toM = MathF.Min(section.ToM, lengthM - paving.ExitM[section.Road]);
        stretch = section with { FromM = fromM, ToM = toM };
        return toM > fromM;
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
