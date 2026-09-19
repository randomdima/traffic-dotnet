using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.App.Render;

/// <summary>Everything painted on the carriageway rather than made of it.</summary>
internal sealed partial class GroundMesh
{
    /// <summary>
    /// <b>The dashed line between two ribbons that touch</b> (TER-6), laid down every carriageway the town
    /// has one on (<see cref="CentrelineRuns"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Where a run stops and what it is a line down is the road's</b> and nothing this pass works out:
    /// what is here is the pitch, the width and the shade. A run is handed over as the one chain it is, so
    /// a street with a car park cut into it is one line and one phase rather than a piece either side of
    /// the lot.
    /// </para>
    /// <para>
    /// <b>And where it stops short of the paint at either end is the run's too</b>
    /// (<see cref="CentrelineRuns.PaintedM"/>). A run ends at a junction that forks and a crossing stands
    /// only at one, so its two ends are the only places its line meets other paint.
    /// </para>
    /// </remarks>
    void LaneDashes(CityPlan plan, SimConfig config, Crossings crossings, Vector3 tint, float[] periods)
    {
        var dashM = config.Road.LaneDashLengthM;
        var pitchM = dashM + config.Road.LaneDashGapM;
        var halfWidthM = config.Road.PaintLineWidthM * 0.5f;
        if (dashM <= 0f || pitchM <= dashM) return;

        var runs = CentrelineRuns.Lay(plan);
        for (var run = 0; run < runs.Count; run++)
        {
            var (fromM, toM) = runs.PaintedM(run, crossings, config);
            DashRun(runs.Of(run), fromM, toM, dashM, pitchM, halfWidthM, tint, periods);
        }
    }

    /// <summary>
    /// <b>One crossing's stripes</b> (TER-6): laid along the direction of the traffic that crosses them and
    /// repeated across the carriageway, <b>centred on the span</b> so a zebra never begins with half a
    /// stripe.
    /// </summary>
    void Zebras(Crossings crossings, SimConfig config, Vector3 tint, float[] periods)
    {
        var stripeM = config.Road.ZebraStripeWidthM;
        var pitchM = config.Road.ZebraStripePitchM;
        if (stripeM <= 0f || pitchM < stripeM) return;

        for (var crossing = 0; crossing < crossings.Count; crossing++)
        {
            var spanM = crossings.SpanM[crossing];
            var depthM = crossings.DepthM[crossing];
            if (spanM <= 0f || depthM <= 0f) continue;

            var along = crossings.Axis[crossing];
            var across = Heading.RightOf(along);
            var stripes = Math.Max(1, (int)MathF.Round((spanM + pitchM - stripeM) / pitchM));
            var laidM = (stripes * stripeM) + ((stripes - 1) * (pitchM - stripeM));
            while (laidM > spanM && stripes > 1)
            {
                stripes--;
                laidM = (stripes * stripeM) + ((stripes - 1) * (pitchM - stripeM));
            }

            var firstM = (stripeM * 0.5f) - (laidM * 0.5f);
            var halfM = new Vector2(depthM * 0.5f, stripeM * 0.5f);
            for (var stripe = 0; stripe < stripes; stripe++)
            {
                OrientedRect(
                    crossings.CentreM[crossing] + (across * (firstM + (stripe * pitchM))), along, halfM,
                    Surface.Tarmac, tint, periods);
            }
        }
    }

    /// <summary>
    /// <b>The bar a driver holds at</b> (TER-6), across every lane that arrives at an arm carrying a
    /// crossing (<see cref="StopBars"/>): one lane wide, square across the way that lane is driven, and a
    /// thickness of paint deep.
    /// </summary>
    /// <remarks>
    /// <b>The one mark here that is laid straight</b>, a bar being painted across a line rather than down
    /// one: what a lane's width of paint bows off the curve it is laid on is a bend's sag over a couple of
    /// metres, which is less than the bar is thick at the tightest radius a town is laid to.
    /// </remarks>
    void Bars(StopBars bars, Vector3 tint, float[] periods)
    {
        for (var bar = 0; bar < bars.Count; bar++)
        {
            OrientedRect(
                bars.CentreM[bar], bars.Approach[bar],
                new Vector2(bars.ThicknessM[bar] * 0.5f, bars.SpanM[bar] * 0.5f), Surface.Tarmac, tint,
                periods);
        }
    }

    /// <summary>
    /// <b>The arrows behind the bars</b> (TER-6a, <see cref="LaneArrows"/>): a shaft down the lane's own line
    /// and a branch off it for every turn the junction in front offers, each ending in a head laid on the
    /// tangent its own branch arrives at.
    /// </summary>
    /// <remarks>
    /// <b>The shaft is drawn once and the branches over it</b> rather than a whole glyph per turn: paint is
    /// laid on the tarmac in one shade and nothing is blended, so what a branch shares with the shaft is the
    /// same colour drawn twice — but a stem laid three times is three times the triangles, on every approach
    /// of every junction in the town.
    /// </remarks>
    void Arrows(LaneLines lanes, StopBars bars, SimConfig config, Vector3 tint, float[] periods)
    {
        var arrows = LaneArrows.Lay(lanes, bars, config);
        var halfWidthM = config.Road.LaneArrowShaftWidthM * 0.5f;
        var headM = config.Road.LaneArrowHeadLengthM;
        var halfHeadM = config.Road.LaneArrowHeadWidthM * 0.5f;
        if (halfWidthM <= 0f) return;

        for (var arrow = 0; arrow < arrows.Count; arrow++)
        {
            CurvedMark(
                lanes.ArcsOf(arrows.Lane[arrow]), arrows.FromM[arrow], arrows.ForkM[arrow], halfWidthM,
                Surface.Tarmac, tint, periods);

            for (var branch = arrows.BranchAt[arrow]; branch < arrows.BranchAt[arrow + 1]; branch++)
            {
                var arc = arrows.Branch.Slice(branch, 1);
                CurvedMark(arc, 0f, arc[0].LengthM, halfWidthM, Surface.Tarmac, tint, periods);
                Head(arc[0], headM, halfHeadM, tint, periods);
            }
        }
    }

    /// <summary>
    /// One head: the triangle standing on the end of its branch, square across the way that branch arrives
    /// and reaching a head's length further along it.
    /// </summary>
    void Head(in ArcSeg branch, float lengthM, float halfM, Vector3 tint, float[] periods)
    {
        var atM = branch.EndM;
        var along = Heading.Unit(branch.HeadingAtRad(branch.LengthM));
        var across = Heading.RightOf(along);
        Triangle(
            Vertex(atM - (across * halfM), Surface.Tarmac, tint, periods),
            Vertex(atM + (along * lengthM), Surface.Tarmac, tint, periods),
            Vertex(atM + (across * halfM), Surface.Tarmac, tint, periods));
    }

    /// <summary>
    /// <b>The line between one bay and the next, and nothing else</b> (GEN-4m): two bays standing side by
    /// side are two ribbons of driven ground touching along one line, and that is the only boundary of a
    /// car park the town's own geometry does not draw already — what runs round the outside of a rank is
    /// the kerb the pavement carries there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Solid, which is a dash the length of its own run</b> (<see cref="DashRun"/>). It is the one thing
    /// a bay says that a street does not: the mark is laid once for the pair rather than once per bay.
    /// </para>
    /// <para>
    /// <b>It is the ways that touch here, the spaces themselves not being laid yet</b>
    /// (<see href="../../../docs/index.md">the known gaps</see>). A bay is reached over a car's width of
    /// ground and a rank is laid one of those apart, so the two ribbons meet a half-width off either line;
    /// when the spaces land, the line a pair of them shares is this same line over a shorter run.
    /// </para>
    /// </remarks>
    void BayStrokes(CityPlan plan, SimConfig config, Vector3 tint, float[] periods)
    {
        var strokeM = config.Road.PaintLineWidthM;
        var parks = plan.CarParks;
        if (strokeM <= 0f || parks.Count == 0) return;

        var roads = plan.Roads;
        var longest = 0;
        foreach (var bay in parks.Road) longest = Math.Max(longest, roads.SegmentsOf(bay).Length);

        var seam = new ArcSeg[longest];
        for (var park = 0; park < parks.Count; park++)
        {
            var from = parks.BayOffsets[park];
            var to = parks.BayOffsets[park + 1];
            for (var one = from; one < to; one++)
            {
                for (var other = one + 1; other < to; other++)
                {
                    var pieces = Between(roads, parks.Road[one], parks.Road[other], seam);
                    if (pieces == 0) continue;

                    var line = seam.AsSpan(0, pieces);
                    var lengthM = Spline.TotalLengthM(line);
                    DashRun(line, 0f, lengthM, lengthM, lengthM, strokeM * 0.5f, tint, periods);
                }
            }
        }
    }

    /// <summary>
    /// The line two bays' ways meet along, or nothing where they do not meet: a half-width off the one,
    /// toward the other, where the two stand exactly their two half-widths apart.
    /// </summary>
    /// <remarks>
    /// <b>Asked of the pair rather than read off the rank's order</b> — the bays of one side are parallel
    /// and a way apart (GEN-53), so which pairs touch is a measurement and the pairs across a street or two
    /// steps down a rank fall out of it by standing further off than they are wide.
    /// </remarks>
    static int Between(CityPlan.RoadArrays roads, int one, int other, Span<ArcSeg> into)
    {
        var line = roads.SegmentsOf(one);
        var beside = roads.SegmentsOf(other);
        if (line.Length == 0 || beside.Length == 0) return 0;

        var at = Spline.SampleAt(line, 0f);
        var acrossM = Vector2.Dot(beside[0].StartM - at.PositionM, at.Right);
        var touchingM = (roads.WidthM[one] + roads.WidthM[other]) * 0.5f;
        if (MathF.Abs(MathF.Abs(acrossM) - touchingM) > LineTolerance.RoundingM) return 0;

        Spline.OffsetInto(line, MathF.Sign(acrossM) * roads.WidthM[one] * 0.5f, into);
        return line.Length;
    }

    /// <summary>
    /// One run of dashes down a stretch of a line, <b>centred on that stretch</b> so a street never begins
    /// with half a dash, and laid on the line's own curve rather than on the chord of it
    /// (<see cref="CurvedMark"/>).
    /// </summary>
    /// <remarks>
    /// <b>A solid line is a dashed one whose dash is the whole run</b>, so this is the whole of how a
    /// straight mark is painted and there is no second way to lay one.
    /// </remarks>
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
}
