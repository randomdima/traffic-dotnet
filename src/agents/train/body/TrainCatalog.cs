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

    /// <summary>Its length over the buffers and its width, which is what it is drawn at.</summary>
    public required Vector2 FootprintM { get; init; }

    /// <summary>How far its buffers stand out past its body at each end: inside its footprint, and never collided.</summary>
    public required float BufferM { get; init; }

    public float CornerRadiusM { get; init; }

    /// <summary>
    /// Each bogie's pivot, ahead of the body's middle along it, front first (TRN-8): each is one wheel held on the rail
    /// (<see cref="TrainVehicle.MostBogies"/> at most).
    /// </summary>
    public required float[] BogiesM { get; init; }

    public required float MassKg { get; init; }

    /// <summary>The most its engine pulls with at the rail, shared among its bogies; nought for a vehicle with none.</summary>
    public float TractiveEffortN { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true, UseStringEnumConverter = true, Converters = [typeof(Vector2Json)])]
[JsonSerializable(typeof(TrainVehicleFile))]
internal sealed partial class TrainVehicleJson : JsonSerializerContext;

/// <summary>One vehicle a train may be made of, as the catalogue reads it.</summary>
/// <param name="SpritePath">Where its picture is, or null where it has none yet.</param>
internal readonly record struct TrainVehicle(
    string Id, VehicleKind Kind, string? SpritePath, Vector2 FootprintM, float BufferM, float CornerRadiusM, float[] BogiesM,
    float MassKg, float TractiveEffortN)
{
    /// <summary>The most bogies a vehicle stands on: a wheel row a bogie, and four rows a body on the device as a car's.</summary>
    public const int MostBogies = 4;

    /// <summary>
    /// <b>What it is collided as</b>: its footprint short of its buffers at both ends, so coupled vehicles meet buffer to
    /// buffer (<see cref="BufferM"/>) and not body to body.
    /// </summary>
    public Vector2 BodyM => FootprintM - new Vector2(2f * BufferM, 0f);

    /// <summary>
    /// How far its body reaches out past its line on the tightest curve a track is laid round (RAIL-2): the outside at its
    /// ends, the inside in its middle between the bogies — what its ribbon has to hold beside its width.
    /// </summary>
    public float OverhangM(float radiusM)
    {
        var pitchM = BogiesM[0] - BogiesM[^1];
        var middleM = pitchM * pitchM / (8f * radiusM);
        var endsM = ((FootprintM.X * FootprintM.X) - (pitchM * pitchM)) / (8f * radiusM);
        return MathF.Max(middleM, endsM);
    }
}

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
                ref readonly var one = ref vehicles[vehicle];
                ref readonly var first = ref vehicles[of[0]];
                if (one.FootprintM != first.FootprintM || one.BufferM != first.BufferM || one.CornerRadiusM != first.CornerRadiusM
                    || !one.BogiesM.AsSpan().SequenceEqual(first.BogiesM)
                    || one.MassKg != first.MassKg || one.TractiveEffortN != first.TractiveEffortN)
                {
                    throw new InvalidDataException($"{one.Id} is not the vehicle {first.Id} is, the {(VehicleKind)kind} it is a look of.");
                }

                if (one.BogiesM.Length is 0 or > TrainVehicle.MostBogies)
                {
                    throw new InvalidDataException($"{one.Id} stands on {one.BogiesM.Length} bogies, not 1 to {TrainVehicle.MostBogies}.");
                }

                if (one.BufferM < 0f || one.BodyM.X <= 0f)
                {
                    throw new InvalidDataException($"{one.Id}'s buffers stand {one.BufferM} m out of a {one.FootprintM.X} m footprint.");
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

    /// <summary>
    /// <b>How wide a rail lane's ribbon is</b> (RAIL-9): the widest vehicle and the most any vehicle reaches past its line on
    /// the tightest curve a track is laid round, either side — so every vehicle on a lane stands on its ribbon.
    /// </summary>
    public float RibbonWidthM(RailFigures rail)
    {
        var widthM = 0f;
        foreach (var vehicle in Vehicles) widthM = MathF.Max(widthM, vehicle.FootprintM.Y + (2f * vehicle.OverhangM(rail.LeastRadiusM)));
        return widthM;
    }

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
                file.BufferM, file.CornerRadiusM, file.BogiesM, file.MassKg, file.TractiveEffortN);
        }

        return new TrainCatalog(vehicles);
    }
}
