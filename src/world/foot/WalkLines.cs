using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Foot;

/// <summary>
/// <b>The lines a pedestrian lane is walked on, as the driven ground's boundary moved off itself once per
/// lane</b> (WLK-11, TER-7b): one course per lane of a pavement, each the whole town's boundary at that
/// lane's own distance off it (<see cref="SimConfig.WalkingLaneAtM"/>), indexed and walkable exactly as the
/// boundary is (<see cref="KerbLines"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The move is the whole shape's and never a stretch of it</b> (<see cref="BandShell.Outset"/>). A
/// shape's offset is cut against every piece of the shape, because a feature anywhere may swallow an offset
/// anywhere: two kerbs nearer than twice the distance come back as one line, a fillet tighter than the
/// distance comes back as nothing, and the ring count that goes in has nothing to do with the one that comes
/// out. <b>A stretch offset on its own cannot know any of that</b> — it can only cut a fold against the
/// pieces that made it (<see cref="ArcOutset.Beside"/>), so a corner whose radius the offset swallows folds
/// into a knot of chords laid over the tarmac.
/// </para>
/// <para>
/// <b>So a way between two points is a stretch of a course and not a stretch of the boundary moved.</b> The
/// offset is taken once for the town, the two points are dropped onto the course they belong to, and what
/// runs between them is the pieces of it between those two places. It is the same construction the ground
/// beside a road is struck with (<c>CityGen.GroundRings</c>) read at two more figures.
/// </para>
/// <para>
/// <b>Every corner of a course is rounded, at the courses' own radius</b>
/// (<see cref="RoadFigures.WalkRoundedM"/>, TER-3c.10). <b>A corner turning in is a notch the fold cut
/// left</b>, and left sharp it is a spike of course standing in open pavement — a walk following it goes out
/// to the point and back rather than round the corner, which is a line nobody walks. A corner turning away
/// is the arc of the distance moved and is already wider than this roll, so the ball goes straight round it
/// and the walk is never pulled towards the tarmac. <b>It is not the figure the ground's own layers are
/// struck at</b> (<see cref="RoadFigures.LineRoundedM"/>): a course is a line a body is held on rather than
/// a thing the town is built of, and nothing is laid along it to disagree with.
/// </para>
/// <para>
/// <b>It exists only while a town is being laid</b>, as the ways struck off it do
/// (<see cref="FootWays"/>).
/// </para>
/// </remarks>
internal sealed class WalkLines
{
    readonly KerbLines[] _courses;
    readonly ArcSeg[][][] _rings;
    readonly ArcSeg[][][] _loose;

    WalkLines(KerbLines boundary, KerbLines[] courses, ArcSeg[][][] rings, ArcSeg[][][] loose)
    {
        Boundary = boundary;
        _courses = courses;
        _rings = rings;
        _loose = loose;
    }

    /// <summary>
    /// <b>The boundary all of them were struck off</b> (<see cref="KerbLines"/>), which is what settles a
    /// question two courses can answer differently: every course is cut against the whole town at its own
    /// distance, so which side of a block a way runs down is read here and carried onto them.
    /// </summary>
    public KerbLines Boundary { get; }

    /// <summary>Moves the town's boundary off itself once per walking lane. Build-time only — it allocates freely.</summary>
    public static WalkLines Of(CityPlan plan, SimConfig config) =>
        Of(plan.Paving(config).Perimeter(config), config);

    /// <summary>The same, off a merge somebody already holds.</summary>
    public static WalkLines Of(BandShell shell, SimConfig config)
    {
        var courses = new KerbLines[FootConnectors.LanesPerWay];
        var rings = new ArcSeg[courses.Length][][];
        var loose = new ArcSeg[courses.Length][][];
        for (var lane = 0; lane < courses.Length; lane++)
        {
            (rings[lane], loose[lane]) = shell.Outset(
                config.WalkingLaneAtM(lane), config.Road.WalkRoundedM, ArcOutset.Corners.Filled);
            courses[lane] = KerbLines.Of(rings[lane], loose[lane], config);
        }

        return new WalkLines(KerbLines.Of(shell, config), courses, rings, loose);
    }

    /// <summary>How many courses there are, which is how many lanes a pavement is walked at (WLK-8).</summary>
    public int CourseCount => _courses.Length;

    /// <summary>The course one lane of a pavement is walked on.</summary>
    public KerbLines Course(int lane) => _courses[lane];

    /// <summary>
    /// One course as the lines the move closed, for a reader that wants the shape rather than a place on it
    /// (<see cref="KerbLines"/> is the same lines indexed).
    /// </summary>
    public ReadOnlySpan<ArcSeg[]> RingsOf(int lane) => _rings[lane];

    /// <summary>And what the move could not close, which is a fault of the shape and is drawn as one.</summary>
    public ReadOnlySpan<ArcSeg[]> LooseOf(int lane) => _loose[lane];
}
