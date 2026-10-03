namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// <b>The arrows painted on one lane</b>, as its <c>turn:lanes</c> entry names them (Key:turn): every way it may
/// be left in at the junction it runs into, in OSM's own words.
/// </summary>
[Flags]
internal enum OsmArrows : ushort
{
    None = 0,
    Through = 1 << 0,
    Left = 1 << 1,
    SlightLeft = 1 << 2,
    SharpLeft = 1 << 3,
    Right = 1 << 4,
    SlightRight = 1 << 5,
    SharpRight = 1 << 6,
    Reverse = 1 << 7,
    MergeToLeft = 1 << 8,
    MergeToRight = 1 << 9,
}

/// <summary>
/// <b>One turn OSM forbids a car, or the one it allows</b>, off a turn restriction (Relation:restriction): from one
/// road way, at the node it meets another, onto that other.
/// </summary>
internal sealed class OsmTurnRestriction
{
    /// <summary>The relation it was read off.</summary>
    public required long Relation { get; init; }

    /// <summary>The way the turn is made from, by its OSM id.</summary>
    public required long From { get; init; }

    /// <summary>The node it is made at, as its index into <see cref="OsmExtract.Nodes"/>.</summary>
    public required int Via { get; init; }

    /// <summary>The way it is made onto, by its OSM id.</summary>
    public required long To { get; init; }

    /// <summary>Whether it is the one turn allowed off the way at the node (<c>only_*</c>) rather than one forbidden (<c>no_*</c>).</summary>
    public required bool Only { get; init; }
}

/// <summary>
/// <b>One lane joined to another across a junction</b>, off a lane connectivity relation (Relation:connectivity):
/// the lanes a car may be driven between, from one road way at the node it meets another onto that other.
/// </summary>
internal sealed class OsmLaneLink
{
    /// <summary>The relation it was read off.</summary>
    public required long Relation { get; init; }

    public required long From { get; init; }

    /// <summary>The node the two ways meet at, as its index into <see cref="OsmExtract.Nodes"/>.</summary>
    public required int Via { get; init; }

    public required long To { get; init; }

    /// <summary>The lane left, counted from one at the left as its own traffic looks.</summary>
    public required int FromLane { get; init; }

    /// <summary>The lane taken, counted the same way.</summary>
    public required int ToLane { get; init; }
}

/// <summary>
/// <b>Where OSM says a car may turn</b>: the turn restrictions and lane connectivity relations over a place's
/// roads as their tagging rules mean them for a car — read by the scanner, so the engine interprets no relation.
/// </summary>
/// <remarks>
/// <para>
/// <b>A restriction is a car's</b> where its <c>restriction</c> — or its <c>restriction:motorcar</c>,
/// <c>:motor_vehicle</c> or <c>:vehicle</c> — says so, no <c>except</c> lets cars off it, and it is in force at
/// every hour: a <c>restriction:conditional</c>, <c>hour_on</c> or <c>day_on</c> is a turn forbidden at times, and
/// a map with no clock of day cannot keep it. <c>no_entry</c> and <c>no_exit</c> name several ways at one end, and
/// forbid every pair.
/// </para>
/// <para>
/// <b>Both are read where the turn is made at a node.</b> One made over a way — a U-turn across a median, from one
/// carriageway over the gap onto the other — is a turn through two junctions, which no one junction can forbid
/// without forbidding a turn the relation does not name. A connectivity relation with no via is over the node its
/// two ways share.
/// </para>
/// </remarks>
internal sealed class OsmTurns
{
    public static OsmTurns None => new() { Restrictions = [], LaneLinks = [] };

    public required OsmTurnRestriction[] Restrictions { get; init; }

    public required OsmLaneLink[] LaneLinks { get; init; }

    static readonly string[] CarsOwn = ["restriction:motorcar", "restriction:motor_vehicle", "restriction:vehicle"];

    static readonly string[] AtTimes = ["hour_on", "hour_off", "day_on", "day_off"];

    static readonly string[] Cars = ["motorcar", "motor_vehicle", "vehicle"];

    /// <summary>The arrows one lane's <c>turn:lanes</c> entry names, any word OSM does not define left out.</summary>
    public static OsmArrows ArrowsOf(string? entry)
    {
        var arrows = OsmArrows.None;
        if (entry is null) return arrows;

        foreach (var word in entry.Split(';'))
        {
            if (Arrow(word.Trim()) is { } arrow) arrows |= arrow;
        }

        return arrows;
    }

    /// <summary>One word of a <c>turn:lanes</c> entry, or null for one OSM does not define.</summary>
    public static OsmArrows? Arrow(string word) => word switch
    {
        "" or "none" => OsmArrows.None,
        "through" => OsmArrows.Through,
        "left" => OsmArrows.Left,
        "slight_left" => OsmArrows.SlightLeft,
        "sharp_left" => OsmArrows.SharpLeft,
        "right" => OsmArrows.Right,
        "slight_right" => OsmArrows.SlightRight,
        "sharp_right" => OsmArrows.SharpRight,
        "reverse" => OsmArrows.Reverse,
        "merge_to_left" => OsmArrows.MergeToLeft,
        "merge_to_right" => OsmArrows.MergeToRight,
        _ => null,
    };

    /// <summary>
    /// Every restriction and lane link the relations hold for a car, and each relation not read with why, over the
    /// road ways of an extract by OSM id and its nodes' indices by OSM id.
    /// </summary>
    public static OsmTurns Read(
        IEnumerable<OsmRelation> relations, IReadOnlyDictionary<long, OsmWay> roads, IReadOnlyDictionary<long, int> nodeIndex,
        List<(long Relation, string Why)> unread)
    {
        var restrictions = new List<OsmTurnRestriction>();
        var links = new List<OsmLaneLink>();
        foreach (var relation in relations)
        {
            var why = relation.Tags.GetValueOrDefault("type") switch
            {
                "restriction" => Restriction(relation, roads, nodeIndex, restrictions),
                "connectivity" => Connectivity(relation, roads, nodeIndex, links),
                _ => "neither a restriction nor connectivity",
            };
            if (why is not null) unread.Add((relation.Id, why));
        }

        return new OsmTurns { Restrictions = [.. restrictions], LaneLinks = [.. links] };
    }

    static string? Restriction(
        OsmRelation relation, IReadOnlyDictionary<long, OsmWay> roads, IReadOnlyDictionary<long, int> nodeIndex,
        List<OsmTurnRestriction> into)
    {
        var tags = relation.Tags;
        var value = tags.GetValueOrDefault("restriction") ?? CarsOwn.Select(tags.GetValueOrDefault).FirstOrDefault(own => own is not null);
        if (value is null)
        {
            return tags.Keys.Any(key => key.StartsWith("restriction", StringComparison.Ordinal) && key.EndsWith(":conditional", StringComparison.Ordinal))
                ? "in force only at times"
                : "for other vehicles";
        }

        if (AtTimes.Any(tags.ContainsKey)) return "in force only at times";
        if (tags.GetValueOrDefault("except")?.Split(';').Any(mode => Cars.Contains(mode.Trim())) == true) return "not for cars";

        var only = value.StartsWith("only_", StringComparison.Ordinal);
        if (!only && !value.StartsWith("no_", StringComparison.Ordinal)) return "a value OSM does not define";

        var why = Members(relation, roads, nodeIndex, out var from, out var via, out var to);
        if (why is not null) return why;
        if (only && (from.Count != 1 || to.Count != 1)) return "an only_ turn from or onto more than one way";

        foreach (var fromWay in from)
        {
            foreach (var toWay in to)
            {
                into.Add(new OsmTurnRestriction { Relation = relation.Id, From = fromWay, Via = via, To = toWay, Only = only });
            }
        }

        return null;
    }

    static string? Connectivity(
        OsmRelation relation, IReadOnlyDictionary<long, OsmWay> roads, IReadOnlyDictionary<long, int> nodeIndex,
        List<OsmLaneLink> into)
    {
        if (Pairs(relation.Tags.GetValueOrDefault("connectivity")) is not { } pairs) return "a connectivity OSM does not define";

        var why = Members(relation, roads, nodeIndex, out var from, out var via, out var to);
        if (why is not null) return why;
        if (from.Count != 1 || to.Count != 1) return "lanes from or onto more than one way";

        foreach (var (fromLane, toLane) in pairs)
        {
            into.Add(new OsmLaneLink { Relation = relation.Id, From = from[0], Via = via, To = to[0], FromLane = fromLane, ToLane = toLane });
        }

        return null;
    }

    /// <summary>
    /// A <c>connectivity</c> value as its lane pairs — <c>1:1,(2)|2:3</c> — an optional lane in brackets being a
    /// lane a car may take as much as any other; null where it is not one.
    /// </summary>
    static List<(int From, int To)>? Pairs(string? value)
    {
        if (value is null) return null;

        var pairs = new List<(int, int)>();
        foreach (var group in value.Split('|'))
        {
            var sides = group.Split(':');
            if (sides.Length != 2 || Lane(sides[0]) is not { } from) return null;

            foreach (var onto in sides[1].Split(','))
            {
                if (Lane(onto) is not { } to) return null;

                pairs.Add((from, to));
            }
        }

        return pairs;

        static int? Lane(string text) =>
            int.TryParse(text.Trim().Trim('(', ')'), out var lane) && lane > 0 ? lane : null;
    }

    /// <summary>
    /// A relation's ways from and onto, and the node the turn is made at — every one on the map and the node on
    /// every way — or why not.
    /// </summary>
    static string? Members(
        OsmRelation relation, IReadOnlyDictionary<long, OsmWay> roads, IReadOnlyDictionary<long, int> nodeIndex,
        out List<long> from, out int via, out List<long> to)
    {
        from = [.. relation.Members.Where(member => member is { Role: "from", Type: "way" }).Select(member => member.Ref)];
        to = [.. relation.Members.Where(member => member is { Role: "to", Type: "way" }).Select(member => member.Ref)];
        via = -1;
        var vias = relation.Members.Where(member => member.Role == "via").ToArray();
        if (from.Count == 0 || to.Count == 0) return "no way from or onto";
        if (vias.Any(member => member.Type != "node")) return "made over a way";
        if (vias.Length > 1) return "made at more than one node";

        if (!from.Concat(to).All(roads.ContainsKey)) return "a way not on the map";

        if (vias.Length == 1)
        {
            if (!nodeIndex.TryGetValue(vias[0].Ref, out via)) return "a node not on the map";
        }
        else
        {
            // No via: the node the two ways share, where there is exactly one.
            var shared = roads[from[0]].Nodes.Intersect(roads[to[0]].Nodes).ToArray();
            if (shared.Length != 1) return "no via, and not one node the ways share";

            via = shared[0];
        }

        var at = via;
        return from.Concat(to).All(way => Array.IndexOf(roads[way].Nodes, at) >= 0) ? null : "a node off its ways";
    }
}
