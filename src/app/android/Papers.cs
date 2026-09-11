using System.Formats.Tar;
using System.IO.Compression;
using Android.Content.Res;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.App.Android;

/// <summary>
/// The town's own files, put where the town already looks for them (AND-3).
/// </summary>
/// <remarks>
/// <para>
/// <b>An APK is a zip and <see cref="ProjectPaths"/> walks a tree</b>, so the build packs
/// <c>assets/</c> and <c>towns/</c> into one archive, this unpacks it into the app's own folder, and
/// every reader above is untouched. <b>There is no second asset story</b>: no provider threaded through
/// fifteen call sites, and no path that means one thing here and another on a desk.
/// </para>
/// <para>
/// <b>Once per install and not once per run.</b> The stamp is what the package manager says about the
/// install, so a reinstall or an upgrade unpacks again and an ordinary start reads a file and a
/// directory listing. An unpack that was interrupted leaves no stamp and is done again from the top,
/// which is why the stamp is written last and the folder is emptied first.
/// </para>
/// </remarks>
internal static class Papers
{
    /// <summary>What the build packed, at the name the project file gave it.</summary>
    const string Archive = "town.tar.gz";

    /// <summary>The install the folder was unpacked from, beside what it unpacked.</summary>
    const string Stamp = ".packed";

    /// <summary>
    /// Unpacks the archive if this install has not been unpacked yet, and tells
    /// <see cref="ProjectPaths"/> where the tree is either way. Called on the loop's own thread before
    /// anything reads a figure.
    /// </summary>
    /// <param name="assets">The APK's own assets, which is where the archive is.</param>
    /// <param name="filesDir">The app's private folder — the one place a run may write.</param>
    /// <param name="install">What the package manager says about this install: the version and when it landed.</param>
    public static void Unpack(AssetManager assets, string filesDir, string install)
    {
        var root = Path.Combine(filesDir, "town");
        var stamp = Path.Combine(root, Stamp);

        if (!File.Exists(stamp) || File.ReadAllText(stamp) != install)
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            Directory.CreateDirectory(root);

            using (var packed = assets.Open(Archive))
            using (var inflating = new GZipStream(packed, CompressionMode.Decompress))
            {
                TarFile.ExtractToDirectory(inflating, root, overwriteFiles: true);
            }

            File.WriteAllText(stamp, install);
        }

        ProjectPaths.FoundAt(root);
    }
}
