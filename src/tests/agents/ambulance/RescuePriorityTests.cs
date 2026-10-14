using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Ambulance;

/// <summary>
/// AMB-4 as the road states it: <b>a rung orders who waits and never who is driven into</b>. The whole of
/// the ambulance's priority is one rung of <see cref="ClaimPriority"/>, so this is where the promise that it
/// takes only what a rung may take is asserted rather than assumed.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P2)]
public class RescuePriorityTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly TownWays Ways = TownWays.OfTheRoad(RoadGraph.Build(Towns.Of("Test"), Config));

    /// <summary>
    /// Whether a holder asking for a stretch of one way at <paramref name="mine"/> keeps it against another asking for
    /// the same stretch at <paramref name="theirs"/> — the other asked first, so only the rung can give it to the first.
    /// </summary>
    static bool Keeps(ClaimPriority mine, ClaimPriority theirs)
    {
        var index = new LaneOccupancy(Ways, WayCrossings.None);
        var other = index.BeginHold(2, LaneRoster.Driving);
        index.Ask(other, 0, 10f, 20f, 10f, theirs);
        var asker = index.BeginHold(1, LaneRoster.Driving);
        index.Ask(asker, 0, 10f, 20f, 10f, mine);
        index.Answer();
        return !index.CutOf(asker).Refused;
    }

    /// <summary>
    /// A rescue outranks every ordinary movement, the paint and <b>a closed road</b> (SRV-6), so none of their
    /// plans keeps ground a rescue asks for — the last of those is the whole of "the officer lets the other
    /// services through".
    /// </summary>
    /// <remarks>
    /// <b>The movements are named by their byte</b>, because a theory's parameters are as public as the test
    /// and the ladder is not: 6, 7 and 8 are straight on, ordinary and the turn across.
    /// </remarks>
    [Theory]
    [InlineData((byte)6)]
    [InlineData((byte)7)]
    [InlineData((byte)8)]
    public void ARescueKeepsGroundAgainstEveryOrdinaryMovement(byte theirs)
    {
        Assert.True(Keeps(ClaimPriority.Special, (ClaimPriority)theirs));
        Assert.True(Keeps(ClaimPriority.Special, ClaimPriority.Signal));
        Assert.True(Keeps(ClaimPriority.Special, ClaimPriority.Afoot));
    }

    /// <summary>And the mirror of it: everything below a rescue gives it way, which is what "yield" means here.</summary>
    [Theory]
    [InlineData((byte)6)]
    [InlineData((byte)7)]
    [InlineData((byte)8)]
    public void EverythingBelowARescueGivesItWay(byte mine)
    {
        Assert.False(Keeps((ClaimPriority)mine, ClaimPriority.Special));
    }

    /// <summary>
    /// <b>Ground its holder can no longer stop short of is nobody's to take</b> — the one plan a rescue does
    /// not outrank, because the ladder orders who waits and is not a licence to drive into somebody.
    /// </summary>
    [Fact]
    public void ARescueGivesWayToGroundItsHolderCanNoLongerStopShortOf()
    {
        Assert.False(Keeps(ClaimPriority.Special, ClaimPriority.Committed));
    }
}
