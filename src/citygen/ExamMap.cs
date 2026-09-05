using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>A lattice written out as a map</b>: the junctions, the roads between them, the kerb fillets where
/// two of them turn, a crossing on every arm and a bar on every arm that is lit — and, on top of that
/// ground, whoever the map's own cards stand up.
/// </summary>
/// <remarks>
/// <b>One ground under both exams.</b> The driving exam (<see cref="ExamPlan"/>) and the walking one
/// (<see cref="FootwayPlan"/>) differ in their cards and in who is stood up; a junction laid twice would
/// be a junction that could pass one exam and fail the other for reasons nobody could name.
/// </remarks>
internal static class ExamMap
{
    public static CityPlan Lay(
        string name, ulong seed, ExamGround lattice, SimConfig config, CityPlan.SpawnArrays spawns)
    {
        var widthM = config.RoadWidthM;
        var segments = new ArcSeg[lattice.Roads.Count];
        for (var road = 0; road < lattice.Roads.Count; road++)
        {
            var run = lattice.Roads[road].ToM - lattice.Roads[road].FromM;
            segments[road] = new ArcSeg(lattice.Roads[road].FromM, ExamGround.Facing(run), run.Length(), 0f);
        }

        return new CityPlan
        {
            Seed = seed,
            Name = name,
            WorldSizeM = lattice.WorldSizeM,
            PavementWidthM = config.PavementWidthM,
            Junctions = Nodes(lattice),
            JunctionCorners = Corners(lattice, config, widthM),
            PavementCorners = new CityPlan.PavementCornerArrays
            {
                CornerM = [], NormalA = [], NormalB = [], RadiusM = [],
            },
            Roads = Streets(lattice, segments, widthM),
            Bridges = new CityPlan.BridgeArrays
            {
                Road = [], FromM = [], ToM = [], DeckWidthM = [], PavementWidthM = [],
            },
            PavedAreas = CityPlan.PavedAreaArrays.None,
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
            Props = new CityPlan.PropArrays { CentreM = [], RadiusM = [], BearingRad = [], Kind = [] },
            Spawns = spawns,
            Water = CityPlan.WaterArrays.None,
        };
    }

    /// <summary>
    /// One kerb fillet a junction carries: the wedge between two of its arms, paved back to the arc
    /// tangent to both carriageways (TER-5). <b>Only where two kerbs actually turn</b> — two arms running
    /// straight on have a kerb running straight past and nothing is drawn.
    /// </summary>
    readonly record struct Corner(Vector2 CornerM, Vector2 ArcCentreM, Vector2 TangentAM, Vector2 TangentBM);

    static List<Corner> EveryCorner(ExamGround lattice, SimConfig config, float widthM)
    {
        var halfM = widthM * 0.5f;
        var radiusM = config.IntersectionCornerRadiusM;
        var corners = new List<Corner>();

        for (var cell = 0; cell < lattice.Cells; cell++)
        {
            var centreM = lattice.JunctionM(cell);
            foreach (var (first, second) in (ReadOnlySpan<(ExamArm, ExamArm)>)
                     [
                         (ExamArm.North, ExamArm.East), (ExamArm.East, ExamArm.South),
                         (ExamArm.South, ExamArm.West), (ExamArm.West, ExamArm.North),
                     ])
            {
                if (lattice.ArmRoad(cell, first) == ExamGround.NoRoad) continue;
                if (lattice.ArmRoad(cell, second) == ExamGround.NoRoad) continue;

                var a = ExamGround.Bearing(first);
                var b = ExamGround.Bearing(second);
                corners.Add(new Corner(
                    centreM + (a * halfM) + (b * halfM),
                    centreM + (a * (halfM + radiusM)) + (b * (halfM + radiusM)),
                    centreM + (a * (halfM + radiusM)) + (b * halfM),
                    centreM + (a * halfM) + (b * (halfM + radiusM))));
            }
        }

        return corners;
    }

    static CityPlan.JunctionCornerArrays Corners(ExamGround lattice, SimConfig config, float widthM)
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

    static CityPlan.JunctionArrays Nodes(ExamGround lattice)
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

    static CityPlan.RoadArrays Streets(ExamGround lattice, ArcSeg[] segments, float widthM)
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
            Flow = CityPlan.RoadArrays.AllBothWays(lattice.Roads.Count),
            SegmentOffsets = offsets, Segments = segments,
        };
    }

    /// <summary>
    /// <b>One crossing on every arm of every junction</b> (TER-6), and the ones a cell asked for in the
    /// middle of a block. It is the placement rule and not a hand-picked set: paint on the cards that are
    /// about paint and nowhere else would leave every block's pavement a ring with no way off it.
    /// </summary>
    static CityPlan.CrosswalkArrays Crossings(ExamGround lattice, SimConfig config)
    {
        var centreM = new List<Vector2>();
        var axis = new List<Vector2>();
        var depthM = new List<float>();
        var road = new List<int>();
        var junction = new List<int>();

        foreach (var crossing in lattice.Crossings())
        {
            centreM.Add(crossing.CentreM);
            axis.Add(crossing.Axis);
            depthM.Add(config.Road.CrossingDepthM);
            road.Add(crossing.Road);
            junction.Add(crossing.Junction);
        }

        return new CityPlan.CrosswalkArrays
        {
            CentreM = [.. centreM], Axis = [.. axis], DepthM = [.. depthM], Road = [.. road],
            Junction = [.. junction],
        };
    }

    /// <summary>
    /// A bar on every arm of every junction that carries lights, and on no other: a bar is a place to stop
    /// that a driver is told about before it needs to, and there is nothing to tell one at an unlit box.
    /// </summary>
    static CityPlan.StopLineArrays Bars(ExamGround lattice, SimConfig config)
    {
        var centreM = new List<Vector2>();
        var approach = new List<Vector2>();
        var spanM = new List<float>();
        var thicknessM = new List<float>();
        var junction = new List<int>();
        var road = new List<int>();

        for (var cell = 0; cell < lattice.Cells; cell++)
        {
            if (!lattice.Lit(cell)) continue;

            for (var arm = 0; arm < 4; arm++)
            {
                var on = lattice.ArmRoad(cell, (ExamArm)arm);
                if (on == ExamGround.NoRoad) continue;

                // Behind the paint and not in front of it: what a driver stops at is the bar, and a bar
                // painted inside the crossing would hold the car on the zebra it stopped for.
                var outward = ExamGround.Bearing((ExamArm)arm);
                var travel = -outward;
                centreM.Add(
                    lattice.JunctionM(cell)
                    + (outward * lattice.BarM)
                    + (Heading.RightOf(travel) * config.LaneOffsetM * config.RoadSideSign));
                approach.Add(travel);
                spanM.Add(config.RoadWidthM * 0.5f);
                thicknessM.Add(config.Road.StopBarThicknessM);
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
}
