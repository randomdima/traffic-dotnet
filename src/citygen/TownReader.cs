
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Persistence;

namespace TrafficSimulation.CityGen;

/// <summary>
/// Reads a <c>.town</c> file into a <see cref="CityPlan"/>.
/// </summary>
/// <remarks>
/// Refuses a version it does not know and refuses a file with bytes left over — one rule: a file that
/// is nearly the one that was written must not be allowed to look like it worked.
/// <para>
/// Two things on the way in are not a straight copy: the sparse lane directions are expanded into the
/// dense grid the follower's inner loop wants, and every run becomes a flat array with an offsets
/// array beside it. Everything else is the file's own field order.
/// </para>
/// </remarks>
internal static class TownReader
{
    /// <summary>"TFSNTOWN", little-endian.</summary>
    public const ulong Magic = 0x4E574F544E534654;

    public const uint Version = 3;

    /// <summary>What the file writes where a record points at nothing — a crossing struck mid-block belongs to no junction.</summary>
    const uint NoIndex = 0xFFFFFFFF;

    public static CityPlan ReadFile(string path) => Read(File.ReadAllBytes(path), path);

    public static CityPlan Read(ReadOnlySpan<byte> bytes, string what = "<memory>")
    {
        var cursor = new ByteCursor(bytes);

        var magic = cursor.U64();
        if (magic != Magic) throw new FormatException($"{what} is not a .town file: magic {magic:x16}, wanted {Magic:x16}.");

        var version = cursor.U32();
        if (version != Version) throw new FormatException($"{what} is format version {version}; this engine reads version {Version} only.");

        var name = Encoding.UTF8.GetString(cursor.Take(cursor.Count("name", bytesEach: 1)));
        var seed = cursor.U64();
        var worldSizeM = cursor.V2();
        var cellSizeM = cursor.F32();
        var pavementWidthM = cursor.F32();
        var gridWidth = (int)cursor.U32();
        var gridHeight = (int)cursor.U32();
        if (gridWidth <= 0 || gridHeight <= 0 || (long)gridWidth * gridHeight > cursor.Remaining)
        {
            throw new FormatException($"{what} claims a {gridWidth}x{gridHeight} cell grid, which its {cursor.Remaining} remaining bytes cannot hold.");
        }

        var cells = new Ground[gridWidth * gridHeight];
        MemoryMarshal.Cast<byte, Ground>(cursor.Take(cells.Length)).CopyTo(cells);
        foreach (var ground in cells)
        {
            // Everything above indexes the catalogue's tables by this byte, so a cell outside it is
            // refused here rather than read out of bounds a million ticks later.
            if ((int)ground >= Grounds.Kinds) throw new FormatException($"{what} carries a cell of terrain type {(int)ground}, which no catalogue has.");
        }

        var laneDirs = ReadLaneDirs(ref cursor, cells.Length);
        var junctions = ReadJunctions(ref cursor);
        var junctionCorners = ReadJunctionCorners(ref cursor);
        var pavementCorners = ReadPavementCorners(ref cursor);
        var roads = ReadRoads(ref cursor);
        var bridges = ReadBridges(ref cursor);
        var pavedAreas = ReadPavedAreas(ref cursor);
        var crosswalks = ReadCrosswalks(ref cursor);
        var stopLines = ReadStopLines(ref cursor);
        var parkingLots = ReadParkingLots(ref cursor);
        var buildings = ReadBuildings(ref cursor);
        var props = ReadProps(ref cursor);
        var spawns = ReadSpawns(ref cursor);
        var water = ReadWater(ref cursor);

        if (cursor.Remaining != 0)
        {
            throw new FormatException($"{what} has {cursor.Remaining} bytes after the last field the format declares.");
        }

        return new CityPlan
        {
            Seed = seed,
            Name = name,
            WorldSizeM = worldSizeM,
            CellSizeM = cellSizeM,
            PavementWidthM = pavementWidthM,
            GridWidth = gridWidth,
            GridHeight = gridHeight,
            Cells = cells,
            LaneDirs = laneDirs,
            Junctions = junctions,
            JunctionCorners = junctionCorners,
            PavementCorners = pavementCorners,
            Roads = roads,
            Bridges = bridges,
            PavedAreas = pavedAreas,
            Crosswalks = crosswalks,
            StopLines = stopLines,
            ParkingLots = parkingLots,
            Buildings = buildings,
            Props = props,
            Spawns = spawns,
            Water = water,
        };
    }

    /// <summary>
    /// The file's <c>(index, x, y)</c> triples, laid into two bytes a cell over the whole grid. The
    /// expansion is the reason the file may stay sparse: a city's grid is 24.6 MB of which a few per
    /// cent is non-zero, and the tick asks for a direction by position.
    /// </summary>
    static sbyte[] ReadLaneDirs(ref ByteCursor cursor, int cellCount)
    {
        var laneDirs = new sbyte[cellCount * 2];
        var count = cursor.Count("lane directions", bytesEach: 6);
        for (var i = 0; i < count; i++)
        {
            var cell = cursor.U32();
            var x = cursor.I8();
            var y = cursor.I8();
            if (cell >= (uint)cellCount) throw new FormatException($"A lane direction is on cell {cell}, off a grid of {cellCount}.");

            laneDirs[cell * 2] = x;
            laneDirs[cell * 2 + 1] = y;
        }

        return laneDirs;
    }

    /// <summary>
    /// A reference to another of the plan's records, or <see cref="CityPlan.NoRecord"/>. Read through
    /// one place so that the file's sentinel becomes an index nothing can mistake for a record — the
    /// straight cast would make it 4 294 967 295 or −1 depending on which type the reader happened to
    /// use, and only one of those is caught by anything.
    /// </summary>
    static int Index(ref ByteCursor cursor)
    {
        var raw = cursor.U32();
        if (raw == NoIndex) return CityPlan.NoRecord;
        if (raw > int.MaxValue) throw new FormatException($"An index of {raw} at offset {cursor.Offset - 4} is neither a record nor the absence of one.");

        return (int)raw;
    }

    static CityPlan.JunctionArrays ReadJunctions(ref ByteCursor cursor)
    {
        var count = cursor.Count("junctions", bytesEach: 17);
        var centreM = new Vector2[count];
        var radiusM = new float[count];
        var lit = new bool[count];
        var phaseOffsetS = new float[count];
        for (var i = 0; i < count; i++)
        {
            centreM[i] = cursor.V2();
            radiusM[i] = cursor.F32();
            lit[i] = cursor.U8() != 0;
            phaseOffsetS[i] = cursor.F32();
        }

        return new CityPlan.JunctionArrays { CentreM = centreM, RadiusM = radiusM, Lit = lit, PhaseOffsetS = phaseOffsetS };
    }

    static CityPlan.JunctionCornerArrays ReadJunctionCorners(ref ByteCursor cursor)
    {
        var count = cursor.Count("junction corners", bytesEach: 36);
        var cornerM = new Vector2[count];
        var arcCentreM = new Vector2[count];
        var radiusM = new float[count];
        var tangentAM = new Vector2[count];
        var tangentBM = new Vector2[count];
        for (var i = 0; i < count; i++)
        {
            cornerM[i] = cursor.V2();
            arcCentreM[i] = cursor.V2();
            radiusM[i] = cursor.F32();
            tangentAM[i] = cursor.V2();
            tangentBM[i] = cursor.V2();
        }

        return new CityPlan.JunctionCornerArrays
        {
            CornerM = cornerM, ArcCentreM = arcCentreM, RadiusM = radiusM, TangentAM = tangentAM, TangentBM = tangentBM,
        };
    }

    static CityPlan.PavementCornerArrays ReadPavementCorners(ref ByteCursor cursor)
    {
        var count = cursor.Count("pavement corners", bytesEach: 28);
        var cornerM = new Vector2[count];
        var normalA = new Vector2[count];
        var normalB = new Vector2[count];
        var radiusM = new float[count];
        for (var i = 0; i < count; i++)
        {
            cornerM[i] = cursor.V2();
            normalA[i] = cursor.V2();
            normalB[i] = cursor.V2();
            radiusM[i] = cursor.F32();
        }

        return new CityPlan.PavementCornerArrays { CornerM = cornerM, NormalA = normalA, NormalB = normalB, RadiusM = radiusM };
    }

    static CityPlan.RoadArrays ReadRoads(ref ByteCursor cursor)
    {
        var count = cursor.Count("roads", bytesEach: 16);
        var fromJunction = new int[count];
        var toJunction = new int[count];
        var widthM = new float[count];
        var offsets = new int[count + 1];
        var segments = new List<ArcSeg>(count * 4);
        for (var i = 0; i < count; i++)
        {
            fromJunction[i] = Index(ref cursor);
            toJunction[i] = Index(ref cursor);
            widthM[i] = cursor.F32();
            offsets[i] = segments.Count;
            var pieces = cursor.Count("road segments", bytesEach: 20);
            for (var piece = 0; piece < pieces; piece++)
            {
                segments.Add(new ArcSeg(cursor.V2(), cursor.F32(), cursor.F32(), cursor.F32()));
            }
        }

        offsets[count] = segments.Count;
        return new CityPlan.RoadArrays
        {
            FromJunction = fromJunction, ToJunction = toJunction, WidthM = widthM,
            SegmentOffsets = offsets, Segments = segments.ToArray(),
        };
    }

    static CityPlan.BridgeArrays ReadBridges(ref ByteCursor cursor)
    {
        var count = cursor.Count("bridges", bytesEach: 20);
        var road = new int[count];
        var fromM = new float[count];
        var toM = new float[count];
        var deckWidthM = new float[count];
        var pavementWidthM = new float[count];
        for (var i = 0; i < count; i++)
        {
            road[i] = Index(ref cursor);
            fromM[i] = cursor.F32();
            toM[i] = cursor.F32();
            deckWidthM[i] = cursor.F32();
            pavementWidthM[i] = cursor.F32();
        }

        return new CityPlan.BridgeArrays
        {
            Road = road, FromM = fromM, ToM = toM, DeckWidthM = deckWidthM, PavementWidthM = pavementWidthM,
        };
    }

    static CityPlan.PavedAreaArrays ReadPavedAreas(ref ByteCursor cursor)
    {
        var count = cursor.Count("paved areas", bytesEach: 16);
        var minM = new Vector2[count];
        var sizeM = new Vector2[count];
        for (var i = 0; i < count; i++)
        {
            minM[i] = cursor.V2();
            sizeM[i] = cursor.V2();
        }

        return new CityPlan.PavedAreaArrays { MinM = minM, SizeM = sizeM };
    }

    static CityPlan.CrosswalkArrays ReadCrosswalks(ref ByteCursor cursor)
    {
        var count = cursor.Count("crosswalks", bytesEach: 28);
        var centreM = new Vector2[count];
        var axis = new Vector2[count];
        var depthM = new float[count];
        var spanM = new float[count];
        var junction = new int[count];
        for (var i = 0; i < count; i++)
        {
            centreM[i] = cursor.V2();
            axis[i] = cursor.V2();
            depthM[i] = cursor.F32();
            spanM[i] = cursor.F32();
            junction[i] = Index(ref cursor);
        }

        return new CityPlan.CrosswalkArrays
        {
            CentreM = centreM, Axis = axis, DepthM = depthM, SpanM = spanM, Junction = junction,
        };
    }

    static CityPlan.StopLineArrays ReadStopLines(ref ByteCursor cursor)
    {
        var count = cursor.Count("stop lines", bytesEach: 32);
        var centreM = new Vector2[count];
        var approach = new Vector2[count];
        var spanM = new float[count];
        var thicknessM = new float[count];
        var junction = new int[count];
        var road = new int[count];
        for (var i = 0; i < count; i++)
        {
            centreM[i] = cursor.V2();
            approach[i] = cursor.V2();
            spanM[i] = cursor.F32();
            thicknessM[i] = cursor.F32();
            junction[i] = Index(ref cursor);
            road[i] = Index(ref cursor);
        }

        return new CityPlan.StopLineArrays
        {
            CentreM = centreM, Approach = approach, SpanM = spanM, ThicknessM = thicknessM, Junction = junction, Road = road,
        };
    }

    static CityPlan.ParkingLotArrays ReadParkingLots(ref ByteCursor cursor)
    {
        var count = cursor.Count("parking lots", bytesEach: 28);
        var centreM = new Vector2[count];
        var axis = new Vector2[count];
        var halfExtentM = new Vector2[count];
        var offsets = new int[count + 1];
        var positionM = new List<Vector2>(count * 4);
        var headingRad = new List<float>(count * 4);
        for (var i = 0; i < count; i++)
        {
            centreM[i] = cursor.V2();
            axis[i] = cursor.V2();
            halfExtentM[i] = cursor.V2();
            offsets[i] = positionM.Count;
            var spaces = cursor.Count("parking spaces", bytesEach: 12);
            for (var space = 0; space < spaces; space++)
            {
                positionM.Add(cursor.V2());
                headingRad.Add(cursor.F32());
            }
        }

        offsets[count] = positionM.Count;
        return new CityPlan.ParkingLotArrays
        {
            CentreM = centreM, Axis = axis, HalfExtentM = halfExtentM, SpaceOffsets = offsets,
            SpacePositionM = positionM.ToArray(), SpaceHeadingRad = headingRad.ToArray(),
        };
    }

    static CityPlan.BuildingArrays ReadBuildings(ref ByteCursor cursor)
    {
        var count = cursor.Count("buildings", bytesEach: 29);
        var centreM = new Vector2[count];
        var sizeM = new Vector2[count];
        var headingRad = new float[count];
        var capacity = new int[count];
        var use = new BuildingUse[count];
        var offsets = new int[count + 1];
        var entryPointM = new List<Vector2>(count);
        for (var i = 0; i < count; i++)
        {
            centreM[i] = cursor.V2();
            sizeM[i] = cursor.V2();
            headingRad[i] = cursor.F32();
            capacity[i] = (int)cursor.U32();

            // Bounded against the plan's own list, as a cell byte is: a use nothing has a roof or an apron
            // for is refused here rather than indexed out of a table when the map is stood up.
            var kind = cursor.U8();
            if (kind >= BuildingUseKinds.Count) throw new FormatException($"A building is of use {kind}, which the plan has no name for.");

            use[i] = (BuildingUse)kind;
            offsets[i] = entryPointM.Count;
            var entries = cursor.Count("building entry points", bytesEach: 8);
            for (var entry = 0; entry < entries; entry++) entryPointM.Add(cursor.V2());
        }

        offsets[count] = entryPointM.Count;
        return new CityPlan.BuildingArrays
        {
            CentreM = centreM, SizeM = sizeM, HeadingRad = headingRad, Capacity = capacity, Use = use,
            EntryOffsets = offsets, EntryPointM = entryPointM.ToArray(),
        };
    }

    static CityPlan.PropArrays ReadProps(ref ByteCursor cursor)
    {
        var count = cursor.Count("props", bytesEach: 13);
        var centreM = new Vector2[count];
        var radiusM = new float[count];
        var kind = new byte[count];
        for (var i = 0; i < count; i++)
        {
            centreM[i] = cursor.V2();
            radiusM[i] = cursor.F32();
            kind[i] = cursor.U8();
        }

        return new CityPlan.PropArrays { CentreM = centreM, RadiusM = radiusM, Kind = kind };
    }

    static CityPlan.SpawnArrays ReadSpawns(ref ByteCursor cursor)
    {
        var count = cursor.Count("spawns", bytesEach: 13);
        var kind = new byte[count];
        var positionM = new Vector2[count];
        var headingRad = new float[count];
        for (var i = 0; i < count; i++)
        {
            kind[i] = cursor.U8();
            positionM[i] = cursor.V2();
            headingRad[i] = cursor.F32();
        }

        return new CityPlan.SpawnArrays { Kind = kind, PositionM = positionM, HeadingRad = headingRad };
    }

    static CityPlan.WaterArrays ReadWater(ref ByteCursor cursor)
    {
        var count = cursor.Count("water outlines", bytesEach: 4);
        var offsets = new int[count + 1];
        var pointM = new List<Vector2>(count * 8);
        for (var i = 0; i < count; i++)
        {
            offsets[i] = pointM.Count;
            var points = cursor.Count("water outline points", bytesEach: 8);
            for (var point = 0; point < points; point++) pointM.Add(cursor.V2());
        }

        offsets[count] = pointM.Count;
        return new CityPlan.WaterArrays { OutlineOffsets = offsets, PointM = pointM.ToArray() };
    }
}
