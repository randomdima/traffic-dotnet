using System.Globalization;
using System.Numerics;
using System.Text;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Statics;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>A brief's town drawn as a sketch</b> (<c>--brief NAME --out FILE.svg</c>): its water and railway, each district
/// tinted and numbered, the roads between districts and to outside, and every road offered coloured by the piece of the
/// network it was joined into before the pieces off the largest were deleted — what a picture of the finished town cannot
/// show, since what was deleted is not in it.
/// </summary>
internal static class BriefSketch
{
    /// <summary>The colours the pieces that are not the largest are drawn in, in turn.</summary>
    static readonly string[] Pieces = ["#d0308a", "#7a3cd6", "#d67a00", "#00a0a0", "#808000", "#c00000"];

    public static void Write(TownGen.Authored town, SimConfig config, string path)
    {
        var sizeM = town.Map.SizeM;
        var svg = new StringBuilder();
        svg.Append(Invariant($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {sizeM.X:F0} {sizeM.Y:F0}\" width=\"{sizeM.X / 4f:F0}\" height=\"{sizeM.Y / 4f:F0}\">\n"));
        svg.Append(Invariant($"<rect width=\"{sizeM.X:F0}\" height=\"{sizeM.Y:F0}\" fill=\"#e8f0d8\"/>\n"));

        var water = town.Water.Rings.Outline;
        for (var ring = 0; ring < water.Count; ring++) Polygon(svg, water.RingOf(ring), "#8db8e0", "none");

        var partition = town.Partition;
        for (var district = 0; district < partition.Count; district++)
        {
            var hue = (district * 137) % 360;
            Polygon(svg, [.. partition.Outline(district)], $"hsla({hue},55%,70%,0.45)", "#606060");
            var at = partition.CentreM[district];
            svg.Append(Invariant($"<text x=\"{at.X:F0}\" y=\"{at.Y:F0}\" font-size=\"40\" text-anchor=\"middle\">{district} {town.Districts[district].Kind}</text>\n"));
        }

        var rails = town.Connectors.Rails;
        for (var track = 0; track < rails.Count; track++) Line(svg, rails.TrackOf(track), "#404040", 4f);

        var laid = TownGen.Lay(town, config, ShippedArt.Sizes.Buildings, static _ => { }, (layout, paths) =>
        {
            foreach (var path in paths.All)
            {
                var (colour, width) = path.Class == RoadClass.Intercity ? ("#e0202060", 14f) : ("#f0a00060", 8f + (path.Lanes * 2f));
                Line(svg, path.Line.PointsM, colour, width);
            }

            var root = Clusters.Apart(layout.NodeM.Count);
            foreach (var edge in layout.Edges) Clusters.Union(root, edge.From, edge.To);

            var size = new Dictionary<int, int>();
            foreach (var edge in layout.Edges) size[Clusters.Find(root, edge.From)] = size.GetValueOrDefault(Clusters.Find(root, edge.From)) + 1;

            var largest = size.Count == 0 ? -1 : size.MaxBy(pair => pair.Value).Key;
            var colourOf = new Dictionary<int, string>();
            for (var road = 0; road < layout.Edges.Count; road++)
            {
                var piece = Clusters.Find(root, layout.Edges[road].From);
                if (!colourOf.TryGetValue(piece, out var colour)) colourOf[piece] = colour = piece == largest ? "#202020" : Pieces[colourOf.Count % Pieces.Length];

                var line = layout.LineOf(road);
                var lengthM = Spline.TotalLengthM(line);
                var points = new List<Vector2>();
                for (var alongM = 0f; alongM < lengthM; alongM += 5f) points.Add(Spline.SampleAt(line, alongM).PositionM);
                if (lengthM > 0f) points.Add(Spline.SampleAt(line, lengthM).PositionM);
                Line(svg, [.. points], colour, 2.5f);
            }

            foreach (var nodeM in layout.NodeM) svg.Append(Invariant($"<circle cx=\"{nodeM.X:F1}\" cy=\"{nodeM.Y:F1}\" r=\"3\" fill=\"#000\"/>\n"));
        });

        MainNetworks(svg, laid.Roads.Roads);

        var coreM = town.Connectors.CoreM;
        svg.Append(Invariant($"<circle cx=\"{coreM.X:F0}\" cy=\"{coreM.Y:F0}\" r=\"25\" fill=\"none\" stroke=\"#e02020\" stroke-width=\"6\"/>\n"));
        svg.Append("</svg>\n");
        File.WriteAllText(path, svg.ToString());
    }

    /// <summary>
    /// The finished town's roads that are no district's own (GEN-69), dashed over the rest: the largest network they make in
    /// white, every other in a colour of its own.
    /// </summary>
    static void MainNetworks(StringBuilder svg, CityPlan.RoadArrays roads)
    {
        var junctions = 0;
        for (var road = 0; road < roads.Count; road++) junctions = Math.Max(junctions, Math.Max(roads.FromJunction[road], roads.ToJunction[road]) + 1);

        var root = Clusters.Apart(junctions);
        var main = new List<int>();
        for (var road = 0; road < roads.Count; road++)
        {
            if (roads.Class[road] is RoadClass.Street or RoadClass.CarPark) continue;

            main.Add(road);
            Clusters.Union(root, roads.FromJunction[road], roads.ToJunction[road]);
        }

        var largest = main.GroupBy(road => Clusters.Find(root, roads.FromJunction[road])).MaxBy(piece => piece.Count())?.Key ?? -1;
        var colourOf = new Dictionary<int, string>();
        foreach (var road in main)
        {
            var piece = Clusters.Find(root, roads.FromJunction[road]);
            if (!colourOf.TryGetValue(piece, out var colour)) colourOf[piece] = colour = piece == largest ? "#ffffff" : Pieces[colourOf.Count % Pieces.Length];

            var arcs = roads.SegmentsOf(road);
            var lengthM = Spline.TotalLengthM(arcs);
            var points = new List<Vector2>();
            for (var alongM = 0f; alongM < lengthM; alongM += 5f) points.Add(Spline.SampleAt(arcs, alongM).PositionM);
            if (lengthM > 0f) points.Add(Spline.SampleAt(arcs, lengthM).PositionM);
            svg.Append(Invariant($"<polyline fill=\"none\" stroke=\"{colour}\" stroke-width=\"6\" stroke-dasharray=\"12 8\" points=\""));
            foreach (var pointM in points) svg.Append(Invariant($"{pointM.X:F1},{pointM.Y:F1} "));
            svg.Append("\"/>\n");
        }
    }

    static void Polygon(StringBuilder svg, ReadOnlySpan<Vector2> ringM, string fill, string stroke)
    {
        svg.Append(Invariant($"<polygon fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"2\" points=\""));
        foreach (var pointM in ringM) svg.Append(Invariant($"{pointM.X:F0},{pointM.Y:F0} "));
        svg.Append("\"/>\n");
    }

    static void Line(StringBuilder svg, ReadOnlySpan<Vector2> lineM, string stroke, float widthM)
    {
        svg.Append(Invariant($"<polyline fill=\"none\" stroke=\"{stroke}\" stroke-width=\"{widthM:F1}\" stroke-linejoin=\"round\" points=\""));
        foreach (var pointM in lineM) svg.Append(Invariant($"{pointM.X:F1},{pointM.Y:F1} "));
        svg.Append("\"/>\n");
    }

    static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
