using TrafficSimulation.Runtime;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>A dark line round a picture's solid part, laid into its texels as it is packed</b>
/// (<see cref="SheetSource.OutlineArtPx"/>): what makes a prop read against the lawn it stands on, at no cost a frame.
/// </summary>
/// <remarks>
/// <para>
/// <b>Laid over the picture's fringe and never its solid part</b>: a soft edge is a few of the picture's pixels wide, and
/// zoomed in a line laid outside it, under it, is hidden by it.
/// </para>
/// <para>
/// <b>Solid is most of opaque</b>, so a faint fringe — a patch of ground cover meant to fade into the lawn — is not ringed.
/// </para>
/// <para>
/// <b>The file is left as it is</b>: the line is the packer's, so the picture still traces to the prompt that made it
/// (<c>raw_assets/index.md</c>), and its width is one figure rather than a pass over every sheet.
/// </para>
/// </remarks>
internal static class SheetOutline
{
    const float FaintAlpha = 0.5f;
    const float SolidAlpha = 0.9f;

    /// <summary>
    /// Lays the line <paramref name="widthPx"/> of the picture's pixels wide round its solid part, whole within that and
    /// fading out over the next pixel so its edge is not a staircase. Straight alpha in and out, as the sheets are kept.
    /// </summary>
    public static void Ring(Span<Texel> pixels, int width, int height, float widthPx)
    {
        var solid = new float[pixels.Length];
        for (var at = 0; at < pixels.Length; at++) solid[at] = Solid(pixels[at].A);

        var reach = (int)MathF.Ceiling(widthPx);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var at = (y * width) + x;
                var line = Near(solid, width, height, x, y, reach, widthPx) * (1f - solid[at]);
                if (line <= 0f) continue;

                var texel = pixels[at];
                var alpha = texel.A / 255f;
                var covered = line + (alpha * (1f - line));
                var kept = alpha * (1f - line) / covered;
                pixels[at] = new Texel(Byte(texel.R * kept), Byte(texel.G * kept), Byte(texel.B * kept), Byte(covered * 255f));
            }
        }
    }

    static float Near(float[] solid, int width, int height, int x, int y, int reach, float widthPx)
    {
        var near = 0f;
        for (var dy = -reach; dy <= reach; dy++)
        {
            var row = y + dy;
            if ((uint)row >= (uint)height) continue;

            for (var dx = -reach; dx <= reach; dx++)
            {
                var column = x + dx;
                if ((uint)column >= (uint)width) continue;

                var weight = Math.Clamp(widthPx + 1f - MathF.Sqrt((dx * dx) + (dy * dy)), 0f, 1f);
                near = MathF.Max(near, solid[(row * width) + column] * weight);
            }
        }

        return near;
    }

    static float Solid(byte alpha)
    {
        var t = Math.Clamp(((alpha / 255f) - FaintAlpha) / (SolidAlpha - FaintAlpha), 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    static byte Byte(float value) => (byte)Math.Clamp(MathF.Round(value), 0f, 255f);
}
