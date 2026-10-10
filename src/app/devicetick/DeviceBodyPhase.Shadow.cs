using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// The bodies' phase in shadow: the town as its step finds it told to the device, and what the device's step left that
/// the town's did not.
/// </summary>
internal sealed partial class DeviceBodyPhase
{
    /// <summary>
    /// <b>The edge of rest</b>: a speed or a turn below 2⁻⁹⁶ the device may leave otherwise than the host, since a body
    /// decaying to rest passes through terms a kernel flushes as subnormal and the host keeps (lavapipe preserves none, so
    /// no kernel asks it to). Every other figure of a body is the host's to the bit, in contact order.
    /// </summary>
    public static readonly float EdgeOfRest = MathF.ScaleB(1f, -96);

    /// <summary>The same speed or turn as the host's, or both at the <see cref="EdgeOfRest"/>.</summary>
    public static bool SameMotion(float host, float onDevice) =>
        host == onDevice || (MathF.Abs(host) < EdgeOfRest && MathF.Abs(onDevice) < EdgeOfRest);

    /// <inheritdoc cref="SameMotion(float, float)"/>
    public static bool SameMotion(Vector2 host, Vector2 onDevice) => SameMotion(host.X, onDevice.X) && SameMotion(host.Y, onDevice.Y);

    /// <summary>
    /// <b>The town as its bodies' phase finds it</b>, laid to go with the next <see cref="Step"/>, handed over where the
    /// tick's impulses are written and none is spent (<see cref="TownWorld.BeforeTheImpulses"/>): the solver's table, every
    /// car's body and whether it rested parked, its pose going in, every walker's impulse (but where the walkers are the
    /// device's own, <see cref="TakeTheWalkers"/>) and every tow bar's — and the wheels: every car's where
    /// <paramref name="everyWheel"/>, otherwise only those the tyre pass does not step.
    /// </summary>
    public void Lay(TownWorld town, float dtS, bool everyWheel)
    {
        _solver.Lay(town.PhysicsForInstruments.Table);

        var cars = town.Cars;
        for (var car = 0; car < cars.Count; car++)
        {
            LayCar(car, cars.Body[car].Index, town.RestsParked(car));
            _tyres.Poses[car] = DeviceTyres.PoseRow.Of(new CarPose(
                cars.PositionM[car], cars.HeadingRad[car], cars.VelocityMps[car], cars.YawRateRadPerS[car], cars.MassKg[car],
                cars.AccelerationMps2[car]));
            if (everyWheel || town.OnTheHook(car)) LayWheels(car, town.WheelImpulsesOf(car));
        }

        _tyres.Table.Poses.Mark(0, cars.Count);

        var people = town.People;
        for (var person = 0; _walkers == null && person < people.Count; person++)
        {
            var impulseNs = town.WalkerImpulseNs(person);
            if (people.Inside[person].Any || impulseNs == Vector2.Zero) continue;

            LayCentralImpulse(people.Body[person].Index, impulseNs);
        }

        if (!town.AnyOnTheBar) return;

        for (var car = 0; car < cars.Count; car++)
        {
            if (!town.TheBarPulls(car, dtS, out var bar)) continue;

            LayImpulse(bar.Towed.Index, bar.PullNs, bar.EyeM);
            LayImpulse(bar.Tractor.Index, -bar.PullNs, bar.HookM);
        }
    }

    /// <summary>
    /// <b>What the device's impulses left on a body that the town's did not</b>, asked where the town's step is about to
    /// begin (<see cref="TownWorld.BeforeTheSolverSteps"/>) — or null where the two agree to the bit, as the same
    /// products and sums in the same order do, but at rest's edge (<see cref="Spent"/>).
    /// </summary>
    public string? Applied(in BodyTable table, int body)
    {
        var onDevice = MotionApplied[body];
        var velocityMps = table.VelocityMps[body];
        return Spent(velocityMps.X, onDevice.X) && Spent(velocityMps.Y, onDevice.Y) && Spent(table.YawRateRadPerS[body], onDevice.Z)
            ? null
            : $"body {body}: town {velocityMps} {table.YawRateRadPerS[body]}, device {onDevice}";
    }

    /// <summary>
    /// The same figure to the bit, <b>or both within the impulses' flushed terms of nought</b>: a kernel's float flushes
    /// whatever falls below the least normal float, and a body coming to rest decays through such figures for a score of
    /// ticks — a yaw rate of 1e-39 rad/s, or four wheels' turns of that size the host sums to cancel a yaw the device
    /// keeps. Each of a body's impulses is a term, so the two can stand that many least normal floats apart.
    /// </summary>
    static bool Spent(float host, float onDevice) =>
        host == onDevice || (MathF.Abs(host) <= FlushedTermsMost && MathF.Abs(onDevice) <= FlushedTermsMost);

    /// <summary>What one body's impulses can drop by flushing: its wheels' and the one the host lays, each below the least normal float.</summary>
    static readonly float FlushedTermsMost = (WheelsACar + 1) * BitConverter.Int32BitsToSingle(0x0080_0000);

    /// <summary>
    /// <b>Where the device's step left a car that the town's did not</b>, asked once both have stepped — or null where
    /// they agree: every figure to the bit but its motion at the <see cref="EdgeOfRest"/>.
    /// </summary>
    public string? Disagreement(TownWorld town, int car)
    {
        var cars = town.Cars;
        var row = PosesAfter[car];
        var forward = town.PhysicsForInstruments.RotationOf(cars.Body[car]);
        var same = cars.PositionM[car] == row.PositionM && SameMotion(cars.VelocityMps[car], row.VelocityMps)
                   && SameMotion(cars.YawRateRadPerS[car], row.YawRateRadPerS) && cars.HeadingRad[car] == row.HeadingRad
                   && forward == row.Forward && cars.AccelerationMps2[car] == row.AccelerationMps2 && cars.MassKg[car] == row.MassKg;
        return same
            ? null
            : $"town at {cars.PositionM[car]} moving {cars.VelocityMps[car]} heading {cars.HeadingRad[car]} turning "
              + $"{cars.YawRateRadPerS[car]} forward {forward} accelerating {cars.AccelerationMps2[car]}; device {row}";
    }
}
