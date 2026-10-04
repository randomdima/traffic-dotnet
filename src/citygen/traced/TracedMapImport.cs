using System.Numerics;

namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// <b>What else is known of a surveyed place, by OSM's own ids</b>: each road's width as measured, each junction's
/// control, every pedestrian crossing, every building's footprint and every tree, as the scanner reads them off the enrichment's layers into the extract's frame — imported with the extract
/// (<see cref="TracedMapImport.Of"/>). Facts and no rule: what a traced town takes of them is <see cref="Survey.Of"/>'s.
/// </summary>
internal sealed class PlaceFacts
{
    /// <summary>Each road way's measured width, by its OSM id; a way with none measured is not here.</summary>
    public required WidthArrays Widths { get; init; }

    /// <summary>Each junction's control, by its node's OSM id; a junction the rules decide is not here.</summary>
    public required ControlArrays Controls { get; init; }

    public required CrossingArrays Crossings { get; init; }

    public required TracedMap.FootprintArrays Footprints { get; init; }

    public required Vector2[] TreeM { get; init; }

    /// <summary>Nothing known but OSM's ways and sea.</summary>
    public static PlaceFacts None => new()
    {
        Widths = new WidthArrays { Way = [], WidthM = [], From = [] },
        Controls = new ControlArrays { Node = [], Control = [], Cluster = [] },
        Crossings = new CrossingArrays { Way = [], AtM = [], Kind = [], Painted = [], Junction = [] },
        Footprints = TracedMap.FootprintArrays.None,
        TreeM = [],
    };

    internal sealed class WidthArrays
    {
        public required long[] Way { get; init; }
        public required float[] WidthM { get; init; }
        public required MeasuredFrom[] From { get; init; }
        public int Count => Way.Length;
    }

    internal sealed class ControlArrays
    {
        public required long[] Node { get; init; }
        public required SurveyControl[] Control { get; init; }

        /// <summary>The near junctions it is controlled with as one, named by one of their nodes' OSM ids; nought where it stands alone.</summary>
        public required long[] Cluster { get; init; }

        public int Count => Node.Length;
    }

    internal sealed class CrossingArrays
    {
        /// <summary>The road way it crosses, by its OSM id.</summary>
        public required long[] Way { get; init; }
        public required Vector2[] AtM { get; init; }
        public required SurveyCrossingKind[] Kind { get; init; }

        /// <summary>Whether its tags say it is painted on the road, or null where they say nothing of it.</summary>
        public required bool?[] Painted { get; init; }

        /// <summary>The junction whose arm it is on, by its node's OSM id, or nought where it is struck mid-block.</summary>
        public required long[] Junction { get; init; }

        public int Count => Way.Length;
    }
}

/// <summary>
/// <b>A traced map made off the scanner's extract and what else is known of the place</b> (GEN-57,
/// <c>qq osm --import</c>): every road way OSM gives lanes, with its carriageway, class and level as its tags say and
/// its width as measured; the coastline; the turns; and the facts — each node a point in the extract's own frame,
/// numbered as the roads first pass them, and each node or way a fact names resolved to its point or kept by its id.
/// </summary>
/// <remarks>
/// <b>What the map does not need is left behind</b>: every tag but what is read here, every node no road or coast
/// passes, the surfaces outlining roads, the relations themselves. A fact naming a node no road passes is not
/// imported, nor a turn at one.
/// </remarks>
internal static class TracedMapImport
{
    public static TracedMap Of(OsmExtract extract, PlaceFacts facts)
    {
        var frame = extract.Frame;
        var projection = frame.Projection();
        var nodes = extract.Nodes;
        var pointOf = new int[nodes.Id.Length];
        Array.Fill(pointOf, -1);
        var pointM = new List<Vector2D>(nodes.Id.Length);

        var measured = new Dictionary<long, (float WidthM, MeasuredFrom From)>(facts.Widths.Count);
        for (var at = 0; at < facts.Widths.Count; at++) measured[facts.Widths.Way[at]] = (facts.Widths.WidthM[at], facts.Widths.From[at]);

        var roads = new List<TracedRoad>();
        foreach (var way in extract.Ways)
        {
            if (way.Carriageway is { } carriageway) roads.Add(Road(way, carriageway, measured.TryGetValue(way.Id, out var width) ? width : null, Placed(way.Nodes)));
        }

        var coast = new List<int[]>();
        foreach (var way in extract.Ways)
        {
            if (way.Tag("natural") == "coastline") coast.Add(Placed(way.Nodes));
        }

        var nodeOf = new Dictionary<long, int>(nodes.Id.Length);
        for (var node = 0; node < nodes.Id.Length; node++) nodeOf[nodes.Id[node]] = node;

        var controls = Enumerable.Range(0, facts.Controls.Count).Where(at => PointOf(facts.Controls.Node[at]) >= 0).ToArray();
        var turns = extract.Turns;
        return new TracedMap
        {
            Name = extract.Name,
            Description = extract.Description,
            Licence = extract.Source.Licence,
            OsmBase = extract.Source.OsmBase,
            Relation = extract.Source.Relation,
            Frame = frame,
            PointM = [.. pointM],
            Roads = [.. roads],
            Coast = [.. coast],
            Turns = new OsmTurns
            {
                Restrictions =
                [
                    .. turns.Restrictions.Where(turn => pointOf[turn.Via] >= 0).Select(turn => new OsmTurnRestriction
                    {
                        Relation = turn.Relation, From = turn.From, Via = pointOf[turn.Via], To = turn.To, Only = turn.Only,
                    }),
                ],
                LaneLinks =
                [
                    .. turns.LaneLinks.Where(link => pointOf[link.Via] >= 0).Select(link => new OsmLaneLink
                    {
                        Relation = link.Relation, From = link.From, Via = pointOf[link.Via], To = link.To, FromLane = link.FromLane, ToLane = link.ToLane,
                    }),
                ],
            },
            Controls = new TracedMap.ControlArrays
            {
                Point = [.. controls.Select(at => PointOf(facts.Controls.Node[at]))],
                Control = [.. controls.Select(at => facts.Controls.Control[at])],
                Cluster = [.. controls.Select(at => facts.Controls.Cluster[at])],
            },
            Crossings = new TracedMap.CrossingArrays
            {
                Way = facts.Crossings.Way,
                AtM = facts.Crossings.AtM,
                Kind = facts.Crossings.Kind,
                Painted = facts.Crossings.Painted,
                Junction = [.. facts.Crossings.Junction.Select(node => PointOf(node) is var point and >= 0 ? point : TracedMap.NoJunction)],
            },
            Footprints = facts.Footprints,
            TreeM = facts.TreeM,
        };

        int[] Placed(int[] wayNodes)
        {
            var points = new int[wayNodes.Length];
            for (var at = 0; at < points.Length; at++)
            {
                ref var point = ref pointOf[wayNodes[at]];
                if (point < 0)
                {
                    point = pointM.Count;
                    var (x, y) = frame.Place(projection, nodes, wayNodes[at]);
                    pointM.Add(new Vector2D(x, y));
                }

                points[at] = point;
            }

            return points;
        }

        int PointOf(long node) => nodeOf.TryGetValue(node, out var index) ? pointOf[index] : -1;
    }

    static TracedRoad Road(OsmWay way, OsmCarriageway carriageway, (float WidthM, MeasuredFrom From)? measured, int[] points)
    {
        var lanes = new OsmLane[carriageway.Lanes.Length];
        var marked = false;
        for (var lane = 0; lane < lanes.Length; lane++)
        {
            var tagged = carriageway.Lanes[lane];
            marked |= tagged.Turn is not null || tagged.Change is not null || tagged.Psv is not null;
            lanes[lane] = new OsmLane { Way = tagged.Way, WidthM = tagged.WidthM, Arrows = tagged.Arrows };
        }

        return new TracedRoad
        {
            OsmId = way.Id,
            Highway = way.Tags["highway"],
            Bridge = (way.Tag("bridge") ?? "no") != "no",
            Tunnel = (way.Tag("tunnel") ?? "no") != "no",
            Roundabout = way.Tag("junction") is "roundabout" or "circular",
            Marked = marked,
            Carriageway = new OsmCarriageway
            {
                Lanes = lanes, CentreOffsetM = carriageway.CentreOffsetM, LanesFrom = carriageway.LanesFrom, WidthFrom = carriageway.WidthFrom,
            },
            Measured = measured,
            Points = points,
        };
    }
}
