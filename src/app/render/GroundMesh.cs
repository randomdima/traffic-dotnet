using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Rail;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.App.Render;

/// <summary>Which surface a triangle wears, or <see cref="Paint"/> for a flat colour.</summary>
internal enum Surface : uint
{
    Grass = 0,
    Tarmac = 1,
    Pavement = 2,
    Deck = 3,
    Water = 4,

    /// <summary>
    /// The track's strip (RAIL-3): the one surface laid along a line rather than anchored to the world, so its corners
    /// carry where on the strip they stand (<see cref="GroundVertex.OnTheTrack"/>) in place of a tint, and what the strip
    /// leaves clear is not drawn at all.
    /// </summary>
    Track = 5,

    /// <summary>The shore along every bank (GEN-2c): a surface of its own, last so no number before it moved when it came.</summary>
    Shore = 6,

    /// <summary>
    /// <b>The town's kerb, laid as its kerbstones</b> (TER-3d): a strip of its art along its line, as the track's is, its
    /// corners filing how many pictures along it they stand (<see cref="GroundVertex.OnTheStrip"/>), the picture's low
    /// side — its bottom — on the right of the line, the driven ground's side (<see cref="KerbLooks.Kerb"/>).
    /// </summary>
    Kerb = 7,

    /// <summary>The walk's own kerb, its edging stones laid the same way, the low side on the left of the line: the grass's
    /// side (<see cref="KerbLooks.Edging"/>).</summary>
    Edging = 8,

    /// <summary>
    /// <b>The shadow a kerb casts on the ground beside its low side</b>: no picture, the ground darkened by a cover that
    /// fades out away from the stone (<see cref="GroundVertex.Fading"/>), so a kerb reads as a stone standing up off the
    /// road or the grass rather than a stripe painted on it.
    /// </summary>
    Shadow = 9,

    /// <summary>Not a surface: the tint alone, for anything that is not ground.</summary>
    Paint = 255,
}

/// <summary>
/// One corner of the ground: where it stands, and its shade and surface in one word (<see cref="Pack"/>). The
/// texture coordinate is not carried: the vertex stage reads it off the world position and the surface's period
/// (<see cref="CameraView.SurfacePeriodsM"/>), which is what anchors every surface's texture to the world origin
/// rather than to the shape being painted, and what makes the triangulation invisible (TER-7's drawn half).
/// </summary>
/// <remarks>
/// <b>Twelve bytes and not thirty-two</b>, because a traced city's ground is four million corners: a coordinate
/// that is a division and three floats of a shade that is one of a few are most of the mesh, on the device and off it.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct GroundVertex(Vector2 PositionM, uint Shade)
{
    /// <summary>What a shade's every channel is filed against, a little over the brightest the ground wears (<see cref="GroundMesh.Paint"/>).</summary>
    /// <remarks>Said again in both vertex stages, which unpack it (<c>ground.vert</c>, <c>town.wgsl</c>).</remarks>
    public const float BrightestTint = 3f;

    /// <summary>The steps a channel is filed in: nine bits each, three channels and the surface's five in a word.</summary>
    const uint ChannelSteps = (1u << 9) - 1;

    /// <summary>Where the surface stands in the word, and what <see cref="Surface.Paint"/> is filed as there.</summary>
    const int SurfaceShift = 27;

    const uint PaintFiled = 31;

    public GroundVertex(Vector2 positionM, Vector3 tint, Surface surface) : this(positionM, Pack(tint, surface))
    {
    }

    public Vector3 Tint => new(Channel(Shade), Channel(Shade >> 9), Channel(Shade >> 18));

    public Surface Surface => (Shade >> SurfaceShift) == PaintFiled ? Surface.Paint : (Surface)((Shade >> SurfaceShift) & ~FadingFiled);

    /// <summary>
    /// What a surface is filed as where it is laid fading over the ground under it (<see cref="Fading"/>): its own number
    /// with this bit, which every surface but <see cref="Surface.Paint"/> leaves clear.
    /// </summary>
    /// <remarks>Said again in both vertex stages (<c>ground.vert</c>, <c>town.wgsl</c>).</remarks>
    const uint FadingFiled = 16;

    /// <summary>How much of a fading corner's surface covers the ground laid before it, from nought to one.</summary>
    public float Cover => Fades ? Channel(Shade) / BrightestTint : 1f;

    /// <summary>Whether the corner is a surface fading out over the ground before it (<see cref="Fading"/>), whole or not.</summary>
    public bool Fades =>(Shade >> SurfaceShift) != PaintFiled && ((Shade >> SurfaceShift) & FadingFiled) != 0;

    /// <summary>
    /// <b>A corner of a surface fading out over the ground laid before it</b> (GEN-2c): how much of it covers that
    /// ground, filed where a tint is, the surface drawn as itself and the blend doing the rest.
    /// </summary>
    public static uint Fading(Surface surface, float cover) =>
        Filed(Math.Clamp(cover, 0f, 1f) * BrightestTint) | (((uint)surface | FadingFiled) << SurfaceShift);

    /// <summary>A shade and a surface as one word: each channel to the nearest of its steps up to <see cref="BrightestTint"/>.</summary>
    public static uint Pack(Vector3 tint, Surface surface) =>
        Filed(tint.X) | (Filed(tint.Y) << 9) | (Filed(tint.Z) << 18)
        | ((surface == Surface.Paint ? PaintFiled : (uint)surface) << SurfaceShift);

    /// <summary>The steps a strip's corner files how far along it it stands: a picture's length in a few hundred.</summary>
    public const uint TrackStepsPerPicture = 256;

    /// <summary>
    /// How many of a strip's pictures one filing runs over before it starts again, which the strip is cut at
    /// (<see cref="GroundMesh.TrackStrip"/>): the whole pictures eighteen bits of steps hold.
    /// </summary>
    public const float TrackPicturesFiled = ((1u << 18) / TrackStepsPerPicture) - 1;

    const uint AcrossSteps = (1u << 9) - 1;

    /// <summary>Whether the corner is on a strip laid along a line (<see cref="OnTheStrip"/>), which files no tint.</summary>
    public bool OnAStrip => Surface is Surface.Track or Surface.Kerb or Surface.Edging;

    /// <summary>How far across its strip a strip's corner stands, from nought on the left of its line to one on the right.</summary>
    public float Across => ((Shade >> 18) & AcrossSteps) / (float)AcrossSteps;

    /// <summary>A corner of the track's strip (<see cref="Surface.Track"/>), as <see cref="OnTheStrip"/> files it.</summary>
    public static uint OnTheTrack(float picturesAlong, float acrossShare) => OnTheStrip(Surface.Track, picturesAlong, acrossShare);

    /// <summary>
    /// <b>A corner of a strip laid along a line</b> — the track's, or a kerb's: how many of its pictures along the strip
    /// it stands, and how far across it from nought, the left of the line, to one, where a corner of any other surface
    /// files its tint.
    /// </summary>
    /// <remarks>
    /// <b>Along is counted on and not wrapped at every picture</b>, so a seam where the coordinate wraps falls once in
    /// <see cref="TrackPicturesFiled"/> pictures and not at every one: a wrap inside a pixel quad reads as a smear the
    /// width of the whole picture's smallest mip.
    /// </remarks>
    public static uint OnTheStrip(Surface strip, float picturesAlong, float acrossShare) =>
        (uint)MathF.Round(Math.Clamp(picturesAlong, 0f, TrackPicturesFiled) * TrackStepsPerPicture)
        |((uint)MathF.Round(Math.Clamp(acrossShare, 0f, 1f) * AcrossSteps) << 18)
        | ((uint)strip << SurfaceShift);

    static uint Filed(float channel) => (uint)MathF.Round(Math.Clamp(channel / BrightestTint, 0f, 1f) * ChannelSteps);

    static float Channel(uint word) => (word & ChannelSteps) * BrightestTint / ChannelSteps;
}

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
/// buffer and nothing to sort: two indexed draws, <b>the second the level above</b> (<see cref="GroundPart.Above"/>)
/// — the bridges over roads, drawn after the bodies on the ground and before the bodies on them.
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
/// a straight, whatever the fills either side of it were thinned to. <b>Each is laid as its stones</b>
/// (<see cref="Kerbstones"/>), a strip along its line. <b>And a kerb is the only edge of a layer anybody sees</b>
/// (<see cref="Line"/>, <see cref="HiddenShare"/>): the fill beneath it is the kerb's own line thinned by what the
/// stone hides, so what is cut for the picture is the line and what is cut for the kerb is the fill. <b>A rim is
/// the other way of drawing a line and is what is left where nothing has a boundary to strike one off</b> — the
/// region twice, a line's width apart, the outer in the line's shade and the inner laid over it in the surface's
/// own. A deck's edge is drawn that way, a deck being a ribbon about a road's own line. <b>The shore's edges are
/// fades</b> (<see cref="Fade"/>), the ground beside it faded out onto it: a fade is the one place the ground is
/// blended rather than laid opaque.
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
    public static readonly Vector3 Paint = new(2.6f, 2.6f, 2.5f);

    readonly List<GroundVertex> _vertices = [];
    readonly List<uint> _indices = [];

    readonly GroundTally[] _parts = new GroundTally[GroundParts.Count];

    /// <summary>
    /// Which corner already stands at a place, wearing a surface and a shade (<see cref="Vertex"/>), so a
    /// shape laid at a size and the same shape laid a line's width inside it do not each carry their own
    /// copy of the stations they agree on. It is laying scratch and is dropped once the ground is laid.
    /// </summary>
    Dictionary<(int X, int Y, uint Shade), int> _welds = [];

    /// <summary>
    /// Whether a corner asked for is shared with whatever already stands at it. <b>The ground is welded and
    /// the marks are not</b>: a mark is four corners read back as four corners.
    /// </summary>
    bool _welding = true;

    GroundMesh()
    {
    }

    /// <summary>What the ground was laid from, to lay its triangles again once they are let go (<see cref="ForgetTheTriangles"/>).</summary>
    (CityPlan Plan, SimConfig Config)? _laidFrom;

    bool _forgotten;

    /// <summary>
    /// <b>The triangles let go, once the device holds its own</b> — the host keeps no geometry it does not read (the
    /// owner, 2026-10-13). <see cref="Parts"/> and the figures stay; <b>the triangles asked for again are laid again</b>
    /// off the plan, which on a city is seconds and which only the debug wireframe asks.
    /// </summary>
    public void ForgetTheTriangles()
    {
        if (_laidFrom is null) return;

        _vertices.Clear();
        _vertices.TrimExcess();
        _indices.Clear();
        _indices.TrimExcess();
        _forgotten = true;
    }

    void LayAgainIfForgotten()
    {
        if (!_forgotten || _laidFrom is not var (plan, config)) return;

        var again = Build(plan, config);
        _vertices.AddRange(again.Vertices);
        _indices.AddRange(again.Indices);
        _forgotten = false;
    }

    public ReadOnlySpan<GroundVertex> Vertices
    {
        get
        {
            LayAgainIfForgotten();
            return CollectionsMarshal.AsSpan(_vertices);
        }
    }

    /// <summary>
    /// The period each surface's texture repeats over, grass, tarmac, pavement and deck, in metres from the figures
    /// config carries — what the vertex stage divides a corner's place by (<see cref="CameraView.SurfacePeriodsM"/>).
    /// </summary>
    public Vector4 SurfacePeriodsM { get; private init; } = Vector4.One;

    /// <summary>And the water's, the fifth surface, then the shore's (<see cref="Surface.Shore"/>).</summary>
    public Vector4 WaterPeriodM { get; private init; } = Vector4.One;

    /// <summary>The tint every surface takes from where it lies, from the same config (<see cref="CameraView.Patches"/>).</summary>
    public GroundPatches Patches { get; private init; }

    /// <summary>How tall a band of rows a frame draws the ground by is (<see cref="GroundBands"/>): the town's own grid's cell (SIM-8).</summary>
    public float BandM { get; private init; } = 1f;

    public ReadOnlySpan<uint> Indices
    {
        get
        {
            LayAgainIfForgotten();
            return CollectionsMarshal.AsSpan(_indices);
        }
    }

    /// <summary>
    /// Where the marks start, as an index into <see cref="Vertices"/>: everything from here to
    /// <see cref="FirstArrowVertex"/> is a dash, a bar, a zebra's stripe or a bay stroke, four corners at a time.
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
    /// glyph is a ribbon and a head, so a reader walking marks in fours reads between these two. The ground's
    /// arrows end where the level above begins (<see cref="GroundPart.Above"/>).
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
    /// And what reading the layers' lines off their rings cost — each walked finely for its kerb and thinned for its
    /// fill (<see cref="Line"/>, <see cref="Filled"/>) — which is laid before any part and belongs to none.
    /// </summary>
    public double LinesMs { get; private set; }

    /// <summary>
    /// And what placing the paint cost before a mark was laid — the kerb ends read off the boundary, the bands
    /// across them and the bars behind those — which belongs to no part either.
    /// </summary>
    public double EndsMs { get; private set; }

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
            mesh._vertices.Add(new GroundVertex(Vector2.Zero, Vector3.Zero, Surface.Grass));
            mesh._indices.Add((uint)corner);
        }

        // The one triangle is the world's own layer, so the parts still tile the mesh (<see cref="Parts"/>)
        // and a ground section opened over no town reads zeroes rather than a mesh nothing accounts for.
        mesh.Laid(GroundPart.Grass, grass);
        return mesh;
    }

    public static GroundMesh Build(CityPlan plan, SimConfig config)
    {
        var mesh = new GroundMesh
        {
            SurfacePeriodsM = new Vector4(
                config.View.GroundPeriodGrassM, config.View.GroundPeriodTarmacM, config.View.GroundPeriodPavementM,
                config.View.GroundPeriodDeckM),
            WaterPeriodM = new Vector4(config.View.GroundPeriodWaterM, config.View.GroundPeriodShoreM, 0f, 0f),
            Patches = GroundPatches.From(config.View),
            BandM = config.Grid.Main.CellM,
            _laidFrom = (plan, config),
        };
        var startedAt = Stopwatch.GetTimestamp();
        var worldM = plan.WorldSizeM;

        // <b>The pavement is a step and not a shape this pass works out for itself</b> (TER-3c): the walk
        // and the lines a car is driven on are the town's own (<see cref="Paving"/>). Derived again here,
        // the picture and the answer are two readings that have to be kept in step by whoever remembers.
        var paving = plan.Paving(config);

        // <b>One kerb's width and two kerbs struck at it</b>: where the carriageway hands over to the walk,
        // and where the walk hands over to the grass (TER-3c.3). It is the width the kerbstone is and not a
        // line's width, which is why it is the world's figure and not the paint's.
        var kerbM = config.Road.KerbWidthM;

        var grass = mesh.Starting();
        mesh.Rect(Vector2.Zero, plan.WorldSizeM, Surface.Grass, Plain);
        mesh.Laid(GroundPart.Grass, grass);

        // <b>The track at grade under all the town's ground</b> (RAIL-3, TER-7b): laid whole, so a road and its walk crossing
        // it are laid over it, and its rails are laid again over them (GroundPart.Rails).
        var tracks = TrackLine.Of(plan.Rails);
        var track = mesh.Starting();
        mesh.Track(tracks, config, worldM, bridged: false);
        mesh.Laid(GroundPart.Track, track);

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
        // <b>Cut to the map before either is read</b> (GEN-2b): a road running off it carries its ground on past the
        // edge (<see cref="LaneShell"/>), and what the map keeps is that ground cut there, with no kerb struck along
        // the cut.
        var linesFrom = Stopwatch.GetTimestamp();
        var walkLine = MapCut.Rings(Line(rings.Walk.Rings), worldM);
        var carriagewayLine = MapCut.Rings(Line(rings.Carriageway.Rings), worldM);
        var walkFill = Filled(walkLine, kerbM);
        var carriagewayFill = Filled(carriagewayLine, kerbM);

        // <b>And both cut into triangles side by side</b>: each is one outer ring holding all its holes, which no
        // pass splits, and the two share nothing (<see cref="InChunks.Both{TFirst, TSecond}"/>).
        var (walkShell, carriagewayShell) = InChunks.Both(
            () => ShellFill.Of(walkFill), () => ShellFill.Of(carriagewayFill));
        mesh.LinesMs = Stopwatch.GetElapsedTime(linesFrom).TotalMilliseconds;

        // <b>The pavement and the kerb along its outer face, under the water and the decks</b> (TER-7b): a
        // walk that reaches a shore is ground the water then covers, so what lies outside the carriageway is
        // laid before it and not over it. <b>The walk is the offset filled whole</b> and the driven ground it
        // covers is covered back by the carriageway below, which is what a layer enclosing every layer inside
        // it means and what spares this fill a second copy of the boundary to carry and thin.
        // <b>The line is struck along the walk's own outer face and not cut out of the fill</b> (TER-3d): it
        // is a kerb's width about that line wherever it runs, which is what the thinning either fill is laid
        // at cannot promise, and it is laid after the fill so the fill cannot eat into it. <b>Laid as its
        // edging stones</b> (<see cref="Surface.Edging"/>), their shadow cast on the grass.
        var walk = mesh.Starting();
        mesh.Shell(walkShell, Surface.Pavement, Plain);
        mesh.Laid(GroundPart.Walk, walk);

        var walkKerb = mesh.Starting();
        mesh.KerbShadow(walkLine, OnTheEdge, kerbM, config.View.KerbShadowM, config.View.KerbShadowShare, onTheRight: false);
        mesh.Kerbstones(walkLine, OnTheEdge, kerbM, Surface.Edging, KerbLooks.Shared.Edging.PictureM);
        mesh.Laid(GroundPart.WalkKerb, walkKerb);

        // The shore and the water set in it (GEN-2c), <b>each edge a fade and not a line</b> (<see cref="Fade"/>): the
        // grass fades out onto the shore from its outer edge, and the water out onto it from its own, so both lie on
        // the shore and neither reaches past it onto a walk.
        var water = mesh.Starting();
        var fadeM = config.View.ShoreFadeM;
        Water(mesh, plan.Water.Shore, Surface.Shore, Plain);
        mesh.Fade(plan.Water.Shore, OnTheEdge, fadeM, inside: true, Surface.Grass);
        Water(mesh, plan.Water.Outline, Surface.Water, Plain);
        mesh.Fade(plan.Water.Outline, OnTheEdge, fadeM, inside: false, Surface.Water);
        mesh.Laid(GroundPart.Water, water);

        // A deck is drawn out to its own half-width, with a rim rather than a stroke: the piece at full size
        // in the edge shade, then a line's width smaller in its own. A ribbon about a road's line has no
        // shell of its own to strike a kerb along, which is the one place a rim is what is left.
        // <b>And nothing but the deck</b> — a pavement carried across one at the width it has on land would be
        // a line beside a road struck by arithmetic of its own (TER-3c.3), so the margin outside the
        // carriageway is deck all the way out. <b>The ground's decks only</b>:
        // a bridge over other roads is drawn over them (<see cref="Above"/>).
        var decks = mesh.Starting();
        mesh.Decks(plan, paving.Lanes, CityPlan.RoadArrays.Ground, config);
        mesh.Laid(GroundPart.Decks, decks);

        // <b>The carriageway at its own size</b> (TER-7b): the boundary filled as the shape it is, over the
        // walk that reaches under it, and struck after the decks because a bridge carries its carriageway
        // over its own surface and not over the land's.
        var carriageway = mesh.Starting();
        mesh.Shell(carriagewayShell, Surface.Tarmac, Plain);
        mesh.Laid(GroundPart.Carriageway, carriageway);

        // <b>Between the stroke and the carriageway, the tarmac that is not a road</b>: a slab. It is where
        // the answer puts it (<c>GroundShapes.At</c>).
        var slabs = mesh.Starting();
        for (var slab = 0; slab < plan.PavedAreas.Count; slab++)
        {
            mesh.Rect(plan.PavedAreas.MinM[slab], plan.PavedAreas.SizeM[slab], Surface.Tarmac, Plain);
        }

        mesh.Laid(GroundPart.Slabs, slabs);

        // <b>Then the town's kerb, which is a line and not a layer</b> (TER-3d, TER-7b): a kerb's width laid
        // about the boundary itself, over everything the layers left along it. Last of the ground, so what it
        // covers is a kerb wherever the boundary runs — and so the place the walk meets the carriageway, each
        // of them thinned on its own terms, is under the middle of it rather than beside it.
        // <b>Laid as its kerbstones</b> (<see cref="Surface.Kerb"/>), the riser on the carriageway's side and its shadow
        // cast on the road.
        var kerb = mesh.Starting();
        mesh.KerbShadow(carriagewayLine, OnTheEdge, kerbM, config.View.KerbShadowM, config.View.KerbShadowShare, onTheRight: true);
        mesh.Kerbstones(carriagewayLine, OnTheEdge, kerbM, Surface.Kerb, KerbLooks.Shared.Kerb.PictureM);
        mesh.Laid(GroundPart.Kerb, kerb);

        // <b>Then the paint, which is the one layer above the ground rather than in it</b> (TER-7b): a mark
        // sits on the surface it belongs to, and marks are not welded — a dash is read back out of the mesh
        // as the four corners it was laid as (<see cref="FirstMarkVertex"/>).
        // <b>And the whole of it is what the lanes and the roads say</b>: the lines between two ribbons, the
        // zebra at the end of every arm a junction forks at — or where the town says its zebras are
        // (CityPlan.ZebraAtEveryStation) — and the bar behind each station.
        mesh._welding = false;
        mesh._welds = [];

        // <b>Where the walk meets the road is the walk's to say</b> (WLK-10): the places a walk beside a road
        // is cut are the two kerbs a band reaches between, so every mark here is asked for by the town's own
        // kerb ends (<see cref="CityGen.KerbEnds"/>) rather than read off the roads a second time.
        // <b>And the walk has two answers to give</b>: a street too short to be crossed twice is crossed once
        // in the middle while still being held at both of its ends, so the zebra is laid off where the walk
        // crosses and the bar — with the dashes that stop short of it — off what each arm holds behind,
        // which there is the end of the road's own kerb (WLK-10a).
        var endsFrom = Stopwatch.GetTimestamp();
        var ends = paving.RoadEnds(config);
        var crossed = Crossings.Lay(plan, config, ends.CrossedM);
        var held = Crossings.Lay(plan, config, ends.HeldM);

        // <b>The bars are laid once and read twice</b>: the paint a driver holds at, and the arrows behind it
        // that say what they are holding for (TER-6a). Laid again for the arrows, the two would be free to
        // disagree about where the bar stands.
        var bars = StopBars.Lay(paving.Lanes, held, config, crossed);
        var arrows = LaneArrows.Lay(paving.Lanes, bars, crossed, config);
        var runs = CentrelineRuns.Lay(plan, config);
        var surveyed = Crossings.Of(plan);
        mesh.EndsMs = Stopwatch.GetElapsedTime(endsFrom).TotalMilliseconds;

        // <b>Each level's paint with its own carriageway</b> (TER-7b): what is painted on a bridge over a road is
        // laid with the bridge, over the bodies on the ground, and what is painted on the road under it is not.
        const byte ground = CityPlan.RoadArrays.Ground;
        var marks = mesh.Starting();

        mesh.FirstMarkVertex = mesh._vertices.Count;
        mesh.LaneDashes(runs, plan, config, held, crossed, Paint, ground);
        mesh.RoadsideLines(plan, config, held, crossed, Paint, ground);
        mesh.BayStrokes(plan, config, Paint);
        mesh.Zebras(crossed, plan, config, Paint, ground);
        mesh.Zebras(surveyed, plan, config, Paint, ground);
        mesh.Bars(bars, paving.Lanes, Paint, ground);
        mesh.Bars(CrossingBars(plan, paving.Lanes, config), paving.Lanes, Paint, ground);

        mesh.FirstArrowVertex = mesh._vertices.Count;
        mesh.Arrows(paving.Lanes, arrows, config, Paint, ground);
        mesh.Laid(GroundPart.Paint, marks);

        // <b>Then the rails over every road, walk and mark crossing them</b> (RAIL-3, TER-7b): what a train is borne on is
        // over the ground it crosses and the paint on it.
        var rails = mesh.Starting();
        mesh.Rails(tracks, config, worldM, bridged: false);
        mesh.Laid(GroundPart.Rails, rails);

        // <b>A rail bridge is of the level above</b> (RAIL-8, PHY-1a): its deck, track and rails over the bodies on the
        // ground, as a road's bridge is, and under the bodies on it.
        var above = mesh.Starting();
        mesh.RailDecks(tracks, config);
        mesh.Track(tracks, config, worldM, bridged: true);
        mesh.Rails(tracks, config, worldM, bridged: true);
        mesh.Above(plan, paving, config, held, crossed, bars, arrows, runs, surveyed);
        mesh.Laid(GroundPart.Above, above);

        // Grown by doubling and kept for the whole run, the two lists would keep up to as much again as they hold.
        mesh._vertices.TrimExcess();
        mesh._indices.TrimExcess();

        mesh.LaidMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        return mesh;

        bool OnTheEdge(Vector2 fromM, Vector2 toM) => MapCut.AlongTheEdge(fromM, toM, worldM);
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
        GroundMesh mesh, CityPlan.RingArrays rings, Surface surface, Vector3 tint)
    {
        for (var ring = 0; ring < rings.Count; ring++) mesh.Polygon(rings.RingOf(ring), surface, tint);
    }
}
