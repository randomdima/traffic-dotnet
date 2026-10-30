using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

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
/// <para>
/// <b>A walk that ends is turned back at its end</b> (GEN-75): the walk is struck off the walked roads alone
/// (<see cref="Paving.WalkedCourses"/>), so where one ends at a road with no walk its courses close round the end over that
/// road's carriageway. That stretch of each ring is walked by nobody (<see cref="GapsOf"/>), and a walker reaching it
/// steps across to the other lane and back the way they came (<see cref="Turns"/>) — or, where a crossing stands just
/// short of it, the walk runs straight onto the paint instead (<see cref="EndedAtTheCrossings"/>).
/// </para>
/// </remarks>
internal sealed class PavementLanes
{
    /// <summary>A town with no pavement, which is walked nowhere.</summary>
    public static readonly PavementLanes None = new([], [], [], [], [], []);

    readonly ArcSeg[][][] _rings;
    readonly ArcSeg[][][] _loose;
    readonly float[] _offsetM;
    readonly bool[] _runsWithTheRing;
    readonly (float FromM, float ToM)[][][] _gaps;
    readonly (Vector2 OneM, Vector2 OtherM)[] _turns;

    PavementLanes(
        ArcSeg[][][] rings, ArcSeg[][][] loose, float[] offsetM, bool[] runsWithTheRing, (float FromM, float ToM)[][][] gaps,
        (Vector2 OneM, Vector2 OtherM)[] turns)
    {
        _rings = rings;
        _loose = loose;
        _offsetM = offsetM;
        _runsWithTheRing = runsWithTheRing;
        _gaps = gaps;
        _turns = turns;
    }

    /// <summary>Lays every lane of the town's pavement. Build-time only — it allocates freely.</summary>
    public static PavementLanes Of(CityPlan plan, SimConfig config)
    {
        var paving = plan.Paving(config);
        var lanes = Of(paving.WalkedCourses(config), config);
        return paving.Of.Roads.Unwalked.Length == 0 ? lanes : lanes.EndedAt(paving.Rings(config).Carriageway.Rings, config);
    }

    /// <summary>The same, off a merge somebody already holds.</summary>
    public static PavementLanes Of(BandShell shell, SimConfig config)
    {
        var count = SimConfig.LanesPerPavement;
        var rings = new ArcSeg[count][][];
        var loose = new ArcSeg[count][][];
        var offsetM = new float[count];
        var runsWithTheRing = new bool[count];

        // Each lane's move reads the shell and writes its own slots, so the lanes are moved side by side.
        InChunks.Over(count, static () => 0, (_, lane) =>
        {
            offsetM[lane] = config.WalkingLaneAtM(lane);
            (rings[lane], loose[lane]) = shell.Outset(
                offsetM[lane], config.Road.WalkRoundedM, ArcOutset.Corners.Filled);

            // The lane against the kerb keeps the hand the traffic beside it keeps, and the one beyond it
            // takes the other: a pair walked both ways is what makes a pavement two-way (WLK-8).
            runsWithTheRing[lane] = (lane == 0) == (config.RoadSideSign > 0f);
        });

        var gaps = new (float FromM, float ToM)[count][][];
        for (var lane = 0; lane < count; lane++) gaps[lane] = new (float, float)[rings[lane].Length][];
        foreach (var laneGaps in gaps) Array.Fill(laneGaps, []);
        return new PavementLanes(rings, loose, offsetM, runsWithTheRing, gaps, []);
    }

    /// <summary>
    /// <b>The same lanes, ended where they leave the walk</b> (GEN-75): every stretch of a ring standing on the driven
    /// ground or nearer its edge than the ring's own offset is a gap, and each end of a gap on one lane is joined to the
    /// nearest end of a gap on the other by a turn — as long as the gap between the two lanes, and no longer.
    /// </summary>
    /// <param name="carriageway">The whole town's driven ground (<see cref="GroundRings.Carriageway"/>).</param>
    PavementLanes EndedAt(ArcSeg[][] carriageway, SimConfig config)
    {
        var sides = RingSides.Of(carriageway, config.ShellLevel);
        var kerb = ChainIndex.OfPieces(ArcRings.Flat(carriageway), config.Grid.Main);
        var scan = kerb.NewScan();
        var stepM = config.WalkingLaneWidthM * StepsAWalkingLane;
        var gaps = new (float FromM, float ToM)[_rings.Length][][];
        var ends = new List<Vector2>[_rings.Length];
        for (var lane = 0; lane < _rings.Length; lane++)
        {
            var clearM = _offsetM[lane] - LineTolerance.OnePlaceM;
            bool Off(Vector2 atM) =>
                sides.Encloses(atM) || kerb.Nearest(scan, atM, clearM, out _, out _) >= 0;

            gaps[lane] = new (float, float)[_rings[lane].Length][];
            ends[lane] = [];
            for (var ring = 0; ring < _rings[lane].Length; ring++)
            {
                var line = _rings[lane][ring];
                var lengthM = Spline.TotalLengthM(line);
                gaps[lane][ring] = Gaps(line, stepM, Off);

                // A ring's own start is no end: a gap over it is the one reported twice.
                foreach (var (fromM, toM) in gaps[lane][ring])
                {
                    if (fromM > 0f) ends[lane].Add(Spline.SampleAt(line, fromM).PositionM);
                    if (toM < lengthM) ends[lane].Add(Spline.SampleAt(line, toM).PositionM);
                }
            }
        }

        return new PavementLanes(_rings, _loose, _offsetM, _runsWithTheRing, gaps, Turned(ends, config));
    }

    /// <summary>
    /// <b>The same lanes, each walk that ends past a crossing ended where it runs onto the paint</b> (GEN-75,
    /// <see cref="CrossingWays.EndsOn"/>): no stretch past it walked only to turn back, and no turn there — the walk is
    /// a straight way onto the crossing and off it, and does not fork.
    /// </summary>
    /// <remarks>
    /// After the crossings and not in <see cref="EndedAt"/>: where a crossing hands over is read off these lanes' own gaps,
    /// so the walk's end the crossing meets is the one laid before this. <b>The end met is the first the way runs into</b>:
    /// the crossing found it there before it handed over (<see cref="CrossingWays.EndsOn"/>).
    /// </remarks>
    public PavementLanes EndedAtTheCrossings(CrossingWays crossings, SimConfig config)
    {
        if (_turns.Length == 0) return this;

        var gaps = new (float FromM, float ToM)[_rings.Length][][];
        var ends = new List<Vector2>[_rings.Length];
        for (var lane = 0; lane < _rings.Length; lane++)
        {
            gaps[lane] = new (float, float)[_rings[lane].Length][];
            ends[lane] = [];
            for (var ring = 0; ring < _rings[lane].Length; ring++)
            {
                var line = _rings[lane][ring];
                var lengthM = Spline.TotalLengthM(line);
                var was = _gaps[lane][ring];
                var pulled = was.ToArray();
                var (fromOnto, toOnto) = (new bool[was.Length], new bool[was.Length]);
                foreach (var (alongM, way) in crossings.EndsOn(lane, ring))
                {
                    var at = FirstEnd(was, alongM, way, lengthM);
                    if (at < 0) continue;

                    if (way > 0f) (pulled[at].FromM, fromOnto[at]) = (alongM, true);
                    else (pulled[at].ToM, toOnto[at]) = (alongM, true);
                }

                for (var at = 0; at < was.Length; at++)
                {
                    if (was[at].FromM > 0f && !fromOnto[at]) ends[lane].Add(Spline.SampleAt(line, was[at].FromM).PositionM);
                    if (was[at].ToM < lengthM && !toOnto[at]) ends[lane].Add(Spline.SampleAt(line, was[at].ToM).PositionM);
                }

                gaps[lane][ring] = pulled;
            }
        }

        return new PavementLanes(_rings, _loose, _offsetM, _runsWithTheRing, gaps, Turned(ends, config));

        // The gap whose end a walk from a place runs into first, the way along the ring it runs — its start ahead, or its
        // end behind — or −1. A ring's own start is no end (EndedAt).
        static int FirstEnd(ReadOnlySpan<(float FromM, float ToM)> gaps, float alongM, float way, float lengthM)
        {
            var first = -1;
            var firstM = float.PositiveInfinity;
            for (var at = 0; at < gaps.Length; at++)
            {
                var endM = way > 0f ? gaps[at].FromM : gaps[at].ToM;
                if (endM <= 0f || endM >= lengthM) continue;

                var aheadM = (endM - alongM) * way;
                if (aheadM < 0f || aheadM >= firstM) continue;

                (first, firstM) = (at, aheadM);
            }

            return first;
        }
    }

    /// <summary>
    /// <b>The same lanes with every stretch where <paramref name="off"/> holds walked by nobody too</b> — its own gaps
    /// beside those already there, and no turn at them: what joins their ends is the caller's (<see cref="DeckWalks"/>).
    /// </summary>
    /// <param name="off">Whether a point of a lane's ring is walked by nobody, the lane given.</param>
    public PavementLanes GappedWhere(Func<int, Vector2, bool> off, SimConfig config)
    {
        var stepM = config.WalkingLaneWidthM * StepsAWalkingLane;
        var gaps = new (float FromM, float ToM)[_rings.Length][][];
        for (var lane = 0; lane < _rings.Length; lane++)
        {
            gaps[lane] = new (float, float)[_rings[lane].Length][];
            for (var ring = 0; ring < _rings[lane].Length; ring++)
            {
                var onLane = lane;
                var more = Gaps(_rings[lane][ring], stepM, atM => off(onLane, atM));
                gaps[lane][ring] = more.Length == 0 ? _gaps[lane][ring] : Merged(_gaps[lane][ring], more);
            }
        }

        return new PavementLanes(_rings, _loose, _offsetM, _runsWithTheRing, gaps, _turns);
    }

    /// <summary>Two runs of stretches along one ring, each in order, as the one run in order they cover together.</summary>
    static (float FromM, float ToM)[] Merged(ReadOnlySpan<(float FromM, float ToM)> one, ReadOnlySpan<(float FromM, float ToM)> other)
    {
        var all = new List<(float FromM, float ToM)>(one.Length + other.Length);
        foreach (var gap in one) all.Add(gap);
        foreach (var gap in other) all.Add(gap);
        all.Sort(static (a, b) => a.FromM.CompareTo(b.FromM));

        var merged = new List<(float FromM, float ToM)>(all.Count);
        foreach (var gap in all)
        {
            if (merged.Count > 0 && gap.FromM <= merged[^1].ToM) merged[^1] = (merged[^1].FromM, MathF.Max(merged[^1].ToM, gap.ToM));
            else merged.Add(gap);
        }

        return [.. merged];
    }

    /// <summary>The turns across the walk, from each of one lane's ends to the other's beside it.</summary>
    (Vector2 OneM, Vector2 OtherM)[] Turned(List<Vector2>[] ends, SimConfig config)
    {
        var turns = new List<(Vector2, Vector2)>();
        var acrossM = MathF.Abs(_offsetM[^1] - _offsetM[0]) + config.WalkingLaneWidthM;
        if (_rings.Length > 1 && ends[1].Count > 0)
        {
            foreach (var endM in ends[0])
            {
                var nearestM = ends[1].MinBy(otherM => Vector2.DistanceSquared(endM, otherM));
                if (Vector2.Distance(endM, nearestM) <= acrossM) turns.Add((endM, nearestM));
            }
        }

        return [.. turns];
    }

    /// <summary>How many steps a ring is read in across a walking lane's width, before each change is halved down to.</summary>
    const float StepsAWalkingLane = 0.25f;

    /// <summary>How many times a step a change was seen in is halved, which puts the gap's end inside a millimetre of it.</summary>
    const int Halvings = 10;

    /// <summary>
    /// The stretches of one closed ring where <paramref name="off"/> holds, in order along it from its start — one
    /// running past the start reported as two, the one at its end and the one at its start.
    /// </summary>
    static (float FromM, float ToM)[] Gaps(ArcSeg[] ring, float stepM, Func<Vector2, bool> off)
    {
        var lengthM = Spline.TotalLengthM(ring);
        var gaps = new List<(float, float)>();
        var wasOff = off(ring[0].StartM);
        var openedM = wasOff ? 0f : -1f;
        for (var atM = stepM; ; atM += stepM)
        {
            var hereM = MathF.Min(atM, lengthM);
            var isOff = off(Spline.SampleAt(ring, hereM).PositionM);
            if (isOff != wasOff)
            {
                var changedM = Changed(ring, hereM - stepM, hereM, wasOff, off);
                if (isOff) openedM = changedM;
                else gaps.Add((openedM, changedM));
                wasOff = isOff;
            }

            if (hereM >= lengthM) break;
        }

        if (wasOff) gaps.Add((openedM, lengthM));
        return [.. gaps];
    }

    static float Changed(ArcSeg[] ring, float fromM, float toM, bool fromOff, Func<Vector2, bool> off)
    {
        for (var halving = 0; halving < Halvings; halving++)
        {
            var middleM = (fromM + toM) * 0.5f;
            if (off(Spline.SampleAt(ring, middleM).PositionM) == fromOff) fromM = middleM;
            else toM = middleM;
        }

        return (fromM + toM) * 0.5f;
    }

    /// <summary>
    /// <b>The stretches of one ring of one lane nobody walks</b> (GEN-75), in metres along it and in order: where the
    /// walk it is a course of ends at a road with no walk, and the ring closes round that end over the road. None where
    /// every road is walked.
    /// </summary>
    public ReadOnlySpan<(float FromM, float ToM)> GapsOf(int lane, int ring) => _gaps[lane][ring];

    /// <summary>
    /// <b>Where a walker at the end of a walk steps across to the other lane</b> (GEN-75): each a lane's gap end and the
    /// other lane's nearest, walked either way.
    /// </summary>
    public ReadOnlySpan<(Vector2 OneM, Vector2 OtherM)> Turns => _turns;

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
