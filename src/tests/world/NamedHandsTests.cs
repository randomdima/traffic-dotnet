using System.Numerics;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>CTL-5d: <b>the seats</b> — each holds one car by its number, and a car is one seat's at a time.</summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P7)]
public class NamedHandsTests
{
    static readonly HandInput Throttle = new(Held: true, Throttle: 1f, Steer: 0f, Handbrake: false, WalkDirection: Vector2.Zero);

    [Fact]
    public void ACarAnotherSeatHoldsIsNotTaken()
    {
        var seats = new NamedHands(seats: 2, cars: 4);

        seats.Hold(0, 3, Throttle);
        seats.Hold(1, 3, Throttle);

        Assert.Equal(0, seats.SeatOf(3));
        Assert.Equal(-1, seats.CarOf(1));
    }

    [Fact]
    public void ASeatNamingAnotherCarLetsGoOfTheFirst()
    {
        var seats = new NamedHands(seats: 1, cars: 4);

        seats.Hold(0, 1, Throttle);
        seats.Hold(0, 2, Throttle);

        Assert.False(seats.Holds(1));
        Assert.True(seats.Holds(2));
    }

    [Fact]
    public void NamingNoCarLetsGo()
    {
        var seats = new NamedHands(seats: 1, cars: 4);

        seats.Hold(0, 1, Throttle);
        seats.Hold(0, -1, Throttle);

        Assert.Equal(-1, seats.SeatOf(1));
        Assert.Equal(default, seats.On(1));
        Assert.False(seats.AnyHeld);
    }
}
