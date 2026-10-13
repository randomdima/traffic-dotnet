using System.Numerics;
using TrafficSimulation.Agents.Car.Body;

namespace TrafficSimulation.World.Town;

/// <summary>Where a car's command leaves the town: the pose it was decided on, and the wheel and pedals handed to the world.</summary>
internal sealed partial class TownWorld
{
    /// <summary>A car as its driver reads it: where it is and how it moves.</summary>
    /// <remarks>
    /// <b>Not how it is loaded</b>: the acceleration the tyres weigh their loads by is the world's, which steps them off
    /// a pose of its own, and nothing a driver decides reads it — so this one carries none.
    /// </remarks>
    CarPose PoseOf(int car) => new(
        Cars.PositionM[car], Cars.HeadingRad[car], Cars.Forward[car], Cars.VelocityMps[car], Cars.YawRateRadPerS[car],
        Cars.MassKgOf(car), Vector2.Zero);

    /// <summary>
    /// <b>This car's command delivered</b>: handed to the world, whose tyres turn it into four contact patches
    /// (<c>TyrePass</c>) in its next step.
    /// </summary>
    void Tyres(int car) => SignalTheWheel(car);
}
