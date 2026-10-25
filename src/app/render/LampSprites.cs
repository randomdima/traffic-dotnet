using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Runtime;

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
/// (TEC-1). The glow that spills onto the bodywork around a lit one is that sheet's shapes blurred, built here
/// when the town loads (<see cref="Glow"/>), and addressed by the same cell.
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
    /// <summary>
    /// The side of a glow cell, in the glow sheet's own texels. <b>The glow sheet is the lens sheet's grid at a coarser
    /// texel</b>, so a lens's cell (<see cref="LensCell"/>) is its glow's cell too, on either sheet: a blur needs no
    /// more than this, and a cell as fine as the lens's would be a second lamp sheet's memory spent on a gradient.
    /// Each cell spans its own lens's <see cref="GlowM"/>, so every glow is drawn on a quad no larger than its light.
    /// </summary>
    public const int GlowCellPx = 24;

    /// <summary>How far out, in standard deviations of <see cref="LampFigures.GlowReachM"/>, the blur is carried before it is called nothing.</summary>
    public const float ReachSigmas = 2.5f;

    /// <summary>How finely the lens is sampled under each glow texel, both ways: what it covers is the mean.</summary>
    const int CoverSamples = 4;

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

            var count = CarLamps.Shown(lenses, cars.Lamps[car], config, elapsedS, shown);

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
                if (glowM < leastGlowM) continue;

                into[written++] = new SpriteInstance(
                    centreM + (forward * lens.AtBodyM.X) + (right * lens.AtBodyM.Y),
                    new Vector2(glowM * 0.5f), LensCell(variant, lamp, 0, rows, lenses), GlowTint(shown[lamp].Colour),
                    (uint)glowSheet, headingRad);
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
                    halfCellM, LensCell(variant, lamp, state, rows, lenses), SpriteTint.Plain, (uint)lensSheet, headingRad);
            }
        }

        return written;
    }

    /// <summary>
    /// How wide a lit lens's light reaches across: the lens the art draws and the spill either side of it. The side of
    /// the quad its glow is drawn on and of the glow cell baked for it (<see cref="Glow"/>), and what a frame culls a
    /// lamp by.
    /// </summary>
    public static float GlowM(in CarLens lens, SimConfig config) =>
        MathF.Max(lens.SizeM.X, lens.SizeM.Y) + (2f * ReachSigmas * ReachM(lens.Fitting, config));

    static float ReachM(CarLampFitting fitting, SimConfig config) =>
        LampAtlas.IsBeacon(fitting) ? config.Lamps.BeaconReachM : config.Lamps.GlowReachM;

    static float StrengthOf(CarLampFitting fitting, SimConfig config) =>
        LampAtlas.IsBeacon(fitting) ? config.Lamps.BeaconGlowStrength : config.Lamps.GlowStrength;

    /// <summary>
    /// What a glow burning <paramref name="colour"/> is tinted: the light's own colour, no brighter than an indicator's
    /// — its strength is the glow sheet's (<see cref="LampFigures.GlowStrength"/>, baked in <see cref="Glow"/>).
    /// </summary>
    /// <remarks>
    /// <b>The glow is added light</b>, so a white one adds on all three channels where a red one adds on one: unscaled,
    /// a reversing lamp spilled three times a brake lamp's light and washed a white car's tail out. So a white light is
    /// brought down to an indicator's brightness, the one lamp read at a glance; the coloured ones, at or under it,
    /// keep their own — scaled to the brake's instead, a turn read a third dimmer than it had.
    /// </remarks>
    public static uint GlowTint(CarLamp colour)
    {
        var light = LampAtlas.ColourOf(colour);
        var scale = MathF.Min(1f, Brightness(LampAtlas.ColourOf(CarLamp.Indicator)) / Brightness(light));
        return SpriteTint.Pack(new Vector4(light.X * scale, light.Y * scale, light.Z * scale, 1f));
    }

    static float Brightness(Vector4 colour) => (0.30f * colour.X) + (0.59f * colour.Y) + (0.11f * colour.Z);

    /// <summary>
    /// The cell of the lens sheet a lens burning in <paramref name="state"/> is cut into (<see cref="LampAtlas"/>):
    /// <see cref="LampAtlas.CellOf"/> in a sheet of <paramref name="looks"/>.
    /// </summary>
    public static SpriteCell LensCell(int variant, int lens, int state, int looks, ReadOnlySpan<CarLens> lenses)
    {
        var (column, row) = LampAtlas.CellOf(variant, lens, state, lenses);
        return SpriteCell.Of(column, row, LampAtlas.SheetColumns, LampAtlas.SheetRows(looks));
    }

    /// <summary>
    /// The light around every lit lens of <paramref name="catalogue"/>: its first cell of the lens sheet at
    /// <paramref name="lensSheet"/>, its shape blurred by <see cref="LampFigures.GlowReachM"/> into the same cell of a
    /// coarser grid (<see cref="GlowCellPx"/>) spanning the lens's <see cref="GlowM"/>, white, its brightest at
    /// <see cref="LampFigures.GlowStrength"/> of opaque and capped there. <b>The spill is the lamp's own outline gone
    /// soft</b> — a strip along a strip, an L round an L — where one round sprite for every lamp hid the shape the lens
    /// was cut to.
    /// </summary>
    /// <remarks>
    /// Built when the town's sheets are, from the lens sheet itself, so the two cannot be cut from different pictures and
    /// <see cref="LampFigures.GlowReachM"/> is not baked into a file. Only a lens's first cell is drawn as its glow, so
    /// only those are blurred. Each glow is as strong as the next whatever its lens's size: an indicator four texels
    /// wide says a turn as loudly as a brake cluster says a stop.
    /// </remarks>
    public static SheetSource Glow(string lensSheet, CarCatalog catalogue, SimConfig config)
    {
        var (widthPx, heightPx) = ImageHeader.Measure(lensSheet);
        var lit = new Texel[widthPx * heightPx];
        Texels.Decode(lensSheet, lit);

        var glowWidthPx = LampAtlas.SheetColumns * GlowCellPx;
        var rgba = new byte[glowWidthPx * LampAtlas.SheetRows(catalogue.SheetCount) * GlowCellPx * 4];
        var cover = new float[GlowCellPx * GlowCellPx];
        var across = new float[cover.Length];

        for (var variant = 0; variant < catalogue.SheetCount; variant++)
        {
            var lenses = catalogue.LensesOf(variant);
            for (var lens = 0; lens < lenses.Length; lens++)
            {
                var (column, row) = LampAtlas.CellOf(variant, lens, 0, lenses);
                var glowM = GlowM(lenses[lens], config);
                if (!Cover(lit, widthPx, column, row, glowM, cover)) continue;

                var weights = Gaussian(ReachM(lenses[lens].Fitting, config) / glowM * GlowCellPx);
                Blur(cover, across, weights, 1);
                Blur(across, cover, weights, GlowCellPx);

                var brightest = 0f;
                foreach (var light in cover) brightest = MathF.Max(brightest, light);

                var gain = StrengthOf(lenses[lens].Fitting, config) / brightest;
                for (var texel = 0; texel < cover.Length; texel++)
                {
                    var at = ((((row * GlowCellPx) + (texel / GlowCellPx)) * glowWidthPx) + (column * GlowCellPx) + (texel % GlowCellPx)) * 4;
                    rgba[at] = rgba[at + 1] = rgba[at + 2] = 255;
                    rgba[at + 3] = (byte)Math.Clamp(cover[texel] * gain * 255f, 0f, 255f);
                }
            }
        }

        return SheetSource.Generated(rgba, glowWidthPx, LampAtlas.SheetRows(catalogue.SheetCount) * GlowCellPx) with
        {
            AddsLight = true,
        };
    }

    /// <summary>
    /// How much of each glow texel of one cell the lit lens covers, 0 to 1, over the <paramref name="glowM"/> the glow
    /// cell spans round the lens's own cell; false for a cell with nothing lit in it.
    /// </summary>
    static bool Cover(Texel[] lit, int widthPx, int column, int row, float glowM, Span<float> into)
    {
        var lensPerGlowPx = glowM * LampAtlas.ArtPxPerM / GlowCellPx;
        var fromPx = (LampAtlas.CellPx - (glowM * LampAtlas.ArtPxPerM)) * 0.5f;
        var any = false;
        for (var texel = 0; texel < into.Length; texel++)
        {
            var covered = 0;
            for (var sample = 0; sample < CoverSamples * CoverSamples; sample++)
            {
                var x = (int)MathF.Floor(fromPx + (((texel % GlowCellPx) + (((sample % CoverSamples) + 0.5f) / CoverSamples)) * lensPerGlowPx));
                var y = (int)MathF.Floor(fromPx + (((texel / GlowCellPx) + (((sample / CoverSamples) + 0.5f) / CoverSamples)) * lensPerGlowPx));
                if (x < 0 || y < 0 || x >= LampAtlas.CellPx || y >= LampAtlas.CellPx) continue;

                covered += lit[(((row * LampAtlas.CellPx) + y) * widthPx) + (column * LampAtlas.CellPx) + x].A;
            }

            into[texel] = covered / (255f * CoverSamples * CoverSamples);
            any |= into[texel] > 0f;
        }

        return any;
    }

    /// <summary>A normalised Gaussian of <paramref name="sigmaPx"/>, carried <see cref="ReachSigmas"/> out each side.</summary>
    static float[] Gaussian(float sigmaPx)
    {
        var reachPx = Math.Max(1, (int)MathF.Ceiling(ReachSigmas * sigmaPx));
        var weights = new float[(2 * reachPx) + 1];
        var sum = 0f;
        for (var offset = -reachPx; offset <= reachPx; offset++)
        {
            sum += weights[offset + reachPx] = MathF.Exp(-(offset * offset) / (2f * sigmaPx * sigmaPx));
        }

        for (var tap = 0; tap < weights.Length; tap++) weights[tap] /= sum;
        return weights;
    }

    /// <summary>One pass of the blur over a cell, along whichever axis <paramref name="step"/> walks; past the cell is dark.</summary>
    static void Blur(float[] from, float[] into, float[] weights, int step)
    {
        var reachPx = weights.Length / 2;
        for (var texel = 0; texel < from.Length; texel++)
        {
            var along = step == 1 ? texel % GlowCellPx : texel / GlowCellPx;
            var sum = 0f;
            for (var offset = -reachPx; offset <= reachPx; offset++)
            {
                if (along + offset < 0 || along + offset >= GlowCellPx) continue;

                sum += from[texel + (offset * step)] * weights[offset + reachPx];
            }

            into[texel] = sum;
        }
    }
}
