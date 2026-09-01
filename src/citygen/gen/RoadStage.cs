using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>Every road of the layout as the curve it is driven along</b>, and the junctions at its ends: the
/// stage that turns nodes and edges into the arcs, the discs and the widths a plan carries.
/// </summary>
/// <remarks>
/// <para>
/// <b>A street is a chord that wanders, and the wander is bounded by geometry rather than by taste</b>
/// (GEN-12). The
/// traced cities are the argument: their streets are single arcs at the ninetieth percentile and their
/// median sinuosity is 1.000, so straight is what a street is and a bend is what it is allowed. Three
/// bounds hold, and each of them is a rule rather than a preference:
/// </para>
/// <list type="bullet">
/// <item><b>Both ends are straight.</b> A junction's own ground, the corner an arm flares back to, the
/// crossing and the bar behind it are all laid across a straight arm, so the first and last stretch of every
/// road is one straight piece and the wander lives between them. <b>An end at a node that forks nothing is
/// the exception</b> (GEN-12a): the two arms there are swept into one arc, and what that arm carries is laid
/// past it (<see cref="Bends"/>).</item>
/// <item><b>The wander is bounded by the block and not by the road</b> — a street may not stray so far off
/// its chord that it could meet the street a block over.</item>
/// <item><b>Nothing bends tighter than its own class's floor</b>, which is what
/// <see cref="SimConfig.CarCorneringRadiusM"/> gives for the speed that class is laid for. The floor is
/// derived from a speed and a grip; it is never authored as a radius.</item>
/// </list>
/// <para>
/// <b>The orbital is the exception and it is an arc by construction</b>: it is a circle, and its curvature
/// comes from the layout rather than from any draw here.
/// </para>
/// </remarks>
internal static class RoadStage
{
    /// <summary>How much of a segment a rounded corner may eat, either side of the vertex it rounds.</summary>
    const float TangentShareOfSegment = 0.45f;

    /// <summary>Below this deflection a vertex is straight through and is not rounded at all.</summary>
    const float StraightThroughRad = 0.002f;

    internal readonly record struct Laid(
        CityPlan.RoadArrays Roads,
        CityPlan.JunctionArrays Junctions,
        CityPlan.JunctionCornerArrays Corners,
        CityPlan.CrosswalkArrays Crosswalks,
        CityPlan.StopLineArrays StopLines,
        CityPlan.BridgeArrays Bridges);

    public static Laid Lay(
        TownLayout layout, Districts districts, TownBrief brief, GenRaster raster, GroundPainter painter,
        SimConfig config, ref Rng shape, ref Rng signals)
    {
        // One width for every road there is, arterial or street (GEN-15).
        var widthM = config.RoadWidthM;
        var chains = new ArcSeg[layout.Edges.Count][];
        for (var road = 0; road < layout.Edges.Count; road++)
        {
            chains[road] = Chain(layout, districts, layout.Edges[road], config, ref shape);
        }

        var junctions = Junctions(layout, Bends(layout, chains, config), brief, config, widthM, ref signals);
        var furniture = Furniture.Lay(layout, chains, junctions, config, widthM);

        Paint(painter, chains, junctions, furniture, config, widthM);

        return new Laid(
            Roads(layout, chains, widthM),
            junctions,
            furniture.Corners,
            furniture.Crosswalks,
            furniture.StopLines,
            Bridges(layout, chains, config, widthM));
    }

    /// <summary>
    /// One road's own curve. <b>Everything is an arc</b>: a straight is one at zero curvature, the orbital's
    /// piece is one the layout asked for, and a wandering street is straights with its corners rounded off.
    /// </summary>
    static ArcSeg[] Chain(
        TownLayout layout, Districts districts, LayoutEdge edge, SimConfig config, ref Rng draw)
    {
        var fromM = layout.NodeM[edge.From];
        var toM = layout.NodeM[edge.To];
        var chordM = toM - fromM;
        var lengthM = chordM.Length();
        if (lengthM <= 0f) return [];

        var unit = chordM / lengthM;

        // <b>A bridge is straight and nothing else</b> (GEN-14a): the deck is a straight thing, and the two
        // bridgeheads are already the shortest line over the water the layout could find.
        if (edge.Class == RoadClass.Bridge) return [new ArcSeg(fromM, Facing(unit), lengthM, 0f)];
        if (MathF.Abs(edge.Curvature) > 0f) return [Arc(fromM, unit, lengthM, edge.Curvature)];

        var stubM = StubM(config);
        var wanderNodes = WanderNodes(districts, edge, fromM, toM, ref draw);
        if (wanderNodes == 0 || lengthM <= stubM * 2.5f)
        {
            return [new ArcSeg(fromM, Facing(unit), lengthM, 0f)];
        }

        Span<Vector2> pointsM = stackalloc Vector2[wanderNodes + 2];
        Wander(pointsM, districts, edge, fromM, toM, unit, lengthM, stubM, wanderNodes, config, ref draw);
        return Rounded(pointsM, FloorRadiusM(config, edge.Class));
    }

    /// <summary>The one arc a piece of the orbital is: the chord it stands on decides how far round it goes.</summary>
    static ArcSeg Arc(Vector2 fromM, Vector2 unit, float chordM, float curvature)
    {
        var radiusM = 1f / MathF.Abs(curvature);
        var half = MathF.Asin(MathF.Min(1f, chordM * 0.5f / radiusM));
        // An arc's chord runs at its own start bearing plus half its sweep (<see cref="ArcSeg.PointAtM"/>),
        // so the bearing it has to start on is the chord's less that half — the tangent at the node.
        var sweep = 2f * half * MathF.Sign(curvature);
        return new ArcSeg(fromM, Facing(unit) - (sweep * 0.5f), MathF.Abs(sweep) * radiusM, curvature);
    }

    /// <summary>
    /// The points a wandering street is drawn through: its two ends, and its virtual nodes standing off the
    /// chord between the stubs. <b>The offset is clamped so that the corner it makes cannot be tighter than
    /// the class's floor</b> — a street asked to wander further than its own length can turn through is
    /// straightened rather than bent past what a car could take.
    /// </summary>
    static void Wander(
        Span<Vector2> pointsM, Districts districts, LayoutEdge edge, Vector2 fromM, Vector2 toM, Vector2 unit,
        float lengthM, float stubM, int nodes, SimConfig config, ref Rng draw)
    {
        var side = Heading.RightOf(unit);
        var segmentM = (lengthM - (stubM * 2f)) / (nodes + 1);
        var floorM = FloorRadiusM(config, edge.Class);
        var wanderM = MathF.Min(
            WanderM(districts, edge, (fromM + toM) * 0.5f, config),
            TangentShareOfSegment * segmentM * segmentM / MathF.Max(floorM, 1f));

        pointsM[0] = fromM;
        pointsM[^1] = toM;
        for (var node = 0; node < nodes; node++)
        {
            var alongM = stubM + (segmentM * (node + 1));
            pointsM[node + 1] = fromM + (unit * alongM) + (side * draw.NextFloat(-wanderM, wanderM));
        }
    }

    static int WanderNodes(Districts districts, LayoutEdge edge, Vector2 fromM, Vector2 toM, ref Rng draw)
    {
        // A spoke is straight because the layout reads it as a ray: everything that asks whether a point
        // stands clear of an arterial asks it of a line through the hub.
        if (edge.Class != RoadClass.Street) return 0;

        var districtAt = districts.At((fromM + toM) * 0.5f);
        var strict = districtAt < 0 || districts[districtAt].Strict;
        return strict ? draw.NextInt(2) : 1 + draw.NextInt(3);
    }

    /// <summary>
    /// A polyline with its corners rounded off, as the chain of straights and arcs a road is driven along.
    /// <b>Tangent continuous by construction</b>: each arc leaves the straight before it on that straight's
    /// own bearing, which is what a follower reads off the road and what keeps a drawn ribbon from creasing.
    /// </summary>
    static ArcSeg[] Rounded(ReadOnlySpan<Vector2> pointsM, float floorRadiusM)
    {
        var chain = new List<ArcSeg>((pointsM.Length * 2) - 1);
        var atM = pointsM[0];
        for (var vertex = 1; vertex + 1 < pointsM.Length; vertex++)
        {
            var intoM = pointsM[vertex] - atM;
            var outOfM = pointsM[vertex + 1] - pointsM[vertex];
            var intoLengthM = intoM.Length();
            var outOfLengthM = outOfM.Length();
            if (intoLengthM <= 0f || outOfLengthM <= 0f) continue;

            var into = intoM / intoLengthM;
            var outOf = outOfM / outOfLengthM;
            var deflection = MathF.Asin(Math.Clamp(Cross(into, outOf), -1f, 1f));
            if (MathF.Abs(deflection) < StraightThroughRad) continue;

            // <b>A corner too tight for the class is not rounded harder, it is not turned at all</b>: the
            // road runs straight through the vertex and the wander it asked for is given up. Rounding it at
            // the floor instead would need a tangent longer than the straights either side can spare, and
            // what that lays is a road that leaves its own carriageway.
            var tangentM = TangentShareOfSegment * MathF.Min(intoLengthM, outOfLengthM);
            var radiusM = tangentM / MathF.Tan(MathF.Abs(deflection) * 0.5f);
            if (radiusM < floorRadiusM) continue;

            var startsM = pointsM[vertex] - (into * tangentM);
            var straightM = (startsM - atM).Length();
            if (straightM > 0f) chain.Add(new ArcSeg(atM, Facing(into), straightM, 0f));

            var curvature = MathF.Sign(deflection) / radiusM;
            chain.Add(new ArcSeg(startsM, Facing(into), MathF.Abs(deflection) * radiusM, curvature));
            atM = pointsM[vertex] + (outOf * tangentM);
        }

        var lastM = pointsM[^1] - atM;
        var runM = lastM.Length();
        if (runM > 0f) chain.Add(new ArcSeg(atM, Facing(lastM / runM), runM, 0f));

        return [.. chain];
    }

    /// <summary>
    /// <b>A node with two arms is a road that bends, not a corner cars turn across</b> (TER-5b, GEN-12a): the
    /// two chains meeting there are bent to arrive on <b>one tangent</b>, each taking half the turn on an arc
    /// of the same radius, and the node moves to the middle of that arc. What is left is an inline junction —
    /// two arms leaving in opposite directions, paving no ground of its own — and a carriageway, a kerb and a
    /// lane line that run through it without a crease. The junction centres are what comes back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The arc is the widest the two roads can spare and never wider than the class's floor</b>
    /// (<see cref="FloorRadiusM"/>): sweeping wider than the speed a road is laid for asks only cuts deeper
    /// inside the corner, and the ground it cuts is ground the layout put somewhere else.
    /// </para>
    /// <para>
    /// <b>A pair with room for nothing better than the fillet keeps its junction.</b> Below
    /// <see cref="SimConfig.RoadCornerRadiusM"/> — the bend whose inner kerb stands exactly where a junction
    /// would have flared it — a bend is a worse corner than the corner it replaces, and a bridge is straight
    /// and nothing else (GEN-14a).
    /// </para>
    /// </remarks>
    static Vector2[] Bends(TownLayout layout, ArcSeg[][] chains, SimConfig config)
    {
        var centreM = new Vector2[layout.NodeM.Count];
        for (var node = 0; node < centreM.Length; node++) centreM[node] = layout.NodeM[node];

        var arms = new List<(int Road, bool AtFromEnd)>[centreM.Length];
        for (var node = 0; node < arms.Length; node++) arms[node] = [];

        for (var road = 0; road < chains.Length; road++)
        {
            if (chains[road].Length == 0) continue;

            arms[layout.Edges[road].From].Add((road, true));
            arms[layout.Edges[road].To].Add((road, false));
        }

        for (var node = 0; node < arms.Length; node++)
        {
            if (arms[node].Count != 2) continue;
            if (layout.Edges[arms[node][0].Road].Class == RoadClass.Bridge) continue;
            if (layout.Edges[arms[node][1].Road].Class == RoadClass.Bridge) continue;

            Bend(layout, chains, config, arms[node][0], arms[node][1], ref centreM[node]);
        }

        return centreM;
    }

    /// <summary>One such node bent: both chains rewritten and the node moved onto the arc they now share.</summary>
    static void Bend(
        TownLayout layout, ArcSeg[][] chains, SimConfig config, (int Road, bool AtFromEnd) first,
        (int Road, bool AtFromEnd) second, ref Vector2 nodeM)
    {
        var outOfFirst = Outward(chains[first.Road], first.AtFromEnd);
        var outOfSecond = Outward(chains[second.Road], second.AtFromEnd);

        // How far the through traffic is turned at the node, which is the whole of what has to be swept.
        var into = -outOfFirst;
        var deflection = MathF.Atan2(Cross(into, outOfSecond), Vector2.Dot(into, outOfSecond));
        var halfTurn = MathF.Abs(deflection) * 0.5f;
        if (halfTurn < StraightThroughRad) return;

        var spareM = TangentShareOfSegment * MathF.Min(
            StraightAtTheEndM(chains[first.Road], first.AtFromEnd),
            StraightAtTheEndM(chains[second.Road], second.AtFromEnd));
        var radiusM = MathF.Min(
            spareM / MathF.Tan(halfTurn),
            MathF.Max(
                FloorRadiusM(config, layout.Edges[first.Road].Class),
                FloorRadiusM(config, layout.Edges[second.Road].Class)));
        if (radiusM < config.RoadCornerRadiusM) return;

        // The arc tangent to both arms stands off the node along the bisector of the wedge between them,
        // and the two chains now meet at its middle rather than at the node the layout put there.
        var tangentM = radiusM * MathF.Tan(halfTurn);
        var bisector = Vector2.Normalize(outOfFirst + outOfSecond);
        var apexM = nodeM + (bisector * ((radiusM / MathF.Cos(halfTurn)) - radiusM));

        // The heading the through traffic holds at the apex, from the first arm towards the second, and the
        // way it is turning: each arm's own half is that arc walked outward, so the first one's is reversed.
        var through = Vector2.Normalize(outOfSecond - outOfFirst);
        var curvature = MathF.Sign(deflection) / radiusM;

        chains[first.Road] = Bent(
            chains[first.Road], first.AtFromEnd, apexM, nodeM + (outOfFirst * tangentM), -through, outOfFirst,
            -curvature, radiusM * halfTurn, tangentM);
        chains[second.Road] = Bent(
            chains[second.Road], second.AtFromEnd, apexM, nodeM + (outOfSecond * tangentM), through,
            outOfSecond, curvature, radiusM * halfTurn, tangentM);
        nodeM = apexM;
    }

    /// <summary>
    /// One chain with the half-turn on its end: the straight there is cut back to where the arc leaves it,
    /// and the arc runs from the apex out to that point. <b>Reckoned apex-outward whichever way the road is
    /// driven</b>, and laid in reversed where the road ends at the node rather than starting there.
    /// </summary>
    static ArcSeg[] Bent(
        ArcSeg[] chain, bool atFromEnd, Vector2 apexM, Vector2 tangentPointM, Vector2 apexHeading,
        Vector2 outward, float curvature, float sweptM, float tangentM)
    {
        var trimmed = Trimmed(chain, atFromEnd, tangentM);
        if (atFromEnd) return [new ArcSeg(apexM, Facing(apexHeading), sweptM, curvature), .. trimmed];

        return [.. trimmed, new ArcSeg(tangentPointM, Facing(-outward), sweptM, -curvature)];
    }

    /// <summary>Which way a chain leaves one of its ends, as the unit pointing away from the node there.</summary>
    static Vector2 Outward(ArcSeg[] chain, bool atFromEnd) =>
        atFromEnd
            ? Spline.SampleAt(chain, 0f).Direction
            : -Spline.SampleAt(chain, Spline.TotalLengthM(chain)).Direction;

    /// <summary>How much straight a chain has at one end, which is all a bend there may eat into.</summary>
    static float StraightAtTheEndM(ArcSeg[] chain, bool atFromEnd)
    {
        var end = atFromEnd ? chain[0] : chain[^1];
        return end.Curvature == 0f ? end.LengthM : 0f;
    }

    /// <summary>The chain with that much taken off the straight at one end, which is the ground the bend takes.</summary>
    static ArcSeg[] Trimmed(ArcSeg[] chain, bool atFromEnd, float byM)
    {
        var kept = new ArcSeg[chain.Length];
        Array.Copy(chain, kept, chain.Length);
        if (atFromEnd) kept[0] = kept[0] with { StartM = kept[0].PointAtM(byM), LengthM = kept[0].LengthM - byM };
        else kept[^1] = kept[^1] with { LengthM = kept[^1].LengthM - byM };

        return kept;
    }

    /// <summary>
    /// How tightly a road of this class may bend: the radius its own design speed affords on tarmac.
    /// <b>Derived and never authored</b> — a bend quoted in metres would be a figure nobody could check
    /// against the car that has to take it.
    /// </summary>
    public static float FloorRadiusM(SimConfig config, RoadClass roadClass) =>
        config.CarCorneringRadiusM(
            roadClass == RoadClass.Street
                ? config.CityGen.StreetDesignSpeedMps
                : config.CityGen.ArterialDesignSpeedMps,
            config.Terrain.PavedCoefficient);

    /// <summary>How much of each end of a road is one straight piece: everything a junction lays across an arm stands on it.</summary>
    public static float StubM(SimConfig config) => config.StraightStubM;

    /// <summary>
    /// <b>How far off its own chord a road is allowed to wander</b>: the block spacing of the district it
    /// runs through, at the share of a block that class of road is allowed (GEN-12). A grid's share is the
    /// tighter one, because a grid is straight.
    /// </summary>
    static float WanderM(Districts districts, LayoutEdge edge, Vector2 middleM, SimConfig config)
    {
        var districtAt = districts.At(middleM);
        var spacingM = districtAt < 0
            ? config.CityGen.BlockSpacingAlongMinM
            : districts[districtAt].BlockTightestM;
        var strict = districtAt >= 0 && districts[districtAt].Strict;
        return spacingM * (edge.Class == RoadClass.Arterial || strict
            ? config.CityGen.GridWanderInBlocks
            : config.CityGen.StreetWanderInBlocks);
    }

    /// <summary>
    /// <b>The furthest each road's drawn shape stands off the chord the layout joined it on</b>, road for
    /// road: what its own district allows a street to wander, or the sagitta of the arc the layout asked an
    /// arterial for. It is what the layout keeps two roads clear of each other by
    /// (<see cref="TownLayout.UnpickTheCrossings"/>, GEN-17).
    /// </summary>
    /// <remarks>
    /// <b>The bound the wander is clamped to, and not the wander itself.</b> What a road actually strays is
    /// drawn on this stage's own stream, which cannot run until the crossings are settled and the roads that
    /// are left are known — so what the layout is measured against is the most a road could take rather than
    /// the piece it went on to take. A street held straight by its own corner floor keeps ground it never
    /// uses, and that is the cheaper mistake of the two.
    /// </remarks>
    public static float[] StraysM(TownLayout layout, Districts districts, SimConfig config)
    {
        var straysM = new float[layout.Edges.Count];
        for (var road = 0; road < straysM.Length; road++)
        {
            var edge = layout.Edges[road];
            var fromM = layout.NodeM[edge.From];
            var toM = layout.NodeM[edge.To];
            straysM[road] = edge.Class == RoadClass.Street
                ? WanderM(districts, edge, (fromM + toM) * 0.5f, config)
                : SagittaM(edge.Curvature, (toM - fromM).Length());
        }

        return straysM;
    }

    /// <summary>How far an arc bulges off the chord it stands on, which is nil for a straight.</summary>
    static float SagittaM(float curvature, float chordM)
    {
        if (MathF.Abs(curvature) <= 0f) return 0f;

        var radiusM = 1f / MathF.Abs(curvature);
        var halfM = chordM * 0.5f;
        return radiusM - MathF.Sqrt(MathF.Max(0f, (radiusM * radiusM) - (halfM * halfM)));
    }

    static CityPlan.JunctionArrays Junctions(
        TownLayout layout, Vector2[] centreM, TownBrief brief, SimConfig config, float widthM, ref Rng draw)
    {
        var arms = layout.Arms();
        var radiusM = new float[centreM.Length];
        var lit = new bool[centreM.Length];
        var phaseOffsetS = new float[centreM.Length];

        for (var junction = 0; junction < centreM.Length; junction++)
        {
            radiusM[junction] = widthM * 0.5f;

            // <b>Only a junction that admits conflicting movements may be lit at all</b> (TLT-3), and a share
            // of those is left to the ranking instead (TER-5e) — drawn here, so a town lights the same way
            // every time it is opened and differently from the next town.
            lit[junction] = arms[junction] >= 3 && draw.NextFloat() >= brief.UnregulatedJunctionShare;
            phaseOffsetS[junction] = draw.NextFloat(0f, config.Signals.CycleS);
        }

        return new CityPlan.JunctionArrays
        {
            CentreM = centreM, RadiusM = radiusM, Lit = lit, PhaseOffsetS = phaseOffsetS,
        };
    }

    static CityPlan.RoadArrays Roads(TownLayout layout, ArcSeg[][] chains, float widthM)
    {
        var fromJunction = new int[chains.Length];
        var toJunction = new int[chains.Length];
        var widths = new float[chains.Length];
        var offsets = new int[chains.Length + 1];
        var segments = new List<ArcSeg>(chains.Length * 2);

        for (var road = 0; road < chains.Length; road++)
        {
            fromJunction[road] = layout.Edges[road].From;
            toJunction[road] = layout.Edges[road].To;
            widths[road] = widthM;
            offsets[road] = segments.Count;
            segments.AddRange(chains[road]);
        }

        offsets[^1] = segments.Count;
        return new CityPlan.RoadArrays
        {
            FromJunction = fromJunction, ToJunction = toJunction, WidthM = widths,
            SegmentOffsets = offsets, Segments = [.. segments],
        };
    }

    /// <summary>
    /// A deck for every bridge. <b>A bridge is a road rather than a stretch of one</b> (GEN-14a): it runs
    /// bridgehead to bridgehead, so the deck runs the whole road and reaches standable ground at both ends
    /// (TER-3b) rather than stopping where the water happened to.
    /// </summary>
    static CityPlan.BridgeArrays Bridges(
        TownLayout layout, ArcSeg[][] chains, SimConfig config, float widthM)
    {
        var road = new List<int>();
        var fromM = new List<float>();
        var toM = new List<float>();
        var deckWidthM = new List<float>();
        var pavementWidthM = new List<float>();

        for (var at = 0; at < chains.Length; at++)
        {
            if (chains[at].Length == 0 || layout.Edges[at].Class != RoadClass.Bridge) continue;

            road.Add(at);
            fromM.Add(0f);
            toM.Add(Spline.TotalLengthM(chains[at]));
            deckWidthM.Add(config.RoadFootprintM);
            pavementWidthM.Add(config.PavementWidthM);
        }

        return new CityPlan.BridgeArrays
        {
            Road = [.. road], FromM = [.. fromM], ToM = [.. toM],
            DeckWidthM = [.. deckWidthM], PavementWidthM = [.. pavementWidthM],
        };
    }

    /// <summary>
    /// The ground, in the order the strokes have to be laid: the pavement, the carriageway over it, the
    /// ground each junction's arms share, the kerb fillets, and the paint last of all
    /// (<see cref="GroundPainter"/>).
    /// </summary>
    /// <remarks>
    /// <b>It is the order and the shapes <c>GroundMesh</c> draws</b>, piece for piece: a band either side
    /// of every road and a disc round every node in pavement, the same two in carriageway over them, and a
    /// fillet at every corner. Classified any other way, a town is drawn one shape and walked another —
    /// tarmac under a drawn pavement at every junction, and grass under the walk that turns its corner.
    /// </remarks>
    static void Paint(
        GroundPainter painter, ArcSeg[][] chains, CityPlan.JunctionArrays junctions,
        Furniture.Laid furniture, SimConfig config, float widthM)
    {
        var halfM = widthM * 0.5f;
        var walkM = config.PavementWidthM;
        foreach (var chain in chains) painter.Verge(chain, halfM, halfM + walkM, Ground.Sidewalk);
        for (var junction = 0; junction < junctions.Count; junction++)
        {
            painter.Disc(junctions.CentreM[junction], junctions.RadiusM[junction] + walkM, Ground.Sidewalk);
        }

        foreach (var chain in chains) painter.Road(chain, widthM);

        for (var junction = 0; junction < junctions.Count; junction++)
        {
            painter.Disc(junctions.CentreM[junction], junctions.RadiusM[junction], Ground.Intersection);
        }

        for (var corner = 0; corner < furniture.Corners.Count; corner++)
        {
            painter.Fillet(
                furniture.Corners.CornerM[corner], furniture.Corners.TangentAM[corner],
                furniture.Corners.TangentBM[corner], furniture.Corners.ArcCentreM[corner],
                furniture.Corners.RadiusM[corner]);
        }

        // A crossing spans the road it is painted on and nothing narrower, which here is every road there
        // is (GEN-15).
        for (var crossing = 0; crossing < furniture.Crosswalks.Count; crossing++)
        {
            painter.Crossing(
                furniture.Crosswalks.CentreM[crossing], furniture.Crosswalks.Axis[crossing],
                furniture.Crosswalks.DepthM[crossing], widthM);
        }
    }

    public static float Facing(Vector2 unit) => MathF.Atan2(unit.Y, unit.X);

    static float Cross(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);
}
