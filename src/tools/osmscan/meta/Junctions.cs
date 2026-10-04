using System.Globalization;
using TrafficSimulation.CityGen.Traced;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>Every junction of the survey's roads</b>: its arms, how it is controlled and on what evidence, and every
/// movement through it — from which arm onto which, the turn it is, whether a car may make it and why not, and from
/// which lanes.
/// </summary>
/// <remarks>
/// <para>
/// <b>A junction is a node of three arms or more</b> (<see cref="Town"/>). Junctions a few metres apart joined by a
/// road — the two halves of a dual carriageway crossing a street — are one <b>cluster</b>, and a cluster is
/// controlled as one: a signal mapped at one of its nodes controls them all.
/// </para>
/// <para>
/// <b>A signal or sign is the junction's it stands before</b>: OSM maps one at the junction's node, or on an
/// approach at the stop line facing the traffic it holds (<c>traffic_signals:direction</c>), so each is walked
/// along its road to the first junction ahead of the traffic it faces. A crossing's own lights hold the cars on its
/// road, so a junction whose arms' crossings carry them is signalled even where no node says so. A light or sign a
/// camera saw (<see cref="Sightings"/>) is given to its junction the same way, and decides the control only where
/// OSM maps nothing that controls it.
/// </para>
/// <para>
/// <b>An unsigned junction is read by Ukraine's traffic rules</b> (Правила дорожнього руху, 16.11–16.12): a car
/// leaving a driveway, car park or yard gives way to the road (10.2), an unpaved road to a paved one, and between
/// equals the car from the right has the way. Which arm would be the main road where signs are only unmapped is a
/// hint, its class, and never a rule.
/// </para>
/// <para>
/// <b>A movement's lanes are its arrows</b> where its arm is painted (<c>turn:lanes</c>, for the junction its way
/// ends at, Key:turn): a slight arrow is its side's turn where the junction has one and straight on where it has
/// not, and a left arrow allows the U-turn as Ukraine's rules read it. An unpainted arm turns from its outermost
/// lanes (10.4): left and back from the leftmost, right from the rightmost, straight on from any.
/// </para>
/// </remarks>
internal static class Junctions
{
    /// <summary>How far along an arm its bearing is read, past any kink at the node.</summary>
    const double BearingReachM = 15;

    /// <summary>The farthest before a junction a signal is its: a stop line set back for a crossing and a queue.</summary>
    const double SignalReachM = 60;

    /// <summary>The farthest before a junction a stop or give-way sign is its.</summary>
    const double SignReachM = 40;

    /// <summary>The farthest from a junction a crossing is on its arm rather than between junctions.</summary>
    public const double CrossingReachM = 30;

    /// <summary>The longest road between two junctions that makes them one: a dual carriageway's median and its
    /// crossing.</summary>
    const double ClusterM = 30;

    /// <summary>A turn this near straight is straight on; a slight turn is up to the next, a sharp one past the last.</summary>
    const double StraightDeg = 30, SlightDeg = 60, SharpDeg = 140, BackDeg = 170;

    /// <summary>The farthest from a junction an outside source's sighting of a signal is taken as the junction's.</summary>
    const double HintM = 35;

    public static JunctionsFound Lay(
        Town town, Controls controls, List<ControlNode> seen, List<CrossingRecord> crossings, List<OsmoseIssue> osmose, Flags flags, string into, List<Written> written)
    {
        var crossingKinds = crossings.Where(crossing => crossing.Node is not null).ToDictionary(crossing => crossing.Node!.Value, crossing => crossing.Kind);

        // A seen zebra or pedestrians' light is a crossing only where the crossing layer laid it as one: where OSM maps
        // one near it, OSM's crossing node is the one held.
        var laid = crossings.Where(crossing => crossing.Seen is not null).Select(crossing => crossing.Seen!).ToHashSet();
        seen = [.. seen.Where(control => control.Tag("highway") != "crossing" || laid.Contains(control.Seen!))];
        var sightings = osmose.Where(issue => issue.SaysSignals).Select(issue => (Issue: issue, At: town.Plane.At(issue.Lat, issue.Lon))).ToArray();
        var sightingGrid = new Grid(HintM);
        for (var at = 0; at < sightings.Length; at++) sightingGrid.Add(at, Box.Empty.With(sightings[at].At));

        var junctions = new List<int>();
        for (var node = 0; node < town.NodeM.Length; node++)
        {
            if (town.IsJunction(node)) junctions.Add(node);
        }

        var cluster = Clusters(town, junctions);
        var held = Assign(town, controls.All.Concat(seen), crossingKinds, flags);
        var members = junctions.GroupBy(node => cluster[node]).ToDictionary(group => group.Key, group => group.ToArray());
        var restrictions = town.Extract.Turns.Restrictions.ToLookup(turn => turn.Via);
        var links = town.Extract.Turns.LaneLinks.ToLookup(link => link.Via);
        var relations = town.Extract.Relations.ToDictionary(relation => relation.Id);
        var conditional = Conditional(town);

        var found = new JunctionsFound();
        var records = new List<JunctionRecord>(junctions.Count);
        foreach (var node in junctions)
        {
            var arms = town.Arms(node);
            var armRecords = arms.Select(arm => ArmOf(town, arm)).ToArray();
            var mine = held.GetValueOrDefault(node) ?? [];
            var group = members[cluster[node]];
            var clustered = group.SelectMany(member => held.GetValueOrDefault(member) ?? []).ToList();
            var (control, from, rule, main, evidence) = Control(town, node, arms, clustered);
            var movements = Movements(town, node, arms, restrictions[node], links[node], conditional[node], relations);
            string[]? hints = null;
            if (control is not ("signals" or "blinking"))
            {
                var near = new HashSet<int>();
                sightingGrid.Near(Box.Empty.With(town.NodeM[node]).Grown(HintM), near);
                hints = [.. near.Select(at => sightings[at]).Where(sighting => (sighting.At - town.NodeM[node]).Length <= HintM)
                    .Select(sighting => string.Create(CultureInfo.InvariantCulture,
                        $"Osmose {sighting.Issue.Item}/{sighting.Issue.Class} {(sighting.At - town.NodeM[node]).Length:F0} m off: {sighting.Issue.Title}{(sighting.Issue.Subtitle is { } said ? $" ({said})" : "")}"))];
                if (hints.Length == 0) hints = null;
            }

            var record = new JunctionRecord
            {
                Node = town.NodeId(node),
                At = [town.Extract.Nodes.Lat[node], town.Extract.Nodes.Lon[node]],
                Cluster = group.Length > 1 ? town.NodeId(group.Min()) : null,
                Arms = armRecords,
                Control = control,
                ControlFrom = from,
                Regulated = control is "signals" or "signs" or "roundabout" or "priority_road",
                Rule = rule,
                LikelyMain = main,
                Evidence = evidence,
                Hints = hints,
                Held = mine.Count > 0 ? [.. mine.Select(at => new HeldRecord
                {
                    Node = at.By.Seen is null ? at.By.Id : null,
                    Seen = at.By.Seen,
                    Kind = at.Kind,
                    Arm = at.Approach is { } approach ? arms.IndexOf(approach) : null,
                    DistanceM = Math.Round(at.DistanceM, 1),
                    Value = at.Value,
                })] : null,
                Movements = movements,
            };
            records.Add(record);
            found.ByNode[node] = record;
        }

        written.Add(Layers.Write(into, "junctions",
            "Every node of three road arms or more: its arms, its control, what decided it — OSM, a camera's sighting where OSM maps nothing, or the rules — and the evidence, the signals, signs and crossings it holds, and every movement through it with whether a car may make it and from which lanes.",
            ["survey", "osm-control", "osmose", Mapillary.Name],
            records,
            new
            {
                control = records.GroupBy(record => record.Control).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count()),
                controlFrom = records.GroupBy(record => record.ControlFrom).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count()),
                rules = records.Where(record => record.Rule is not null).GroupBy(record => record.Rule!).ToDictionary(g => g.Key, g => g.Count()),
                clusters = records.Where(record => record.Cluster is not null).Select(record => record.Cluster).Distinct().Count(),
                hintedSignals = records.Count(record => record.Hints is not null),
                movements = records.Sum(record => record.Movements.Length),
                forbidden = records.Sum(record => record.Movements.Count(movement => !movement.Allowed)),
                conditional = records.Sum(record => record.Movements.Count(movement => movement.Conditional is not null)),
                paintedArms = records.Sum(record => record.Arms.Count(arm => arm.Arrows is not null)),
            }));
        return found;
    }

    static JunctionArm ArmOf(Town town, Arm arm)
    {
        var way = town.Roads[arm.Road];
        var arriving = Arriving(way.Carriageway!, -arm.Direction);
        var marked = arm.Ends(town) && arriving.Any(lane => lane.Turn is not null);
        return new JunctionArm
        {
            Way = way.Id,
            At = arm.At,
            Along = arm.Direction,
            Highway = way.Tags["highway"],
            Name = way.Tag("name"),
            BearingDeg = (int)Math.Round(arm.BearingDeg(town, BearingReachM)),
            In = arm.In(town),
            Out = arm.Out(town),
            LanesIn = arriving.Count,
            LanesOut = Arriving(way.Carriageway!, arm.Direction).Count,
            Arrows = marked ? [.. arriving.Select(lane => lane.Turn ?? "none")] : null,
            Roundabout = way.Tag("junction") is "roundabout" or "circular" ? true : null,
            Priority = way.Tag("priority_road"),
            Surface = way.Tag("surface"),
        };
    }

    /// <summary>The lanes a car travelling a way one way is in, left to right as it sees them: those both ways share,
    /// which are the middle of the road, then its own.</summary>
    static List<OsmLane> Arriving(OsmCarriageway carriageway, int travel)
    {
        var own = travel > 0 ? OsmLaneWay.Forward : OsmLaneWay.Backward;
        var shared = carriageway.Lanes.Where(lane => lane.Way == OsmLaneWay.Both);
        var mine = carriageway.Lanes.Where(lane => lane.Way == own);
        return travel > 0 ? [.. shared, .. mine] : [.. shared.Reverse(), .. mine.Reverse()];
    }

    /// <summary>
    /// Junctions joined by a short road into clusters, by union: both of three main arms or more, and the road
    /// between them neither a service road nor a track.
    /// </summary>
    static Dictionary<int, int> Clusters(Town town, List<int> junctions)
    {
        var parent = junctions.ToDictionary(node => node, node => node);
        int Root(int node)
        {
            while (parent[node] != node) node = parent[node] = parent[parent[node]];
            return node;
        }

        bool Main(int road) => town.Highway(road) is not ("service" or "track" or "busway" or "bus_guideway" or "raceway" or "escape");
        int MainArms(int node) => town.Arms(node).Count(arm => Main(arm.Road));

        foreach (var node in junctions)
        {
            if (MainArms(node) < 3) continue;

            foreach (var arm in town.Arms(node))
            {
                if (!Main(arm.Road)) continue;

                foreach (var (met, _, _) in arm.Walk(town, ClusterM))
                {
                    if (!town.IsJunction(met)) continue;
                    if (MainArms(met) >= 3) parent[Root(met)] = Root(node);
                    break;
                }
            }
        }

        return junctions.ToDictionary(node => node, Root);
    }

    /// <summary>One control a junction holds: the node, what it is, how far before the junction and on which arm.</summary>
    sealed record Holding(int Junction, ControlNode By, string Kind, string? Value, double DistanceM, Arm? Approach);

    /// <summary>
    /// Every signal, sign and signalled crossing given to the junction it stands before, walked along its road toward
    /// the traffic it faces; one that reaches no junction is raised as a flag where it controls cars. A crossing on a
    /// road is of the kind the crossing layer read off its node and its way together.
    /// </summary>
    static Dictionary<int, List<Holding>> Assign(Town town, IEnumerable<ControlNode> controls, Dictionary<long, string> crossingKinds, Flags flags)
    {
        var held = new Dictionary<int, List<Holding>>();
        foreach (var control in controls)
        {
            var (kind, value, reachM) = Classify(control.Tags, crossingKinds.GetValueOrDefault(control.Id));
            if (kind is null) continue;

            var hit = FirstJunction(town, control, reachM);
            if (hit is { } found)
            {
                (held.TryGetValue(found.Node, out var list) ? list : held[found.Node] = []).Add(new Holding(found.Node, control, kind, value, found.DistanceM, found.Approach));
            }
            else if (kind is "signals" or "stop" or "give_way" && control.Seen is null)
            {
                var why = control.Node is null && control.SnappedRoad is null ? "beside no road" : $"no junction within {reachM:F0} m ahead of the traffic it faces";
                if (kind != "signals" || control.Tag("crossing") is null)
                {
                    flags.Raise($"{kind}_without_junction", town, control.At, why, $"n{control.Id}");
                }
            }
        }

        return held;
    }

    static (string? Kind, string? Value, double ReachM) Classify(Dictionary<string, string> tags, string? crossingKind)
    {
        if (Controls.Signals(tags) is { } signals) return ("signals", signals, SignalReachM);

        crossingKind ??= Crossings.Kind(tags);
        return tags.GetValueOrDefault("highway") switch
        {
            "stop" => ("stop", tags.GetValueOrDefault("stop"), SignReachM),
            "give_way" => ("give_way", null, SignReachM),
            "mini_roundabout" => ("mini_roundabout", null, 0),
            "crossing" when crossingKind == "signals" => ("crossing_signals", null, CrossingReachM),
            "crossing" => ("crossing", crossingKind, CrossingReachM),
            _ => (null, null, 0),
        };
    }

    /// <summary>
    /// The first junction ahead of the traffic a control faces, within a reach: the node itself where it is one, else
    /// along its road each way it faces, the nearer taken.
    /// </summary>
    static (int Node, double DistanceM, Arm? Approach)? FirstJunction(Town town, ControlNode control, double reachM)
    {
        if (control.Node is { } node)
        {
            if (town.IsJunction(node)) return (node, 0, null);

            (int Node, double DistanceM, Arm? Approach)? best = null;
            foreach (var arm in town.Arms(node))
            {
                if (control.Facing != 0 && arm.Direction != control.Facing) continue;

                best = Nearer(best, Ahead(town, arm, 0, reachM));
            }

            return best;
        }

        if (control.SnappedRoad is not { } road) return null;

        var nodes = town.Roads[road].Nodes;
        var segment = town.NearestRoad(control.At, control.SnappedOffM + 1, candidate => candidate == road)!.Value.Segment;
        var startM = town.AlongM(road, segment);
        var endM = town.AlongM(road, segment + 1);
        var forward = control.Facing < 0 ? null : town.IsJunction(nodes[segment + 1])
            ? ((int Node, double DistanceM, Arm? Approach)?)(nodes[segment + 1], endM - control.SnappedAlongM, new Arm(road, segment + 1, -1))
            : Ahead(town, new Arm(road, segment + 1, +1), endM - control.SnappedAlongM, reachM);
        var backward = control.Facing > 0 ? null : town.IsJunction(nodes[segment])
            ? ((int Node, double DistanceM, Arm? Approach)?)(nodes[segment], control.SnappedAlongM - startM, new Arm(road, segment, +1))
            : Ahead(town, new Arm(road, segment, -1), control.SnappedAlongM - startM, reachM);
        return Nearer(forward?.DistanceM <= reachM ? forward : null, backward?.DistanceM <= reachM ? backward : null);
    }

    static (int Node, double DistanceM, Arm? Approach)? Ahead(Town town, Arm arm, double alreadyM, double reachM)
    {
        foreach (var (met, atM, along) in arm.Walk(town, reachM - alreadyM))
        {
            if (town.IsJunction(met)) return (met, alreadyM + atM, along with { Direction = -along.Direction });
        }

        return null;
    }

    static (int Node, double DistanceM, Arm? Approach)? Nearer((int Node, double DistanceM, Arm? Approach)? a, (int Node, double DistanceM, Arm? Approach)? b) =>
        a is null ? b : b is null ? a : a.Value.DistanceM <= b.Value.DistanceM ? a : b;

    /// <summary>
    /// A junction's control off what OSM maps holding it, and — only where OSM maps nothing that controls it — off
    /// what a camera saw holding it too; with which decided it: <c>osm</c>, <c>seen</c>, or <c>rules</c> where
    /// neither did and the traffic rules read it.
    /// </summary>
    static (string Control, string From, string? Rule, int[]? Main, string[] Evidence) Control(Town town, int node, List<Arm> arms, List<Holding> held)
    {
        var mapped = Decide(town, node, arms, [.. held.Where(at => at.By.Seen is null)]);
        if (mapped.Control != "unsigned") return (mapped.Control, "osm", mapped.Rule, mapped.Main, mapped.Evidence);

        var seen = held.Any(at => at.By.Seen is not null) ? Decide(town, node, arms, held) : mapped;
        return seen.Control != "unsigned" ? (seen.Control, "seen", seen.Rule, seen.Main, seen.Evidence) : (mapped.Control, "rules", mapped.Rule, mapped.Main, mapped.Evidence);
    }

    /// <summary>A junction's control off what holds it, the rule an unsigned one is read by, the arms likeliest its main road, and why.</summary>
    static (string Control, string? Rule, int[]? Main, string[] Evidence) Decide(Town town, int node, List<Arm> arms, List<Holding> held)
    {
        var evidence = new List<string>();
        void Say(Holding at) => evidence.Add(string.Create(CultureInfo.InvariantCulture,
            $"{at.By.Label} {at.Kind}{(at.Value is null ? "" : "=" + at.Value)} {(at.DistanceM == 0 ? "at" : $"{at.DistanceM:F0} m before")} " +
            $"{(at.Junction == node ? "this node" : $"n{town.NodeId(at.Junction)} of its cluster")}"));

        var signals = held.Where(at => at.Kind == "signals").ToList();
        if (signals.Count > 0)
        {
            signals.ForEach(Say);
            return (signals.All(at => at.Value == "blinking_yellow") ? "blinking" : "signals", null, null, [.. evidence]);
        }

        var lit = held.Where(at => at.Kind == "crossing_signals").ToList();
        if (lit.Select(at => (at.Junction, at.Approach)).Distinct().Count() >= 2)
        {
            lit.ForEach(Say);
            evidence.Add("signalled crossings on two arms or more hold its cars");
            return ("signals", null, null, [.. evidence]);
        }

        if (arms.Any(arm => town.Roads[arm.Road].Tag("junction") is "roundabout" or "circular") || held.Any(at => at.Kind == "mini_roundabout"))
        {
            evidence.Add("a roundabout passes through it");
            return ("roundabout", null, null, [.. evidence]);
        }

        var signs = held.Where(at => at.Kind is "stop" or "give_way").ToList();
        if (signs.Count > 0)
        {
            signs.ForEach(Say);
            return ("signs", null, null, [.. evidence]);
        }

        var priority = arms.Where(arm => town.Roads[arm.Road].Tag("priority_road") is "designated" or "yes_unposted").ToArray();
        if (priority.Length > 0)
        {
            evidence.Add($"priority_road on {string.Join(", ", priority.Select(arm => $"w{town.Roads[arm.Road].Id}").Distinct())}");
            return ("priority_road", null, [.. priority.Select(arm => arms.IndexOf(arm))], [.. evidence]);
        }

        var yard = arms.Select(arm => Yard(town.Roads[arm.Road].Tags)).ToArray();
        var paved = arms.Select(arm => Paved(town.Roads[arm.Road].Tag("surface"), town.Highway(arm.Road))).ToArray();
        var rank = arms.Select(arm => Rank(town.Highway(arm.Road))).ToArray();
        var top = rank.Max();
        int[]? main = rank.Distinct().Count() > 1 ? [.. Enumerable.Range(0, arms.Count).Where(at => rank[at] == top)] : null;

        if (yard.Any(on => on) && yard.Any(on => !on))
        {
            evidence.Add("arms off a driveway, car park, yard or living street give way to the road (ПДР 10.2)");
            return ("unsigned", "adjacent_territory", [.. Enumerable.Range(0, arms.Count).Where(at => !yard[at])], [.. evidence]);
        }

        if (paved.Any(on => on == true) && paved.Any(on => on == false))
        {
            evidence.Add("unpaved arms give way to paved ones (ПДР 16.11)");
            return ("unsigned", "unpaved_yields", [.. Enumerable.Range(0, arms.Count).Where(at => paved[at] == true)], [.. evidence]);
        }

        evidence.Add("no signal, sign or priority road mapped: the car from the right has the way (ПДР 16.12)");
        return ("unsigned", "right_hand", main, [.. evidence]);
    }

    /// <summary>Whether a road is a way off a property onto the road rather than a road: a driveway, car park aisle,
    /// yard lane or drive-through, or a living street.</summary>
    public static bool Yard(Dictionary<string, string> tags) =>
        tags.GetValueOrDefault("highway") == "living_street"
        || (tags.GetValueOrDefault("highway") == "service" && tags.GetValueOrDefault("service") is "driveway" or "parking_aisle" or "alley" or "drive-through");

    /// <summary>Whether a road is paved by its surface, else by its class; null where neither says.</summary>
    public static bool? Paved(string? surface, string highway) => surface switch
    {
        null => highway == "track" ? false : null,
        "asphalt" or "paved" or "concrete" or "concrete:plates" or "concrete:lanes" or "paving_stones" or "sett" or "cobblestone"
            or "cobblestone:flattened" or "unhewn_cobblestone" or "metal" or "wood" or "bricks" or "chipseal" => true,
        _ => false,
    };

    /// <summary>A road class's rank, the higher the more important, by OSM's own ladder.</summary>
    public static int Rank(string highway) => highway.Replace("_link", "") switch
    {
        "motorway" => 9,
        "trunk" => 8,
        "primary" => 7,
        "secondary" => 6,
        "tertiary" => 5,
        "unclassified" or "road" => 4,
        "residential" => 3,
        "living_street" => 2,
        "service" or "busway" => 1,
        _ => 0,
    };

    /// <summary>Restrictions OSM keeps in force only at times, by their via node: the value with its condition.</summary>
    static ILookup<int, (long From, long To, string Said, long Relation)> Conditional(Town town)
    {
        var found = new List<(int Via, long From, long To, string Said, long Relation)>();
        foreach (var relation in town.Extract.Relations)
        {
            var said = relation.Tags.GetValueOrDefault("restriction:conditional")
                       ?? relation.Tags.GetValueOrDefault("restriction:motorcar:conditional")
                       ?? (relation.Tags.ContainsKey("hour_on") || relation.Tags.ContainsKey("day_on")
                           ? $"{relation.Tags.GetValueOrDefault("restriction")} @ ({relation.Tags.GetValueOrDefault("day_on")}-{relation.Tags.GetValueOrDefault("day_off")} {relation.Tags.GetValueOrDefault("hour_on")}-{relation.Tags.GetValueOrDefault("hour_off")})"
                           : null);
            if (said is null) continue;

            var via = relation.Members.FirstOrDefault(member => member.Role == "via" && member.Type == "node");
            if (via is null || !town.NodeIndex.TryGetValue(via.Ref, out var node)) continue;

            foreach (var from in relation.Members.Where(member => member.Role == "from" && member.Type == "way"))
            {
                foreach (var to in relation.Members.Where(member => member.Role == "to" && member.Type == "way"))
                {
                    found.Add((node, from.Ref, to.Ref, said, relation.Id));
                }
            }
        }

        return found.ToLookup(entry => entry.Via, entry => (entry.From, entry.To, entry.Said, entry.Relation));
    }

    static MovementRecord[] Movements(
        Town town, int node, List<Arm> arms, IEnumerable<OsmTurnRestriction> restrictions, IEnumerable<OsmLaneLink> links,
        IEnumerable<(long From, long To, string Said, long Relation)> conditional, Dictionary<long, OsmRelation> relations)
    {
        var bearings = arms.Select(arm => arm.BearingDeg(town, BearingReachM)).ToArray();
        var movements = new List<MovementRecord>();
        var restricted = restrictions.ToArray();
        var linked = links.ToArray();
        var timed = conditional.ToArray();
        for (var from = 0; from < arms.Count; from++)
        {
            if (!arms[from].In(town)) continue;

            var fromWay = town.Roads[arms[from].Road];
            var arriving = Arriving(fromWay.Carriageway!, -arms[from].Direction);
            var marked = arms[from].Ends(town) && arriving.Any(lane => lane.Turn is not null);
            var heading = (bearings[from] + 180) % 360;
            var turns = new List<(int To, string Turn, double AngleDeg)>();
            for (var to = 0; to < arms.Count; to++)
            {
                if (!arms[to].Out(town)) continue;

                var angle = to == from ? 180 : Shape.TurnDeg(heading, bearings[to]);
                turns.Add((to, Turn(angle, to == from), angle));
            }

            var hasLeft = turns.Any(turn => turn.Turn is "left" or "sharp_left");
            var hasRight = turns.Any(turn => turn.Turn is "right" or "sharp_right");
            foreach (var (to, turn, angle) in turns)
            {
                var toWay = town.Roads[arms[to].Road];
                string? why = null;
                var sameWay = fromWay.Id == toWay.Id && to != from;
                foreach (var restriction in restricted)
                {
                    if (restriction.From != fromWay.Id || sameWay) continue;

                    var value = relations.TryGetValue(restriction.Relation, out var relation)
                        ? relation.Tags.GetValueOrDefault("restriction") ?? relation.Tags.GetValueOrDefault("restriction:motorcar") ?? "restriction"
                        : "restriction";
                    if (!restriction.Only && restriction.To == toWay.Id) why = $"{value} r{restriction.Relation}";
                    else if (restriction.Only && restriction.To != toWay.Id) why = $"{value} r{restriction.Relation}";
                }

                int[] lanes = [.. Enumerable.Range(0, arriving.Count).Where(lane => marked
                    ? Serves(arriving[lane].Arrows, arriving[lane].Turn, turn, hasLeft, hasRight)
                    : turn switch
                    {
                        "through" => true,
                        "u_turn" or "slight_left" or "left" or "sharp_left" => lane == 0,
                        _ => lane == arriving.Count - 1,
                    }).Select(lane => lane + 1)];
                if (why is null && marked && lanes.Length == 0) why = "no lane's arrows name it";

                var timedSaid = timed.Where(entry => entry.From == fromWay.Id && (entry.To == toWay.Id) != entry.Said.StartsWith("only_", StringComparison.Ordinal))
                    .Select(entry => $"{entry.Said} r{entry.Relation}").FirstOrDefault();
                var lanePairs = linked.Where(link => link.From == fromWay.Id && link.To == toWay.Id).Select(link => $"{link.FromLane}:{link.ToLane}").ToArray();
                movements.Add(new MovementRecord
                {
                    From = from,
                    To = to,
                    Turn = turn,
                    AngleDeg = (int)Math.Round(angle),
                    Allowed = why is null,
                    Why = why,
                    Lanes = lanes,
                    Conditional = timedSaid,
                    LaneLinks = lanePairs.Length > 0 ? lanePairs : null,
                });
            }
        }

        return [.. movements];
    }

    static string Turn(double angleDeg, bool back)
    {
        if (back || Math.Abs(angleDeg) >= BackDeg) return "u_turn";

        var side = angleDeg > 0 ? "right" : "left";
        var size = Math.Abs(angleDeg);
        return size <= StraightDeg ? "through" : size <= SlightDeg ? $"slight_{side}" : size <= SharpDeg ? side : $"sharp_{side}";
    }

    /// <summary>Whether a painted lane serves a turn: its arrows name the turn's side, a slight arrow being its side's
    /// turn where the junction has one and straight on where it has not, a lane painted none straight on only.</summary>
    static bool Serves(OsmArrows arrows, string? entry, string turn, bool hasLeft, bool hasRight)
    {
        if (arrows == OsmArrows.None) return entry is null or "none" && turn == "through";

        var through = (arrows & OsmArrows.Through) != 0
                      || (!hasLeft && (arrows & OsmArrows.SlightLeft) != 0)
                      || (!hasRight && (arrows & OsmArrows.SlightRight) != 0);
        var left = (arrows & (OsmArrows.Left | OsmArrows.SharpLeft)) != 0 || (hasLeft && (arrows & OsmArrows.SlightLeft) != 0);
        var right = (arrows & (OsmArrows.Right | OsmArrows.SharpRight)) != 0 || (hasRight && (arrows & OsmArrows.SlightRight) != 0);
        return turn switch
        {
            "through" => through,
            "slight_left" => left || (arrows & OsmArrows.SlightLeft) != 0,
            "slight_right" => right || (arrows & OsmArrows.SlightRight) != 0,
            "left" or "sharp_left" => left,
            "right" or "sharp_right" => right,
            "u_turn" => (arrows & (OsmArrows.Reverse | OsmArrows.Left)) != 0,
            _ => false,
        };
    }
}

/// <summary>What the junction layer found that later layers read: each junction's record by its survey node.</summary>
internal sealed class JunctionsFound
{
    public Dictionary<int, JunctionRecord> ByNode { get; } = [];
}

internal sealed class JunctionRecord
{
    /// <summary>The junction's OSM node id.</summary>
    public required long Node { get; init; }

    /// <summary>Lat, lon in 1e-7°.</summary>
    public required int[] At { get; init; }

    /// <summary>The cluster it is controlled with, named by the lowest index node of it; null alone.</summary>
    public long? Cluster { get; init; }

    public required JunctionArm[] Arms { get; init; }

    /// <summary><c>signals</c>, <c>blinking</c>, <c>roundabout</c>, <c>signs</c>, <c>priority_road</c> or <c>unsigned</c>.</summary>
    public required string Control { get; init; }

    /// <summary>
    /// What decided its control: <c>osm</c>, what OSM maps; <c>seen</c>, a camera's sighting where OSM maps nothing that
    /// controls it; <c>rules</c>, neither, the traffic rules reading it.
    /// </summary>
    public required string ControlFrom { get; init; }

    /// <summary>Whether something mapped or seen regulates it: signals, signs, a roundabout or a priority road.</summary>
    public required bool Regulated { get; init; }

    /// <summary>The rule an unsigned junction is read by: <c>adjacent_territory</c>, <c>unpaved_yields</c> or <c>right_hand</c>.</summary>
    public string? Rule { get; init; }

    /// <summary>The arms with the way, by their index: the road under a rule, else the highest class as a hint.</summary>
    public int[]? LikelyMain { get; init; }

    public required string[] Evidence { get; init; }

    /// <summary>
    /// For a junction no signal holds, Osmose's reading of its crossings that one is near (2090). A hint to weigh,
    /// never a control; a signal a camera saw is a sighting, held as one.
    /// </summary>
    public string[]? Hints { get; init; }

    /// <summary>Every signal, sign and crossing this node holds, its own and not its cluster's, mapped or seen.</summary>
    public HeldRecord[]? Held { get; init; }

    public required MovementRecord[] Movements { get; init; }
}

internal sealed class JunctionArm
{
    public required long Way { get; init; }

    /// <summary>
    /// The junction's place in the way's node list, from 0: a way drawn through its own node twice — a loop closed
    /// on its middle — leaves it by two arms the same way, told apart by this.
    /// </summary>
    public required int At { get; init; }

    /// <summary>+1 where the arm leaves along the way as drawn, −1 against it.</summary>
    public required int Along { get; init; }

    public required string Highway { get; init; }

    public string? Name { get; init; }

    /// <summary>The bearing it leaves on, clockwise from north.</summary>
    public required int BearingDeg { get; init; }

    /// <summary>Whether a car may come into the junction along it.</summary>
    public required bool In { get; init; }

    public required bool Out { get; init; }

    public required int LanesIn { get; init; }

    public required int LanesOut { get; init; }

    /// <summary>Each lane in's <c>turn:lanes</c> entry, left to right as its traffic sees them, where the arm is painted.</summary>
    public string[]? Arrows { get; init; }

    public bool? Roundabout { get; init; }

    public string? Priority { get; init; }

    public string? Surface { get; init; }
}

internal sealed class HeldRecord
{
    /// <summary>The OSM node held; null for a sighting.</summary>
    public long? Node { get; init; }

    /// <summary>The sighting held (<see cref="SeenRecord"/>), by its id; null for an OSM node.</summary>
    public string? Seen { get; init; }

    /// <summary><c>signals</c>, <c>stop</c>, <c>give_way</c>, <c>mini_roundabout</c>, <c>crossing_signals</c> or <c>crossing</c>.</summary>
    public required string Kind { get; init; }

    public int? Arm { get; init; }

    public required double DistanceM { get; init; }

    public string? Value { get; init; }
}

internal sealed class MovementRecord
{
    public required int From { get; init; }

    public required int To { get; init; }

    /// <summary><c>through</c>, <c>slight_left</c>, <c>left</c>, <c>sharp_left</c>, the same right, or <c>u_turn</c>.</summary>
    public required string Turn { get; init; }

    /// <summary>The turn's angle, positive to the right.</summary>
    public required int AngleDeg { get; init; }

    public required bool Allowed { get; init; }

    /// <summary>Why a car may not: the restriction relation, or that no lane's arrows name it.</summary>
    public string? Why { get; init; }

    /// <summary>
    /// The lanes in it is made from, counted from one at the left as the traffic sees them: by the arrows where its
    /// arm is painted (<see cref="JunctionArm.Arrows"/>), else by ПДР 10.4.
    /// </summary>
    public required int[] Lanes { get; init; }

    /// <summary>A restriction in force only at times, with its condition.</summary>
    public string? Conditional { get; init; }

    /// <summary>Lane pairs a connectivity relation joins, from:to.</summary>
    public string[]? LaneLinks { get; init; }
}
