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
/// (GEN-47). The
/// traced cities are the argument: their streets are single arcs at the ninetieth percentile and their
/// median sinuosity is 1.000, so straight is what a street is and a bend is what it is allowed. Three
/// bounds hold, and each of them is a rule rather than a preference:
/// </para>
/// <list type="bullet">
/// <item><b>Both ends are straight.</b> A junction's own ground, the corner an arm flares back to, the
/// crossing and the bar behind it are all laid across a straight arm, so the first and last stretch of every
/// road is one straight piece and the wander lives between them. <b>An end at a node that forks nothing is
/// the exception: the two arms there are swept into one arc, and what that arm carries is laid
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
    /// <summary>
    /// How much of a segment a rounded corner may eat, either side of the vertex it rounds. <b>It is the
    /// same share the jitter bound is derived against</b> (<see cref="ConnectionPoints"/>): a corner allowed
    /// more ground here than the bound assumed is a road that turns off its arm where the arm was drawn not
    /// to let it.
    /// </summary>
    const float TangentShareOfSegment = ConnectionPoints.TangentShareOfSegment;

    /// <summary>
    /// The stream a round of the laying draws its wander from. <b>A round of its own</b>, so that a town
    /// whose second pass lays fewer roads does not draw the first pass's numbers for them.
    /// </summary>
    const ulong ShapeRoundStream = 0x7368_6170_655F_7200;


    /// <summary>Below this deflection a vertex is straight through and is not rounded at all.</summary>
    const float StraightThroughRad = 0.002f;

    /// <summary>
    /// <b>How open a joint may read and still be one line</b>: the angle that moves the line laid furthest
    /// off a carriageway by the rounding two computations of one distance disagree by
    /// (<see cref="LineTolerance.RoundingM"/>). It is float slop over a chain of arcs and nothing else; a
    /// road that really creases does so by tenths of a radian.
    /// </summary>
    /// <remarks>
    /// <b>The furthest line off a carriageway is a lane's own</b> (<see cref="SimConfig.LaneOffsetM"/>). It
    /// used to be the pavement's, laid half a walk outside the kerb — and nothing lays a pavement, so the
    /// figure follows the line that is actually there rather than the one it was calibrated against.
    /// </remarks>
    public static float CreaseRad(SimConfig config) => LineTolerance.RoundingM / config.LaneOffsetM;

    internal readonly record struct Laid(
        CityPlan.RoadArrays Roads,
        CityPlan.JunctionArrays Junctions,
        CityPlan.JunctionCornerArrays Corners,
        CityPlan.CrosswalkArrays Crosswalks,
        CityPlan.StopLineArrays StopLines,
        CityPlan.BridgeArrays Bridges,
        CityPlan.RoundaboutArrays Roundabouts);

    public static Laid Lay(
        TownLayout layout, Districts districts, TownBrief brief, SimConfig config, ref Rng shape)
    {
        var seed = brief.Seed;

        // One lane's width for every road there is, arterial or street, and as many lanes as it is driven
        // ways (GEN-15, TER-4d).
        var widthM = new float[layout.Edges.Count];
        for (var road = 0; road < layout.Edges.Count; road++)
        {
            widthM[road] = config.LaneWidthM
                           * (layout.Edges[road].Flow == RoadFlow.BothWays ? SimConfig.LanesPerCarriageway : 1);
        }

        // <b>A node's centre is the layout's and nothing moves it.</b> The connection points are drawn off
        // the two centres a link joins (<see cref="ConnectionPoints"/>) and are drawn again at derivation
        // time off the plan's, so a stage that nudged a node afterwards would be a stage that moved every
        // lane end round it.
        var centreM = new Vector2[layout.NodeM.Count];
        for (var node = 0; node < centreM.Length; node++) centreM[node] = layout.NodeM[node];

        // <b>Lay, refuse, repair, lay again</b> (§6.8). A link the spline cannot satisfy and a pair of
        // roads that would share ground are both deletions, and a deletion behind the layout's own repairs
        // can strand a component or leave a junction nothing reaches (GEN-5) — so the repairs run again
        // behind the laying, and the laying again behind them, until nothing is refused.
        ArcSeg[][] chains;
        CityPlan.RoundaboutArrays rings;
        for (var round = 0; ; round++)
        {
            rings = Rings(layout);
            var arms = Skeleton(seed, layout, centreM, widthM, rings, config);

            chains = new ArcSeg[layout.Edges.Count][];
            var laying = new Rng(seed, ShapeRoundStream ^ (ulong)round);
            for (var road = 0; road < layout.Edges.Count; road++)
            {
                chains[road] = Chain(arms, layout, districts, road, config, ref laying);
            }

            // <b>Every round is a strict deletion</b>, so the sequence ends on its own: a town whose every
            // link is refused ends with no links, and one that refuses none ends here.
            if (!Refused(layout, chains, config, out var refused)) break;


            layout.DropTheRoads(refused);
            layout.KeepTheLargestComponent();
            layout.PruneTheDeadEnds();

            centreM = new Vector2[layout.NodeM.Count];
            for (var node = 0; node < centreM.Length; node++) centreM[node] = layout.NodeM[node];

            widthM = new float[layout.Edges.Count];
            for (var road = 0; road < layout.Edges.Count; road++)
            {
                widthM[road] = config.LaneWidthM
                               * (layout.Edges[road].Flow == RoadFlow.BothWays ? SimConfig.LanesPerCarriageway : 1);
            }
        }

        OntoTheDrivenHalf(layout, chains, widthM, config);

        var junctions = Junctions(centreM, config);

        return new Laid(
            Roads(layout, chains, widthM),
            junctions,

            // <b>A junction turns no kerb corner, strikes no crossing and paints no bar.</b> A fillet is
            // kerb geometry and the kerb is not laid here any more; the crossings and the bars come back
            // with it (TER-6).
            new CityPlan.JunctionCornerArrays
            {
                CornerM = [], ArcCentreM = [], RadiusM = [], TangentAM = [], TangentBM = [],
            },
            new CityPlan.CrosswalkArrays { CentreM = [], Axis = [], DepthM = [], Road = [], Junction = [] },
            new CityPlan.StopLineArrays
            {
                CentreM = [], Approach = [], SpanM = [], ThicknessM = [], Junction = [], Road = [],
            },
            Bridges(layout, chains, config),
            rings);
    }

    /// <summary>
    /// <b>Which roads this town cannot lay</b> (§4.4): the ones whose spline could not meet both of its
    /// drawn bearings, and the lower-ranked of every pair that would share ground with a road it does not
    /// meet at a node (GEN-49).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the drawn lines that are asked and never their chords.</b> A spline free to reach its own
    /// end bearings is not bounded by the chord between them, so a separation measured on chords says
    /// nothing about the roads that were laid — which is why the rule it replaces is deleted rather than
    /// restated.
    /// </para>
    /// <para>
    /// <b>Two roads that share a node are left alone.</b> They touch there because that is what a junction
    /// is, and how square they have to stand to each other is the layout's (GEN-13).
    /// </para>
    /// </remarks>
    static bool Refused(TownLayout layout, ArcSeg[][] chains, SimConfig config, out bool[] refused)
    {
        refused = new bool[chains.Length];
        var any = false;

        var building = new ChainIndex.Builder();
        for (var road = 0; road < chains.Length; road++)
        {
            if (chains[road].Length == 0)
            {
                refused[road] = true;
                any = true;
                continue;
            }

            building.Add(road, chains[road], Spline.TotalLengthM(chains[road]));
        }

        var index = building.Seal(config.NearestChainCellM);
        var apartM = config.RoadFootprintM;

        Span<int> near = stackalloc int[MostRoadsNear];
        Span<SplineCrossing> crossings = stackalloc SplineCrossing[MostCrossings];
        for (var road = 0; road < chains.Length; road++)
        {
            if (refused[road]) continue;

            var found = index.Crossing(chains[road], apartM, near);
            for (var at = 0; at < Math.Min(found, near.Length); at++)
            {
                var other = near[at];
                if (other == road || refused[other] || SharesANode(layout, road, other)) continue;

                // The lower-ranked road is the one that goes: an arterial keeps its ground against a street
                // and a street against a spoke, which is the order the layout offered them in.
                if (Precedence(layout.Edges[road].Class) > Precedence(layout.Edges[other].Class)) continue;

                if (Spline.CrossingsM(chains[road], chains[other], 0f, 0f, crossings) == 0
                    && !PassesInside(chains[road], chains[other], apartM))
                {
                    continue;
                }

                refused[road] = true;
                any = true;
                break;
            }
        }

        return any;
    }

    /// <summary>
    /// <b>Whether two roads pass closer than the ground they take</b>, which is a road's whole width and the
    /// walk either side of it (<see cref="SimConfig.RoadFootprintM"/>). Walked at the tolerance a road is
    /// laid to rather than solved: two curves' closest approach has no closed form, and what is being asked
    /// is whether their bands overlap at all.
    /// </summary>
    static bool PassesInside(ReadOnlySpan<ArcSeg> one, ReadOnlySpan<ArcSeg> other, float apartM)
    {
        var lengthM = Spline.TotalLengthM(one);
        var otherLengthM = Spline.TotalLengthM(other);
        var stations = Math.Max(1, (int)MathF.Ceiling(lengthM / apartM));

        for (var station = 0; station <= stations; station++)
        {
            var atM = Spline.SampleAt(one, lengthM * station / stations).PositionM;
            var onM = Spline.ProjectM(other, atM, otherLengthM * 0.5f, otherLengthM);
            if (Vector2.Distance(Spline.SampleAt(other, onM).PositionM, atM) < apartM) return true;
        }

        return false;
    }

    /// <summary>How many roads may pass near one before the index's answer stops being the whole one.</summary>
    const int MostRoadsNear = 64;

    /// <summary>And how many places two of them may cross before the pair is plainly one road over another.</summary>
    const int MostCrossings = 4;

    static bool SharesANode(TownLayout layout, int road, int other)
    {
        var one = layout.Edges[road];
        var two = layout.Edges[other];
        return one.From == two.From || one.From == two.To || one.To == two.From || one.To == two.To;
    }

    /// <summary>Which of two roads the other gives way to where a refusal has to choose between them.</summary>
    static int Precedence(RoadClass roadClass) => roadClass switch
    {
        RoadClass.Roundabout => 3,
        RoadClass.Bridge => 3,
        RoadClass.Arterial => 2,
        RoadClass.Street => 1,
        _ => 0,
    };

    /// <summary>
    /// <b>The ground a road is laid on before it has a shape</b>: the junctions at the layout's own centres,
    /// every road as its two ends and how wide it is, which of them are bridges and which circulate.
    /// <see cref="ConnectionPoints"/> asks for nothing else, and nothing else is settled yet.
    /// </summary>
    static GroundPieces Skeleton(
        ulong seed, TownLayout layout, Vector2[] centreM, float[] widthM, CityPlan.RoundaboutArrays rings,
        SimConfig config)
    {
        var roads = layout.Edges.Count;
        var fromJunction = new int[roads];
        var toJunction = new int[roads];
        var flow = new RoadFlow[roads];
        var bridge = new List<int>();
        for (var road = 0; road < roads; road++)
        {
            fromJunction[road] = layout.Edges[road].From;
            toJunction[road] = layout.Edges[road].To;
            flow[road] = layout.Edges[road].Flow;
            if (layout.Edges[road].Class == RoadClass.Bridge) bridge.Add(road);
        }

        var bare = GroundPieces.None(seed, Vector2.Zero, config.PavementWidthM);
        return bare with
        {
            Junctions = new CityPlan.JunctionArrays
            {
                CentreM = centreM,
                RadiusM = new float[centreM.Length],
                Lit = new bool[centreM.Length],
                PhaseOffsetS = new float[centreM.Length],
            },
            Roads = new CityPlan.RoadArrays
            {
                FromJunction = fromJunction, ToJunction = toJunction, WidthM = widthM, Flow = flow,
                SegmentOffsets = new int[roads + 1], Segments = [],
            },
            Bridges = new CityPlan.BridgeArrays
            {
                Road = [.. bridge],
                FromM = new float[bridge.Count],
                ToM = new float[bridge.Count],
                DeckWidthM = new float[bridge.Count],
                PavementWidthM = new float[bridge.Count],
            },
            Roundabouts = rings,
        };
    }

    /// <summary>
    /// <b>One road's own curve, laid to arrive on the bearings its two ends were drawn with</b> (TER-5d).
    /// It runs from the stand point of one arm to the stand point of the other, holds each arm's own
    /// bearing for the lead it is given, and wanders between them by as much as its district allows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the inversion.</b> The road used to be a chord that wandered and the lanes were what was
    /// left of it once the junction discs had bitten; here the ends are drawn first and the road is the
    /// thing that has to satisfy them, which is what TER-5d described all along.
    /// </para>
    /// <para>
    /// <b>The bearing is held by two points and not by a special case</b>: a lead laid along the arm puts
    /// the polyline's first leg on the arm's own line, so the chain <see cref="Rounded"/> lays off it
    /// begins as a straight on that bearing and ends as one arriving on the other. A road whose first or
    /// last corner cannot be turned inside its class's floor is one <see cref="Rounded"/> gives a vertex up
    /// on, and what comes back then does not meet its bearings — so it is refused, and the layout is
    /// repaired behind the refusal rather than left a piece short.
    /// </para>
    /// </remarks>
    static ArcSeg[] Chain(
        GroundPieces arms, TownLayout layout, Districts districts, int road, SimConfig config, ref Rng draw)
    {
        var edge = layout.Edges[road];
        var from = ConnectionPoints.ArmOf(arms, config, road, atFrom: true);
        var to = ConnectionPoints.ArmOf(arms, config, road, atFrom: false);

        var betweenM = Vector2.Distance(from.StandM, to.StandM);
        if (betweenM <= 0f) return [];

        // <b>A ring piece is one arc of one circle</b> (GEN-19) and is not laid to anything: both its arms
        // were held to the circle's own tangents, so the piece between their stand points is the circle.
        if (MathF.Abs(from.Curvature) > 0f)
        {
            return [Arc(from.StandM, (to.StandM - from.StandM) / betweenM, betweenM, from.Curvature)];
        }


        // <b>A road is the biarc between its two end poses</b>, wandering through whatever via points its
        // district affords. A biarc is tangent to both poses by construction and to itself at its joint, so
        // the bearings are met exactly rather than approached — and what it costs is a curvature nobody
        // chose, which is measured below and refused where the class cannot hold it.
        var floorM = FloorRadiusM(config, edge.Class);
        var wanderNodes = WanderNodes(districts, edge, from.NodeM, to.NodeM, config, ref draw);

        Span<Vector2> pointsM = stackalloc Vector2[wanderNodes + 2];
        pointsM[0] = from.StandM;
        pointsM[^1] = to.StandM;
        Wander(pointsM, districts, edge, wanderNodes, config, ref draw);

        // <b>Nothing bends tighter than its own class's floor</b>, which is what the design speed affords on
        // tarmac. A road that asks for one is straightened before it is refused — the wander is what it
        // wanted and the bearings are what it owes, so the wander is what gives way (GEN-10: the answer is
        // deletion and never a retry over the whole town).
        for (var wandering = wanderNodes; wandering >= 0; wandering--)
        {
            var chain = Biarcs(pointsM[..(wandering + 1)], to.StandM, from.StandUnit, -to.StandUnit);
            if (chain.Length > 0 && TightestRadiusM(chain) >= floorM) return chain;
        }

        return [];
    }

    /// <summary>
    /// <b>The chain of biarcs through a polyline, leaving and arriving on two given bearings.</b> Every
    /// interior point is passed on the line through its neighbours, so the pieces either side of it are
    /// tangent there and the whole chain is one line a follower can read.
    /// </summary>
    static ArcSeg[] Biarcs(ReadOnlySpan<Vector2> throughM, Vector2 lastM, Vector2 leaves, Vector2 arrives)
    {
        var chain = new List<ArcSeg>(throughM.Length * 2);
        Span<ArcSeg> drawn = stackalloc ArcSeg[2];

        var at = leaves;
        for (var piece = 0; piece < throughM.Length; piece++)
        {
            var ontoM = piece + 1 < throughM.Length ? throughM[piece + 1] : lastM;
            var onward = piece + 2 < throughM.Length
                ? Through(throughM[piece], throughM[piece + 1], throughM[piece + 2])
                : piece + 2 == throughM.Length ? Through(throughM[piece], throughM[piece + 1], lastM)
                : arrives;

            // <b>Both halves are kept, even where they read as one arc.</b> Rubbing the joint out moves the
            // line by the rounding the merge allows, and a road is read as one line to a far finer angle
            // than that (<see cref="CreaseRad"/>) — so a chain that was tangent by construction comes back
            // creased by the tidying.
            var laid = Spline.BiarcInto(throughM[piece], Facing(at), ontoM, Facing(onward), drawn);
            if (laid == 0) return [];

            // <b>Two straights in a line are one straight</b>, which is what a bridge's span is and what a
            // road drawn between two arms that agree comes to. Nothing else is rubbed out: a merge over two
            // arcs that merely read alike moves the line by the tolerance it allowed, and a road is read as
            // one line to a far finer angle than that (<see cref="CreaseRad"/>).
            if (laid == 2 && drawn[0].Curvature == 0f && drawn[1].Curvature == 0f)
            {
                chain.Add(new ArcSeg(
                    drawn[0].StartM, drawn[0].HeadingRad, drawn[0].LengthM + drawn[1].LengthM, 0f));
            }
            else
            {
                for (var arc = 0; arc < laid; arc++) chain.Add(drawn[arc]);
            }

            at = onward;
        }

        return [.. chain];
    }

    /// <summary>The bearing a line passes an interior point on: the direction its two neighbours leave it in.</summary>
    static Vector2 Through(Vector2 beforeM, Vector2 atM, Vector2 afterM)
    {
        var run = afterM - beforeM;
        return run.LengthSquared() > 0f ? Vector2.Normalize(run) : Vector2.UnitX;
    }

    /// <summary>The tightest circle anywhere in a chain, which is the whole question about what a car can hold on it.</summary>
    static float TightestRadiusM(ReadOnlySpan<ArcSeg> arcs)
    {
        var bend = 0f;
        foreach (var arc in arcs) bend = MathF.Max(bend, MathF.Abs(arc.Curvature));

        return bend <= 1e-6f ? float.PositiveInfinity : 1f / bend;
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
    /// The points a wandering street is drawn through: <b>the two ends of its leads, and its virtual nodes
    /// standing off the line between them</b>. The offset is clamped so that the corner it makes cannot be
    /// tighter than the class's floor — a street asked to wander further than its own length can turn
    /// through is straightened rather than bent past what a car could take.
    /// </summary>
    /// <remarks>
    /// <b>The ends are given and not drawn.</b> They are where the arms' own leads finish, so what is drawn
    /// here is the middle alone — which is the only part of a road this stage is still free to choose.
    /// </remarks>
    static void Wander(
        Span<Vector2> pointsM, Districts districts, LayoutEdge edge, int nodes, SimConfig config, ref Rng draw)
    {
        if (nodes == 0) return;

        var fromM = pointsM[0];
        var toM = pointsM[^1];
        var acrossM = toM - fromM;
        var lengthM = acrossM.Length();
        if (lengthM <= 0f) return;

        var unit = acrossM / lengthM;
        var side = Heading.RightOf(unit);
        var segmentM = lengthM / (nodes + 1);
        var floorM = FloorRadiusM(config, edge.Class);

        // <b>Bounded by the block and by the bend together</b>: a street may not stray so far off its own
        // line that it could meet the street a block over, and it may not ask for a corner its class cannot
        // turn — which over a segment of its own length is the sagitta that radius affords.
        var wanderM = MathF.Min(
            WanderM(districts, edge, (fromM + toM) * 0.5f, config),
            TangentShareOfSegment * segmentM * segmentM / MathF.Max(floorM, 1f));

        for (var node = 0; node < nodes; node++)
        {
            var alongM = segmentM * (node + 1);
            pointsM[node + 1] = fromM + (unit * alongM) + (side * draw.NextFloat(-wanderM, wanderM));
        }
    }

    static int WanderNodes(
        Districts districts, LayoutEdge edge, Vector2 fromM, Vector2 toM, SimConfig config, ref Rng draw)
    {
        // A spoke is straight because the layout reads it as a ray: everything that asks whether a point
        // stands clear of an arterial asks it of a line through the hub.
        if (edge.Class != RoadClass.Street) return 0;

        // <b>A strict district wanders through at most one virtual node and a loose one through the town's
        // own bound</b> (<see cref="CityGenFigures.WanderNodesMost"/>), which is what makes a strict
        // district's streets read as near-chords and a loose one's as curves.
        var most = config.CityGen.WanderNodesMost;
        var districtAt = districts.At((fromM + toM) * 0.5f);
        var strict = districtAt < 0 || districts[districtAt].Strict;
        return strict ? draw.NextInt(2) : 1 + draw.NextInt(most);
    }

    /// <summary>
    /// <b>Every one-way road moved onto the half of the carriageway its traffic drives</b> (TER-4d): its own
    /// half to the driving side, so its kerb there and its lane are the kerb and the lane a road of two ways
    /// has in that direction — a street that runs on into the next one rather than one that steps sideways
    /// into it. <b>The road's own width and never the catalogue's</b> (TER-4): what it is half of is what it
    /// was laid at.
    /// </summary>
    /// <remarks>
    /// <b>After the bends and never before them</b> (<see cref="Bends"/>): two arms swept onto one tangent
    /// are still met once both are moved to the same side of the travel they share, where two merely joined
    /// at a node would come apart by the deflection between them. <b>A node its own two arms are all of goes
    /// with them</b> — the disc a junction is drawn on belongs on the road rather than beside it, and a node
    /// with a fork in it keeps the place the layout put it whatever its arms do (<see cref="Junctions"/>).
    /// <para>
    /// <b>Except a roundabout's ring, which is the whole of its own corridor</b> (GEN-19). A scattered
    /// one-way street is half of the two ways it was laid as and belongs on the half it is driven; a ring
    /// was laid one way round the circle <see cref="Roundabouts.RadiusM"/> sized, and moved half a lane off
    /// it the carriageway leaves its own nodes — the arms then end on its far kerb, which stands exactly half
    /// a walk from the line the pavement round the island runs down, and whether that pavement exists at each
    /// entry comes down to the last bits of a float.
    /// </para>
    /// </remarks>
    static void OntoTheDrivenHalf(TownLayout layout, ArcSeg[][] chains, float[] widthM, SimConfig config)
    {
        for (var road = 0; road < chains.Length; road++)
        {
            if (chains[road].Length == 0
                || layout.Edges[road].Flow == RoadFlow.BothWays
                || layout.Edges[road].Class == RoadClass.Roundabout)
            {
                continue;
            }

            var halfM = widthM[road] * 0.5f * config.RoadSideSign;
            var moved = new ArcSeg[chains[road].Length];
            Spline.OffsetInto(
                chains[road], layout.Edges[road].Flow == RoadFlow.WithTheRoad ? halfM : -halfM, moved);
            chains[road] = moved;
        }
    }

    /// <summary>
    /// How tightly a road of this class may bend: the radius its own design speed affords on tarmac.
    /// <b>Derived and never authored</b> — a bend quoted in metres would be a figure nobody could check
    /// against the car that has to take it.
    /// </summary>
    public static float FloorRadiusM(SimConfig config, RoadClass roadClass) =>
        config.CarCorneringRadiusM(
            roadClass switch
            {
                RoadClass.Street => config.CityGen.StreetDesignSpeedMps,
                RoadClass.Roundabout => config.CityGen.RoundaboutDesignSpeedMps,
                _ => config.CityGen.ArterialDesignSpeedMps,
            },
            config.Terrain.PavedCoefficient);

    /// <summary>How much of each end of a road is one straight piece: everything a junction lays across an arm stands on it.</summary>
    public static float StubM(SimConfig config) => config.StraightStubM;

    /// <summary>
    /// <b>How far off its own chord a road is allowed to wander</b>: the block spacing of the district it
    /// runs through, at the share of a block that class of road is allowed (GEN-47). A grid's share is the
    /// tighter one, because a grid is straight.
    /// </summary>
    public static float WanderM(Districts districts, LayoutEdge edge, Vector2 middleM, SimConfig config)
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
    /// <b>The disc every junction is drawn on, which is the standoff its arms' lanes end at</b>
    /// (<see cref="SimConfig.JunctionRadiusM"/>, TER-5). One figure for every node in the town, because the
    /// standoff is one figure: the disc follows the standoff and the arms follow the disc, and sizing it off
    /// the arms that end at the standoff would be a circle.
    /// </summary>
    static CityPlan.JunctionArrays Junctions(Vector2[] centreM, SimConfig config)
    {
        var radiusM = new float[centreM.Length];
        Array.Fill(radiusM, config.JunctionRadiusM);

        // <b>Nothing is lit.</b> Whether a junction carries a timetable was drawn here, in a stream of its
        // own, and the signals come back with the crossings and the bars they order (TLT-3) — so what the
        // plan carries is a town of junctions that all rank their movements (TER-5e).
        return new CityPlan.JunctionArrays
        {
            CentreM = centreM,
            RadiusM = radiusM,
            Lit = new bool[centreM.Length],
            PhaseOffsetS = new float[centreM.Length],
        };
    }

    /// <summary>
    /// <b>The town's roundabouts, as which roads each one's ring is made of</b> (GEN-19) — the rings picked
    /// out of the finished road list by the nodes they share, so a plan says which of its one-way roads are
    /// somebody circulating and which are a street the scatter took (GEN-18).
    /// </summary>
    /// <remarks>
    /// <b>Membership and no geometry.</b> Where a ring stands and how wide it is are its arcs' to say, and a
    /// centre carried beside them would be a second answer that goes stale the moment either is laid again.
    /// </remarks>
    static CityPlan.RoundaboutArrays Rings(TownLayout layout)
    {
        var root = new int[layout.NodeM.Count];
        for (var node = 0; node < root.Length; node++) root[node] = node;
        foreach (var edge in layout.Edges)
        {
            if (edge.Class == RoadClass.Roundabout) Union(root, edge.From, edge.To);
        }

        var ringAt = new int[layout.NodeM.Count];
        Array.Fill(ringAt, -1);

        var road = new List<List<int>>();
        for (var edge = 0; edge < layout.Edges.Count; edge++)
        {
            if (layout.Edges[edge].Class != RoadClass.Roundabout) continue;

            var cluster = Find(root, layout.Edges[edge].From);
            if (ringAt[cluster] < 0)
            {
                ringAt[cluster] = road.Count;
                road.Add([]);
            }

            road[ringAt[cluster]].Add(edge);
        }

        var offsets = new int[road.Count + 1];
        var flat = new List<int>();
        for (var ring = 0; ring < road.Count; ring++)
        {
            offsets[ring] = flat.Count;
            flat.AddRange(road[ring]);
        }

        offsets[^1] = flat.Count;
        return new CityPlan.RoundaboutArrays { RingOffsets = offsets, Road = [.. flat] };
    }

    static int Find(int[] root, int node)
    {
        while (root[node] != node)
        {
            root[node] = root[root[node]];
            node = root[node];
        }

        return node;
    }

    static void Union(int[] root, int a, int b)
    {
        a = Find(root, a);
        b = Find(root, b);
        if (a != b) root[b] = a;
    }

    static CityPlan.RoadArrays Roads(TownLayout layout, ArcSeg[][] chains, float[] widthM)
    {
        var fromJunction = new int[chains.Length];
        var toJunction = new int[chains.Length];
        var flow = new RoadFlow[chains.Length];
        var offsets = new int[chains.Length + 1];
        var segments = new List<ArcSeg>(chains.Length * 2);

        for (var road = 0; road < chains.Length; road++)
        {
            fromJunction[road] = layout.Edges[road].From;
            toJunction[road] = layout.Edges[road].To;
            flow[road] = layout.Edges[road].Flow;
            offsets[road] = segments.Count;
            segments.AddRange(chains[road]);
        }

        offsets[^1] = segments.Count;
        return new CityPlan.RoadArrays
        {
            FromJunction = fromJunction, ToJunction = toJunction, WidthM = widthM, Flow = flow,
            SegmentOffsets = offsets, Segments = [.. segments],
        };
    }

    /// <summary>
    /// A deck for every bridge. <b>A bridge is a road rather than a stretch of one</b> (GEN-14a): it runs
    /// bridgehead to bridgehead, so the deck runs the whole road and reaches standable ground at both ends
    /// (TER-3b) rather than stopping where the water happened to.
    /// </summary>
    static CityPlan.BridgeArrays Bridges(TownLayout layout, ArcSeg[][] chains, SimConfig config)
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

    public static float Facing(Vector2 unit) => MathF.Atan2(unit.Y, unit.X);
}
