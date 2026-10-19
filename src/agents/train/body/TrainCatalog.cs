using System.Numerics;
using System.Text.Json.Serialization;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.Agents.Train.Body;

/// <summary>What one vehicle of a train is (TRN-4): which of the two locomotives, or which of the three wagons.</summary>
internal enum VehicleKind : byte
{
    /// <summary>An electric locomotive: an intercity train's.</summary>
    Electric,

    /// <summary>A diesel locomotive: a freight train's.</summary>
    Diesel,

    Coach,
    Tank,

    /// <summary>An open wagon heaped with coal.</summary>
    Coal,
}

/// <summary>Which of the three a train is (TRN-4).</summary>
internal enum TrainKind : byte
{
    Intercity,
    Oil,
    Coal,
}

/// <summary>One vehicle's file (<c>assets/agents/train/variants/&lt;id&gt;/&lt;id&gt;.json</c>).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class TrainVehicleFile
{
    public required string Id { get; init; }

    public required VehicleKind Kind { get; init; }

    /// <summary>What it looks like, in the words its picture was asked for in (<c>qq sprites template</c>).</summary>
    public string? Describe { get; init; }

    /// <summary>The picture it is drawn as, its front at +x, its length the footprint's; none until its art is kept.</summary>
    public string? Sprite { get; init; }

    /// <summary>Its length over the buffers and its width, which is what it is drawn at and collided as.</summary>
    public required Vector2 FootprintM { get; init; }

    public float CornerRadiusM { get; init; }

    /// <summary>Between its two bogies' pivots (TRN-8).</summary>
    public required float BogiePitchM { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true, UseStringEnumConverter = true, Converters = [typeof(Vector2Json)])]
[JsonSerializable(typeof(TrainVehicleFile))]
internal sealed partial class TrainVehicleJson : JsonSerializerContext;

/// <summary>One vehicle a train may be made of, as the catalogue reads it.</summary>
/// <param name="SpritePath">Where its picture is, or null where it has none yet.</param>
internal readonly record struct TrainVehicle(
    string Id, VehicleKind Kind, string? SpritePath, Vector2 FootprintM, float CornerRadiusM, float BogiePitchM);

/// <summary>
/// <b>The rolling stock</b>, read from <c>assets/agents/train/variants/common/Rolling.json</c>: every vehicle a train may be
/// made of, and which of them is of each kind (TRN-4).
/// </summary>
/// <remarks>
/// <b>Every vehicle of a kind is one shape</b>: a train's wagons are bodies laid once (TRN-7), a slot a vehicle of each kind,
/// and which of a kind's looks one wears is drawn as the train is sent — so a kind's looks differ in paint and never in size,
/// and a catalogue whose do is refused.
/// </remarks>
internal sealed class TrainCatalog
{
    readonly int[][] _ofKind;

    TrainCatalog(TrainVehicle[] vehicles)
    {
        Vehicles = vehicles;
        _ofKind = new int[Enum.GetValues<VehicleKind>().Length][];
        for (var kind = 0; kind < _ofKind.Length; kind++)
        {
            var of = Enumerable.Range(0, vehicles.Length).Where(vehicle => (int)vehicles[vehicle].Kind == kind).ToArray();
            if (of.Length == 0) throw new InvalidDataException($"The rolling stock has no {(VehicleKind)kind}.");

            foreach (var vehicle in of)
            {
                if (vehicles[vehicle].FootprintM != vehicles[of[0]].FootprintM || vehicles[vehicle].CornerRadiusM != vehicles[of[0]].CornerRadiusM
                    || vehicles[vehicle].BogiePitchM != vehicles[of[0]].BogiePitchM)
                {
                    throw new InvalidDataException($"{vehicles[vehicle].Id} is not the shape of {vehicles[of[0]].Id}, the {(VehicleKind)kind} it is a look of.");
                }
            }

            _ofKind[kind] = of;
        }
    }

    public TrainVehicle[] Vehicles { get; }

    /// <summary>The looks of one kind, as indices into <see cref="Vehicles"/>.</summary>
    public ReadOnlySpan<int> OfKind(VehicleKind kind) => _ofKind[(int)kind];

    /// <summary>A kind's one shape: the first of its looks.</summary>
    public ref readonly TrainVehicle ShapeOf(VehicleKind kind) => ref Vehicles[_ofKind[(int)kind][0]];

    /// <summary>The rolling stock, read once: data, the same for every town.</summary>
    public static TrainCatalog Shared { get; } = Load();

    static TrainCatalog Load()
    {
        var list = Path.Combine(ProjectPaths.Assets, "agents", "train", "variants", "common", "Rolling.json");
        var files = AssetJson.Catalog(list);
        var vehicles = new TrainVehicle[files.Length];
        for (var at = 0; at < files.Length; at++)
        {
            var file = AssetJson.Read(files[at], TrainVehicleJson.Default.TrainVehicleFile);
            vehicles[at] = new TrainVehicle(
                file.Id, file.Kind, file.Sprite is { } sprite ? AssetJson.Beside(files[at], sprite) : null, file.FootprintM,
                file.CornerRadiusM, file.BogiePitchM);
        }

        return new TrainCatalog(vehicles);
    }
}
