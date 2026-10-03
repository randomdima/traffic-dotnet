using TrafficSimulation.CityGen.Traced;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Traced;

/// <summary>
/// <b>Where a car may turn is what OSM's tagging rules say</b> (Key:turn, Relation:restriction,
/// Relation:connectivity): a lane's arrows, the restrictions a car is under at a node, and the lanes a connectivity
/// relation joins — and a relation a car is not under, or one made over a way, read as nothing.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P4)]
public class OsmTurnsTests
{
    /// <summary>The way a turn is made from, at node 1 of the three it and the others share.</summary>
    const long From = 10;

    const long To = 20;

    /// <summary><b>A lane's arrows are every word of its entry</b>, and <c>none</c> is no arrow.</summary>
    [Fact]
    public void ALanesArrowsAreEveryWordOfItsEntry()
    {
        Assert.Equal(OsmArrows.Left | OsmArrows.Through, OsmTurns.ArrowsOf("left;through"));
        Assert.Equal(OsmArrows.None, OsmTurns.ArrowsOf("none"));
    }

    /// <summary><b>A word OSM does not define is no arrow</b>, and the rest of the entry is still read.</summary>
    [Fact]
    public void AWordOsmDoesNotDefineIsNoArrow()
    {
        Assert.Equal(OsmArrows.Right, OsmTurns.ArrowsOf("rihgt;right"));
    }

    /// <summary><b>A no_ restriction forbids its turn</b>: from its way, at its node, onto the other.</summary>
    [Fact]
    public void ANoRestrictionForbidsItsTurn()
    {
        var turn = Assert.Single(Read(Restriction(("restriction", "no_left_turn"))).Restrictions);

        Assert.Equal((From, 1, To, false), (turn.From, turn.Via, turn.To, turn.Only));
    }

    /// <summary><b>An only_ restriction is the one turn allowed</b> off its way at its node.</summary>
    [Fact]
    public void AnOnlyRestrictionIsTheOneTurnAllowed()
    {
        Assert.True(Assert.Single(Read(Restriction(("restriction", "only_straight_on"))).Restrictions).Only);
    }

    /// <summary><b>A restriction in force only at times is not read</b>: a map with no clock of day cannot keep it.</summary>
    [Fact]
    public void ARestrictionInForceOnlyAtTimesIsNotRead()
    {
        Assert.Empty(Read(Restriction(("restriction:conditional", "no_left_turn @ (07:00-19:00)"))).Restrictions);
        Assert.Empty(Read(Restriction(("restriction", "no_left_turn"), ("hour_on", "07:00"))).Restrictions);
    }

    /// <summary><b>A restriction cars are let off is not read</b>, and neither is one only another vehicle is under.</summary>
    [Fact]
    public void ARestrictionCarsAreNotUnderIsNotRead()
    {
        Assert.Empty(Read(Restriction(("restriction", "no_left_turn"), ("except", "psv;motorcar"))).Restrictions);
        Assert.Empty(Read(Restriction(("restriction:hgv", "no_left_turn"))).Restrictions);
    }

    /// <summary><b>A restriction made over a way is not read</b>: it is a turn through two junctions.</summary>
    [Fact]
    public void ARestrictionMadeOverAWayIsNotRead()
    {
        var overAWay = new OsmRelation
        {
            Id = 1, Tags = new() { ["type"] = "restriction", ["restriction"] = "no_u_turn" },
            Members = [Member("way", From, "from"), Member("way", 30, "via"), Member("way", To, "to")],
        };

        Assert.Empty(Read(overAWay).Restrictions);
    }

    /// <summary>
    /// <b>A connectivity relation joins the lanes it pairs</b>, a lane in brackets as much as any, over the node its
    /// two ways share where it names none.
    /// </summary>
    [Fact]
    public void AConnectivityRelationJoinsTheLanesItPairs()
    {
        var connectivity = new OsmRelation
        {
            Id = 1, Tags = new() { ["type"] = "connectivity", ["connectivity"] = "1:1,(2)|2:3" },
            Members = [Member("way", From, "from"), Member("way", To, "to")],
        };

        var links = Read(connectivity).LaneLinks;
        Assert.Equal([(1, 1, 1), (1, 2, 1), (2, 3, 1)], links.Select(link => (link.FromLane, link.ToLane, link.Via)));
    }

    static OsmRelation Restriction(params (string Key, string Value)[] tags)
    {
        var tagged = new Dictionary<string, string> { ["type"] = "restriction" };
        foreach (var (key, value) in tags) tagged[key] = value;

        return new OsmRelation
        {
            Id = 1, Tags = tagged, Members = [Member("way", From, "from"), Member("node", 101, "via"), Member("way", To, "to")],
        };
    }

    static OsmMember Member(string type, long reference, string role) => new() { Type = type, Ref = reference, Role = role };

    /// <summary>Read over two ways meeting at node 101, the second of the three nodes the map holds.</summary>
    static OsmTurns Read(OsmRelation relation)
    {
        var roads = new Dictionary<long, OsmWay>
        {
            [From] = new() { Id = From, Tags = new() { ["highway"] = "residential" }, Nodes = [0, 1] },
            [To] = new() { Id = To, Tags = new() { ["highway"] = "residential" }, Nodes = [1, 2] },
        };

        return OsmTurns.Read([relation], roads, new Dictionary<long, int> { [100] = 0, [101] = 1, [102] = 2 }, []);
    }
}
