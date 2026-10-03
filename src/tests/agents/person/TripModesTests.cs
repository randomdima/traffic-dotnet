using System.Numerics;
using TrafficSimulation.Agents.Person.Control;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Person;

/// <summary>
/// PER-29: <b>nobody walks further than a few blocks</b>. A door within a walk of both the person and their car is
/// walked to, any other is driven to where a car is to hand, and refused where none is.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P2)]
public class TripModesTests
{
    const float WalkReachM = 100f;

    static readonly Vector2 PersonM = Vector2.Zero;

    [Fact]
    public void ADoorWithinAWalkOfThePersonAndTheirCarIsWalked() =>
        Assert.Equal(TripMode.Walked, ModeOf(new Vector2(80f, 0f), carToHand: true, carM: new Vector2(20f, 0f)));

    [Fact]
    public void ADoorFurtherThanAWalkIsDriven() =>
        Assert.Equal(TripMode.Driven, ModeOf(new Vector2(120f, 0f), carToHand: true, carM: new Vector2(20f, 0f)));

    /// <summary>The car stays within a walk of its owner wherever their walks take them.</summary>
    [Fact]
    public void ADoorWithinAWalkOfThePersonButNotOfTheirCarIsDriven() =>
        Assert.Equal(TripMode.Driven, ModeOf(new Vector2(-80f, 0f), carToHand: true, carM: new Vector2(80f, 0f)));

    [Fact]
    public void WithNoCarToHandADoorWithinAWalkIsWalked() =>
        Assert.Equal(TripMode.Walked, ModeOf(new Vector2(80f, 0f), carToHand: false, carM: PersonM));

    [Fact]
    public void WithNoCarToHandADoorFurtherThanAWalkIsOutOfReach() =>
        Assert.Equal(TripMode.OutOfReach, ModeOf(new Vector2(120f, 0f), carToHand: false, carM: PersonM));

    /// <summary>Somebody at the wheel parks before they walk anywhere, so even the door beside them is driven to.</summary>
    [Fact]
    public void AtTheWheelEvenANearDoorIsDriven() =>
        Assert.Equal(
            TripMode.Driven,
            TripModes.Of(PersonM, new Vector2(10f, 0f), aboard: true, carToHand: false, PersonM, WalkReachM));

    static TripMode ModeOf(Vector2 doorM, bool carToHand, Vector2 carM) =>
        TripModes.Of(PersonM, doorM, aboard: false, carToHand, carM, WalkReachM);
}
