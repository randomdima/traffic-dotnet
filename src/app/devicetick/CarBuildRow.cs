using System.Runtime.InteropServices;
using TrafficSimulation.Agents.Car.Body;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>One variant as a kernel reads it</b> (<c>agents/car/body/kernels/car_build.glsl</c>), field for field: what its
/// tyres, its driver and its tow bar are worth, and its wheels.
/// </summary>
/// <remarks>
/// Thirty-two floats and two counts before the wheels, so they start on the eight bytes std430 lays a <c>vec2</c> at — so a
/// float added here takes a count's partner with it.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CarBuildRow(
    float WheelbaseM, float CentreAheadOfAxleM, float HalfTrackM, float CgHeightM, float FrontWeightShare, float GripMps2,
    float WheelRotatingMassKg, float AccelerationMps2, float BrakingMps2, float MaxSteerRad,
    float LookaheadFloorM, float LookaheadCeilingM, float ProjectionWindowM, float SteerRateRadPerS, float MassKg,
    float WheelWidthM, float LengthM, float WidthM, float TowHingeAheadOfCentreM, float TowReachM,
    float TowGripFromTheMiddleM, float InverseWheelRotatingMassPerKg, float WheelRateNPerM, float WheelDampingNsPerM,
    float CgAheadOfCentreM, float StandingN, float StandingNPerMAlong, float StandingNPerMAcross, float InverseMassPerKg,
    float InversePitchInertiaPerKgM2, float InverseRollInertiaPerKgM2, float InverseMassCgHeightPerKgM, uint WheelCount,
    uint AxleCount,
    CarWheelTable Wheels, AxleDriveTable AxleDrive)
{
    public static CarBuildRow Of(in CarBuild car) => new(
        car.WheelbaseM, car.CentreAheadOfAxleM, car.HalfTrackM, car.CgHeightM, car.FrontWeightShare, car.GripMps2,
        car.WheelRotatingMassKg, car.AccelerationMps2, car.BrakingMps2, car.MaxSteerRad,
        car.LookaheadFloorM, car.LookaheadCeilingM, car.ProjectionWindowM, car.SteerRateRadPerS, car.MassKg,
        car.WheelWidthM, car.LengthM, car.WidthM, car.TowHingeAheadOfCentreM, car.TowReachM,
        car.TowGripFromTheMiddleM, car.InverseWheelRotatingMassPerKg, car.WheelRateNPerM, car.WheelDampingNsPerM,
        car.CgAheadOfCentreM, car.StandingN, car.StandingNPerMAlong, car.StandingNPerMAcross, car.InverseMassPerKg,
        car.InversePitchInertiaPerKgM2, car.InverseRollInertiaPerKgM2, car.InverseMassCgHeightPerKgM, (uint)car.WheelCount,
        (uint)car.AxleCount,
        car.Wheels, car.AxleDrive);

    /// <summary>Every build as its row, in the order <c>BuildOf</c> names them.</summary>
    public static CarBuildRow[] Of(ReadOnlySpan<CarBuild> builds)
    {
        var rows = new CarBuildRow[builds.Length];
        for (var build = 0; build < rows.Length; build++) rows[build] = Of(builds[build]);
        return rows;
    }
}
