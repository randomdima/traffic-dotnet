using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace TrafficSimulation.Agents.Car.Body;

/// <summary>
/// Cuts the town's lamp sheet out of the cars themselves: for every lens a variant draws, the section
/// of that variant's own sprite it is measured off, driven emissive in each colour that lens can burn
/// (CAR-14a). Laid out by <see cref="LampAtlas"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>A workshop step, never a build step.</b> It is run when a variant's art or its lens rectangles
/// change, and the sheet it writes is committed beside the sprites it was cut from. The runtime reads
/// that file and knows nothing about this.
/// </para>
/// <para>
/// <b>The cut is the whole of the shape.</b> Nothing here draws a lens — no rounded corner, no bezel, no
/// highlight — because the artist already drew all three at the fleet's own resolution, and a lamp that
/// invents them lands a second pixel grid on top of the first. What is applied is light: each texel's
/// own luminance carried up a ramp from a dim <see cref="Dimmest"/> of the lamp's colour, through the
/// colour itself, to a near-white filament — so glass takes the colour and the brightest texel becomes
/// the bulb, while a dark bezel goes faint rather than dark (<see cref="Lit"/>).
/// </para>
/// </remarks>
internal static class LampAtlasBake
{
    /// <summary>
    /// Where the ramp reaches the lamp's own colour, in both what a texel burns and how solidly it is
    /// drawn. Below it a texel is that colour at a fraction and thinner with it; above it the colour
    /// washes out towards the filament. <b>It is therefore how much of a lamp is its colour rather than
    /// white</b>: the span above it is the bulb, and a lamp of a dozen texels has no room for a large one
    /// before it reads as a white blob.
    /// </summary>
    const float Knee = 0.86f;

    /// <summary>
    /// How much of the lamp's colour the bottom of the ramp still burns. <b>A lit lens is never painted
    /// black</b>: every texel of it is drawn over the lamp's own glow, and a texel darker than the light
    /// behind it is a hole in that light rather than a shadow on it — a lamp casts neither. What the
    /// shading of a lit lens is carried in is how opaque it is, which is <see cref="Lit"/>'s alpha.
    /// </summary>
    public const float Dimmest = 0.45f;

    /// <summary>
    /// How far in from the cut's own edge it comes up to full strength. <b>The rectangle is not a thing
    /// on the car</b>: where it stops, the picture goes on — the same panel, lit by the lamp's spill
    /// instead of by the cut — and a cut that ended square drew that seam as a hard line round every lit
    /// lamp. Only the edge fades; what the artist painted inside it is the lamp and stays as it is.
    /// </summary>
    const float EdgePx = 2.5f;

    /// <summary>
    /// And how much of the lens that fade may take, since a lamp six texels across has no room for a
    /// two-texel border and would come out a rim with a hole in it.
    /// </summary>
    const float EdgeShare = 0.25f;

    /// <summary>What the filament is, which is not quite white: a bulb photographs as its own colour's brightest corner.</summary>
    static readonly Vector3 Filament = new(1f, 0.97f, 0.92f);

    /// <summary>How far apart two colours have to be, over the RGB cube's 441, before one is not the other.</summary>
    const float ApartOf255 = 70f;

    /// <summary>How coarsely a sprite's colours are counted when asking which of them it is mostly made of.</summary>
    const int BucketPx = 8;

    /// <summary>And how much of a car a colour has to cover to be one the car is made of rather than a detail on it.</summary>
    const float CommonShare = 0.02f;

    /// <summary>One lens cut and lit, as the report prints it.</summary>
    public readonly record struct CutLamp(string Variant, CarLampFitting Fitting, int WidthPx, int HeightPx, float Distinct);

    /// <summary>
    /// Writes the sheet and returns what went into it, a line a lens. <b>The distinctness is the
    /// instrument</b>: a rectangle over bodywork nobody painted a lamp on cuts the paint that surrounds
    /// it and comes back near zero, and that is a sprite to finish rather than a number to adjust.
    /// </summary>
    public static List<CutLamp> Write(CarCatalog catalogue, string path)
    {
        var rows = catalogue.SheetCount;
        using var atlas = new Image<Rgba32>(LampAtlas.Columns * LampAtlas.CellPx, rows * LampAtlas.CellPx);
        var cut = new List<CutLamp>();

        for (var variant = 0; variant < rows; variant++)
        {
            var entry = catalogue.Variants[variant];
            using var sprite = Image.Load<Rgba32>(entry.SpritePath);
            var perM = new Vector2(sprite.Width / entry.FootprintM.X, sprite.Height / entry.FootprintM.Y);
            var common = Common(sprite);

            var lenses = entry.Lenses;
            for (var lens = 0; lens < lenses.Length; lens++)
            {
                var centrePx = ((entry.FootprintM * 0.5f) + lenses[lens].AtBodyM) * perM;
                var rect = RectOf(lenses[lens], centrePx, perM, sprite.Size);
                cut.Add(new CutLamp(
                    entry.Id, lenses[lens].Fitting, rect.Width, rect.Height, Distinct(sprite, rect, common)));

                for (var state = 0; state < LampAtlas.StatesOf(lenses[lens].Fitting); state++)
                {
                    var colour = LampAtlas.ColourAt(lenses[lens].Fitting, state);
                    Paste(
                        sprite, rect, centrePx, atlas, CellOf(variant, lens, state), LampAtlas.ColourOf(colour));
                }
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        atlas.SaveAsPng(path);
        return cut;
    }

    /// <summary>
    /// The lens as whole texels of the sprite it is measured off. <b>Both edges are rounded to the grid
    /// rather than one edge and a width</b>, so the cut straddles the centre the variant authored to
    /// inside half a texel — which is 5 mm of car, under the resolution either of them is drawn at. A
    /// width rounded on its own puts the cut's centre up to a whole texel off that.
    /// </summary>
    static Rectangle RectOf(CarLens lens, Vector2 centrePx, Vector2 perM, Size sprite)
    {
        var halfPx = lens.SizeM * 0.5f * perM;

        var x0 = (int)MathF.Round(centrePx.X - halfPx.X);
        var y0 = (int)MathF.Round(centrePx.Y - halfPx.Y);
        var width = Math.Max(1, (int)MathF.Round(centrePx.X + halfPx.X) - x0);
        var height = Math.Max(1, (int)MathF.Round(centrePx.Y + halfPx.Y) - y0);

        // A rectangle reaching past the picture is a lens measured off the edge of the car; it is cut to
        // what is there rather than refused, and the flat contrast it reports is what says so.
        x0 = Math.Clamp(x0, 0, Math.Max(0, sprite.Width - 1));
        y0 = Math.Clamp(y0, 0, Math.Max(0, sprite.Height - 1));
        return new Rectangle(x0, y0, Math.Min(width, sprite.Width - x0), Math.Min(height, sprite.Height - y0));
    }

    /// <summary>Where a cell's top-left texel is in the sheet.</summary>
    static Point CellOf(int variant, int lens, int state) => new(
        (((lens * LampAtlas.StatesPerLens) + state) * LampAtlas.CellPx), variant * LampAtlas.CellPx);

    /// <summary>
    /// The cut, lit, laid in its cell around the lens centre the variant authored — which is where the
    /// quad that draws the cell is centred, so the lit lamp lands on the texels of the dull one. The cell
    /// is larger than every lens the fleet draws, so what surrounds the lamp is transparent: the bodywork
    /// around it is the car's own quad, already drawn.
    /// </summary>
    /// <remarks>
    /// <b>The ramp is spread over the cut's own range of light and not over the whole scale</b>, because
    /// what a lens is painted at is the artist's business: a dark red cluster on a black coupé and a
    /// bright one on a white ambulance are both a bezel, a glass and a highlight, and both have to burn.
    /// </remarks>
    static void Paste(
        Image<Rgba32> sprite, Rectangle rect, Vector2 centrePx, Image<Rgba32> atlas, Point cell, Vector4 colour)
    {
        var intoX = cell.X + AnchorIn(rect.Width, centrePx.X - rect.X);
        var intoY = cell.Y + AnchorIn(rect.Height, centrePx.Y - rect.Y);
        var (least, most) = Range(sprite, rect);
        var span = MathF.Max(most - least, 1e-3f);
        var tint = new Vector3(colour.X, colour.Y, colour.Z);
        var edge = MathF.Min(EdgePx, Math.Min(rect.Width, rect.Height) * EdgeShare);

        for (var y = 0; y < rect.Height; y++)
        {
            for (var x = 0; x < rect.Width; x++)
            {
                var texel = sprite[rect.X + x, rect.Y + y];
                var carried = Math.Clamp((Luminance(texel) - least) / span, 0f, 1f);
                var inFromEdge = Math.Min(Math.Min(x, y), Math.Min(rect.Width - 1 - x, rect.Height - 1 - y));
                atlas[intoX + x, intoY + y] = Lit(
                    texel.A, carried, tint, SmoothStep(0f, edge, inFromEdge + 0.5f));
            }
        }
    }

    /// <summary>
    /// Where a cut's first texel goes along one axis of its cell, so that the lens centre inside the cut
    /// falls on the cell's own centre — which is where the quad that draws the cell is centred. It comes
    /// out whole because <see cref="CarLens.AtBodyM"/> is on the art's texel grid; the rounding is what
    /// float arithmetic leaves behind. Held inside the cell, which a lens as wide as one can otherwise
    /// leave by the texel that costs.
    /// </summary>
    static int AnchorIn(int extent, float centreInCut) => Math.Clamp(
        (int)MathF.Round((LampAtlas.CellPx * 0.5f) - centreInCut), 0, LampAtlas.CellPx - extent);

    /// <summary>
    /// One texel of bodywork, burning. Its place in the cut's own range of light is the whole of the
    /// shading — the shape, the bezel and the highlight the artist drew survive the change of colour,
    /// which is why a lit lamp is still recognisably the lamp that was there when it was dark.
    /// </summary>
    /// <remarks>
    /// <b>How lit a texel is, is how opaque it is</b>, and the ramp to full opacity is the ramp to the
    /// lamp's own colour: nothing is drawn solid until it is burning that colour, so what a lens shades
    /// with is the glow it lets through rather than a darkness of its own. The darkest texels of a lens
    /// are the outline the artist drew around it, and an outline is not a thing that lights — it is
    /// already on screen in the car's own picture, with the lamp's spill falling across it.
    /// </remarks>
    /// <param name="atEdge">How much of the cut is here rather than at its fading rim (<see cref="EdgePx"/>).</param>
    static Rgba32 Lit(byte alpha, float carried, Vector3 colour, float atEdge)
    {
        var lit = carried < Knee
            ? colour * (Dimmest + ((1f - Dimmest) * carried / Knee))
            : Vector3.Lerp(colour, Filament, (carried - Knee) / (1f - Knee));

        return new Rgba32(
            (byte)Math.Clamp(lit.X * 255f, 0f, 255f),
            (byte)Math.Clamp(lit.Y * 255f, 0f, 255f),
            (byte)Math.Clamp(lit.Z * 255f, 0f, 255f),
            (byte)Math.Clamp(alpha * SmoothStep(0f, Knee, carried) * atEdge, 0f, 255f));
    }

    static float SmoothStep(float from, float to, float at)
    {
        var t = Math.Clamp((at - from) / MathF.Max(to - from, 1e-6f), 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    static float Luminance(Rgba32 texel) => ((0.30f * texel.R) + (0.59f * texel.G) + (0.11f * texel.B)) / 255f;

    /// <summary>The darkest and the brightest of a cut, over the texels the car actually covers.</summary>
    static (float Least, float Most) Range(Image<Rgba32> sprite, Rectangle rect)
    {
        var least = 1f;
        var most = 0f;
        for (var y = rect.Y; y < rect.Y + rect.Height; y++)
        {
            for (var x = rect.X; x < rect.X + rect.Width; x++)
            {
                var texel = sprite[x, y];
                if (texel.A < 128) continue;

                least = MathF.Min(least, Luminance(texel));
                most = MathF.Max(most, Luminance(texel));
            }
        }

        return most > least ? (least, most) : (0f, 1f);
    }

    /// <summary>
    /// How much of a cut is a colour the rest of the car barely uses — as close as arithmetic gets to
    /// "did somebody paint a lamp here". <b>A lamp is drawn in a colour that is nowhere else on the
    /// body</b>: paint, glass, outline, chequer and shadow are each a large share of a sprite, and a lens
    /// is a few dozen texels of something none of them is. A rectangle over bare bodywork cuts nothing
    /// but common colours and comes back near zero.
    /// </summary>
    /// <remarks>
    /// It is a figure to be looked at and never a verdict: a lamp painted in a colour the car wears
    /// elsewhere — the ambulance's red, whose stripe runs the length of it — reads low and is there.
    /// </remarks>
    static float Distinct(Image<Rgba32> sprite, Rectangle rect, List<Vector3> common)
    {
        var apart = 0;
        var counted = 0;
        for (var y = rect.Y; y < rect.Y + rect.Height; y++)
        {
            for (var x = rect.X; x < rect.X + rect.Width; x++)
            {
                var texel = sprite[x, y];
                if (texel.A < 128) continue;

                counted++;
                var here = new Vector3(texel.R, texel.G, texel.B);
                var nearest = float.MaxValue;
                foreach (var colour in common) nearest = MathF.Min(nearest, (here - colour).Length());
                if (nearest > ApartOf255) apart++;
            }
        }

        return counted > 0 ? apart / (float)counted : 0f;
    }

    /// <summary>
    /// The colours a sprite is mostly made of, quantised: every bucket holding at least
    /// <see cref="CommonShare"/> of the car. What is left over is detail — a lens, a badge, a wiper.
    /// </summary>
    static List<Vector3> Common(Image<Rgba32> sprite)
    {
        var counts = new Dictionary<int, int>();
        var opaque = 0;
        for (var y = 0; y < sprite.Height; y++)
        {
            for (var x = 0; x < sprite.Width; x++)
            {
                var texel = sprite[x, y];
                if (texel.A < 128) continue;

                opaque++;
                var bucket = ((texel.R / BucketPx) << 10) | ((texel.G / BucketPx) << 5) | (texel.B / BucketPx);
                counts[bucket] = counts.GetValueOrDefault(bucket) + 1;
            }
        }

        var common = new List<Vector3>();
        foreach (var (bucket, count) in counts)
        {
            if (count < opaque * CommonShare) continue;

            common.Add(new Vector3(
                ((bucket >> 10) & 31) * BucketPx, ((bucket >> 5) & 31) * BucketPx, (bucket & 31) * BucketPx)
                + new Vector3(BucketPx * 0.5f));
        }

        return common;
    }
}
