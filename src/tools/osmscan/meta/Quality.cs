namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>Where the sources disagree with themselves or each other</b>: every flag a layer raised while it was laid, and
/// every open OSM note over the map — each at a place, with the OSM elements it is about.
/// </summary>
internal sealed class Flags
{
    readonly List<FlagRecord> _raised = [];

    public IReadOnlyList<FlagRecord> Raised => _raised;

    public void Raise(string kind, Town town, Pt at, string about, params string[] elements) =>
        _raised.Add(new FlagRecord { Kind = kind, At = town.Plane.Degrees(at), About = about, Elements = elements });

    public void Raise(string kind, int lat, int lon, string about, params string[] elements) =>
        _raised.Add(new FlagRecord { Kind = kind, At = [lat, lon], About = about, Elements = elements });

    public static Written Lay(Town town, Flags flags, List<OsmoseIssue> osmose, string into)
    {
        var notes = town.Fetched.NotesAt is { } kept ? Notes.Read(kept) : [];
        var items = new List<FlagRecord>(flags._raised.Count + notes.Count + osmose.Count);
        items.AddRange(flags._raised.OrderBy(flag => flag.Kind, StringComparer.Ordinal));
        items.AddRange(osmose.Select(issue => new FlagRecord
        {
            Kind = $"osmose_{issue.Item}_{issue.Class}",
            At = [issue.Lat, issue.Lon],
            About = issue.Subtitle is null ? issue.Title : $"{issue.Title} ({issue.Subtitle})",
            Elements = issue.Elements,
            Proposed = issue.Proposed.Count > 0 ? issue.Proposed : null,
        }));
        items.AddRange(notes.Select(note => new FlagRecord
        {
            Kind = "osm_note",
            At = [note.Lat, note.Lon],
            About = $"opened {note.Opened[..Math.Min(10, note.Opened.Length)]}, {note.Comments} comments: {note.Text}",
            Elements = [$"note{note.Id}"],
        }));

        var summary = items.GroupBy(flag => flag.Kind.StartsWith("osmose_", StringComparison.Ordinal) ? $"{flag.Kind}: {flag.About.Split(" (")[0]}" : flag.Kind)
            .OrderByDescending(group => group.Count()).ToDictionary(group => group.Key, group => group.Count());
        return Layers.Write(into, "quality",
            "Where the sources disagree with themselves or each other: flags raised while the layers were laid, every Osmose issue of the items read (with the tags a Mapillary-detected sign would add), and every open OSM note.",
            ["derived", "osmose", "notes.json"], items, summary);
    }
}

/// <summary>One flag: what kind, where in 1e-7° as lat, lon, what is wrong, and the OSM elements it is about.</summary>
internal sealed class FlagRecord
{
    public required string Kind { get; init; }

    public required int[] At { get; init; }

    public required string About { get; init; }

    public required string[] Elements { get; init; }

    /// <summary>The tags a fix would add, where the source proposes one.</summary>
    public Dictionary<string, string>? Proposed { get; init; }
}

/// <summary>One open OSM note: where, when it was opened, how many comments it has and the first of them.</summary>
internal sealed class NoteRecord
{
    /// <summary>The longest first comment kept, in characters; a longer one is cut and marked so.</summary>
    public const int TextLength = 400;

    public required long Id { get; init; }

    public required int Lat { get; init; }

    public required int Lon { get; init; }

    public required string Opened { get; init; }

    public required int Comments { get; init; }

    public required string Text { get; init; }
}
