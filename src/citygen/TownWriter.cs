using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Persistence;

namespace TrafficSimulation.CityGen;

/// <summary>
/// Writes a <see cref="CityPlan"/> back out as a <c>.town</c> file — <see cref="TownReader"/>'s own
/// field order, run for run and byte for byte.
/// </summary>
/// <remarks>
/// <para>
/// <b>It exists so that a map this build lays itself is a map like any other</b>: the game opens it by
/// name, every sweep that asks a question of every shipped town asks it of this one too, and nothing
/// downstream can tell a town that was authored here from one that arrived as a file. A plan held only
/// in code would be a second kind of map, readable by whatever built it and by nothing else.
/// </para>
/// <para>
/// <b>The two shapes the reader adds on the way in are undone here.</b> The dense lane-direction grid
/// goes back to the sparse triples the file carries, and a flat run with its offsets beside it goes back
/// to a count followed by its records. Everything else is a straight copy in the file's own order, and
/// the round trip over every shipped map is what holds the two halves to each other.
/// </para>
/// </remarks>
internal static class TownWriter
{
    /// <summary>What the file writes where a record points at nothing. <see cref="TownReader"/>'s own sentinel.</summary>
    const uint NoIndex = 0xFFFFFFFF;

    public static void WriteFile(CityPlan plan, string path)
    {
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        File.WriteAllBytes(path, Write(plan));
    }

    public static byte[] Write(CityPlan plan)
    {
        var tape = new ByteTape();

        tape.U64(TownReader.Magic);
        tape.U32(TownReader.Version);

        var name = Encoding.UTF8.GetBytes(plan.Name);
        tape.Count(name.Length);
        tape.Bytes(name);

        tape.U64(plan.Seed);
        tape.V2(plan.WorldSizeM);
        tape.F32(plan.CellSizeM);
        tape.F32(plan.PavementWidthM);
        tape.U32((uint)plan.GridWidth);
        tape.U32((uint)plan.GridHeight);
        tape.Bytes(MemoryMarshal.AsBytes(plan.Cells.AsSpan()));

        WriteLaneDirs(tape, plan.LaneDirs);
        WriteJunctions(tape, plan.Junctions);
        WriteJunctionCorners(tape, plan.JunctionCorners);
        WritePavementCorners(tape, plan.PavementCorners);
        WriteRoads(tape, plan.Roads);
        WriteBridges(tape, plan.Bridges);
        WritePavedAreas(tape, plan.PavedAreas);
        WriteCrosswalks(tape, plan.Crosswalks);
        WriteStopLines(tape, plan.StopLines);
        WriteParkingLots(tape, plan.ParkingLots);
        WriteBuildings(tape, plan.Buildings);
        WriteProps(tape, plan.Props);
        WriteSpawns(tape, plan.Spawns);
        WriteWater(tape, plan.Water);

        return tape.Written();
    }

    /// <summary>
    /// The dense grid back to the file's <c>(cell, x, y)</c> triples: a cell with no direction on it is a
    /// cell off the carriageway, and the file carries none of them.
    /// </summary>
    static void WriteLaneDirs(ByteTape tape, sbyte[] laneDirs)
    {
        var count = 0;
        for (var cell = 0; cell * 2 + 1 < laneDirs.Length; cell++)
        {
            if (laneDirs[cell * 2] != 0 || laneDirs[cell * 2 + 1] != 0) count++;
        }

        tape.Count(count);
        for (var cell = 0; cell * 2 + 1 < laneDirs.Length; cell++)
        {
            if (laneDirs[cell * 2] == 0 && laneDirs[cell * 2 + 1] == 0) continue;

            tape.U32((uint)cell);
            tape.I8(laneDirs[cell * 2]);
            tape.I8(laneDirs[cell * 2 + 1]);
        }
    }

    /// <summary>A reference to another of the plan's records, or the file's own absence of one.</summary>
    static void Index(ByteTape tape, int record) => tape.U32(record < 0 ? NoIndex : (uint)record);

    static void WriteJunctions(ByteTape tape, CityPlan.JunctionArrays junctions)
    {
        tape.Count(junctions.Count);
        for (var i = 0; i < junctions.Count; i++)
        {
            tape.V2(junctions.CentreM[i]);
            tape.F32(junctions.RadiusM[i]);
            tape.U8(junctions.Lit[i] ? (byte)1 : (byte)0);
            tape.F32(junctions.PhaseOffsetS[i]);
        }
    }

    static void WriteJunctionCorners(ByteTape tape, CityPlan.JunctionCornerArrays corners)
    {
        tape.Count(corners.Count);
        for (var i = 0; i < corners.Count; i++)
        {
            tape.V2(corners.CornerM[i]);
            tape.V2(corners.ArcCentreM[i]);
            tape.F32(corners.RadiusM[i]);
            tape.V2(corners.TangentAM[i]);
            tape.V2(corners.TangentBM[i]);
        }
    }

    static void WritePavementCorners(ByteTape tape, CityPlan.PavementCornerArrays corners)
    {
        tape.Count(corners.Count);
        for (var i = 0; i < corners.Count; i++)
        {
            tape.V2(corners.CornerM[i]);
            tape.V2(corners.NormalA[i]);
            tape.V2(corners.NormalB[i]);
            tape.F32(corners.RadiusM[i]);
        }
    }

    static void WriteRoads(ByteTape tape, CityPlan.RoadArrays roads)
    {
        tape.Count(roads.Count);
        for (var i = 0; i < roads.Count; i++)
        {
            Index(tape, roads.FromJunction[i]);
            Index(tape, roads.ToJunction[i]);
            tape.F32(roads.WidthM[i]);

            var segments = roads.SegmentsOf(i);
            tape.Count(segments.Length);
            foreach (var segment in segments) WriteArc(tape, segment);
        }
    }

    static void WriteArc(ByteTape tape, in ArcSeg arc)
    {
        tape.V2(arc.StartM);
        tape.F32(arc.HeadingRad);
        tape.F32(arc.LengthM);
        tape.F32(arc.Curvature);
    }

    static void WriteBridges(ByteTape tape, CityPlan.BridgeArrays bridges)
    {
        tape.Count(bridges.Count);
        for (var i = 0; i < bridges.Count; i++)
        {
            Index(tape, bridges.Road[i]);
            tape.F32(bridges.FromM[i]);
            tape.F32(bridges.ToM[i]);
            tape.F32(bridges.DeckWidthM[i]);
            tape.F32(bridges.PavementWidthM[i]);
        }
    }

    static void WritePavedAreas(ByteTape tape, CityPlan.PavedAreaArrays areas)
    {
        tape.Count(areas.Count);
        for (var i = 0; i < areas.Count; i++)
        {
            tape.V2(areas.MinM[i]);
            tape.V2(areas.SizeM[i]);
        }
    }

    static void WriteCrosswalks(ByteTape tape, CityPlan.CrosswalkArrays crosswalks)
    {
        tape.Count(crosswalks.Count);
        for (var i = 0; i < crosswalks.Count; i++)
        {
            tape.V2(crosswalks.CentreM[i]);
            tape.V2(crosswalks.Axis[i]);
            tape.F32(crosswalks.DepthM[i]);
            tape.F32(crosswalks.SpanM[i]);
            Index(tape, crosswalks.Junction[i]);
        }
    }

    static void WriteStopLines(ByteTape tape, CityPlan.StopLineArrays stopLines)
    {
        tape.Count(stopLines.Count);
        for (var i = 0; i < stopLines.Count; i++)
        {
            tape.V2(stopLines.CentreM[i]);
            tape.V2(stopLines.Approach[i]);
            tape.F32(stopLines.SpanM[i]);
            tape.F32(stopLines.ThicknessM[i]);
            Index(tape, stopLines.Junction[i]);
            Index(tape, stopLines.Road[i]);
        }
    }

    static void WriteParkingLots(ByteTape tape, CityPlan.ParkingLotArrays lots)
    {
        tape.Count(lots.Count);
        for (var i = 0; i < lots.Count; i++)
        {
            tape.V2(lots.CentreM[i]);
            tape.V2(lots.Axis[i]);
            tape.V2(lots.HalfExtentM[i]);

            var from = lots.SpaceOffsets[i];
            var to = lots.SpaceOffsets[i + 1];
            tape.Count(to - from);
            for (var space = from; space < to; space++)
            {
                tape.V2(lots.SpacePositionM[space]);
                tape.F32(lots.SpaceHeadingRad[space]);
            }
        }
    }

    static void WriteBuildings(ByteTape tape, CityPlan.BuildingArrays buildings)
    {
        tape.Count(buildings.Count);
        for (var i = 0; i < buildings.Count; i++)
        {
            tape.V2(buildings.CentreM[i]);
            tape.V2(buildings.SizeM[i]);
            tape.F32(buildings.HeadingRad[i]);
            tape.U32((uint)buildings.Capacity[i]);

            var from = buildings.EntryOffsets[i];
            var to = buildings.EntryOffsets[i + 1];
            tape.Count(to - from);
            for (var entry = from; entry < to; entry++) tape.V2(buildings.EntryPointM[entry]);
        }
    }

    static void WriteProps(ByteTape tape, CityPlan.PropArrays props)
    {
        tape.Count(props.Count);
        for (var i = 0; i < props.Count; i++)
        {
            tape.V2(props.CentreM[i]);
            tape.F32(props.RadiusM[i]);
            tape.U8(props.Kind[i]);
        }
    }

    static void WriteSpawns(ByteTape tape, CityPlan.SpawnArrays spawns)
    {
        tape.Count(spawns.Count);
        for (var i = 0; i < spawns.Count; i++)
        {
            tape.U8(spawns.Kind[i]);
            tape.V2(spawns.PositionM[i]);
            tape.F32(spawns.HeadingRad[i]);
        }
    }

    static void WriteWater(ByteTape tape, CityPlan.WaterArrays water)
    {
        tape.Count(water.Count);
        for (var outline = 0; outline < water.Count; outline++)
        {
            var points = water.OutlineOf(outline);
            tape.Count(points.Length);
            foreach (var point in points) tape.V2(point);
        }
    }
}
