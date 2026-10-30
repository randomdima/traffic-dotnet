using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;

namespace TrafficSimulation.App.Render;

/// <summary>
/// Where one sheet sits in the atlas, as the vertex shader reads it: two <c>vec4</c>s under std140, so
/// this struct is the layout of the uniform block and there is no second description of it.
/// </summary>
/// <remarks>
/// <c>WidthPx</c> and <c>HeightPx</c> are the sheet's own size and not the page's, which is what the
/// aspects are measured from; the shader reads the layer of that second <c>vec4</c> and its last float,
/// <paramref name="Light"/>: one for a sheet that adds light (<see cref="SheetSource.AddsLight"/>), nought for one laid over.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Size = 32)]
internal readonly record struct SheetPlace(
    Vector2 OriginUv, Vector2 ScaleUv, float Layer, float WidthPx, float HeightPx, float Light);

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
    /// A page a side, which is a page of 64 MB. <b>Four thousand and not two</b>: the whole town's art
    /// is eleven million texels, which is one page of this and four of two thousand — a size that holds
    /// the largest building across without wasting the strip beside it.
    /// </summary>
    public const int PagePx = 4096;

    /// <summary>
    /// How many sheets the shaders' table of places holds — every walker's look, two a car (the car and its
    /// wreck), one a roof, prefab and prop look. It is the length of the uniform array in <c>quad.glsl</c> and
    /// <c>town.wgsl</c>, which <c>SheetAtlasTests</c> holds them to.
    /// </summary>
    /// <remarks>
    /// <b>Sixty-four kilobytes of places</b>, thirty-two bytes each: WebGPU's default
    /// <c>maxUniformBufferBindingSize</c>, which every adapter offers, and what desktop Vulkan drivers
    /// report as <c>maxUniformBufferRange</c>. Vulkan itself promises only sixteen, so the Vulkan head refuses a
    /// device that offers less rather than drawing past it. What bounds the art beyond that is the pages'
    /// memory (<see cref="PagePx"/>), not this count.
    /// </remarks>
    public const int Slots = 2048;

    const int GutterPx = 1;

    readonly IReadOnlyList<SheetSource> _sources;
    readonly Rect[] _rects;
    readonly BakedSheets.Baked[]? _blocks;

    SheetAtlas(IReadOnlyList<SheetSource> sources, Rect[] rects, BakedSheets.Baked[]? blocks, SheetPlace[] places, int pages)
    {
        _sources = sources;
        _rects = rects;
        _blocks = blocks;
        Places = places;
        Pages = pages;
    }

    /// <summary>How many layers the array texture needs. At least one, so a town with no sheets still has a texture.</summary>
    public int Pages { get; }

    /// <summary>One entry per sheet, in the order the sheets were handed over, ready to be written into the uniform block.</summary>
    public SheetPlace[] Places { get; }

    /// <summary>The format its pages are filled in: RGBA texels, or the blocks it was packed from (<see cref="FillBlocks"/>).</summary>
    public SheetFormat Format { get; private init; }

    /// <remarks>
    /// <para>
    /// <b>A sheet takes the room it is packed at</b> (<see cref="Packed"/>) — its art's, or a texel where it is not used —
    /// <b>and its place keeps its art's size</b>, which is what an aspect is read off: the shader reaches a sheet by its
    /// share of the page and never by its texels.
    /// </para>
    /// <para>
    /// <b>Packed from <paramref name="baked"/> blocks</b>, a sheet takes its baked box — its gutter already round it, its
    /// sides whole blocks — so every box lands on a block's corner and a page is filled by copying blocks; and every sheet
    /// nothing draws shares one clear block.
    /// </para>
    /// </remarks>
    public static SheetAtlas Pack(IReadOnlyList<SheetSource> sources, BakedSheets? baked = null)
    {
        var count = sources.Count;
        var clear = count;
        var items = count + (baked is null ? 0 : 1);
        var rects = new Rect[items];
        var places = new SheetPlace[count];
        var sizes = new (int Width, int Height)[items];
        var blocks = baked is null ? null : new BakedSheets.Baked[items];
        var packed = new List<int>(items);
        for (var sheet = 0; sheet < count; sheet++)
        {
            var source = sources[sheet];
            if (blocks is null)
            {
                sizes[sheet] = Packed(source);
                packed.Add(sheet);
            }
            else if (!source.Unused)
            {
                blocks[sheet] = baked!.Of(sheet);
                sizes[sheet] = (blocks[sheet].BoxWidthPx, blocks[sheet].BoxHeightPx);
                packed.Add(sheet);
            }
        }

        if (blocks is not null)
        {
            blocks[clear] = baked!.Clear;
            sizes[clear] = (blocks[clear].BoxWidthPx, blocks[clear].BoxHeightPx);
            packed.Add(clear);
        }

        // Tallest first, and every open page tried before another is started: the art is a hundred and
        // seventy pictures of thirty different shapes, and a pack that fills only the newest page
        // wastes half of every one it leaves behind.
        var order = packed.ToArray();
        Array.Sort(order, (left, right) => sizes[right].Height != sizes[left].Height
            ? sizes[right].Height.CompareTo(sizes[left].Height)
            : sizes[right].Width.CompareTo(sizes[left].Width));

        // A baked box carries its own gutter, and its sides being whole blocks keep every corner the skyline lays on one.
        var gutterPx = blocks is null ? GutterPx : 0;
        var pages = new List<Skyline>();
        foreach (var item in order)
        {
            var (width, height) = sizes[item];
            var boxWidth = width + (gutterPx * 2);
            var boxHeight = height + (gutterPx * 2);
            if (boxWidth > PagePx || boxHeight > PagePx) throw new InvalidOperationException(
                $"Sheet {item} is {width}x{height}, which does not fit a {PagePx} page.");

            var placed = false;
            for (var page = 0; page < pages.Count && !placed; page++)
            {
                if (!pages[page].TryPlace(boxWidth, boxHeight, out var x, out var y)) continue;

                rects[item] = new Rect(page, x + gutterPx, y + gutterPx, width, height);
                placed = true;
            }

            if (placed) continue;

            var fresh = new Skyline();
            pages.Add(fresh);
            fresh.TryPlace(boxWidth, boxHeight, out var freshX, out var freshY);
            rects[item] = new Rect(pages.Count - 1, freshX + gutterPx, freshY + gutterPx, width, height);
        }

        for (var sheet = 0; sheet < count; sheet++)
        {
            var source = sources[sheet];
            var (artWidth, artHeight) = Measure(source);
            var spanPx = source.Unused ? Vector2.One : new Vector2(artWidth, artHeight);

            // A baked sheet's art starts a gutter into its box; one nothing draws stands inside the clear block.
            var (rect, insetPx) = blocks is null ? (rects[sheet], 0)
                : source.Unused ? (rects[clear], 1)
                : (rects[sheet], BakedSheets.GutterPx);
            places[sheet] = new SheetPlace(
                new Vector2(rect.X + insetPx, rect.Y + insetPx) / PagePx, spanPx / PagePx,
                rect.Page, artWidth, artHeight, source.AddsLight ? 1f : 0f);
        }

        return new SheetAtlas(sources, rects, blocks, places, Math.Max(1, pages.Count))
        {
            Format = baked?.Format ?? SheetFormat.Rgba,
        };
    }

    /// <summary>
    /// <b>Where each baked box stands</b>: its page, its corner in texels and its blocks — what a page writes to the device
    /// straight out of the blocks it fetched, a sheet at a time. Empty for an atlas of texels.
    /// </summary>
    public IEnumerable<(int Page, int X, int Y, BakedSheets.Baked Blocks)> Boxes()
    {
        if (_blocks is null) yield break;

        for (var item = 0; item < _blocks.Length; item++)
        {
            if (_blocks[item].BoxWidthPx == 0) continue;

            var rect = _rects[item];
            yield return (rect.Page, rect.X, rect.Y, _blocks[item]);
        }
    }

    /// <summary>One block row of a page, in bytes.</summary>
    public const int PageBlockRowBytes = PagePx / BakedSheets.BlockPx * BakedSheets.BlockBytes;

    /// <summary>A page of blocks, in bytes.</summary>
    public const int PageBlockBytes = PageBlockRowBytes * (PagePx / BakedSheets.BlockPx);

    /// <summary>
    /// One page's blocks, top block row first, each box's read off <paramref name="baked"/> and copied into place — the
    /// desktop's fill; a page writes its boxes to the device itself (<see cref="Boxes"/>).
    /// </summary>
    public void FillBlocks(int page, Span<byte> into, BakedSheets baked)
    {
        into.Clear();
        var box = Array.Empty<byte>();
        foreach (var (onPage, x, y, blocks) in Boxes())
        {
            if (onPage != page) continue;

            if (box.Length < blocks.Bytes) box = new byte[blocks.Bytes];
            baked.Read(blocks, box);
            var rowBytes = blocks.BoxWidthPx / BakedSheets.BlockPx * BakedSheets.BlockBytes;
            for (var row = 0; row < blocks.BoxHeightPx / BakedSheets.BlockPx; row++)
            {
                var at = ((y / BakedSheets.BlockPx) + row) * PageBlockRowBytes + (x / BakedSheets.BlockPx * BakedSheets.BlockBytes);
                box.AsSpan(row * rowBytes, rowBytes).CopyTo(into[at..]);
            }
        }
    }

    /// <summary>
    /// One page's texels, top row first. Everything the packer put on that page is decoded here and
    /// nowhere else, so a page's memory is live for exactly as long as its upload.
    /// </summary>
    public void FillPage(int page, Span<Texel> into)
    {
        if (_blocks is not null) throw new InvalidOperationException("An atlas packed from blocks is filled by FillBlocks.");

        into.Clear();
        for (var sheet = 0; sheet < _sources.Count; sheet++)
        {
            var source = _sources[sheet];
            if (_rects[sheet].Page != page || source.Unused) continue;

            Blit(Prepared(source).Texels, _rects[sheet], into);
        }
    }

    /// <summary>A sheet's texels as they are packed — decoded and its outline laid — and their size.</summary>
    public static (Texel[] Texels, int Width, int Height) Prepared(SheetSource source)
    {
        var (width, height) = Measure(source);
        var pixels = Decode(source);
        if (source.OutlineArtPx > 0f) SheetOutline.Ring(pixels, width, height, source.OutlineArtPx);

        return (pixels, width, height);
    }

    /// <summary>
    /// <b>A sheet's packed texels as the build bakes them</b>: carried out <see cref="BakedSheets.GutterPx"/> past every
    /// edge by repeating the edge, and on right and bottom to whole blocks — the gutter <see cref="Blit"/> lays a texel
    /// wide, a block wide, so a block on the rim holds nothing of a neighbour.
    /// </summary>
    public static (Texel[] Texels, int Width, int Height) Boxed(Texel[] texels, int width, int height)
    {
        const int gutterPx = BakedSheets.GutterPx;
        var boxWidth = RoundToBlock(width + (gutterPx * 2));
        var boxHeight = RoundToBlock(height + (gutterPx * 2));
        var box = new Texel[boxWidth * boxHeight];
        for (var y = 0; y < boxHeight; y++)
        {
            var fromY = Math.Clamp(y - gutterPx, 0, height - 1);
            for (var x = 0; x < boxWidth; x++) box[(y * boxWidth) + x] = texels[(fromY * width) + Math.Clamp(x - gutterPx, 0, width - 1)];
        }

        return (box, boxWidth, boxHeight);
    }

    static int RoundToBlock(int px) => (px + BakedSheets.BlockPx - 1) / BakedSheets.BlockPx * BakedSheets.BlockPx;

    /// <summary>What a sheet takes on a page: its art's size, or one texel where it is not used.</summary>
    public static (int Width, int Height) Packed(SheetSource source) => source.Unused ? (1, 1) : Measure(source);

    /// <summary>The sheet's own texels at the size <see cref="Measure"/> gives it, for a caller that wants the picture rather than the page.</summary>
    public static Texel[] Decode(SheetSource source)
    {
        var (width, height) = Measure(source);
        var pixels = new Texel[width * height];
        if (source.Rgba is { } raw) MemoryMarshal.Cast<byte, Texel>(raw).CopyTo(pixels);
        else Texels.Decode(source.Path!, pixels);

        return pixels;
    }

    /// <summary>What a sheet takes on a page: its art's size.</summary>
    public static (int Width, int Height) Measure(SheetSource source) =>
        source.Path is { } path ? ImageHeader.Measure(path) : (source.WidthPx, source.HeightPx);

    /// <summary>The sheet into its rectangle, and its own edge into the gutter around it.</summary>
    static void Blit(Texel[] pixels, Rect rect, Span<Texel> into)
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
