using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Rail;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.App.Render;

/// <summary>Everything painted on the carriageway rather than made of it.</summary>
internal sealed partial class GroundMesh
{
    /// <summary>
    /// <b>The dashed line between two ribbons that touch</b> (TER-6), laid down every carriageway the town
    /// has one on (<see cref="CentrelineRuns"/>) — unbroken where the run says so
    /// (<see cref="CentrelineRuns.IsUnbroken"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Where a run stops and what it is a line down is the road's</b> and nothing this pass works out:
    /// what is here is the pitch, the width and the shade. A run is handed over as the one chain it is, so
    /// a street with a node nothing turns at is one line and one phase rather than a piece either side of
    /// it.
    /// </para>
    /// <para>
    /// <b>And where it stops short of the paint at either end is the run's too</b>
    /// (<see cref="CentrelineRuns.PaintedM"/>). A run ends at a junction that forks and a crossing stands at one
    /// — or midway down a road too short to be crossed twice (WLK-10a), which the line is parted round
    /// (<see cref="LineRound"/>).
    /// </para>
    /// </remarks>
    /// <param name="crossings">What each arm holds behind, which the line stops short of at its two ends.</param>
    /// <param name="zebras">The paint the walk crosses on, whose bands the line is parted round wherever it runs under one.</param>
    /// <param name="level">The level whose runs are laid: a run never crosses from one to the other (<see cref="CentrelineRuns"/>).</param>
    void LaneDashes(
        CentrelineRuns runs, CityPlan plan, SimConfig config, Crossings crossings, Crossings zebras, Vector3 tint, byte level)
    {
        var dashM = config.Road.LaneDashLengthM;
        var pitchM = dashM + config.Road.LaneDashGapM;
        var halfWidthM = config.Road.PaintLineWidthM * 0.5f;
        if (dashM <= 0f || pitchM <= dashM) return;

        var bands = new PaintedBands(zebras, PaintedStepM(config));
        var levelCrossings = level == CityPlan.RoadArrays.Ground ? new CrossingStretchFinder(plan, config) : null;
        for (var run = 0; run < runs.Count; run++)
        {
            if (plan.Roads.LevelOf(runs.RoadsOf(run)[0]) != level) continue;

            var arcs = runs.Of(run);
            var (fromM, toM) = runs.PaintedM(run, crossings, config);
            if (runs.IsUnbroken(run) || levelCrossings is null)
            {
                LineRound(arcs, bands, config, fromM, toM, runs.IsUnbroken(run) ? 0f : dashM, pitchM, halfWidthM, tint);
                continue;
            }

            // Unbroken from stop bar to stop bar over a level crossing, as a junction's lines are (RAIL-7).
            var pieceFromM = fromM;
            foreach (var stretch in levelCrossings.Along(arcs, toM, CrossingLineKind.Painted))
            {
                var (lowM, highM) = (MathF.Max(stretch.FromM, pieceFromM), MathF.Min(stretch.ToM, toM));
                if (highM <= lowM) continue;

                LineRound(arcs, bands, config, pieceFromM, lowM, dashM, pitchM, halfWidthM, tint);
                LineRound(arcs, bands, config, lowM, highM, 0f, pitchM, halfWidthM, tint);
                pieceFromM = highM;
            }

            LineRound(arcs, bands, config, pieceFromM, toM, dashM, pitchM, halfWidthM, tint);
        }
    }

    /// <summary>
    /// <b>One line from <paramref name="fromM"/> to <paramref name="toM"/>, parted round every painted band it would run
    /// under</b> (TER-6) — one midway down a road it runs along (WLK-10a), or another road's reaching over it — each a
    /// bar's setback clear of the band either side, as a line stops short of the paint at a road's end. A
    /// <paramref name="dashM"/> of nought is a line unbroken.
    /// </summary>
    void LineRound(
        ReadOnlySpan<ArcSeg> arcs, PaintedBands bands, SimConfig config, float fromM, float toM,
        float dashM, float pitchM, float halfWidthM, Vector3 tint)
    {
        var clearM = config.Road.StopBarSetbackM;
        var pieceFromM = fromM;
        foreach (var (underM, overM) in Under(arcs, bands, config, fromM, toM, halfWidthM))
        {
            var (lowM, highM) = (underM - clearM, overM + clearM);
            if (highM <= pieceFromM || lowM >= toM) continue;

            LinePiece(arcs, pieceFromM, MathF.Min(lowM, toM), dashM, pitchM, halfWidthM, tint, config.Road.LaneDashLengthM);
            pieceFromM = MathF.Max(pieceFromM, highM);
        }

        LinePiece(arcs, pieceFromM, toM, dashM, pitchM, halfWidthM, tint, pieceFromM > fromM ? config.Road.LaneDashLengthM : 0f);
    }

    /// <summary>
    /// One unparted stretch of a line, dashed or — at a <paramref name="dashM"/> of nought — unbroken, and none at all
    /// shorter than <paramref name="leastM"/>: what parting a line round a band leaves between the band and a bar is a
    /// stub that reads as a stray dash.
    /// </summary>
    void LinePiece(
        ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, float dashM, float pitchM, float halfWidthM, Vector3 tint, float leastM)
    {
        if (toM - fromM <= leastM || toM <= fromM) return;

        if (dashM <= 0f) DashRun(arcs, fromM, toM, toM - fromM, toM - fromM, halfWidthM, tint);
        else DashRun(arcs, fromM, toM, dashM, pitchM, halfWidthM, tint);
    }

    /// <summary>
    /// How long a stretch of line is asked of the painted bands at once (<see cref="Under"/>): a crossing's depth, the
    /// narrowest a band is along a line it lies across, so no band is stepped over.
    /// </summary>
    static float PaintedStepM(SimConfig config) => config.Road.CrossingDepthM;

    /// <summary>
    /// <b>The stretches of a line from <paramref name="fromM"/> to <paramref name="toM"/> that run under a painted
    /// band</b>, in order along it: asked a crossing's depth at a time, and a stretch that answers read again a stripe at a
    /// time (<see cref="RoadFigures.ZebraStripeWidthM"/>), which is as finely as the paint over it is laid.
    /// </summary>
    static List<(float LowM, float HighM)> Under(
        ReadOnlySpan<ArcSeg> arcs, PaintedBands bands, SimConfig config, float fromM, float toM, float halfWidthM)
    {
        var under = new List<(float LowM, float HighM)>();
        var stepM = PaintedStepM(config);
        var fineM = config.Road.ZebraStripeWidthM;
        if (stepM <= 0f || fineM <= 0f) return under;

        for (var atM = fromM; atM < toM; atM += stepM)
        {
            var endM = MathF.Min(atM + stepM, toM);
            if (!Painted(arcs, atM, endM)) continue;

            for (var fine = atM; fine < endM; fine += fineM)
            {
                var fineEndM = MathF.Min(fine + fineM, endM);
                if (!Painted(arcs, fine, fineEndM)) continue;

                if (under.Count > 0 && under[^1].HighM >= fine) under[^1] = (under[^1].LowM, fineEndM);
                else under.Add((fine, fineEndM));
            }
        }

        return under;

        bool Painted(ReadOnlySpan<ArcSeg> line, float lowM, float highM)
        {
            var middle = Spline.SampleAt(line, (lowM + highM) * 0.5f);
            return bands.Under(new OrientedBox(middle.PositionM, middle.Direction, (highM - lowM) * 0.5f, halfWidthM)) != Crossings.None;
        }
    }

    /// <summary>
    /// <b>The solid line between a road's lanes and its roadside</b> (TER-6, GEN-57): down the lanes' own edge at
    /// each kerb the road carries a roadside at (<see cref="CityPlan.RoadArrays.RoadsideLineM"/>), from one end of the
    /// road to the other less the paint at either end, as a lane line stops (<see cref="CentrelineRuns.PaintedM(ReadOnlySpan{ArcSeg}, int, int, Crossings, SimConfig)"/>).
    /// </summary>
    /// <remarks>
    /// <b>Solid, and a road's own</b>: it parts what is driven from what is not, so it is not carried through a
    /// junction the way a lane line is — the turns out of the kerb lane sweep over the box past it.
    /// </remarks>
    /// <param name="level">The level whose lines are laid: a roadside is its road's.</param>
    void RoadsideLines(CityPlan plan, SimConfig config, Crossings crossings, Crossings zebras, Vector3 tint, byte level)
    {
        var roads = plan.Roads;
        var halfWidthM = config.Road.PaintLineWidthM * 0.5f;
        if (roads.RoadsideWithM.Length == 0 || halfWidthM <= 0f) return;

        var bands = new PaintedBands(zebras, PaintedStepM(config));

        for (var road = 0; road < roads.Count; road++)
        {
            var line = roads.SegmentsOf(road);
            if (line.Length == 0 || roads.LevelOf(road) != level) continue;

            var edge = new ArcSeg[line.Length];
            foreach (var withTheRoad in (ReadOnlySpan<bool>)[true, false])
            {
                if (roads.RoadsideM(road, withTheRoad) <= 0f) continue;

                var sideSign = withTheRoad ? config.RoadSideSign : -config.RoadSideSign;
                Spline.OffsetInto(line, roads.RoadsideLineM(road, withTheRoad) * sideSign, edge);
                var (fromM, toM) = CentrelineRuns.PaintedM(
                    edge, JunctionArms.End(road, atTo: false), JunctionArms.End(road, atTo: true), crossings, config);
                LineRound(edge, bands, config, fromM, toM, 0f, 0f, halfWidthM, tint);
            }
        }
    }

    /// <summary>
    /// <b>One crossing's stripes</b> (TER-6): laid along the direction of the traffic that crosses them and
    /// repeated across the carriageway, <b>centred on the span</b> so a zebra never begins with half a
    /// stripe.
    /// </summary>
    /// <param name="level">The level whose crossings are laid: a crossing is its road's.</param>
    void Zebras(Crossings crossings, CityPlan plan, SimConfig config, Vector3 tint, byte level)
    {
        var stripeM = config.Road.ZebraStripeWidthM;
        var pitchM = config.Road.ZebraStripePitchM;
        if (stripeM <= 0f || pitchM < stripeM) return;

        for (var crossing = 0; crossing < crossings.Count; crossing++)
        {
            var road = crossings.Road[crossing];
            if ((road >= 0 ? plan.Roads.LevelOf(road) : CityPlan.RoadArrays.Ground) != level) continue;

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
                    Surface.Tarmac, tint);
            }
        }
    }

    /// <summary>
    /// <b>The bar a driver holds at</b> (TER-6), across every lane that arrives at an arm carrying a
    /// crossing (<see cref="StopBars"/>): one lane wide, square across its <see cref="StopBars.Approach"/> — parallel
    /// to the band in front of it — and a thickness of paint deep.
    /// </summary>
    /// <remarks>
    /// <b>The one mark here that is laid straight</b>, a bar being painted across a line rather than down
    /// one: what a lane's width of paint bows off the curve it is laid on is a bend's sag over a couple of
    /// metres, which is less than the bar is thick at the tightest radius a town is laid to.
    /// </remarks>
    /// <param name="level">The level whose bars are laid: a bar is its lane's.</param>
    void Bars(StopBars bars, LaneLines lanes, Vector3 tint, byte level)
    {
        for (var bar = 0; bar < bars.Count; bar++)
        {
            if (lanes.LaneLevel[bars.Lane[bar]] != level) continue;

            OrientedRect(
                bars.CentreM[bar], bars.Approach[bar],
                new Vector2(bars.ThicknessM[bar] * 0.5f, bars.SpanM[bar] * 0.5f), Surface.Tarmac, tint);
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
    /// <param name="level">The level whose arrows are laid: an arrow is its lane's.</param>
    void Arrows(LaneLines lanes, LaneArrows arrows, SimConfig config, Vector3 tint, byte level)
    {
        var halfWidthM = config.Road.LaneArrowShaftWidthM * 0.5f;
        var headM = config.Road.LaneArrowHeadLengthM;
        var halfHeadM = config.Road.LaneArrowHeadWidthM * 0.5f;
        if (halfWidthM <= 0f) return;

        for (var arrow = 0; arrow < arrows.Count; arrow++)
        {
            if (lanes.LaneLevel[arrows.Lane[arrow]] != level) continue;

            CurvedMark(
                lanes.ArcsOf(arrows.Lane[arrow]), arrows.FromM[arrow], arrows.ForkM[arrow], halfWidthM,
                Surface.Tarmac, tint);

            for (var branch = arrows.BranchAt[arrow]; branch < arrows.BranchAt[arrow + 1]; branch++)
            {
                var arc = arrows.Branch.Slice(branch, 1);
                CurvedMark(arc, 0f, arc[0].LengthM, halfWidthM, Surface.Tarmac, tint);
                Head(arc[0], headM, halfHeadM, tint);
            }
        }
    }

    /// <summary>
    /// One head: the triangle standing on the end of its branch, square across the way that branch arrives
    /// and reaching a head's length further along it.
    /// </summary>
    void Head(in ArcSeg branch, float lengthM, float halfM, Vector3 tint)
    {
        var atM = branch.EndM;
        var along = Heading.Unit(branch.HeadingAtRad(branch.LengthM));
        var across = Heading.RightOf(along);
        Triangle(
            Vertex(atM - (across * halfM), Surface.Tarmac, tint),
            Vertex(atM + (along * lengthM), Surface.Tarmac, tint),
            Vertex(atM + (across * halfM), Surface.Tarmac, tint));
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
    void BayStrokes(CityPlan plan, SimConfig config, Vector3 tint)
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

                    // Along the space and not the apron in front of it (GEN-53), which is ground the street's side of it.
                    var line = seam.AsSpan(0, pieces);
                    var lengthM = Spline.TotalLengthM(line);
                    var fromM = MathF.Max(0f, lengthM - config.CarParkBayLengthM);
                    DashRun(line, fromM, lengthM, lengthM - fromM, lengthM - fromM, strokeM * 0.5f, tint);
                }
            }
        }
    }

    /// <summary>
    /// The line two bays meet along, or nothing where they do not meet: a half-width off the one,
    /// toward the other, where the two stand exactly their two half-widths apart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Asked of the pair rather than read off the rank's order</b> — the bays of one side are parallel
    /// and a way apart (GEN-53), so which pairs touch is a measurement and two steps down a rank falls out
    /// of it by standing further off than they are wide.
    /// </para>
    /// <para>
    /// <b>The two have to run the same way as well as stand a width apart</b>, because the measurement is
    /// taken across the one line and drops everything along it: the bays of the far side of the street
    /// point back the other way and one of those, a step further along the street, reads exactly as a
    /// neighbour does. What it would paint is the shorter rank's own outer boundary — the kerb line, in the
    /// one place a driver is looking (GEN-4m).
    /// </para>
    /// </remarks>
    static int Between(CityPlan.RoadArrays roads, int one, int other, Span<ArcSeg> into)
    {
        var line = roads.SegmentsOf(one);
        var beside = roads.SegmentsOf(other);
        if (line.Length == 0 || beside.Length == 0) return 0;
        if (Vector2.Dot(line[0].StartUnit, beside[0].StartUnit) <= 0f) return 0;

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
        Vector3 tint)
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
            CurvedMark(arcs, atM, atM + dashM, halfWidthM, Surface.Tarmac, tint);
        }
    }
}
