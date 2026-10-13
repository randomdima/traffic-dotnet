using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Routing;
using Xunit;

namespace TrafficSimulation.Tests.World.Foot;

/// <summary>
/// <b>A walk sets off from the entry the search priced</b> (<see cref="WalkingNetwork.SetOffFrom"/>), where a body at a
/// loop's end stands at two places of one contracted run (WLK-8).
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P4)]
public class WalkingNetworkTests
{
    const int Link = 7;
    const float LinkLengthM = 40f;

    static readonly RouteEntry[] AtALoopsEnd =
    [
        new(Link, 5f, LinkLengthM - 5f),
        new(Link, 30f, LinkLengthM - 30f),
    ];

    /// <summary>A route that goes on past the link sets off from the place nearer its end.</summary>
    [Fact]
    public void ARouteThatGoesOnSetsOffFromThePlaceNearerTheLinksEnd()
    {
        var entry = WalkingNetwork.SetOffFrom(AtALoopsEnd, [Link, Link + 1], new RouteGoal(Link + 1, 3f));

        Assert.Equal(AtALoopsEnd[1], entry);
    }

    /// <summary>A goal between the two places is walked to from the one short of it, and never set off past.</summary>
    [Fact]
    public void AGoalBetweenTheTwoIsWalkedToFromTheOneShortOfIt()
    {
        var entry = WalkingNetwork.SetOffFrom(AtALoopsEnd, [Link], new RouteGoal(Link, 20f));

        Assert.Equal(AtALoopsEnd[0], entry);
    }
}
