using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Exam;

/// <summary>
/// <b>The scenario map</b>: a lattice of junctions with one traffic scenario staged at each, laid from
/// <see cref="ExamCards"/> and written out as a map like any other.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is being measured is a movement through a box</b>, so where every car stands and what else is
/// coming has to be <em>chosen</em> rather than found in a city and hoped for. A junction in Odesa is whatever
/// Odesa happens to hold; here it is a card, and the card says what the real rule makes of it.
/// </para>
/// <para>
/// <b>The paint and the walk are the town's own</b>: the pavement is struck off the driven ground and the
/// zebras are laid where three or more roads meet (TER-6, WLK-10), exactly as in a generated city. What the
/// plan lays that a generated town does not is the <b>lights and their bars</b>, on the junctions whose cards
/// are about lights — a town lays none of either (the known gaps), and a light with no bar is one nothing
/// stops at (<c>LaneFurniture.StopBars</c> reads the plan's).
/// </para>
/// <para>
/// <b>Laid in two passes because the paint is the pavement's.</b> Where a bar stands is behind the zebra on
/// its arm and where somebody waits to cross is at that zebra's kerb, and both are read off the kerb ends the
/// pavement comes out with (<see cref="KerbEnds"/>) — so the town is laid once without them, asked where its
/// zebras are, and written again with the pavement it was asked of handed over whole.
/// </para>
/// </remarks>
internal static class ExamPlan
{
    /// <summary>The map's catalogue name.</summary>
    public const string Name = "Exam";

    /// <summary>
    /// The map's own seed. <b>The one thing it draws is its arms' bearings</b> (TER-5d), which the town reads
    /// back off the plan by the same draw — so the lattice is laid with it and the plan carries it.
    /// </summary>
    public const ulong Seed = 0x6578616D_6C617474UL;

    public static CityPlan Lay(SimConfig config)
    {
        var lattice = ExamLattice.Of(config);
        var bare = Write(lattice, config, NoBars, NoSpawns, paving: null);
        var paving = bare.Paving(config);
        var ends = paving.RoadEnds(config);
        return Write(lattice, config, Bars(lattice, ends, config), Spawns(lattice, ends), paving);
    }

    static CityPlan Write(
        ExamLattice lattice, SimConfig config, CityPlan.StopLineArrays bars, CityPlan.SpawnArrays spawns,
        Paving? paving)
    {
        var ground = lattice.Ground;
        return new CityPlan
        {
            Seed = Seed,
            Name = Name,
            WorldSizeM = ground.WorldSizeM,
            PavementWidthM = config.PavementWidthM,
            Junctions = Nodes(ground, config),
            JunctionCorners = new CityPlan.JunctionCornerArrays
            {
                CornerM = [], ArcCentreM = [], RadiusM = [], TangentAM = [], TangentBM = [],
            },
            Roads = Streets(ground),
            Bridges = new CityPlan.BridgeArrays
            {
                Road = [], FromM = [], ToM = [], DeckWidthM = [], PavementWidthM = [],
            },
            Roundabouts = new CityPlan.RoundaboutArrays
            {
                RingOffsets = ground.RingOffsets.ToArray(), Road = ground.RingRoads.ToArray(),
            },
            PavedAreas = CityPlan.PavedAreaArrays.None,
            Crosswalks = new CityPlan.CrosswalkArrays { CentreM = [], Axis = [], DepthM = [], Road = [], Junction = [] },
            StopLines = bars,
            ParkingLots = new CityPlan.ParkingLotArrays
            {
                CentreM = [], Axis = [], HalfExtentM = [], SpaceOffsets = [0], SpacePositionM = [], SpaceHeadingRad = [],
            },
            Buildings = CityPlan.BuildingArrays.None,
            Props = new CityPlan.PropArrays { CentreM = [], RadiusM = [], BearingRad = [], Kind = [] },
            Spawns = spawns,
            Water = CityPlan.WaterArrays.None,
            PavingLaidWithIt = paving,
        };
    }

    static CityPlan.JunctionArrays Nodes(ExamGround ground, SimConfig config)
    {
        var centreM = new Vector2[ground.JunctionCount];
        var radiusM = new float[ground.JunctionCount];
        var lit = new bool[ground.JunctionCount];
        var phaseOffsetS = new float[ground.JunctionCount];
        for (var junction = 0; junction < ground.JunctionCount; junction++)
        {
            centreM[junction] = ground.JunctionM(junction);
            radiusM[junction] = config.JunctionRadiusM;
            lit[junction] = ground.Lit(junction);
            phaseOffsetS[junction] = ground.PhaseOffsetS(junction);
        }

        return new CityPlan.JunctionArrays { CentreM = centreM, RadiusM = radiusM, Lit = lit, PhaseOffsetS = phaseOffsetS };
    }

    static CityPlan.RoadArrays Streets(ExamGround ground)
    {
        var count = ground.Roads.Count;
        var fromJunction = new int[count];
        var toJunction = new int[count];
        var widthM = new float[count];
        var flow = new RoadFlow[count];
        var offsets = new int[count + 1];
        var segments = new List<ArcSeg>();
        for (var road = 0; road < count; road++)
        {
            var laid = ground.Roads[road];
            fromJunction[road] = laid.FromJunction;
            toJunction[road] = laid.ToJunction;
            widthM[road] = ground.WidthM(laid);
            flow[road] = laid.Flow;
            segments.AddRange(laid.Line);
            offsets[road + 1] = segments.Count;
        }

        return new CityPlan.RoadArrays
        {
            FromJunction = fromJunction, ToJunction = toJunction, WidthM = widthM, Flow = flow,
            SegmentOffsets = offsets, Segments = [.. segments],
        };
    }

    static CityPlan.StopLineArrays NoBars => new()
    {
        CentreM = [], Approach = [], SpanM = [], ThicknessM = [], Junction = [], Road = [],
    };

    static CityPlan.SpawnArrays NoSpawns => new() { Kind = [], PositionM = [], HeadingRad = [] };

    /// <summary>
    /// <b>A bar across every lane arriving at a lit junction</b>, where the town would paint it (TER-6): a
    /// setback clear of the near edge of the band the traffic is held behind at that arm
    /// (<see cref="KerbEnds.HeldM"/>), so what a car stops at on a red is the paint it can see.
    /// </summary>
    /// <remarks>
    /// The band's place along the arm, less half its depth, less the setback and half the bar, is where the bar
    /// stands, in the lane arriving there. A zebra that was not painted (<see cref="KerbNodes.Painted"/>) is a
    /// band of no depth, and the bar is held clear of the kerb end itself.
    /// </remarks>
    static CityPlan.StopLineArrays Bars(ExamLattice lattice, KerbEnds ends, SimConfig config)
    {
        var ground = lattice.Ground;
        var centreM = new List<Vector2>();
        var approach = new List<Vector2>();
        var spanM = new List<float>();
        var thicknessM = new List<float>();
        var junction = new List<int>();
        var road = new List<int>();

        for (var cell = 0; cell < ground.Cells; cell++)
        {
            var node = ground.Node(cell);
            if (node == ExamGround.NoRoad || !ground.Lit(node)) continue;

            for (var arm = 0; arm < 4; arm++)
            {
                var on = ground.ArmRoad(cell, (ExamArm)arm);
                if (on == ExamGround.NoRoad || !Arrives(ground.Roads[on], node)) continue;
                if (!Held(ends, ground.Roads[on], on, node, out var held)) continue;

                var depthM = held.Painted ? config.Road.CrossingDepthM : 0f;
                var outM = ground.OutAlongM(node, on, (held.NearM + held.FarM) * 0.5f)
                           + (depthM * 0.5f) + config.Road.StopBarSetbackM + (config.Road.StopBarThicknessM * 0.5f);
                var (barM, travel) = ground.OnTheLane(node, on, outM, arriving: true);

                centreM.Add(barM);
                approach.Add(travel);
                spanM.Add(config.LaneWidthM);
                thicknessM.Add(config.Road.StopBarThicknessM);
                junction.Add(node);
                road.Add(on);
            }
        }

        return new CityPlan.StopLineArrays
        {
            CentreM = [.. centreM], Approach = [.. approach], SpanM = [.. spanM], ThicknessM = [.. thicknessM],
            Junction = [.. junction], Road = [.. road],
        };
    }

    /// <summary>Whether traffic on a road can arrive at one of its two ends, which is its flow read from that end.</summary>
    static bool Arrives(in ExamRoad road, int junction) => road.Flow switch
    {
        RoadFlow.WithTheRoad => road.ToJunction == junction,
        RoadFlow.AgainstTheRoad => road.FromJunction == junction,
        _ => true,
    };

    /// <summary>The station the traffic arriving at one end of a road is held behind (<see cref="KerbEnds.HeldM"/>).</summary>
    static bool Held(KerbEnds ends, in ExamRoad laid, int road, int junction, out KerbNodes held) =>
        Station(ends.HeldM, road, laid.ToJunction == junction, out held);

    /// <summary>
    /// The station at one end of one road, among the kerb ends' answers — which are named by the road and the
    /// cut, and a road crossed once midway answers the one station at both of its ends (WLK-10a).
    /// </summary>
    public static bool Station(ReadOnlySpan<KerbNodes> stations, int road, bool atTo, out KerbNodes station)
    {
        foreach (var nodes in stations)
        {
            if (nodes.Road != road) continue;
            if (nodes.Cut == KerbCut.Midway || (nodes.Cut == KerbCut.AtTheFarEnd) == atTo)
            {
                station = nodes;
                return true;
            }
        }

        station = default;
        return false;
    }

    /// <summary>
    /// <b>Every car first and in card order</b>, so a card's drivers are a run of the fleet and a car's index
    /// says which card staged it; then the people, in the same order — which is the numbering
    /// <see cref="ExamLattice.WalkerOf"/> hands back.
    /// </summary>
    static CityPlan.SpawnArrays Spawns(ExamLattice lattice, KerbEnds ends)
    {
        var kind = new List<byte>();
        var positionM = new List<Vector2>();
        var headingRad = new List<float>();

        for (var card = 0; card < lattice.Cards; card++)
        {
            for (var driver = 0; driver < lattice.Card(card).Drivers.Length; driver++)
            {
                kind.Add(SpawnKindCar);
                positionM.Add(lattice.StandM(card, driver));
                headingRad.Add(lattice.StandHeadingRad(card, driver));
            }
        }

        for (var card = 0; card < lattice.Cards; card++)
        {
            for (var walker = 0; walker < lattice.Card(card).Walkers.Length; walker++)
            {
                if (!lattice.Kerbs(card, walker, ends, out var fromM, out var toM))
                {
                    throw new InvalidOperationException(
                        $"\"{lattice.Card(card).Name}\": walker {walker} is sent over a zebra the town did not paint.");
                }

                kind.Add(SpawnKindPerson);
                positionM.Add(fromM);
                headingRad.Add(ExamGround.Facing(toM - fromM));
            }
        }

        return new CityPlan.SpawnArrays { Kind = [.. kind], PositionM = [.. positionM], HeadingRad = [.. headingRad] };
    }

    const byte SpawnKindPerson = 0;

    const byte SpawnKindCar = 1;
}
