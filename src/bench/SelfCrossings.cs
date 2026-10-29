using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>Every lane and every movement whose own line crosses itself</b>: read at <see cref="StepM"/>, any two of its steps
/// not next to each other that cross. A line a car is driven along never crosses itself.
/// </summary>
/// <remarks>Quoted and gating nothing: the count is what a change to the lanes or the movements is weighed against.</remarks>
internal static class SelfCrossings
{
    /// <summary>How finely a line is read.</summary>
    const float StepM = 0.25f;

    const int Listed = 30;

    public static void Run(string map, SimConfig config)
    {
        var plan = Maps.Plan(map, config, ShippedArt.Sizes);
        var lanes = plan.Paving(config).Lanes;
        var points = new List<Vector2>();

        var laneRows = new List<string>();
        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            if (Crosses(lanes.ArcsOf(lane), out var atM)) laneRows.Add($"  lane {lane,6} of road {lanes.LaneRoad[lane],6}  {atM.X,7:F0}{atM.Y,8:F0}");
        }

        var movementRows = new List<string>();
        for (var connector = 0; connector < lanes.ConnectorCount; connector++)
        {
            if (Crosses(lanes.ArcsOfConnector(connector), out var atM))
            {
                movementRows.Add($"  movement {connector,6} {lanes.ConnectorKind[connector],-9}  {atM.X,7:F0}{atM.Y,8:F0}");
            }
        }

        Console.WriteLine($"{plan.Name}  {laneRows.Count} of {lanes.LaneCount} lanes and {movementRows.Count} of " +
                          $"{lanes.ConnectorCount} movements cross themselves");
        foreach (var rows in (List<string>[])[laneRows, movementRows])
        {
            var every = Math.Max(1, rows.Count / Listed);
            for (var row = 0; row < rows.Count; row += every) Console.WriteLine(rows[row]);
        }

        bool Crosses(ReadOnlySpan<ArcSeg> line, out Vector2 atM)
        {
            atM = default;
            points.Clear();
            var lengthM = Spline.TotalLengthM(line);
            for (var alongM = 0f; alongM < lengthM; alongM += StepM) points.Add(Spline.SampleAt(line, alongM).PositionM);
            points.Add(Spline.SampleAt(line, lengthM).PositionM);

            for (var one = 1; one < points.Count; one++)
            {
                for (var other = one + 2; other < points.Count; other++)
                {
                    if (!Straights(points[one - 1], points[one], points[other - 1], points[other])) continue;

                    atM = points[one];
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Whether two straights cross between their ends, touching excluded.</summary>
    static bool Straights(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
    {
        var (runA, runB) = (a1 - a0, b1 - b0);
        var across = Spline.Cross(runA, runB);
        if (MathF.Abs(across) < 1e-9f) return false;

        var apart = b0 - a0;
        var alongA = Spline.Cross(apart, runB) / across;
        var alongB = Spline.Cross(apart, runA) / across;
        return alongA is > 0f and < 1f && alongB is > 0f and < 1f;
    }
}
