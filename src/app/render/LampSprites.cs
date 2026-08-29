using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Render;

/// <summary>
/// The lamps a car is showing (CAR-14), as instances of the same quad everything else is drawn with —
/// laid over the bodies, because a lamp is a light on the panels and not a thing standing beside them.
/// </summary>
/// <remarks>
/// <para>
/// <b>A lit lamp is the car's own section of picture, burning</b> (CAR-14a). The lens is the piece of
/// bodywork the variant's art draws one on (<see cref="CarLens"/>), and the sheet holds that very
/// section cut from that very sprite and driven emissive (<see cref="LampAtlasBake"/>). So a lamp lands
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

    /// <param name="handDriven">
    /// The units the player has the wheel of, walkers and all: each car among them runs its beacon while
    /// it is held (CTL-5c), and on a car whose art draws no bar that says nothing.
    /// </param>
    public static int Fill(
        CarFleet cars, CarCatalog catalogue, SimConfig config, int lensSheet, int glowSheet, float elapsedS,
        ReadOnlySpan<Selection> handDriven, Vector2 viewCentreM, Vector2 viewSpanM, Span<SpriteInstance> into)
    {
        var written = 0;
        var halfView = viewSpanM * 0.5f;
        var lamps = config.Lamps;
        var rows = catalogue.SheetCount;
        var halfCellM = new Vector2(LampAtlas.CellM * 0.5f);
        var cellSize = LampAtlas.CellSize(rows);
        Span<ShownLamp> shown = stackalloc ShownLamp[CarLamps.MostLenses];

        for (var car = 0; car < cars.Count && written + CarLamps.Most <= into.Length; car++)
        {
            // A wreck's art is its own crumpled picture, which the lenses of the car it was are not
            // measured against — and a wreck shows nothing anyway (CAR-14.5).
            if (cars.Broken[car]) continue;

            var variant = cars.Variant[car] % rows;
            var lenses = catalogue.LensesOf(variant);
            if (lenses.IsEmpty) continue;

            // Culled before the car is asked what it is showing: reading the line ahead for a turn is
            // arithmetic over the arcs in hand, and a town's worth of it a frame is arithmetic for
            // bodies nobody can see.
            var centreM = cars.PositionM[car];
            ref readonly var build = ref cars.BuildOf(car);
            var reachM = new Vector2(build.LengthM, build.WidthM).Length() * 0.5f;
            var offset = centreM - viewCentreM;
            if (MathF.Abs(offset.X) > halfView.X + reachM || MathF.Abs(offset.Y) > halfView.Y + reachM) continue;

            var count = CarLamps.Shown(
                lenses, CarLamps.Showing(cars, car, config, Selection.Holds(handDriven, SelectionKind.Car, car)),
                config, elapsedS,
                cars.FuseJitter[car], shown);

            var headingRad = cars.HeadingRad[car];
            var forward = new Vector2(MathF.Cos(headingRad), MathF.Sin(headingRad));
            var right = new Vector2(-forward.Y, forward.X);

            // The glow under the lens rather than over it: a lamp is a lit lens with light around it,
            // and a fade laid on top of the lens washes the lens out. It is sized off the lens the art
            // draws and not off the cell that carries it, with the floor that keeps a lamp of a few
            // texels visible from the height a street is watched from.
            for (var lamp = 0; lamp < count; lamp++)
            {
                // A dull lens is the car's own picture and is already drawn (CAR-14a). Nothing is owed
                // to a lamp that is off.
                if (!shown[lamp].Lit) continue;

                var lens = shown[lamp].Lens;
                var lensM = MathF.Max(MathF.Max(lens.SizeM.X, lens.SizeM.Y), lamps.LeastGlowM);
                into[written++] = new SpriteInstance(
                    centreM + (forward * lens.AtBodyM.X) + (right * lens.AtBodyM.Y),
                    new Vector2(lensM * 0.5f * lamps.GlowSpread), Vector2.Zero, Vector2.One,
                    LampAtlas.ColourOf(shown[lamp].Colour) with { W = lamps.GlowStrength },
                    (uint)glowSheet, headingRad);
            }

            // Every glow on the car before any of its lenses, rather than a glow and a lens a lamp at a
            // time: the second way lays the next lamp's spill over the last one's glass, and a pair of
            // lamps a hand's width apart came out one washed and one crisp.
            for (var lamp = 0; lamp < count; lamp++)
            {
                if (!shown[lamp].Lit) continue;

                // The cell carries the lamp's colour and its shading both, so the lens is drawn
                // untinted: what a lit lamp looks like is the car's own art and not a tint over it.
                var lens = shown[lamp].Lens;
                var state = LampAtlas.StateOf(lens.Fitting, shown[lamp].Colour);
                into[written++] = new SpriteInstance(
                    centreM + (forward * lens.AtBodyM.X) + (right * lens.AtBodyM.Y),
                    halfCellM, LampAtlas.CellAt(variant, lamp, state, rows), cellSize,
                    PersonSprites.Plain, (uint)lensSheet, headingRad);
            }
        }

        return written;
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
