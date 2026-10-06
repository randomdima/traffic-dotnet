using System.Numerics;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Zones;

namespace TrafficSimulation.CityGen.Traced;

/// <summary>Where a road's measured width was read.</summary>
internal enum MeasuredFrom : byte
{
    /// <summary>Its <c>width</c> tag, the mapper's own word.</summary>
    Tag,

    /// <summary>The carriageway surface OSM outlines it with (<c>area:highway</c>).</summary>
    Surface,

    /// <summary>The paved surface a model read off aerial imagery, kerb to kerb, parking and gutters in it.</summary>
    Imagery,
}

/// <summary>
/// <b>What else is known of a surveyed place's roads</b>: each road's width as measured, by its OSM id, as the scanner
/// reads it off the enrichment's layers — imported with the extract (<see cref="TracedMapImport.Of"/>).
/// </summary>
internal sealed class PlaceFacts
{
    /// <summary>Each road way's measured width, by its OSM id; a way with none measured is not here.</summary>
    public required WidthArrays Widths { get; init; }

    /// <summary>Nothing known but OSM's ways and sea.</summary>
    public static PlaceFacts None => new() { Widths = new WidthArrays { Way = [], WidthM = [], From = [] } };

    internal sealed class WidthArrays
    {
        public required long[] Way { get; init; }
        public required float[] WidthM { get; init; }
        public required MeasuredFrom[] From { get; init; }
        public int Count => Way.Length;
    }
}

/// <summary>
/// <b>A traced map made off the scanner's extract and what else is known of the place</b> (GEN-57,
/// <c>qq osm --import</c>): every road way OSM gives lanes, with its class, level and lanes as its tags say and its
/// width as measured, and the coastline — each node a point in the extract's own frame, numbered as the roads first
/// pass them. Its zones are laid on it after, off its own streets (<c>ZoneHints</c>).
/// </summary>
/// <remarks>
/// <b>What the map does not need is left behind</b>: every tag but what is read here, every node no road or coast
/// passes, the surfaces outlining roads, the relations, each lane's own width and arrows, and a width that is no
/// measure to lay a road by (<see cref="Road"/>).
/// </remarks>
internal static class TracedMapImport
{
    public static TownMap Of(OsmExtract extract, PlaceFacts facts)
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
            if (way.Carriageway is not { } carriageway) continue;

            roads.Add(Road(
                way.Id, way.Tags["highway"], (way.Tag("bridge") ?? "no") != "no", way.Tag("junction") is "roundabout" or "circular", carriageway,
                Marked(carriageway), measured.TryGetValue(way.Id, out var width) ? width : null, Placed(way.Nodes)));
        }

        var coast = new List<int[]>();
        foreach (var way in extract.Ways)
        {
            if (way.Tag("natural") == "coastline") coast.Add(Placed(way.Nodes));
        }

        return new TownMap
        {
            Name = extract.Name,
            Description = extract.Description,
            Licence = extract.Source.Licence,
            OsmBase = extract.Source.OsmBase,
            Seed = (ulong)extract.Source.Relation,
            Frame = frame,
            PointM = [.. pointM],
            Roads = [.. roads],
            Coast = [.. coast],

            // Nothing zoned but the whole map, which the zones laid off its streets replace (<c>ZoneHints</c>).
            Zones = TownMap.ZoneArrays.Whole(new Vector2((float)frame.WidthM, (float)frame.HeightM), ZoneKind.Town, []),
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
    }

    /// <summary>
    /// <b>One road way as the map holds it</b>: OSM's lane counts, where its carriageway's middle stands as a share of
    /// its width, and its measured width where it is one to lay the road by — measured on the way itself (its tag, or the
    /// surface OSM outlines it with) or read off imagery under a street, and on a way OSM gives no width lane by lane nor
    /// places off its middle. Imagery is not taken under a service road, a track or a link: a yard's paving or a slip
    /// road's merge reads as their width.
    /// </summary>
    /// <param name="marked">Whether any lane has a <c>turn</c>, <c>change</c> or bus <c>:lanes</c> entry.</param>
    public static TracedRoad Road(
        long id, string highway, bool bridge, bool roundabout, OsmCarriageway carriageway, bool marked, (float WidthM, MeasuredFrom From)? measured,
        int[] points)
    {
        var laysBy = measured is { } read && carriageway.WidthFrom != OsmWidthFrom.Lanes && carriageway.CentreOffsetM == 0f
                     && (read.From != MeasuredFrom.Imagery || TracedRoad.Rank(highway) > 0);
        return new TracedRoad
        {
            OsmId = id,
            Highway = highway,
            Bridge = bridge,
            Roundabout = roundabout,
            LanesForward = carriageway.Count(OsmLaneWay.Forward),
            LanesBackward = carriageway.Count(OsmLaneWay.Backward),
            LanesShared = carriageway.Count(OsmLaneWay.Both),
            LanesTagged = carriageway.LanesFrom == OsmLanesFrom.Tagged,
            Marked = marked,
            CentreOffsetShare = carriageway.WidthM > 0f ? carriageway.CentreOffsetM / carriageway.WidthM : 0f,
            WidthM = laysBy ? measured!.Value.WidthM : null,
            Points = points,
        };
    }

    static bool Marked(OsmCarriageway carriageway)
    {
        foreach (var lane in carriageway.Lanes)
        {
            if (lane.Turn is not null || lane.Change is not null || lane.Psv is not null) return true;
        }

        return false;
    }
}
