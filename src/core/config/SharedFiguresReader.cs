using System.Reflection;
using System.Text.Json;

namespace TrafficSimulation.Core.Config;

/// <summary>
/// Applies <c>assets/shared/config/SimConfig.json</c> over the shipped figures — the one place a figure
/// is retuned without a code change. The groups nest as they do on <see cref="SimConfig"/>.
/// </summary>
/// <remarks>
/// <para>
/// Startup only, so reflection is the right price for refusing to carry a second list of names. An
/// unknown key throws rather than being ignored: a figure silently dropped is exactly the drift this
/// file exists to prevent, and a <b>derived</b> figure is refused for the same reason — it is a get-only
/// property, and a binder that skipped it would leave the author believing the override took.
/// </para>
/// <para>
/// That refusal is why this is hand-written rather than <c>ConfigurationBinder</c>, which cannot tell a
/// property it may not write from one it has already written.
/// </para>
/// </remarks>
internal static class SharedFiguresReader
{
    public static SimConfig Apply(SimConfig config, string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"The shared figures are missing: {path}");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                File.ReadAllBytes(path),
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException failure)
        {
            throw new FormatException($"{path}: {failure.Message}", failure);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new FormatException($"{path}: the figures are an object of groups.");

            Apply(config, document.RootElement, "", path);
        }

        return config;
    }

    static void Apply(object owner, JsonElement group, string prefix, string path)
    {
        foreach (var member in group.EnumerateObject())
        {
            var key = prefix.Length == 0 ? member.Name : $"{prefix}.{member.Name}";
            var figure = Property(owner, member.Name, key, path);

            if (member.Value.ValueKind == JsonValueKind.Object)
            {
                Apply(
                    figure.GetValue(owner) ?? throw new FormatException($"{path}: {key} names a group that is not there."),
                    member.Value, key, path);
                continue;
            }

            if (!figure.CanWrite)
                throw new FormatException($"{path}: {key} is derived from other figures and cannot be overridden.");

            figure.SetValue(owner, Read(figure.PropertyType, member.Value, key, path));
        }
    }

    static PropertyInfo Property(object owner, string name, string key, string path) =>
        owner.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
        ?? throw new FormatException(
            $"{path}: {key} is not a figure this engine holds. Add it to SimConfig or take it out of the file.");

    static object Read(Type type, JsonElement value, string key, string path)
    {
        if (type == typeof(bool) && value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return value.GetBoolean();

        if (value.ValueKind == JsonValueKind.Number)
        {
            if (type == typeof(int) && value.TryGetInt32(out var whole)) return whole;
            if (type == typeof(float) && value.TryGetSingle(out var real)) return real;
        }

        throw new FormatException($"{path}: {key} = {value} is not a {type.Name}.");
    }
}
