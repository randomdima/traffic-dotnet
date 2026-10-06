using System.Numerics;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// <b>A survey's ways as the junctions and roads a plan carries</b> (GEN-57): where ways meet is a junction,
/// what runs between two of them is a road, and each road is laid along the line the survey drew.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing the survey holds is lost on the way in.</b> Every place ways meet is a junction standing exactly
/// there, or amid the places a short road joins it to (<see cref="Gathered"/>); every point a way was drawn through stands within a tolerance of its road,
/// which is laid in the fewest corners and the roundest arcs that keep it so (<see cref="TracedAlignment"/>), and
/// those in the fewest pieces (<see cref="TracedPieces"/>); a
/// road's corners are rounded only as tight as its own carriageway allows; and a piece of the network joined to
/// nothing else is kept. The generator's rules that would take something out — one junction for two inside a
/// locality (GEN-16), a corner no tighter than a class's design speed (GEN-47), one connected network (GEN-5)
/// — are not asked of a traced town, and how far it stands off its survey is <c>--bench fidelity</c>'s.
/// </para>
/// <para>
/// <b>What a survey says and a generated town never has is kept</b>: a dead end, a junction of six arms, two
/// arms at a shallow angle, a one-way carriageway beside its twin, four junctions where two dual carriageways
/// cross. They are the place, and a rule refusing them would be a rule refusing the city it was asked to trace.
/// </para>
/// </remarks>
internal static partial class TracedStreets
{
    /// <param name="WayOffsets">Count + 1 entries, over <paramref name="Ways"/>: the survey ways each road was laid along.</param>
    /// <param name="Ways">Indices into <see cref="Survey.Ways"/>, a road's in the order it runs.</param>
    /// <param name="JunctionOf">The junction standing at each survey point, or <see cref="CityPlan.NoRecord"/> where none does.</param>
    /// <param name="Bridges">The deck under every road the survey carries on a bridge.</param>
    /// <param name="Roundabouts">The roads OSM tags as circulating, a ring at a time.</param>
    internal readonly record struct Laid(
        CityPlan.JunctionArrays Junctions, CityPlan.RoadArrays Roads, TurnsLaid Turns, SharedLaid Shared, GatheredLaid Gathered,
        int[] WayOffsets, int[] Ways, int[] JunctionOf, CityPlan.BridgeArrays Bridges, CityPlan.RoundaboutArrays Roundabouts);

    /// <summary>
    /// <b>How much of where OSM says a car may turn was laid</b>: the road ends whose lanes carry its arrows, and the
    /// restrictions and lane links laid and not — at a node no junction stands at, or naming a way that has no end at
    /// that junction or more than one.
    /// </summary>
    internal readonly record struct TurnsLaid(
        int ArrowedEnds, int Restrictions, int RestrictionsAtNoJunction, int RestrictionsUnmatched, int Bans,
        int Links, int LinksNotLaid);

    /// <summary>
    /// <b>The traffic arriving at one end of a road, as the survey has it</b>: the way it arrives on, whether it runs
    /// along that way's points, and whether that way ends here for it — the end its lanes' arrows are painted for
    /// (Key:turn). <see cref="None"/> at a junction this engine made.
    /// </summary>
    readonly record struct Arrival(int Way, bool Along, bool WayEnds)
    {
        public static Arrival None => new(-1, false, false);
    }

    /// <summary>One run of a way between two places it meets another, in the way's own order.</summary>
    readonly record struct Edge(int[] Points, int Way)
    {
        public int First => Points[0];

        public int Last => Points[^1];
    }

    /// <summary>
    /// A road's carriageway as OSM has it: its lanes each way, its width kerb to kerb, where its middle stands off
    /// the surveyed line, to the right of it as the road runs, whether it is one lane both ways share — none is, once
    /// <see cref="OneWayShared"/> has run each one way — the level it
    /// is driven on — a bridge's above the ground (<see cref="CityPlan.RoadArrays.Level"/>) — whether it circulates
    /// on a roundabout, and the roadside beside the kerb each way's traffic keeps to
    /// (<see cref="CityPlan.RoadArrays.RoadsideWithM"/>).
    /// </summary>
    readonly record struct Carriage(
        RoadLanes Lanes, float WidthM, float CentreOffsetM, bool Shared, byte Level, bool Circulates, float RoadsideWithM,
        float RoadsideAgainstM)
    {
        public Carriage Turned =>
            new(Lanes.Turned, WidthM, -CentreOffsetM, Shared, Level, Circulates, RoadsideAgainstM, RoadsideWithM);

        /// <summary>How wide the lanes are side by side, between the two roadsides.</summary>
        public float LanesWidthM => WidthM - RoadsideWithM - RoadsideAgainstM;

        /// <summary>The roadside beside the kerb one way's traffic keeps to.</summary>
        public float RoadsideM(bool withTheRoad) => withTheRoad ? RoadsideWithM : RoadsideAgainstM;

        /// <summary>
        /// How far one lane stands off the road's line, toward the side its own traffic keeps, counted from its kerb —
        /// as the plan lays it (<see cref="CityPlan.RoadArrays.LaneOffsetM"/>).
        /// </summary>
        public float LaneOffsetM(int fromKerb, bool withTheRoad) =>
            (WidthM * 0.5f) - RoadsideM(withTheRoad) - ((fromKerb + 0.5f) * LanesWidthM / (Lanes.With + Lanes.Against));

        /// <summary>
        /// The same carriageway as though it had been measured that much wider at one kerb, which it holds as a roadside
        /// there (<see cref="Survey"/>): its middle where it was, so its lanes stand off it as a roadside sets them.
        /// </summary>
        public Carriage Edged(bool withTheRoad, float stripM) => withTheRoad
            ? this with { WidthM = WidthM + stripM, RoadsideWithM = stripM }
            : this with { WidthM = WidthM + stripM, RoadsideAgainstM = stripM };

        /// <summary>The same carriageway without the roadside at one kerb, as though measured that much narrower there.</summary>
        public Carriage Unedged(bool withTheRoad) => withTheRoad
            ? this with { WidthM = WidthM - RoadsideWithM, RoadsideWithM = 0f }
            : this with { WidthM = WidthM - RoadsideAgainstM, RoadsideAgainstM = 0f };
    }

    /// <summary>
    /// A road while the network is still being settled: which junctions, the points it was surveyed through
    /// from the place it leaves to the place it reaches, and its carriageway.
    /// </summary>
    sealed class Road
    {
        public required int From;
        public required int To;
        public required List<Vector2> PointsM;
        public required Carriage Carriage;
        public required string Highway;

        /// <summary>The traffic arriving at <see cref="From"/>, which is the lanes against the road.</summary>
        public required Arrival AtFrom;

        /// <summary>And at <see cref="To"/>, which is the lanes with it.</summary>
        public required Arrival AtTo;

        /// <summary>The survey ways it runs along, in the order it runs.</summary>
        public required List<int> Ways;

        public bool Gone;

        public void Turn()
        {
            (From, To) = (To, From);
            (AtFrom, AtTo) = (AtTo, AtFrom);
            PointsM.Reverse();
            Ways.Reverse();
            Carriage = Carriage.Turned;
        }
    }

    public static Laid Lay(Survey survey, SimConfig config)
    {
        var edges = Edges(survey);
        var ends = EndsAt(survey.PointCount, edges);
        var place = Places(survey, edges, ends);
        var (junctionOf, centreM, pointOf) = Junctions(survey, place);

        var roads = Walked(survey, edges, ends, place, junctionOf);
        roads = Unlooped(roads, centreM);
        var shared = OneWayShared(roads, centreM.Count);
        Evened(roads, centreM, config);
        JoinedThrough(roads, centreM.Count);
        roads = CutShort(roads, centreM, config.CityGen.TracedRoadLongestM);
        var gathered = Gathered(survey, roads, centreM, junctionOf, config);

        var runsOff = RunsOff(survey, roads, pointOf, centreM.Count);
        var standoffM = Standoffs(roads, centreM, runsOff, gathered.Into, config);
        var merging = new Merging[roads.Count];
        var roadOf = new List<int>(roads.Count);
        for (var road = 0; road < roads.Count; road++)
        {
            if (roads[road].Gone) continue;

            var line = Line(roads[road], centreM, standoffM.FromM[road], standoffM.ToM[road], new Vector2(survey.WidthM, survey.HeightM), config);
            if (line is null)
            {
                roads[road].Gone = true;
                continue;
            }

            merging[road] = line;
            roadOf.Add(road);
        }

        return MergedAsFarAsKept(survey, config, [.. roadOf], merging, chosen =>
        {
            var lines = new ArcSeg[roads.Count][];
            var through = new Vector2[roads.Count][];
            foreach (var road in roadOf) (lines[road], through[road]) = chosen[road];

            return Arrays(survey, junctionOf, config, roads, lines, through, centreM, standoffM, runsOff, shared, gathered);
        });
    }

    /// <summary>
    /// <b>Where a road runs on off the map</b>: a junction at a place the survey's way left the map
    /// (<see cref="Survey.LeavesTheMap"/>) that only that one road reaches. One where another road meets it too is a
    /// junction like any other, and what ran on past it was shorter than a road.
    /// </summary>
    static bool[] RunsOff(Survey survey, List<Road> roads, int[] pointOf, int junctions)
    {
        var arms = new int[junctions];
        foreach (var road in roads)
        {
            if (road.Gone) continue;

            arms[road.From]++;
            arms[road.To]++;
        }

        var runsOff = new bool[junctions];
        for (var junction = 0; junction < pointOf.Length; junction++) runsOff[junction] = arms[junction] == 1 && survey.Leaves(pointOf[junction]);
        return runsOff;
    }

    /// <summary>
    /// <b>How many lanes a surveyed way is driven in each way</b> (GEN-57): OSM's own, as the scanner read them
    /// (<see cref="OsmCarriageway"/>). A single lane two-way traffic shares is one lane each way until it is run one
    /// way or taken out (<see cref="OneWayShared"/>). Lanes driven both ways down the middle of lanes each
    /// way — a tidal pair, a centre turning lane — are laid each as a lane of one way, the half nearer the lanes
    /// against the way as theirs and the odd one with it, so every lane stands where OSM puts it.
    /// </summary>
    internal static RoadLanes Driven(SurveyWay way)
    {
        if (way.Oneway) return new RoadLanes(AtLeastOne(way.LanesForward), 0);

        if (way.LanesForward == 0 && way.LanesBackward == 0) return new RoadLanes(1, 1);

        var againstShare = AgainstShare(way);
        return new RoadLanes(
            AtLeastOne(way.LanesForward + way.LanesShared - againstShare), AtLeastOne(way.LanesBackward + againstShare));

        static byte AtLeastOne(int lanes) => (byte)Math.Clamp(lanes, 1, byte.MaxValue);
    }

    /// <summary>How many of a way's lanes driven both ways are laid as lanes against it: the half nearer those lanes.</summary>
    static int AgainstShare(SurveyWay way) => way.LanesShared / 2;

    /// <summary>
    /// <b>The arrows on a surveyed way's lanes driven one way</b>, left to right as their traffic looks: the lanes
    /// <see cref="Driven"/> lays that way, so a lane both ways share is each way's own. Empty where none is painted.
    /// </summary>
    static OsmArrows[] DrivenArrows(SurveyWay way, bool along)
    {
        var arrows = way.Arrows;
        if (arrows.Length == 0) return [];
        if (way.LanesForward == 0 && way.LanesBackward == 0) return [along ? arrows[^1] : arrows[0]];

        var split = way.LanesBackward + AgainstShare(way);
        if (along) return arrows[split..];

        var against = arrows[..split];
        Array.Reverse(against);
        return against;
    }

    /// <summary>
    /// <b>How far off its junction's centre each road's lanes end, at each of its two ends</b> (TER-5d): the town's own
    /// standoff, stood out by however much wider than a street of one lane each way the junction's widest arm is
    /// (<see cref="SimConfig.JunctionRadiusAcrossM"/>) — <b>and a road's own end never past the middle of the way to the
    /// junction at its other end</b>, less the shortest road a traced map lays (<see cref="CityGenFigures.TracedShortestRoadM"/>),
    /// so a road the survey drew between two places close together is a short road and never lost. <b>None where a road
    /// runs off the map</b>, so its lanes run up to the map's edge.
    /// </summary>
    /// <remarks>
    /// <b>A short road squeezes its own ends and not its junctions' other arms</b>, which end no more than
    /// <see cref="CityGenFigures.TracedArmEndsApartM"/> further back than the shortest end there. Squeezed with it, a
    /// crossing a way runs a few metres past before it changes has every arm run into its middle, and every turn across
    /// it winds round to come back to where its two lanes' lines cross.
    /// </remarks>
    /// <param name="into">The junction each is laid as (<see cref="Gathered"/>), which is where its roads turn onto each other.</param>
    static (float[] FromM, float[] ToM) Standoffs(List<Road> roads, List<Vector2> centreM, bool[] runsOff, int[] into, SimConfig config)
    {
        var widestM = new float[centreM.Count];
        foreach (var road in roads)
        {
            if (road.Gone) continue;

            var widthM = road.Carriage.WidthM;
            widestM[road.From] = MathF.Max(widestM[road.From], widthM);
            widestM[road.To] = MathF.Max(widestM[road.To], widthM);
        }

        var askedM = new float[centreM.Count];
        for (var junction = 0; junction < askedM.Length; junction++)
        {
            askedM[junction] = runsOff[junction] ? 0f : config.JunctionRadiusAcrossM(widestM[junction]);
        }

        var roomM = new float[roads.Count];
        var shortestM = (float[])askedM.Clone();
        for (var road = 0; road < roads.Count; road++)
        {
            if (roads[road].Gone) continue;

            var (from, to) = (roads[road].From, roads[road].To);
            roomM[road] = MathF.Max(0f, (Vector2.Distance(centreM[from], centreM[to]) - config.CityGen.TracedShortestRoadM) * 0.5f);
            shortestM[from] = MathF.Min(shortestM[from], roomM[road]);
            shortestM[to] = MathF.Min(shortestM[to], roomM[road]);
        }

        var fromM = new float[roads.Count];
        var toM = new float[roads.Count];
        for (var road = 0; road < roads.Count; road++)
        {
            if (roads[road].Gone) continue;

            fromM[road] = EndM(roads[road].From, roomM[road]);
            toM[road] = EndM(roads[road].To, roomM[road]);
        }

        RoomToTurn(roads, centreM, into, fromM, toM, roomM, config);
        return (fromM, toM);

        float EndM(int junction, float roomM) =>
            MathF.Min(MathF.Min(askedM[junction], roomM), shortestM[junction] + config.CityGen.TracedArmEndsApartM);
    }

    /// <summary>
    /// <b>Each road's end stood back as far as the turns off and onto it need to begin</b> (TER-5d.2): a turn is made on
    /// <see cref="SimConfig.JunctionTurnRoomM"/> at the corner its two lanes' lines make, so the lane in has to end that
    /// circle's tangent short of the corner and the lane out begin as far past it — the near side's from the kerb lane
    /// onto the kerb lane and the far side's from the lane beside the line onto the lane beside the line, which are the
    /// two whose corner comes nearest each end. An end standing nearer is moved back, but never past its road's
    /// <paramref name="roomM"/>, so the road between two junctions is still laid.
    /// </summary>
    /// <remarks>
    /// <b>Moved back rather than swung out</b>: a turn with too little room to its corner reaches its circle by first
    /// swinging the other way, over the lanes beside it, and started sooner it needs no swing. Where two lanes' lines
    /// cross does not depend on where either lane ends, so how far an end moves is the tangent less what it has. Straight
    /// on asks nothing of it, nor does a turn within the straight-on tolerance of turning back, which has no corner.
    /// </remarks>
    static void RoomToTurn(
        List<Road> roads, List<Vector2> centreM, int[] into, float[] fromM, float[] toM, float[] roomM, SimConfig config)
    {
        var ends = new Dictionary<int, List<ArmEnd>>();
        for (var road = 0; road < roads.Count; road++)
        {
            if (roads[road].Gone) continue;

            foreach (var atTo in (ReadOnlySpan<bool>)[false, true])
            {
                if (ArmEnd.Of(roads[road], road, atTo, atTo ? toM[road] : fromM[road], centreM) is not { } end) continue;

                var laidAs = into[atTo ? roads[road].To : roads[road].From];
                if (!ends.TryGetValue(laidAs, out var list)) ends[laidAs] = list = [];
                list.Add(end);
            }
        }

        var straightRad = config.Road.TurnStraightToleranceDeg * MathF.PI / 180f;
        var neededFromM = (float[])fromM.Clone();
        var neededToM = (float[])toM.Clone();
        foreach (var arms in ends.Values)
        {
            foreach (var arriving in arms)
            {
                foreach (var leaving in arms)
                {
                    if (arriving.Road == leaving.Road || arriving.Lanes(arriving: true) == 0 || leaving.Lanes(arriving: false) == 0) continue;

                    var inRad = MathF.Atan2(-arriving.OutM.Y, -arriving.OutM.X);
                    var outRad = MathF.Atan2(leaving.OutM.Y, leaving.OutM.X);
                    var turnRad = Spline.WrapRad(outRad - inRad);
                    if (MathF.Abs(turnRad) <= straightRad || MathF.Abs(turnRad) >= MathF.PI - straightRad) continue;

                    var nearSide = MathF.Sign(turnRad) == MathF.Sign(config.RoadSideSign);
                    var inM = arriving.LaneEndM(arriving: true, nearSide, config.RoadSideSign);
                    var outM = leaving.LaneEndM(arriving: false, nearSide, config.RoadSideSign);
                    if (!Spline.ToTheCorner(inM, inRad, outM, outRad, out _, out var beforeM, out var afterM)) continue;

                    var tangentM = config.JunctionTurnRoomM * MathF.Tan(MathF.Abs(turnRad) * 0.5f);
                    Needs(arriving, arriving.StandoffM + tangentM - beforeM);
                    Needs(leaving, leaving.StandoffM + tangentM - afterM);
                }
            }
        }

        for (var road = 0; road < roads.Count; road++)
        {
            fromM[road] = MathF.Max(fromM[road], MathF.Min(neededFromM[road], roomM[road]));
            toM[road] = MathF.Max(toM[road], MathF.Min(neededToM[road], roomM[road]));
        }

        void Needs(ArmEnd end, float standoffM)
        {
            var needed = end.AtTo ? neededToM : neededFromM;
            needed[end.Road] = MathF.Max(needed[end.Road], standoffM);
        }
    }

    /// <summary>
    /// One road's end at a junction, read where its line leaves the disc it stands off: which way the road runs out from
    /// there, and the place its line stands.
    /// </summary>
    readonly record struct ArmEnd(int Road, bool AtTo, float StandoffM, Vector2 LineM, Vector2 OutM, Carriage Carriage)
    {
        /// <summary>How many of its lanes arrive at the junction, or leave it.</summary>
        public int Lanes(bool arriving) => arriving == AtTo ? Carriage.Lanes.With : Carriage.Lanes.Against;

        /// <summary>
        /// Where the lane a turn to one side is made from ends, or the lane it is made onto begins: the kerb lane for the
        /// near side and the lane beside the line for the far.
        /// </summary>
        public Vector2 LaneEndM(bool arriving, bool nearSide, float roadSideSign)
        {
            var headingM = arriving ? -OutM : OutM;
            var fromKerb = nearSide ? 0 : Lanes(arriving) - 1;
            return LineM + (Heading.RightOf(headingM) * roadSideSign * Carriage.LaneOffsetM(fromKerb, withTheRoad: arriving == AtTo));
        }

        /// <summary>The end, or none where the road never leaves its junction's disc.</summary>
        public static ArmEnd? Of(Road road, int index, bool atTo, float standoffM, List<Vector2> centreM)
        {
            var (leg, atM) = Leaving(road.PointsM, centreM[atTo ? road.To : road.From], standoffM, forward: !atTo);
            if (leg < 0) return null;

            var runM = Vector2.Normalize(road.PointsM[leg + 1] - road.PointsM[leg]);
            var outM = atTo ? -runM : runM;
            return new ArmEnd(index, atTo, standoffM, atM + (Heading.RightOf(runM) * road.Carriage.CentreOffsetM), outM, road.Carriage);
        }
    }

    /// <summary>
    /// Every way cut at every point it shares with another way, at its own two ends and where it meets
    /// itself, so an edge's two ends are the only places on it anything else touches.
    /// </summary>
    static List<Edge> Edges(Survey survey)
    {
        var uses = new int[survey.PointCount];
        var end = new bool[survey.PointCount];
        var ways = new int[survey.Ways.Length][];
        for (var way = 0; way < ways.Length; way++)
        {
            ways[way] = Distinct(survey.Ways[way].Points);
            foreach (var point in ways[way]) uses[point]++;

            end[ways[way][0]] = end[ways[way][^1]] = true;
        }

        var edges = new List<Edge>();
        for (var way = 0; way < ways.Length; way++)
        {
            var points = ways[way];
            if (points.Length < 2) continue;

            var start = 0;
            for (var at = 1; at < points.Length; at++)
            {
                if (!end[points[at]] && uses[points[at]] < 2) continue;

                edges.Add(new Edge(points[start..(at + 1)], way));
                start = at;
            }
        }

        return edges;
    }

    /// <summary>A way's points with any point repeated straight after itself taken out.</summary>
    static int[] Distinct(int[] points)
    {
        var kept = new List<int>(points.Length);
        foreach (var point in points)
        {
            if (kept.Count == 0 || kept[^1] != point) kept.Add(point);
        }

        return [.. kept];
    }

    /// <summary>Which edge ends stand at each point, as (edge, whether it is the edge's first point).</summary>
    static List<(int Edge, bool AtFirst)>[] EndsAt(int points, List<Edge> edges)
    {
        var ends = new List<(int Edge, bool AtFirst)>[points];
        for (var edge = 0; edge < edges.Count; edge++)
        {
            (ends[edges[edge].First] ??= []).Add((edge, true));
            (ends[edges[edge].Last] ??= []).Add((edge, false));
        }

        return ends;
    }

    static Carriage CarriageOf(Survey survey, in Edge edge)
    {
        var way = survey.Ways[edge.Way];
        var shared = way.LanesShared > 0 && way.LanesForward == 0 && way.LanesBackward == 0;
        return new Carriage(
            Driven(way), way.CarriagewayM, way.CentreOffsetM, shared, way.Bridge ? CityPlan.RoadArrays.Over : CityPlan.RoadArrays.Ground,
            way.Roundabout, way.RoadsideAlongM, way.RoadsideAgainstM);
    }

    /// <summary>
    /// <b>Whether each point is a place</b> — somewhere ways meet, a way ends, or traffic cannot simply carry
    /// on. A point exactly two edges meet at is a place only where the two disagree about their carriageway:
    /// a one-way street running into a two-way one, two one-way streets both arriving, a carriageway that gains
    /// or loses a lane there (GEN-51), one OSM widens, narrows or places off its line differently — or a bridge's
    /// end, the bridgehead, so a bridge is a road of its own (GEN-14a).
    /// </summary>
    static bool[] Places(Survey survey, List<Edge> edges, List<(int Edge, bool AtFirst)>[] ends)
    {
        var place = new bool[survey.PointCount];
        for (var point = 0; point < place.Length; point++)
        {
            var here = ends[point];
            if (here is null) continue;

            place[point] = here.Count != 2
                || here[0].Edge == here[1].Edge
                || !CarriesOn(CarriageOf(survey, edges[here[0].Edge]), here[0].AtFirst,
                              CarriageOf(survey, edges[here[1].Edge]), here[1].AtFirst);
        }

        // A ring of ways that only carry on into each other — two one-way carriageways joined end to end and to
        // nothing else this map lays — has no place on it to be walked from, and one of its points is made one.
        var seen = new bool[edges.Count];
        for (var first = 0; first < edges.Count; first++)
        {
            if (seen[first] || place[edges[first].First]) continue;

            var edge = first;
            var forward = true;
            while (true)
            {
                seen[edge] = true;
                var reached = forward ? edges[edge].Last : edges[edge].First;
                if (place[reached]) break;
                if (reached == edges[first].First)
                {
                    place[reached] = true;
                    break;
                }

                var onward = ends[reached][0].Edge == edge ? ends[reached][1] : ends[reached][0];
                edge = onward.Edge;
                forward = onward.AtFirst;
            }
        }

        return place;
    }

    /// <summary>
    /// Whether traffic carries on from one road end into another at the place they meet: the carriageway
    /// arriving on either is the one leaving on the other, lane for lane, as wide and placed the same.
    /// </summary>
    static bool CarriesOn(Carriage one, bool oneLeavesHere, Carriage other, bool otherLeavesHere) =>
        (oneLeavesHere ? one.Turned : one) == (otherLeavesHere ? other : other.Turned);

    /// <summary>
    /// <b>The junctions: one at every place, standing exactly where the survey put it</b> (GEN-57) — until those a
    /// short road joins are gathered into one amid them (<see cref="Gathered"/>), each road still leaving its own
    /// place's disc.
    /// </summary>
    static (int[] JunctionOf, List<Vector2> CentreM, int[] PointOf) Junctions(Survey survey, bool[] place)
    {
        var junctionOf = new int[place.Length];
        Array.Fill(junctionOf, CityPlan.NoRecord);
        var centreM = new List<Vector2>();
        var pointOf = new List<int>();
        for (var point = 0; point < place.Length; point++)
        {
            if (!place[point]) continue;

            junctionOf[point] = centreM.Count;
            centreM.Add(survey.PointM(point));
            pointOf.Add(point);
        }

        return (junctionOf, centreM, [.. pointOf]);
    }

    /// <summary>
    /// <b>Every road, walked from a place through every point traffic merely carries on through to the next
    /// place</b>. A ring of ways meeting nothing but each other is walked by nothing and is not a road.
    /// </summary>
    static List<Road> Walked(
        Survey survey, List<Edge> edges, List<(int Edge, bool AtFirst)>[] ends, bool[] place, int[] junctionOf)
    {
        var roads = new List<Road>();
        var walked = new bool[edges.Count];
        for (var point = 0; point < place.Length; point++)
        {
            if (!place[point]) continue;

            foreach (var (first, atFirst) in ends[point])
            {
                if (walked[first]) continue;

                var points = new List<int> { point };
                var highway = survey.Ways[edges[first].Way].Highway;
                var carriage = atFirst ? CarriageOf(survey, edges[first]) : CarriageOf(survey, edges[first]).Turned;
                var ways = new List<int>();

                var edge = first;
                var forward = atFirst;
                while (true)
                {
                    walked[edge] = true;
                    if (ways.Count == 0 || ways[^1] != edges[edge].Way) ways.Add(edges[edge].Way);
                    var run = edges[edge].Points;
                    for (var at = 1; at < run.Length; at++) points.Add(forward ? run[at] : run[^(at + 1)]);
                    if (Rank(survey.Ways[edges[edge].Way].Highway) > Rank(highway)) highway = survey.Ways[edges[edge].Way].Highway;

                    var reached = points[^1];
                    if (place[reached]) break;

                    var onward = ends[reached][0].Edge == edge ? ends[reached][1] : ends[reached][0];
                    edge = onward.Edge;
                    forward = onward.AtFirst;
                }

                var pointsM = new List<Vector2>(points.Count);
                foreach (var at in points) pointsM.Add(survey.PointM(at));

                // The road's traffic against it arrives at its first point running against the walk.
                roads.Add(new Road
                {
                    From = junctionOf[point], To = junctionOf[points[^1]], PointsM = pointsM, Carriage = carriage,
                    Highway = highway, Ways = ways,
                    AtFrom = Arriving(survey, edges[first].Way, !atFirst, point),
                    AtTo = Arriving(survey, edges[edge].Way, forward, points[^1]),
                });
            }
        }

        return roads;
    }

    static Arrival Arriving(Survey survey, int way, bool along, int point)
    {
        var points = survey.Ways[way].Points;
        return new Arrival(way, along, point == (along ? points[^1] : points[0]));
    }

    /// <summary>
    /// <b>A road that comes back to the junction it left is a loop</b>, which is cut at its furthest point into
    /// two roads and a junction of two arms (GEN-51), since a road runs between two junctions.
    /// </summary>
    static List<Road> Unlooped(List<Road> roads, List<Vector2> centreM)
    {
        var kept = new List<Road>(roads.Count);
        foreach (var road in roads)
        {
            if (road.From != road.To)
            {
                kept.Add(road);
                continue;
            }

            var furthest = 0;
            var furthestM = 0f;
            for (var at = 1; at + 1 < road.PointsM.Count; at++)
            {
                var offM = Vector2.Distance(road.PointsM[at], centreM[road.From]);
                if (offM <= furthestM) continue;

                furthest = at;
                furthestM = offM;
            }

            if (furthest > 0) kept.AddRange(CutAt(road, furthest, centreM));
        }

        return kept;
    }

    /// <summary>One road cut at one of its own points into two, with a junction of two arms standing there.</summary>
    static Road[] CutAt(Road road, int at, List<Vector2> centreM)
    {
        var cut = centreM.Count;
        centreM.Add(road.PointsM[at]);
        return
        [
            new Road
            {
                From = road.From, To = cut, PointsM = road.PointsM[..(at + 1)], Carriage = road.Carriage, Highway = road.Highway,
                AtFrom = road.AtFrom, AtTo = Arrival.None, Ways = [.. road.Ways],
            },
            new Road
            {
                From = cut, To = road.To, PointsM = road.PointsM[at..], Carriage = road.Carriage, Highway = road.Highway,
                AtFrom = Arrival.None, AtTo = road.AtTo, Ways = [.. road.Ways],
            },
        ];
    }

    /// <summary>
    /// <b>Every road no longer than a traced map lays one</b> (<see cref="CityGenFigures.TracedRoadLongestM"/>):
    /// one that runs further is cut into as many even lengths as it takes, each at a place of two arms. It is the
    /// one place a traced town keeps a junction nothing meets at (GEN-51), and it keeps it because a lane is a
    /// whole road and a lane has a length past which nothing downstream files it.
    /// </summary>
    static List<Road> CutShort(List<Road> roads, List<Vector2> centreM, float longestM)
    {
        var kept = new List<Road>(roads.Count);
        foreach (var road in roads)
        {
            var lengthM = 0f;
            for (var at = 1; at < road.PointsM.Count; at++) lengthM += Vector2.Distance(road.PointsM[at - 1], road.PointsM[at]);

            var pieces = (int)MathF.Ceiling(lengthM / longestM);
            var rest = road;
            for (var piece = 1; piece < pieces; piece++)
            {
                var halves = CutAt(rest, Inserted(rest.PointsM, lengthM / pieces), centreM);
                kept.Add(halves[0]);
                rest = halves[1];
            }

            kept.Add(rest);
        }

        return kept;
    }

    /// <summary>The index of a point standing this far along a line, put into it where there is none.</summary>
    static int Inserted(List<Vector2> pointsM, float alongM)
    {
        for (var at = 1; at < pointsM.Count; at++)
        {
            var legM = Vector2.Distance(pointsM[at - 1], pointsM[at]);
            if (alongM > legM)
            {
                alongM -= legM;
                continue;
            }

            if (alongM >= legM) return at;

            pointsM.Insert(at, Vector2.Lerp(pointsM[at - 1], pointsM[at], alongM / legM));
            return at;
        }

        return pointsM.Count - 1;
    }

    /// <summary>
    /// <b>Every junction left holding two roads that carry on into each other is a place nothing meets at</b>
    /// (GEN-51) — a junction whose third arm was a stretch between two of its own places — and the two are one
    /// road through it. A pair whose far ends are one junction stays two, since one road would leave it and come
    /// back.
    /// </summary>
    static void JoinedThrough(List<Road> roads, int junctions)
    {
        var at = new List<Road>[junctions];
        for (var junction = 0; junction < junctions; junction++) at[junction] = [];
        foreach (var road in roads)
        {
            at[road.From].Add(road);
            at[road.To].Add(road);
        }

        for (var junction = 0; junction < junctions; junction++)
        {
            if (at[junction].Count != 2) continue;

            var (one, other) = (at[junction][0], at[junction][1]);
            if (one == other) continue;

            if (one.To != junction) one.Turn();
            if (other.From != junction) other.Turn();
            if (one.From == other.To || !CarriesOn(one.Carriage, false, other.Carriage, true)) continue;

            one.PointsM.AddRange(other.PointsM[1..]);
            one.Ways.AddRange(other.Ways[(other.Ways[0] == one.Ways[^1] ? 1 : 0)..]);
            one.To = other.To;
            one.AtTo = other.AtTo;
            if (Rank(other.Highway) > Rank(one.Highway)) one.Highway = other.Highway;

            other.Gone = true;
            at[junction].Clear();
            at[other.To][at[other.To].IndexOf(other)] = one;
        }

        roads.RemoveAll(road => road.Gone);
    }

    /// <summary>
    /// <b>One road's line</b>: the survey's own line from where it leaves its junction's disc — its own end's
    /// standoff about the junction's centre (<see cref="Standoffs"/>, TER-5d) — to where it enters the other's,
    /// normalised (<see cref="TracedAlignment"/>) and moved across to its carriageway's middle where OSM places the
    /// way off it, with every corner rounded only as tight as its own carriageway allows — and that line merged into
    /// the fewest pieces that keep it each way it may be (<see cref="TracedPieces"/>), the wider the road the more
    /// loosely (<see cref="CityGenFigures.TracedLineToleranceShare"/>), its corners those pieces' own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Laid as the survey drew it and not as a car would like it</b> (GEN-57): a bend is rounded as wide as keeps
    /// the survey within <see cref="CityGenFigures.TracedCornerToleranceM"/>, and a corner sharper than that at half its
    /// carriageway, whose inside kerb is the corner itself and no lane folds back over it. A road rounded for a
    /// speed is the drivers' to ask for later (GEN-47), and a reading of how far a traced town stands off its
    /// survey is <c>--bench fidelity</c>.
    /// </para>
    /// <para>
    /// <b>It leaves the disc where the survey does, on the survey's own heading</b>, and not on the chord from
    /// the centre to the first point outside: a way bending inside the disc would have that chord swing the
    /// whole of its first leg off the survey, metres at the far end of a long one. Nothing reads a traced
    /// road's arms (<c>ConnectionPoints.ArmOf</c>): its lanes end where its line does and its connectors are
    /// drawn between those ends.
    /// </para>
    /// </remarks>
    /// <returns>The line and the ways it may be merged, or nothing where the road has no line.</returns>
    static Merging? Line(Road road, List<Vector2> centreM, float fromStandoffM, float toStandoffM, Vector2 mapM, SimConfig config)
    {
        var pointsM = road.PointsM;
        var (leavesOn, startM) = Leaving(pointsM, centreM[road.From], fromStandoffM, forward: true);
        var (entersOn, endM) = Leaving(pointsM, centreM[road.To], toStandoffM, forward: false);
        if (leavesOn < 0 || entersOn < leavesOn) return null;

        var through = new List<Vector2>(entersOn - leavesOn);
        for (var at = leavesOn + 1; at <= entersOn; at++)
        {
            var atM = pointsM[at];
            if (atM != startM && atM != endM && (through.Count == 0 || atM != through[^1])) through.Add(atM);
        }

        if (through.Count == 0 && startM == endM) return null;

        var lanes = road.Carriage.Lanes.With + road.Carriage.Lanes.Against;
        var roundedM = road.Carriage.WidthM * 0.5f;

        // A road of one lane has nothing beside the lane to cover its ground where it folds, so the ground's own inner
        // edge is what may not fold: no tighter than half the carriageway. A wider road's innermost lane, at its share
        // of the carriageway (CityPlan.RoadArrays.LaneOffsetM), may not fold back over the corner itself, and nor may
        // the middle of a roadside — whichever of those stands furthest off the line, on whichever side the corner turns.
        var lone = lanes == 1;
        var outermostM = roundedM - MathF.Min(road.Carriage.RoadsideWithM, road.Carriage.RoadsideAgainstM)
                         - (road.Carriage.LanesWidthM / lanes * 0.5f);
        var roadsideMiddleM = MathF.Max(MiddleOf(road.Carriage.RoadsideWithM), MiddleOf(road.Carriage.RoadsideAgainstM));
        var floorM = lone ? roundedM : MathF.Max(outermostM, roadsideMiddleM) + config.CityGen.TracedTightestLaneRadiusM;
        while (true)
        {
            var surveyedM = new Vector2[through.Count + 2];
            surveyedM[0] = startM;
            through.CopyTo(surveyedM, 1);
            surveyedM[^1] = endM;

            // The road's own line is its carriageway's middle, which OSM places off the way where it says so.
            var surveyedLaidM = new Vector2[surveyedM.Length];
            OsmCarriageway.OffsetInto(surveyedM, road.Carriage.CentreOffsetM, surveyedLaidM);
            var laidM = surveyedLaidM;

            // <b>No lane folds back over a corner</b>: a point the legs either side have no room to round even
            // with the innermost lane at its tightest is a survey's kink and not a bend, and it is eased.
            var atRoundedM = Filled(laidM.Length - 2, roundedM);
            var reachM = TracedAlignment.Reaches(laidM, atRoundedM, atRoundedM);
            var tightest = TightestCorner(laidM, reachM, floorM);
            if (tightest > 0)
            {
                Eased(through, surveyedM, tightest);
                continue;
            }

            // Normalised where every corner of it still has the room; a kink's easing is the survey's line, so the
            // two are asked in that order and not the other.
            var (alignedM, alignedReachM) = Aligned(surveyedM, road.Carriage.CentreOffsetM, roundedM, config);
            if (TightestCorner(alignedM, alignedReachM, floorM) == 0) (laidM, reachM) = (alignedM, alignedReachM);

            var arcs = new ArcSeg[(2 * laidM.Length) - 3];
            var rounded = arcs[..Spline.RoundedInto(laidM, reachM, arcs)];
            return new Merging(rounded, surveyedLaidM, mapM, MathF.Max(roundedM, floorM), config.TracedLineToleranceAcrossM(road.Carriage.WidthM));
        }

        // Off the road's line toward its kerb, the middle of a roadside that wide — or nothing, where there is none.
        float MiddleOf(float roadsideM) => roadsideM > 0f ? roundedM - (roadsideM * 0.5f) : 0f;
    }

    /// <summary>
    /// <b>A road's surveyed line normalised</b> (<see cref="TracedAlignment"/>) and moved across to its carriageway's
    /// middle: its corners, and how far back along its legs each is rounded from.
    /// </summary>
    /// <remarks>
    /// A corner keeps its centre across the move, so a middle moved toward it rounds tighter by as much and one
    /// moved away wider — and never tighter than <paramref name="roundedM"/>.
    /// </remarks>
    static (Vector2[] LaidM, float[] ReachM) Aligned(Vector2[] surveyedM, float centreOffsetM, float roundedM, SimConfig config)
    {
        var (cornersM, tightestM, widestM) = TracedAlignment.Of(surveyedM, roundedM, config.CityGen.TracedCornerToleranceM);
        var laidM = new Vector2[cornersM.Length];
        OsmCarriageway.OffsetInto(cornersM, centreOffsetM, laidM);
        for (var corner = 1; corner < laidM.Length - 1; corner++)
        {
            var inwardM = centreOffsetM * MathF.Sign(Spline.Cross(laidM[corner] - laidM[corner - 1], laidM[corner + 1] - laidM[corner]));
            tightestM[corner - 1] = MathF.Max(roundedM, tightestM[corner - 1] - inwardM);
            widestM[corner - 1] = MathF.Max(roundedM, widestM[corner - 1] - inwardM);
        }

        return (laidM, TracedAlignment.Reaches(laidM, tightestM, widestM));
    }

    /// <summary>
    /// <b>A kink eased the way that stands least off the survey</b>: the kink taken out, or a point beside it, or
    /// the kink and a point beside it carried on along their outer legs to where those meet — whichever leaves the
    /// line nearest the line it was, read at the points either one has and the other has not.
    /// </summary>
    /// <remarks>
    /// Taking the kink out swings both its legs onto the straight between their far ends, metres off the survey at
    /// the end of a long one; a kink of a few short legs in the corner of two long ones is eased with the long ones
    /// kept, meeting in the one corner the short ones made. Every easing takes a point out, so easing ends.
    /// </remarks>
    /// <param name="lineM">The line as surveyed, its two ends where it leaves the discs, which never move.</param>
    /// <param name="kink">The kink, as an index into <paramref name="lineM"/>.</param>
    static void Eased(List<Vector2> through, ReadOnlySpan<Vector2> lineM, int kink)
    {
        var (lowest, highest) = (Math.Max(1, kink - 1), Math.Min(lineM.Length - 2, kink + 1));
        var (offM, taken, meets, meetM) = (float.PositiveInfinity, 0, false, Vector2.Zero);
        for (var point = lowest; point <= highest; point++)
        {
            var takenOffM = OffM(lineM[point], [lineM[point - 1], lineM[point + 1]]);
            if (takenOffM < offM) (offM, taken, meets) = (takenOffM, point, false);
        }

        for (var first = lowest; first < highest; first++)
        {
            var wasM = lineM[(first - 1)..(first + 3)];
            if (!Meeting(wasM, out var atM)) continue;

            ReadOnlySpan<Vector2> nowM = [wasM[0], atM, wasM[3]];
            var metOffM = MathF.Max(OffM(atM, wasM), MathF.Max(OffM(wasM[1], nowM), OffM(wasM[2], nowM)));
            if (metOffM < offM) (offM, taken, meets, meetM) = (metOffM, first, true, atM);
        }

        // The line's points past its first are the through points, one index along.
        if (meets) through[taken - 1] = meetM;
        through.RemoveAt(meets ? taken : taken - 1);
    }

    /// <summary>
    /// Where the first leg of four points carried on meets the last carried back, both forwards of where they
    /// were — and not at all where they run parallel or meet behind either.
    /// </summary>
    /// <remarks>
    /// Two legs all but parallel meet far off, which stands that far off the survey and is never the easing
    /// taken (<see cref="Eased"/>), so only parallel itself is refused here.
    /// </remarks>
    static bool Meeting(ReadOnlySpan<Vector2> legsM, out Vector2 atM)
    {
        var arriving = legsM[1] - legsM[0];
        var leaving = legsM[2] - legsM[3];
        var across = Cross(arriving, leaving);
        atM = default;
        if (across == 0f) return false;

        var between = legsM[3] - legsM[0];
        var alongArriving = Cross(between, leaving) / across;
        var alongLeaving = Cross(between, arriving) / across;
        if (alongArriving <= 0f || alongLeaving <= 0f) return false;

        atM = legsM[0] + (arriving * alongArriving);
        return true;

        static float Cross(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);
    }

    /// <summary>How far a point stands off the nearest place on a line.</summary>
    static float OffM(Vector2 pointM, ReadOnlySpan<Vector2> lineM)
    {
        var offM = float.PositiveInfinity;
        for (var at = 1; at < lineM.Length; at++)
        {
            var runM = lineM[at] - lineM[at - 1];
            var lengthSquared = runM.LengthSquared();
            var along = lengthSquared > 0f ? Math.Clamp(Vector2.Dot(pointM - lineM[at - 1], runM) / lengthSquared, 0f, 1f) : 0f;
            offM = MathF.Min(offM, (pointM - (lineM[at - 1] + (runM * along))).Length());
        }

        return offM;
    }

    /// <summary>
    /// <b>Where a line first leaves a disc</b>, walked from its first point or, against it, from its last: the leg
    /// it leaves on, as the index of that leg's first point counted the line's own way, and the place it crosses
    /// the disc's edge — and a leg of −1 where it never leaves.
    /// </summary>
    static (int Leg, Vector2 AtM) Leaving(List<Vector2> pointsM, Vector2 centreM, float radiusM, bool forward)
    {
        var radiusSq = radiusM * radiusM;
        for (var step = 1; step < pointsM.Count; step++)
        {
            var (inside, outside) = forward ? (step - 1, step) : (pointsM.Count - step, pointsM.Count - step - 1);
            if (Vector2.DistanceSquared(pointsM[outside], centreM) <= radiusSq) continue;

            // The far root of |inside + t·leg − centre| = radius, the one point past which the leg is outside.
            var offM = pointsM[inside] - centreM;
            var legM = pointsM[outside] - pointsM[inside];
            var along = Vector2.Dot(offM, legM);
            var legSq = legM.LengthSquared();
            var t = (-along + MathF.Sqrt(MathF.Max(0f, (along * along) - (legSq * (offM.LengthSquared() - radiusSq))))) / legSq;
            return (Math.Min(inside, outside), pointsM[inside] + (legM * Math.Clamp(t, 0f, 1f)));
        }

        return (-1, default);
    }

    /// <summary>
    /// The corner of a line whose reach (<see cref="TracedAlignment.Reaches"/>) rounds it tightest below <paramref name="floorM"/>,
    /// or nought where none does.
    /// </summary>
    /// <remarks>
    /// Asked as the reach a corner needs against the reach it has, and never as a radius read back through the
    /// turn's tangent: a corner with exactly the reach rounds at exactly the radius, and the radius read back
    /// can come out a float's step short of it.
    /// </remarks>
    static int TightestCorner(ReadOnlySpan<Vector2> pointsM, float[] reachM, float floorM)
    {
        var tightest = 0;
        var tightestM = float.PositiveInfinity;
        for (var corner = 1; corner < pointsM.Length - 1; corner++)
        {
            var halfTurnTan = Spline.HalfTurnTan(pointsM, corner);
            if (!(halfTurnTan > 0f) || reachM[corner - 1] >= floorM * halfTurnTan) continue;

            var radiusM = reachM[corner - 1] / halfTurnTan;
            if (radiusM >= tightestM) continue;

            tightest = corner;
            tightestM = radiusM;
        }

        return tightest;
    }

    /// <param name="gathered">What <see cref="Gathered"/> made of the junctions standing close together.</param>
    static Laid Arrays(
        Survey survey, int[] junctionOf, SimConfig config, List<Road> roads, ArcSeg[][] lines, Vector2[][] through,
        List<Vector2> centreM, (float[] FromM, float[] ToM) standoffM, bool[] runsOff, SharedLaid shared,
        (int[] Into, List<Unmade> Unmade, HashSet<OsmTurnRestriction> Absorbed, GatheredLaid Tally) gathered)
    {
        var keepsRight = config.Road.TrafficKeepsRight;
        var into = gathered.Into;
        var boxM = Boxes(roads, into, centreM, standoffM);
        var renumbered = new int[centreM.Count];
        Array.Fill(renumbered, CityPlan.NoRecord);
        var keptM = new List<Vector2>();
        var radiusM = new List<float>();
        var keptRunsOff = new List<bool>();
        var laidRoads = new List<Road>(roads.Count);
        var wayOffsets = new List<int> { 0 };
        var ways = new List<int>();

        var fromJunction = new List<int>();
        var toJunction = new List<int>();
        var widthM = new List<float>();
        var flow = new List<RoadFlow>();
        var lanes = new List<RoadLanes>();
        var segmentOffsets = new List<int> { 0 };
        var segments = new List<ArcSeg>();
        var throughOffsets = new List<int> { 0 };
        var throughM = new List<Vector2>();
        var level = new List<byte>();
        var roadsideWithM = new List<float>();
        var roadsideAgainstM = new List<float>();

        for (var road = 0; road < roads.Count; road++)
        {
            if (roads[road].Gone) continue;

            level.Add(roads[road].Carriage.Level);
            roadsideWithM.Add(roads[road].Carriage.RoadsideWithM);
            roadsideAgainstM.Add(roads[road].Carriage.RoadsideAgainstM);
            laidRoads.Add(roads[road]);
            fromJunction.Add(Kept(roads[road].From));
            toJunction.Add(Kept(roads[road].To));
            widthM.Add(roads[road].Carriage.WidthM);
            flow.Add(roads[road].Carriage.Lanes.Flow);
            lanes.Add(roads[road].Carriage.Lanes);
            segments.AddRange(lines[road]);
            segmentOffsets.Add(segments.Count);
            throughM.AddRange(through[road]);
            throughOffsets.Add(throughM.Count);
            ways.AddRange(roads[road].Ways);
            wayOffsets.Add(ways.Count);
        }

        var junctionAt = new int[junctionOf.Length];
        for (var point = 0; point < junctionAt.Length; point++)
        {
            junctionAt[point] = junctionOf[point] == CityPlan.NoRecord ? CityPlan.NoRecord : renumbered[into[junctionOf[point]]];
        }

        var (lit, phaseOffsetS) = Lights(survey, junctionAt, keptM.Count, config.Signals.CycleS);
        var junctions = new CityPlan.JunctionArrays
        {
            CentreM = [.. keptM],
            RadiusM = [.. radiusM],
            Lit = lit,
            PhaseOffsetS = phaseOffsetS,
            RunsOffTheMap = keptRunsOff.Contains(true) ? [.. keptRunsOff] : [],
        };

        var (arrows, arrowOffsets, arrowedEnds) = Arrowed(survey, laidRoads, keepsRight);
        var (bans, links, tally) = Turned(survey, laidRoads, junctionOf, renumbered, gathered.Unmade, gathered.Absorbed, keepsRight);
        var roadsides = roadsideWithM.Exists(m => m > 0f) || roadsideAgainstM.Exists(m => m > 0f);
        var laid = new CityPlan.RoadArrays
        {
            FromJunction = [.. fromJunction], ToJunction = [.. toJunction], WidthM = [.. widthM], Flow = [.. flow],
            Lanes = [.. lanes], SegmentOffsets = [.. segmentOffsets], Segments = [.. segments],
            ThroughOffsets = [.. throughOffsets], ThroughM = [.. throughM],
            LaidStraight = Filled(widthM.Count, true),
            MarkedTurns = arrows, MarkedTurnOffsets = arrowOffsets, BannedTurns = bans, LaneLinks = links,
            Level = level.Contains(CityPlan.RoadArrays.Over) ? [.. level] : [],
            RoadsideWithM = roadsides ? [.. roadsideWithM] : [],
            RoadsideAgainstM = roadsides ? [.. roadsideAgainstM] : [],
        };

        return new Laid(
            junctions, laid, tally with { ArrowedEnds = arrowedEnds }, shared, gathered.Tally, [.. wayOffsets], [.. ways], junctionAt,
            Decks(laid, config), Rings(laidRoads, laid));

        int Kept(int junction)
        {
            if (renumbered[junction] != CityPlan.NoRecord) return renumbered[junction];
            if (into[junction] != junction) return renumbered[junction] = Kept(into[junction]);

            renumbered[junction] = keptM.Count;
            keptM.Add(centreM[junction]);
            radiusM.Add(boxM[junction]);
            keptRunsOff.Add(runsOff[junction]);
            return renumbered[junction];
        }
    }

    /// <summary>
    /// How far each junction's box reaches from where it stands: the furthest any road's lanes end off it — off the
    /// place each was surveyed to, for one gathered from several (<see cref="Gathered"/>).
    /// </summary>
    static float[] Boxes(List<Road> roads, int[] into, List<Vector2> centreM, (float[] FromM, float[] ToM) standoffM)
    {
        var boxM = new float[centreM.Count];
        for (var road = 0; road < roads.Count; road++)
        {
            if (roads[road].Gone) continue;

            foreach (var (junction, endM) in (ReadOnlySpan<(int, float)>)[(roads[road].From, standoffM.FromM[road]), (roads[road].To, standoffM.ToM[road])])
            {
                var laidAs = into[junction];
                boxM[laidAs] = MathF.Max(boxM[laidAs], Vector2.Distance(centreM[junction], centreM[laidAs]) + endM);
            }
        }

        return boxM;
    }

    /// <summary>
    /// <b>The roundabouts</b> (GEN-19): the roads OSM tags as circulating, each ring those of them that meet at their
    /// junctions. A ring is membership and no geometry, as a generated one is; what it changes is where the kerb ends
    /// stand no station and which roads carry no walk.
    /// </summary>
    static CityPlan.RoundaboutArrays Rings(List<Road> laidRoads, CityPlan.RoadArrays roads)
    {
        var ringOf = new Dictionary<int, int>();
        var parent = new List<int>();
        for (var road = 0; road < laidRoads.Count; road++)
        {
            if (!laidRoads[road].Carriage.Circulates) continue;

            var (from, to) = (Root(roads.FromJunction[road]), Root(roads.ToJunction[road]));
            if (from != to) parent[from] = to;
        }

        var byRing = new SortedDictionary<int, List<int>>();
        for (var road = 0; road < laidRoads.Count; road++)
        {
            if (!laidRoads[road].Carriage.Circulates) continue;

            var ring = Root(roads.FromJunction[road]);
            if (!byRing.TryGetValue(ring, out var members)) byRing[ring] = members = [];
            members.Add(road);
        }

        var offsets = new List<int> { 0 };
        var ringRoads = new List<int>();
        foreach (var members in byRing.Values)
        {
            ringRoads.AddRange(members);
            offsets.Add(ringRoads.Count);
        }

        return new CityPlan.RoundaboutArrays { RingOffsets = [.. offsets], Road = [.. ringRoads] };

        // A ring is named by the root of its junctions, found by halving the path to it as it is walked.
        int Root(int junction)
        {
            if (!ringOf.TryGetValue(junction, out var at))
            {
                ringOf[junction] = at = parent.Count;
                parent.Add(at);
            }

            while (parent[at] != at) at = parent[at] = parent[parent[at]];
            return at;
        }
    }

    /// <summary>
    /// <b>A deck under every bridge</b> (TER-3b): each bridge is a road of its own (<see cref="Places"/>), so its deck
    /// runs the whole of it — as wide as its carriageway and the walk either side, which is what the ground answers
    /// as the deck's margin.
    /// </summary>
    static CityPlan.BridgeArrays Decks(CityPlan.RoadArrays roads, SimConfig config)
    {
        var (road, toM, deckWidthM) = (new List<int>(), new List<float>(), new List<float>());
        for (var at = 0; at < roads.Count; at++)
        {
            if (roads.LevelOf(at) != CityPlan.RoadArrays.Over) continue;

            road.Add(at);
            toM.Add(Spline.TotalLengthM(roads.SegmentsOf(at)));
            deckWidthM.Add(roads.WidthM[at] + (2f * config.WalkOuterM));
        }

        return new CityPlan.BridgeArrays
        {
            Road = [.. road], FromM = new float[road.Count], ToM = [.. toM], DeckWidthM = [.. deckWidthM],
            PavementWidthM = Filled(road.Count, config.PavementWidthM),
        };
    }

    /// <summary>
    /// <b>The junctions with lights, as the survey says</b> (GEN-57): each standing at a point the survey's pack reads
    /// as signalled — or gathered from one that does (<see cref="Gathered"/>) — and none else; whether one is lit at all
    /// is still the lights' own question of its arms (TLT-3). <b>The junctions controlled as one share a clock</b>, a
    /// dual carriageway's crossing OSM draws as four junctions being one set of lights, and each such set starts its
    /// cycle at a place read off the node it is named by, so no seed draws it and two sets apart are not in step.
    /// </summary>
    /// <param name="junctionAt">The junction standing at each survey point, or <see cref="CityPlan.NoRecord"/>.</param>
    static (bool[] Lit, float[] PhaseOffsetS) Lights(Survey survey, int[] junctionAt, int junctions, float cycleS)
    {
        var lit = new bool[junctions];
        var offsetS = new float[junctions];
        for (var point = 0; point < junctionAt.Length; point++)
        {
            var junction = junctionAt[point];
            if (junction == CityPlan.NoRecord || lit[junction] || survey.ControlAt(point) != SurveyControl.Signals) continue;

            lit[junction] = true;
            var control = survey.Controls[point];
            var named = control.Cluster != 0 ? control.Cluster : point;
            offsetS[junction] = cycleS * (float)(((ulong)named * 0x9E3779B97F4A7C15UL) >> 40) / (1 << 24);
        }

        return (lit, offsetS);
    }

    /// <summary>
    /// <b>The arrows on every road's lanes at the end they run into</b>, where the way they arrive on ends there
    /// (Key:turn): a road's lanes with it from the kerb, then those against it, or nothing for a road with none
    /// painted — and how many road ends carry some.
    /// </summary>
    /// <remarks>
    /// A way's arrows are for the junction it ends at. A way running on through a junction is not turned off there as
    /// its arrows say, so a road ending at that junction runs into it unmarked.
    /// </remarks>
    static (MarkedTurns[] Arrows, int[] Offsets, int Ends) Arrowed(Survey survey, List<Road> roads, bool keepsRight)
    {
        var arrows = new List<MarkedTurns>();
        var offsets = new int[roads.Count + 1];
        var ends = 0;
        for (var road = 0; road < roads.Count; road++)
        {
            var lanes = roads[road].Carriage.Lanes;
            var with = Painted(survey, roads[road].AtTo, lanes.With, keepsRight);
            var against = Painted(survey, roads[road].AtFrom, lanes.Against, keepsRight);
            if (with.Length + against.Length > 0)
            {
                arrows.AddRange(with.Length > 0 ? with : new MarkedTurns[lanes.With]);
                arrows.AddRange(against.Length > 0 ? against : new MarkedTurns[lanes.Against]);
                ends += (with.Length > 0 ? 1 : 0) + (against.Length > 0 ? 1 : 0);
            }

            offsets[road + 1] = arrows.Count;
        }

        return arrows.Count > 0 ? ([.. arrows], offsets, ends) : ([], [], 0);
    }

    /// <summary>
    /// The arrows on the lanes arriving at one road end, from the kerb — where the way they arrive on ends there and
    /// paints as many lanes as the road lays that way — or none.
    /// </summary>
    static MarkedTurns[] Painted(Survey survey, Arrival arrival, int lanes, bool keepsRight)
    {
        if (!arrival.WayEnds || lanes == 0) return [];

        var leftToRight = DrivenArrows(survey.Ways[arrival.Way], arrival.Along);
        if (leftToRight.Length != lanes || Array.TrueForAll(leftToRight, arrow => arrow == OsmArrows.None)) return [];

        var fromKerb = new MarkedTurns[lanes];
        for (var lane = 0; lane < lanes; lane++) fromKerb[lane] = InOurWords(leftToRight[keepsRight ? lanes - 1 - lane : lane], keepsRight);

        return fromKerb;
    }

    /// <summary>OSM's arrows in this engine's words, the near side being the side traffic keeps to.</summary>
    static MarkedTurns InOurWords(OsmArrows osm, bool keepsRight)
    {
        var (left, right) = keepsRight ? (MarkedTurns.FarSide, MarkedTurns.NearSide) : (MarkedTurns.NearSide, MarkedTurns.FarSide);
        var (bearLeft, bearRight) = keepsRight
            ? (MarkedTurns.BearFarSide, MarkedTurns.BearNearSide)
            : (MarkedTurns.BearNearSide, MarkedTurns.BearFarSide);

        var arrows = MarkedTurns.None;
        if ((osm & (OsmArrows.Through | OsmArrows.MergeToLeft | OsmArrows.MergeToRight)) != 0) arrows |= MarkedTurns.Straight;
        if ((osm & (OsmArrows.Left | OsmArrows.SharpLeft)) != 0) arrows |= left;
        if ((osm & (OsmArrows.Right | OsmArrows.SharpRight)) != 0) arrows |= right;
        if (osm.HasFlag(OsmArrows.SlightLeft)) arrows |= bearLeft;
        if (osm.HasFlag(OsmArrows.SlightRight)) arrows |= bearRight;

        // A U-turn crosses the oncoming traffic whichever side is kept.
        if (osm.HasFlag(OsmArrows.Reverse)) arrows |= MarkedTurns.FarSide;
        return arrows;
    }

    /// <summary>
    /// <b>Every turn a restriction forbids and every lane link, on the roads as laid</b> (<see cref="OsmTurns"/>): a
    /// <c>no_</c> turn forbidden from the road its way arrives on into the road its other way leaves on, and an
    /// <c>only_</c> one forbidding every other turn off that road there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One is laid where a junction stands at its node and each of its ways has exactly one road end there. A node two
    /// ways merely carry on through is no junction and turns nowhere; and a way running on through its node has two
    /// ends there, of which the relation does not say which — OSM asks a restriction's ways to end at its node.
    /// </para>
    /// <para>
    /// <b>At a junction gathered from several</b> (<see cref="Gathered"/>) each is read at the place its node was, among
    /// the roads that reach that place, and so is a turn none of the roads between them made forbidden — which is
    /// where one naming a road between them was laid.
    /// </para>
    /// </remarks>
    static (RoadTurn[] Bans, LaneLink[] Links, TurnsLaid Tally) Turned(
        Survey survey, List<Road> roads, int[] junctionOf, int[] renumbered, List<Unmade> unmade, HashSet<OsmTurnRestriction> absorbed,
        bool keepsRight)
    {
        var at = new Dictionary<int, List<(Road Road, bool AtTo)>>();
        var indexOf = new Dictionary<Road, int>(roads.Count);
        for (var road = 0; road < roads.Count; road++)
        {
            indexOf[roads[road]] = road;
            Add(roads[road].From, roads[road], false);
            Add(roads[road].To, roads[road], true);
        }

        var bans = new HashSet<RoadTurn>();
        var (laid, atNoJunction, unmatched) = (0, 0, 0);
        foreach (var turn in survey.Turns.Restrictions)
        {
            if (absorbed.Contains(turn))
            {
                laid++;
                continue;
            }

            if (JunctionAt(turn.Via) is not { } junction)
            {
                atNoJunction++;
                continue;
            }

            var (ends, from, to) = (at[junction], EndOf(survey, at[junction], turn.From), EndOf(survey, at[junction], turn.To));
            if (from < 0 || to < 0)
            {
                unmatched++;
                continue;
            }

            laid++;
            foreach (var (other, _) in ends)
            {
                if (turn.Only ? other != ends[to].Road : other == ends[to].Road)
                {
                    bans.Add(new RoadTurn(renumbered[junction], indexOf[ends[from].Road], indexOf[other]));
                }
            }
        }

        foreach (var (junction, from, to) in unmade)
        {
            if (indexOf.TryGetValue(from, out var off) && indexOf.TryGetValue(to, out var onto)) bans.Add(new RoadTurn(renumbered[junction], off, onto));
        }

        var links = new List<LaneLink>();
        var linksNotLaid = 0;
        foreach (var link in survey.Turns.LaneLinks)
        {
            if (JunctionAt(link.Via) is not { } junction)
            {
                linksNotLaid++;
                continue;
            }

            var (from, to) = (EndOf(survey, at[junction], link.From), EndOf(survey, at[junction], link.To));
            if (from < 0 || to < 0)
            {
                linksNotLaid++;
                continue;
            }

            var (off, onto) = (at[junction][from], at[junction][to]);
            var arriving = off.AtTo ? off.Road.Carriage.Lanes.With : off.Road.Carriage.Lanes.Against;
            var leaving = onto.AtTo ? onto.Road.Carriage.Lanes.Against : onto.Road.Carriage.Lanes.With;
            if (link.FromLane > arriving || link.ToLane > leaving)
            {
                linksNotLaid++;
                continue;
            }

            links.Add(new LaneLink(
                renumbered[junction], indexOf[off.Road], FromKerb(link.FromLane, arriving), indexOf[onto.Road], FromKerb(link.ToLane, leaving)));
        }

        return ([.. bans], [.. links], new TurnsLaid(0, laid, atNoJunction, unmatched, bans.Count, links.Count, linksNotLaid));

        void Add(int junction, Road road, bool atTo)
        {
            if (!at.TryGetValue(junction, out var ends)) at[junction] = ends = [];
            ends.Add((road, atTo));
        }

        int? JunctionAt(int node) =>
            (uint)node < (uint)junctionOf.Length && junctionOf[node] != CityPlan.NoRecord && at.ContainsKey(junctionOf[node])
                ? junctionOf[node]
                : null;

        // OSM counts a way's lanes from the left as its traffic looks.
        int FromKerb(int fromLeft, int lanes) => keepsRight ? lanes - fromLeft : fromLeft - 1;
    }

    static T[] Filled<T>(int count, T value)
    {
        var filled = new T[count];
        Array.Fill(filled, value);
        return filled;
    }

    /// <summary>
    /// How much a way matters by its class, for which class a road walked through several ways is read as: nought for
    /// any class that is not a street.
    /// </summary>
    internal static int Rank(string highway) => highway switch
    {
        "motorway" => 7,
        "trunk" => 6,
        "primary" => 5,
        "secondary" => 4,
        "tertiary" => 3,
        "unclassified" or "residential" => 2,
        "living_street" => 1,
        _ => 0,
    };
}
