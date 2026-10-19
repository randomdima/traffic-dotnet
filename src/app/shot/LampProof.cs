using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;

namespace TrafficSimulation.App.Shot;

/// <summary>
/// The proof that a look's art works (<c>--proof FILE.gif</c>): one car of every look stood side by side and put
/// through every state its lamps can say (CAR-14), then wrecked, as one captioned animated picture.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every answer in it is the engine's.</b> Where a body, a tyre and a lamp are drawn, from which sheet and which
/// cell, at what tint and when a flashing one is on, are the instances the town's own frame is built from —
/// <see cref="CarSprites.Fill"/>, <see cref="CarSprites.FillFrontTyres"/> and <see cref="LampSprites.Fill"/> — over the
/// very sheets the renderer uploads: each look's sprite and wreck with their tyres painted in
/// (<see cref="CarSheets"/>), the lamp sheet a told fleet was cut to or the committed one, and the glow. Only laying
/// a quad onto the picture is done here, with the sprite pipeline's blend: the sample times the tint, over.
/// </para>
/// <para>
/// <b>What it proves is the assets and not the driving</b>: each state is set on the car as its tick would write it
/// (<see cref="CarFleet.Lamps"/>) rather than reached by driving there, so a lamp that does not show is the look's
/// art, lens, zone map or cut and nothing else. Every car flashes in step, so a tile that does not is the one to
/// look at.
/// </para>
/// </remarks>
internal static class LampProof
{
    /// <summary>One stretch of the timeline: what every car's lamps say through it, and whether it is a wreck by then.</summary>
    readonly record struct Phase(string Name, CarLampSet Lamps, float Seconds, bool Wrecked = false);

    static readonly Phase[] Timeline =
    [
        new("parked", CarLampSet.None, 1f),
        new("driven: headlamps", CarLampSet.Head, 1f),
        new("braking", CarLampSet.Head | CarLampSet.Brake, 1f),
        new("turning left", CarLampSet.Head | CarLampSet.TurnLeft, 2f),
        new("turning right", CarLampSet.Head | CarLampSet.TurnRight, 2f),
        new("reversing", CarLampSet.Head | CarLampSet.Reverse, 1f),
        new("on a call: beacons", CarLampSet.Head | CarLampSet.Beacon, 2f),
        new("at work: amber bar", CarLampSet.Head | CarLampSet.Works, 2f),
        new("wrecked", CarLampSet.None, 1.5f, Wrecked: true),
    ];

    /// <summary>How often the timeline is looked at. A frame is kept only when it differs from the last one.</summary>
    const float SampleHz = 20f;

    /// <summary>The picture's scale: the fleet's own art grid, so a texel of a sprite is a pixel of the proof.</summary>
    const float PxPerM = LampAtlas.ArtPxPerM;

    /// <summary>Road round each car in its tile, so a tyre, a mirror or a glow past the body is in the picture.</summary>
    const float TileMarginM = 0.8f;

    const int Columns = 3;

    const int LabelPx = 16;

    const int HeadPx = 22;

    static readonly Rgba32 Road = new(38, 40, 44, 255);
    static readonly Rgba32 Ink = new(225, 228, 232, 255);
    static readonly Rgba32 Band = new(18, 19, 22, 255);

    /// <summary>
    /// Writes the proof of the catalogue's fleet looks — the told ones under <c>--fleet</c>, or every look when the
    /// shipped fleet is read — and returns how many frames it holds.
    /// </summary>
    public static int Write(string path, SimConfig config)
    {
        var catalogue = CarCatalog.Shared;
        var looks = FleetList.IsTold ? catalogue.Count : catalogue.SheetCount;
        var fleet = new CarFleet(looks, arcsPerCar: 1, CarBuilds.OfTheFleet(config, catalogue));

        var tileM = Vector2.Zero;
        for (var look = 0; look < looks; look++) tileM = Vector2.Max(tileM, catalogue.Variants[look].FootprintM);
        tileM += new Vector2(TileMarginM * 2f);
        var rows = (looks + Columns - 1) / Columns;
        var tilePx = new Size((int)MathF.Ceiling(tileM.X * PxPerM), (int)MathF.Ceiling(tileM.Y * PxPerM) + LabelPx + 4);
        var bandPx = HeadPx + 12;
        var picturePx = new Size(tilePx.Width * Columns, bandPx + (tilePx.Height * rows));

        // Each car stands at its tile's centre, nose to the right as its art is drawn, all flashing in step.
        var centresPx = new Vector2[looks];
        for (var look = 0; look < looks; look++)
        {
            centresPx[look] = new Vector2(
                ((look % Columns) + 0.5f) * tilePx.Width, bandPx + LabelPx + 4 + ((look / Columns) * tilePx.Height) + ((tileM.Y * PxPerM) * 0.5f));
            fleet.Add(centresPx[look] / PxPerM, 0f, (byte)look, backsIntoBays: false, new Rng(1, (ulong)look + 1));
            fleet.FuseJitter[look] = 1f;
        }

        var sheets = Sheets(catalogue, config, out var scales, out var firstLens);
        var drawn = Enumerable.Range(0, looks).ToArray();
        var viewSpanM = new Vector2(picturePx.Width, picturePx.Height) / PxPerM;
        var viewCentreM = viewSpanM * 0.5f;
        var into = new SpriteInstance[looks * (2 + TyreModel.SteeredWheels + (2 * CarLamps.MostLenses))];

        using var proof = new Image<Rgba32>(picturePx.Width, picturePx.Height);
        Image<Rgba32>? last = null;
        var lastKey = 0L;
        var heldS = 0f;
        var elapsedS = 0f;
        foreach (var phase in Timeline)
        {
            for (var sample = 0; sample < (int)MathF.Round(phase.Seconds * SampleHz); sample++, elapsedS += 1f / SampleHz)
            {
                for (var car = 0; car < looks; car++)
                {
                    fleet.Lamps[car] = phase.Lamps;
                    fleet.Broken[car] = phase.Wrecked;
                }

                var written = CarSprites.Fill(fleet, drawn, catalogue, 0, scales, viewCentreM, viewSpanM, into);
                written += CarSprites.FillFrontTyres(
                    fleet, drawn, sheets.Length - 1, 0f, viewCentreM, viewSpanM, into.AsSpan(written));
                written += LampSprites.Fill(
                    fleet, drawn, catalogue, config, firstLens, firstLens + 1, elapsedS, viewCentreM, viewSpanM,
                    into.AsSpan(written));

                var key = KeyOf(into.AsSpan(0, written), phase.Name);
                if (last is not null && key == lastKey)
                {
                    heldS += 1f / SampleHz;
                    continue;
                }

                if (last is not null) Keep(proof, last, heldS);
                last?.Dispose();
                last = Frame(picturePx, into.AsSpan(0, written), sheets, catalogue, looks, centresPx, tilePx, phase, elapsedS);
                (lastKey, heldS) = (key, 1f / SampleHz);
            }
        }

        if (last is not null) Keep(proof, last, heldS);
        last?.Dispose();

        proof.Frames.RemoveFrame(0);
        proof.Metadata.GetGifMetadata().RepeatCount = 0;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        proof.SaveAsGif(path);
        return proof.Frames.Count;
    }

    /// <summary>
    /// The sheets the instances index, laid out as the proof numbers them: every look intact, every look wrecked, the
    /// lamp sheet and the glow, and the rubber a steered tyre is stretched over last.
    /// </summary>
    static Sheet[] Sheets(CarCatalog catalogue, SimConfig config, out Vector2[] scales, out int firstLens)
    {
        var looks = catalogue.SheetCount;
        var sheets = new Sheet[(looks * 2) + 3];
        scales = new Vector2[looks * 2];
        for (var look = 0; look < looks; look++)
        {
            ref readonly var variant = ref catalogue.Variants[look];
            var build = CarBuild.Of(config, variant);
            sheets[look] = Sheet.Of(CarSheets.WithTyres(variant.SpritePath, variant.FootprintM, build, out scales[look]));
            sheets[looks + look] = Sheet.Of(CarSheets.WithTyres(
                variant.WreckSpritePath, variant.FootprintM * variant.WreckScale, build, out scales[looks + look]));
        }

        firstLens = looks * 2;
        sheets[firstLens] = Sheet.Of(SheetSource.File(FleetList.LampAtlasFile ?? ProjectPaths.LampAtlasFile()));
        sheets[firstLens + 1] = Sheet.Of(LampSprites.Glow());
        sheets[firstLens + 2] = Sheet.Of(CarSheets.RubberSheet());
        return sheets;
    }

    /// <summary>One sheet's texels, as the renderer would upload them.</summary>
    readonly record struct Sheet(Texel[] Texels, int Width, int Height)
    {
        public static Sheet Of(SheetSource source)
        {
            var (width, height) = SheetAtlas.Measure(source);
            return new Sheet(SheetAtlas.Decode(source), width, height);
        }

        /// <summary>Bilinear inside one cell, never reaching into the next: the atlas pads each sheet with its own edge.</summary>
        public Vector4 Sample(Vector2 uv, SpriteCell cell)
        {
            var min = cell.UvMin * new Vector2(Width, Height);
            var max = min + (cell.UvSize * new Vector2(Width, Height)) - Vector2.One;
            var at = Vector2.Clamp((uv * new Vector2(Width, Height)) - new Vector2(0.5f), min, max);
            var x0 = (int)at.X;
            var y0 = (int)at.Y;
            var x1 = Math.Min(x0 + 1, (int)max.X);
            var y1 = Math.Min(y0 + 1, (int)max.Y);
            var fx = at.X - x0;
            var fy = at.Y - y0;
            return Vector4.Lerp(
                Vector4.Lerp(Of(x0, y0), Of(x1, y0), fx), Vector4.Lerp(Of(x0, y1), Of(x1, y1), fx), fy);
        }

        Vector4 Of(int x, int y)
        {
            var texel = Texels[(y * Width) + x];
            return new Vector4(texel.R, texel.G, texel.B, texel.A) / 255f;
        }
    }

    static Image<Rgba32> Frame(
        Size picturePx, ReadOnlySpan<SpriteInstance> drawn, Sheet[] sheets, CarCatalog catalogue, int looks,
        Vector2[] centresPx, Size tilePx, in Phase phase, float elapsedS)
    {
        var frame = new Image<Rgba32>(picturePx.Width, picturePx.Height, Road);
        Fill(frame, new Rectangle(0, 0, picturePx.Width, HeadPx + 12), Band);
        GlyphStamp.Write(frame, new Point(10, 6), $"lamp proof   {phase.Name}   t {elapsedS:F2} s", HeadPx, Ink);
        for (var look = 0; look < looks; look++)
        {
            var label = new Point(
                (int)(centresPx[look].X - (tilePx.Width * 0.5f)) + 8,
                (int)(centresPx[look].Y - (tilePx.Height * 0.5f)) + 4);
            GlyphStamp.Write(frame, label, catalogue.Variants[look].Id, LabelPx, Ink);
        }

        foreach (var instance in drawn) Lay(frame, instance, sheets[instance.Sheet]);
        return frame;
    }

    /// <summary>One quad onto the picture: turned to its heading, sampled from its cell, tinted, laid over.</summary>
    static void Lay(Image<Rgba32> frame, in SpriteInstance instance, in Sheet sheet)
    {
        var centrePx = instance.CentreM * PxPerM;
        var halfM = instance.HalfSizeM;
        var forward = Heading.Unit(instance.HeadingRad);
        var right = new Vector2(-forward.Y, forward.X);
        var reachPx = halfM.Length() * PxPerM;
        var tint = instance.Tint;
        var cell = instance.Cell;

        var x0 = Math.Max(0, (int)(centrePx.X - reachPx));
        var x1 = Math.Min(frame.Width - 1, (int)(centrePx.X + reachPx) + 1);
        var y0 = Math.Max(0, (int)(centrePx.Y - reachPx));
        var y1 = Math.Min(frame.Height - 1, (int)(centrePx.Y + reachPx) + 1);
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                var offsetM = (new Vector2(x + 0.5f, y + 0.5f) - centrePx) / PxPerM;
                var along = Vector2.Dot(offsetM, forward) / halfM.X;
                var across = Vector2.Dot(offsetM, right) / halfM.Y;
                if (MathF.Abs(along) > 1f || MathF.Abs(across) > 1f) continue;

                var uv = cell.UvMin + (new Vector2((along * 0.5f) + 0.5f, (across * 0.5f) + 0.5f) * cell.UvSize);
                var source = sheet.Sample(uv, cell) * tint;
                if (source.W <= 0f) continue;

                var under = frame[x, y].ToVector4();
                var over = new Vector4(
                    (source.X * source.W) + (under.X * (1f - source.W)),
                    (source.Y * source.W) + (under.Y * (1f - source.W)),
                    (source.Z * source.W) + (under.Z * (1f - source.W)), 1f);
                frame[x, y] = new Rgba32(over);
            }
        }
    }

    /// <summary>What a frame shows, as a number: the same instances under the same caption are the same picture.</summary>
    static long KeyOf(ReadOnlySpan<SpriteInstance> drawn, string phase)
    {
        var hash = new HashCode();
        hash.Add(phase);
        foreach (var instance in drawn)
        {
            hash.Add(instance.Sheet);
            hash.Add(instance.Cell.Packed);
            hash.Add(instance.PackedTint);
            hash.Add(instance.CentreM);
            hash.Add(instance.HeadingRad);
        }

        return hash.ToHashCode();
    }

    /// <summary>A frame onto the proof, held as long as nothing on it changed.</summary>
    static void Keep(Image<Rgba32> proof, Image<Rgba32> frame, float heldS)
    {
        var kept = proof.Frames.AddFrame(frame.Frames.RootFrame);
        kept.Metadata.GetGifMetadata().FrameDelay = Math.Max(2, (int)MathF.Round(heldS * 100f));
    }

    static void Fill(Image<Rgba32> into, Rectangle at, Rgba32 colour)
    {
        for (var y = at.Top; y < at.Bottom; y++)
        {
            for (var x = at.Left; x < at.Right; x++) into[x, y] = colour;
        }
    }
}
