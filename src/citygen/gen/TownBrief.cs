using System.Text.Json.Serialization;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>What shape of water a town is laid around.</summary>
internal enum WaterKind
{
    None,

    /// <summary>A sea along one edge, its shoreline wandering about a bearing.</summary>
    Coast,

    /// <summary>A river across the town, entering one side and leaving the other.</summary>
    River,
}

/// <summary>
/// <b>A town as it is authored: a seed and the intent, never the geometry.</b> Everything a reader would
/// call the map — its streets, its blocks, its buildings, its people — is derived from this by
/// <see cref="TownGenerator"/>, so a brief is kilobytes and a town is whatever the seed makes of them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing derived may be written here.</b> No district polygon, no node, no curve, no cell: the moment
/// a brief carries geometry there are two answers to where the town is, and the one on disk is the one that
/// goes stale. What a brief may carry is what a person would say about a place — how big it is, what water
/// it stands on, how many districts and how strictly they are laid out, and how many of everything.
/// </para>
/// <para>
/// <b>Counts are the map's and figures are the engine's</b> (GEN-6): how many people live here is a fact
/// about this town, and how far apart props stand is a fact about towns. A count a town cannot afford is
/// clamped to what its ground affords and reported, never retried.
/// </para>
/// </remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class TownBrief
{
    /// <summary>The catalogue name, which is also the brief's file name and what <c>--map</c> is given.</summary>
    public required string Name { get; init; }

    /// <summary>The line the start menu prints under the name.</summary>
    public required string Description { get; init; }

    /// <summary>The world seed: the same brief at the same seed is the same town, every time it is opened (GEN-1).</summary>
    public required ulong Seed { get; init; }

    public required float WidthM { get; init; }

    public required float HeightM { get; init; }

    /// <summary>How coarse the terrain classification is. A metre, as every shipped map has been.</summary>
    public float CellSizeM { get; init; } = 1f;

    public WaterKind Water { get; init; } = WaterKind.None;

    /// <summary>Which way the coast runs, or which way the river flows across the town.</summary>
    public float WaterBearingDeg { get; init; }

    /// <summary>How much of the town the water takes, as a share of its short side.</summary>
    public float WaterShare { get; init; } = 0.2f;

    /// <summary>How far the shoreline or the bank wanders off its own bearing, as a share of the water's own width.</summary>
    public float WaterMeander { get; init; } = 0.5f;

    /// <summary>How many districts the streets are laid in, each on its own bearing at its own spacing.</summary>
    public required int Districts { get; init; }

    /// <summary>
    /// How many of them are laid as a strict grid, as a share. A strict district's streets are very nearly
    /// chords between their junctions; the rest wander within the bound their block spacing allows.
    /// </summary>
    public float GridDistrictShare { get; init; } = 0.5f;

    /// <summary>How far a district's own bearing may stand off the town's, either way.</summary>
    public float BearingSpreadDeg { get; init; } = 40f;

    /// <summary>How much of the town's short side the orbital stands off its centre. Zero lays no orbital.</summary>
    public float RingShare { get; init; } = 0.34f;

    /// <summary>
    /// How many of the junctions that could carry lights are left to the ranking instead (TER-5e). Only a
    /// junction of three arms or more can carry any (TLT-3), so this is a share of those.
    /// </summary>
    public float UnregulatedJunctionShare { get; init; } = 0.15f;

    /// <summary>How many buildings the town stands, if its frontages afford that many.</summary>
    public required int Buildings { get; init; }

    /// <summary>And how many of its frontage slots are given over to a car park instead, as a share.</summary>
    public float ParkingSlotShare { get; init; } = 0.25f;

    public required int People { get; init; }

    public required int Cars { get; init; }

    public int Hospitals { get; init; } = 1;

    public int PoliceStations { get; init; } = 1;

    public int Depots { get; init; } = 1;

    /// <summary>Refuses a brief that cannot describe a town, at the point it is read rather than half way through laying one.</summary>
    public void Check(string what)
    {
        Positive(WidthM, nameof(WidthM), what);
        Positive(HeightM, nameof(HeightM), what);
        Positive(CellSizeM, nameof(CellSizeM), what);
        Share(GridDistrictShare, nameof(GridDistrictShare), what);
        Share(ParkingSlotShare, nameof(ParkingSlotShare), what);
        Share(UnregulatedJunctionShare, nameof(UnregulatedJunctionShare), what);
        Share(WaterShare, nameof(WaterShare), what);

        if (string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException($"{what}: a brief with no name.");
        if (Districts < 1) throw new InvalidDataException($"{what}: {Districts} districts is no town.");
        if (Buildings < 0 || People < 0 || Cars < 0) throw new InvalidDataException($"{what}: a negative roster.");
    }

    static void Positive(float value, string field, string what)
    {
        if (value > 0f) return;

        throw new InvalidDataException($"{what}: {field} is {value}, which is no size.");
    }

    static void Share(float value, string field, string what)
    {
        if (value is >= 0f and <= 1f) return;

        throw new InvalidDataException($"{what}: {field} is {value}, which is not a share.");
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(TownBrief))]
internal sealed partial class TownBriefJson : JsonSerializerContext;
