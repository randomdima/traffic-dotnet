using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Ambulance;

/// <summary>
/// AMB-4 as the road states it: <b>a rank orders who waits and never who is driven into</b>. The whole of
/// the ambulance's priority is one value of <see cref="RightOfWay"/>, so this is where the promise that it
/// takes only what a rank may take is asserted rather than assumed.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class RescuePriorityTests
{
    static LaneSlot Claim(RightOfWay right) => new(10f, 20f, 20f, 0f, 7, LaneUse.Claimed, Right: right);

    static LaneSlot Body(LaneUse use, RightOfWay right) => new(10f, 20f, 20f, 0f, 7, use, Right: right);

    /// <summary>
    /// A rescue outranks every ordinary movement, the paint and <b>a closed road</b> (SRV-6), so none of
    /// their claims refuses it — the last of those is the whole of "the officer lets the other services
    /// through".
    /// </summary>
    /// <remarks>The ranks are named by their byte, because a theory's parameters are as public as the test.</remarks>
    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    public void AClaimBelowARescueDoesNotBindIt(byte theirs)
    {
        Assert.False(LaneOccupancy.Binds(Claim((RightOfWay)theirs), RightOfWay.Emergency));
    }

    /// <summary>And the mirror of it: everything below is refused by a rescue's claim, which is what "yield" means here.</summary>
    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    public void ARescuesClaimBindsEverythingBelowIt(byte mine)
    {
        Assert.True(LaneOccupancy.Binds(Claim(RightOfWay.Emergency), (RightOfWay)mine));
    }

    /// <summary>
    /// <b>SRV-6, both halves at once</b>: a closed road refuses every ordinary movement and the paint, and
    /// does not refuse a vehicle answering a call. It is the whole mechanism of the closure — one rank in
    /// one order — so it is asserted here beside the rescue's rather than in a slice of its own.
    /// </summary>
    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    public void AClosedRoadBindsOrdinaryTrafficAndNotACall(byte mine)
    {
        Assert.True(LaneOccupancy.Binds(Claim(RightOfWay.Closed), (RightOfWay)mine));
        Assert.False(LaneOccupancy.Binds(Claim(RightOfWay.Closed), RightOfWay.Emergency));
    }

    /// <summary>
    /// And a closure takes a claim and nothing else: a body standing in a closed street, and the road a body
    /// is committed to being able to stop in, are no more an officer's than anybody's (AMB-4a).
    /// </summary>
    [Fact]
    public void AClosureTakesNoBody()
    {
        Assert.True(LaneOccupancy.Binds(Body(LaneUse.Reserved, RightOfWay.Traffic), RightOfWay.Closed));
        Assert.True(LaneOccupancy.Binds(Claim(RightOfWay.Committed), RightOfWay.Closed));
    }

    /// <summary>
    /// <b>A body past the point it could stop short is nobody's to take</b> — the one rank a rescue does
    /// not outrank, because a right of way is a rule about who waits and not a licence to drive into
    /// somebody.
    /// </summary>
    [Fact]
    public void ARescueGivesWayToABodyThatCanNoLongerGiveGroundBack()
    {
        Assert.True(LaneOccupancy.Binds(Claim(RightOfWay.Committed), RightOfWay.Emergency));
    }

    /// <summary>
    /// <b>And it takes a claim and nothing else.</b> Ground a body is standing on, and the road a body is
    /// committed to being able to stop in, refuse a rescue exactly as they refuse anybody.
    /// </summary>
    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    public void ARescueIsRefusedByABodyWhateverRankThatBodyHolds(byte use)
    {
        Assert.True(LaneOccupancy.Binds(Body((LaneUse)use, RightOfWay.Traffic), RightOfWay.Emergency));
    }

    /// <summary>
    /// The rank is an order and the order runs one way: a byte comparison is the whole mechanism, so the
    /// one thing that could break it silently is somebody inserting a value in the wrong place.
    /// </summary>
    [Fact]
    public void ARescueStandsBetweenThePaintAndACommittedBody()
    {
        Assert.True(RightOfWay.Emergency > RightOfWay.OnThePaint);
        Assert.True(RightOfWay.Emergency < RightOfWay.Committed);
    }

    /// <summary>
    /// <b>And a closed road stands between the paint and a rescue</b> (SRV-6) — the one placing in the order
    /// that gives a closure both of the things it is for, and the one an inserted value could silently move.
    /// </summary>
    [Fact]
    public void AClosedRoadStandsBetweenThePaintAndARescue()
    {
        Assert.True(RightOfWay.Closed > RightOfWay.OnThePaint);
        Assert.True(RightOfWay.Closed < RightOfWay.Emergency);
    }
}
