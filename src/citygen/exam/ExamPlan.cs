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
/// <b>The paint, the bars and the walk are the town's own</b>: the pavement is struck off the driven ground,
/// and the zebras and the bars behind them are laid where three or more roads meet (TER-6, WLK-10), exactly as
/// in a generated city. <b>What the plan chooses is which junctions are lit</b> — the ones whose cards are
/// about lights, and no other, where a generated town draws a share of them (TLT-3).
/// </para>
/// <para>
/// <b>Laid in two passes because the paint is the pavement's.</b> Where somebody waits to cross is at a
/// zebra's kerb, which is read off the kerb ends the pavement comes out with (<see cref="KerbEnds"/>) — so the
/// town is laid once without its people, asked where its zebras are, and written again with the pavement it
/// was asked of handed over whole.
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
        var bare = Write(lattice, config, NoSpawns, paving: null);
        var paving = bare.Paving(config);
        return Write(lattice, config, Spawns(lattice, paving.RoadEnds(config)), paving);
    }

    static CityPlan Write(ExamLattice lattice, SimConfig config, CityPlan.SpawnArrays spawns, Paving? paving)
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

    static CityPlan.SpawnArrays NoSpawns => new() { Kind = [], PositionM = [], HeadingRad = [] };

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
