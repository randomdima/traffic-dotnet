using System.Numerics;
using TrafficSimulation.Agents.Evacuator;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Evacuator;

/// <summary>
/// <b>EVA-5's couplings and its catch, judged without a town</b>: one fork to an end and two ends to a body, a chain
/// read to its front, and what an arm swung out from its hinge takes hold of.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P2)]
public class TowHitchesTests
{
    /// <summary>EVA-5: <b>a body held at its nose can be taken by its tail by a second arm</b>, and each arm lets go of its own end.</summary>
    [Fact]
    public void TwoArmsHoldOneBodyByItsTwoEndsAndEachLetsGoOfItsOwn()
    {
        var hitches = new TowHitches(cars: 3, wagons: 0);

        hitches.Hitch(0, 2, byTheTail: false, forkAlongM: 1f);
        hitches.Hitch(1, 2, byTheTail: true, forkAlongM: -1f);
        Assert.Equal((0, 1), (hitches.HeldBy(2, byTheTail: false), hitches.HeldBy(2, byTheTail: true)));

        Assert.Equal(2, hitches.Unhitch(0));
        Assert.Equal((TowHitches.Nothing, 1), (hitches.HeldBy(2, byTheTail: false), hitches.HeldBy(2, byTheTail: true)));
        Assert.True(hitches.IsHeld(2));
    }

    /// <summary>EVA-5: <b>a chain is laid under whoever pulls the whole of it</b> — a trailer's tractor's tractor.</summary>
    [Fact]
    public void AChainIsReadToTheTractorNothingHolds()
    {
        var hitches = new TowHitches(cars: 3, wagons: 0);
        hitches.Hitch(0, 1, byTheTail: false, forkAlongM: 0f);
        hitches.Hitch(1, 2, byTheTail: false, forkAlongM: 0f);

        Assert.Equal(0, hitches.FrontOfTheChain(2));
    }

    /// <summary>EVA-5: <b>a loop of arms has no front</b>, and reading one ends rather than going round it for ever.</summary>
    [Fact]
    public void ALoopOfArmsIsReadToAnEnd()
    {
        var hitches = new TowHitches(cars: 2, wagons: 0);
        hitches.Hitch(0, 1, byTheTail: false, forkAlongM: 0f);
        hitches.Hitch(1, 0, byTheTail: false, forkAlongM: 0f);

        Assert.InRange(hitches.FrontOfTheChain(0), 0, 1);
    }

    /// <summary>EVA-5: <b>a rail vehicle is a body an arm holds like any other</b>, numbered past the cars, and never a car on the bar.</summary>
    [Fact]
    public void ARailVehicleIsHeldPastTheCars()
    {
        var hitches = new TowHitches(cars: 2, wagons: 3);
        var wagon = hitches.OfWagon(1);

        hitches.Hitch(0, wagon, byTheTail: true, forkAlongM: -9f);

        Assert.Equal((3, 0, TowHitches.Nothing), (wagon, hitches.HeldBy(wagon, byTheTail: true), hitches.CarOnTheBar(0)));
    }

    /// <summary>EVA-5: <b>the arm catches the nearer end of what is behind it</b>, the nose of a body pointing at the truck.</summary>
    [Fact]
    public void TheArmCatchesTheNearerEnd()
    {
        var reach = ArmBehindTheOrigin();

        reach.Consider(7, BodyAtM, Vector2.UnitX, HalfSizeM, noseHeld: false, tailHeld: false);

        Assert.Equal((7, false), (reach.Caught, reach.ByTheTail));
    }

    /// <summary>EVA-5: <b>an end another arm holds is passed over, and the body's other end is caught</b> where the arm reaches it.</summary>
    [Fact]
    public void AHeldEndIsPassedOverForTheOtherEnd()
    {
        var reach = ArmBehindTheOrigin();

        reach.Consider(7, BodyAtM, Vector2.UnitX, HalfSizeM, noseHeld: true, tailHeld: false);

        Assert.Equal((7, true), (reach.Caught, reach.ByTheTail));
    }

    /// <summary>EVA-5: <b>a body with both ends held is not caught at all</b>, however near the arm it stands.</summary>
    [Fact]
    public void ABodyHeldAtBothEndsIsNotCaught()
    {
        var reach = ArmBehindTheOrigin();

        reach.Consider(7, BodyAtM, Vector2.UnitX, HalfSizeM, noseHeld: true, tailHeld: true);

        Assert.Equal(TowHitches.Nothing, reach.Caught);
    }

    /// <summary>EVA-5: <b>nothing in front of the hinge is caught</b>: the cab is in the way.</summary>
    [Fact]
    public void NothingAheadOfTheHingeIsCaught()
    {
        var reach = ArmBehindTheOrigin();

        reach.Consider(7, -BodyAtM, Vector2.UnitX, HalfSizeM, noseHeld: false, tailHeld: false);

        Assert.Equal(TowHitches.Nothing, reach.Caught);
    }

    /// <summary>A truck facing +x with its hinge at the origin, reaching <see cref="ReachM"/>.</summary>
    static ArmReach ArmBehindTheOrigin() => new(Vector2.Zero, Vector2.UnitX, ReachM);

    const float ReachM = 7f;

    /// <summary>A 4 m by 2 m body facing the truck, its nose 1 m behind the hinge and its tail 5 m — both inside the reach.</summary>
    static readonly Vector2 BodyAtM = new(-3f, 0f);

    static readonly Vector2 HalfSizeM = new(2f, 1f);
}
