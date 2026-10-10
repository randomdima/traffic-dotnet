using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>A following car's drive context set on the device</b> (<c>world/town/kernels/drive_context.glsl</c>), asked the
/// town's own question: every tick of the suite's city, each car that followed its line handed over as the tick left it,
/// and what it was told held to what the town told it.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class DriveContextKernelTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Ten seconds of the suite's city: queues, junctions, lights, and cars sent to places.</summary>
    const int Ticks = 600;

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct Figures(uint CarCount, uint ArcsPerCar, uint MostLanes, uint FirstConnector, float SlowerToPassMps, float QueueInCarLengths);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct CarLineRow(uint ArcCount, uint LaneCount, float LengthM, uint FinalApproach);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct ContextCarRow(
        float ProgressM, float CoveredSinceClaimM, float AuthorityM, float HorizonM, float ClaimFromM, float CommittedToM,
        float GrantMarginM, float PlannedMps, uint GrantCutBy, uint Flags, int CutOn, float Pad0, Vector2 PlaceM,
        float PlaceReachM, float Pad, Vector2 PositionM, float Pad1, float Pad2);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct ContextRow(
        float AuthorityM, uint GrantCutBy, float PlaceStopM, float MarginM, uint WaitsToPass, float HorizonM, float PassAsideM,
        float BackUpM, uint Blocked, float WaitAtM, float Pad0, float Pad1);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct BoxRow(float ToTheBoxM, float LightAheadM, uint Flags, uint Pad);

    /// <summary>
    /// <b>Every car that followed its line is told what the town told it</b> — its grant, what cut it, its margin, the
    /// end of its plan, whether it waits to pass, the junction and light ahead, and where the place it was sent to falls on
    /// its line — exactly.
    /// </summary>
    /// <remarks>
    /// Ten seconds of the suite's city have 36 000 such contexts, inside boxes, behind lights and short of claimed ones —
    /// but no car waiting to pass and none sent to a place, so those two branches are not asked here.
    /// </remarks>
    [Fact]
    public void EveryFollowingCarIsToldWhatTheTownToldIt()
    {
        using var world = new TownWorld(Towns.Of(Towns.City), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var cars = world.Cars;
        var roads = world.Roads;
        var capacity = cars.Capacity;
        var arcsPerCar = cars.LineArcsOf(0).Length;
        var lanes = LineAssembler.MostLanes;
        var claims = world.Occupancy;

        var fleet = CarBuilds.OfTheFleet(Config, CarCatalog.Shared);
        using var builds = device.Holding<CarBuildRow>(
            Enumerable.Range(0, CarCatalog.Shared.SheetCount).Select(variant => CarBuildRow.Of(fleet.Of(variant))).ToArray());
        using var buildOf = device.Sized<uint>(capacity);
        using var lines = device.Sized<CarLineRow>(capacity);
        using var lineArcs = device.Sized<ArcRow>(capacity * arcsPerCar);
        using var chain = device.Sized<int>(capacity * lanes);
        using var starts = device.Sized<float>(capacity * lanes);
        using var ends = device.Sized<float>(capacity * lanes);
        using var laneLength = device.Holding<float>(roads.LaneLengthM);
        var connectorAt = new uint[roads.LaneCount + 1];
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            var run = roads.ConnectorsFrom(lane);
            connectorAt[lane] = (uint)run.FirstId;
            connectorAt[lane + 1] = (uint)(run.FirstId + run.Count);
        }

        using var connectorAtBuffer = device.Holding<uint>(connectorAt);
        using var connectorTo = device.Holding<int>(Enumerable.Range(0, roads.ConnectorCount).Select(roads.ConnectorTo).ToArray());
        using var connectorRung = device.Holding<uint>(Enumerable.Range(0, roads.ConnectorCount).Select(c => (uint)roads.FirmOnConnector(c)).ToArray());
        using var connectorLength = device.Holding<float>(Enumerable.Range(0, roads.ConnectorCount).Select(roads.ConnectorLengthM).ToArray());
        using var lanePassable = device.Holding<uint>(Enumerable.Range(0, roads.LaneCount)
            .Select(lane => roads.LaneReverse[lane] >= 0 && !roads.LaneOverOneLine[lane] && roads.LaneCrossesToPass[lane] ? 1u : 0u).ToArray());
        using var rows = device.Sized<DeviceClaims.ClaimRow>(claims.Capacity);
        using var plannedRuns = device.Sized<UInt2>(claims.WayCount);
        using var plannedOrder = device.Sized<uint>(claims.Capacity);
        using var carRows = device.Sized<ContextCarRow>(capacity);
        using var cutters = device.Sized<DeviceClaims.ClaimRow>(capacity);
        using var contexts = device.Sized<ContextRow>(capacity);
        using var boxes = device.Sized<BoxRow>(capacity);
        GpuBuffer[] buffers =
        [
            builds, buildOf, lines, lineArcs, chain, starts, ends, laneLength, connectorAtBuffer, connectorTo, connectorRung,
            connectorLength, lanePassable, rows, plannedRuns, plannedOrder, carRows, cutters, contexts, boxes,
        ];
        using var kernel = new ComputeKernel(device.Vk, "drive_context.comp", buffers.Length, (uint)Marshal.SizeOf<Figures>());
        var set = kernel.Bind(buffers);
        using var batch = new ComputeBatch(device.Vk);

        var plannedBefore = new float[capacity];
        var followed = new bool[capacity];
        var listed = new int[claims.Capacity];
        var differ = new List<string>();
        var asked = 0;
        loop.WatchingTheAgents = (town, stage) =>
        {
            if (stage == AgentsStage.Due)
            {
                for (var car = 0; car < cars.Count; car++)
                {
                    plannedBefore[car] = cars.PlannedMps[car];
                    followed[car] = cars.Driven[car] && cars.Action[car] == CarAction.Follow && !cars.Pass[car].Any;
                }

                return;
            }

            HostClaims.Lay(town.Occupancy, rows.Span<DeviceClaims.ClaimRow>(), plannedRuns.Span<UInt2>(), plannedOrder.Span<uint>(), listed);
            for (var car = 0; car < cars.Count; car++)
            {
                buildOf.Span<uint>()[car] = cars.Variant[car];
                var line = cars.Line[car];
                lines.Span<CarLineRow>()[car] = new CarLineRow((uint)line.ArcCount, (uint)line.LaneCount, line.LengthM, 0u);
                for (var arc = 0; arc < line.ArcCount; arc++) lineArcs.Span<ArcRow>()[(car * arcsPerCar) + arc] = ArcRow.Of(cars.LineOf(car)[arc]);
                for (var slot = 0; slot < line.LaneCount; slot++)
                {
                    chain.Span<int>()[(car * lanes) + slot] = cars.ChainOf(car)[slot];
                    starts.Span<float>()[(car * lanes) + slot] = cars.LaneStartsOf(car)[slot];
                    ends.Span<float>()[(car * lanes) + slot] = cars.LaneEndsOf(car)[slot];
                }

                var hold = town.PlanHoldOf(car);
                var cut = LaneClaim.Nothing;
                var cutOn = -1;
                if (hold != LaneOccupancy.NoHold)
                {
                    town.Occupancy.HoldEndsAtM(hold, out _, out cut);
                    cutOn = town.Occupancy.HoldCutOn(hold);
                }

                var hasAPlace = town.SceneOf(car, out var placeM, out var reachM);
                var flags = (cars.BlueLight[car] ? 1u : 0u) | (cars.LineIsReverse[car] ? 2u : 0u) | (hasAPlace ? 4u : 0u)
                            | (hold != LaneOccupancy.NoHold ? 8u : 0u) | (cut.Found ? 16u : 0u);
                carRows.Span<ContextCarRow>()[car] = new ContextCarRow(
                    cars.ProgressM[car], cars.CoveredSinceClaimM[car], cars.AuthorityM[car], cars.HorizonM[car], cars.ClaimFromM[car],
                    cars.CommittedToM[car], cars.GrantMarginM[car], plannedBefore[car], (uint)cars.GrantCutBy[car], flags, cutOn, 0f,
                    placeM, reachM, 0f, cars.PositionM[car], 0f, 0f);
                cutters.Span<DeviceClaims.ClaimRow>()[car] = cut.Found ? DeviceClaims.ClaimRow.Of(cut, cutOn, false, cut.FromM) : default;
            }

            kernel.Dispatch(batch.Begin(), set, (uint)Math.Max(1, (cars.Count + 63) / 64), new Figures(
                (uint)cars.Count, (uint)arcsPerCar, (uint)lanes, (uint)town.Ways.OfRoadConnector(0), Config.Ambulance.SlowerToPassMps,
                Config.Patience.LightQueueInCarLengths));
            batch.SubmitAndWait();

            for (var car = 0; car < cars.Count; car++)
            {
                if (!followed[car] || cars.Action[car] != CarAction.Follow || cars.Line[car].LaneCount == 0) continue;

                asked++;
                Compare(cars, car, contexts.Span<ContextRow>()[car], boxes.Span<BoxRow>()[car], loop.Tick, differ);
            }
        };

        for (var tick = 0; tick < Ticks; tick++) loop.Advance();

        Assert.True(differ.Count == 0, $"{differ.Count} of {asked} contexts differ: {string.Join("; ", differ.Take(6))}");
    }

    static void Compare(CarFleet cars, int car, in ContextRow context, in BoxRow box, long tick, List<string> differ)
    {
        var host = cars.Context[car];
        var boxFlags = (cars.BoxIsOurs[car] ? 1u : 0u) | (cars.TurningAtTheBox[car] ? 2u : 0u) | (cars.InsideTheBox[car] ? 4u : 0u)
                       | (cars.CommittedToTheBox[car] ? 8u : 0u);
        var same = host.AuthorityM == context.AuthorityM
                   && (uint)host.GrantCutBy == context.GrantCutBy && Same(host.PlaceStopM, context.PlaceStopM)
                   && host.MarginM == context.MarginM && host.WaitsToPass == (context.WaitsToPass != 0u)
                   && host.HorizonM == context.HorizonM && cars.ToTheBoxM[car] == box.ToTheBoxM
                   && cars.LightAheadM[car] == box.LightAheadM && boxFlags == box.Flags;
        if (!same) differ.Add($"tick {tick} car {car}: town {host} box {cars.ToTheBoxM[car]} light {cars.LightAheadM[car]} flags {boxFlags}; device {context} {box}");
    }

    /// <summary>The same float to the bit.</summary>
    static bool Same(float host, float onDevice) => BitConverter.SingleToInt32Bits(host) == BitConverter.SingleToInt32Bits(onDevice);
}
