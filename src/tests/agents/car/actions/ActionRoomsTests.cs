using TrafficSimulation.Agents.Car.Actions;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car.Actions;

/// <summary>
/// <b>Room for what only a few cars hold at once</b> (<see cref="ActionRooms"/>): one room a share of the fleet and
/// never fewer than the least, a car keeping the room it took, and a car finding none left refused rather than given
/// somebody else's.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P2)]
public class ActionRoomsTests
{
    [Fact]
    public void RoomsAreAShareOfTheFleetAndNeverFewerThanTheLeast()
    {
        Assert.Equal(44, new ActionRooms(cars: 692, share: 16, least: 16).Count);
        Assert.Equal(16, new ActionRooms(cars: 100, share: 16, least: 16).Count);
        Assert.Equal(5, new ActionRooms(cars: 5, share: 16, least: 16).Count);
    }

    [Fact]
    public void ACarAskingAgainKeepsTheRoomItTook()
    {
        var rooms = new ActionRooms(cars: 4, share: 1, least: 1);
        Assert.True(rooms.Take(2, out var first));
        Assert.True(rooms.Take(2, out var again));

        Assert.Equal(first, again);
        Assert.Equal(first, rooms.RoomOf(2));
    }

    [Fact]
    public void ACarFindingNoRoomLeftIsRefusedAndCountedUntilOneIsGivenBack()
    {
        var rooms = new ActionRooms(cars: 3, share: 3, least: 1);
        Assert.True(rooms.Take(0, out var held));

        Assert.False(rooms.Take(1, out _));
        Assert.Equal(ActionRooms.None, rooms.RoomOf(1));
        Assert.Equal(1, rooms.Refused);

        rooms.Free(0);
        Assert.Equal(ActionRooms.None, rooms.RoomOf(0));
        Assert.True(rooms.Take(1, out var handedOn));
        Assert.Equal(held, handedOn);
    }
}
