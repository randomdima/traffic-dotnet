using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Statics;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>Where a town's zebras are painted over each other</b> (WLK-10b): every pair of painted stations whose bands
/// reach into each other, where it stands, what the two are across, and whether their stripes cross or run side by
/// side as one band.
/// </summary>
/// <remarks>
/// <b>Quoted and gating nothing</b>: what is listed is what a reader frames pictures from. A pair whose stripes cross
/// is one WLK-10b strikes, so the count of those is how far the town is from it.
/// </remarks>
internal static class ZebraShape
{
    const int MostListed = 40;

    public static void Run(string map, SimConfig config)
    {
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        var crossed = plan.Paving(config).RoadEnds(config).CrossedM;
        var arms = plan.Ground.ArmsPerJunction();
        var figures = config.Road;

        var rows = new List<string>();
        var (across, sideBySide) = (0, 0);
        for (var one = 0; one < crossed.Length; one++)
        {
            for (var other = one + 1; other < crossed.Length; other++)
            {
                var (a, b) = (crossed[one], crossed[other]);
                if (Vector2.Distance(a.NearM + a.FarM, b.NearM + b.FarM) * 0.5f >
                    ((a.SpanM + b.SpanM) * 0.5f) + figures.CrossingDepthM)
                {
                    continue;
                }

                if (!a.PaintedOver(b, figures.CrossingDepthM, figures.ZebraStripeWidthM)) continue;

                var crosses = !a.OneBand(b, figures.ZebrasOneBandWithinDeg);
                if (crosses) across++;
                else sideBySide++;

                var atM = (a.NearM + a.FarM + b.NearM + b.FarM) * 0.25f;
                rows.Add($"  {atM.X,7:F0}{atM.Y,8:F0}  {(crosses ? "across" : "beside"),-8}{Of(a)}   {Of(b)}");
            }
        }

        Bays(plan, crossed, figures);
        Console.WriteLine($"{plan.Name}  {across} pairs of zebras painted across each other and {sideBySide} side by side, " +
                          $"of {crossed.Length} painted");
        if (rows.Count == 0) return;

        Console.WriteLine();
        Console.WriteLine($"  {"x",7}{"y",8}  {"stripes",-8}road length lanes from(arms)>to(arms) cut, each");
        var every = Math.Max(1, rows.Count / MostListed);
        for (var row = 0; row < rows.Count; row += every) Console.WriteLine(rows[row]);

        static void Bays(CityPlan plan, ReadOnlySpan<KerbNodes> crossed, RoadFigures figures)
        {
            var parks = plan.CarParks;
            var onZebras = new List<string>();
            for (var bay = 0; bay < parks.Road.Length; bay++)
            {
                var road = parks.Road[bay];
                var line = plan.Roads.SegmentsOf(road);
                var bayM = new KerbNodes(road, KerbCut.AtTheNearEnd, line[0].StartM, line[^1].EndM, true);
                foreach (var zebra in crossed)
                {
                    if (Vector2.Distance(zebra.NearM + zebra.FarM, bayM.NearM + bayM.FarM) * 0.5f > zebra.SpanM + bayM.SpanM) continue;
                    if (!zebra.PaintedOver(bayM, figures.CrossingDepthM + plan.Roads.WidthM[road], figures.ZebraStripeWidthM)) continue;

                    onZebras.Add($"  {bayM.NearM.X,7:F0}{bayM.NearM.Y,8:F0}  bay {road} on the zebra across road {zebra.Road}");
                    break;
                }
            }

            Console.WriteLine($"{plan.Name}  {onZebras.Count} of {parks.Road.Length} car park bays stand on a zebra");
            foreach (var row in onZebras.Take(MostListed)) Console.WriteLine(row);
        }

        string Of(in KerbNodes nodes)
        {
            var road = nodes.Road;
            var (from, to) = (plan.Roads.FromJunction[road], plan.Roads.ToJunction[road]);
            var lengthM = 0f;
            foreach (var piece in plan.Roads.SegmentsOf(road)) lengthM += piece.LengthM;
            return $"{road,6} {lengthM,6:F1}m {plan.Roads.LanesWithTheRoad(road)}+{plan.Roads.LanesAgainstTheRoad(road)} " +
                   $"{from}({arms[from]})>{to}({arms[to]}) {nodes.Cut}";
        }
    }
}
