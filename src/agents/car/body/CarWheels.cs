using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TrafficSimulation.Agents.Car.Body;

/// <summary>
/// <b>One wheel of a body</b>: where it stands in the body's own frame — <c>+x</c> the nose, <c>+y</c> the driver's right —
/// the axle it turns on, and what it does: steers, is held by the handbrake, is driven.
/// </summary>
/// <remarks>Field for field with <c>CarWheelRow</c> (<c>kernels/car_build.glsl</c>).</remarks>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CarWheel(Vector2 AtM, uint Axle, uint Flags)
{
    public const uint Steered = 1, Handbrake = 2, Driven = 4;

    public bool IsSteered => (Flags & Steered) != 0;

    public bool IsHandbraked => (Flags & Handbrake) != 0;

    public bool IsDriven => (Flags & Driven) != 0;
}

/// <summary>A body's wheels, <see cref="TyreModel.MostWheels"/> of room and <see cref="CarBuild.WheelCount"/> of them used.</summary>
/// <remarks>Equal by its rows, which an inline array is not of itself — and a build is a record compared whole.</remarks>
[InlineArray(TyreModel.MostWheels)]
internal struct CarWheelTable : IEquatable<CarWheelTable>
{
    CarWheel _first;

    public readonly bool Equals(CarWheelTable other) => ((ReadOnlySpan<CarWheel>)this).SequenceEqual(other);

    public override readonly bool Equals(object? obj) => obj is CarWheelTable other && Equals(other);

    public override readonly int GetHashCode() => this[0].GetHashCode();
}

/// <summary>What each axle is handed of the engine's drive and its hold, by the axle's index; the shares sum to one.</summary>
/// <remarks><inheritdoc cref="CarWheelTable" path="/remarks"/></remarks>
[InlineArray(TyreModel.MostWheels)]
internal struct AxleDriveTable : IEquatable<AxleDriveTable>
{
    float _first;

    public readonly bool Equals(AxleDriveTable other) => ((ReadOnlySpan<float>)this).SequenceEqual(other);

    public override readonly bool Equals(object? obj) => obj is AxleDriveTable other && Equals(other);

    public override readonly int GetHashCode() => this[0].GetHashCode();
}

/// <summary>The layouts a body's wheels are laid out by.</summary>
internal static class CarWheels
{
    /// <summary>
    /// <b>Two axles of two wheels</b>, a track wide: front right, front left, rear right, rear left — the front pair on
    /// axle 0 and steered, the rear on axle 1 and the handbrake's. Which are driven is the axles' shares.
    /// </summary>
    public static int TwoAxles(
        float wheelbaseM, float centreAheadOfAxleM, float halfTrackM, float drivenFrontShare, Span<CarWheel> into)
    {
        var frontM = wheelbaseM - centreAheadOfAxleM;
        var front = CarWheel.Steered | (drivenFrontShare > 0f ? CarWheel.Driven : 0u);
        var rear = CarWheel.Handbrake | (drivenFrontShare < 1f ? CarWheel.Driven : 0u);
        into[0] = new CarWheel(new Vector2(frontM, halfTrackM), 0, front);
        into[1] = new CarWheel(new Vector2(frontM, -halfTrackM), 0, front);
        into[2] = new CarWheel(new Vector2(-centreAheadOfAxleM, halfTrackM), 1, rear);
        into[3] = new CarWheel(new Vector2(-centreAheadOfAxleM, -halfTrackM), 1, rear);
        return 4;
    }

    /// <summary>How many axles the wheels turn on: one past the highest a wheel names.</summary>
    public static int AxlesOf(ReadOnlySpan<CarWheel> wheels)
    {
        var axles = 0;
        foreach (var wheel in wheels) axles = Math.Max(axles, (int)wheel.Axle + 1);
        return axles;
    }
}
