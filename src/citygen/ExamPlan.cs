using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The driving exam</b>: a six by six lattice of junctions with one crossing manoeuvre staged at each
/// of them, laid from <see cref="ExamCards"/> and written out as a map like any other.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the second map this build lays itself</b> and it is laid for the same reason the proving ground
/// is (<see cref="TrackPlan"/>): what is being measured is a movement through a box, so where every car
/// stands and what else is coming has to be <em>chosen</em> rather than found in a city and hoped for. A
/// junction in Odesa is whatever Odesa happens to hold; here it is a card.
/// </para>
/// <para>
/// <b>One make of car and one build of it.</b> Every car on the map is the nominal car (CAR-11a) wearing
/// one ordinary look, because a card compares one crossing against another and a fleet of different weights
/// and drivetrains would put a second variable inside every comparison. That the whole map is one look is
/// a fact about the map's <em>name</em> where the town stands its fleet up (<see cref="StandsOneLook"/>),
/// since a plan may not read the car catalogue — and the look is an ordinary one rather than the police
/// livery, because in this town a police look is what a police car <em>is</em> (SRV-2, SRV-5) and every one
/// of them belongs to a station.
/// </para>
/// <para>
/// <b>Paint on every arm, and a body at four of them.</b> A crossing on every arm of every junction is
/// the rule a generated town is laid to (TER-6) and it is the rule here, because a block whose pavement
/// nobody may leave is a walking network of islands. What a card picks is therefore not where the paint
/// is but which crossing somebody is standing at, and the four cards about paint are the four crossings
/// that have anybody on them.
/// </para>
/// </remarks>
internal static class ExamPlan
{
    /// <summary>The map's catalogue name, which is also its file's.</summary>
    public const string Name = "Exam";

    /// <summary>
    /// Whether the cars on this map are one look and one build rather than the fleet as it ships — asked of
    /// the map's name, because the name is all a town has to go on when it stands its cars up, and the look
    /// itself belongs to a catalogue this folder may not read.
    /// </summary>
    public static bool StandsOneLook(string name) => string.Equals(name, Name, StringComparison.Ordinal);

    const float CellSizeM = ExamLattice.CellSizeM;

    /// <summary>Where the exam's own randomness comes from. Nothing on this map is drawn, so it is a constant and says so.</summary>
    const ulong Seed = 0x6578616D_6A756E63UL;

    public static Vector2 WorldSizeM(SimConfig config) => ExamLattice.Of(config).WorldSizeM;

    public static CityPlan Lay(SimConfig config)
    {
        var lattice = ExamLattice.Of(config);
        var worldSizeM = lattice.WorldSizeM;
        var gridWidth = (int)MathF.Round(worldSizeM.X / CellSizeM);
        var gridHeight = (int)MathF.Round(worldSizeM.Y / CellSizeM);
        var cells = new Ground[gridWidth * gridHeight];
        var laneDirs = new sbyte[cells.Length * 2];
        var widthM = config.RoadWidthM;
        var walkM = config.Road.PavementWidthM;

        var segments = new ArcSeg[lattice.Roads.Count];
        for (var road = 0; road < lattice.Roads.Count; road++)
        {
            var run = lattice.Roads[road].ToM - lattice.Roads[road].FromM;
            segments[road] = new ArcSeg(
                lattice.Roads[road].FromM, ExamLattice.Facing(run), run.Length(), 0f);
        }

        var painter = new GroundPainter(cells, laneDirs, gridWidth, gridHeight, CellSizeM, config.RoadSideSign);
        Paint(painter, lattice, segments, config, widthM, walkM);

        var corners = Corners(lattice, config, widthM);
        return new CityPlan
        {
            Seed = Seed,
            Name = Name,
            WorldSizeM = worldSizeM,
            CellSizeM = CellSizeM,
            PavementWidthM = walkM,
            GridWidth = gridWidth,
            GridHeight = gridHeight,
            Cells = cells,
            LaneDirs = laneDirs,
            Junctions = Nodes(lattice),
            JunctionCorners = corners,
            PavementCorners = new CityPlan.PavementCornerArrays
            {
                CornerM = [], NormalA = [], NormalB = [], RadiusM = [],
            },
            Roads = Streets(lattice, segments, widthM),
            Bridges = new CityPlan.BridgeArrays
            {
                Road = [], FromM = [], ToM = [], DeckWidthM = [], PavementWidthM = [],
            },
            PavedAreas = new CityPlan.PavedAreaArrays { MinM = [], SizeM = [] },
            ParkingLots = new CityPlan.ParkingLotArrays
            {
                CentreM = [], Axis = [], HalfExtentM = [], SpaceOffsets = [0], SpacePositionM = [], SpaceHeadingRad = [],
            },
            Crosswalks = Crossings(lattice, config),
            StopLines = Bars(lattice, config),
            Buildings = new CityPlan.BuildingArrays
            {
                CentreM = [], SizeM = [], HeadingRad = [], Capacity = [], Use = [], EntryOffsets = [0], EntryPointM = [],
            },
            Props = new CityPlan.PropArrays { CentreM = [], RadiusM = [], Kind = [] },
            Spawns = Spawns(lattice),
            Water = new CityPlan.WaterArrays { OutlineOffsets = [0], PointM = [] },
        };
    }

    /// <summary>
    /// The ground, in the order the strokes have to be laid in: the pavement, the carriageway over it, the
    /// ground the arms of each junction share over that, and the paint last of all.
    /// </summary>
    static void Paint(
        GroundPainter painter, ExamLattice lattice, ArcSeg[] segments, SimConfig config, float widthM, float walkM)
    {
        var halfM = widthM * 0.5f;
        foreach (var road in segments) painter.Verge([road], halfM, halfM + walkM, Ground.Sidewalk);

        for (var junction = 0; junction < lattice.JunctionCount; junction++)
        {
            if (lattice.IsHead(junction))
            {
                painter.Head(lattice.JunctionM(junction), lattice.HeadRadiusM + walkM, Ground.Sidewalk);
            }
        }

        foreach (var road in segments) painter.Road([road], widthM);

        for (var junction = 0; junction < lattice.JunctionCount; junction++)
        {
            if (lattice.IsHead(junction))
            {
                painter.Head(lattice.JunctionM(junction), lattice.HeadRadiusM, Ground.Intersection);
                continue;
            }

            painter.Mouth(lattice.JunctionM(junction), halfM);
        }

        foreach (var corner in EveryCorner(lattice, config, widthM))
        {
            painter.Fillet(corner.CornerM, corner.ArcCentreM, config.IntersectionCornerRadiusM);
        }

        foreach (var crossing in lattice.Crossings())
        {
            painter.Crossing(crossing.CentreM, crossing.Axis, ExamLattice.CrossingDepthM, widthM);
        }

    }

    /// <summary>
    /// One kerb fillet a junction carries: the wedge between two of its arms, paved back to the arc
    /// tangent to both carriageways (TER-5). <b>Only where two kerbs actually turn</b> — two arms running
    /// straight on have a kerb running straight past and nothing is drawn.
    /// </summary>
    readonly record struct Corner(Vector2 CornerM, Vector2 ArcCentreM, Vector2 TangentAM, Vector2 TangentBM);

    static List<Corner> EveryCorner(ExamLattice lattice, SimConfig config, float widthM)
    {
        var halfM = widthM * 0.5f;
        var radiusM = config.IntersectionCornerRadiusM;
        var corners = new List<Corner>();

        for (var cell = 0; cell < ExamCards.Count; cell++)
        {
            var centreM = lattice.JunctionM(cell);
            foreach (var (first, second) in (ReadOnlySpan<(ExamArm, ExamArm)>)
                     [
                         (ExamArm.North, ExamArm.East), (ExamArm.East, ExamArm.South),
                         (ExamArm.South, ExamArm.West), (ExamArm.West, ExamArm.North),
                     ])
            {
                if (lattice.ArmRoad(cell, first) == ExamLattice.NoRoad) continue;
                if (lattice.ArmRoad(cell, second) == ExamLattice.NoRoad) continue;

                var a = ExamLattice.Bearing(first);
                var b = ExamLattice.Bearing(second);
                corners.Add(new Corner(
                    centreM + (a * halfM) + (b * halfM),
                    centreM + (a * (halfM + radiusM)) + (b * (halfM + radiusM)),
                    centreM + (a * (halfM + radiusM)) + (b * halfM),
                    centreM + (a * halfM) + (b * (halfM + radiusM))));
            }
        }

        return corners;
    }

    static CityPlan.JunctionCornerArrays Corners(ExamLattice lattice, SimConfig config, float widthM)
    {
        var corners = EveryCorner(lattice, config, widthM);
        var cornerM = new Vector2[corners.Count];
        var arcCentreM = new Vector2[corners.Count];
        var radiusM = new float[corners.Count];
        var tangentAM = new Vector2[corners.Count];
        var tangentBM = new Vector2[corners.Count];
        for (var corner = 0; corner < corners.Count; corner++)
        {
            cornerM[corner] = corners[corner].CornerM;
            arcCentreM[corner] = corners[corner].ArcCentreM;
            radiusM[corner] = config.IntersectionCornerRadiusM;
            tangentAM[corner] = corners[corner].TangentAM;
            tangentBM[corner] = corners[corner].TangentBM;
        }

        return new CityPlan.JunctionCornerArrays
        {
            CornerM = cornerM, ArcCentreM = arcCentreM, RadiusM = radiusM,
            TangentAM = tangentAM, TangentBM = tangentBM,
        };
    }

    static CityPlan.JunctionArrays Nodes(ExamLattice lattice)
    {
        var centreM = new Vector2[lattice.JunctionCount];
        var radiusM = new float[lattice.JunctionCount];
        var lit = new bool[lattice.JunctionCount];
        var phaseOffsetS = new float[lattice.JunctionCount];
        for (var junction = 0; junction < lattice.JunctionCount; junction++)
        {
            centreM[junction] = lattice.JunctionM(junction);
            radiusM[junction] = lattice.RadiusM(junction);
            lit[junction] = lattice.Lit(junction);
            phaseOffsetS[junction] = lattice.PhaseOffsetS(junction);
        }

        return new CityPlan.JunctionArrays
        {
            CentreM = centreM, RadiusM = radiusM, Lit = lit, PhaseOffsetS = phaseOffsetS,
        };
    }

    static CityPlan.RoadArrays Streets(ExamLattice lattice, ArcSeg[] segments, float widthM)
    {
        var fromJunction = new int[lattice.Roads.Count];
        var toJunction = new int[lattice.Roads.Count];
        var widths = new float[lattice.Roads.Count];
        var offsets = new int[lattice.Roads.Count + 1];
        for (var road = 0; road < lattice.Roads.Count; road++)
        {
            fromJunction[road] = lattice.Roads[road].FromJunction;
            toJunction[road] = lattice.Roads[road].ToJunction;
            widths[road] = widthM;
            offsets[road + 1] = road + 1;
        }

        return new CityPlan.RoadArrays
        {
            FromJunction = fromJunction, ToJunction = toJunction, WidthM = widths,
            SegmentOffsets = offsets, Segments = segments,
        };
    }

    /// <summary>
    /// <b>One crossing on every arm of every junction</b> (TER-6), and the ones a card asked for in the
    /// middle of a block. It is the placement rule and not a hand-picked set: paint on the four cards that
    /// are about paint and nowhere else would leave every block's pavement a ring with no way off it.
    /// </summary>
    static CityPlan.CrosswalkArrays Crossings(ExamLattice lattice, SimConfig config)
    {
        var centreM = new List<Vector2>();
        var axis = new List<Vector2>();
        var depthM = new List<float>();
        var spanM = new List<float>();
        var junction = new List<int>();

        foreach (var crossing in lattice.Crossings())
        {
            centreM.Add(crossing.CentreM);
            axis.Add(crossing.Axis);
            depthM.Add(ExamLattice.CrossingDepthM);
            spanM.Add(config.RoadWidthM);
            junction.Add(crossing.Junction);
        }

        return new CityPlan.CrosswalkArrays
        {
            CentreM = [.. centreM], Axis = [.. axis], DepthM = [.. depthM], SpanM = [.. spanM],
            Junction = [.. junction],
        };
    }

    /// <summary>
    /// A bar on every arm of every junction that carries lights, and on no other: a bar is a place to stop
    /// that a driver is told about before it needs to, and there is nothing to tell one at an unlit box.
    /// </summary>
    static CityPlan.StopLineArrays Bars(ExamLattice lattice, SimConfig config)
    {
        var centreM = new List<Vector2>();
        var approach = new List<Vector2>();
        var spanM = new List<float>();
        var thicknessM = new List<float>();
        var junction = new List<int>();
        var road = new List<int>();

        for (var cell = 0; cell < ExamCards.Count; cell++)
        {
            if (!lattice.Lit(cell)) continue;

            for (var arm = 0; arm < 4; arm++)
            {
                var on = lattice.ArmRoad(cell, (ExamArm)arm);
                if (on == ExamLattice.NoRoad) continue;

                // Behind the paint and not in front of it: what a driver stops at is the bar, and a bar
                // painted inside the crossing would hold the car on the zebra it stopped for.
                var outward = ExamLattice.Bearing((ExamArm)arm);
                var travel = -outward;
                centreM.Add(
                    lattice.JunctionM(cell)
                    + (outward * lattice.BarM)
                    + (Heading.RightOf(travel) * config.LaneOffsetM * config.RoadSideSign));
                approach.Add(travel);
                spanM.Add(config.RoadWidthM * 0.5f);
                thicknessM.Add(ExamLattice.BarThicknessM);
                junction.Add(cell);
                road.Add(on);
            }
        }

        return new CityPlan.StopLineArrays
        {
            CentreM = [.. centreM], Approach = [.. approach], SpanM = [.. spanM],
            ThicknessM = [.. thicknessM], Junction = [.. junction], Road = [.. road],
        };
    }

    /// <summary>
    /// <b>Every car first and in card order</b>, so a card's drivers are a run of the fleet and a car's
    /// index is the card that staged it; then the people, one at the kerb of every crossing a card is
    /// about, in the same order — which is the numbering <see cref="ExamLattice.WalkerOf"/> hands back.
    /// </summary>
    /// <remarks>
    /// <b>Where a body stands is all this decides, and not what it then does.</b> This map lays pavement,
    /// so a walker with nowhere to be wanders the whole lattice rather than pacing the road beside it
    /// (<c>TownWorld.PacesARoad</c>) — the harness orders its walkers over their own paint for that
    /// reason, and a card about paint that trusted the wander would be one asked by luck.
    /// </remarks>
    static CityPlan.SpawnArrays Spawns(ExamLattice lattice)
    {
        var kind = new List<byte>();
        var positionM = new List<Vector2>();
        var headingRad = new List<float>();

        for (var card = 0; card < ExamCards.Count; card++)
        {
            for (var driver = 0; driver < ExamCards.All[card].Drivers.Length; driver++)
            {
                kind.Add(SpawnKindCar);
                positionM.Add(lattice.StandM(card, driver));
                headingRad.Add(lattice.StandHeadingRad(card, driver));
            }
        }

        for (var card = 0; card < ExamCards.Count; card++)
        {
            if (!lattice.Waiting(card, out var standM, out var facingRad)) continue;

            kind.Add(SpawnKindPerson);
            positionM.Add(standM);
            headingRad.Add(facingRad);
        }

        return new CityPlan.SpawnArrays
        {
            Kind = [.. kind], PositionM = [.. positionM], HeadingRad = [.. headingRad],
        };
    }

    const byte SpawnKindPerson = 0;

    const byte SpawnKindCar = 1;
}
