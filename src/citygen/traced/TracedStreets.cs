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
/// there, however close to the next; every point a way was drawn through is on its road; a road's corners
/// are rounded only as tight as its own carriageway allows; and a piece of the network joined to nothing
/// else is kept. The generator's rules that would take something out — one junction for two inside a
/// locality (GEN-16), a corner no tighter than a class's design speed (GEN-47), one connected network (GEN-5)
/// — are not asked of a traced town, and how far it stands off its survey is <c>--bench fidelity</c>'s.
/// </para>
/// <para>
/// <b>What a survey says and a generated town never has is kept</b>: a dead end, a junction of six arms, two
/// arms at a shallow angle, a one-way carriageway beside its twin, four junctions where two dual carriageways
/// cross. They are the place, and a rule refusing them would be a rule refusing the city it was asked to trace.
/// </para>
/// </remarks>
internal static class TracedStreets
{
    internal readonly record struct Laid(CityPlan.JunctionArrays Junctions, CityPlan.RoadArrays Roads);

    /// <summary>One run of a way between two places it meets another, in the way's own order.</summary>
    readonly record struct Edge(int[] Points, int Way)
    {
        public int First => Points[0];

        public int Last => Points[^1];
    }

    /// <summary>
    /// A road's carriageway as OSM has it: its lanes each way, every lane's width together, where its middle
    /// stands off the surveyed line, to the right of it as the road runs, and whether it is one lane both ways
    /// share.
    /// </summary>
    readonly record struct Carriage(RoadLanes Lanes, float WidthM, float CentreOffsetM, bool Shared)
    {
        public Carriage Turned => new(Lanes.Turned, WidthM, -CentreOffsetM, Shared);
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
        public bool Gone;

        public void Turn()
        {
            (From, To) = (To, From);
            PointsM.Reverse();
            Carriage = Carriage.Turned;
        }
    }

    public static Laid Lay(Survey survey, SimConfig config)
    {
        var edges = Edges(survey);
        var ends = EndsAt(survey.PointCount, edges);
        var place = Places(survey, edges, ends);
        var (junctionOf, centreM) = Junctions(survey, place);

        var roads = Walked(survey, edges, ends, place, junctionOf);
        roads = Unlooped(roads, centreM);
        JoinedThrough(roads, centreM.Count);
        roads = CutShort(roads, centreM, config.CityGen.TracedRoadLongestM);

        var standoffM = Standoffs(roads, centreM, config);
        var lines = new ArcSeg[roads.Count][];
        var through = new Vector2[roads.Count][];
        for (var road = 0; road < roads.Count; road++)
        {
            if (roads[road].Gone) continue;

            (lines[road], through[road]) = Line(roads[road], centreM, standoffM, config);
            if (lines[road].Length == 0) roads[road].Gone = true;
        }

        return Arrays(roads, lines, through, centreM, standoffM);
    }

    /// <summary>
    /// <b>How many lanes a surveyed way is driven in each way</b> (GEN-57): OSM's own, as the scanner read them
    /// (<see cref="OsmCarriageway"/>). A single lane two-way traffic shares is one lane each way laid over one
    /// line (<see cref="CityPlan.RoadArrays.SharedLane"/>). Lanes driven both ways down the middle of lanes each
    /// way — a tidal pair, a centre turning lane — are laid each as a lane of one way, the half nearer the lanes
    /// against the way as theirs and the odd one with it, so every lane stands where OSM puts it.
    /// </summary>
    internal static RoadLanes Driven(SurveyWay way)
    {
        if (way.Oneway) return new RoadLanes(AtLeastOne(way.LanesForward), 0);

        if (way.LanesForward == 0 && way.LanesBackward == 0) return new RoadLanes(1, 1);

        var againstShare = way.LanesShared / 2;
        return new RoadLanes(
            AtLeastOne(way.LanesForward + way.LanesShared - againstShare), AtLeastOne(way.LanesBackward + againstShare));

        static byte AtLeastOne(int lanes) => (byte)Math.Clamp(lanes, 1, byte.MaxValue);
    }

    /// <summary>
    /// <b>How far off each junction's centre its lanes end</b> (TER-5d): the town's own standoff, stood out by
    /// however much wider than a street of one lane each way its widest arm is (<see cref="SimConfig.JunctionRadiusAcrossM"/>)
    /// — and never past the middle of the way to its nearest neighbour, less the shortest road a traced map lays
    /// (<see cref="CityGenFigures.TracedShortestRoadM"/>), so a road the survey drew between two places close
    /// together is a short road and never lost.
    /// </summary>
    static float[] Standoffs(List<Road> roads, List<Vector2> centreM, SimConfig config)
    {
        var widestM = new float[centreM.Count];
        foreach (var road in roads)
        {
            if (road.Gone) continue;

            var widthM = road.Carriage.WidthM;
            widestM[road.From] = MathF.Max(widestM[road.From], widthM);
            widestM[road.To] = MathF.Max(widestM[road.To], widthM);
        }

        var standoffM = new float[centreM.Count];
        for (var junction = 0; junction < standoffM.Length; junction++) standoffM[junction] = config.JunctionRadiusAcrossM(widestM[junction]);

        foreach (var road in roads)
        {
            if (road.Gone) continue;

            var roomM = MathF.Max(0f, (Vector2.Distance(centreM[road.From], centreM[road.To]) - config.CityGen.TracedShortestRoadM) * 0.5f);
            standoffM[road.From] = MathF.Min(standoffM[road.From], roomM);
            standoffM[road.To] = MathF.Min(standoffM[road.To], roomM);
        }

        return standoffM;
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
        return new Carriage(Driven(way), way.CarriagewayM, way.CentreOffsetM, shared);
    }

    /// <summary>
    /// <b>Whether each point is a place</b> — somewhere ways meet, a way ends, or traffic cannot simply carry
    /// on. A point exactly two edges meet at is a place only where the two disagree about their carriageway:
    /// a one-way street running into a two-way one, two one-way streets both arriving, a carriageway that gains
    /// or loses a lane there (GEN-51), or one OSM widens, narrows or places off its line differently.
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
    /// <b>The junctions: one at every place, standing exactly where the survey put it</b> (GEN-57). Two places
    /// however close are two junctions and the road between them is a short road — a dual carriageway's
    /// crossing is the four junctions OSM draws it as, not one the engine made up between them.
    /// </summary>
    static (int[] JunctionOf, List<Vector2> CentreM) Junctions(Survey survey, bool[] place)
    {
        var junctionOf = new int[place.Length];
        Array.Fill(junctionOf, CityPlan.NoRecord);
        var centreM = new List<Vector2>();
        for (var point = 0; point < place.Length; point++)
        {
            if (!place[point]) continue;

            junctionOf[point] = centreM.Count;
            centreM.Add(survey.PointM(point));
        }

        return (junctionOf, centreM);
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

                var edge = first;
                var forward = atFirst;
                while (true)
                {
                    walked[edge] = true;
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

                roads.Add(new Road
                {
                    From = junctionOf[point], To = junctionOf[points[^1]], PointsM = pointsM, Carriage = carriage,
                    Highway = highway,
                });
            }
        }

        return roads;
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
            },
            new Road
            {
                From = cut, To = road.To, PointsM = road.PointsM[at..], Carriage = road.Carriage, Highway = road.Highway,
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
            one.To = other.To;
            if (Rank(other.Highway) > Rank(one.Highway)) one.Highway = other.Highway;

            other.Gone = true;
            at[junction].Clear();
            at[other.To][at[other.To].IndexOf(other)] = one;
        }

        roads.RemoveAll(road => road.Gone);
    }

    /// <summary>
    /// <b>One road's line</b>: from its junction's centre through every point the survey drew it through to
    /// the other's, stood each junction's own standoff off its centre (<see cref="Standoffs"/>, TER-5d), moved
    /// across to its carriageway's middle where OSM places the way off it, with every corner rounded only as
    /// tight as its own carriageway allows.
    /// </summary>
    /// <remarks>
    /// <b>Laid as the survey drew it and not as a car would like it</b> (GEN-57): rounded at half its carriageway,
    /// a corner's inside kerb is the corner itself and no lane folds back over it, and the line stands off the
    /// surveyed point by no more than that radius's own sag. A rounder road is the drivers' to ask for later
    /// (GEN-47), and a reading of how far a traced town stands off its survey is <c>--bench fidelity</c>.
    /// </remarks>
    static (ArcSeg[] Line, Vector2[] ThroughM) Line(Road road, List<Vector2> centreM, float[] standoffM, SimConfig config)
    {
        var fromM = centreM[road.From];
        var toM = centreM[road.To];
        var fromStandoffM = standoffM[road.From];
        var toStandoffM = standoffM[road.To];

        var through = new List<Vector2>(road.PointsM.Count);
        for (var at = 1; at + 1 < road.PointsM.Count; at++)
        {
            var atM = road.PointsM[at];
            if (atM != fromM && atM != toM && (through.Count == 0 || atM != through[^1])) through.Add(atM);
        }

        var lanes = road.Carriage.Lanes.With + road.Carriage.Lanes.Against;
        var roundedM = road.Carriage.WidthM * 0.5f;

        // A road of one lane on its line — both ways sharing it, or one way of one — has nothing beside the lane to
        // cover its ground where it folds, so the ground's own inner edge is what may not fold: no tighter than half
        // the carriageway. A wider road's innermost lane, at its share of the carriageway (CityPlan.RoadArrays.
        // LaneOffsetM), may not fold back over the corner itself.
        var lone = road.Carriage.Shared || lanes == 1;
        var floorM = lone
            ? roundedM
            : ((lanes * 0.5f) - 0.5f) * (road.Carriage.WidthM / lanes) + config.CityGen.TracedTightestLaneRadiusM;
        while (true)
        {
            // The arm leaves on the chord to the first place outside the disc, so a road's first straight is
            // its arm's own line (GEN-46, ConnectionPoints.ArmOf) and nothing it was surveyed through inside
            // the disc is a corner the lead would have to turn.
            while (through.Count > 0 && Vector2.Distance(through[0], fromM) <= fromStandoffM) through.RemoveAt(0);
            while (through.Count > 0 && Vector2.Distance(through[^1], toM) <= toStandoffM) through.RemoveAt(through.Count - 1);

            var leavingTowardM = through.Count > 0 ? through[0] : toM;
            var arrivingFromM = through.Count > 0 ? through[^1] : fromM;
            if (through.Count == 0 && Vector2.Distance(fromM, toM) <= fromStandoffM + toStandoffM) return ([], []);

            var surveyedM = new Vector2[through.Count + 2];
            surveyedM[0] = fromM + (Vector2.Normalize(leavingTowardM - fromM) * fromStandoffM);
            through.CopyTo(surveyedM, 1);
            surveyedM[^1] = toM + (Vector2.Normalize(arrivingFromM - toM) * toStandoffM);

            // The road's own line is its carriageway's middle, which OSM places off the way where it says so.
            var laidM = new Vector2[surveyedM.Length];
            OsmCarriageway.OffsetInto(surveyedM, road.Carriage.CentreOffsetM, laidM);

            // <b>No lane folds back over a corner</b>: a point the legs either side have no room to round even
            // with the innermost lane at its tightest is a survey's kink and not a bend, and the straight past it
            // stands for it.
            var reachM = Reaches(laidM, roundedM);
            var tightest = TightestCorner(laidM, reachM, floorM);
            if (tightest > 0)
            {
                through.RemoveAt(tightest - 1);
                continue;
            }

            var arcs = new ArcSeg[(2 * laidM.Length) - 3];
            var count = Spline.RoundedInto(laidM, reachM, arcs);
            return ([.. arcs.AsSpan(0, count)], laidM[1..^1]);
        }
    }

    /// <summary>
    /// <b>How far back along its legs each corner of a line is rounded from</b>: as far as rounding at
    /// <paramref name="roundedM"/> takes, and on a leg too short for the corners at both its ends, the leg
    /// shared between the two in proportion to what each wanted — so a sharp corner beside a gentle one takes
    /// nearly all of it, rather than half.
    /// </summary>
    static float[] Reaches(ReadOnlySpan<Vector2> pointsM, float roundedM)
    {
        var corners = pointsM.Length - 2;
        var wantM = new float[corners];
        for (var corner = 0; corner < corners; corner++)
        {
            var want = roundedM * Spline.HalfTurnTan(pointsM, corner + 1);
            wantM[corner] = want > 0f ? want : 0f;
        }

        var reachM = new float[corners];
        for (var corner = 0; corner < corners; corner++)
        {
            var want = wantM[corner];
            if (want <= 0f) continue;

            var arrivingM = Vector2.Distance(pointsM[corner], pointsM[corner + 1]);
            var leavingM = Vector2.Distance(pointsM[corner + 1], pointsM[corner + 2]);
            var before = corner > 0 ? wantM[corner - 1] : 0f;
            var after = corner + 1 < corners ? wantM[corner + 1] : 0f;
            reachM[corner] = MathF.Min(want, MathF.Min(arrivingM * want / (want + before), leavingM * want / (want + after)));
        }

        return reachM;
    }

    /// <summary>
    /// The corner of a line whose reach (<see cref="Reaches"/>) rounds it tightest below <paramref name="floorM"/>,
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

    static Laid Arrays(List<Road> roads, ArcSeg[][] lines, Vector2[][] through, List<Vector2> centreM, float[] standoffM)
    {
        var renumbered = new int[centreM.Count];
        Array.Fill(renumbered, CityPlan.NoRecord);
        var keptM = new List<Vector2>();
        var radiusM = new List<float>();

        var fromJunction = new List<int>();
        var toJunction = new List<int>();
        var widthM = new List<float>();
        var flow = new List<RoadFlow>();
        var lanes = new List<RoadLanes>();
        var shared = new List<bool>();
        var segmentOffsets = new List<int> { 0 };
        var segments = new List<ArcSeg>();
        var throughOffsets = new List<int> { 0 };
        var throughM = new List<Vector2>();

        for (var road = 0; road < roads.Count; road++)
        {
            if (roads[road].Gone) continue;

            fromJunction.Add(Kept(roads[road].From));
            toJunction.Add(Kept(roads[road].To));
            widthM.Add(roads[road].Carriage.WidthM);
            flow.Add(roads[road].Carriage.Lanes.Flow);
            lanes.Add(roads[road].Carriage.Lanes);
            shared.Add(roads[road].Carriage.Shared);
            segments.AddRange(lines[road]);
            segmentOffsets.Add(segments.Count);
            throughM.AddRange(through[road]);
            throughOffsets.Add(throughM.Count);
        }

        var junctions = new CityPlan.JunctionArrays
        {
            CentreM = [.. keptM],
            RadiusM = [.. radiusM],
            Lit = new bool[keptM.Count],
            PhaseOffsetS = new float[keptM.Count],
        };

        var laid = new CityPlan.RoadArrays
        {
            FromJunction = [.. fromJunction], ToJunction = [.. toJunction], WidthM = [.. widthM], Flow = [.. flow],
            Lanes = [.. lanes], SharedLane = [.. shared], SegmentOffsets = [.. segmentOffsets], Segments = [.. segments],
            ThroughOffsets = [.. throughOffsets], ThroughM = [.. throughM],
            LaidStraight = Filled(widthM.Count, true),
        };

        return new Laid(junctions, laid);

        int Kept(int junction)
        {
            if (renumbered[junction] != CityPlan.NoRecord) return renumbered[junction];

            renumbered[junction] = keptM.Count;
            keptM.Add(centreM[junction]);
            radiusM.Add(standoffM[junction]);
            return renumbered[junction];
        }
    }

    static T[] Filled<T>(int count, T value)
    {
        var filled = new T[count];
        Array.Fill(filled, value);
        return filled;
    }

    /// <summary>How much a way matters by its class, for which class a road walked through several ways is read as.</summary>
    static int Rank(string highway) => highway switch
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
