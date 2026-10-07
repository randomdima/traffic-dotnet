using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;

namespace TrafficSimulation.World.Town;

/// <summary>Where a car's command becomes four contact patches: the pose they all read, what each one spends, and what it leaves on the ground.</summary>
internal sealed partial class TownWorld
{
    CarPose PoseOf(int car) => new(
        Cars.PositionM[car], Cars.HeadingRad[car], Cars.VelocityMps[car], Cars.YawRateRadPerS[car],
        Cars.MassKg[car], Cars.AccelerationMps2[car]);

    /// <summary>
    /// One car's four patches for this tick (<see cref="TyrePass.Step"/>), off the fleet's own state, and the marks
    /// its wheels laid filed in wheel order.
    /// </summary>
    /// <returns>Whether the car was at rest and the tyres had nothing to do (<see cref="TyrePass.AtRest"/>).</returns>
    bool Tyres(int car, in CarPose pose)
    {
        Span<TyreMark> marks = stackalloc TyreMark[TyreModel.Wheels];
        var atRest = TyrePass.Step(
            _config, _terrain, Cars.BuildOf(car), pose, Cars.Command[car], Cars.Action[car] == CarAction.Hand,
            Cars.GroundCoefficient[car], 1f / _physics.InverseInertiaOf(Cars.Body[car]), Cars.LevelOf(car), Cars.TyresOf(car),
            _wheels.GroundUnder(car), _wheels.ImpulsesOf(car), _wheels.ScrubOf(car), marks, out var laid);

        foreach (ref readonly var mark in marks[..laid])
        {
            Marks.Mark(mark.FromM, mark.ToM, mark.WidthM, mark.Intensity, mark.Ploughed);
        }

        return atRest;
    }

    /// <summary>For the instruments: the impulse each of this car's wheels spends this tick, as its tyres left it.</summary>
    public ReadOnlySpan<WheelImpulse> WheelImpulsesOf(int car) => _wheels.ImpulsesOf(car);
}
