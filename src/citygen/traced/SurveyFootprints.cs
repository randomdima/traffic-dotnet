using System.Numerics;

namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// <b>What a building is for</b>, as OSM's tags on it say — its <c>building</c> value, else its <c>amenity</c> or
/// <c>shop</c> — and, where they say nothing, as the land use it stands in does.
/// </summary>
/// <remarks>
/// Facts and no rule: what each is drawn as is the scanner's (<c>FootprintUses.LookOf</c>, <see cref="SurveyFootprints.Look"/>).
/// </remarks>
internal enum FootprintUse : byte
{
    /// <summary>Nothing says: <c>building=yes</c> or a machine-traced outline, standing in no land use that tells.</summary>
    Unknown,

    /// <summary>A home whose tags or land use say no more than that — a house or a block of flats.</summary>
    Residential,

    House,
    Apartments,

    /// <summary>A lock-up or a row of them, a carport, or anything standing in a garage cooperative's ground.</summary>
    Garages,

    /// <summary>An outbuilding: a shed, a hut, a guardhouse, a service building.</summary>
    Shed,

    /// <summary>A kiosk, or anything standing in a market.</summary>
    Kiosk,

    Retail,

    /// <summary>Offices, civic and public buildings, hotels and stations.</summary>
    Office,

    /// <summary>Industry, warehouses, hangars and silos, or anything standing in industrial or port ground.</summary>
    Industrial,

    /// <summary>Schools, kindergartens, colleges and universities.</summary>
    School,
    Hospital,
    Religious,

    /// <summary>A roof on posts with no walls (<c>building=roof</c>).</summary>
    Canopy,
    Greenhouse,
}

/// <summary>
/// <b>The surveyed place's buildings as the enrichment's layers hold them</b>, in a map's own metres: each footprint's
/// rings, flat with offsets beside them — the outline first, then any courtyard cut out of it, none closed on its first
/// point — its height, what it is for and what it is drawn as. <b>Never the map's</b>: a stump is read against them
/// (<see cref="Map.TownMap.Stumps"/>) and a zone is measured off them by the scanner, and a probe weighs the town laid
/// against them (<see cref="File"/>); opening a map reads none of it.
/// </summary>
/// <remarks>
/// <b>Written in decimetres</b>, each place its step from the one before, a footprint's look, use and height first —
/// what the probe needs of the place, at a precision no building is drawn to anyway.
/// </remarks>
internal sealed class SurveyFootprints
{
    /// <summary>What the file beside a traced map's layers is called.</summary>
    public const string File = "footprints.bin";

    /// <summary>"TFPR", first in the file.</summary>
    const uint Magic = 0x52504654;

    const ushort Version = 1;

    const double StepsPerM = 10.0;

    /// <summary>Count + 1 entries, over the rings: footprint i's are <c>RingOffsets[i]..RingOffsets[i + 1]</c>.</summary>
    public required int[] RingOffsets { get; init; }

    /// <summary>One entry a ring + 1, over <see cref="PointM"/>.</summary>
    public required int[] PointOffsets { get; init; }

    public required Vector2[] PointM { get; init; }

    /// <summary>How tall it stands, as OSM tags it or its levels make it, or nought where nothing says.</summary>
    public required float[] HeightM { get; init; }

    public required FootprintUse[] Use { get; init; }

    /// <summary>What each is drawn as, off its use, its height and the ground it covers.</summary>
    public required BuildingLook[] Look { get; init; }

    public int Count => Use.Length;

    public ReadOnlySpan<Vector2> RingOf(int ring) =>
        PointM.AsSpan(PointOffsets[ring], PointOffsets[ring + 1] - PointOffsets[ring]);

    public static SurveyFootprints None => new() { RingOffsets = [0], PointOffsets = [0], PointM = [], HeightM = [], Use = [], Look = [] };

    public void Write(string path)
    {
        var partial = path + ".part";
        using (var file = System.IO.File.Create(partial))
        using (var writer = new BinaryWriter(new BufferedStream(file, 1 << 16)))
        {
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write7BitEncodedInt(Count);
            writer.Write7BitEncodedInt(PointOffsets.Length - 1);
            writer.Write7BitEncodedInt(PointM.Length);
            var (x, y) = (0, 0);
            for (var footprint = 0; footprint < Count; footprint++)
            {
                writer.Write((byte)Look[footprint]);
                writer.Write((byte)Use[footprint]);
                writer.Write7BitEncodedInt(checked((int)Math.Round(HeightM[footprint] * StepsPerM)));
                writer.Write7BitEncodedInt(RingOffsets[footprint + 1] - RingOffsets[footprint]);
                for (var ring = RingOffsets[footprint]; ring < RingOffsets[footprint + 1]; ring++)
                {
                    var points = RingOf(ring);
                    writer.Write7BitEncodedInt(points.Length);
                    foreach (var pointM in points)
                    {
                        var (stepX, stepY) = (checked((int)Math.Round(pointM.X * StepsPerM)), checked((int)Math.Round(pointM.Y * StepsPerM)));
                        (var dx, var dy) = (stepX - x, stepY - y);
                        writer.Write7BitEncodedInt((dx << 1) ^ (dx >> 31));
                        writer.Write7BitEncodedInt((dy << 1) ^ (dy >> 31));
                        (x, y) = (stepX, stepY);
                    }
                }
            }
        }

        System.IO.File.Move(partial, path, overwrite: true);
    }

    public static SurveyFootprints Read(string path)
    {
        using var reader = new BinaryReader(new BufferedStream(System.IO.File.OpenRead(path), 1 << 16));
        if (reader.ReadUInt32() != Magic || reader.ReadUInt16() != Version) throw new InvalidDataException($"{path}: not footprints of version {Version}.");

        var (count, rings, points) = (reader.Read7BitEncodedInt(), reader.Read7BitEncodedInt(), reader.Read7BitEncodedInt());
        var (ringOffsets, pointOffsets, pointM) = (new int[count + 1], new int[rings + 1], new Vector2[points]);
        var (heightM, use, look) = (new float[count], new FootprintUse[count], new BuildingLook[count]);
        var (ring, point, x, y) = (0, 0, 0, 0);
        for (var footprint = 0; footprint < count; footprint++)
        {
            look[footprint] = (BuildingLook)reader.ReadByte();
            use[footprint] = (FootprintUse)reader.ReadByte();
            heightM[footprint] = reader.Read7BitEncodedInt() / (float)StepsPerM;
            for (var its = reader.Read7BitEncodedInt(); its > 0; its--)
            {
                for (var at = reader.Read7BitEncodedInt(); at > 0; at--)
                {
                    x += Signed(reader.Read7BitEncodedInt());
                    y += Signed(reader.Read7BitEncodedInt());
                    pointM[point++] = new Vector2((float)(x / StepsPerM), (float)(y / StepsPerM));
                }

                pointOffsets[++ring] = point;
            }

            ringOffsets[footprint + 1] = ring;
        }

        if (ring != rings || point != points) throw new InvalidDataException($"{path}: {rings} rings and {points} points read as {ring} and {point}.");

        return new SurveyFootprints { RingOffsets = ringOffsets, PointOffsets = pointOffsets, PointM = pointM, HeightM = heightM, Use = use, Look = look };

        static int Signed(int value) => (int)((uint)value >> 1) ^ -(value & 1);
    }
}
