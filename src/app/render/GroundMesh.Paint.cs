using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.App.Render;

/// <summary>Everything painted on the carriageway rather than made of it: the lane dashes, the zebras and the bay strokes.</summary>
internal sealed partial class GroundMesh
{
    void LaneDashes(CityPlan plan, SimConfig config, Vector3 tint, float[] periods)
    {
        var dashM = config.Road.LaneDashLengthM;
        var pitchM = dashM + config.Road.LaneDashGapM;
        var halfM = new Vector2(dashM * 0.5f, config.Road.PaintLineWidthM * 0.5f);
        if (dashM <= 0f || pitchM <= dashM) return;

        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var arcs = plan.Roads.SegmentsOf(road);
            var lengthM = 0f;
            foreach (var arc in arcs) lengthM += arc.LengthM;
            if (lengthM <= 0f) continue;

            var openedM = 0f;
            var open = false;
            for (var stepM = 0f; stepM <= lengthM + JunctionStepM; stepM += JunctionStepM)
            {
                var atM = MathF.Min(stepM, lengthM);
                var clear = !BreaksTheDashes(plan, Along(arcs, atM).PointM);
                if (clear && !open) (openedM, open) = (atM, true);
                else if (!clear && open) { DashRun(arcs, openedM, atM, dashM, pitchM, halfM, tint, periods); open = false; }
            }

            if (open) DashRun(arcs, openedM, lengthM, dashM, pitchM, halfM, tint, periods);
        }
    }

    void DashRun(
        ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, float dashM, float pitchM, Vector2 halfM,
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
            var (pointM, headingRad) = Along(arcs, startM + (dash * pitchM) + (dashM * 0.5f));
            OrientedRect(pointM, new Vector2(MathF.Cos(headingRad), MathF.Sin(headingRad)), halfM, Surface.Tarmac, tint, periods);
        }
    }

    /// <summary>
    /// Where a lane's dashes stop: inside a junction, and on a crossing. Both are the same rule read
    /// twice — <b>a dash marks the middle of a carriageway, and neither of these is one</b>; a dash laid
    /// over a zebra is the same paint drawn twice and shows as a bright streak down the bars.
    /// </summary>
    static bool BreaksTheDashes(CityPlan plan, Vector2 pointM)
    {
        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            var reachM = plan.Junctions.RadiusM[junction];
            if (Vector2.DistanceSquared(pointM, plan.Junctions.CentreM[junction]) <= reachM * reachM) return true;
        }

        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            var axis = plan.Crosswalks.Axis[crossing];
            if (axis.LengthSquared() <= 0f) continue;

            var along = Vector2.Normalize(axis);
            var offset = pointM - plan.Crosswalks.CentreM[crossing];
            var down = MathF.Abs(Vector2.Dot(offset, along));
            var across = MathF.Abs((offset.X * -along.Y) + (offset.Y * along.X));
            if (down <= plan.Crosswalks.DepthM[crossing] * 0.5f && across <= plan.Crosswalks.SpanM[crossing] * 0.5f) return true;
        }

        return false;
    }

    /// <summary>Where a distance along a chain of arcs lands, and which way the road is going there.</summary>
    static (Vector2 PointM, float HeadingRad) Along(ReadOnlySpan<ArcSeg> arcs, float distanceM)
    {
        foreach (var arc in arcs)
        {
            if (distanceM <= arc.LengthM) return (arc.PointAtM(distanceM), arc.HeadingAtRad(distanceM));

            distanceM -= arc.LengthM;
        }

        var last = arcs[^1];
        return (last.EndM, last.HeadingAtRad(last.LengthM));
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
    /// Every bay in every car park, outlined. A bay is the parking space at its own size and heading,
    /// and the outline is four strokes round it.
    /// </summary>
    /// <remarks>
    /// <b>Two bays side by side share the line between them, and it is painted once.</b> Paint is the
    /// tarmac drawn brighter through a multiplying tint, so a line laid twice is visibly brighter than
    /// its neighbours — which is what a lot drawn bay by bay would show down every interior line.
    /// </remarks>
    void BayStrokes(CityPlan plan, SimConfig config, Vector3 tint, float[] periods)
    {
        var strokeM = config.Road.PaintLineWidthM;
        if (strokeM <= 0f || plan.ParkingLots.SpaceCount == 0) return;

        var halfLengthM = config.ParkingSpaceLengthM * 0.5f;
        var halfWidthM = config.ParkingSpaceWidthM * 0.5f;
        var laid = new HashSet<(int X, int Y)>();

        for (var space = 0; space < plan.ParkingLots.SpaceCount; space++)
        {
            var centreM = plan.ParkingLots.SpacePositionM[space];
            var headingRad = plan.ParkingLots.SpaceHeadingRad[space];
            var along = new Vector2(MathF.Cos(headingRad), MathF.Sin(headingRad));
            var across = new Vector2(-along.Y, along.X);

            foreach (var side in (ReadOnlySpan<float>)[-1f, 1f])
            {
                Stroke(centreM + across * (halfWidthM * side), along, new Vector2(halfLengthM, strokeM * 0.5f));
                Stroke(centreM + along * (halfLengthM * side), along, new Vector2(strokeM * 0.5f, halfWidthM));
            }
        }

        void Stroke(Vector2 atM, Vector2 axis, Vector2 halfM)
        {
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
            OrientedRect(atM, axis, halfM, Surface.Tarmac, tint, periods);
        }
    }
}
