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
    /// <summary>Ground granted and not reached, at the rung its movement carries.</summary>
    static LaneClaim Claim(ClaimPriority rung) => new(10f, 20f, 10f, 0f, 7, rung);

    /// <summary>A body standing on the ground, whichever way it was measured.</summary>
    static LaneClaim Body(bool onItsLine) =>
        new(10f, 20f, 20f, 0f, 7, ClaimPriority.Hard, OnItsLine: onItsLine);

    /// <summary>
    /// A rescue outranks every ordinary movement, the paint and <b>a closed road</b> (SRV-6), so none of
    /// their claims refuses it — the last of those is the whole of "the officer lets the other services
    /// through".
    /// </summary>
    /// <remarks>
    /// <b>The movements are named by their byte</b>, because a theory's parameters are as public as the
    /// test and the ladder is not: 4, 5 and 6 are straight on, ordinary and the turn across.
    /// </remarks>
    [Theory]
    [InlineData((byte)4)]
    [InlineData((byte)5)]
    [InlineData((byte)6)]
    public void AClaimBelowARescueDoesNotBindIt(byte theirs)
    {
        Assert.False(LaneOccupancy.Binds(Claim((ClaimPriority)theirs), ClaimPriority.Special));
        Assert.False(LaneOccupancy.Binds(Claim(ClaimPriority.Closed), ClaimPriority.Special));
        Assert.False(LaneOccupancy.Binds(Claim(ClaimPriority.Reserved), ClaimPriority.Special));
    }

    /// <summary>And the mirror of it: everything below is refused by a rescue's claim, which is what "yield" means here.</summary>
    [Theory]
    [InlineData((byte)4)]
    [InlineData((byte)5)]
    [InlineData((byte)6)]
    public void ARescuesClaimBindsEverythingBelowIt(byte mine)
    {
        Assert.True(LaneOccupancy.Binds(Claim(ClaimPriority.Special), (ClaimPriority)mine));
        Assert.True(LaneOccupancy.Binds(Claim(ClaimPriority.Special), ClaimPriority.Closed));
    }

    /// <summary>
    /// <b>SRV-6, both halves at once</b>: a closed road refuses every ordinary movement, and does not refuse
    /// a vehicle answering a call. It is the whole mechanism of the closure — one rung in one order — so it
    /// is asserted here beside the rescue's rather than in a slice of its own.
    /// </summary>
    [Theory]
    [InlineData((byte)4)]
    [InlineData((byte)5)]
    [InlineData((byte)6)]
    public void AClosedRoadBindsOrdinaryTrafficAndNotACall(byte mine)
    {
        Assert.True(LaneOccupancy.Binds(Claim(ClaimPriority.Closed), (ClaimPriority)mine));
        Assert.False(LaneOccupancy.Binds(Claim(ClaimPriority.Closed), ClaimPriority.Special));
    }

    /// <summary>
    /// And a closure takes a claim and nothing else: a body standing in a closed street, and the road a body
    /// is committed to being able to stop in, are no more an officer's than anybody's (AMB-4a).
    /// </summary>
    [Fact]
    public void AClosureTakesNoBody()
    {
        Assert.True(LaneOccupancy.Binds(Body(onItsLine: true), ClaimPriority.Closed));
        Assert.True(LaneOccupancy.Binds(Claim(ClaimPriority.Hard), ClaimPriority.Closed));
    }

    /// <summary>
    /// <b>A body past the point it could stop short is nobody's to take</b> — the one rung a rescue does
    /// not outrank, because the ladder orders who waits and is not a licence to drive into somebody.
    /// </summary>
    [Fact]
    public void ARescueGivesWayToABodyThatCanNoLongerGiveGroundBack()
    {
        Assert.True(LaneOccupancy.Binds(Claim(ClaimPriority.Hard), ClaimPriority.Special));
    }

    /// <summary>
    /// <b>And it takes a claim and nothing else.</b> Ground a body is standing on, and the road a body is
    /// committed to being able to stop in, refuse a rescue exactly as they refuse anybody.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ARescueIsRefusedByABodyHoweverItWasMeasured(bool onItsLine)
    {
        Assert.True(LaneOccupancy.Binds(Body(onItsLine), ClaimPriority.Special));
    }

    /// <summary>
    /// The ladder is an order and the order runs one way: a byte comparison is the whole mechanism, so the
    /// one thing that could break it silently is somebody inserting a rung in the wrong place.
    /// </summary>
    [Fact]
    public void ARescueStandsBetweenTheTrafficAndACommittedBody()
    {
        Assert.True(ClaimPriority.Special < ClaimPriority.FirmStraight);
        Assert.True(ClaimPriority.Special > ClaimPriority.Hard);
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

    /// <summary>
    /// <b>A rescue's stated road is a rescue's road</b> (AMB-4, TER-5g): the stated band mirrors the granted
    /// one, so a call that has said where it is going refuses everything an ordinary movement could have
    /// stated and is still taken by a body.
    /// </summary>
    [Theory]
    [InlineData((byte)4)]
    [InlineData((byte)5)]
    [InlineData((byte)6)]
    public void ARescuesStatedRoadBindsOrdinaryTrafficAndNoBody(byte mine)
    {
        Assert.True(LaneOccupancy.Binds(Claim(ClaimPriority.SoftSpecial), (ClaimPriority)mine));
        Assert.False(LaneOccupancy.Binds(Claim(ClaimPriority.SoftSpecial), ClaimPriority.Special));
        Assert.Equal(ClaimPriority.Special, LaneOccupancy.SaidAhead(ClaimPriority.SoftSpecial));
    }
}
