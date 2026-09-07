using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.App.Render;

/// <summary>Everything painted on the carriageway rather than made of it: the lane dashes, the zebras and the bay strokes.</summary>
internal sealed partial class GroundMesh
{
    /// <summary>
    /// The dashed lane centreline, down the middle of every carriageway and <b>stopping at the stop bar</b>
    /// rather than running on into the junction behind it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every run of marks is centred on the stretch it is on</b>, so a street never begins with half a
    /// dash at one end. Which stretches those are is <see cref="CentrelineRuns"/>: the road owns where its
    /// own paint breaks, and this lays dashes along what it is handed.
    /// </para>
    /// <para>
    /// <b>A dash is laid on the road's own curve</b> and not on the chord of it
    /// (<see cref="CurvedMark"/>).
    /// </para>
    /// </remarks>
    void LaneDashes(CityPlan plan, SimConfig config, Vector3 tint, float[] periods)
    {
        var dashM = config.Road.LaneDashLengthM;
        var pitchM = dashM + config.Road.LaneDashGapM;
        var halfWidthM = config.Road.PaintLineWidthM * 0.5f;
        if (dashM <= 0f || pitchM <= dashM) return;

        var runs = CentrelineRuns.Lay(plan, config);
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var arcs = plan.Roads.SegmentsOf(road);
            foreach (var run in runs.On(road))
            {
                DashRun(arcs, run.FromM, run.ToM, dashM, pitchM, halfWidthM, tint, periods);
            }
        }
    }

    void DashRun(
        ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, float dashM, float pitchM, float halfWidthM,
        Vector3 tint, float[] periods)
    {
        var runM = toM - fromM;
        if (runM < dashM) return;

        var dashes = Math.Max(1, (int)MathF.Round((runM + pitchM - dashM) / pitchM));
        var laidM = (dashes * dashM) + ((dashes - 1) * (pitchM - dashM));
        while (laidM > runM && dashes > 1)
        {
            dashes--;
            laidM = (dashes * dashM) + ((dashes - 1) * (pitchM - dashM));
        }

        var startM = fromM + ((runM - laidM) * 0.5f);
        for (var dash = 0; dash < dashes; dash++)
        {
            var atM = startM + (dash * pitchM);
            CurvedMark(arcs, atM, atM + dashM, halfWidthM, Surface.Tarmac, tint, periods);
        }
    }

    /// <summary>
    /// One crossing's bars: laid <b>along the traffic's own direction</b> — the crossing's axis — and
    /// repeated across the carriageway, centred on the span so a zebra never starts with half a bar.
    /// </summary>
    void Zebra(
        Vector2 centreM, Vector2 axis, float depthM, float spanM, SimConfig config, Vector3 tint, float[] periods)
    {
        var stripeM = config.Road.ZebraStripeWidthM;
        var pitchM = config.Road.ZebraStripePitchM;
        if (stripeM <= 0f || pitchM < stripeM || spanM <= 0f || depthM <= 0f) return;

        var along = axis.LengthSquared() > 0f ? Vector2.Normalize(axis) : Vector2.UnitX;
        var across = new Vector2(-along.Y, along.X);

        var stripes = Math.Max(1, (int)MathF.Round((spanM + pitchM - stripeM) / pitchM));
        var laidM = (stripes * stripeM) + ((stripes - 1) * (pitchM - stripeM));
        while (laidM > spanM && stripes > 1)
        {
            stripes--;
            laidM = (stripes * stripeM) + ((stripes - 1) * (pitchM - stripeM));
        }

        var firstM = -laidM * 0.5f + (stripeM * 0.5f);
        var halfM = new Vector2(depthM * 0.5f, stripeM * 0.5f);
        for (var stripe = 0; stripe < stripes; stripe++)
        {
            OrientedRect(centreM + across * (firstM + (stripe * pitchM)), along, halfM, Surface.Tarmac, tint, periods);
        }
    }

    /// <summary>
    /// <b>The line between one bay and the next, and nothing else</b> (GEN-4m). A bay is the parking space
    /// at its own size and heading; each offers the three lines it would be drawn inside — its two sides and
    /// its head, never its mouth — and a line is painted only where two bays offer the same one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A car park is not a case, and the outside of one is nobody's to paint.</b> Every edge of a lot is
    /// a kerb, because a lot is a piece of the town's tarmac like any other and the walk wraps it like any
    /// other (TER-3c.3): the line round the outside of a row of bays is the kerb line the pavement carries
    /// there, and a stroke laid against it is that line painted twice. What is left for a bay to say is the
    /// boundary it shares with its neighbour — which the town's own geometry does not say anywhere.
    /// </para>
    /// <para>
    /// <b>Offered twice is what "between two bays" means</b>, and it is asked of the bays rather than of the
    /// lot they stand in: two rows head to head share their heads, two bays side by side share a side, and a
    /// row's outermost side is offered once. Nothing here reads the lot's rectangle, its frontage, or the
    /// road it opens off. Half-metre cells with their neighbours checked, because two bays laid off one line
    /// agree to a rounding and the nearest two lines that are genuinely different are a bay apart.
    /// </para>
    /// </remarks>
    void BayStrokes(CityPlan plan, SimConfig config, Vector3 tint, float[] periods)
    {
        var lots = plan.ParkingLots;
        var strokeM = config.Road.PaintLineWidthM;
        if (strokeM <= 0f || lots.SpaceCount == 0) return;

        var halfStrokeM = strokeM * 0.5f;
        var halfLengthM = config.ParkingSpaceLengthM * 0.5f;
        var halfWidthM = config.ParkingSpaceWidthM * 0.5f;
        var offered = new Dictionary<(int X, int Y), (Vector2 FromM, Vector2 ToM, int Times)>();

        for (var space = 0; space < lots.SpaceCount; space++)
        {
            var centreM = lots.SpacePositionM[space];
            var headingRad = lots.SpaceHeadingRad[space];
            var along = new Vector2(MathF.Cos(headingRad), MathF.Sin(headingRad));
            var across = new Vector2(-along.Y, along.X) * halfWidthM;
            var mouthM = centreM - (along * halfLengthM);
            var headM = centreM + (along * halfLengthM);

            Offer(mouthM - across, headM - across);
            Offer(mouthM + across, headM + across);
            Offer(headM - across, headM + across);
        }

        foreach (var (fromM, toM, times) in offered.Values)
        {
            if (times < 2) continue;

            var run = toM - fromM;
            OrientedRect(
                (fromM + toM) * 0.5f, run / run.Length(), new Vector2(run.Length() * 0.5f, halfStrokeM),
                Surface.Tarmac, tint, periods);
        }

        void Offer(Vector2 fromM, Vector2 toM)
        {
            var atM = (fromM + toM) * 0.5f;
            var cell = ((int)MathF.Round(atM.X * 2f), (int)MathF.Round(atM.Y * 2f));
            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    var near = (cell.Item1 + x, cell.Item2 + y);
                    if (!offered.TryGetValue(near, out var line)) continue;

                    offered[near] = (line.FromM, line.ToM, line.Times + 1);
                    return;
                }
            }

            offered[cell] = (fromM, toM, 1);
        }
    }
}
