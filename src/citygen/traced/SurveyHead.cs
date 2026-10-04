namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// <b>What the menu reads of a traced map</b>: its description, off the first <see cref="Bytes"/> of the file
/// rather than the whole of a <see cref="TracedMap"/>, which is megabytes.
/// </summary>
/// <remarks>
/// <para>
/// <b>A map writes its name and description first</b> (<see cref="TracedMap.Write(Stream)"/>), so the head holds both
/// whatever the rest holds.
/// </para>
/// <para>
/// <b>The page is handed the head alone until the map is opened</b>: the build publishes these same bytes,
/// laid at the map's own path, and the whole map is unpacked over them when it is picked
/// (<c>App.Main.Data</c>). What is read here is therefore the same bytes on every head, and anything reading
/// the whole file before then fails on a map cut short rather than reading a different one.
/// </para>
/// </remarks>
internal static class SurveyHead
{
    /// <summary>How much of a map is its head. The web build cuts the same count (<c>PackTheSurveys</c>).</summary>
    public const int Bytes = 4096;

    public static string Description(string path)
    {
        var head = new byte[Bytes];
        int read;
        using (var file = File.OpenRead(path)) read = file.ReadAtLeast(head, Bytes, throwOnEndOfStream: false);

        return TracedMap.Head(head.AsSpan(0, read), path).Description;
    }
}
