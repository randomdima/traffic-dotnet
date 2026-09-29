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
    /// <summary>A holder asking for the metre at <paramref name="rung"/>, as near it as the other and able to stop short of it.</summary>
    static PlannedAsk Asking(ClaimPriority rung) =>
        new(0, 1, LaneRoster.Driving, rung, 10f, 10f, AheadM: 5f, CommittedToM: float.NegativeInfinity, AlongMps: 10f);

    /// <summary>Another holder's plan over the same metre, at <paramref name="rung"/>.</summary>
    static LaneClaim Planned(ClaimPriority rung, bool committed = false) =>
        new(10f, 20f, 10f, 2, rung, AheadM: 5f, CommittedToM: committed ? 20f : float.NegativeInfinity);

    static bool Keeps(ClaimPriority mine, in LaneClaim theirs) =>
        LaneOccupancy.Beats(Asking(mine), 10f, false, theirs, false);

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
        Assert.True(Keeps(ClaimPriority.Special, Planned((ClaimPriority)theirs)));
        Assert.True(Keeps(ClaimPriority.Special, Planned(ClaimPriority.Closed)));
        Assert.True(Keeps(ClaimPriority.Special, Planned(ClaimPriority.Afoot)));
    }

    /// <summary>And the mirror of it: everything below a rescue gives it way, which is what "yield" means here.</summary>
    [Theory]
    [InlineData((byte)6)]
    [InlineData((byte)7)]
    [InlineData((byte)8)]
    public void EverythingBelowARescueGivesItWay(byte mine)
    {
        Assert.False(Keeps((ClaimPriority)mine, Planned(ClaimPriority.Special)));
        Assert.False(Keeps(ClaimPriority.Closed, Planned(ClaimPriority.Special)));
    }

    /// <summary>
    /// <b>SRV-6, both halves at once</b>: a closed road keeps its ground against every ordinary movement, and
    /// gives it up to a vehicle answering a call. It is the whole mechanism of the closure — one rung in one
    /// order — so it is asserted here beside the rescue's rather than in a slice of its own.
    /// </summary>
    [Theory]
    [InlineData((byte)6)]
    [InlineData((byte)7)]
    [InlineData((byte)8)]
    public void AClosedRoadKeepsOrdinaryTrafficOutAndLetsACallThrough(byte theirs)
    {
        Assert.True(Keeps(ClaimPriority.Closed, Planned((ClaimPriority)theirs)));
        Assert.False(Keeps(ClaimPriority.Closed, Planned(ClaimPriority.Special)));
    }

    /// <summary>
    /// <b>Ground its holder can no longer stop short of is nobody's to take</b> — the one plan a rescue does
    /// not outrank, because the ladder orders who waits and is not a licence to drive into somebody.
    /// </summary>
    [Fact]
    public void ARescueGivesWayToGroundItsHolderCanNoLongerStopShortOf()
    {
        Assert.False(Keeps(ClaimPriority.Special, Planned(ClaimPriority.FirmAcross, committed: true)));
    }

    /// <summary>
    /// The ladder is an order and the order runs one way: a byte comparison is the whole mechanism, so the
    /// one thing that could break it silently is somebody inserting a rung in the wrong place.
    /// </summary>
    [Fact]
    public void ARescueStandsBetweenTheTrafficAndCommittedGround()
    {
        Assert.True(ClaimPriority.Special < ClaimPriority.FirmStraight);
        Assert.True(ClaimPriority.Special > ClaimPriority.Committed);
    }

    /// <summary>
    /// <b>And a closed road stands between the traffic and a rescue</b> (SRV-6) — the one placing in the
    /// order that gives a closure both of the things it is for, and the one an inserted rung could silently
    /// move.
    /// </summary>
    [Fact]
    public void AClosedRoadStandsBetweenTheTrafficAndARescue()
    {
        Assert.True(ClaimPriority.Closed < ClaimPriority.FirmStraight);
        Assert.True(ClaimPriority.Closed > ClaimPriority.Special);
    }
}
