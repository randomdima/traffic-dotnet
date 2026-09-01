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
    /// Every bay in every car park, drawn. A bay is the parking space at its own size and heading, and
    /// what is drawn is three strokes round it: its two sides and its head, never its mouth.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two bays side by side share the line between them, and it is painted once.</b> Paint is the
    /// tarmac drawn brighter through a multiplying tint, so a line laid twice is visibly brighter than
    /// its neighbours — which is what a lot drawn bay by bay would show down every interior line.
    /// </para>
    /// <para>
    /// <b>The three strokes are laid end to end and not each to the bay's own size.</b> Three strokes at
    /// the bay's size each stop on the line the next is <em>centred</em> on, which leaves half a stroke
    /// painted twice at the corner and half of it not painted at all — a notch and a bright square, both
    /// of them a half line wide and both visible at close range.
    /// </para>
    /// <para>
    /// <b>A stroke within a line's width of the lot's own edge is laid against it, inside it</b> — its
    /// outer face on the edge — which is the same tolerance a frontage is measured by
    /// (<see cref="LotFrontage.FrontsTheKerb"/>). The bays fill their lot, so every outermost line of a
    /// row is on that edge: centred on it, half of each would hang over the kerb line the lot's edge is
    /// against and the other half would leave the line short of it. A stroke further in than a line's
    /// width is one between two bays and stays centred on the boundary the two share.
    /// </para>
    /// <para>
    /// <b>At the mouth of a lot that fronts the kerb, the edge the strokes end on is the road's and not
    /// the lot's</b> (<see cref="RoadFrontages.AtTheKerb"/>). The two are a chord and its curve, so ending
    /// on the rectangle leaves the row of mouths standing a sag short of the kerb line it is meant to
    /// meet. Each stroke is asked for its own end, because two lines a bay's width apart cross a curve at
    /// two different points.
    /// </para>
    /// </remarks>
    void BayStrokes(CityPlan plan, SimConfig config, Vector3 tint, float[] periods, RoadFrontages frontages)
    {
        var lots = plan.ParkingLots;
        var strokeM = config.Road.PaintLineWidthM;
        if (strokeM <= 0f || lots.SpaceCount == 0) return;

        var halfStrokeM = strokeM * 0.5f;
        var halfLengthM = config.ParkingSpaceLengthM * 0.5f;
        var halfWidthM = config.ParkingSpaceWidthM * 0.5f;
        var laid = new HashSet<(int X, int Y)>();

        // The frontage of every lot whose paint runs up to a kerb line, by lot: the frontages come out
        // grouped by road, and what is asked here is one lot at a time.
        var kerbOf = new LotFrontage?[lots.Count];
        foreach (var front in frontages.All)
        {
            if (front.FrontsTheKerb) kerbOf[front.Lot] = front;
        }

        for (var lot = 0; lot < lots.Count; lot++)
        {
            var lotCentreM = lots.CentreM[lot];
            var lotAxis = Vector2.Normalize(lots.Axis[lot]);
            var lotHalfM = lots.HalfExtentM[lot];
            var kerb = kerbOf[lot];

            for (var space = lots.SpaceOffsets[lot]; space < lots.SpaceOffsets[lot + 1]; space++)
            {
                var centreM = lots.SpacePositionM[space];
                var headingRad = lots.SpaceHeadingRad[space];
                var along = new Vector2(MathF.Cos(headingRad), MathF.Sin(headingRad));
                var across = new Vector2(-along.Y, along.X);

                var headOutM = Reach(centreM + along * halfLengthM, along, halfStrokeM);
                var headM = centreM + along * (halfLengthM + headOutM);

                // The sides own both corners: each runs from the mouth to the head stroke's far face, and
                // the head runs between their near faces.
                var leftOutM = Reach(centreM - across * halfWidthM, -across, halfStrokeM);
                var rightOutM = Reach(centreM + across * halfWidthM, across, halfStrokeM);
                var leftM = -(halfWidthM + leftOutM - halfStrokeM);
                var rightM = halfWidthM + rightOutM - halfStrokeM;
                Stroke(Mouth(centreM + across * leftM, along), headM + across * leftM);
                Stroke(Mouth(centreM + across * rightM, along), headM + across * rightM);

                var headCentreM = headM - along * halfStrokeM;
                Stroke(headCentreM + across * (leftM + halfStrokeM), headCentreM + across * (rightM - halfStrokeM));
            }

            // The mouth end of one side stroke, on that stroke's own line. A bay's heading points into it,
            // so the end with no stroke across it is the one behind its centre — a row of bays laid side by
            // side would otherwise run their mouths into one unbroken line between the lot and the road
            // that every car entering the lot drives across, which is the line the kerb's own is broken for.
            Vector2 Mouth(Vector2 lineM, Vector2 along)
            {
                var mouthM = lineM - along * halfLengthM;
                var endM = mouthM - along * Reach(mouthM, -along, 0f);
                if (kerb is null) return endM;

                // A chord's sag past the kerb's own line, which is the reach the strip that breaks the kerb
                // line takes and is taken here for the same reason: the line is drawn as chords of its arc,
                // and a stroke ended on the arc itself leaves a hair of tarmac showing wherever the chord
                // fell inside it. The ground a sag either way is the same tarmac, and paint over paint is
                // paint — the ground carries no blending and every texture is anchored to the world.
                // Within half a bay's length of where the stroke ends, and no further: a stroke behind
                // another row of bays stands a whole bay from the kerb, so it is never the one dragged out
                // to it, and the ground between a mouth and the kerb it fronts is never anything else.
                var reachM = RoadFrontages.ReachToTheKerbM(plan, kerb.Value, endM, -along, halfLengthM);
                return reachM is null ? endM : endM - along * (reachM.Value + ChordSagM);
            }

            // How much further the lot reaches past one end of a bay, where that is near enough to be the
            // end the bay was meant to stand at, and the fallback otherwise.
            float Reach(Vector2 fromM, Vector2 towards, float fallbackM)
            {
                var offsetM = fromM - lotCentreM;
                var localM = new Vector2(
                    Vector2.Dot(offsetM, lotAxis), (offsetM.Y * lotAxis.X) - (offsetM.X * lotAxis.Y));
                var stepM = new Vector2(
                    Vector2.Dot(towards, lotAxis), (towards.Y * lotAxis.X) - (towards.X * lotAxis.Y));

                var reachM = float.PositiveInfinity;
                for (var axis = 0; axis < 2; axis++)
                {
                    var step = axis == 0 ? stepM.X : stepM.Y;
                    if (MathF.Abs(step) < 1e-4f) continue;

                    var half = axis == 0 ? lotHalfM.X : lotHalfM.Y;
                    var local = axis == 0 ? localM.X : localM.Y;
                    reachM = MathF.Min(reachM, ((step > 0f ? half : -half) - local) / step);
                }

                return MathF.Abs(reachM) <= strokeM ? reachM : fallbackM;
            }
        }

        // One stroke of the outline, from end to end of its own centreline.
        void Stroke(Vector2 fromM, Vector2 toM)
        {
            var run = toM - fromM;
            var lengthM = run.Length();
            if (lengthM <= 0f) return;

            var atM = (fromM + toM) * 0.5f;

            // Half-metre cells, and the neighbours checked as well as the cell itself: two bays laid off
            // the same line agree to a rounding rather than to the bit, and the nearest two strokes that
            // are genuinely different are a bay apart.
            var cell = ((int)MathF.Round(atM.X * 2f), (int)MathF.Round(atM.Y * 2f));
            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    if (laid.Contains((cell.Item1 + x, cell.Item2 + y))) return;
                }
            }

            laid.Add(cell);
            OrientedRect(atM, run / lengthM, new Vector2(lengthM * 0.5f, halfStrokeM), Surface.Tarmac, tint, periods);
        }
    }
}
