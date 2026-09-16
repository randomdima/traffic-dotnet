using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>The plan's own arrays, off the layout that stands</b>: every road as the curve it was laid as, the disc
/// at every junction, the decks and the rings.
/// </summary>
/// <remarks>
/// <b>Nothing is drawn here and nothing is refused here</b> (GEN-10). A road's line is drawn when the link is
/// offered and a link the line cannot be drawn for is never a road (<see cref="RoadLines"/>,
/// <see cref="TownLayout.Join"/>), so what is left for this stage is the one thing a line alone cannot say: a
/// one-way road stands on the half of the carriageway its traffic drives (TER-4d), which is settled once the
/// flows are (<see cref="OneWayStreets"/>).
/// </remarks>
internal static class RoadStage
{
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

    public static Laid Lay(TownLayout layout, SimConfig config, CarParks.Laid carParks)
    {
        // One lane's width for every road there is, arterial or street, and as many lanes as it is driven
        // ways (GEN-15, TER-4d). <b>Except a bay's own way</b>, which is one lane wide however it is driven:
        // a car goes in over it and comes back out over it (GEN-53, GEN-4f).
        var bay = Bays(layout, carParks);
        var widthM = new float[layout.Edges.Count];
        for (var road = 0; road < layout.Edges.Count; road++)
        {
            widthM[road] = bay.Length > 0 && bay[road]
                ? config.LaneWidthM
                : WidthM(config, layout.Edges[road].Flow);
        }

        // <b>A node's centre is the layout's and nothing moves it.</b> The connection points are drawn off
        // the two centres a link joins (<see cref="ConnectionPoints"/>) and are drawn again at derivation
        // time off the plan's, so a stage that nudged a node afterwards would be a stage that moved every
        // lane end round it.
        var centreM = new Vector2[layout.NodeM.Count];
        for (var node = 0; node < centreM.Length; node++) centreM[node] = layout.NodeM[node];

        // <b>Every road is already laid</b> (<see cref="RoadLines"/>, GEN-10): a link whose line could not be
        // drawn was never a road, so there is nothing here to refuse, repair or lay a second time.
        var chains = new ArcSeg[layout.Edges.Count][];
        for (var road = 0; road < chains.Length; road++) chains[road] = [.. layout.LineOf(road)];

        OntoTheDrivenHalf(layout, chains, widthM, config);

        return new Laid(
            Roads(layout, chains, widthM, CutRoads(layout, carParks), bay),
            Junctions(centreM, config, carParks),

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
            Rings(layout));
    }

    /// <summary>
    /// <b>Every one-way road moved onto the half of the carriageway its traffic drives</b> (TER-4d): its own
    /// half to the driving side, so its kerb there and its lane are the kerb and the lane a road of two ways
    /// has in that direction — a street that runs on into the next one rather than one that steps sideways
    /// into it. <b>The road's own width and never the catalogue's</b> (TER-4): what it is half of is what it
    /// was laid at.
    /// </summary>
    /// <remarks>
    /// <b>Once the flows are settled and never before</b> (<see cref="OneWayStreets"/>): which way a road runs
    /// is a fact about the whole town, and what is moved is the one line the road was laid as — so a road
    /// joined through the places it passes (GEN-51) is moved whole rather than in pieces that would come apart
    /// by the deflection between them. <b>Nothing moves a node</b>: the disc a junction is drawn on stands
    /// where the layout put it whatever its arms do (<see cref="Junctions"/>).
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

            var moved = new ArcSeg[chains[road].Length];
            Spline.OffsetInto(chains[road], DrivenHalfM(config, layout.Edges[road].Flow, widthM[road]), moved);
            chains[road] = moved;
        }
    }

    /// <summary>
    /// <b>How wide a road of this flow is laid</b> (GEN-15, TER-4d): one lane's width for every way it is
    /// driven, whether it is an arterial or a street.
    /// </summary>
    public static float WidthM(SimConfig config, RoadFlow flow) =>
        config.LaneWidthM * (flow == RoadFlow.BothWays ? SimConfig.LanesPerCarriageway : 1);

    /// <summary>
    /// <b>How far a road of this flow is moved onto the half of the carriageway its traffic drives</b>
    /// (TER-4d), to the right of the road's own direction: nothing where it is driven both ways, and half its
    /// width where the scatter took it one way (GEN-18).
    /// </summary>
    /// <remarks>
    /// <b>One site for it.</b> The stage moves the line by this and a cut reads off it which side of its node
    /// the lane a bay turns off actually runs (<see cref="SimConfig.CarParkBayLeadM"/>, GEN-53); two sites
    /// would be two answers to where the carriageway is.
    /// </remarks>
    public static float DrivenHalfM(SimConfig config, RoadFlow flow, float widthM) => flow switch
    {
        RoadFlow.BothWays => 0f,
        RoadFlow.WithTheRoad => widthM * 0.5f * config.RoadSideSign,
        _ => -widthM * 0.5f * config.RoadSideSign,
    };

    /// <summary>
    /// <b>The disc every junction is drawn on, which is the standoff its arms' lanes end at</b>
    /// (<see cref="SimConfig.JunctionRadiusM"/>, TER-5). One figure for every node the town laid, because
    /// the standoff is one figure: the disc follows the standoff and the arms follow the disc, and sizing it
    /// off the arms that end at the standoff would be a circle.
    /// </summary>
    /// <remarks>
    /// <b>Except a car park's, which is as long as its rank</b> (GEN-53,
    /// <see cref="SimConfig.CarParkStandoffM"/>): its bays hang off the node a lane apart along the street
    /// rather than radiating from it, so the street stands off the whole rank and the disc is that standoff.
    /// </remarks>
    static CityPlan.JunctionArrays Junctions(Vector2[] centreM, SimConfig config, CarParks.Laid carParks)
    {
        var radiusM = new float[centreM.Length];
        Array.Fill(radiusM, config.JunctionRadiusM);
        for (var carPark = 0; carPark < carParks.Junction.Length; carPark++)
        {
            radiusM[carParks.Junction[carPark]] = config.CarParkStandoffM(MostBaysOnASide(carParks, carPark));
        }

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

    /// <summary>The longer of one car park's two ranks, which is what its junction stands off (GEN-53).</summary>
    static int MostBaysOnASide(CarParks.Laid carParks, int carPark)
    {
        var right = 0;
        var left = 0;
        for (var bay = carParks.BayOffsets[carPark]; bay < carParks.BayOffsets[carPark + 1]; bay++)
        {
            if (carParks.Right[bay]) right++;
            else left++;
        }

        return Math.Max(right, left);
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
        var root = Clusters.Apart(layout.NodeM.Count);
        foreach (var edge in layout.Edges)
        {
            if (edge.Class == RoadClass.Roundabout) Clusters.Union(root, edge.From, edge.To);
        }

        var ringAt = new int[layout.NodeM.Count];
        Array.Fill(ringAt, -1);

        var road = new List<List<int>>();
        for (var edge = 0; edge < layout.Edges.Count; edge++)
        {
            if (layout.Edges[edge].Class != RoadClass.Roundabout) continue;

            var cluster = Clusters.Find(root, layout.Edges[edge].From);
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


    /// <summary>
    /// <b>Which roads the town's cuts left reading their arms off their own lines</b> (GEN-52), as the flag
    /// per road the plan carries — <b>empty where the town cut none</b>, which is every map that lays no car
    /// park.
    /// </summary>
    static bool[] CutRoads(TownLayout layout, CarParks.Laid carParks)
    {
        if (carParks.CutRoads.Length == 0) return [];

        var cut = new bool[layout.Edges.Count];
        foreach (var road in carParks.CutRoads) cut[road] = true;

        return cut;
    }

    /// <summary>
    /// <b>Which roads are a car park's bays</b> (GEN-53), as the flag per road the plan carries — <b>empty
    /// where the town lays none.</b>
    /// </summary>
    static bool[] Bays(TownLayout layout, CarParks.Laid carParks)
    {
        if (carParks.Road.Length == 0) return [];

        var bay = new bool[layout.Edges.Count];
        foreach (var road in carParks.Road) bay[road] = true;

        return bay;
    }

    static CityPlan.RoadArrays Roads(
        TownLayout layout, ArcSeg[][] chains, float[] widthM, bool[] cut, bool[] bay)
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
        var (throughOffsets, throughM) = Passes(layout);
        return new CityPlan.RoadArrays
        {
            FromJunction = fromJunction, ToJunction = toJunction, WidthM = widthM, Flow = flow,
            SegmentOffsets = offsets, Segments = [.. segments],
            ThroughOffsets = throughOffsets, ThroughM = [.. throughM],
            Cut = cut, Bay = bay,
        };
    }

    /// <summary>
    /// <b>The places every road of the layout passes</b>, flattened (GEN-51,
    /// <see cref="CityPlan.RoadArrays.ThroughOffsets"/>). <b>Empty where no road passes anywhere</b>, so a
    /// town whose junctions are all places roads meet carries no run at all.
    /// </summary>
    static (int[] Offsets, List<Vector2> PointsM) Passes(TownLayout layout)
    {
        var offsets = new int[layout.Edges.Count + 1];
        var pointsM = new List<Vector2>();
        for (var road = 0; road < layout.Edges.Count; road++)
        {
            offsets[road] = pointsM.Count;
            pointsM.AddRange(layout.Edges[road].ThroughM);
        }

        offsets[^1] = pointsM.Count;
        return pointsM.Count == 0 ? ([], pointsM) : (offsets, pointsM);
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
}
