using System.Globalization;
using System.Numerics;
using System.Text.Json.Serialization;

namespace TrafficSimulation.CityGen.Traced;

/// <summary>Which way a lane is driven, against the way it lies on as OSM draws it.</summary>
internal enum OsmLaneWay : byte
{
    Forward,
    Backward,

    /// <summary>
    /// Driven both ways: a single lane two-way traffic shares, a centre turning lane, or a lane the count holds
    /// and neither way claims, driven whichever way the time of day says.
    /// </summary>
    Both,
}

/// <summary>Where a carriageway's lane count came from.</summary>
internal enum OsmLanesFrom : byte
{
    /// <summary><c>lanes</c>, <c>lanes:forward</c>, <c>lanes:backward</c> or <c>lanes:both_ways</c>.</summary>
    Tagged,

    /// <summary>OSM's own assumption for an untagged way of its class (Key:lanes).</summary>
    Assumed,
}

/// <summary>Where a carriageway's lane widths came from.</summary>
internal enum OsmWidthFrom : byte
{
    /// <summary><c>width:lanes</c>, lane by lane.</summary>
    Lanes,

    /// <summary><c>width</c>, the carriageway's, shared evenly between its lanes.</summary>
    Carriageway,

    /// <summary>Untagged, at <see cref="OsmCarriageway.AssumedLaneWidthM"/>.</summary>
    Assumed,
}

/// <summary>One lane as OSM tags it, its <c>:lanes</c> entries kept as OSM writes them.</summary>
internal sealed class OsmLane
{
    public required OsmLaneWay Way { get; init; }

    public required float WidthM { get; init; }

    /// <summary>Its <c>turn:lanes</c> entry — <c>left;through</c> — or null where untagged.</summary>
    public string? Turn { get; init; }

    /// <summary>The arrows <see cref="Turn"/> names (<see cref="OsmTurns.ArrowsOf"/>), none where it names none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public OsmArrows Arrows { get; init; }

    /// <summary>Its <c>change:lanes</c> entry — <c>not_left</c> — or null where untagged.</summary>
    public string? Change { get; init; }

    /// <summary>Its <c>psv:lanes</c> or <c>bus:lanes</c> entry — <c>designated</c> — or null where untagged.</summary>
    public string? Psv { get; init; }
}

/// <summary>
/// <b>A road way's carriageway exactly as OSM means it</b>: every lane, left to right looking along the way as
/// drawn, with its direction, its width and its own <c>:lanes</c> entries, and where the carriageway's middle
/// stands off the way (Key:lanes, Key:width, Proposed_features/placement).
/// </summary>
/// <remarks>
/// <para>
/// <b>Read by the scanner and never by the engine</b> (src/tools/osmscan/): a traced map's file carries it, so
/// the engine lays the lanes OSM says and interprets no tag.
/// </para>
/// <para>
/// Traffic keeps right, so a two-way way's backward lanes are on its left, forward on its right, and a lane
/// driven both ways between them. An untagged count is OSM's own assumption for the class (Key:lanes): one
/// lane each way on a motorway, trunk, primary, secondary, tertiary or residential way or its link — and a
/// living street, which Key:lanes does not name, read as residential — one shared lane on any other two-way
/// way, one on a one-way way and two on a one-way motorway or trunk. An odd count with no side tagged gives
/// the odd lane to the way the way is drawn, which OSM leaves open; a count more than both sides claim is
/// lanes driven both ways down the middle. An untagged way lies in the middle of its road.
/// </para>
/// </remarks>
internal sealed class OsmCarriageway
{
    /// <summary>
    /// A lane's width where OSM tags none — the standard width the OSM lane renderer (JOSM's lane_features
    /// style) assumes, since OSM itself states no default.
    /// </summary>
    public const float AssumedLaneWidthM = 3.5f;

    /// <summary>Left to right, looking along the way as drawn.</summary>
    public required OsmLane[] Lanes { get; init; }

    /// <summary>How far the carriageway's middle stands off the way, to the right of it as drawn.</summary>
    public required float CentreOffsetM { get; init; }

    public required OsmLanesFrom LanesFrom { get; init; }

    public required OsmWidthFrom WidthFrom { get; init; }

    public float WidthM
    {
        get
        {
            var widthM = 0f;
            foreach (var lane in Lanes) widthM += lane.WidthM;
            return widthM;
        }
    }

    public int Count(OsmLaneWay way)
    {
        var count = 0;
        foreach (var lane in Lanes)
        {
            if (lane.Way == way) count++;
        }

        return count;
    }

    /// <summary>How far one lane's middle stands off the way, to the right of it as drawn.</summary>
    public float LaneOffsetM(int lane)
    {
        var leftM = 0f;
        for (var at = 0; at < lane; at++) leftM += Lanes[at].WidthM;
        return CentreOffsetM - (WidthM * 0.5f) + leftM + (Lanes[lane].WidthM * 0.5f);
    }

    /// <summary>
    /// <b>A line stood off another, to the right of it as drawn</b>, with every corner mitred: the line a lane of
    /// a polyline way is, since OSM draws a road as straights meeting at its nodes. The frame is the engine's,
    /// x east and y south, so right of a run is (−y, x).
    /// </summary>
    /// <remarks>
    /// A corner sharper than <see cref="MitreTurnCos"/> is clipped to that mitre's length, so a hairpin's lanes
    /// stop a few widths past its tip rather than running off to infinity.
    /// </remarks>
    public static void OffsetInto(ReadOnlySpan<Vector2> lineM, float offsetM, Span<Vector2> into)
    {
        for (var at = 0; at < lineM.Length; at++)
        {
            var before = at > 0 ? Right(lineM[at - 1], lineM[at]) : Vector2.Zero;
            var after = at + 1 < lineM.Length ? Right(lineM[at], lineM[at + 1]) : Vector2.Zero;
            if (before == Vector2.Zero) before = after;
            if (after == Vector2.Zero) after = before;

            var mitre = before + after;
            if (mitre.LengthSquared() < 1e-12f)
            {
                into[at] = lineM[at] + (before * offsetM);
                continue;
            }

            mitre = Vector2.Normalize(mitre);
            into[at] = lineM[at] + (mitre * (offsetM / MathF.Max(Vector2.Dot(mitre, before), MitreTurnCos)));
        }
    }

    /// <summary>The cosine of half the sharpest corner a lane is mitred round in full.</summary>
    const float MitreTurnCos = 0.25f;

    static Vector2 Right(Vector2 fromM, Vector2 toM)
    {
        var runM = toM - fromM;
        return runM.LengthSquared() > 0f ? Vector2.Normalize(new Vector2(-runM.Y, runM.X)) : Vector2.Zero;
    }

    /// <summary>
    /// The carriageway OSM tags on a road way, or null for a way that is not a road — or is a road drawn as an
    /// area (<c>area=yes</c>), which is a surface and not a line lanes run along.
    /// </summary>
    public static OsmCarriageway? Read(IReadOnlyDictionary<string, string> tags)
    {
        if (!tags.TryGetValue("highway", out var highway) || tags.GetValueOrDefault("area") == "yes") return null;

        var oneway = tags.GetValueOrDefault("oneway") ?? "";
        var against = oneway is "-1" or "reverse";
        var oneWay = against || oneway is "yes" or "1" or "true"
            || (oneway != "no" && (tags.GetValueOrDefault("junction") is "roundabout" or "circular" || highway is "motorway" or "motorway_link"));

        var lanes = Count(tags, "lanes");
        var forward = Count(tags, "lanes:forward");
        var backward = Count(tags, "lanes:backward");
        var both = Count(tags, "lanes:both_ways") ?? 0;
        var tagged = lanes is not null || forward is not null || backward is not null || both > 0;

        int f, b;
        if (oneWay)
        {
            var count = (against ? backward : forward) ?? lanes ?? (highway is "motorway" or "trunk" ? 2 : 1);
            (f, b, both) = against ? (0, count, 0) : (count, 0, 0);
        }
        else if (forward is not null || backward is not null)
        {
            var rest = Math.Max(0, (lanes ?? 0) - both);
            f = forward ?? Math.Max(0, rest - (backward ?? 0));
            b = backward ?? Math.Max(0, rest - f);

            // Lanes the count holds and neither way claims are driven whichever way the time of day says — a
            // tidal pair down the middle, given by lanes:forward:conditional and lanes:backward:conditional.
            both += Math.Max(0, rest - f - b);
        }
        else if (lanes is { } total)
        {
            var rest = Math.Max(0, total - both);
            if (rest == 1 && both == 0) (f, b, both) = (0, 0, 1);
            else (f, b) = (rest - (rest / 2), rest / 2);
        }
        else if (Bare(highway) is "primary" or "secondary" or "tertiary" or "residential" or "trunk" or "motorway" or "living_street")
        {
            (f, b) = (1, 1);
        }
        else
        {
            (f, b, both) = (0, 0, 1);
        }

        if (f + b + both == 0) (f, b) = oneWay && against ? (0, 1) : (1, oneWay ? 0 : 1);

        // Left to right along the way: the backward lanes as their own traffic numbers them right to left.
        var ways = new List<OsmLaneWay>();
        for (var at = 0; at < b; at++) ways.Add(OsmLaneWay.Backward);
        for (var at = 0; at < both; at++) ways.Add(OsmLaneWay.Both);
        for (var at = 0; at < f; at++) ways.Add(OsmLaneWay.Forward);

        var (widthM, widthFrom) = Widths(tags, ways, b, both, f);
        var laid = new OsmLane[ways.Count];
        for (var at = 0; at < laid.Length; at++)
        {
            var turn = Entry(tags, "turn:lanes", at, b, both, f, oneWay);
            laid[at] = new OsmLane
            {
                Way = ways[at],
                WidthM = widthM[at],
                Turn = turn,
                Arrows = OsmTurns.ArrowsOf(turn),
                Change = Entry(tags, "change:lanes", at, b, both, f, oneWay),
                Psv = Entry(tags, "psv:lanes", at, b, both, f, oneWay) ?? Entry(tags, "bus:lanes", at, b, both, f, oneWay),
            };
        }

        return new OsmCarriageway
        {
            Lanes = laid,
            CentreOffsetM = PlacedCentreOffsetM(tags, widthM, b, both, f),
            LanesFrom = tagged ? OsmLanesFrom.Tagged : OsmLanesFrom.Assumed,
            WidthFrom = widthFrom,
        };
    }

    static string Bare(string highway) => highway.EndsWith("_link", StringComparison.Ordinal) ? highway[..^5] : highway;

    static int? Count(IReadOnlyDictionary<string, string> tags, string key) =>
        tags.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count >= 0
            ? count
            : null;

    static float? Metres(string? value)
    {
        if (value is null) return null;

        var text = value.Trim().Replace(',', '.');
        if (text.EndsWith(" m", StringComparison.Ordinal)) text = text[..^2];
        else if (text.EndsWith('m')) text = text[..^1];
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var metres) && metres > 0f ? metres : null;
    }

    /// <summary>
    /// Every lane's width: <c>width:lanes</c> lane by lane where it is whole, else <c>width</c> shared evenly,
    /// else <see cref="AssumedLaneWidthM"/>.
    /// </summary>
    static (float[] WidthM, OsmWidthFrom From) Widths(
        IReadOnlyDictionary<string, string> tags, List<OsmLaneWay> ways, int backward, int both, int forward)
    {
        var widthM = new float[ways.Count];
        var oneWay = (backward == 0 || forward == 0) && both == 0;
        var byLane = true;
        for (var at = 0; at < widthM.Length; at++)
        {
            if (Metres(Entry(tags, "width:lanes", at, backward, both, forward, oneWay)) is { } laneM) widthM[at] = laneM;
            else byLane = false;
        }

        if (byLane && widthM.Length > 0) return (widthM, OsmWidthFrom.Lanes);

        if (Metres(tags.GetValueOrDefault("width")) is { } carriagewayM)
        {
            Array.Fill(widthM, carriagewayM / widthM.Length);
            return (widthM, OsmWidthFrom.Carriageway);
        }

        Array.Fill(widthM, AssumedLaneWidthM);
        return (widthM, OsmWidthFrom.Assumed);
    }

    /// <summary>
    /// One lane's entry of a <c>:lanes</c> key, the lane counted left to right along the way as drawn: the key
    /// itself on a one-way way or over every lane, else its <c>:forward</c>, <c>:backward</c> or <c>:both_ways</c>
    /// form, whose entries run left to right as that lane's own traffic looks.
    /// </summary>
    static string? Entry(
        IReadOnlyDictionary<string, string> tags, string key, int lane, int backward, int both, int forward, bool oneWay)
    {
        if (Split(tags.GetValueOrDefault(key)) is { } all && (oneWay || all.Length == backward + both + forward))
        {
            return lane < all.Length ? Blank(all[lane]) : null;
        }

        if (lane < backward)
        {
            var entries = Split(tags.GetValueOrDefault(key + ":backward"));
            return entries is not null && entries.Length == backward ? Blank(entries[backward - 1 - lane]) : null;
        }

        if (lane < backward + both)
        {
            var entries = Split(tags.GetValueOrDefault(key + ":both_ways"));
            return entries is not null && entries.Length == both ? Blank(entries[lane - backward]) : null;
        }

        var forwardEntries = Split(tags.GetValueOrDefault(key + ":forward"));
        return forwardEntries is not null && forwardEntries.Length == forward ? Blank(forwardEntries[lane - backward - both]) : null;

        static string[]? Split(string? value) => value?.Split('|');

        static string? Blank(string entry) => entry.Length == 0 ? null : entry;
    }

    /// <summary>
    /// Where the carriageway's middle stands off the way: the way's own place across the lanes (placement),
    /// read left to right along the way, set against the middle of them all. An untagged way, a
    /// <c>transition</c> and a lane the carriageway does not have are all the middle. A lane driven both ways is
    /// a lane of each way, so <c>placement:forward</c> counts it with the forward lanes and
    /// <c>placement:backward</c> with the backward ones.
    /// </summary>
    static float PlacedCentreOffsetM(IReadOnlyDictionary<string, string> tags, float[] widthM, int backward, int both, int forward)
    {
        var totalM = 0f;
        foreach (var laneM in widthM) totalM += laneM;

        var backwardM = 0f;
        for (var at = 0; at < backward; at++) backwardM += widthM[at];
        var middleToM = backwardM;
        for (var at = backward; at < backward + both; at++) middleToM += widthM[at];

        float? wayM = null;
        if (Across(tags.GetValueOrDefault("placement:forward"), both + forward) is { } forwardM)
        {
            wayM = backwardM + Along(forwardM, backward);
        }
        else if (Across(tags.GetValueOrDefault("placement:backward"), backward + both) is { } backwardAcross)
        {
            wayM = middleToM - Mirrored(backwardAcross);
        }
        else if (Across(tags.GetValueOrDefault("placement"), widthM.Length) is { } wholeM)
        {
            wayM = Along(wholeM, 0);
        }

        return wayM is { } atM ? (totalM * 0.5f) - atM : 0f;

        float Along((int Lane, float Share) at, int first)
        {
            var leftM = 0f;
            for (var lane = first; lane < first + at.Lane; lane++) leftM += widthM[lane];
            return leftM + (widthM[first + at.Lane] * at.Share);
        }

        // The backward lanes as their own traffic numbers them: from the middle of the road outward.
        float Mirrored((int Lane, float Share) at)
        {
            var first = backward + both - 1;
            var leftM = 0f;
            for (var lane = 0; lane < at.Lane; lane++) leftM += widthM[first - lane];
            return leftM + (widthM[first - at.Lane] * at.Share);
        }
    }

    /// <summary>A placement value as the lane, from the left and from nought, and the share across it the way lies at.</summary>
    static (int Lane, float Share)? Across(string? value, int lanes)
    {
        if (value is null) return null;

        var colon = value.IndexOf(':');
        if (colon < 0 || !int.TryParse(value.AsSpan(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)) return null;
        if (number < 1 || number > lanes) return null;

        return value[..colon] switch
        {
            "left_of" => (number - 1, 0f),
            "middle_of" => (number - 1, 0.5f),
            "right_of" => (number - 1, 1f),
            _ => null,
        };
    }
}
