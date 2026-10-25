using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.App.Render;

/// <summary>The decks, and the level above the ground drawn as roads of its own.</summary>
internal sealed partial class GroundMesh
{
    /// <summary>
    /// <b>Every deck of one level, and what stands on it</b> (TER-3b.2): on the ground, each bridge's piers first, poking
    /// out from under where its deck is about to be laid; then every deck, its road's own line stroked out to the deck's
    /// width; then on each, a walkway either side of the carriageway, a parapet along either edge, a newel at every
    /// corner where it lands and a wing wall turned back along the bank from it, and its lamps. On the level above, the
    /// movements through a junction on it are decked too, at a lane and a walk either side: a road's deck stops at the
    /// mouth of its box, and the ground would show through the box.
    /// </summary>
    /// <remarks>
    /// <b>The wing walls and the piers are the ground's alone</b>: on the level above they would be drawn over the bodies
    /// on the road beneath.
    /// </remarks>
    void Decks(CityPlan plan, LaneLines lanes, byte level, SimConfig config)
    {
        var onTheGround = level == CityPlan.RoadArrays.Ground;
        if (onTheGround)
        {
            foreach (var deck in DecksOf(plan, lanes, level, config)) Piers(deck, plan.Water.Outline, config);
        }

        for (var connector = 0; connector < lanes.ConnectorCount && !onTheGround; connector++)
        {
            if (lanes.ConnectorLevel(connector) != level) continue;

            var widthM = lanes.ConnectorWidthM(connector) + (config.WalkOuterM * 2f);
            Stroke(lanes.ArcsOfConnector(connector), widthM, closed: false, Surface.Deck, Plain);
        }

        var looks = BridgeLooks.Shared;
        foreach (var deck in DecksOf(plan, lanes, level, config))
        {
            Stroke(deck.Line, deck.HalfM * 2f, closed: false, Surface.Deck, Plain);
            foreach (var onTheRight in (ReadOnlySpan<bool>)[false, true])
            {
                Band(deck.Line, deck.CarriagewayHalfM, deck.WalkOuterM, Surface.BridgeWalkway,
                    looks[BridgeLookKind.Walkway].PictureM, onTheRight);
                Band(deck.Line, deck.WalkOuterM, deck.HalfM, Surface.BridgeParapet,
                    looks[BridgeLookKind.Parapet].PictureM, onTheRight);
            }

            foreach (var atStart in (ReadOnlySpan<bool>)[true, false])
            {
                if (!deck.Lands(atStart)) continue;

                foreach (var side in (ReadOnlySpan<float>)[-1f, 1f])
                {
                    if (onTheGround) WingWall(deck, atStart, side, config);
                    Newel(deck, atStart, side);
                }
            }

            Lamps(deck, config);
        }
    }

    /// <summary>
    /// <b>The carriageway and the joints of every deck of one level</b> (TER-3b.2): over the town's own carriageway and
    /// under its kerb, a strip of the bridge's own road kerb to kerb along the span, and the expansion joint across the
    /// whole deck at each end where it lands — where the lanes of the road it lands on become the bridge's.
    /// </summary>
    void BridgeRoads(CityPlan plan, LaneLines lanes, byte level, SimConfig config)
    {
        var looks = BridgeLooks.Shared;
        var depthM = config.View.BridgeJointDepthM;
        foreach (var deck in DecksOf(plan, lanes, level, config))
        {
            var halfM = deck.CarriagewayHalfM;
            Stroke(deck.Line, false,
                new Sides(new Vector2(halfM), 0, 0, Surface.BridgeRoad, 1f / looks[BridgeLookKind.DeckRoad].PictureM));

            foreach (var atStart in (ReadOnlySpan<bool>)[true, false])
            {
                if (!deck.Lands(atStart)) continue;

                var (endM, inward) = deck.End(atStart);
                var middleM = endM + (inward * (depthM * 0.5f));
                var across = Heading.RightOf(inward) * deck.WalkOuterM;
                Stroke((ReadOnlySpan<Vector2>)[middleM - across, middleM + across], false,
                    new Sides(new Vector2(depthM * 0.5f), 0, 0, Surface.BridgeJoint, 1f / looks[BridgeLookKind.Joint].PictureM));
            }
        }
    }

    /// <summary>
    /// One deck as what stands on it is laid off: the line its road is drawn through, how far out each of its layers
    /// reaches off that line, and which of its two ends land on the road they carry on into.
    /// </summary>
    /// <param name="Arcs">The road's own line, which a fitting along it is placed by.</param>
    /// <param name="Line">The same line as the points a stroke of the deck's width is drawn through.</param>
    /// <param name="HalfM">The deck's half-width: out to the parapet's outer face.</param>
    /// <param name="WalkOuterM">Where the walkway meets the parapet.</param>
    /// <param name="LandsAtStart">
    /// Whether the end it starts at lands: every end of a bridge on the ground, which is its own road between its
    /// bridgeheads (GEN-14a), and on the level above an end at a bridgehead and not at a junction on the bridge.
    /// </param>
    readonly record struct Deck(
        ArcSeg[] Arcs, Vector2[] Line, float HalfM, float WalkOuterM, float CarriagewayHalfM, bool LandsAtStart,
        bool LandsAtEnd)
    {
        public float LengthM => Spline.TotalLengthM(Arcs);

        public bool Lands(bool atStart) => atStart ? LandsAtStart : LandsAtEnd;

        /// <summary>Where one end of the deck stands, and the way into the deck from it.</summary>
        public (Vector2 AtM, Vector2 Inward) End(bool atStart)
        {
            var at = Spline.SampleAt(Arcs, atStart ? 0f : LengthM);
            return (at.PositionM, atStart ? at.Direction : -at.Direction);
        }
    }

    static IEnumerable<Deck> DecksOf(CityPlan plan, LaneLines lanes, byte level, SimConfig config)
    {
        var parapetM = config.CityGen.BridgeParapetWidthM;
        for (var bridge = 0; bridge < plan.Bridges.Count; bridge++)
        {
            var road = plan.Bridges.Road[bridge];
            if (road < 0 || plan.Roads.LevelOf(road) != level) continue;

            var halfM = plan.Bridges.DeckWidthM[bridge] * 0.5f;
            var onTheGround = level == CityPlan.RoadArrays.Ground;
            var arcs = onTheGround
                ? OffTheShore(plan.Roads.SegmentsOf(road), halfM, plan.Water.Shore, config)
                : plan.Roads.SegmentsOf(road).ToArray();
            yield return new Deck(
                arcs, Walked(arcs, halfM, closed: false), halfM, halfM - parapetM, plan.Roads.WidthM[road] * 0.5f,
                onTheGround || lanes.IsBridgehead(plan.Roads.FromJunction[road]),
                onTheGround || lanes.IsBridgehead(plan.Roads.ToJunction[road]));
        }
    }

    /// <summary>
    /// <b>A bridge's line carried on straight past either end until its deck stands clear of the shore</b> (TER-3b.2): its
    /// bridgehead stands a junction's ground back from the water and the shore runs further back than that, and the walk
    /// beside the road stops where the shore begins — so a deck ending where its road does leaves the shore showing
    /// between the two. Asked at both of the deck's corners, the bank meeting a span at whatever angle it does, and never
    /// carried further than a shore's width twice over.
    /// </summary>
    static ArcSeg[] OffTheShore(ReadOnlySpan<ArcSeg> road, float halfM, CityPlan.RingArrays shore, SimConfig config)
    {
        const float stepM = 0.5f;
        var mostM = config.CityGen.ShoreWidthM * 2f;
        var lengthM = Spline.TotalLengthM(road);
        var start = Spline.SampleAt(road, 0f);
        var end = Spline.SampleAt(road, lengthM);
        var beforeM = Beyond(start.PositionM, -start.Direction);
        var afterM = Beyond(end.PositionM, end.Direction);

        var arcs = new List<ArcSeg>(road.Length + 2);
        if (beforeM > 0f)
        {
            arcs.Add(new ArcSeg(start.PositionM - (start.Direction * beforeM), Trig.Atan2(start.Direction.Y, start.Direction.X), beforeM, 0f));
        }

        arcs.AddRange(road);
        if (afterM > 0f) arcs.Add(new ArcSeg(end.PositionM, Trig.Atan2(end.Direction.Y, end.Direction.X), afterM, 0f));
        return [.. arcs];

        float Beyond(Vector2 endM, Vector2 outward)
        {
            var across = Heading.RightOf(outward) * halfM;
            var carriedM = 0f;
            while (carriedM < mostM
                   && (Covers(shore, endM + (outward * carriedM) + across) || Covers(shore, endM + (outward * carriedM) - across)))
            {
                carriedM += stepM;
            }

            return carriedM;
        }
    }

    /// <summary>
    /// <b>A strip laid beside a line</b>, from <paramref name="nearM"/> off it to <paramref name="farM"/>, on its right or
    /// its left, its picture's bottom edge on the near side — so a walkway's is the kerb's and a parapet's the walkway's,
    /// either side of the road alike.
    /// </summary>
    void Band(ReadOnlySpan<Vector2> line, float nearM, float farM, Surface strip, float pictureM, bool onTheRight)
    {
        // A corner files across from nought on the left of the way the line is walked to one on its right, and the
        // picture's bottom is one: so the band is walked with its near side on the right — the line backwards where the
        // band is on its right.
        var sides = new Sides(new Vector2(farM, -nearM), 0, 0, strip, 1f / pictureM);
        if (!onTheRight)
        {
            Stroke(line, closed: false, sides);
            return;
        }

        var reversed = line.ToArray();
        Array.Reverse(reversed);
        Stroke(reversed, closed: false, sides);
    }

    /// <summary>
    /// The parapet turned back along the bank at one corner of a landing end, away from the road (TER-3b.2): a strip of
    /// it from the corner, its picture's bottom edge on the road's side as the parapet's is.
    /// </summary>
    void WingWall(in Deck deck, bool atStart, float side, SimConfig config)
    {
        var (endM, inward) = deck.End(atStart);
        WingWall(endM, inward, side, deck.HalfM, deck.HalfM - deck.WalkOuterM, Surface.BridgeParapet, BridgeLookKind.Parapet, 1f, config);
    }

    /// <summary>
    /// <b>A wing wall</b> (TER-3b.2, RAIL-8): <paramref name="wallM"/> wide at the deck's edge, <paramref name="halfM"/> off
    /// its line, turned back from the end along the bank on one <paramref name="side"/>, as a strip of the picture from its
    /// top edge to <paramref name="acrossTo"/> of it — the parapet's whole, or the parapet's share of a rail bridge's side.
    /// </summary>
    void WingWall(
        Vector2 endM, Vector2 inward, float side, float halfM, float wallM, Surface strip, BridgeLookKind kind, float acrossTo,
        SimConfig config)
    {
        var outward = Heading.RightOf(inward) * side;
        var cornerM = endM + (outward * (halfM - (wallM * 0.5f)));
        var turnRad = config.View.BridgeWingWallTurnDeg * MathF.PI / 180f;
        var away = (-inward * MathF.Cos(turnRad)) + (outward * MathF.Sin(turnRad));
        var tipM = cornerM + (away * config.View.BridgeWingWallM);

        // Walked so the deck is on its right, where a strip's picture has its bottom.
        var deckOnTheRight = Vector2.Dot(Heading.RightOf(away), endM - cornerM) > 0f;
        var sides = new Sides(new Vector2(wallM * 0.5f), 0, 0, strip, 1f / BridgeLooks.Shared[kind].PictureM, 0f, acrossTo);
        Stroke(deckOnTheRight ? [cornerM, tipM] : (ReadOnlySpan<Vector2>)[tipM, cornerM], false, sides);
    }

    /// <summary>The pillar a parapet ends on at one corner of a landing end, standing on the parapet, facing the road.</summary>
    void Newel(in Deck deck, bool atStart, float side)
    {
        var (endM, inward) = deck.End(atStart);
        var outward = Heading.RightOf(inward) * side;
        Fitting(BridgeLookKind.Newel, Post(BridgeLookKind.Newel, endM, inward, outward, deck.HalfM - ((deck.HalfM - deck.WalkOuterM) * 0.5f)), -outward);
    }

    /// <summary>Where a post stands at a corner of a landing end: its own depth's half in from the end, <paramref name="outM"/> off the line.</summary>
    static Vector2 Post(BridgeLookKind kind, Vector2 endM, Vector2 inward, Vector2 outward, float outM) =>
        endM + (inward * (BridgeLooks.Shared[kind].FootprintM.Y * 0.5f)) + (outward * outM);

    /// <summary>
    /// The lamps along both parapets of a deck (<see cref="ViewFigures.BridgeLampPitchM"/>), the two sides opposite each
    /// other and evenly spaced, none nearer an end than half the spacing: each stands with the top edge of its picture on
    /// the parapet's outer face and its arm reaching over the walkway.
    /// </summary>
    void Lamps(in Deck deck, SimConfig config)
    {
        var lengthM = deck.LengthM;
        var count = Math.Max(1, (int)(lengthM / config.View.BridgeLampPitchM));
        var depthM = BridgeLooks.Shared[BridgeLookKind.Lamp].FootprintM.Y;
        for (var lamp = 0; lamp < count; lamp++)
        {
            var at = Spline.SampleAt(deck.Arcs, (lamp + 0.5f) * lengthM / count);
            foreach (var side in (ReadOnlySpan<float>)[-1f, 1f])
            {
                var outward = Heading.RightOf(at.Direction) * side;
                Fitting(BridgeLookKind.Lamp, at.PositionM + (outward * (deck.HalfM - (depthM * 0.5f))), -outward);
            }
        }
    }

    /// <summary>The share of a pier's nose laid under its deck, so no sliver of water shows between the two.</summary>
    const float PierUnderShare = 0.15f;

    /// <summary>
    /// The noses of a deck's piers where they poke out from under it (<see cref="ViewFigures.BridgePierPitchM"/>): a pier
    /// every pitch along the span, both sides, and only where the nose stands in the water. Laid before the deck, so the
    /// square edge it is cut at is under the deck's.
    /// </summary>
    void Piers(in Deck deck, CityPlan.RingArrays water, SimConfig config)
    {
        var lengthM = deck.LengthM;
        var piers = (int)MathF.Ceiling(lengthM / config.View.BridgePierPitchM) - 1;
        var depthM = BridgeLooks.Shared[BridgeLookKind.PierNose].FootprintM.Y;
        var underM = depthM * PierUnderShare;
        for (var pier = 1; pier <= piers; pier++)
        {
            var at = Spline.SampleAt(deck.Arcs, pier * lengthM / (piers + 1));
            foreach (var side in (ReadOnlySpan<float>)[-1f, 1f])
            {
                var outward = Heading.RightOf(at.Direction) * side;
                var centreM = at.PositionM + (outward * (deck.HalfM - underM + (depthM * 0.5f)));
                if (!Covers(water, centreM + (outward * (depthM * 0.5f)))) continue;

                Fitting(BridgeLookKind.PierNose, centreM, -outward);
            }
        }
    }

    /// <summary>
    /// <b>One fitting laid at a place</b>, as its cell of the bridge's sheet (<see cref="BridgeSheet.CellOf"/>) at its own
    /// footprint, the bottom of its picture toward <paramref name="down"/>.
    /// </summary>
    void Fitting(BridgeLookKind kind, Vector2 centreM, Vector2 down)
    {
        var sizeM = BridgeLooks.Shared[kind].FootprintM;
        var (alongFrom, alongTo, acrossFrom, acrossTo) = BridgeSheet.CellOf(kind);
        var across = new Vector2(down.Y, -down.X) * (sizeM.X * 0.5f);
        var downM = down * (sizeM.Y * 0.5f);
        Quad(
            Vertex(centreM - across - downM, GroundVertex.OnTheStrip(Surface.BridgeFittings, alongFrom, acrossFrom)),
            Vertex(centreM + across - downM, GroundVertex.OnTheStrip(Surface.BridgeFittings, alongTo, acrossFrom)),
            Vertex(centreM + across + downM, GroundVertex.OnTheStrip(Surface.BridgeFittings, alongTo, acrossTo)),
            Vertex(centreM - across + downM, GroundVertex.OnTheStrip(Surface.BridgeFittings, alongFrom, acrossTo)));
    }

    /// <summary>Whether a point stands in any of a set of rings, by how many of their edges a ray from it crosses.</summary>
    static bool Covers(CityPlan.RingArrays rings, Vector2 pointM)
    {
        var inside = false;
        for (var ring = 0; ring < rings.Count; ring++)
        {
            var points = rings.RingOf(ring);
            for (int at = 0, before = points.Length - 1; at < points.Length; before = at++)
            {
                var (a, b) = (points[at], points[before]);
                if ((a.Y > pointM.Y) != (b.Y > pointM.Y)
                    && pointM.X < a.X + ((pointM.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y)))
                {
                    inside = !inside;
                }
            }
        }

        return inside;
    }

    /// <summary>
    /// <b>The level above, as a road of its own over the ground and over the bodies on it</b> (TER-7b, PHY-1a): the
    /// decks, then the carriageway kerb to kerb (<see cref="Paving.Above"/>), then its kerb, then its paint — the
    /// ground's own stack again for the bridges over other roads, and nothing in it that is not on a bridge.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Its kerb stops where it lands</b>: at a bridgehead the bridge's end is the ground's road carried on, so the
    /// stretch of its boundary straight across the end is not struck (<see cref="Landings"/>). The ground runs on
    /// under it there (<see cref="SimConfig.UnderTheDeckM"/>), so the end of the deck meets tarmac. A bridge running
    /// off the map is cut by its edge, as the ground is.
    /// </para>
    /// <para>
    /// <b>Filled from the line itself and not thinned under a kerb</b>, because there is no kerb across its ends:
    /// a fill a share of a kerb short of a bridge's end is a stripe of deck across the road.
    /// </para>
    /// </remarks>
    void Above(
        CityPlan plan, Paving paving, SimConfig config, Crossings held, Crossings crossed, StopBars bars, LaneArrows arrows,
        CentrelineRuns runs, Crossings surveyed)
    {
        var rings = paving.Above(config);
        if (rings.Length == 0) return;

        const byte above = CityPlan.RoadArrays.Over;
        var kerbM = config.Road.KerbWidthM;
        Decks(plan, paving.Lanes, above, config);

        var line = MapCut.Rings(Line(rings), plan.WorldSizeM);
        Shell(line, Surface.Tarmac, Plain);
        BridgeRoads(plan, paving.Lanes, above, config);
        KerbAbove(line, Landings(paving.Lanes), plan.WorldSizeM, kerbM * 0.5f, kerbM, config);

        LaneDashes(runs, plan, config, held, crossed, Paint, above);
        RoadsideLines(plan, config, held, crossed, Paint, above);
        Zebras(surveyed, plan, config, Paint, above);
        Bars(bars, paving.Lanes, Paint, above);
        Arrows(paving.Lanes, arrows, config, Paint, above);
    }

    /// <summary>
    /// One end of a bridge where it lands on the ground: the line across its lanes' ends, as a place on it, the way
    /// along the bridge and how far either side of that place the lanes reach.
    /// </summary>
    readonly record struct Landing(Vector2 AtM, Vector2 Along, float FromM, float ToM);

    /// <summary>Every end of a lane above the ground at a bridgehead, gathered by road end into the line across it.</summary>
    static List<Landing> Landings(LaneLines lanes)
    {
        var landings = new List<Landing>();
        var byEnd = new Dictionary<(int Road, int Junction), int>();
        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            if (lanes.LaneLevel[lane] == CityPlan.RoadArrays.Ground) continue;

            var arcs = lanes.ArcsOf(lane);
            foreach (var atStart in (ReadOnlySpan<bool>)[true, false])
            {
                var junction = atStart ? lanes.LaneFromJunction[lane] : lanes.LaneToJunction[lane];
                if (!lanes.IsBridgehead(junction)) continue;

                var end = Spline.SampleAt(arcs, atStart ? 0f : lanes.LaneLengthM[lane]);
                if (!byEnd.TryGetValue((lanes.LaneRoad[lane], junction), out var at))
                {
                    at = landings.Count;
                    byEnd[(lanes.LaneRoad[lane], junction)] = at;
                    landings.Add(new Landing(end.PositionM, end.Direction, 0f, 0f));
                }

                var landing = landings[at];
                var acrossM = Vector2.Dot(end.PositionM - landing.AtM, Heading.RightOf(landing.Along));
                var halfM = lanes.LaneWidthM[lane] * 0.5f;
                landings[at] = landing with
                {
                    FromM = MathF.Min(landing.FromM, acrossM - halfM), ToM = MathF.Max(landing.ToM, acrossM + halfM),
                };
            }
        }

        return landings;
    }

    /// <summary>
    /// The level above's kerb: each ring laid as the town's kerbstones are (<see cref="Kerbstones"/>) with their shadow,
    /// less every stretch that runs straight across a bridge's end or along the map's edge.
    /// </summary>
    void KerbAbove(
        ReadOnlySpan<Vector2[]> line, List<Landing> landings, Vector2 worldM, float withinM, float kerbM, SimConfig config)
    {
        KerbShadow(line, Across, kerbM, config.View.KerbShadowM, config.View.KerbShadowShare, onTheRight: true);
        Kerbstones(line, Across, kerbM, Surface.Kerb, KerbLooks.Shared.Kerb.PictureM);

        bool Across(Vector2 fromM, Vector2 toM)
        {
            if (MapCut.AlongTheEdge(fromM, toM, worldM)) return true;

            foreach (var landing in landings)
            {
                if (On(landing, fromM) && On(landing, toM)) return true;
            }

            return false;
        }

        bool On(in Landing landing, Vector2 pointM)
        {
            var offM = pointM - landing.AtM;
            var acrossM = Vector2.Dot(offM, Heading.RightOf(landing.Along));
            return MathF.Abs(Vector2.Dot(offM, landing.Along)) <= withinM
                   && acrossM >= landing.FromM - withinM && acrossM <= landing.ToM + withinM;
        }
    }
}
