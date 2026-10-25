using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>Bodies on other than two axles of two (CAR-57), laid out the way a variant's file lays its own wheels.</summary>
internal static class Layouts
{
    /// <summary>
    /// <b>Three axles of two</b> on the plain look: a steered front axle, a driven, handbraked middle one the car turns
    /// about, and a driven, handbraked rear one steered the other way — every kind of wheel the model knows.
    /// </summary>
    public static CarBuild SixWheels(SimConfig config)
    {
        var plain = CarCatalog.Shared.Variants[CarCatalog.Shared.Plain];
        const uint Rear = CarWheel.Driven | CarWheel.Handbrake;
        return CarBuild.Of(config, plain with
        {
            FrontAxleM = 1.3f,
            RearAxleM = -0.6f,
            Wheels =
            [
                new CarWheel(new Vector2(1.3f, 0.75f), 0, CarWheel.Steered),
                new CarWheel(new Vector2(1.3f, -0.75f), 0, CarWheel.Steered),
                new CarWheel(new Vector2(-0.6f, 0.75f), 1, Rear),
                new CarWheel(new Vector2(-0.6f, -0.75f), 1, Rear),
                new CarWheel(new Vector2(-1.5f, 0.75f), 2, Rear | CarWheel.Steered),
                new CarWheel(new Vector2(-1.5f, -0.75f), 2, Rear | CarWheel.Steered),
            ],
        });
    }
}
