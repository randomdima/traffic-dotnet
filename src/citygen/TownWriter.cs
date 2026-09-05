using System.Text;
using TrafficSimulation.Core.Persistence;

namespace TrafficSimulation.CityGen;

/// <summary>
/// Writes a <see cref="CityPlan"/> back out as a <c>.town</c> file.
/// </summary>
/// <remarks>
/// <para>
/// <b>Field for field with <see cref="TownReader"/>, in its order.</b> The two are one contract and the
/// round trip is what says so: a plan written here and read back is the plan that was written, for
/// everything the format carries.
/// </para>
/// <para>
/// <b>What the format does not carry is not invented here.</b> A prop's bearing, a water's shore and the
/// two lines drawn along it are solved when the town is stood up rather than shipped, so they are absent
/// from a written file exactly as they are absent from a read one — and a round trip that put them back
/// would be this build's answer wearing the map's name.
/// </para>
/// </remarks>
internal static class TownWriter
{
    public static void WriteFile(CityPlan plan, string path) => File.WriteAllBytes(path, Write(plan));

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
        tape.F32(plan.PavementWidthM);

        WriteJunctions(tape, plan.Junctions);
        WriteJunctionCorners(tape, plan.JunctionCorners);
        WritePavementCorners(tape, plan.PavementCorners);
        WriteRoads(tape, plan.Roads);
        WriteBridges(tape, plan.Bridges);
        WritePavedAreas(tape, plan.PavedAreas);
        WriteCrosswalks(tape, plan);
        WriteStopLines(tape, plan.StopLines);
        WriteParkingLots(tape, plan.ParkingLots);
        WriteBuildings(tape, plan.Buildings);
        WriteProps(tape, plan.Props);
        WriteSpawns(tape, plan.Spawns);
        WriteWater(tape, plan.Water.Outline);

        return tape.Written();
    }

    /// <summary>
    /// A reference to another of the plan's records, or the sentinel that says there is none — the mirror
    /// of <c>TownReader.Index</c>, so that the absence of a record survives the round trip as an absence
    /// rather than as record 4 294 967 295.
    /// </summary>
    static void Index(ByteTape tape, int record) => tape.U32(record < 0 ? NoIndex : (uint)record);

    const uint NoIndex = 0xFFFFFFFF;

    static void WriteJunctions(ByteTape tape, CityPlan.JunctionArrays junctions)
    {
        tape.Count(junctions.Count);
        for (var junction = 0; junction < junctions.Count; junction++)
        {
            tape.V2(junctions.CentreM[junction]);
            tape.F32(junctions.RadiusM[junction]);
            tape.U8(junctions.Lit[junction] ? (byte)1 : (byte)0);
            tape.F32(junctions.PhaseOffsetS[junction]);
        }
    }

    static void WriteJunctionCorners(ByteTape tape, CityPlan.JunctionCornerArrays corners)
    {
        tape.Count(corners.Count);
        for (var corner = 0; corner < corners.Count; corner++)
        {
            tape.V2(corners.CornerM[corner]);
            tape.V2(corners.ArcCentreM[corner]);
            tape.F32(corners.RadiusM[corner]);
            tape.V2(corners.TangentAM[corner]);
            tape.V2(corners.TangentBM[corner]);
        }
    }

    static void WritePavementCorners(ByteTape tape, CityPlan.PavementCornerArrays corners)
    {
        tape.Count(corners.Count);
        for (var corner = 0; corner < corners.Count; corner++)
        {
            tape.V2(corners.CornerM[corner]);
            tape.V2(corners.NormalA[corner]);
            tape.V2(corners.NormalB[corner]);
            tape.F32(corners.RadiusM[corner]);
        }
    }

    static void WriteRoads(ByteTape tape, CityPlan.RoadArrays roads)
    {
        tape.Count(roads.Count);
        for (var road = 0; road < roads.Count; road++)
        {
            Index(tape, roads.FromJunction[road]);
            Index(tape, roads.ToJunction[road]);
            tape.F32(roads.WidthM[road]);

            var arcs = roads.SegmentsOf(road);
            tape.Count(arcs.Length);
            foreach (var arc in arcs)
            {
                tape.V2(arc.StartM);
                tape.F32(arc.HeadingRad);
                tape.F32(arc.LengthM);
                tape.F32(arc.Curvature);
            }
        }
    }

    static void WriteBridges(ByteTape tape, CityPlan.BridgeArrays bridges)
    {
        tape.Count(bridges.Count);
        for (var bridge = 0; bridge < bridges.Count; bridge++)
        {
            Index(tape, bridges.Road[bridge]);
            tape.F32(bridges.FromM[bridge]);
            tape.F32(bridges.ToM[bridge]);
            tape.F32(bridges.DeckWidthM[bridge]);
            tape.F32(bridges.PavementWidthM[bridge]);
        }
    }

    static void WritePavedAreas(ByteTape tape, CityPlan.PavedAreaArrays areas)
    {
        tape.Count(areas.Count);
        for (var area = 0; area < areas.Count; area++)
        {
            tape.V2(areas.MinM[area]);
            tape.V2(areas.SizeM[area]);
        }
    }

    /// <summary>
    /// The zebras, with the span the format has a field for solved off the road each one is painted across
    /// (TER-6). The reader drops that field for the same reason: a crossing has no width of its own, so
    /// what goes in the slot is the answer rather than a second one.
    /// </summary>
    static void WriteCrosswalks(ByteTape tape, CityPlan plan)
    {
        var crosswalks = plan.Crosswalks;
        tape.Count(crosswalks.Count);
        for (var crossing = 0; crossing < crosswalks.Count; crossing++)
        {
            tape.V2(crosswalks.CentreM[crossing]);
            tape.V2(crosswalks.Axis[crossing]);
            tape.F32(crosswalks.DepthM[crossing]);
            tape.F32(plan.CrossingSpanM(crossing));
            Index(tape, crosswalks.Junction[crossing]);
        }
    }

    static void WriteStopLines(ByteTape tape, CityPlan.StopLineArrays bars)
    {
        tape.Count(bars.Count);
        for (var bar = 0; bar < bars.Count; bar++)
        {
            tape.V2(bars.CentreM[bar]);
            tape.V2(bars.Approach[bar]);
            tape.F32(bars.SpanM[bar]);
            tape.F32(bars.ThicknessM[bar]);
            Index(tape, bars.Junction[bar]);
            Index(tape, bars.Road[bar]);
        }
    }

    static void WriteParkingLots(ByteTape tape, CityPlan.ParkingLotArrays lots)
    {
        tape.Count(lots.Count);
        for (var lot = 0; lot < lots.Count; lot++)
        {
            tape.V2(lots.CentreM[lot]);
            tape.V2(lots.Axis[lot]);
            tape.V2(lots.HalfExtentM[lot]);

            var from = lots.SpaceOffsets[lot];
            var to = lots.SpaceOffsets[lot + 1];
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
        for (var building = 0; building < buildings.Count; building++)
        {
            tape.V2(buildings.CentreM[building]);
            tape.V2(buildings.SizeM[building]);
            tape.F32(buildings.HeadingRad[building]);
            tape.U32((uint)buildings.Capacity[building]);
            tape.U8((byte)buildings.Use[building]);

            var from = buildings.EntryOffsets[building];
            var to = buildings.EntryOffsets[building + 1];
            tape.Count(to - from);
            for (var entry = from; entry < to; entry++) tape.V2(buildings.EntryPointM[entry]);
        }
    }

    static void WriteProps(ByteTape tape, CityPlan.PropArrays props)
    {
        tape.Count(props.Count);
        for (var prop = 0; prop < props.Count; prop++)
        {
            tape.V2(props.CentreM[prop]);
            tape.F32(props.RadiusM[prop]);
            tape.U8(props.Kind[prop]);
        }
    }

    static void WriteSpawns(ByteTape tape, CityPlan.SpawnArrays spawns)
    {
        tape.Count(spawns.Count);
        for (var spawn = 0; spawn < spawns.Count; spawn++)
        {
            tape.U8(spawns.Kind[spawn]);
            tape.V2(spawns.PositionM[spawn]);
            tape.F32(spawns.HeadingRad[spawn]);
        }
    }

    static void WriteWater(ByteTape tape, CityPlan.RingArrays outlines)
    {
        tape.Count(outlines.Count);
        for (var outline = 0; outline < outlines.Count; outline++)
        {
            var ringM = outlines.RingOf(outline);
            tape.Count(ringM.Length);
            foreach (var pointM in ringM) tape.V2(pointM);
        }
    }
}
