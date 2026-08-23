using System.Text.Json.Serialization;

namespace TrafficSimulation.Agents.Person.Body;

/// <summary>One walker's look as its file is written. Only the look is data; the pace is everyone's.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class PersonVariantFile
{
    public required string Id { get; init; }

    public required string Sheet { get; init; }

    public required float HeightM { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(PersonVariantFile))]
internal sealed partial class PersonVariantJson : JsonSerializerContext;
