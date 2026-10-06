using System.Runtime.InteropServices;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.Agents.TrafficLight;

/// <summary>
/// <b>The lights on the device</b> (<c>agents/trafficlight/control/kernels/signal.glsl</c>): what
/// <see cref="SignalHolds.ColourOf"/> shows a stretch, asked of the kernel over the fixture town's own approaches and
/// crossings, lit and unlit, across whole cycles and at every edge of one.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class SignalKernelTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct StretchRow(int Subject, uint ForCars, float TimeS, float Pad);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct Figures(uint StretchCount, uint LaneCount, uint CrossingCount, float CycleS, float AmberTailS);

    /// <summary>The fixture town's lights, as the town lays them (TLT-3) under <paramref name="config"/>'s cycle.</summary>
    static SignalService Lights(SimConfig config)
    {
        var plan = Towns.Of(Towns.Fixture);
        var roads = RoadGraph.Build(plan, Config);
        var zebras = Crossings.Lay(plan, Config, plan.Paving(Config).RoadEnds(Config).CrossedM);
        return SignalService.Build(plan, roads, zebras, config);
    }

    /// <summary>
    /// Every approach and every crossing the town has, and one either side of each table, each asked at moments drawn
    /// over four of the shipped cycles, at every edge of a phase and of its amber with a float either side, and an hour in.
    /// </summary>
    static StretchRow[] Stretches(SignalService signals, int seed)
    {
        var random = new Random(seed);
        var table = signals.Table;
        var cycleS = Config.Signals.CycleS;
        var phaseS = cycleS / SignalCycle.Axes;
        var rows = new List<StretchRow>();

        void AskOf(int subject, bool forCars, float offsetS)
        {
            for (var draw = 0; draw < 6; draw++) rows.Add(new StretchRow(subject, forCars ? 1u : 0u, random.NextSingle() * 4f * cycleS, 0f));

            for (var phase = 0; phase < 2 * SignalCycle.Axes; phase++)
            {
                foreach (var edgeS in (ReadOnlySpan<float>)[phase * phaseS, ((phase + 1) * phaseS) - Config.Signals.AmberTailS])
                {
                    var atS = edgeS - offsetS;
                    foreach (var timeS in (ReadOnlySpan<float>)[MathF.BitDecrement(atS), atS, MathF.BitIncrement(atS)])
                    {
                        rows.Add(new StretchRow(subject, forCars ? 1u : 0u, timeS, 0f));
                    }
                }
            }

            rows.Add(new StretchRow(subject, forCars ? 1u : 0u, 3600f + (random.NextSingle() * cycleS), 0f));
        }

        for (var lane = -1; lane <= table.LaneAxis.Length; lane++)
        {
            var lit = signals.AxisOfLane(lane) != SignalService.NoAxis;
            AskOf(lane, forCars: true, lit ? table.OffsetS[table.LaneJunction[lane]] : 0f);
        }

        for (var crossing = -1; crossing <= table.CrossingAxis.Length; crossing++)
        {
            AskOf(crossing, forCars: false, signals.CrossingIsLit(crossing) ? table.OffsetS[table.CrossingJunction[crossing]] : 0f);
        }

        return [.. rows];
    }

    uint[] OnTheDevice(SignalService signals, SimConfig config, StretchRow[] stretches)
    {
        var table = signals.Table;
        using var laneAxis = device.Holding<int>(table.LaneAxis);
        using var laneJunction = device.Holding<int>(table.LaneJunction);
        using var offsets = device.Holding<float>(table.OffsetS);
        using var crossingAxis = device.Holding<int>(table.CrossingAxis);
        using var crossingJunction = device.Holding<int>(table.CrossingJunction);
        using var rows = device.Holding<StretchRow>(stretches);
        using var colours = device.Sized<uint>(stretches.Length);

        GpuBuffer[] buffers = [laneAxis, laneJunction, offsets, crossingAxis, crossingJunction, rows, colours];
        using var kernel = new ComputeKernel(device.Vk, "signal_stretches.comp", buffers.Length, (uint)Marshal.SizeOf<Figures>());
        var set = kernel.Bind(buffers);
        using var batch = new ComputeBatch(device.Vk);
        var recording = batch.Begin();
        kernel.Dispatch(recording, set, (uint)((stretches.Length + 63) / 64), new Figures(
            (uint)stretches.Length, (uint)table.LaneAxis.Length, (uint)table.CrossingAxis.Length, config.Signals.CycleS,
            config.Signals.AmberTailS));
        batch.SubmitAndWait();

        return colours.Span<uint>().ToArray();
    }

    static List<string> Differences(SignalService signals, StretchRow[] stretches, uint[] colours)
    {
        var differ = new List<string>();
        for (var at = 0; at < stretches.Length; at++)
        {
            var stretch = stretches[at];
            var host = SignalHolds.ColourOf(signals, stretch.Subject, stretch.ForCars != 0u, stretch.TimeS);
            if ((uint)host != colours[at])
            {
                differ.Add($"{(stretch.ForCars != 0u ? "lane" : "crossing")} {stretch.Subject} at {stretch.TimeS} s: host {host}, device {(SignalColour)colours[at]}");
            }
        }

        return differ;
    }

    /// <summary>
    /// <b>Every light shows a stretch exactly what the host's table shows it</b>, at every moment asked and on both sides
    /// of every edge: nothing in a light is the device's own trigonometry, so there is nothing to forgive.
    /// </summary>
    [Fact]
    public void EveryStretchIsShownWhatTheHostShowsIt()
    {
        var signals = Lights(Config);
        var stretches = Stretches(signals, 17);

        var differ = Differences(signals, stretches, OnTheDevice(signals, Config, stretches));

        Assert.True(differ.Count == 0, $"{differ.Count} of {stretches.Length} differ: {string.Join("; ", differ.Take(6))}");
    }

    /// <summary><b>A town with no cycle shows a stretch what the host's shows it</b>: the lights are out, and so is the paint's green.</summary>
    [Fact]
    public void WithNoCycleEveryStretchIsShownWhatTheHostShowsIt()
    {
        var unlit = new SimConfig { Signals = new SignalFigures { CycleS = 0f } };
        var signals = Lights(unlit);
        var stretches = Stretches(signals, 19);

        var differ = Differences(signals, stretches, OnTheDevice(signals, unlit, stretches));

        Assert.True(differ.Count == 0, $"{differ.Count} of {stretches.Length} differ: {string.Join("; ", differ.Take(6))}");
    }
}
