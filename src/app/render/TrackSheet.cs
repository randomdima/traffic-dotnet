using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Rail;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>The track's two strips as the one picture the ground samples them from</b> (RAIL-3, RAIL-13): the bed of ballast
/// (<see cref="RailLooks.Ballast"/>) in a band across the top, the sleepers and rails (<see cref="RailLooks.Track"/>) in a
/// band across the bottom, a few clear rows between — each strip laid along the track reading its own band
/// (<see cref="Layout.Bed"/>, <see cref="Layout.Track"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>One picture and not two</b>: a ground corner files its surface in four bits beside its fade's, and every one of
/// the sixteen is taken (<see cref="Surface"/>), so the bed is a band of the track's own picture rather than a surface of
/// its own.
/// </para>
/// <para>
/// <b>As long as a whole number of both</b>: the picture is as many of the sleepers' pictures, up to
/// <see cref="MostTrackPictures"/>, as fit a whole number of beds nearest their own length, and each bed is pressed or
/// drawn out along the track by what is left over.
/// </para>
/// </remarks>
internal static class TrackSheet
{
    /// <summary>The clear rows between the two bands, so a filtered or mipped sample at one's edge reads neither.</summary>
    const int GutterPx = 4;

    const int MostTrackPictures = 4;

    /// <summary>How the two strips stand in the picture, read off their files' headers and footprints alone.</summary>
    /// <param name="TrackPictures">How many of the sleepers' pictures run along it, end to end.</param>
    /// <param name="BedPictures">And how many of the bed's, each pressed to fit.</param>
    internal readonly record struct Layout(int TrackPictures, int BedPictures, int WidthPx, int BedHeightPx, int TrackHeightPx)
    {
        public int HeightPx => BedHeightPx + GutterPx + TrackHeightPx;

        /// <summary>The bed's band, as shares of the picture's height from its top.</summary>
        public (float From, float To) Bed => (0f, BedHeightPx / (float)HeightPx);

        /// <summary>And the sleepers' and rails'.</summary>
        public (float From, float To) Track => ((BedHeightPx + GutterPx) / (float)HeightPx, 1f);

        /// <summary>How far along the track the whole picture runs before it repeats.</summary>
        public float PictureM => TrackPictures * RailLooks.Shared.Track.FootprintM.X;
    }

    /// <summary>The railway's art laid out, read once: data, the same for every town.</summary>
    public static Layout Laid { get; } = Lay();

    static Layout Lay()
    {
        var looks = RailLooks.Shared;
        var (trackWidthPx, trackHeightPx) = ImageHeader.Measure(looks.Track.SpritePath);
        var (_, bedHeightPx) = ImageHeader.Measure(looks.Ballast.SpritePath);
        var (trackM, bedM) = (looks.Track.FootprintM.X, looks.Ballast.FootprintM.X);
        var best = (Track: 1, Bed: 1, Off: float.MaxValue);
        for (var track = 1; track <= MostTrackPictures; track++)
        {
            var beds = Math.Max(1, (int)MathF.Round(track * trackM / bedM));
            var off = MathF.Abs((track * trackM / (beds * bedM)) - 1f);
            if (off < best.Off) best = (track, beds, off);
        }

        return new Layout(best.Track, best.Bed, best.Track * trackWidthPx, bedHeightPx, trackHeightPx);
    }

    public static (Texel[] Texels, int WidthPx, int HeightPx) Compose()
    {
        var looks = RailLooks.Shared;
        var laid = Laid;
        var (bed, bedWidthPx, _) = Decoded(looks.Ballast.SpritePath);
        var (track, trackWidthPx, _) = Decoded(looks.Track.SpritePath);
        var width = laid.WidthPx;
        var all = new Texel[width * laid.HeightPx];
        for (var x = 0; x < width; x++)
        {
            var bedColumn = (int)((x + 0.5f) * laid.BedPictures * bedWidthPx / width) % bedWidthPx;
            for (var y = 0; y < laid.BedHeightPx; y++) all[(y * width) + x] = bed[(y * bedWidthPx) + bedColumn];

            var trackFrom = laid.BedHeightPx + GutterPx;
            for (var y = 0; y < laid.TrackHeightPx; y++) all[((trackFrom + y) * width) + x] = track[(y * trackWidthPx) + (x % trackWidthPx)];

            // The gutter clear, in the colour of the band beside it, so a mip mixing it in mixes no black.
            for (var y = 0; y < GutterPx; y++)
            {
                var beside = y < GutterPx / 2 ? all[((laid.BedHeightPx - 1) * width) + x] : all[(trackFrom * width) + x];
                all[((laid.BedHeightPx + y) * width) + x] = beside with { A = 0 };
            }
        }

        return (all, width, laid.HeightPx);
    }

    static (Texel[] Texels, int WidthPx, int HeightPx) Decoded(string path)
    {
        var (widthPx, heightPx) = ImageHeader.Measure(path);
        var texels = new Texel[widthPx * heightPx];
        Texels.Decode(path, texels);
        return (texels, widthPx, heightPx);
    }
}
