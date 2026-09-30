using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car.Actions;

/// <summary>
/// <b>A car moves over no ground its action has not claimed</b> (CAR-15b), asked of every car on every tick of the
/// suite's own city while its cars stand, leave their bays, follow their routes and park again.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P1)]
public class CarActionTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// Two minutes: past the first stands the round draws (<see cref="DrivingFigures.ParkedMinS"/>), so the town is
    /// leaving bays and parking in them as well as driving between them.
    /// </summary>
    const int Ticks = 7_200;

    /// <summary>
    /// <b>No car is granted road its action has not claimed</b>: a grant is what survives of a plan down the car's
    /// line, or the whole of the ground of a pass or a manoeuvre it holds as a body — and nothing where it holds
    /// neither, whatever it did the tick before.
    /// </summary>
    [Fact]
    public void NoCarIsGrantedRoadItsActionHasNotClaimed()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var cars = world.Cars;

        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance();
            for (var car = 0; car < cars.Count; car++)
            {
                if (cars.AuthorityM[car] <= 0f) continue;

                var action = cars.Action[car];
                var holdsAPlan = world.DriveHold(car) != LaneOccupancy.NoHold;
                var holdsAPass = action == CarAction.Overtake && cars.Pass[car].Begun;
                var holdsAManoeuvre = action is CarAction.Park or CarAction.Unpark && world.Manoeuvres.IsBegun(car);
                Assert.True(
                    holdsAPlan || holdsAPass || holdsAManoeuvre,
                    $"at tick {tick}, car {car} ({action}) was granted {cars.AuthorityM[car]:F2} m and holds nothing");
            }
        }
    }
}
