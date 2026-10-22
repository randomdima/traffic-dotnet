using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.App.Render;

/// <summary>
/// The lamps a car is showing (CAR-14), as instances of the same quad everything else is drawn with —
/// laid over the bodies, because a lamp is a light on the panels and not a thing standing beside them.
/// </summary>
/// <remarks>
/// <para>
/// <b>A lit lamp is the car's own section of picture, burning</b> (CAR-14a). The lens is the piece of
/// bodywork the variant's art draws one on (<see cref="CarLens"/>), and the sheet holds the texels its zone map
/// paints as the lens, cut from that very sprite and driven emissive (<see cref="LampAtlasBake"/>). So a lamp lands
/// on the texel grid the body is drawn on, wears the bezel the artist gave it, and cannot overhang an
/// outline it was cut from the inside of.
/// </para>
/// <para>
/// <b>An unlit lamp is drawn by nobody</b>, because it is already on screen: the dull lens is part of the
/// car's own picture. Drawing one here would be a second copy of it, and two pictures of one lens that
/// agree about neither resolution nor shape read as a sticker on the car.
/// </para>
/// <para>
/// <b>One sheet for every lamp in the town</b> — a row a variant, two columns a lens — so which lamp a
/// quad is costs texture coordinates rather than a bind and the frame's crossings do not count them
/// (TEC-1). The glow that spills onto the bodywork around a lit one is built here, because it is a
/// gradient and art would only be a slower way of writing one.
/// </para>
/// <para>
/// <b>Where a lamp is and whether it is lit are the car's</b> (<see cref="CarLamps"/>); what is here is
/// only which cell of the sheet says so.
/// </para>
/// <para>
/// <b>A car's glows are all written before any of its lenses</b>, since instances are drawn in the order
/// they are laid down. A glow belongs under every lens on the body and not merely under its own — two
/// lamps close enough for their spill to meet are one lamp, and a bar whose ends washed each other by
/// turns is the bug that says so.
/// </para>
/// </remarks>
internal static class LampSprites
{
    /// <summary>Across the glow, both ways. Small: it is a gradient read through a linear sampler.</summary>
    const int GlowSamples = 32;

    /// <summary>What share of the glow's radius is its core rather than the fade around it.</summary>
    const float GlowCoreShare = 0.32f;

    /// <param name="leastGlowM">The narrowest glow this frame draws; a lamp whose glow is narrower is left out, lens and all.</param>
    /// <param name="drawn">The cars a frame may show, in the order drawn (<see cref="CarSprites"/>).</param>
    /// <remarks>
    /// <b>What each car says is what its tick worked out</b> (<see cref="CarFleet.Lamps"/>), the same set the world is
    /// handed — hand at the wheel and all (CTL-5c) — so a frame never answers it a second way.
    /// </remarks>
    public static int Fill(
        CarFleet cars, ReadOnlySpan<int> drawn, CarCatalog catalogue, SimConfig config, int lensSheet, int glowSheet,
        float elapsedS, Vector2 viewCentreM, Vector2 viewSpanM, Span<SpriteInstance> into, int level = CarSprites.EveryLevel,
        float leastGlowM = 0f)
    {
        var written = 0;
        var halfView = viewSpanM * 0.5f;
        var lamps = config.Lamps;
        var rows = catalogue.SheetCount;
        var halfCellM = new Vector2(LampAtlas.CellM * 0.5f);
        Span<ShownLamp> shown = stackalloc ShownLamp[CarLamps.MostLenses];
        Span<float> glowsM = stackalloc float[CarLamps.MostLenses];

        foreach (var car in drawn)
        {
            // A wreck's art is its own crumpled picture, which the lenses of the car it was are not
            // measured against — and a wreck shows nothing anyway (CAR-14.5).
            if (cars.Broken[car] || !CarSprites.IsOn(cars, car, level)) continue;

            var variant = cars.Variant[car] % rows;
            var lenses = catalogue.LensesOf(variant);
            if (lenses.IsEmpty) continue;

            // A glow and a lens a lens at the most: the room is laid for the catalogue's most lenses on one look.
            if (written + (2 * lenses.Length) > into.Length) break;

            var centreM = cars.PositionM[car];
            ref readonly var build = ref cars.BuildOf(car);
            var reachM = new Vector2(build.LengthM, build.WidthM).Length() * 0.5f;
            var offset = centreM - viewCentreM;
            if (MathF.Abs(offset.X) > halfView.X + reachM || MathF.Abs(offset.Y) > halfView.Y + reachM) continue;

            var count = CarLamps.Shown(lenses, cars.Lamps[car], config, elapsedS, cars.FuseJitter[car], shown);

            var headingRad = cars.HeadingRad[car];
            var forward = Heading.Unit(headingRad);
            var right = new Vector2(-forward.Y, forward.X);

            // The glow under the lens rather than over it: a lamp is a lit lens with light around it,
            // and a fade laid on top of the lens washes the lens out.
            for (var lamp = 0; lamp < count; lamp++)
            {
                // A dull lens is the car's own picture and is already drawn (CAR-14a). Nothing is owed
                // to a lamp that is off.
                if (!shown[lamp].Lit) continue;

                var lens = shown[lamp].Lens;
                var glowM = glowsM[lamp] = GlowM(lens, config);
                if (glowM < leastGlowM || !Glows(shown[lamp].Colour)) continue;

                into[written++] = new SpriteInstance(
                    centreM + (forward * lens.AtBodyM.X) + (right * lens.AtBodyM.Y),
                    new Vector2(glowM * 0.5f), SpriteCell.Whole, GlowTint(shown[lamp].Colour, config), (uint)glowSheet,
                    headingRad);
            }

            // Every glow on the car before any of its lenses, rather than a glow and a lens a lamp at a
            // time: the second way lays the next lamp's spill over the last one's glass, and a pair of
            // lamps a hand's width apart came out one washed and one crisp.
            for (var lamp = 0; lamp < count; lamp++)
            {
                if (!shown[lamp].Lit || glowsM[lamp] < leastGlowM) continue;

                // The cell carries the lamp's colour and its shading both, so the lens is drawn
                // untinted: what a lit lamp looks like is the car's own art and not a tint over it.
                var lens = shown[lamp].Lens;
                var state = LampAtlas.StateOf(lens.Fitting, shown[lamp].Colour);
                into[written++] = new SpriteInstance(
                    centreM + (forward * lens.AtBodyM.X) + (right * lens.AtBodyM.Y),
                    halfCellM, LensCell(variant, lamp, state, rows), SpriteTint.Plain, (uint)lensSheet, headingRad);
            }
        }

        return written;
    }

    /// <summary>
    /// Whether a lamp burning <paramref name="colour"/> lays a glow under its lens. <b>A headlamp does not</b>
    /// (CAR-14.8): it is lit glass that says the car is driven, and a glow on every moving car's nose is a town washed in
    /// bloom. Its lens is still culled by the glow it would have (<see cref="GlowM"/>), as every lens is.
    /// </summary>
    public static bool Glows(CarLamp colour) => colour != CarLamp.Head;

    /// <summary>
    /// How wide a lit lens's glow is drawn: sized off the lens the art draws and not off the cell that carries
    /// it, with the floor that keeps a lamp of a few texels visible from the height a street is watched from.
    /// </summary>
    public static float GlowM(in CarLens lens, SimConfig config) =>
        MathF.Max(MathF.Max(lens.SizeM.X, lens.SizeM.Y), config.Lamps.LeastGlowM) * config.Lamps.GlowSpread;

    /// <summary>What a glow burning <paramref name="colour"/> is tinted: the light's own colour at the glow's strength.</summary>
    public static uint GlowTint(CarLamp colour, SimConfig config) =>
        SpriteTint.Pack(LampAtlas.ColourOf(colour) with { W = config.Lamps.GlowStrength });

    /// <summary>
    /// The cell of the lens sheet a lens burning in <paramref name="state"/> is cut into (<see cref="LampAtlas"/>):
    /// <see cref="LampAtlas.CellOf"/> in a sheet of <paramref name="looks"/>.
    /// </summary>
    public static SpriteCell LensCell(int variant, int lens, int state, int looks)
    {
        var (column, row) = LampAtlas.CellOf(variant, lens, state);
        return SpriteCell.Of(column, row, LampAtlas.SheetColumns, LampAtlas.SheetRows(looks));
    }

    /// <summary>
    /// The light around a lit one: white, opaque across its core and fading out to nothing at its rim.
    /// Round rather than square, so what spills onto the bodywork reads as light on it. <b>Built rather
    /// than shipped</b>: it is a gradient, and a picture of one is a slower way of writing it down.
    /// </summary>
    public static SheetSource Glow()
    {
        var rgba = new byte[GlowSamples * GlowSamples * 4];
        for (var row = 0; row < GlowSamples; row++)
        {
            for (var column = 0; column < GlowSamples; column++)
            {
                var fromCentre = new Vector2(
                    ((column + 0.5f) / GlowSamples * 2f) - 1f, ((row + 0.5f) / GlowSamples * 2f) - 1f);
                var alpha = 1f - SmoothStep(GlowCoreShare, 1f, fromCentre.Length());

                var texel = ((row * GlowSamples) + column) * 4;
                rgba[texel + 0] = 255;
                rgba[texel + 1] = 255;
                rgba[texel + 2] = 255;
                rgba[texel + 3] = (byte)Math.Clamp(alpha * 255f, 0f, 255f);
            }
        }

        return SheetSource.Generated(rgba, GlowSamples, GlowSamples);
    }

    static float SmoothStep(float from, float to, float at)
    {
        var t = Math.Clamp((at - from) / MathF.Max(to - from, 1e-6f), 0f, 1f);
        return t * t * (3f - (2f * t));
    }
}
