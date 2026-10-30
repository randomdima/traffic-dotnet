using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>The atlas's sheets compressed by the build</b> (<c>--bake-sheets</c>): every sheet a run can pack, boxed
/// (<see cref="SheetAtlas.Boxed"/>), encoded once to
/// UASTC by KTX-Software's <c>ktx</c> and transcoded from that to BC7 and to ASTC 4×4 — written as the record and the two
/// block files <see cref="BakedSheets"/> reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>A box already encoded is never encoded again</b>: each is kept in the folder's cache under a print of its texels,
/// so a bake after one picture changed encodes that one.
/// </para>
/// <para>
/// <b>Without <c>ktx</c> nothing is baked</b>, and what an earlier bake left is removed: the build then ships the
/// pictures alone and every device packs RGBA, which is a slower page and never a broken one.
/// </para>
/// </remarks>
internal static class SheetBake
{
    const string Tool = "ktx";

    /// <summary>What the transcoded files must say they hold: <c>VK_FORMAT_BC7_UNORM_BLOCK</c> and <c>VK_FORMAT_ASTC_4x4_UNORM_BLOCK</c>.</summary>
    const uint Bc7Format = 145;

    const uint AstcFormat = 157;

    /// <summary>
    /// The UASTC encoder's effort, 0 to 4: two measured as good as four on the art, at a fifteenth of the time. No RDO: it
    /// took 4–10% off the download for 1–9 dB of the picture (2026-10-30).
    /// </summary>
    const string UastcQuality = "2";

    static string Cache => Path.Combine(BakedSheets.Folder, "cache");

    static string Work => Path.Combine(BakedSheets.Folder, "work");

    public static int Run(SimConfig config)
    {
        if (!Runs(Tool, "--version"))
        {
            Remove();
            Console.WriteLine($"no `{Tool}` (KTX-Software) on the PATH: the sheets are not baked, and every device packs them as pictures");
            return 0;
        }

        var started = Stopwatch.GetTimestamp();
        Directory.CreateDirectory(Cache);
        Directory.CreateDirectory(Work);
        var looks = TownSprites.Load(config);
        var sheets = looks.Sheets;
        var slots = new int[sheets.Length + 1];
        slots[0] = BakedSheets.ClearSlot;
        for (var slot = 0; slot < sheets.Length; slot++) slots[slot + 1] = slot;

        var made = new Made[slots.Length];
        var encoded = 0;
        Parallel.For(
            0, slots.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) },
            job =>
            {
                made[job] = Bake(sheets, slots[job], out var fresh);
                if (fresh) Interlocked.Increment(ref encoded);
            });

        Write(made, slots);
        File.WriteAllLines(Fresh(BakedSheets.PicturesFile), OnlyTheAtlasReads(looks));
        Commit(BakedSheets.PicturesFile);
        var pruned = Prune(made);
        Console.WriteLine(
            $"{slots.Length} sheets baked into {BakedSheets.Folder}, {encoded} of them encoded afresh, {pruned} cached left behind dropped, in " +
            $"{Stopwatch.GetElapsedTime(started).TotalSeconds:F1} s");
        return 0;
    }

    /// <summary>One box made: what it was made from, its size, and the name its blocks are cached under.</summary>
    readonly record struct Made(string Key, ulong Print, int ArtWidthPx, int ArtHeightPx, int BoxWidthPx, int BoxHeightPx, string Cached);

    static Made Bake(SheetSource[] sheets, int slot, out bool fresh)
    {
        Texel[] box;
        int boxWidth, boxHeight, artWidth, artHeight;
        string key;
        ulong print;
        if (slot == BakedSheets.ClearSlot)
        {
            (boxWidth, boxHeight, artWidth, artHeight) = (BakedSheets.BlockPx, BakedSheets.BlockPx, 1, 1);
            box = new Texel[boxWidth * boxHeight];
            (key, print) = ("-", 0UL);
        }
        else
        {
            var source = sheets[slot] with { Unused = false };
            (artWidth, artHeight) = SheetAtlas.Measure(source);
            var (texels, width, height) = SheetAtlas.Prepared(source);
            (box, boxWidth, boxHeight) = SheetAtlas.Boxed(texels, width, height);
            key = BakedSheets.KeyOf(source);
            print = source.Path is { } path ? BakedSheets.PrintOfFile(path) : BakedSheets.Print(source.Rgba);
        }

        var bytes = MemoryMarshal.AsBytes(box.AsSpan());
        var cached = $"{BakedSheets.Print(bytes):x16}-{boxWidth}x{boxHeight}-uastc{UastcQuality}";
        fresh = !File.Exists(Path.Combine(Cache, cached + ".bc7")) || !File.Exists(Path.Combine(Cache, cached + ".astc"));
        if (fresh) Encode(bytes, boxWidth, boxHeight, cached);

        return new Made(key, print, artWidth, artHeight, boxWidth, boxHeight, cached);
    }

    /// <summary>A box's texels to UASTC, and from that to the two block formats, each kept as its bare level-0 blocks.</summary>
    static void Encode(ReadOnlySpan<byte> rgba, int widthPx, int heightPx, string name)
    {
        // Two sheets of the same texels are one name, and may be encoded at once: their work files are their own.
        var working = $"{name}.{Guid.NewGuid():N}";
        var raw = Path.Combine(Work, working + ".raw");
        var uastc = Path.Combine(Work, working + ".ktx2");
        File.WriteAllBytes(raw, rgba.ToArray());
        try
        {
            Ktx("create", "--raw", "--width", $"{widthPx}", "--height", $"{heightPx}", "--format", "R8G8B8A8_UNORM",
                "--encode", "uastc", "--uastc-quality", UastcQuality, raw, uastc);
            Transcode(uastc, "bc7", Bc7Format, Path.Combine(Cache, name + ".bc7"), widthPx, heightPx);
            Transcode(uastc, "astc", AstcFormat, Path.Combine(Cache, name + ".astc"), widthPx, heightPx);
        }
        finally
        {
            File.Delete(raw);
            File.Delete(uastc);
        }
    }

    static void Transcode(string uastc, string target, uint format, string into, int widthPx, int heightPx)
    {
        var transcoded = $"{uastc}.{target}.ktx2";
        try
        {
            Ktx("transcode", "--target", target, uastc, transcoded);
            var blocks = LevelZero(File.ReadAllBytes(transcoded), format, transcoded);
            var expected = widthPx / BakedSheets.BlockPx * (heightPx / BakedSheets.BlockPx) * BakedSheets.BlockBytes;
            if (blocks.Length != expected) throw new InvalidDataException($"{transcoded}: {blocks.Length} bytes of blocks, not {expected}.");

            var whole = transcoded + ".blocks";
            File.WriteAllBytes(whole, blocks.ToArray());
            File.Move(whole, into, overwrite: true);
        }
        finally
        {
            File.Delete(transcoded);
        }
    }

    /// <summary>
    /// The first level's bytes of a KTX2 file (Khronos KTX 2.0 §3): its format at byte 12, its supercompression at 44, and
    /// its level index from byte 80 — the first entry's offset and length.
    /// </summary>
    static ReadOnlySpan<byte> LevelZero(byte[] ktx2, uint format, string named)
    {
        var read = BinaryPrimitives.ReadUInt32LittleEndian(ktx2.AsSpan(12));
        var supercompressed = BinaryPrimitives.ReadUInt32LittleEndian(ktx2.AsSpan(44));
        if (read != format || supercompressed != 0)
            throw new InvalidDataException($"{named}: format {read}, supercompression {supercompressed}; wanted {format} and none.");

        var at = (int)BinaryPrimitives.ReadUInt64LittleEndian(ktx2.AsSpan(80));
        var length = (int)BinaryPrimitives.ReadUInt64LittleEndian(ktx2.AsSpan(88));
        return ktx2.AsSpan(at, length);
    }

    /// <summary>The two block files, every box end to end in the order baked, and the record that says where each starts.</summary>
    static void Write(Made[] made, int[] slots)
    {
        var lines = new string[made.Length];
        using (var bc7 = File.Create(Fresh(BakedSheets.BlocksFile(SheetFormat.Bc7))))
        using (var astc = File.Create(Fresh(BakedSheets.BlocksFile(SheetFormat.Astc))))
        {
            for (var job = 0; job < made.Length; job++)
            {
                var box = made[job];
                var entry = new BakedSheets.Entry(
                    slots[job], box.ArtWidthPx, box.ArtHeightPx, box.BoxWidthPx, box.BoxHeightPx, bc7.Position,
                    astc.Position, box.Print, box.Key);
                bc7.Write(File.ReadAllBytes(Path.Combine(Cache, box.Cached + ".bc7")));
                astc.Write(File.ReadAllBytes(Path.Combine(Cache, box.Cached + ".astc")));
                lines[job] = entry.ToString();
            }
        }

        File.WriteAllLines(Fresh(BakedSheets.RecordFile), lines);
        Commit(BakedSheets.BlocksFile(SheetFormat.Bc7));
        Commit(BakedSheets.BlocksFile(SheetFormat.Astc));
        Commit(BakedSheets.RecordFile);
    }

    /// <summary>
    /// The pictures nothing but the atlas reads — every walker's and car's look and every roof's — by their path from the
    /// root: what a page holding their blocks is not sent. Anything else that reads one asks its size, which the record
    /// tells (<see cref="BakedSheets.TellTheSizes"/>).
    /// </summary>
    static IEnumerable<string> OnlyTheAtlasReads(TownSprites looks)
    {
        var sheets = looks.Sheets;
        for (var slot = 0; slot < sheets.Length; slot++)
        {
            var onlyTheAtlas = slot < looks.FirstBeamSheet || (slot >= looks.FirstBuildingSheet && slot < looks.FirstPropSheet);
            if (onlyTheAtlas && sheets[slot].Path is { } path)
                yield return Path.GetRelativePath(ProjectPaths.Root, path).Replace('\\', '/');
        }
    }

    /// <summary>Every cached box this bake did not use dropped — a picture since changed, or a box no longer packed — and how many.</summary>
    static int Prune(Made[] made)
    {
        var kept = new HashSet<string>(StringComparer.Ordinal);
        foreach (var box in made) kept.Add(box.Cached);

        var pruned = 0;
        foreach (var file in Directory.EnumerateFiles(Cache))
        {
            if (kept.Contains(Path.GetFileNameWithoutExtension(file))) continue;

            File.Delete(file);
            pruned++;
        }

        return pruned;
    }

    static void Remove()
    {
        foreach (var file in new[]
                 {
                     BakedSheets.RecordFile, BakedSheets.PicturesFile, BakedSheets.BlocksFile(SheetFormat.Bc7),
                     BakedSheets.BlocksFile(SheetFormat.Astc),
                 })
        {
            File.Delete(file);
        }
    }

    /// <summary>Where a file is written before it replaces the one a reader may be reading.</summary>
    static string Fresh(string path) => path + ".new";

    /// <summary>The fresh file in place of the old — or dropped where it is the same bytes, so a build packing it again finds it unchanged.</summary>
    static void Commit(string path)
    {
        var fresh = Fresh(path);
        if (File.Exists(path) && new FileInfo(path).Length == new FileInfo(fresh).Length &&
            File.ReadAllBytes(path).AsSpan().SequenceEqual(File.ReadAllBytes(fresh)))
        {
            File.Delete(fresh);
            return;
        }

        File.Move(fresh, path, overwrite: true);
    }

    static void Ktx(params string[] arguments)
    {
        var (exit, said) = Start(Tool, arguments);
        if (exit != 0) throw new InvalidOperationException($"{Tool} {string.Join(' ', arguments)}: {said}");
    }

    static bool Runs(string tool, params string[] arguments)
    {
        try
        {
            return Start(tool, arguments).Exit == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    static (int Exit, string Said) Start(string tool, string[] arguments)
    {
        var start = new ProcessStartInfo(tool) { RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var said = process.StandardError.ReadToEndAsync();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, said.Result.Trim());
    }
}
