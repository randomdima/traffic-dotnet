using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

namespace TrafficSimulation.Agents.Car.Body;

/// <summary>
/// Cuts the town's lamp sheet out of the cars themselves (CAR-14a): for every lens a variant draws, the texels of its
/// own sprite that its zone map paints as that lens (<see cref="PasteZoned"/>), or for a look with none, the section the
/// lens is measured off (<see cref="Paste"/>), driven emissive in each colour that lens can burn. Laid out by
/// <see cref="LampAtlas"/>.
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
/// invents them lands a second pixel grid on top of the first. <b>What is applied is light, to the art's own
/// texels</b>: each keeps its colour, its contrast against the rest of its lens is stretched and it is brightened,
/// then its lens's core is taken towards white (<see cref="Lit"/>). The bezel stays the bezel and the highlight
/// the highlight; the lens is the one the artist drew, switched on.
/// </para>
/// <para>
/// <b>A lit lens has to read without a glow round it</b>, and a repaint is not how: a ramp that re-coloured the
/// lens from its luminance alone thinned the darker half to nothing, and a solid fill with a hot middle wiped the
/// artist's lens off the car. Brighter and sharper than the dull one is what lit means on the same picture.
/// </para>
/// </remarks>
internal static class LampAtlasBake
{
    /// <summary>How much a lit lens's light is stretched about its own mean: the detail the artist drew, sharper.</summary>
    const float Contrast = 1.8f;

    /// <summary>
    /// What a lit lens gains, as a share of full, on the strongest channel of every texel and the rest in step. At a
    /// quarter an amber or clear lens the art paints near full came on barely brighter than it stood (the owner,
    /// 2026-10-24: "more contrast").
    /// </summary>
    public const float Brighten = 0.4f;

    /// <summary>
    /// How much of a lens burning a light its glass is not painted in takes that light's hue (<see cref="Recolours"/>):
    /// enough to read as the light, and the rest the art's own colour, so it is still the lens that was drawn.
    /// </summary>
    const float RecolourShare = 0.85f;

    /// <summary>
    /// The least a lit texel burns, as a share of full on its glass's hue: a texel the art drew dark inside the lens (a
    /// shade, a seam, a bezel the outline took in) stays as dark as drawn while the rest brightens, and reads as a black
    /// hole in the lamp (the owner, 2026-10-23).
    /// </summary>
    const float LitFloor = 0.7f;

    /// <summary>
    /// How far a road lamp's deepest texel is taken towards white, as <see cref="BeaconHeat"/> is a beacon's, in
    /// proportion to how much green its light carries (<see cref="PeakHeatOf"/>): a clear lamp's core goes white and an
    /// amber one's yellow, and a red one's stays red — red glass burns redder and not pink. A hot core is what tells a
    /// lit lens from glass the art already paints bright.
    /// </summary>
    const float LampHeat = 0.5f;

    /// <summary>
    /// What a beacon end gains and the least it burns, in place of <see cref="Brighten"/> and <see cref="LitFloor"/>: a
    /// strobe is the brightest thing in the street, and a bar lit like a tail lamp read as one barely on (the owner,
    /// 2026-10-23).
    /// </summary>
    const float BeaconBrighten = 0.6f;

    const float BeaconFloor = 0.8f;

    /// <summary>
    /// How far a beacon end's deepest texel is taken towards white, falling off evenly to none at its rim. <b>The core
    /// has to outshine its own spill</b>: a lens paler or duller than the glow laid round it at full read as a dull panel
    /// in a lit field (the owner, 2026-10-23: "feels turned off"). The light's own colour at the rim and hot in the
    /// middle is how a strobe looks.
    /// </summary>
    const float BeaconHeat = 0.7f;

    /// <summary>Under this saturation a colour is white — the glass clear, or the light a white one.</summary>
    const float PaleSaturation = 0.3f;

    /// <summary>How far apart two hues are, in degrees, before a glass of one is not a lamp of the other.</summary>
    const float HueApartDeg = 35f;

    /// <summary>
    /// The light a lens's <paramref name="state"/> burns, as <see cref="LampAtlas.ColourOf"/> gives it.
    /// </summary>
    static Vector3 LightOf(CarLampFitting fitting, int state)
    {
        var colour = LampAtlas.ColourOf(LampAtlas.ColourAt(fitting, state));
        return new Vector3(colour.X, colour.Y, colour.Z);
    }

    /// <summary>
    /// Whether a lens whose glass is painted <paramref name="glass"/> (its texels' mean) cannot burn
    /// <paramref name="light"/> in its own colour. <b>A lamp keeps the colour its glass is</b> (the owner, 2026-10-22),
    /// but where the art has no glass of the light's colour — an indicator cut on a section painted red, a reversing
    /// lamp that is the red cluster itself, a clear lens that burns amber — the glass's colour would say the wrong
    /// thing, and the lens takes the light's (the owner, 2026-10-23).
    /// </summary>
    static bool Recolours(Vector3 glass, Vector3 light)
    {
        var (glassSat, glassHue) = (Saturation(glass), HueDeg(glass));
        var (lightSat, lightHue) = (Saturation(light), HueDeg(light));
        if (lightSat < PaleSaturation) return glassSat >= PaleSaturation;
        if (glassSat < PaleSaturation) return true;

        var apart = MathF.Abs(glassHue - lightHue) % 360f;
        return MathF.Min(apart, 360f - apart) > HueApartDeg;
    }

    static float Saturation(Vector3 colour)
    {
        var most = MathF.Max(colour.X, MathF.Max(colour.Y, colour.Z));
        return most > 0f ? (most - MathF.Min(colour.X, MathF.Min(colour.Y, colour.Z))) / most : 0f;
    }

    static float HueDeg(Vector3 colour)
    {
        var most = MathF.Max(colour.X, MathF.Max(colour.Y, colour.Z));
        var span = most - MathF.Min(colour.X, MathF.Min(colour.Y, colour.Z)) + 1e-6f;
        var sixths = most == colour.X ? ((colour.Y - colour.Z) / span % 6f + 6f) % 6f
            : most == colour.Y ? ((colour.Z - colour.X) / span) + 2f
            : ((colour.X - colour.Y) / span) + 4f;
        return sixths * 60f;
    }

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

    /// <summary>How far a texel's strongest channel has to stand over its weakest for it to be tinted glass rather than grey housing.</summary>
    const int GlassChromaOf255 = 30;

    /// <summary>And how light an untinted texel has to be to be a highlight on the glass rather than the housing.</summary>
    const float GlassLight = 0.5f;

    /// <summary>How far apart two colours have to be, over the RGB cube's 441, before one is not the other.</summary>
    const float ApartOf255 = 70f;

    /// <summary>How coarsely a sprite's colours are counted when asking which of them it is mostly made of.</summary>
    const int BucketPx = 8;

    /// <summary>And how much of a car a colour has to cover to be one the car is made of rather than a detail on it.</summary>
    const float CommonShare = 0.02f;

    /// <summary>
    /// Whether a zone map marks a fitting at all. Every fitting has a glass there; a beacon end gets it once its outline
    /// is traced with <c>qq props</c> like any other lens.
    /// </summary>
    public static bool IsZoned(CarLampFitting fitting) => GlassOf(fitting) >= 0;

    /// <summary>
    /// Whether a zone map's texel lies under a lens of this fitting. <b>Each channel is a colour of glass</b> — red the
    /// tail and brake, green the amber indicator, blue the white head and reversing lamps — so a texel two lenses share
    /// (an indicator section drawn inside a brake cluster) carries both and burns for each. <c>qq props install</c>
    /// writes it from the lens outlines.
    /// </summary>
    /// <remarks>
    /// Head and reverse share blue: they stand at opposite ends, and a lens finds its texels only within a cell of its
    /// own centre (<see cref="Zoned"/>).
    /// </remarks>
    public static bool Holds(Rgba32 zone, CarLampFitting fitting) => GlassOf(fitting) switch
    {
        0 => zone.R >= 128,
        1 => zone.G >= 128,
        2 => zone.B >= 128,
        _ => false,
    };

    /// <remarks>
    /// A beacon end's glass shares its colour's channel with the lamps at the car's ends: a lens's texels are found
    /// within a cell of its own centre (<see cref="Zoned"/>), and no roof bar stands within a cell of a lamp.
    /// </remarks>
    static int GlassOf(CarLampFitting fitting) => fitting switch
    {
        CarLampFitting.Rear or CarLampFitting.Brake or CarLampFitting.BeaconRed => 0,
        CarLampFitting.Indicator or CarLampFitting.BeaconAmber => 1,
        CarLampFitting.Head or CarLampFitting.Reverse or CarLampFitting.BeaconBlue => 2,
        _ => -1,
    };

    /// <summary>
    /// The texels of one lens's cell its zone map paints as that lens, row-major over a square of
    /// <see cref="LampAtlas.CellPx"/> from <see cref="OriginPx"/> on the sprite — the cell centred on the lens as its
    /// quad is.
    /// </summary>
    readonly record struct ZoneWindow(Point OriginPx, bool[] Lens)
    {
        public Point At(int texel) => new(OriginPx.X + (texel % LampAtlas.CellPx), OriginPx.Y + (texel / LampAtlas.CellPx));

        /// <summary>The smallest rectangle of the sprite holding every texel of the lens, or an empty one.</summary>
        public Rectangle Bounds()
        {
            int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
            for (var texel = 0; texel < Lens.Length; texel++)
            {
                if (!Lens[texel]) continue;

                var at = At(texel);
                (left, top, right, bottom) = (Math.Min(left, at.X), Math.Min(top, at.Y), Math.Max(right, at.X), Math.Max(bottom, at.Y));
            }

            return left > right ? Rectangle.Empty : new Rectangle(left, top, right - left + 1, bottom - top + 1);
        }
    }

    /// <summary>One lens cut and lit, as the report prints it: <paramref name="Zoned"/> where its zone map gave the shape.</summary>
    public readonly record struct CutLamp(
        string Variant, CarLampFitting Fitting, int WidthPx, int HeightPx, float Distinct, bool Zoned);

    /// <summary>
    /// Writes the sheet and returns what went into it, a line a lens. <b>The distinctness is the
    /// instrument</b>: a rectangle over bodywork nobody painted a lamp on cuts the paint that surrounds
    /// it and comes back near zero, and that is a sprite to finish rather than a number to adjust.
    /// </summary>
    public static List<CutLamp> Write(CarCatalog catalogue, string path)
    {
        var rows = catalogue.SheetCount;
        using var atlas = new Image<Rgba32>(LampAtlas.SheetColumns * LampAtlas.CellPx, LampAtlas.SheetRows(rows) * LampAtlas.CellPx);
        var cut = new List<CutLamp>();

        for (var variant = 0; variant < rows; variant++)
        {
            var entry = catalogue.Variants[variant];
            using var sprite = Image.Load<Rgba32>(entry.SpritePath);
            using var zones = entry.ZonesPath is { } zonesPath ? Image.Load<Rgba32>(zonesPath) : null;
            if (zones is not null && zones.Size != sprite.Size)
            {
                throw new InvalidDataException(
                    $"{entry.ZonesPath} is {zones.Width}x{zones.Height}, and the sprite it maps {sprite.Width}x{sprite.Height}.");
            }

            var perM = new Vector2(sprite.Width / entry.FootprintM.X, sprite.Height / entry.FootprintM.Y);
            var common = Common(sprite);

            var lenses = entry.Lenses;
            var seconds = 0;
            foreach (var lens in lenses) seconds += LampAtlas.StatesOf(lens.Fitting) - 1;
            if (seconds > LampAtlas.SecondLights)
            {
                throw new InvalidDataException(
                    $"{entry.Id} has {seconds} lenses burning a second light, and a look's block keeps room for {LampAtlas.SecondLights}.");
            }

            var centresPx = new Vector2[lenses.Length];
            var rects = new Rectangle[lenses.Length];
            for (var lens = 0; lens < lenses.Length; lens++)
            {
                centresPx[lens] = ((entry.FootprintM * 0.5f) + lenses[lens].AtBodyM) * perM;
                rects[lens] = RectOf(lenses[lens], centresPx[lens], perM, sprite.Size);
            }

            for (var lens = 0; lens < lenses.Length; lens++)
            {
                var fitting = lenses[lens].Fitting;
                if (zones is not null && Zoned(zones, lenses, rects, centresPx[lens], lens) is { } zoned)
                {
                    var drawn = zoned.Bounds();
                    cut.Add(new CutLamp(entry.Id, fitting, drawn.Width, drawn.Height, Distinct(sprite, drawn, common), true));

                    for (var state = 0; state < LampAtlas.StatesOf(fitting); state++)
                    {
                        PasteZoned(
                            sprite, zoned, atlas, CellOf(variant, lens, state, lenses), LightOf(fitting, state),
                            LampAtlas.IsBeacon(fitting));
                    }

                    continue;
                }

                var rect = rects[lens];
                cut.Add(new CutLamp(entry.Id, fitting, rect.Width, rect.Height, Distinct(sprite, rect, common), false));

                for (var state = 0; state < LampAtlas.StatesOf(fitting); state++)
                {
                    Paste(
                        sprite, rect, centresPx[lens], atlas, CellOf(variant, lens, state, lenses), LightOf(fitting, state),
                        beacon: LampAtlas.IsBeacon(fitting));
                }
            }
        }

        for (var row = 0; row < LampAtlas.SheetRows(rows); row++)
        {
            for (var column = 0; column < LampAtlas.SheetColumns; column++)
            {
                FillClear(atlas, new Point(column * LampAtlas.CellPx, row * LampAtlas.CellPx));
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Lossless, unlike the sprites it was cut from. The distinctness beside each line is measured
        // off these very pixels, so a lossy sheet would be an instrument reading its own compression.
        // And the colour under every clear texel kept, which FillClear laid there for the sampler.
        atlas.Save(path, new WebpEncoder
        {
            FileFormat = WebpFileFormatType.Lossless, TransparentColorMode = WebpTransparentColorMode.Preserve,
        });
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

    /// <summary>
    /// The texels of one lens's cell that its zone map paints in the lens's zone, which is the lens in the shape and the
    /// place the generator drew it rather than the rectangle its file frames; or none, for a fitting no zone marks or a
    /// lens the map left out. <b>A zone texel is the nearest lens's of its fitting</b>, measured to each one's rectangle,
    /// so two tail lamps a cell apart each burn their own.
    /// </summary>
    static ZoneWindow? Zoned(Image<Rgba32> zones, ReadOnlySpan<CarLens> lenses, Rectangle[] rects, Vector2 centrePx, int lens)
    {
        var fitting = lenses[lens].Fitting;
        if (!IsZoned(fitting)) return null;

        var origin = new Point(
            (int)MathF.Round(centrePx.X) - (LampAtlas.CellPx / 2), (int)MathF.Round(centrePx.Y) - (LampAtlas.CellPx / 2));
        var window = new ZoneWindow(origin, new bool[LampAtlas.CellPx * LampAtlas.CellPx]);
        var any = false;
        for (var texel = 0; texel < window.Lens.Length; texel++)
        {
            var at = window.At(texel);
            if (at.X < 0 || at.Y < 0 || at.X >= zones.Width || at.Y >= zones.Height || !Holds(zones[at.X, at.Y], fitting)) continue;
            if (Nearest(rects, lenses, fitting, new Vector2(at.X + 0.5f, at.Y + 0.5f)) != lens) continue;

            any = window.Lens[texel] = true;
        }

        return any ? window : null;
    }

    static int Nearest(Rectangle[] rects, ReadOnlySpan<CarLens> lenses, CarLampFitting fitting, Vector2 atPx)
    {
        var nearest = -1;
        var leastPx = float.MaxValue;
        for (var lens = 0; lens < rects.Length; lens++)
        {
            if (lenses[lens].Fitting != fitting) continue;

            var rect = rects[lens];
            var dx = MathF.Max(MathF.Max(rect.Left - atPx.X, atPx.X - rect.Right), 0f);
            var dy = MathF.Max(MathF.Max(rect.Top - atPx.Y, atPx.Y - rect.Bottom), 0f);
            var apartPx = (dx * dx) + (dy * dy);
            if (apartPx < leastPx) (nearest, leastPx) = (lens, apartPx);
        }

        return nearest;
    }

    /// <summary>
    /// A zoned lens's texels of the sprite, lit, into its cell, where the quad that draws it lays them back onto the very
    /// texels they were taken from, lit about the zone's own mean light (<see cref="Lit"/>). The shape is the zone's, and
    /// only its rim is faded (<see cref="RimPx"/>).
    /// </summary>
    static void PasteZoned(Image<Rgba32> sprite, ZoneWindow zoned, Image<Rgba32> atlas, Point cell, Vector3 light, bool beacon)
    {
        var (sum, glass, count) = (0f, Vector3.Zero, 0);
        for (var texel = 0; texel < zoned.Lens.Length; texel++)
        {
            if (!zoned.Lens[texel]) continue;

            var drawn = sprite[zoned.At(texel).X, zoned.At(texel).Y];
            sum += Luminance(drawn);
            glass += new Vector3(drawn.R, drawn.G, drawn.B) / 255f;
            count++;
        }

        var mean = sum / Math.Max(count, 1);
        glass /= Math.Max(count, 1);
        Vector3? recolour = beacon || Recolours(glass, light) ? light : null;
        var floor = FloorOf(recolour ?? glass, light, beacon ? BeaconFloor : LitFloor);
        var brighten = beacon ? BeaconBrighten : Brighten;
        var peakHeat = PeakHeatOf(light, beacon);
        var inPx = InFromRim(zoned.Lens);
        var deepestPx = 0f;
        foreach (var depth in inPx) deepestPx = MathF.Max(deepestPx, depth - 0.5f);

        for (var texel = 0; texel < zoned.Lens.Length; texel++)
        {
            if (!zoned.Lens[texel]) continue;

            var at = zoned.At(texel);
            atlas[cell.X + (texel % LampAtlas.CellPx), cell.Y + (texel / LampAtlas.CellPx)] =
                Lit(
                    sprite[at.X, at.Y], mean, recolour, floor, brighten, HeatAt(peakHeat, inPx[texel] - 0.5f, deepestPx),
                    SmoothStep(0f, RimPx, inPx[texel] - 0.5f));
        }
    }

    /// <summary>
    /// How far in from a zoned lens's rim it comes up to full strength. <b>The glow lies under the lens</b> and lifts the
    /// paint round it near to the light's own colour, while the art draws a lens's outermost texels darker as they meet
    /// its frame: solid, they were a dark ring between the lens and its light (the owner, 2026-10-23). Faded, the
    /// glow shows through them.
    /// </summary>
    const float RimPx = 1.5f;

    /// <summary>Each texel of a cell's lens, its distance in texels from the nearest texel outside it (1 on the rim).</summary>
    static int[] InFromRim(bool[] lens)
    {
        var inPx = new int[lens.Length];
        var frontier = new Queue<int>();
        for (var texel = 0; texel < lens.Length; texel++)
        {
            if (lens[texel]) continue;

            frontier.Enqueue(texel);
        }

        while (frontier.TryDequeue(out var texel))
        {
            var (x, y) = (texel % LampAtlas.CellPx, texel / LampAtlas.CellPx);
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var (nx, ny) = (x + dx, y + dy);
                    if (nx < 0 || ny < 0 || nx >= LampAtlas.CellPx || ny >= LampAtlas.CellPx) continue;

                    var near = (ny * LampAtlas.CellPx) + nx;
                    if (!lens[near] || inPx[near] > 0) continue;

                    inPx[near] = inPx[texel] + 1;
                    frontier.Enqueue(near);
                }
            }
        }

        return inPx;
    }

    /// <summary><paramref name="share"/> of full on the hue of <paramref name="colour"/>, or of the light where it has none.</summary>
    static Vector3 FloorOf(Vector3 colour, Vector3 light, float share)
    {
        var most = MathF.Max(colour.X, MathF.Max(colour.Y, colour.Z));
        var hue = most > 0f ? colour / most : light / MathF.Max(light.X, MathF.Max(light.Y, light.Z));
        return hue * share;
    }

    /// <summary>
    /// Every clear texel of one cell given the colour of the nearest lit one, its alpha left at nothing. <b>The sheet is
    /// sampled linearly and blended by straight alpha</b>, so a clear texel's colour is mixed into the lens's edge:
    /// left black, it drew a dark ring between every lit lens and the glow under it.
    /// </summary>
    static void FillClear(Image<Rgba32> atlas, Point cell)
    {
        var filled = new bool[LampAtlas.CellPx * LampAtlas.CellPx];
        var frontier = new Queue<int>();
        for (var texel = 0; texel < filled.Length; texel++)
        {
            if (atlas[cell.X + (texel % LampAtlas.CellPx), cell.Y + (texel / LampAtlas.CellPx)].A == 0) continue;

            filled[texel] = true;
            frontier.Enqueue(texel);
        }

        while (frontier.TryDequeue(out var texel))
        {
            var (x, y) = (texel % LampAtlas.CellPx, texel / LampAtlas.CellPx);
            var colour = atlas[cell.X + x, cell.Y + y];
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var (nx, ny) = (x + dx, y + dy);
                    if (nx < 0 || ny < 0 || nx >= LampAtlas.CellPx || ny >= LampAtlas.CellPx) continue;

                    var near = (ny * LampAtlas.CellPx) + nx;
                    if (filled[near]) continue;

                    filled[near] = true;
                    atlas[cell.X + nx, cell.Y + ny] = new Rgba32(colour.R, colour.G, colour.B, 0);
                    frontier.Enqueue(near);
                }
            }
        }
    }

    /// <summary>Where a cell's top-left texel is in the sheet.</summary>
    static Point CellOf(int variant, int lens, int state, ReadOnlySpan<CarLens> lenses)
    {
        var (column, row) = LampAtlas.CellOf(variant, lens, state, lenses);
        return new(column * LampAtlas.CellPx, row * LampAtlas.CellPx);
    }

    /// <summary>
    /// The cut, lit, laid in its cell around the lens centre the variant authored — which is where the
    /// quad that draws the cell is centred, so the lit lamp lands on the texels of the dull one. The cell
    /// is larger than every lens the fleet draws, so what surrounds the lamp is transparent: the bodywork
    /// around it is the car's own quad, already drawn.
    /// </summary>
    /// <remarks>
    /// <b>The contrast is stretched about the cut's own mean light and not about the middle of the scale</b>,
    /// because what a lens is painted at is the artist's business: a dark red cluster on a black coupé and a
    /// bright one on a white ambulance are both a bezel, a glass and a highlight, and both have to burn.
    /// </remarks>
    /// <param name="beacon">
    /// A beacon end, which burns harder (<see cref="BeaconBrighten"/>) and cuts only the glass inside the rectangle
    /// (<see cref="IsGlass"/>): one whose outline is not traced yet is a rectangle <c>qq beacons fit</c> measured,
    /// holding its housing too, and a housing lit — and blurred into a glow — is a coloured box round the lamp.
    /// </param>
    static void Paste(
        Image<Rgba32> sprite, Rectangle rect, Vector2 centrePx, Image<Rgba32> atlas, Point cell, Vector3 light,
        bool beacon)
    {
        var intoX = cell.X + AnchorIn(rect.Width, centrePx.X - rect.X);
        var intoY = cell.Y + AnchorIn(rect.Height, centrePx.Y - rect.Y);
        var (mean, glass) = Mean(sprite, rect, glassOnly: beacon);
        Vector3? recolour = beacon || Recolours(glass, light) ? light : null;
        var floor = FloorOf(recolour ?? glass, light, beacon ? BeaconFloor : LitFloor);
        var brighten = beacon ? BeaconBrighten : Brighten;
        var peakHeat = PeakHeatOf(light, beacon);
        var deepestPx = (Math.Min(rect.Width, rect.Height) - 1) * 0.5f;
        var edge = MathF.Min(EdgePx, Math.Min(rect.Width, rect.Height) * EdgeShare);

        for (var y = 0; y < rect.Height; y++)
        {
            for (var x = 0; x < rect.Width; x++)
            {
                var texel = sprite[rect.X + x, rect.Y + y];
                if (beacon && !IsGlass(texel)) continue;

                // The glass's own edge is its outline; only a cut that is the whole rectangle needs its edge faded.
                var inFromEdge = Math.Min(Math.Min(x, y), Math.Min(rect.Width - 1 - x, rect.Height - 1 - y));
                atlas[intoX + x, intoY + y] = Lit(
                    texel, mean, recolour, floor, brighten, HeatAt(peakHeat, inFromEdge, deepestPx),
                    beacon ? 1f : SmoothStep(0f, edge, inFromEdge + 0.5f));
            }
        }
    }

    /// <summary>
    /// Whether a texel of a beacon's rectangle is its glass: tinted, or lit up by a highlight — and not the housing,
    /// which is drawn dark and grey.
    /// </summary>
    static bool IsGlass(Rgba32 texel) =>
        texel.A >= 128
        && ((Math.Max(texel.R, Math.Max(texel.G, texel.B)) - Math.Min(texel.R, Math.Min(texel.G, texel.B)) >= GlassChromaOf255)
            || Luminance(texel) >= GlassLight);

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
    /// One texel of the art, switched on: its own colour stretched by <see cref="Contrast"/> about its lens's
    /// <paramref name="mean"/> light and raised by <paramref name="brighten"/>. <b>Nothing in it is the bake's but the
    /// light</b>: drawn solid over the dull lens, it is that lens, brighter and sharper.
    /// </summary>
    /// <param name="recolour">
    /// The light the lens burns where its glass is not painted that colour (<see cref="Recolours"/>): the texel then
    /// takes that hue at its own brightness, <see cref="RecolourShare"/> of the way.
    /// </param>
    /// <param name="floor">The least it burns, each channel (<see cref="LitFloor"/>).</param>
    /// <param name="brighten">What it gains on its strongest channel (<see cref="Brighten"/>).</param>
    /// <param name="heat">How far it is then taken towards white (<see cref="PeakHeatOf"/>).</param>
    /// <param name="atEdge">How much of the cut is here rather than at its fading rim (<see cref="EdgePx"/>).</param>
    static Rgba32 Lit(Rgba32 texel, float mean, Vector3? recolour, Vector3 floor, float brighten, float heat, float atEdge)
    {
        var drawn = new Vector3(texel.R, texel.G, texel.B) / 255f;
        var stretched = Vector3.Max(((drawn - new Vector3(mean)) * Contrast) + new Vector3(mean), Vector3.Zero);

        // Raised along its own colour rather than towards white, so red glass burns redder and not pink.
        var strongest = MathF.Max(stretched.X, MathF.Max(stretched.Y, stretched.Z));
        var lifted = strongest > 0f ? Vector3.Min(stretched + (stretched / strongest * brighten), Vector3.One) : stretched;

        // Never darker than the art it was cut from: a texel the stretch pushed below its own drawn shade is a dark
        // rim between the lens and the glow round it, which a lamp coming on does not draw. A recoloured one is held
        // at its drawn brightness rather than its drawn colour, which is the colour it is leaving.
        Vector3 lit;
        if (recolour is { } light)
        {
            var value = MathF.Max(MathF.Max(lifted.X, MathF.Max(lifted.Y, lifted.Z)), MathF.Max(drawn.X, MathF.Max(drawn.Y, drawn.Z)));
            var hue = light / MathF.Max(light.X, MathF.Max(light.Y, light.Z));
            lit = Vector3.Lerp(Vector3.Max(lifted, drawn), hue * value, RecolourShare);
        }
        else
        {
            lit = Vector3.Max(lifted, drawn);
        }

        lit = Vector3.Lerp(Vector3.Max(lit, floor), Vector3.One, heat);
        return new Rgba32(
            (byte)Math.Clamp(lit.X * 255f, 0f, 255f),
            (byte)Math.Clamp(lit.Y * 255f, 0f, 255f),
            (byte)Math.Clamp(lit.Z * 255f, 0f, 255f),
            (byte)Math.Clamp(texel.A * atEdge, 0f, 255f));
    }

    /// <summary>How far a lens burning <paramref name="light"/> is taken towards white at its deepest texel.</summary>
    static float PeakHeatOf(Vector3 light, bool beacon) =>
        beacon ? BeaconHeat : LampHeat * light.Y / MathF.Max(light.X, MathF.Max(light.Y, light.Z));

    static float HeatAt(float peakHeat, float inFromRimPx, float deepestPx) =>
        peakHeat * Math.Clamp(inFromRimPx / MathF.Max(deepestPx, 1f), 0f, 1f);

    static float SmoothStep(float from, float to, float at)
    {
        var t = Math.Clamp((at - from) / MathF.Max(to - from, 1e-6f), 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    static float Luminance(Rgba32 texel) => ((0.30f * texel.R) + (0.59f * texel.G) + (0.11f * texel.B)) / 255f;

    /// <summary>The mean light and colour of a cut, over the texels the car actually covers — or its glass alone.</summary>
    static (float Light, Vector3 Colour) Mean(Image<Rgba32> sprite, Rectangle rect, bool glassOnly)
    {
        var (sum, colour, count) = (0f, Vector3.Zero, 0);
        for (var y = rect.Y; y < rect.Y + rect.Height; y++)
        {
            for (var x = rect.X; x < rect.X + rect.Width; x++)
            {
                var texel = sprite[x, y];
                if (texel.A < 128 || (glassOnly && !IsGlass(texel))) continue;

                sum += Luminance(texel);
                colour += new Vector3(texel.R, texel.G, texel.B) / 255f;
                count++;
            }
        }

        return count > 0 ? (sum / count, colour / count) : (0.5f, Vector3.Zero);
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
