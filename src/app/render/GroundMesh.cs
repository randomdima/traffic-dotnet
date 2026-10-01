using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Foot;
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
/// The town's standing ground, triangulated once at load from the plan's <b>shapes</b> — the driven
/// ground's boundary and the walk struck off it (<see cref="GroundRings"/>), decks along their roads' own
/// splines, slabs and water outlines, and the paint the lanes, bays and crossings carry. Never from the cell
/// grid: no arrangement of metre squares is a kerb running at 40°.
/// </summary>
/// <remarks>
/// <para>
/// <b>The ground is a stack of layers and a layer beside a road is a region of the town's own boundary</b>
/// (TER-7b): grass over the whole world, then the pavement and the kerb along its outer face, then the water
/// and the decks, then the carriageway at its own size and the slabs, then the town's kerb, then the paint.
/// A block is a hole in both layers, read off its ring's own winding, and takes no pass of its own. <b>The two layers are one boundary read at two distances</b> (<see cref="GroundRings"/>,
/// TER-3c.3) — the carriageway inside it and the walk out to the pavement's outer face — so the inner
/// encloses nothing the outer does not and each is simply laid over the one before it. There is no depth
/// buffer and nothing to sort: one indexed draw in one pass.
/// </para>
/// <para>
/// <b>It is <c>GroundShapes.At</c>'s order, forwards</b> (TER-7), and the two read the same rings: the answer
/// is which side of them a point stands, and this fills them. A shape added to one is added to the other, at
/// the same place in the order, and what parts them is only the chords a fill is cut into — which the kerb is
/// laid over.
/// </para>
/// <para>
/// <b>A kerb is a line with a mesh of its own</b> (<see cref="Stroke"/>, TER-3d): the shell it belongs to
/// laid at the width a kerbstone is with that shell running down the middle of it, over the fills rather
/// than cut out of them, and there are two — the town's own boundary and the walk's outer face. Neither
/// borrows a triangle from a fill, so each is two hundred millimetres wherever its line runs, on a bend as on
/// a straight, whatever the fills either side of it were thinned to. <b>And a kerb is the only edge of a
/// layer anybody sees</b> (<see cref="Line"/>, <see cref="HiddenShare"/>): the fill beneath it is the kerb's
/// own line thinned by what the stone hides, so what is cut for the picture is the line and what is cut for
/// the kerb is the fill. <b>A rim is the other way of drawing a line and is what is left where
/// nothing has a boundary to strike one off</b> — the region twice, a line's width apart, the outer in the
/// line's shade and the inner laid over it in the surface's own. A deck's edge and the shore are drawn that
/// way, a deck being a ribbon about a road's own line.
/// </para>
/// <para>
/// What is <b>not</b> here is anything that is not ground: buildings, props, agents and their sprites
/// are the second pipeline.
/// </para>
/// </remarks>
internal sealed partial class GroundMesh
{
    /// <summary>How far a drawn chord is allowed to bow off the arc it stands for.</summary>
    public const float ChordSagM = 0.02f;

    /// <summary>
    /// <b>How much of a kerb's half-width a fill may be let get wrong</b>, the kerb being laid over it: the
    /// thinning every filled shell here is cut at, as a share of the stone that hides it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A fill has no visible edge in this town</b> (TER-3d, TER-7b). Every shell filled here carries a
    /// kerb along its boundary, and the kerb is laid last of the three — so where a fill cuts a corner what
    /// shows through is the layer under it, and two hundred millimetres of stone is laid over both. The one
    /// thing that may not happen is the fill reaching out from under that stone, which is what this share is
    /// of: at three quarters the fill's edge stands at worst 75 mm off the kerb's line and 25 mm inside the
    /// kerb's own edge.
    /// </para>
    /// <para>
    /// <b>So the thinning is bounded by the kerbstone and not by a zoom</b>, and it is spent to the bound:
    /// a sag of two centimetres alone reads the town as 26 241 corners and this hands back two thirds of
    /// them. What a picture is worth decides the <em>line</em> instead (<see cref="ChordTurnRad"/>), which
    /// is the only part of either layer anybody looks at.
    /// </para>
    /// </remarks>
    public const float HiddenShare = 0.75f;

    /// <summary>
    /// <b>How much of a turn one chord of a kerb's line may stand for</b>, whatever the sag says
    /// (<see cref="ShellFill"/>). It is the figure the town's curves are judged on, a kerb being the only
    /// edge of a layer anybody sees.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The budget in metres runs out the wrong way on a tight bend.</b> The step a sag earns is
    /// <c>2·acos(1 − sag/R)</c>, which <em>grows</em> as the radius shrinks: the boundary's tightest turn is
    /// 0.14 m of radius (<c>--bench outset</c>) and comes back at sixty degrees a chord, inside a sag of two
    /// centimetres the whole way. <b>What a picture loses on a bend is direction and not distance</b>, which
    /// is the eye's own measure of a curve and has no budget in metres — so this is an angle.
    /// </para>
    /// <para>
    /// <b>And it is what cuts the kerb for its own ribbon.</b> A stroke's outer edge goes round a circle a
    /// half-width wider than its line's, so a line cut at a sag alone leaves that edge bowing by
    /// <c>sag·(R+½w)/R</c> — which only passes a tenth over below a metre of radius, while this binds
    /// everywhere under twenty-one. The angle covers the ribbon's own need several times over, and a
    /// segment's offset being a segment, there is nothing between two corners left to refine.
    /// </para>
    /// </remarks>
    public const float ChordTurnRad = 8f * MathF.PI / 180f;

    /// <summary>
    /// <b>And how far the line itself may be let off the arcs</b>, which is the same tolerance as the sag
    /// and for the same reason: it is what a frame can tell apart. It takes the near-collinear corners out
    /// of the straights, where a sag spends evenly and a kerb has nothing to show for it.
    /// </summary>
    public const float LineThriftM = 0.02f;

    /// <summary>
    /// <b>The line a shell's kerb is struck from</b>, which is the shell cut for the picture: every budget
    /// here is what a frame can tell apart, this being the one line anybody sees.
    /// </summary>
    public static Vector2[][] Line(ReadOnlySpan<ArcSeg[]> rings) =>
        ShellFill.Outline(rings, ChordSagM, LineThriftM, ChordTurnRad);

    /// <summary>
    /// <b>And the fill laid under it</b> (<see cref="HiddenShare"/>): that same line thinned by what the
    /// kerb hides, so the two part by one budget rather than by the sum of two readings.
    /// </summary>
    public static Vector2[][] Filled(ReadOnlySpan<Vector2[]> line, float kerbWidthM) =>
        ShellFill.Outline(line, kerbWidthM * 0.5f * HiddenShare);

    /// <summary>
    /// How far apart two corners may stand and still be one point: a millimetre, which is the rounding the
    /// town's own outline is cut at (<see cref="LineTolerance.RoundingM"/>) and well under anything a frame shows.
    /// </summary>
    const float OnePointM = LineTolerance.RoundingM;

    /// <summary>White: a surface drawn as itself.</summary>
    static readonly Vector3 Plain = Vector3.One;

    /// <summary>
    /// <b>An edge is the surface darkened</b>: a deck's rim, which is the one line here left where nothing
    /// has a boundary to strike a stroke off. It is a measurement and not a relation, and nothing in the
    /// town is drawn in a colour of its own.
    /// </summary>
    public static readonly Vector3 Edge = new(0.58f, 0.58f, 0.62f);

    /// <summary>
    /// <b>And paint is the surface brightened</b>: a mark is the tarmac under it catching the light, so the
    /// factor is above one and the mark is read off the ground it is laid on rather than given a colour.
    /// It is a measurement like <see cref="Edge"/> and not the inverse of one — 1/0.58 is 1.72 and lays a
    /// dash two and a half times too dark.
    /// </summary>
    /// <remarks>
    /// <b>The town's kerb is drawn in it, on the carriageway's own grain</b>: the line along the driven
    /// ground's boundary is the white line at the edge of the road and reads as the dashes down the middle
    /// of it do, which is one white in the town rather than two that nearly agree. It is still ground and
    /// not a mark (<see cref="FirstMarkVertex"/>).
    /// </remarks>
    public static readonly Vector3 Paint = new(2.6f, 2.6f, 2.5f);

    /// <summary>
    /// <b>And the walk's own kerb is the pavement's own stone in shadow</b>: the concrete it bounds, on the
    /// same grain, taken down to two thirds — the back of a pavement is the same slab standing on its side,
    /// so what tells it from the walk is the light on it and not a second material.
    /// </summary>
    /// <remarks>
    /// <b>Well short of <see cref="Edge"/>, which is a rim and not a kerb.</b> A line that dark beside the
    /// turf reads as a shadow cast on the concrete rather than as the stone at the back of it.
    /// </remarks>
    public static readonly Vector3 Stone = new(0.66f, 0.66f, 0.69f);

    readonly List<GroundVertex> _vertices = [];
    readonly List<uint> _indices = [];

    readonly GroundTally[] _parts = new GroundTally[GroundParts.Count];

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
    /// <b>The kerb line is not among them</b>, though it is laid as quads of its own (<see cref="Stroke"/>)
    /// in the same surface and the same shade: a kerb is ground the town is built of and a mark is paint on
    /// it. Neither the tint nor the surface tells the two apart, which is why anything asking what was
    /// <em>painted</em> asks this instead.
    /// </remarks>
    public int FirstMarkVertex { get; private set; }

    /// <summary>
    /// And where the arrows start (TER-6a), which is where the mesh stops being four corners a mark: a
    /// glyph is a ribbon and a head, so a reader walking marks in fours reads between these two.
    /// </summary>
    /// <remarks>
    /// <b>They are laid last of the paint for this reason alone.</b> A mark asked about by its four corners
    /// is every mark a lane, a bay or a crossing carries; an arrow is asked about as the arrow it is
    /// (<see cref="LaneArrows"/>), which is the one reading that says which turns it is there to name.
    /// </remarks>
    public int FirstArrowVertex { get; private set; }

    /// <summary>
    /// What each layer of the ground came to, in the order they were laid: the run of the index buffer
    /// that is that layer, and what cutting it cost (<see cref="GroundTally"/>, OBS-2v).
    /// </summary>
    /// <remarks>
    /// <b>The runs tile the mesh</b> — each part starts where the one before it ended and the last of them
    /// ends at the last index — which is what lets a part be left out of a frame by shortening the draw
    /// rather than by laying the ground again (<c>TownRenderer.ShowGround</c>).
    /// </remarks>
    public ReadOnlySpan<GroundTally> Parts => _parts;

    /// <summary>
    /// The whole of what laying this ground cost, the boundary it was struck off included — the figure
    /// <c>--map</c> prints and the menu's ground section reads.
    /// </summary>
    public double LaidMs { get; private set; }

    /// <summary>
    /// And how much of that went on the one shape every layer is a distance off (<see cref="GroundRings"/>)
    /// rather than on any layer of it. <b>It is the merge and not a triangulation</b>, so a town whose
    /// ground is slow to lay is answered here or in the parts and never in both.
    /// </summary>
    /// <remarks>
    /// <b>What a second town pays is not what the first one did</b>: the paving behind the rings is the
    /// plan's own and is laid once, so this figure is the whole merge for the town that asked for it first
    /// and the reading off a cache for anything asking after.
    /// </remarks>
    public double BoundaryMs { get; private set; }

    /// <summary>
    /// And how much of <em>that</em> was the merge alone (<c>LaneShell</c>) — every ribbon the town is
    /// driven on cut against every ribbon near it, before a single layer has been struck off the shape it
    /// comes to. It is the whole of the ground's cost on a city and is nothing to do with triangles.
    /// </summary>
    public double MergeMs { get; private set; }

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
        var grass = mesh.Starting();
        for (var corner = 0; corner < 3; corner++)
        {
            mesh._vertices.Add(new GroundVertex(Vector2.Zero, Vector2.Zero, Vector3.Zero, Surface.Grass));
            mesh._indices.Add((uint)corner);
        }

        // The one triangle is the world's own layer, so the parts still tile the mesh (<see cref="Parts"/>)
        // and a ground section opened over no town reads zeroes rather than a mesh nothing accounts for.
        mesh.Laid(GroundPart.Grass, grass);
        return mesh;
    }

    public static GroundMesh Build(CityPlan plan, SimConfig config)
    {
        var mesh = new GroundMesh();
        var startedAt = Stopwatch.GetTimestamp();
        var periods = Periods(config);

        // <b>The pavement is a step and not a shape this pass works out for itself</b> (TER-3c): the walk
        // and the lines a car is driven on are the town's own (<see cref="Paving"/>). Derived again here,
        // the picture and the answer are two readings that have to be kept in step by whoever remembers.
        var paving = plan.Paving(config);
        var edgeM = config.Road.EdgeLineWidthM;

        // <b>One kerb's width and two kerbs struck at it</b>: where the carriageway hands over to the walk,
        // and where the walk hands over to the grass (TER-3c.3). It is the width the kerbstone is and not a
        // line's width, which is why it is the world's figure and not the paint's.
        var kerbM = config.Road.KerbWidthM;

        var grass = mesh.Starting();
        mesh.Rect(Vector2.Zero, plan.WorldSizeM, Surface.Grass, Plain, periods);
        mesh.Laid(GroundPart.Grass, grass);

        // <b>The merge apart from the layers struck off it</b>: the two are one ask (<c>Paving.Rings</c>)
        // and the first of them is the expensive half by two orders of magnitude, so a ground that takes
        // seconds to lay is answered by which of these two numbers is the seconds rather than by both.
        var boundaryFrom = Stopwatch.GetTimestamp();
        paving.Perimeter(config);
        mesh.MergeMs = Stopwatch.GetElapsedTime(boundaryFrom).TotalMilliseconds;
        var rings = paving.Rings(config);
        mesh.BoundaryMs = Stopwatch.GetElapsedTime(boundaryFrom).TotalMilliseconds;

        // <b>Each layer's boundary read twice, the line finely and the fill by what the line hides</b>
        // (<see cref="ShellFill.Outline"/>, TER-3d). <b>The kerb is the only edge of a layer anyone sees</b>:
        // every shell filled here carries one along its boundary, so where the fill cuts a corner what shows
        // through is the layer beneath and the kerb is laid over both. So the line is cut for the picture —
        // <see cref="ChordSagM"/> and <see cref="ChordTurnRad"/>, and no thinning at all — and the fill is
        // that same line thinned by <see cref="HiddenShare"/> of a kerb's half-width, which is the whole of
        // what may be got wrong under it.
        // <b>Thinned from the line and not read again from the arcs</b>, so how far the two part is that one
        // budget rather than the sum of what each strays.
        var walkLine = Line(rings.Walk.Rings);
        var carriagewayLine = Line(rings.Carriageway.Rings);
        var walkFill = Filled(walkLine, kerbM);
        var carriagewayFill = Filled(carriagewayLine, kerbM);

        // <b>The pavement and the kerb along its outer face, under the water and the decks</b> (TER-7b): a
        // walk that reaches a shore is ground the water then covers, so what lies outside the carriageway is
        // laid before it and not over it. <b>The walk is the offset filled whole</b> and the driven ground it
        // covers is covered back by the carriageway below, which is what a layer enclosing every layer inside
        // it means and what spares this fill a second copy of the boundary to carry and thin.
        // <b>The line is struck along the walk's own outer face and not cut out of the fill</b> (TER-3d): it
        // is a kerb's width about that line wherever it runs, which is what the thinning either fill is laid
        // at cannot promise, and it is laid after the fill so the fill cannot eat into it. <b>And it wears
        // the concrete it bounds, darkened</b> (<see cref="Stone"/>): the same surface as the walk, told
        // from it by the light on the stone alone.
        var walk = mesh.Starting();
        mesh.Shell(walkFill, Surface.Pavement, Plain, periods);
        mesh.Laid(GroundPart.Walk, walk);

        var walkKerb = mesh.Starting();
        foreach (var ring in walkLine)
        {
            mesh.Stroke(ring, kerbM, closed: true, Surface.Pavement, Stone, periods);
        }

        mesh.Laid(GroundPart.WalkKerb, walkKerb);

        // The water and the shore it is set in, largest ring first (GEN-2c). Each fill leaves a line's width
        // of the one under it showing, which is the same trick every other line here is drawn by: what
        // survives is one line where the shore meets the grass and another where it meets the water. <b>Each
        // takes the colour of the ground it meets</b> — green against the grass and blue against the water —
        // and each is drawn darker than that ground, so the edge reads as the shore's own shadow on it
        // rather than as a highlight laid over it.
        var water = mesh.Starting();
        Water(mesh, plan.Water.Shore, Surface.Pavement, Shade(0.3f, 0.48f, 0.22f), periods);
        Water(mesh, plan.Water.ShoreEdge, Surface.Pavement, Plain, periods);
        Water(mesh, plan.Water.WaterEdge, Surface.Pavement, Shade(0.08f, 0.2f, 0.3f), periods);
        Water(mesh, plan.Water.Outline, Surface.Water, Plain, periods);
        mesh.Laid(GroundPart.Water, water);

        // A deck is drawn out to its own half-width, with a rim rather than a stroke: the piece at full size
        // in the edge shade, then a line's width smaller in its own. A ribbon about a road's line has no
        // shell of its own to strike a kerb along, which is the one place a rim is what is left.
        // <b>And nothing but the deck</b> — the pavement that used to be carried across one at the width it
        // has on land was the last line beside a road struck by arithmetic of its own (TER-3c.3), so it is
        // gone and the margin outside the carriageway is deck all the way out.
        var decks = mesh.Starting();
        for (var bridge = 0; bridge < plan.Bridges.Count; bridge++)
        {
            var road = plan.Bridges.Road[bridge];
            if (road < 0) continue;

            var span = plan.Roads.SegmentsOf(road);
            var deckM = plan.Bridges.DeckWidthM[bridge];
            mesh.Stroke(span, deckM, closed: false, Surface.Deck, Edge, periods);
            mesh.Stroke(span, deckM - (edgeM * 2f), closed: false, Surface.Deck, Plain, periods);
        }

        mesh.Laid(GroundPart.Decks, decks);

        // <b>The carriageway at its own size</b> (TER-7b): the boundary filled as the shape it is, over the
        // walk that reaches under it, and struck after the decks because a bridge carries its carriageway
        // over its own surface and not over the land's.
        var carriageway = mesh.Starting();
        mesh.Shell(carriagewayFill, Surface.Tarmac, Plain, periods);
        mesh.Laid(GroundPart.Carriageway, carriageway);

        // <b>Between the stroke and the carriageway, the tarmac that is not a road</b>: a slab. It is where
        // the answer puts it (<c>GroundShapes.At</c>).
        var slabs = mesh.Starting();
        for (var slab = 0; slab < plan.PavedAreas.Count; slab++)
        {
            mesh.Rect(plan.PavedAreas.MinM[slab], plan.PavedAreas.SizeM[slab], Surface.Tarmac, Plain, periods);
        }

        mesh.Laid(GroundPart.Slabs, slabs);

        // <b>Then the town's kerb, which is a line and not a layer</b> (TER-3d, TER-7b): a kerb's width laid
        // about the boundary itself, over everything the layers left along it. Last of the ground, so what it
        // covers is a kerb wherever the boundary runs — and so the place the walk meets the carriageway, each
        // of them thinned on its own terms, is under the middle of it rather than beside it.
        // <b>Drawn as the road's own edge line</b> (<see cref="Paint"/>): the carriageway's grain through the
        // shade every other white line in the town is laid in, and not the pavement's.
        var kerb = mesh.Starting();
        foreach (var ring in carriagewayLine)
        {
            mesh.Stroke(ring, kerbM, closed: true, Surface.Tarmac, Paint, periods);
        }

        mesh.Laid(GroundPart.Kerb, kerb);

        // <b>Then the paint, which is the one layer above the ground rather than in it</b> (TER-7b): a mark
        // sits on the surface it belongs to, and marks are not welded — a dash is read back out of the mesh
        // as the four corners it was laid as (<see cref="FirstMarkVertex"/>).
        // <b>And the whole of it is what the lanes and the roads say</b>: the lines between two ribbons, the
        // zebra at the end of every arm a junction forks at, and the bar behind each of them.
        mesh.FirstMarkVertex = mesh._vertices.Count;
        mesh._welding = false;
        mesh._welds = [];

        // <b>Where the walk meets the road is the walk's to say</b> (WLK-10): the places a walk beside a road
        // is cut are the two kerbs a band reaches between, so every mark here is asked for by the town's own
        // kerb ends (<see cref="CityGen.KerbEnds"/>) rather than read off the roads a second time.
        // <b>And the walk has two answers to give</b>: a street too short to be crossed twice is crossed once
        // in the middle while still being held at both of its ends, so the zebra is laid off where the walk
        // crosses and the bar — with the dashes that stop short of it — off what each arm holds behind,
        // which there is the end of the road's own kerb (WLK-10a).
        var ends = paving.RoadEnds(config);
        var crossed = Crossings.Lay(plan, config, ends.CrossedM);
        var held = Crossings.Lay(plan, config, ends.HeldM);

        // <b>The bars are laid once and read twice</b>: the paint a driver holds at, and the arrows behind it
        // that say what they are holding for (TER-6a). Laid again for the arrows, the two would be free to
        // disagree about where the bar stands.
        var bars = StopBars.Lay(paving.Lanes, held, config);

        var marks = mesh.Starting();
        mesh.LaneDashes(plan, config, held, Paint, periods);
        mesh.BayStrokes(plan, config, Paint, periods);
        mesh.Zebras(crossed, config, Paint, periods);
        mesh.Bars(bars, Paint, periods);

        mesh.FirstArrowVertex = mesh._vertices.Count;
        mesh.Arrows(paving.Lanes, bars, config, Paint, periods);
        mesh.Laid(GroundPart.Paint, marks);

        // Grown by doubling and kept for the whole run, the two lists would keep up to as much again as they hold.
        mesh._vertices.TrimExcess();
        mesh._indices.TrimExcess();

        mesh.LaidMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        return mesh;
    }


    /// <summary>
    /// Where the mesh stood as a part began, so what that part came to is the difference rather than a
    /// figure anything has to be told (<see cref="Laid"/>).
    /// </summary>
    readonly record struct PartStart(int Index, int Corner, long At);

    PartStart Starting() => new(_indices.Count, _vertices.Count, Stopwatch.GetTimestamp());

    void Laid(GroundPart part, PartStart from) => _parts[(int)part] = new GroundTally(
        from.Index, _indices.Count - from.Index, _vertices.Count - from.Corner,
        Stopwatch.GetElapsedTime(from.At).TotalMilliseconds);

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
