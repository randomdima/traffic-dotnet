using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Foot;

/// <summary>
/// <b>The lines a walk is turned through a place on</b> (WLK-13): one per lane arriving at a place and lane
/// setting off from it, which is what makes a corner somewhere a walk goes <em>through</em> rather than a
/// place several walks happen to end at.
/// </summary>
/// <remarks>
/// <para>
/// <b>A movement is what the car side calls one</b> (<c>CityGen.Paving.ArcsOfMovement</c>): the ways are the
/// stretches between places and these are the turns within one, so between them every walk in the town is a
/// run of lines that meet end to end. <b>Per place and not per node</b> (WLK-3) — two nodes at one corner are
/// one place to arrive at, so a walk arriving off either of them may leave by any way either of them hands
/// over.
/// </para>
/// <para>
/// <b>A turn back down the way it came is not one.</b> Every other pair of an arrival and a departure is,
/// including the pair that leaves by the other way of the same kind at a merged corner — what is left out is
/// only the walker who arrives and immediately retraces the lane beside the one they came in on.
/// </para>
/// <para>
/// <b>A movement runs along the course its departing lane is walked on</b> (<see cref="WalkLines"/>, as
/// WLK-11 lays a way), so a walk carrying on round a corner follows the kerb round it rather than cutting
/// across the mouth. <b>And it is joined to its own two ends by a curve no tighter than the circle the feet
/// hold at pace</b> (<see cref="SimConfig.WalkerTightestTurnM"/>, <see cref="Spline.CorneredInto"/>): a walk
/// meeting its course at an angle is a walk that stops and pivots, which costs a walker time they need not
/// spend. <b>Where no such curve joins the two, the straight between them is laid and
/// <see cref="Held"/> says so.</b>
/// </para>
/// <para>
/// <b>It exists only while a town is being laid</b>, as everything under it does. Nothing on a tick holds
/// one: what a body is held on is the walking network's own (<see cref="WalkingNetwork"/>).
/// </para>
/// </remarks>
internal sealed class FootMovements
{
    /// <summary>A town with no pavement, which has no place to be turned through.</summary>
    public static readonly FootMovements None = new(FootWays.None, [], [], [0], [], [], 0);

    readonly FootWays _ways;

    /// <summary>The lane arriving and the lane setting off, packed as one entry each.</summary>
    readonly int[] _fromLane;

    readonly int[] _ontoLane;

    /// <summary>Where each movement's chain begins in <see cref="_arcs"/>, one more entry than movements.</summary>
    readonly int[] _arcOffsets;

    readonly ArcSeg[] _arcs;
    readonly bool[] _held;

    FootMovements(
        FootWays ways, int[] fromLane, int[] ontoLane, int[] arcOffsets, ArcSeg[] arcs, bool[] held,
        int refused)
    {
        _ways = ways;
        _fromLane = fromLane;
        _ontoLane = ontoLane;
        _arcOffsets = arcOffsets;
        _arcs = arcs;
        _held = held;
        Refused = refused;
    }

    /// <summary>
    /// <b>How many movements the places would have had and do not</b>, because no line joined the two lanes
    /// without a pivot at one end of it (WLK-14, <see cref="Smooth"/>). <b>It is the cost of that rule and
    /// the instruments' to report</b>, alongside the places nothing turns through at all.
    /// </summary>
    public int Refused { get; }

    /// <summary>How many turns the town's places are walked through on.</summary>
    public int Count => _fromLane.Length;

    /// <summary>The ways these turns run between, and through them the points and the nodes under those.</summary>
    public FootWays Ways => _ways;

    /// <summary>The way a movement arrives on, and which lane of it.</summary>
    public int FromWay(int movement) => _fromLane[movement] / FootConnectors.LanesPerWay;

    /// <inheritdoc cref="FromWay"/>
    public int FromLane(int movement) => _fromLane[movement] % FootConnectors.LanesPerWay;

    /// <summary>And the way it sets off down, and which lane of that.</summary>
    public int OntoWay(int movement) => _ontoLane[movement] / FootConnectors.LanesPerWay;

    /// <inheritdoc cref="OntoWay"/>
    public int OntoLane(int movement) => _ontoLane[movement] % FootConnectors.LanesPerWay;

    /// <summary>
    /// The place it is turned through, which is where the lane it arrives on ends
    /// (<see cref="FootWays.ArrivesAt"/>).
    /// </summary>
    public int PlaceOf(int movement) =>
        _ways.Connectors.PlaceOf(_ways.ArrivesAt(FromWay(movement), FromLane(movement)));

    /// <summary>
    /// One movement's own line, <b>running the way it is walked</b> — from where the arriving lane ends to
    /// where the departing lane begins, so a route is a run of chains that meet end to end.
    /// </summary>
    public ReadOnlySpan<ArcSeg> ArcsOf(int movement) =>
        _arcs.AsSpan(_arcOffsets[movement], _arcOffsets[movement + 1] - _arcOffsets[movement]);

    /// <summary>
    /// <b>Whether every part of it is a curve the feet can hold</b> (WLK-13): false where no such curve joins
    /// the two poses and the straight between them was laid instead, which is a walker stopping to pivot.
    /// <b>Which is the instruments' to report</b> rather than something to pass over.
    /// </summary>
    public bool Held(int movement) => _held[movement];

    /// <summary>Lays every turn through every place in the town. Build-time only — it allocates freely.</summary>
    public static FootMovements Lay(CityPlan plan, SimConfig config)
    {
        var walks = WalkLines.Of(plan.Paving(config).Perimeter(config), config);
        return Lay(FootWays.Lay(plan, config, walks), walks, config);
    }

    /// <summary>The same, off ways and courses somebody already holds.</summary>
    public static FootMovements Lay(FootWays ways, WalkLines walks, SimConfig config)
    {
        if (ways.Count == 0) return None;

        var (arriving, setsOff) = Ports(ways);
        var fromLane = new List<int>();
        var ontoLane = new List<int>();
        var arcOffsets = new List<int> { 0 };
        var arcs = new List<ArcSeg>();
        var held = new List<bool>();
        var refused = 0;

        for (var place = 0; place < arriving.Length; place++)
        {
            foreach (var from in arriving[place])
            {
                foreach (var onto in setsOff[place])
                {
                    // The one pair that is not a turn: arriving and walking straight back out beside
                    // yourself down the lane you came in on.
                    if (from / FootConnectors.LanesPerWay == onto / FootConnectors.LanesPerWay) continue;

                    var line = Through(ways, walks, from, onto, config.WalkerTightestTurnM, out var laidHeld);
                    if (!Smooth(ways, from, onto, line))
                    {
                        refused++;
                        continue;
                    }

                    fromLane.Add(from);
                    ontoLane.Add(onto);
                    arcs.AddRange(line);
                    held.Add(laidHeld);
                    arcOffsets.Add(arcs.Count);
                }
            }
        }

        return new FootMovements(
            ways, [.. fromLane], [.. ontoLane], [.. arcOffsets], [.. arcs], [.. held], refused);
    }

    /// <summary>
    /// <b>Whether a walk taken through this turn carries on at both joints rather than pivoting at one</b>
    /// (WLK-14): the lane arriving against the turn's first piece, and the turn's last against the lane
    /// setting off, each within the angle at which a joint is one line carrying straight on
    /// (<see cref="LineTolerance.StraightOnRad"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What fails it is not laid at all</b>, which is the car side's own answer to the same question — a
    /// junction refuses a movement whose join is tighter than the steering affords (TER-5f) rather than
    /// handing out a line nothing can drive. A rough joint is a body stopping dead in the middle of a
    /// junction; <b>the honest answer is that the place does not offer that movement</b>, and the census says
    /// how many it dropped and where.
    /// </para>
    /// <para>
    /// <b>A joint against a lane with no line of its own is not read.</b> A welded lane (WLK-12) arrives and
    /// sets off at one point and faces nowhere, so there is no heading there to carry on from — what stands
    /// between the two ways is the weld and not this turn.
    /// </para>
    /// </remarks>
    static bool Smooth(FootWays ways, int from, int onto, ReadOnlySpan<ArcSeg> line)
    {
        var arriving = ways.LaneOf(from / FootConnectors.LanesPerWay, from % FootConnectors.LanesPerWay);
        var leaving = ways.LaneOf(onto / FootConnectors.LanesPerWay, onto % FootConnectors.LanesPerWay);
        if (line.Length == 0) return true;

        return (arriving.Length == 0
                || CarriesOn(arriving[^1].HeadingAtRad(arriving[^1].LengthM), line[0].HeadingRad))
            && (leaving.Length == 0
                || CarriesOn(line[^1].HeadingAtRad(line[^1].LengthM), leaving[0].HeadingRad));
    }

    /// <summary>Whether two headings meeting at a joint are one line carrying straight on.</summary>
    static bool CarriesOn(float oneRad, float otherRad) =>
        MathF.Abs(Spline.WrapRad(otherRad - oneRad)) <= LineTolerance.StraightOnRad;

    /// <summary>
    /// <b>Which lanes arrive at each place and which set off from it</b>, one entry per lane of every way —
    /// the two lists a place's turns are the product of.
    /// </summary>
    /// <remarks>
    /// <b>A lane appears in one list at each of its way's two ends</b>, and a welded lane (WLK-12) appears in
    /// both lists at the one place its two ends were moved onto: it has no length, so arriving on it and
    /// setting off down it are the same step, and a walk crossing that place still has to be turned through
    /// it.
    /// </remarks>
    static (List<int>[] Arriving, List<int>[] SetsOff) Ports(FootWays ways)
    {
        var places = ways.Connectors.PlaceCount;
        var arriving = new List<int>[places];
        var setsOff = new List<int>[places];
        for (var place = 0; place < places; place++)
        {
            arriving[place] = [];
            setsOff[place] = [];
        }

        for (var way = 0; way < ways.Count; way++)
        {
            for (var lane = 0; lane < FootConnectors.LanesPerWay; lane++)
            {
                var at = (way * FootConnectors.LanesPerWay) + lane;
                var ends = ways.Connectors.PlaceOf(ways.ArrivesAt(way, lane));
                var begins = ways.Connectors.PlaceOf(ways.SetsOffAt(way, lane));
                if (ends >= 0) arriving[ends].Add(at);
                if (begins >= 0) setsOff[begins].Add(at);
            }
        }

        return (arriving, setsOff);
    }

    /// <summary>
    /// <b>One turn's line: the stretch of the course its departing lane is walked on, joined to each of the
    /// turn's own ends by a corner the feet can hold</b> — run from the arriving end, which is the whole of
    /// what gives a movement its direction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Change lane and then walk, which is what a walker does and what a straight between the two ends
    /// does not.</b> A turn's two ends are metres apart round a corner as often as they are across a
    /// pavement, and the straight between them cuts that corner — which at a mouth is a walk laid over the
    /// carriageway. Carried onto the departing lane's own course first, every turn runs on a line the walk is
    /// already placed on.
    /// </para>
    /// <para>
    /// <b>And the joins are turned rather than cornered</b> (<see cref="Spline.CorneredInto"/>, WLK-13): a
    /// walk that meets its course at an angle is a walk that stops and pivots, which costs a walker time they
    /// need not spend, so the change of lane is a curve no tighter than the circle the feet hold at pace
    /// (<see cref="SimConfig.WalkerTightestTurnM"/>). <b>The course gives up a margin at each end for it</b>,
    /// there being no room to round a corner against a point that cannot move: the turn's own two ends are
    /// where the ways either side of it end, and those do not shift for this.
    /// </para>
    /// <para>
    /// <b>A turn already tangent to its course has nothing to round</b>, which is every turn whose two ends
    /// share one: two lanes of one offset that are not a crossing's do, so a walk carrying on round a corner
    /// is that corner and nothing else. <b>And a crossing's course is the boundary itself</b>, its points
    /// being struck on it rather than a lane's offset off it (WLK-9).
    /// </para>
    /// <para>
    /// <b>The straight between the two ends is what is left where none of that will lay</b> — a course that
    /// cannot answer between them, or a corner that runs away or hairpins. It closes the turn where a curve
    /// would not, and <b>how many of them a town has is the census's to report</b>.
    /// </para>
    /// </remarks>
    /// <param name="held">Whether every part of it is a curve the feet can hold (<see cref="Held"/>).</param>
    static ArcSeg[] Through(
        FootWays ways, WalkLines walks, int from, int onto, float tightestM, out bool held)
    {
        var (fromWay, fromLane) = (from / FootConnectors.LanesPerWay, from % FootConnectors.LanesPerWay);
        var (ontoWay, ontoLane) = (onto / FootConnectors.LanesPerWay, onto % FootConnectors.LanesPerWay);
        var fromM = ways.Connectors.PointM(
            ways.ArrivesAt(fromWay, fromLane), ways.KindOf(fromWay), fromLane);
        var ontoM = ways.Connectors.PointM(
            ways.SetsOffAt(ontoWay, ontoLane), ways.KindOf(ontoWay), ontoLane);
        var leaves = Facing(ways.LaneOf(fromWay, fromLane), leaving: true);
        var arrives = Facing(ways.LaneOf(ontoWay, ontoLane), leaving: false);

        // <b>The turn itself first, and the course only for what it will not reach.</b> A turn is a couple of
        // metres of ground between two poses far more often than it is a walk down a street, and one curve
        // between them is the line a walker takes; routed onto the course and off it whatever its length, the
        // same turn came back as an S where one arc would do and as a loop where the course doubled back.
        var direct = Turned(fromM, leaves, ontoM, arrives, tightestM, out held);
        if (held) return direct;

        var course = CourseOf(walks, ways.KindOf(ontoWay), ontoLane);
        if (course.NearestTo(fromM, out var joined, out var at) && course.NearestTo(ontoM, out var met, out var to)
            && course.Between(at, to) is { Length: > 0 } alongTheCourse)
        {
            var walked = Walked(alongTheCourse, joined.PositionM);
            var laid = Joined(
                walked, fromM, leaves, Vector2.Distance(fromM, joined.PositionM), ontoM, arrives,
                Vector2.Distance(ontoM, met.PositionM), tightestM, out held);

            // <b>And only where it is a curve at both ends.</b> The course is here to give a curve where one
            // between the two poses will not lay; a route that pivots onto it and then walks the long way
            // round is worse than the one pivot the straight costs, and it is what drew a walk arriving at a
            // point and setting off back the way it came.
            if (laid.Length > 0 && held && WorthWalking(laid, fromM, ontoM)) return laid;
        }

        // <b>And the pivot where neither is worth walking</b>: a body that stands, turns and walks the
        // straight, which is the line whose heading is exactly what its two ends ask for
        // (<see cref="Spline.PivotedM"/>). <b>How many turns a town comes to this on is the census's to
        // report.</b>
        held = false;
        return Step(fromM, ontoM);
    }

    /// <summary>
    /// <b>Whether a line laid through a place is one anybody would walk</b>: the two bounds a corner itself is
    /// held to (<see cref="Spline.CorneredInto"/>), asked of the whole line however it was laid — <b>no more
    /// than half a turn of heading over what its own two ends ask for, and no more of the straight between
    /// them than a half turn covers of its chord</b> (<see cref="Spline.HalfATurnOfItsChord"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Asked of the whole line because the parts pass and the line does not.</b> Two corners onto a course
    /// with a stretch of course between them are each inside both bounds while the three together are a ring:
    /// two ends all but on top of each other facing opposite ways get a corner out and a corner back, and what
    /// is drawn is a loop a body would walk round to arrive where it already stood.
    /// </para>
    /// <para>
    /// <b>And the ground bound is what catches the long way round</b>, which the heading cannot see: a course
    /// that meanders at a corner spends no extra heading and still runs three and a half times the straight
    /// its two ends stand apart. <b>A turn is ground a walker crosses</b>, and past what an arc between those
    /// two ends could ever cover the walk is the straight — the pavement the course was keeping to is not
    /// worth going round the houses for.
    /// </para>
    /// </remarks>
    static bool WorthWalking(ReadOnlySpan<ArcSeg> line, Vector2 fromM, Vector2 ontoM) =>
        Spline.SweptRad(line) - Spline.AskedRad(line) <= Spline.HalfATurnRad
        && Spline.TotalLengthM(line)
            <= (Spline.HalfATurnOfItsChord * Vector2.Distance(fromM, ontoM)) + LineTolerance.JoinedM;

    /// <summary>
    /// A stretch of course with the corner onto it at each end: <b>the margin the corners are given is the
    /// course's to give up</b>, and where the stretch has no margin to spare there is no middle to it and the
    /// whole turn is one corner.
    /// </summary>
    static ArcSeg[] Joined(
        ArcSeg[] walked, Vector2 fromM, float? leaves, float offTheCourseM, Vector2 ontoM, float? arrives,
        float ontoTheCourseM, float tightestM, out bool held)
    {
        var lengthM = Spline.TotalLengthM(walked);
        var leavingM = Margin(offTheCourseM, tightestM, lengthM);
        var joiningM = Margin(ontoTheCourseM, tightestM, lengthM);
        var joins = Spline.SampleAt(walked, leavingM);
        var parts = Spline.SampleAt(walked, lengthM - joiningM);

        var onto = Turned(fromM, leaves, joins.PositionM, joins.HeadingRad, tightestM, out var heldOnto);
        var off = Turned(parts.PositionM, parts.HeadingRad, ontoM, arrives, tightestM, out var heldOff);
        held = heldOnto && heldOff;
        if (onto.Length == 0 || off.Length == 0 || lengthM <= leavingM + joiningM) return [];

        var middle = new ArcSeg[walked.Length * 2];
        var taken = Spline.SubChainInto(walked, leavingM, lengthM - joiningM, middle);
        return [.. onto, .. middle.AsSpan(0, taken), .. off];
    }

    /// <summary>
    /// <b>How much of a course one corner onto it is given: the ground that corner has to cover</b>, which is
    /// how far the turn's own end stands off the course — never less than the room a half-turn on the
    /// tightest circle needs, and never more than the course has.
    /// </summary>
    /// <remarks>
    /// <b>The floor alone would have a corner cover ground it has no room for</b>: a turn onto a crossing
    /// swings a right angle onto a point a walking lane off the course, and half a metre of course is not
    /// where a curve the feet can hold does that.
    /// </remarks>
    static float Margin(float offTheCourseM, float tightestM, float lengthM) =>
        MathF.Min(MathF.Max(tightestM * MarginInTightestTurns, offTheCourseM), lengthM * 0.5f);

    /// <summary>
    /// <b>The least a corner onto a course is given, in the tightest turns the feet can hold</b> — two of
    /// them, which is the room a half-turn on that circle needs and the same floor the walking network's own
    /// corners are given (<c>WalkingNetwork</c>).
    /// </summary>
    const float MarginInTightestTurns = 2f;

    /// <summary>
    /// <b>The curve between two poses, and the straight between the two points where there is no holding
    /// one</b> (<see cref="Spline.CorneredInto"/>) — nothing at all where the two are one place, and the
    /// straight where either end has no heading to turn onto, a welded lane (WLK-12) having no line to read
    /// one off.
    /// </summary>
    /// <param name="held">
    /// Whether what was laid is a curve the feet can hold, which <b>a gap crossed by the straight is not</b>
    /// — nothing to cross at all counts as held, there being no corner there to fail at.
    /// </param>
    static ArcSeg[] Turned(
        Vector2 fromM, float? leaves, Vector2 ontoM, float? arrives, float tightestM, out bool held)
    {
        held = true;
        if (Vector2.Distance(fromM, ontoM) <= LineTolerance.RoundingM) return [];

        held = false;
        if (leaves is not { } leavesRad || arrives is not { } arrivesRad) return Step(fromM, ontoM);

        // <b>The biarc and not the turn on a named circle</b> (<see cref="Spline.StraightArcStraightInto"/>),
        // which lays its straights along the two pose lines and so runs out to wherever those lines happen to
        // cross: tried here, the turns at nearly parallel poses reached 1 782 m and the pivots it was meant
        // to remove stayed at every one of the 81 it started with.
        var drawn = new ArcSeg[MostArcsInACorner];
        var laid = Spline.CorneredInto(fromM, leavesRad, ontoM, arrivesRad, tightestM, drawn);
        if (laid == 0) return Step(fromM, ontoM);

        held = true;
        return drawn[..laid];
    }

    /// <summary>The same, onto or off a place on a course, which always has a heading.</summary>
    static ArcSeg[] Turned(
        Vector2 fromM, float? leaves, Vector2 ontoM, float arrives, float tightestM, out bool held) =>
        Turned(fromM, leaves, ontoM, (float?)arrives, tightestM, out held);

    /// <summary>How many arcs the corner between two poses is, which is an equal-tangent biarc's two.</summary>
    const int MostArcsInACorner = 2;

    /// <summary>
    /// Which way a lane runs where a turn meets it, or <b>nothing where the lane has no line to read it
    /// off</b> — a lane welded to one point (WLK-12) arrives and sets off at the same place and faces
    /// nowhere, so what joins it is the straight rather than a curve onto a heading it does not have.
    /// </summary>
    static float? Facing(ReadOnlySpan<ArcSeg> lane, bool leaving) =>
        lane.Length == 0
            ? null
            : leaving
                ? lane[^1].HeadingAtRad(lane[^1].LengthM)
                : lane[0].HeadingRad;

    /// <summary>
    /// Which line a lane is walked on: <b>its own course, and the boundary itself for a crossing</b>, whose
    /// pair is struck on that boundary rather than a lane's offset off it (WLK-9).
    /// </summary>
    static KerbLines CourseOf(WalkLines walks, FootConnectorKind kind, int lane) =>
        kind == FootConnectorKind.Crossing ? walks.Boundary : walks.Course(lane);

    /// <summary>The straight from one place to another, which is nothing at all where they are one place.</summary>
    static ArcSeg[] Step(Vector2 fromM, Vector2 ontoM)
    {
        var run = ontoM - fromM;
        var lengthM = run.Length();
        return lengthM > LineTolerance.RoundingM
            ? [new ArcSeg(fromM, MathF.Atan2(run.Y, run.X), lengthM, 0f)]
            : [];
    }

    /// <summary>
    /// A stretch cut off a course, turned round where it was cut from the far end: <b>a course is cut forward
    /// whichever of its two places the arc begins at</b> (<see cref="KerbLines.Between"/>), and a movement
    /// runs from the end a walk joins it at.
    /// </summary>
    static ArcSeg[] Walked(ArcSeg[] line, Vector2 fromM)
    {
        if (Vector2.DistanceSquared(line[0].StartM, fromM)
            <= Vector2.DistanceSquared(line[^1].EndM, fromM))
        {
            return line;
        }

        var back = new ArcSeg[line.Length];
        Spline.ReverseInto(line, back);
        return back;
    }
}
