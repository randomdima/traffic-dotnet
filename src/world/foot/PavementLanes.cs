using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Foot;

/// <summary>
/// <b>The town's pavement, as the lanes it is walked down</b> (WLK-1, WLK-8): the driven ground's boundary
/// moved off itself once per walking lane, each closed line of each move being one lane, walked one way.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two moves are the whole town's pavement.</b> A boundary has the road between its two hands, so the
/// move keeps both of them: one offset lays the line down either kerb of every road at once, round every
/// block, round the mouth of every rank and round the outside of a ring. Nothing here asks about a road —
/// there is no end to stand a corner on, no arm to count and nothing to exclude.
/// </para>
/// <para>
/// <b>A lane is made a lane here and is not cut out of anything afterwards.</b> The move's own answer is
/// the line a walker is held on, so there is no second offset between the shape and the body — and the
/// second offset is the one that cannot work: <see cref="ArcOutset.Beside"/> moves a stretch against the
/// pieces either side of it alone, so it folds through itself wherever the shape swallowed more than that,
/// while <see cref="BandShell.Outset"/> is cut against every piece of the town.
/// </para>
/// <para>
/// <b>And rounded at the courses' own radius</b> (<see cref="RoadFigures.WalkRoundedM"/>, TER-3c.10), which
/// is not the one the ground's layers are struck at: a course is a line a body is held on rather than a
/// thing the town is built of, so what it owes is a walk nobody has to pick their way round. <b>The notches
/// a fold left are filled and no corner is cut</b> (<see cref="ArcOutset.Corners.Filled"/>) — a corner the
/// town turns away at comes back as the arc of the distance moved however wide the roll is asked for, so no
/// radius moves a walk towards the tarmac and none of them has a bound.
/// </para>
/// <para>
/// <b>The two lanes of a pavement are walked opposite ways</b> (WLK-8, TER-4a). Every ring here is wound
/// with the driven ground on its right (TER-3c.9), so the lane against the kerb is walked the way its ring
/// runs in a town that keeps right and against it in one that keeps left, and the lane beyond it takes the
/// other. It is one statement about which hand the tarmac is on, made once for the town.
/// </para>
/// <para>
/// <b>What the move owes is closure</b>, and what it could not close is kept apart rather than dropped
/// (<see cref="LooseOf"/>): a course that came back as a run is a pavement with two ends in the middle of
/// the town, which is a fault to look at and not a lane to walk.
/// </para>
/// </remarks>
internal sealed class PavementLanes
{
    /// <summary>A town with no pavement, which is walked nowhere.</summary>
    public static readonly PavementLanes None = new([], [], [], []);

    readonly ArcSeg[][][] _rings;
    readonly ArcSeg[][][] _loose;
    readonly float[] _offsetM;
    readonly bool[] _runsWithTheRing;

    PavementLanes(ArcSeg[][][] rings, ArcSeg[][][] loose, float[] offsetM, bool[] runsWithTheRing)
    {
        _rings = rings;
        _loose = loose;
        _offsetM = offsetM;
        _runsWithTheRing = runsWithTheRing;
    }

    /// <summary>Lays every lane of the town's pavement. Build-time only — it allocates freely.</summary>
    public static PavementLanes Of(CityPlan plan, SimConfig config) =>
        Of(plan.Paving(config).Perimeter(config), config);

    /// <summary>The same, off a merge somebody already holds.</summary>
    public static PavementLanes Of(BandShell shell, SimConfig config)
    {
        var count = SimConfig.LanesPerPavement;
        var rings = new ArcSeg[count][][];
        var loose = new ArcSeg[count][][];
        var offsetM = new float[count];
        var runsWithTheRing = new bool[count];
        for (var lane = 0; lane < count; lane++)
        {
            offsetM[lane] = config.WalkingLaneAtM(lane);
            (rings[lane], loose[lane]) = shell.Outset(
                offsetM[lane], config.Road.WalkRoundedM, ArcOutset.Corners.Filled);

            // The lane against the kerb keeps the hand the traffic beside it keeps, and the one beyond it
            // takes the other: a pair walked both ways is what makes a pavement two-way (WLK-8).
            runsWithTheRing[lane] = (lane == 0) == (config.RoadSideSign > 0f);
        }

        return new PavementLanes(rings, loose, offsetM, runsWithTheRing);
    }

    /// <summary>How many lanes a pavement is walked at, which is how many moves the town's pavement is.</summary>
    public int Count => _rings.Length;

    /// <summary>How far off the driven ground this lane's own line stands.</summary>
    public float OffsetM(int lane) => _offsetM[lane];

    /// <summary>The closed lines this lane's move came back with, each of them one lane of pavement.</summary>
    public ReadOnlySpan<ArcSeg[]> RingsOf(int lane) => _rings[lane];

    /// <summary>And what the move could not close, which is a fault of the shape and is walked by nobody.</summary>
    public ReadOnlySpan<ArcSeg[]> LooseOf(int lane) => _loose[lane];

    /// <summary>
    /// Whether this lane is walked the way its rings are wound, which is what puts the tarmac on the hand
    /// the town keeps (WLK-8, TER-4a).
    /// </summary>
    public bool RunsWithTheRing(int lane) => _runsWithTheRing[lane];
}
