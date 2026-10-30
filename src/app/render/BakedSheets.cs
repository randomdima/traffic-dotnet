using System.Globalization;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.App.Render;

/// <summary>What the atlas's pages are held in on the device.</summary>
internal enum SheetFormat : byte
{
    /// <summary>Four bytes a texel, as a picture decodes: every device has it.</summary>
    Rgba = 0,

    /// <summary>Sixteen bytes a 4×4 block: every desktop GPU, and every desktop browser's.</summary>
    Bc7 = 1,

    /// <summary>The same, in ASTC 4×4: every phone's and tablet's.</summary>
    Astc = 2,
}

/// <summary>
/// <b>The atlas's sheets as the build compressed them</b> (<c>--bake-sheets</c>, <see cref="SheetBake"/>): each sheet's
/// blocks in BC7 and in ASTC 4×4, padded to whole blocks round a gutter a block wide, and the record that says which
/// sheet each is — so a device that takes either format packs its atlas out of blocks and never decodes a picture.
/// </summary>
/// <remarks>
/// <para>
/// <b>A sheet is named by its slot and by what it was made from</b>: a picture's path and a print of its file, or a
/// generated sheet's print of its texels. A run whose sheets the record does not match — art or figures changed since the
/// bake — is told so (<see cref="Matches"/>) and packs its pictures as they are.
/// </para>
/// <para>
/// <b>The blocks are never all in memory</b>: a desktop reads a sheet's at a time off its file, and a page writes them
/// to the device straight out of the archive it fetched (<c>town.js</c>), so the page's own heap holds none of them.
/// </para>
/// </remarks>
internal sealed class BakedSheets
{
    public const int BlockPx = 4;

    public const int BlockBytes = 16;

    /// <summary>The edge each sheet is carried out past itself, a block wide, so a block on its rim samples only itself.</summary>
    public const int GutterPx = BlockPx;

    /// <summary>The slot the one clear block is recorded under, which a sheet nothing draws is packed as.</summary>
    public const int ClearSlot = -1;

    readonly Dictionary<int, Entry> _entries;

    BakedSheets(SheetFormat format, Dictionary<int, Entry> entries)
    {
        Format = format;
        _entries = entries;
    }

    public SheetFormat Format { get; }

    /// <summary>Whether this run packs its sheets as pictures whatever was baked (<c>--sheets rgba</c>): a comparison shot's.</summary>
    public static bool Refused { get; set; }

    /// <summary>
    /// <b>The record a page fetched in place of its pictures</b> (<c>Data.Art</c>), which every atlas it packs is packed
    /// from; null where it fetched the pictures. A page holding it holds no picture to fall back to.
    /// </summary>
    public static BakedSheets? Fetched { get; set; }

    /// <summary>The block file of this record's format, as the archive that brought it names it: from the root.</summary>
    public string BlocksPath => Path.GetRelativePath(ProjectPaths.Root, BlocksFile(Format)).Replace('\\', '/');

    /// <summary>The bake's record in <paramref name="format"/> where it is this run's <paramref name="sheets"/>' to the last one; or null, and why.</summary>
    public static BakedSheets? For(SheetFormat format, IReadOnlyList<SheetSource> sheets, out string why)
    {
        why = Refused ? "asked for pictures" : format == SheetFormat.Rgba ? "the device takes neither format" : "nothing baked";
        if (Refused || Open(format) is not { } baked) return null;

        return baked.Matches(sheets, out why) ? baked : null;
    }

    /// <summary>Where the bake writes and a run reads: the build's own, beside the root's <c>obj/</c>.</summary>
    public static string Folder => Path.Combine(ProjectPaths.Root, "obj", "baked");

    public static string RecordFile => Path.Combine(Folder, "sheets.txt");

    /// <summary>The pictures nothing but the atlas reads, one path a line from the root: what a page holding blocks need not fetch.</summary>
    public static string PicturesFile => Path.Combine(Folder, "pictures.txt");

    public static string BlocksFile(SheetFormat format) => Path.Combine(Folder, format switch
    {
        SheetFormat.Bc7 => "bc7.bin",
        SheetFormat.Astc => "astc.bin",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "RGBA is not baked."),
    });

    /// <summary>A record of these entries, read off no file: what the suite packs against.</summary>
    internal static BakedSheets Recorded(SheetFormat format, IEnumerable<Entry> entries)
    {
        var recorded = new Dictionary<int, Entry>();
        foreach (var entry in entries) recorded[entry.Slot] = entry;
        return new BakedSheets(format, recorded);
    }

    /// <summary>The bake's record of <paramref name="format"/>, or null where there is none to read — or one in an older shape, which the next bake rewrites.</summary>
    public static BakedSheets? Open(SheetFormat format)
    {
        if (format == SheetFormat.Rgba || !File.Exists(RecordFile)) return null;

        var entries = new Dictionary<int, Entry>();
        foreach (var line in File.ReadLines(RecordFile))
        {
            if (line.Length == 0) continue;
            if (Entry.Parse(line) is not { } entry) return null;

            entries[entry.Slot] = entry;
        }

        return entries.ContainsKey(ClearSlot) ? new BakedSheets(format, entries) : null;
    }

    /// <summary>
    /// <b>Every picture the record holds told to <see cref="ImageHeader"/></b>, for a page that fetched the blocks and not
    /// the pictures: what reads a picture's size (the atlas, the fans' table) is answered without the file.
    /// </summary>
    public void TellTheSizes()
    {
        foreach (var entry in _entries.Values)
        {
            if (entry.Key != Entry.Generated)
                ImageHeader.Tell(Path.Combine(ProjectPaths.Root, entry.Path), entry.ArtWidthPx, entry.ArtHeightPx);
        }
    }

    /// <summary>
    /// <b>Whether every sheet a run would pack is the one the record holds</b>: a picture by its path, and by its file's
    /// print where the run holds the file; a generated sheet by its texels' print. A sheet nothing draws needs no blocks.
    /// </summary>
    public bool Matches(IReadOnlyList<SheetSource> sheets, out string why)
    {
        for (var slot = 0; slot < sheets.Count; slot++)
        {
            var source = sheets[slot];
            if (source.Unused) continue;

            if (!_entries.TryGetValue(slot, out var entry))
            {
                why = $"sheet {slot} was not baked";
                return false;
            }

            if (entry.Key != KeyOf(source))
            {
                why = $"sheet {slot} is {KeyOf(source)}, and the bake's is {entry.Key}";
                return false;
            }

            var print = source.Path is { } path ? File.Exists(path) ? PrintOfFile(path) : entry.Print : Print(source.Rgba);
            if (print != entry.Print)
            {
                why = $"sheet {slot} ({entry.Key}) has changed since the bake";
                return false;
            }
        }

        why = string.Empty;
        return true;
    }

    /// <summary>A sheet's blocks: their box, a gutter round the art and padded to whole blocks, and where they start in <see cref="BlocksFile"/>.</summary>
    public Baked Of(int slot)
    {
        var entry = _entries[slot];
        return new Baked(entry.BoxWidthPx, entry.BoxHeightPx, Format == SheetFormat.Bc7 ? entry.Bc7At : entry.AstcAt);
    }

    public Baked Clear => Of(ClearSlot);

    /// <summary>A sheet's blocks read off <see cref="BlocksFile"/>, a block row after another.</summary>
    public void Read(in Baked baked, Span<byte> into)
    {
        using var file = File.OpenHandle(BlocksFile(Format));
        RandomAccess.Read(file, into[..baked.Bytes], baked.At);
    }

    /// <summary>What a sheet is recorded as: a picture's path from the root and the outline it is packed with, or a generated one.</summary>
    public static string KeyOf(in SheetSource source)
    {
        if (source.Path is not { } path) return Entry.Generated;

        var relative = Path.GetRelativePath(ProjectPaths.Root, path).Replace('\\', '/');
        return source.OutlineArtPx > 0f
            ? $"{relative}@{source.OutlineArtPx.ToString(CultureInfo.InvariantCulture)}"
            : relative;
    }

    public static ulong PrintOfFile(string path) => Print(File.ReadAllBytes(path));

    /// <summary>FNV-1a over the bytes: enough to tell a changed picture from the one baked, and nothing about security.</summary>
    public static ulong Print(ReadOnlySpan<byte> bytes)
    {
        var hash = 14695981039346656037UL;
        foreach (var b in bytes) hash = (hash ^ b) * 1099511628211UL;
        return hash;
    }

    /// <summary>One sheet's blocks, as <see cref="Of"/> places them.</summary>
    internal readonly record struct Baked(int BoxWidthPx, int BoxHeightPx, long At)
    {
        public int Bytes => BoxWidthPx / BlockPx * (BoxHeightPx / BlockPx) * BlockBytes;
    }

    /// <summary>
    /// One line of the record: <c>slot artW artH boxW boxH bc7At astcAt print key</c>, the key last because a path is
    /// the only field that may hold anything.
    /// </summary>
    internal readonly record struct Entry(
        int Slot, int ArtWidthPx, int ArtHeightPx, int BoxWidthPx, int BoxHeightPx, long Bc7At, long AstcAt, ulong Print,
        string Key)
    {
        public const string Generated = "-";

        const int Fields = 9;

        /// <summary>A print's width in the record, which is what tells a line of this shape from one of an older.</summary>
        const int PrintDigits = 16;

        /// <summary>The picture's path from the root, without the outline its key may carry.</summary>
        public string Path => Key.IndexOf('@') is var at and >= 0 ? Key[..at] : Key;

        public override string ToString() => string.Create(
            CultureInfo.InvariantCulture,
            $"{Slot} {ArtWidthPx} {ArtHeightPx} {BoxWidthPx} {BoxHeightPx} {Bc7At} {AstcAt} {Print.ToString($"x{PrintDigits}")} {Key}");

        /// <summary>A line of the record, or null where it is not one of this shape.</summary>
        public static Entry? Parse(string line)
        {
            var fields = line.Split(' ', Fields);
            var invariant = CultureInfo.InvariantCulture;
            if (fields.Length != Fields || fields[7].Length != PrintDigits ||
                !ulong.TryParse(fields[7], NumberStyles.HexNumber, invariant, out var print))
                return null;

            return new Entry(
                int.Parse(fields[0], invariant), int.Parse(fields[1], invariant), int.Parse(fields[2], invariant),
                int.Parse(fields[3], invariant), int.Parse(fields[4], invariant), long.Parse(fields[5], invariant),
                long.Parse(fields[6], invariant), print, fields[8]);
        }
    }
}
