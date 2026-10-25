using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>A generated town, whole: every parameter and every seed it is laid with</b> (GEN-60,
/// <c>towns/briefs/&lt;Name&gt;.json</c>). A town is laid off its brief when it is opened (<see cref="TownGen"/>), so the
/// same brief is the same town on every head and every time.
/// </summary>
/// <remarks>
/// <para>
/// <b>Intent and never geometry, and never a derived figure</b> (GEN-60): a count, a share, a length, a bearing — what a
/// person would say about a place — and no river course, district outline or node. A figure a brief leaves out is the
/// engine's own (<c>SimConfig.CityGen</c>); a figure only the engine may hold, such as how big a district grows, is never
/// a brief's.
/// </para>
/// <para>
/// <b>Grouped by the stage that reads it</b> (GEN-61): the terrain reads <see cref="Sea"/> and <see cref="Rivers"/>, the
/// connectors <see cref="Connectors"/>, the town <see cref="Districts"/>, <see cref="Streets"/> and
/// <see cref="Roster"/> — and each stage draws on its own seed (<see cref="Seeds"/>), so a seed rerolled moves its stage
/// and the stages after it and nothing laid before.
/// </para>
/// <para>
/// <b>Written back whole</b> (<see cref="Write"/>): every field, defaults included, so a brief saved off the debug panel
/// is the town that was on screen. A comment in a hand-written brief does not survive a save.
/// </para>
/// </remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record TownBrief
{
    /// <summary>The catalogue name, which is also the brief's file name and what <c>--map</c> is given.</summary>
    public required string Name { get; init; }

    /// <summary>The line the start menu prints under the name.</summary>
    public required string Description { get; init; }

    public required float WidthM { get; init; }

    public required float HeightM { get; init; }

    public BriefSeeds Seeds { get; init; } = new();

    /// <summary>The sea along one edge of the map, or none (GEN-62).</summary>
    public SeaBrief? Sea { get; init; }

    public RiversBrief Rivers { get; init; } = new();

    public ConnectorsBrief Connectors { get; init; } = new();

    public DistrictsBrief Districts { get; init; } = new();

    public StreetsBrief Streets { get; init; } = new();

    public RosterBrief Roster { get; init; } = new();

    /// <summary>Refuses a brief that cannot describe a town, at the point it is read rather than half way through laying one.</summary>
    public void Check()
    {
        Positive(WidthM, nameof(WidthM));
        Positive(HeightM, nameof(HeightM));
        if (string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException("a brief with no name.");
        if (Sea is { } sea)
        {
            Share(sea.Share, "sea.share");
            if (sea.Share >= 0.5f) throw new InvalidDataException($"{Name}: a sea over half the map leaves no town.");
            NotNegative(sea.Meander, "sea.meander");
        }

        NotNegative(Rivers.PerKm, "rivers.perKm");
        Positive(Rivers.WidthM, "rivers.widthM");
        NotNegative(Rivers.Meander, "rivers.meander");
        Share(Rivers.WidthSpread, "rivers.widthSpread");
        if (Rivers.WidthSpread >= 1f) throw new InvalidDataException($"{Name}: a river narrowing by {Rivers.WidthSpread} of its width runs dry.");
        NotNegative(Rivers.Tributaries, "rivers.tributaries");
        NotNegative(Connectors.IntercityRoads, "connectors.intercityRoads");
        Share(Connectors.MinorShare, "connectors.minorShare");
        NotNegative(Connectors.Railways, "connectors.railways");
        if (Connectors.RailTracks is < 1 or > 2) throw new InvalidDataException($"{Name}: a railway of {Connectors.RailTracks} tracks; a brief lays one or two.");
        if (Connectors.RailLoopDeg != 0f && MathF.Abs(Connectors.RailLoopDeg) is < RailLines.LoopLeastDeg or > RailLines.LoopMostDeg)
        {
            throw new InvalidDataException(
                $"{Name}: a loop of {Connectors.RailLoopDeg}° does not cross its own way in; a brief turns one {RailLines.LoopLeastDeg}° to {RailLines.LoopMostDeg}°.");
        }

        Share(Districts.GridShare, "districts.gridShare");
        Share(Streets.UnregulatedShare, "streets.unregulatedShare");
        NotNegative(Roster.Buildings, "roster.buildings");
        NotNegative(Roster.People, "roster.people");
        NotNegative(Roster.Cars, "roster.cars");
    }

    void Positive(float value, string field)
    {
        if (value <= 0f) throw new InvalidDataException($"{Name}: {field} is {value}, which is no size.");
    }

    void NotNegative(float value, string field)
    {
        if (value < 0f) throw new InvalidDataException($"{Name}: {field} is {value}, which is less than none.");
    }

    void Share(float value, string field)
    {
        if (value is < 0f or > 1f) throw new InvalidDataException($"{Name}: {field} is {value}, which is not a share.");
    }

    /// <summary>A brief read off its file, and checked.</summary>
    public static TownBrief Read(string path)
    {
        var brief = JsonSerializer.Deserialize(File.ReadAllText(path), TownBriefJson.Default.TownBrief)
                    ?? throw new InvalidDataException($"{path}: no brief.");
        brief.Check();
        return brief;
    }

    /// <summary>A brief read off its text, and checked — what a head with no file of it is handed.</summary>
    public static TownBrief Parse(string json)
    {
        var brief = JsonSerializer.Deserialize(json, TownBriefJson.Default.TownBrief) ?? throw new InvalidDataException("no brief.");
        brief.Check();
        return brief;
    }

    /// <summary>The brief as its file holds it: every field written out, so it is the whole town.</summary>
    public string Json() => JsonSerializer.Serialize(this, TownBriefJson.Default.TownBrief);

    public void Write(string path) => File.WriteAllText(path, Json() + Environment.NewLine);
}

/// <summary>
/// <b>The seed every stage draws on</b> (GEN-60, GEN-11): one for the whole town, and one of its own for any stage given
/// one — which is how a stage is laid again without moving what was laid before it.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record BriefSeeds
{
    /// <summary>What every stage not given a seed of its own draws on.</summary>
    public ulong Town { get; init; } = 1;

    public ulong? Terrain { get; init; }

    public ulong? Connectors { get; init; }

    public ulong? Districts { get; init; }

    public ulong? Streets { get; init; }

    public ulong? Buildings { get; init; }

    public ulong? Props { get; init; }

    public ulong? Roster { get; init; }

    [JsonIgnore]
    public StageSeeds Resolved => new(
        Terrain ?? Town, Connectors ?? Town, Districts ?? Town, Streets ?? Town, Buildings ?? Town, Props ?? Town, Roster ?? Town);
}

/// <summary>Each stage's seed, resolved: its own where it has one and the town's where it has not.</summary>
internal readonly record struct StageSeeds(
    ulong Terrain, ulong Connectors, ulong Districts, ulong Streets, ulong Buildings, ulong Props, ulong Roster)
{
    /// <summary>One seed for every stage, which is a traced map's — its seed is its survey's (GEN-57).</summary>
    public static StageSeeds All(ulong seed) => new(seed, seed, seed, seed, seed, seed, seed);
}

/// <summary>The sea (GEN-62): which way it lies from the map's middle, how far in it reaches and how its coast wanders.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SeaBrief
{
    /// <summary>Which way from the middle of the map the sea lies.</summary>
    public float BearingDeg { get; init; }

    /// <summary>How much of the map the sea takes, as a share of the map's breadth that way.</summary>
    public float Share { get; init; } = 0.2f;

    /// <summary>How far the coast wanders off its line, as a share of the sea's own breadth.</summary>
    public float Meander { get; init; } = 0.5f;
}

/// <summary>The rivers (GEN-62).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record RiversBrief
{
    /// <summary>How many rivers cross the map for each kilometre of its long side, rounded: none is a map with no river.</summary>
    public float PerKm { get; init; }

    /// <summary>
    /// <b>Which way the rivers flow</b>, where there is no sea for them to flow into; with a sea, each flows to it.
    /// </summary>
    public float BearingDeg { get; init; }

    /// <summary>How wide a river is, on its mean. Held to what a bridge spans at its widest pool (GEN-62).</summary>
    public float WidthM { get; init; } = 60f;

    /// <summary>How far a river wanders off its line, as a share of the run it makes across the map.</summary>
    public float Meander { get; init; } = 0.05f;

    /// <summary>How far a river widens and narrows along its course, as a share of its own width either way.</summary>
    public float WidthSpread { get; init; } = 0.3f;

    /// <summary>How many smaller rivers flow into the rivers, each from past the map's edge. What fits is what is laid.</summary>
    public int Tributaries { get; init; }
}

/// <summary>The roads and railways that reach the town from outside (GEN-63).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ConnectorsBrief
{
    /// <summary>How many intercity roads enter the map, spread round its edge.</summary>
    public int IntercityRoads { get; init; } = 3;

    /// <summary>How many of them are minor — two lanes rather than four (GEN-67) — as a share.</summary>
    public float MinorShare { get; init; }

    /// <summary>How many railways cross the map, edge to edge.</summary>
    public int Railways { get; init; } = 1;

    /// <summary>How many tracks each is laid with side by side: a single track or a double (RAIL-2).</summary>
    public int RailTracks { get; init; } = 2;

    /// <summary>
    /// How far the first railway's one loop turns it (RAIL-11), signed toward the right of its bearing: none, or from
    /// <see cref="RailLines.LoopLeastDeg"/> to <see cref="RailLines.LoopMostDeg"/>, so its way out crosses its way in.
    /// </summary>
    public float RailLoopDeg { get; init; }
}

/// <summary>The districts (GEN-64…66, GEN-74).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DistrictsBrief
{
    /// <summary>How many of them are laid as a grid rather than loose, as a share (GEN-47).</summary>
    public float GridShare { get; init; } = 0.5f;
}

/// <summary>The streets (GEN-67, GEN-68).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record StreetsBrief
{
    /// <summary>
    /// How many of the junctions that could carry lights are left to the ranking instead (TER-5e): a share of the
    /// junctions where two district-or-wider roads meet (GEN-68), drawn towards the bigger (<see cref="LitJunctions"/>).
    /// </summary>
    public float UnregulatedShare { get; init; } = 0.2f;
}

/// <summary>How many of everything (GEN-6): what a town asks for, and what fits is what it gets (GEN-8).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record RosterBrief
{
    /// <summary>How many buildings the town plans (GEN-54), and what its car parks are counted off (GEN-53).</summary>
    public int Buildings { get; init; }

    /// <summary>How many people stand at its doors (GEN-7).</summary>
    public int People { get; init; }

    /// <summary>How many cars a town nobody lives in stands; where people live, every car is somebody's (GEN-7).</summary>
    public int Cars { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(TownBrief))]
internal sealed partial class TownBriefJson : JsonSerializerContext;
