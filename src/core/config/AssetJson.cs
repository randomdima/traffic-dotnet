using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace TrafficSimulation.Core.Config;

/// <summary>A catalogue: the variant files it lists, each named relative to the catalogue itself.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class CatalogFile
{
    public required string[] Variants { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(CatalogFile))]
internal sealed partial class CatalogJson : JsonSerializerContext;

/// <summary>
/// Reading the files under <c>assets/</c> that describe what the town is drawn from — the catalogues and
/// the variants they list. Startup only; nothing here runs inside a frame.
/// </summary>
/// <remarks>
/// <para>
/// <b>A path inside one of these files is relative to that file's own folder</b>, so a variant folder
/// names its own art and stays correct wherever the folder is moved to. <see cref="Beside"/> is the one
/// place a path is resolved.
/// </para>
/// <para>
/// The shapes live with the slices that read them and the serialisers are source-generated, so no
/// reflection is involved and adding a field is a change to one record. This knows how to read a file
/// and what to say when it cannot; it does not know what a car or a roof is.
/// </para>
/// </remarks>
internal static class AssetJson
{
    /// <summary>Reads one asset file into <typeparamref name="T"/>, faulting with the file's own path.</summary>
    /// <param name="shape">The source-generated type info, from the reading slice's own context.</param>
    public static T Read<T>(string path, JsonTypeInfo<T> shape)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"No asset file at {path}.");

        using var stream = File.OpenRead(path);
        try
        {
            return JsonSerializer.Deserialize(stream, shape)
                   ?? throw new InvalidDataException($"{path}: holds null rather than an asset.");
        }
        catch (JsonException failure)
        {
            throw new InvalidDataException($"{path}: {failure.Message}", failure);
        }
    }

    /// <summary>
    /// The variant files a catalogue lists, resolved and <b>in file order</b> — that order is the
    /// catalogue's meaning, since a variant's index is where it sits in the list.
    /// </summary>
    public static string[] Catalog(string path)
    {
        var listed = Read(path, CatalogJson.Default.CatalogFile).Variants;
        if (listed.Length == 0) throw new InvalidDataException($"{path} lists no variants.");

        var entries = new string[listed.Length];
        for (var entry = 0; entry < listed.Length; entry++) entries[entry] = Beside(path, listed[entry]);

        return entries;
    }

    /// <summary>The absolute path a relative one names, resolved against the asset file that named it.</summary>
    public static string Beside(string assetFile, string relative) =>
        Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(assetFile)!, relative.Replace('/', Path.DirectorySeparatorChar)));
}
