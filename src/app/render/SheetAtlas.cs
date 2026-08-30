using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace TrafficSimulation.App.Render;

/// <summary>
/// Where one sheet sits in the atlas, as the vertex shader reads it: two <c>vec4</c>s under std140, so
/// this struct is the layout of the uniform block and there is no second description of it.
/// </summary>
/// <remarks>
/// <c>Tiles</c> is one for the sheet that repeats and zero for every other, which is how the fragment
/// stage knows to reach for the tile sampler instead of the atlas. <c>WidthPx</c> and <c>HeightPx</c>
/// are the sheet's own size and not the page's, which is what the aspects are measured from.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct SheetPlace(
    Vector2 OriginUv, Vector2 ScaleUv, float Layer, float Tiles, float WidthPx, float HeightPx);

/// <summary>
/// Every sprite sheet the town draws with, packed into the layers of one array texture: a sheet is
/// then a rectangle of a page rather than a texture of its own, and the shader reaches it by
/// transforming a coordinate instead of by indexing a descriptor.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it exists.</b> A dynamic index into an array of samplers is a Vulkan extension that neither
/// WebGPU nor WebGL2 has, and on the desktop it can cost a scalarisation loop wherever a wave spans
/// quads drawn from different sheets — which is every frame, since the bodies are one instanced draw
/// over mixed looks. An array <em>layer</em> is a coordinate and costs neither.
/// </para>
/// <para>
/// <b>Every sheet is packed but one.</b> The tread is a tile: its quad lays several pitches over
/// itself and scrolls, so its coordinates run outside the unit square and it wants a repeating,
/// mipped sampler of its own (<see cref="SheetSource"/>). A page repeats nothing and carries no mip
/// chain, so a sheet that says it repeats is kept out and drawn through that one extra binding. <b>The
/// town may have one such sheet</b>: a second would need a second binding, and the shader says so.
/// </para>
/// <para>
/// <b>The gutter is what makes clamping still true.</b> Every packed sheet is laid with a one-texel
/// border replicating its own edge, so a coordinate at the sheet's boundary blends against the same
/// texel <c>ClampToEdge</c> would have given it and never against its neighbour on the page. What a
/// sheet does <em>inside</em> itself is unchanged: the walk sheets have always been a grid whose cells
/// are sampled to their edges, and that was true of a texture of its own too.
/// </para>
/// <para>
/// Nothing is decoded twice and no page outlives its upload: the pass that packs reads image headers
/// alone, and <see cref="FillPage"/> is handed one page's pixels at a time.
/// </para>
/// </remarks>
internal sealed class SheetAtlas
{
    /// <summary>
    /// A page a side, which is a page of 64 MB. <b>Four thousand and not two</b>: the buildings are
    /// around 1200 square, and two thousand holds one of those across and wastes the strip beside it,
    /// which cost seventeen pages where this costs three.
    /// </summary>
    public const int PagePx = 4096;

    const int GutterPx = 1;

    readonly IReadOnlyList<SheetSource> _sources;
    readonly Rect[] _rects;

    SheetAtlas(IReadOnlyList<SheetSource> sources, Rect[] rects, SheetPlace[] places, int pages)
    {
        _sources = sources;
        _rects = rects;
        Places = places;
        Pages = pages;
    }

    /// <summary>How many layers the array texture needs. At least one, so a town with no sheets still has a texture.</summary>
    public int Pages { get; }

    /// <summary>One entry per sheet, in the order the sheets were handed over, ready to be written into the uniform block.</summary>
    public SheetPlace[] Places { get; }

    /// <summary>The sheet that repeats, or -1 where the town draws nothing that tiles.</summary>
    public int TileSheet { get; private init; } = -1;

    public static SheetAtlas Pack(IReadOnlyList<SheetSource> sources)
    {
        var count = sources.Count;
        var rects = new Rect[count];
        var places = new SheetPlace[count];
        var sizes = new (int Width, int Height)[count];
        for (var sheet = 0; sheet < count; sheet++) sizes[sheet] = Measure(sources[sheet]);

        var tile = -1;
        for (var sheet = 0; sheet < count; sheet++)
        {
            if (!sources[sheet].Repeats) continue;
            if (tile >= 0) throw new InvalidOperationException(
                $"Sheets {tile} and {sheet} both tile, and the sprite shader has one tile sampler.");

            tile = sheet;
        }

        // Tallest first, and every open page tried before another is started: the art is a hundred and
        // seventy pictures of thirty different shapes, and a pack that fills only the newest page
        // wastes half of every one it leaves behind.
        var order = new int[count];
        for (var sheet = 0; sheet < count; sheet++) order[sheet] = sheet;
        Array.Sort(order, (left, right) => sizes[right].Height != sizes[left].Height
            ? sizes[right].Height.CompareTo(sizes[left].Height)
            : sizes[right].Width.CompareTo(sizes[left].Width));

        var pages = new List<Skyline>();
        foreach (var sheet in order)
        {
            if (sheet == tile) continue;

            var (width, height) = sizes[sheet];
            var boxWidth = width + (GutterPx * 2);
            var boxHeight = height + (GutterPx * 2);
            if (boxWidth > PagePx || boxHeight > PagePx) throw new InvalidOperationException(
                $"Sheet {sheet} is {width}x{height}, which does not fit a {PagePx} page.");

            var placed = false;
            for (var page = 0; page < pages.Count && !placed; page++)
            {
                if (!pages[page].TryPlace(boxWidth, boxHeight, out var x, out var y)) continue;

                rects[sheet] = new Rect(page, x + GutterPx, y + GutterPx, width, height);
                placed = true;
            }

            if (placed) continue;

            var fresh = new Skyline();
            pages.Add(fresh);
            fresh.TryPlace(boxWidth, boxHeight, out var freshX, out var freshY);
            rects[sheet] = new Rect(pages.Count - 1, freshX + GutterPx, freshY + GutterPx, width, height);
        }

        for (var sheet = 0; sheet < count; sheet++)
        {
            var (width, height) = sizes[sheet];
            var rect = rects[sheet];
            places[sheet] = sheet == tile
                ? new SheetPlace(Vector2.Zero, Vector2.One, 0f, 1f, width, height)
                : new SheetPlace(
                    new Vector2(rect.X / (float)PagePx, rect.Y / (float)PagePx),
                    new Vector2(width / (float)PagePx, height / (float)PagePx),
                    rect.Page, 0f, width, height);
        }

        return new SheetAtlas(sources, rects, places, Math.Max(1, pages.Count)) { TileSheet = tile };
    }

    /// <summary>
    /// One page's texels, top row first. Everything the packer put on that page is decoded here and
    /// nowhere else, so a page's memory is live for exactly as long as its upload.
    /// </summary>
    public void FillPage(int page, Span<Rgba32> into)
    {
        into.Clear();
        for (var sheet = 0; sheet < _sources.Count; sheet++)
        {
            if (sheet == TileSheet || _rects[sheet].Page != page) continue;

            var rect = _rects[sheet];
            var pixels = Decode(_sources[sheet], rect.Width, rect.Height);
            Blit(pixels, rect, into);
        }
    }

    /// <summary>The sheet's own texels, for a caller that wants the picture rather than the page.</summary>
    public static Rgba32[] Decode(SheetSource source, int width, int height)
    {
        var pixels = new Rgba32[width * height];
        if (source.Rgba is { } raw)
        {
            MemoryMarshal.Cast<byte, Rgba32>(raw).CopyTo(pixels);
            return pixels;
        }

        using var decoded = Image.Load<Rgba32>(source.Path!);
        decoded.CopyPixelDataTo(pixels);
        return pixels;
    }

    static (int Width, int Height) Measure(SheetSource source)
    {
        if (source.Path is not { } path) return (source.WidthPx, source.HeightPx);

        var info = Image.Identify(path);
        return (info.Width, info.Height);
    }

    /// <summary>The sheet into its rectangle, and its own edge into the gutter around it.</summary>
    static void Blit(Rgba32[] pixels, Rect rect, Span<Rgba32> into)
    {
        for (var row = 0; row < rect.Height; row++)
        {
            var from = pixels.AsSpan(row * rect.Width, rect.Width);
            var at = ((rect.Y + row) * PagePx) + rect.X;
            from.CopyTo(into[at..]);

            for (var gutter = 1; gutter <= GutterPx; gutter++)
            {
                into[at - gutter] = from[0];
                into[at + rect.Width + gutter - 1] = from[^1];
            }
        }

        var stride = rect.Width + (GutterPx * 2);
        for (var gutter = 1; gutter <= GutterPx; gutter++)
        {
            var top = ((rect.Y - gutter) * PagePx) + rect.X - GutterPx;
            var above = ((rect.Y - gutter + 1) * PagePx) + rect.X - GutterPx;
            into.Slice(above, stride).CopyTo(into[top..]);

            var bottom = ((rect.Y + rect.Height + gutter - 1) * PagePx) + rect.X - GutterPx;
            var below = ((rect.Y + rect.Height + gutter - 2) * PagePx) + rect.X - GutterPx;
            into.Slice(below, stride).CopyTo(into[bottom..]);
        }
    }

    readonly record struct Rect(int Page, int X, int Y, int Width, int Height);

    /// <summary>
    /// One page's horizon: the height already built up to, as runs across the page. A rectangle goes
    /// at the lowest place it fits and the horizon rises under it — which packs pictures of unrelated
    /// shapes far tighter than shelves do, because nothing is charged the height of its tallest
    /// neighbour.
    /// </summary>
    sealed class Skyline
    {
        readonly List<Run> _runs = [new Run(0, 0, PagePx)];

        public bool TryPlace(int width, int height, out int x, out int y)
        {
            x = 0;
            y = 0;
            var bestTop = int.MaxValue;
            var bestLeft = int.MaxValue;
            var at = -1;
            for (var run = 0; run < _runs.Count; run++)
            {
                if (!Rests(run, width, out var top) || top + height > PagePx) continue;
                if (top + height > bestTop || (top + height == bestTop && _runs[run].X >= bestLeft)) continue;

                bestTop = top + height;
                bestLeft = _runs[run].X;
                at = run;
                x = _runs[run].X;
                y = top;
            }

            if (at < 0) return false;

            Raise(at, x, y + height, width);
            return true;
        }

        /// <summary>How high a rectangle starting at this run must sit to clear everything it spans.</summary>
        bool Rests(int run, int width, out int top)
        {
            top = 0;
            var left = width;
            for (var span = run; left > 0; span++)
            {
                if (span >= _runs.Count) return false;

                top = Math.Max(top, _runs[span].Y);
                left -= _runs[span].Width;
            }

            return _runs[run].X + width <= PagePx;
        }

        void Raise(int at, int x, int top, int width)
        {
            _runs.Insert(at, new Run(x, top, width));
            for (var run = at + 1; run < _runs.Count;)
            {
                var over = _runs[at].X + _runs[at].Width - _runs[run].X;
                if (over <= 0) break;

                if (_runs[run].Width <= over)
                {
                    _runs.RemoveAt(run);
                    continue;
                }

                _runs[run] = new Run(_runs[run].X + over, _runs[run].Y, _runs[run].Width - over);
                break;
            }

            for (var run = 0; run < _runs.Count - 1;)
            {
                if (_runs[run].Y != _runs[run + 1].Y)
                {
                    run++;
                    continue;
                }

                _runs[run] = _runs[run] with { Width = _runs[run].Width + _runs[run + 1].Width };
                _runs.RemoveAt(run + 1);
            }
        }

        readonly record struct Run(int X, int Y, int Width);
    }
}
