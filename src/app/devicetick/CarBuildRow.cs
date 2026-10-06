using System.Runtime.InteropServices;
using TrafficSimulation.Agents.Car.Body;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>One variant as a kernel reads it</b> (<c>agents/car/body/kernels/car_build.glsl</c>), field for field: what its
/// tyres and its driver are worth.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CarBuildRow(
    float WheelbaseM, float CentreAheadOfAxleM, float HalfTrackM, float CgHeightM, float FrontWeightShare, float GripMps2,
    float WheelRotatingMassKg, float AccelerationMps2, float DrivenFrontShare, float MaxSpeedMps, float ReverseMaxMps,
    float BrakingMps2, float MaxSteerRad, float PedalRateMps3, float LookaheadFloorM, float LookaheadCeilingM,
    float ProjectionWindowM, float SteerRateRadPerS, float TurningRadiusM, float MassKg)
{
    public static CarBuildRow Of(in CarBuild car) => new(
        car.WheelbaseM, car.CentreAheadOfAxleM, car.HalfTrackM, car.CgHeightM, car.FrontWeightShare, car.GripMps2,
        car.WheelRotatingMassKg, car.AccelerationMps2, car.DrivenFrontShare, car.MaxSpeedMps, car.ReverseMaxMps,
        car.BrakingMps2, car.MaxSteerRad, car.PedalRateMps3, car.LookaheadFloorM, car.LookaheadCeilingM,
        car.ProjectionWindowM, car.SteerRateRadPerS, car.TurningRadiusM, car.MassKg);
}
