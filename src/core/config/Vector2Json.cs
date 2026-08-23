using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrafficSimulation.Core.Config;

/// <summary>A <see cref="Vector2"/> as the pair <c>[x, y]</c>, which is how every measurement in an asset file is written.</summary>
internal sealed class Vector2Json : JsonConverter<Vector2>
{
    public override Vector2 Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("expected [x, y]");

        var vector = new Vector2(Number(ref reader), Number(ref reader));

        if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray)
            throw new JsonException("expected [x, y] and nothing after y");

        return vector;
    }

    public override void Write(Utf8JsonWriter writer, Vector2 vector, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(vector.X);
        writer.WriteNumberValue(vector.Y);
        writer.WriteEndArray();
    }

    static float Number(ref Utf8JsonReader reader) =>
        reader.Read() && reader.TokenType == JsonTokenType.Number
            ? reader.GetSingle()
            : throw new JsonException("expected a number in [x, y]");
}
