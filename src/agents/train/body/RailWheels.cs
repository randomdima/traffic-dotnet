using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Rail;

namespace TrafficSimulation.Agents.Train.Body;

/// <summary>
/// <b>A rail vehicle as its wheels see it</b> (TRN-8): each bogie's pivot ahead of its middle, how many it has, its mass,
/// and what its engine pulls with — none for a wagon. Blittable, as the device keeps it.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct RailBuild(Vector4 BogiesM, int Bogies, float MassKg, float TractiveEffortN, float Unused)
{
    public static RailBuild Of(in TrainVehicle vehicle)
    {
        Span<float> at = stackalloc float[TrainVehicle.MostBogies];
        vehicle.BogiesM.CopyTo(at);
        return new RailBuild(new Vector4(at[0], at[1], at[2], at[3]), vehicle.BogiesM.Length, vehicle.MassKg, vehicle.TractiveEffortN, 0f);
    }

    public float BogieM(int bogie) => bogie switch { 0 => BogiesM.X, 1 => BogiesM.Y, 2 => BogiesM.Z, _ => BogiesM.W };
}

/// <summary>
/// <b>A rail vehicle's wheels</b> (TRN-8, TRN-9): each bogie one large wheel on the body's centre line, turning freely
/// about its pivot and held on its rail — the car's tyre pass said of a wheel that cannot leave its line. Nothing here
/// touches a body: what each wheel spends is written as an impulse at its pivot, which the world spends on the body.
/// </summary>
/// <remarks>
/// <para>
/// <b>Across its rail a wheel holds without limit</b>, as a flange does: it takes out the pivot's speed across the line
/// and pulls it back onto the line over a settle time, priced at what an impulse at that point is worth to the body
/// turning as well as moving — the contact's own arithmetic. <b>The wheels are taken in turn</b>, each against what
/// the ones before it left, over the solver's own passes: all at once, a share of the mass each, they rock the body.
/// </para>
/// <para>
/// <b>Along it a wheel has two states, as a tyre has.</b> Rolling, it loses a rolling resistance and nothing else, and a
/// driven one pulls with its share of what the engine is asked for, up to what steel on steel holds. Braked, it slides,
/// and resists any motion along the rail up to that same friction. A wheel turns no body about its own pivot, since it
/// pushes there.
/// </para>
/// <para>
/// <b>A throttle is an acceleration of the vehicle's own mass</b>, as a car's is of its own: whatever drives the train
/// works out what that is for the train it pulls (TRN-2). Nothing here knows of a train.
/// </para>
/// </remarks>
internal static class RailWheels
{
    /// <summary>
    /// <b>One tick of a vehicle's wheels</b>: each bogie's cursor walked on (<see cref="RailGraph.Follow"/>), then what
    /// each wheel spends across and along its rail, written into <paramref name="into"/> at its pivot.
    /// </summary>
    /// <remarks>
    /// <c>world/town/kernels/world_rail.comp</c> is this on the device, line for line: a change to one is a change to the
    /// other, which <c>DeviceWorldTests</c> holds to the bit.
    /// </remarks>
    public static void Step(
        SimConfig config, float dtS, RailGraph graph, ReadOnlySpan<byte> switches, in RailBuild build, Vector2 positionM,
        Vector2 forward, Vector2 velocityMps, float yawRadPerS, float inverseMass, float inverseInertia, in DriveCommand command,
        Span<RailCursor> cursors, Span<WheelImpulse> into)
    {
        var train = config.Train;
        var bogies = build.Bogies;
        Span<Vector2> armM = stackalloc Vector2[TrainVehicle.MostBogies];
        Span<Vector2> onM = stackalloc Vector2[TrainVehicle.MostBogies];
        Span<Vector2> along = stackalloc Vector2[TrainVehicle.MostBogies];
        Span<Vector2> spentNs = stackalloc Vector2[TrainVehicle.MostBogies];
        for (var bogie = 0; bogie < bogies; bogie++)
        {
            armM[bogie] = forward * build.BogieM(bogie);
            (onM[bogie], var way) = graph.Follow(ref cursors[bogie], positionM + armM[bogie], switches);
            along[bogie] = Vector2.Dot(way, forward) >= 0f ? way : -way;
            spentNs[bogie] = Vector2.Zero;
        }

        var velocity = velocityMps;
        var yaw = yawRadPerS;
        var settle = 1f / train.WheelOntoTheLineS;
        for (var pass = 0; pass < config.Solver.VelocityIterations; pass++)
        {
            for (var bogie = 0; bogie < bogies; bogie++)
            {
                var arm = armM[bogie];
                var across = new Vector2(-along[bogie].Y, along[bogie].X);
                var offM = Vector2.Dot(positionM + arm - onM[bogie], across);
                var acrossMps = Vector2.Dot(velocity + Turning(yaw, arm), across);
                var impulseNs = across * (MassAt(inverseMass, inverseInertia, arm, across) * ((-offM * settle) - acrossMps));
                Spend(ref velocity, ref yaw, inverseMass, inverseInertia, arm, impulseNs);
                spentNs[bogie] += impulseNs;
            }
        }

        var gravity = config.Tyre.StandardGravityMps2;
        var loadN = build.MassKg * gravity / bogies;
        var holdsNs = train.WheelRailFriction * loadN * dtS;
        var braked = command.Handbrake || command.BrakeMps2 > 0f;
        var pullNs = build.TractiveEffortN > 0f && command.ThrottleMps2 > 0f && !braked
            ? MathF.Min(command.ThrottleMps2 * build.MassKg, build.TractiveEffortN) / bogies * dtS * (command.Reverse ? -1f : 1f)
            : 0f;
        for (var bogie = 0; bogie < bogies; bogie++)
        {
            var arm = armM[bogie];
            var way = along[bogie];
            var mass = MassAt(inverseMass, inverseInertia, arm, way);
            var alongMps = Vector2.Dot(velocity + Turning(yaw, arm), way);
            var stopNs = -alongMps * mass;
            float impulseNs;
            if (braked)
            {
                impulseNs = Math.Clamp(stopNs, -holdsNs, holdsNs);
            }
            else
            {
                var rollingNs = MathF.Min(train.RollingResistance * loadN * dtS, MathF.Abs(stopNs));
                impulseNs = Math.Clamp(pullNs, -holdsNs, holdsNs) - (alongMps >= 0f ? rollingNs : -rollingNs);
            }

            var spent = way * impulseNs;
            Spend(ref velocity, ref yaw, inverseMass, inverseInertia, arm, spent);
            spentNs[bogie] += spent;
        }

        for (var bogie = 0; bogie < bogies; bogie++) into[bogie] = new WheelImpulse(spentNs[bogie], positionM + armM[bogie]);
    }

    static Vector2 Turning(float yawRadPerS, Vector2 armM) => new(-yawRadPerS * armM.Y, yawRadPerS * armM.X);

    static float Cross(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);

    /// <summary>What an impulse at a point, in a direction, is worth to a body moving and turning: the contact's effective mass.</summary>
    static float MassAt(float inverseMass, float inverseInertia, Vector2 armM, Vector2 direction)
    {
        var turn = Cross(armM, direction);
        var resistance = inverseMass + (inverseInertia * turn * turn);
        return resistance > 0f ? 1f / resistance : 0f;
    }

    static void Spend(ref Vector2 velocityMps, ref float yawRadPerS, float inverseMass, float inverseInertia, Vector2 armM, Vector2 impulseNs)
    {
        velocityMps += impulseNs * inverseMass;
        yawRadPerS += inverseInertia * Cross(armM, impulseNs);
    }
}
