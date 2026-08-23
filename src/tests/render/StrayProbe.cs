using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace TrafficSimulation.Tests.Render;

[Trait(Tier.Key, Tier.Unit)]
public class StrayProbe(ITestOutputHelper output)
{
    [Fact]
    public void Dump()
    {
        var plan = TownReader.ReadFile(ProjectPaths.TownFile("Test"));
        var target = new Vector2(278f, 170f);
        for (var c = 0; c < plan.JunctionCorners.Count; c++)
        {
            if (Vector2.Distance(plan.JunctionCorners.CornerM[c], target) > 20f) continue;
            output.WriteLine($"corner {c} cornerM {plan.JunctionCorners.CornerM[c]} arcC {plan.JunctionCorners.ArcCentreM[c]} " +
                             $"r {plan.JunctionCorners.RadiusM[c]:F2} tA {plan.JunctionCorners.TangentAM[c]} tB {plan.JunctionCorners.TangentBM[c]}");
        }

        // Road 6's south edge, sampled along the arc, against the corner arc it should meet.
        var arcs = plan.Roads.SegmentsOf(6);
        for (var d = 0f; d < 20f; d += 2f)
        {
            var arc = arcs[0];
            var p = arc.PointAtM(d);
            var h = arc.HeadingAtRad(d);
            var across = new Vector2(-MathF.Sin(h), MathF.Cos(h));
            output.WriteLine($"road6 at {d:F0} m centre {p} south edge {p + across * 4f}");
        }
    }
}
