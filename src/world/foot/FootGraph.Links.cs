using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.World.Foot;

/// <summary>What joins the bands to everything off them: the ways into a parking lot, and the crossings cut through a carriageway.</summary>
internal sealed partial class FootGraph
{
    /// <summary>Whether any station of a line stands inside ground the plan paved over for traffic.</summary>
    static bool RunsOverAPavedArea(CityPlan plan, ArcSeg line)
    {
        var areas = plan.PavedAreas;
        if (areas.Count == 0) return false;

        ReadOnlySpan<ArcSeg> chain = new(in line);
        var steps = Math.Max(2, (int)MathF.Ceiling(line.LengthM / plan.CellSizeM));
        for (var step = 0; step <= steps; step++)
        {
            var pointM = Spline.SampleAt(chain, line.LengthM * step / steps).PositionM;
            for (var area = 0; area < areas.Count; area++)
            {
                var offM = pointM - areas.MinM[area];
                if (offM.X >= 0f && offM.Y >= 0f && offM.X <= areas.SizeM[area].X && offM.Y <= areas.SizeM[area].Y)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Where one lot severs the street's pavement, and the two far corners of the way past it.</summary>
    readonly record struct LotWay(int NearNode, int FarNode, Vector2 CornerAM, Vector2 CornerBM);

    /// <summary>
    /// The way past a parking lot. <b>No edge of this network enters a lot</b>, and
    /// a lot is stamped over the pavement it stands on — so refusing its ground and stopping there would
    /// sever the street at every lot in town. What replaces the severed stretch is <b>the band the lot is
    /// wrapped in: its own middle, three sides of it, joined to the street's pavement at both ends</b>,
    /// laid as one ordinary stretch of pavement — one edge, one curve, one lane — with its corners
    /// filleted at half a band.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Strung instead as a chain of right-angled straights it is a special case in every pass downstream,
    /// and the pieces it takes at either end are too short to carry a lane and are where the town stands
    /// still.
    /// </para>
    /// <para>
    /// <b>Which face the street is on is asked of the ground</b>, not assumed of the lot's own axis: every
    /// lot on every shipped map has exactly one face with carriageway half a walk off it, and that is the
    /// one face the band does not run down.
    /// </para>
    /// </remarks>
    static void LotBands(CityPlan plan, TerrainGrid terrain, Builder builder, float bandM)
    {
        var lots = plan.ParkingLots;
        if (lots.Count == 0) return;

        var outM = bandM * 0.5f;
        var ways = new List<LotWay>();
        var severed = new List<(Vector2 CentreM, Vector2 Axis, Vector2 HalfM)>();

        for (var lot = 0; lot < lots.Count; lot++)
        {
            var centreM = lots.CentreM[lot];
            var axis = Vector2.Normalize(lots.Axis[lot]);
            var across = new Vector2(-axis.Y, axis.X);
            var half = lots.HalfExtentM[lot];

            var toStreet = Vector2.Zero;
            var toStreetHalfM = 0f;
            var alongHalfM = 0f;
            var faces = 0;
            foreach (var (direction, halfM, otherHalfM) in (ReadOnlySpan<(Vector2, float, float)>)
                     [(axis, half.X, half.Y), (-axis, half.X, half.Y),
                      (across, half.Y, half.X), (-across, half.Y, half.X)])
            {
                if (!terrain.At(centreM + direction * (halfM + outM)).Drivable) continue;

                faces++;
                toStreet = direction;
                toStreetHalfM = halfM;
                alongHalfM = otherHalfM;
            }

            // Exactly one, or there is no telling which three sides the way past is: a lot with two roads
            // off it is a lot this construction has nothing true to say about, and leaving its pavement
            // severed says so where picking a face at random would not.
            if (faces != 1) continue;

            // The street's own pavement line stands half a walk back from the kerb, and the lot's street
            // face *is* the kerb — so the line the lot severs runs half a walk inside the lot's own box,
            // and the band picks it up half a walk clear of the lot at either end.
            var along = new Vector2(-toStreet.Y, toStreet.X);
            var onStreetM = toStreetHalfM - outM;
            var reachM = alongHalfM + outM;
            var nearNode = builder.SplitNearest(centreM - along * reachM + toStreet * onStreetM, bandM);
            var farNode = builder.SplitNearest(centreM + along * reachM + toStreet * onStreetM, bandM);
            if (nearNode < 0 || farNode < 0 || nearNode == farNode) continue;

            ways.Add(new LotWay(
                nearNode, farNode,
                centreM - along * reachM - toStreet * (toStreetHalfM + outM),
                centreM + along * reachM - toStreet * (toStreetHalfM + outM)));
            severed.Add((centreM, axis, half + new Vector2(outM, outM)));
        }

        // Every severed stretch goes before any band is laid, so that a band standing near a second lot
        // cannot be cut by it: what is refused is the pavement the lot was stamped over, and nothing else.
        builder.KillPavementWhere(pointM =>
        {
            foreach (var (centreM, axis, halfM) in severed)
            {
                var offM = pointM - centreM;
                var alongM = MathF.Abs(Vector2.Dot(offM, axis));
                var acrossM = MathF.Abs(Spline.Cross(axis, offM));
                if (alongM <= halfM.X && acrossM <= halfM.Y) return true;
            }

            return false;
        });

        var line = new ArcSeg[8];
        foreach (var way in ways)
        {
            ReadOnlySpan<Vector2> pointsM =
                [builder.PositionOf(way.NearNode), way.CornerAM, way.CornerBM, builder.PositionOf(way.FarNode)];
            var arcCount = Spline.FilletedInto(pointsM, outM, line);
            if (arcCount == 0) continue;

            builder.AddChain(way.NearNode, way.FarNode, line.AsSpan(0, arcCount), bandM, FootEdgeKind.Pavement);
        }
    }

    /// <summary>
    /// The arc about a centre that joins two nodes standing on its own circle. Walked with the angle
    /// increasing is a heading that increases, so the curvature is positive; the other way it is negative,
    /// and the piece needs no second form either way.
    /// </summary>
    static ArcSeg Around(Builder builder, Vector2 centreM, float radiusM, int fromNode, int toNode, bool theLongWay = false)
    {
        var fromM = builder.PositionOf(fromNode) - centreM;
        var toM = builder.PositionOf(toNode) - centreM;
        var fromRad = MathF.Atan2(fromM.Y, fromM.X);
        var sweepRad = Spline.WrapRad(MathF.Atan2(toM.Y, toM.X) - fromRad);
        if (theLongWay) sweepRad += sweepRad < 0f ? MathF.Tau : -MathF.Tau;

        var sign = sweepRad < 0f ? -1f : 1f;
        return new ArcSeg(
            builder.PositionOf(fromNode), fromRad + sign * MathF.PI * 0.5f, radiusM * MathF.Abs(sweepRad), sign / radiusM);
    }

    /// <summary>
    /// Every crossing, as the one edge of this network that touches a carriageway — and as a <b>T</b> in
    /// the pavement either side of it: a zebra attaches to the middle of a strip, where the strip's own
    /// line meets the paint's axis, and the pavement runs on through it.
    /// </summary>
    static void Crossings(CityPlan plan, Builder builder, float bandM)
    {
        var crossings = plan.Crosswalks;
        for (var crossing = 0; crossing < crossings.Count; crossing++)
        {
            var centreM = crossings.CentreM[crossing];
            var axis = Vector2.Normalize(crossings.Axis[crossing]);

            // The axis runs *along* the road the crossing crosses, so the way over is square to it and
            // the span is how far.
            var across = new Vector2(-axis.Y, axis.X);
            var reachM = crossings.SpanM[crossing] * 0.5f + bandM * 0.5f;

            var near = builder.SplitNearest(centreM - across * reachM, bandM);
            var far = builder.SplitNearest(centreM + across * reachM, bandM);
            if (near < 0 || far < 0 || near == far) continue;

            var fromM = builder.PositionOf(near);
            var toM = builder.PositionOf(far);
            var lineM = toM - fromM;
            builder.AddArc(
                near, far, new ArcSeg(fromM, MathF.Atan2(lineM.Y, lineM.X), lineM.Length(), 0f),
                crossings.DepthM[crossing], FootEdgeKind.Crossing);
        }
    }
}
