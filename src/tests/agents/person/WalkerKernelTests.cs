using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.Runtime;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Person;

/// <summary>
/// <b>The walker's tick on the device</b> (<c>agents/person/control/kernels/walker.glsl</c>): what
/// <see cref="WalkerFollower.Tick"/> makes of a walker — where it faces, what it declares and what that costs — asked of
/// the kernel over every case the host branches on. No town is stood up: a walker's tick reads nothing of the ground
/// (PER-3), only its own body, its aim and its flags.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class WalkerKernelTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static float MostTurnRad => Config.PersonTurnRateDegPerS * MathF.PI / 180f * Config.TickSeconds;

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct WalkerRow(
        Vector2 PositionM, Vector2 VelocityMps, Vector2 DeclaredMps, Vector2 AimM, float HeadingRad, float MassKg, uint Flags,
        float Pad);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct OutcomeRow(Vector2 DesiredMps, Vector2 ImpulseNs, float HeadingRad, uint Writes, Vector2 Pad);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct Figures(
        uint WalkerCount, float DtS, float WalkSpeedMps, float TurnRateDegPerS, float FootGripMps2, float SlidingGripMps2,
        float AimUnderfootM2);

    /// <summary>Where a walker's aim stands against its body, which is every way the follower's own branches go.</summary>
    enum AimKind
    {
        /// <summary>Under the body: arrived, or nowhere to be.</summary>
        Arrived,

        /// <summary>Inside a tick's walk: a grant ending just short of it, or a queue creeping.</summary>
        Shuffle,

        /// <summary>Further than a tick's walk and within a tick's turn of its heading: walked at, on the bearing.</summary>
        Facing,

        /// <summary>Further, and off its heading by more than a tick's turn — a sidestep, a way behind it: turned toward.</summary>
        Turning,
    }

    sealed record Walker(
        AimKind Kind, float HeadingRad, Vector2 PositionM, Vector2 VelocityMps, Vector2 DeclaredMps, Vector2 AimM, bool Walking,
        bool Wounded, bool Inside, bool Paused, float MassKg)
    {
        public uint Flags => (Walking ? 1u : 0u) | (Wounded ? 2u : 0u) | (Inside ? 4u : 0u) | (Paused ? 8u : 0u);

        public bool Tick(out WalkerStep step) => WalkerFollower.Tick(
            Config, HeadingRad, PositionM, VelocityMps, DeclaredMps, AimM, Walking, Wounded, Inside, Paused, MassKg,
            Config.TickSeconds, out step);
    }

    /// <remarks>
    /// <b>Each aim is the kind it is drawn as</b>: a facing one a fifth of a tick's turn inside the edge between facing and
    /// turning, a turning one a fifth past it and a twentieth of a radian short of half a turn round — so each kind is asked
    /// as often as it is drawn.
    /// </remarks>
    static Walker[] Walkers(int count, int seed)
    {
        var random = new Random(seed);
        float Between(float least, float most) => least + (random.NextSingle() * (most - least));
        Vector2 Towards(float headingRad, float lengthM) => new Vector2(MathF.Cos(headingRad), MathF.Sin(headingRad)) * lengthM;

        var hopM = Config.PersonWalkSpeedMps * Config.TickSeconds;
        var footMps = Config.PersonFootGripMps2 * Config.TickSeconds;
        var walkers = new Walker[count];
        for (var at = 0; at < count; at++)
        {
            var kind = (AimKind)random.Next(4);

            // Most headings as the town keeps them, and some a turn or so either way, which Wrap brings round.
            var headingRad = random.Next(8) == 0 ? Between(-3f * MathF.PI, 3f * MathF.PI) : Between(-MathF.PI, MathF.PI);
            var positionM = new Vector2(Between(0f, 2000f), Between(0f, 2000f));
            var aimM = kind switch
            {
                AimKind.Arrived => random.Next(2) == 0 ? positionM : positionM + Towards(Between(-MathF.PI, MathF.PI), Between(0f, 5e-5f)),
                AimKind.Shuffle => positionM + Towards(Between(-MathF.PI, MathF.PI), Between(1e-3f, hopM)),
                AimKind.Facing => positionM + Towards(headingRad + Between(-0.8f, 0.8f) * MostTurnRad, Between(hopM * 2f, 40f)),
                _ => positionM + Towards(
                    headingRad + ((random.Next(2) == 0 ? 1f : -1f) * Between(MostTurnRad * 1.2f, MathF.PI - 0.05f)),
                    Between(hopM * 2f, 40f)),
            };

            // Declared nothing, or a walk; and the solver left it as declared, shoved inside its grip, or thrown past it.
            var declaredMps = random.Next(3) == 0 ? Vector2.Zero : Towards(Between(-MathF.PI, MathF.PI), Config.PersonWalkSpeedMps);
            var velocityMps = random.Next(4) switch
            {
                0 => Vector2.Zero,
                1 => declaredMps,
                2 => declaredMps + Towards(Between(-MathF.PI, MathF.PI), Between(0f, footMps * 0.9f)),
                _ => declaredMps + Towards(Between(-MathF.PI, MathF.PI), Between(footMps * 2f, 12f)),
            };

            walkers[at] = new Walker(
                kind, headingRad, positionM, velocityMps, declaredMps, aimM, Walking: random.Next(4) != 0,
                Wounded: random.Next(8) == 0, Inside: random.Next(10) == 0, Paused: random.Next(10) == 0, Between(40f, 120f));
        }

        return walkers;
    }

    OutcomeRow[] OnTheDevice(Walker[] walkers)
    {
        using var rows = device.Holding<WalkerRow>(walkers.Select(walker => new WalkerRow(
            walker.PositionM, walker.VelocityMps, walker.DeclaredMps, walker.AimM, walker.HeadingRad, walker.MassKg, walker.Flags,
            0f)).ToArray());
        using var outcomes = device.Sized<OutcomeRow>(walkers.Length);

        GpuBuffer[] buffers = [rows, outcomes];
        using var kernel = new ComputeKernel(device.Vk, "walker_tick.comp", buffers.Length, (uint)Marshal.SizeOf<Figures>());
        var set = kernel.Bind(buffers);
        using var batch = new ComputeBatch(device.Vk);
        var recording = batch.Begin();
        kernel.Dispatch(recording, set, (uint)((walkers.Length + 63) / 64), new Figures(
            (uint)walkers.Length, Config.TickSeconds, Config.PersonWalkSpeedMps, Config.PersonTurnRateDegPerS,
            Config.PersonFootGripMps2, Config.PersonSlidingGripMps2, Config.Person.AimUnderfootM2));
        batch.SubmitAndWait();

        return outcomes.Span<OutcomeRow>().ToArray();
    }

    /// <summary>
    /// <b>Two thousand walkers declare and spend exactly what the host's do</b> — the velocity each asks for and the
    /// impulse it costs, to the last bit, and nothing written for the same casualties. No transcendental reaches either:
    /// the bearing decides only which way a walker goes, never how fast.
    /// </summary>
    [Fact]
    public void EveryWalkerDeclaresAndSpendsExactlyWhatTheHostDoes()
    {
        var walkers = Walkers(2400, 11);
        var outcomes = OnTheDevice(walkers);

        var differ = new List<string>();
        for (var at = 0; at < walkers.Length; at++)
        {
            var writes = walkers[at].Tick(out var host);
            var onDevice = outcomes[at];
            if (writes != (onDevice.Writes != 0u)
                || (writes && (host.DesiredMps != onDevice.DesiredMps || host.ImpulseNs != onDevice.ImpulseNs)))
            {
                differ.Add($"walker {at} ({walkers[at].Kind}, flags {walkers[at].Flags}): host {writes} {host}, device {onDevice.Writes} declared {onDevice.DesiredMps} impulse {onDevice.ImpulseNs}");
            }
        }

        Assert.True(differ.Count == 0, $"{differ.Count} of {walkers.Length} differ: {string.Join("; ", differ.Take(5))}");
    }

    /// <summary>
    /// <b>Two thousand walkers face where the host's do</b>, to the bit: where one turned at the turn rate, stood, was not
    /// its own to turn, or took the bearing of an aim it was already facing.
    /// </summary>
    [Fact]
    public void EveryWalkerFacesWhereTheHostsDoes()
    {
        var walkers = Walkers(2400, 13);
        var outcomes = OnTheDevice(walkers);

        var differ = new List<string>();
        for (var at = 0; at < walkers.Length; at++)
        {
            var walker = walkers[at];
            if (!walker.Tick(out var host)) continue;

            var onDevice = outcomes[at].HeadingRad;
            if (BitConverter.SingleToInt32Bits(host.HeadingRad) != BitConverter.SingleToInt32Bits(onDevice))
            {
                differ.Add($"walker {at} ({walker.Kind}, flags {walker.Flags}): from {walker.HeadingRad}, host {host.HeadingRad}, device {onDevice}");
            }
        }

        Assert.True(differ.Count == 0, $"{differ.Count} of {walkers.Length} face otherwise: {string.Join("; ", differ.Take(5))}");
    }
}
