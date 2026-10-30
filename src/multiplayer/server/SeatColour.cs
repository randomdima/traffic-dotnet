namespace TrafficSimulation.Multiplayer.Server;

/// <summary>
/// <b>The colour a player asked for, made one a map can show</b> (MP-12, MP-15): its hue kept, its saturation and its
/// brightness raised to at least the least the figures allow, so no player is a grey or a white that reads as a road, or
/// a black that reads as nothing.
/// </summary>
internal static class SeatColour
{
    /// <summary><paramref name="rgb"/>, <c>0xRRGGBB</c>, with its saturation and value at least those given, each a share of one.</summary>
    public static uint Vivid(uint rgb, float leastSaturation, float leastValue)
    {
        var r = ((rgb >> 16) & 0xFF) / 255f;
        var g = ((rgb >> 8) & 0xFF) / 255f;
        var b = (rgb & 0xFF) / 255f;
        var most = MathF.Max(r, MathF.Max(g, b));
        var least = MathF.Min(r, MathF.Min(g, b));
        var chroma = most - least;

        var hue = chroma <= 0f ? 0f
            : most == r ? (g - b) / chroma
            : most == g ? 2f + (b - r) / chroma
            : 4f + (r - g) / chroma;
        hue = (hue % 6f + 6f) % 6f;
        var saturation = MathF.Max(most <= 0f ? 0f : chroma / most, leastSaturation);
        var value = MathF.Max(most, leastValue);

        var c = value * saturation;
        var x = c * (1f - MathF.Abs(hue % 2f - 1f));
        var (r1, g1, b1) = (int)hue switch
        {
            0 => (c, x, 0f),
            1 => (x, c, 0f),
            2 => (0f, c, x),
            3 => (0f, x, c),
            4 => (x, 0f, c),
            _ => (c, 0f, x),
        };

        var floor = value - c;
        return (Byte(r1 + floor) << 16) | (Byte(g1 + floor) << 8) | Byte(b1 + floor);
    }

    static uint Byte(float share) => (uint)MathF.Round(Math.Clamp(share, 0f, 1f) * 255f);
}
