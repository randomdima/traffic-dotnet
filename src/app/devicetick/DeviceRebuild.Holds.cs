using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using static TrafficSimulation.App.DeviceTick.DeviceClaims;
using static TrafficSimulation.Runtime.GpuBuffer;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The holds the town reads, sent back as it asks for them</b> (<see cref="HoldsAsAsked"/>): a layer that lands later
/// sends back none of its holds, and leaves them on the device as the readers' layer holds them; each tick asks for the
/// holds of the agents the town decides and drives itself (<c>readers_holds_out.comp</c>), written into the readers' layer
/// before any of them is read.
/// </summary>
/// <remarks>
/// <b>A hold is read only by its own holder</b> — every hold the town reads is a car's or a walker's own, in its decision or
/// in the town's own tick of it; every other reader goes by a way's lists. What reads another's hold — the backing, the
/// checks — reads a layer sent back whole.
/// </remarks>
internal sealed partial class DeviceRebuild
{
    /// <summary>One hold asked for (<c>readers_holds_out.comp</c>'s <c>HoldAnswered</c>): its number, the stamp that wrote it, and it.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct HoldAnswered(uint Hold, uint Stamp, uint Pad0, uint Pad1, HoldOut Held, ClaimRow Cutter);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct HoldsAskedPush(uint Count, uint CarStamp, uint WalkerStamp, uint Stamp, uint Mode)
    {
        public const uint Asked = 0u, Found = 1u, FoundWalkers = 2u;
    }

    /// <summary>Whose decision an ask waits on (<c>readers_holds_out.comp</c>'s): none, or a car's or a walker's, by index.</summary>
    const uint AskIfDecided = 0x80000000u, AskOfAWalker = 0x40000000u;

    /// <summary>A found car's hold number where it holds none (<c>readers_holds_out.comp</c>'s <c>NoHold</c>).</summary>
    const uint FoundNoHold = 0xffffffffu;

    /// <summary>
    /// Every hold and what cut it as the readers' layer holds them, copied on the device as a layer is sent back; the holds
    /// asked this tick, written by the host; what came of them, in the host's memory.
    /// </summary>
    GpuBuffer _readersHoldsOut = null!, _readersHoldCutters = null!, _holdsAsked = null!, _holdsAnswered = null!;

    /// <summary>
    /// Each car's hold number as the readers' layer numbers it (<see cref="_holdOf"/>, copied with the layer), and each car
    /// the tick found more than its clock without the host naming it, its own hold as that layer holds it, a row a car in the
    /// host's memory (<see cref="DeviceCarTick.FindsTheDecisionsDue"/>).
    /// </summary>
    GpuBuffer _readersHoldOf = null!, _foundAnswered = null!;

    /// <summary>The same of the walkers (<see cref="DeviceWalkers.FindsTheDecisionsDue"/>): each one's hold number as the readers' layer numbers it, and what came of each one found.</summary>
    GpuBuffer _readersWalkHoldOf = null!, _foundWalkerAnswered = null!;

    Bound _holdsAskedKernel;
    int _holdsAskedCount;

    /// <summary>The stamp the last holds asked for were handed back with, never the zero of one unwritten.</summary>
    uint _holdsStamp;

    /// <summary>
    /// <b>Whether a layer that lands later sends back only the holds the town asks for</b>, each tick
    /// (<see cref="RecordTheHoldsAsked"/>) — or every hold with it. Only where the town takes every decision once the
    /// device's verdicts are in, which is when the holds asked for come back; never where a layer sent back in part is
    /// held to the whole (<see cref="ChecksTheLayerSentInPart"/>), which reads every hold.
    /// </summary>
    public bool HoldsAsAsked { get; set; }

    bool LandsEveryHold => !HoldsAsAsked || ChecksTheLayerSentInPart;

    /// <summary>How many holds the last tick asked for, and how many came back.</summary>
    public (int Asked, int Answered) HoldsAsked { get; private set; }

    /// <summary>The kernel that hands back the holds asked for, and its tables, laid once.</summary>
    void LayTheHoldsAsked(DeviceCarTick cars)
    {
        _readersHoldsOut = Device<HoldOut>(_holdRoom);
        _readersHoldCutters = Device<ClaimRow>(_holdRoom);
        var room = (2 * _cars) + _walkersRoom;
        _holdsAsked = Own(_vk.CreateBuffer(Bytes<UInt2>(room), BufferUsageFlags.StorageBufferBit, HostAccess.Writes));
        _holdsAnswered = Own(_vk.CreateBuffer(Bytes<HoldAnswered>(room), BufferUsageFlags.StorageBufferBit, HostAccess.Reads));
        _readersHoldOf = Device<int>(_cars);
        _foundAnswered = Own(_vk.CreateBuffer(Bytes<HoldAnswered>(_cars), BufferUsageFlags.StorageBufferBit, HostAccess.Reads));
        _readersWalkHoldOf = Device<int>(_walkersRoom);
        _foundWalkerAnswered = Own(_vk.CreateBuffer(Bytes<HoldAnswered>(_walkersRoom), BufferUsageFlags.StorageBufferBit, HostAccess.Reads));

        // An ask the kernel leaves unanswered keeps whatever the memory held, so it begins at a stamp no submit writes.
        _holdsAnswered.Span<HoldAnswered>().Clear();
        _foundAnswered.Span<HoldAnswered>().Clear();
        _foundWalkerAnswered.Span<HoldAnswered>().Clear();
        var verdicts = _walkers.Keeps ? _walkers.Verdicts : Device<uint>(1);
        var (foundCounts, foundMore) = cars.FoundTables;
        var (walkersFoundCounts, walkersFoundMore) = _walkers.Keeps ? _walkers.FoundTables : (Device<uint>(2), Device<uint>(1));
        _holdsAskedKernel = Bound.Of<HoldsAskedPush>(
            _vk, "readers_holds_out.comp",
            [
                _holdsAsked, _readersHoldsOut, _readersHoldCutters, cars.DecidedAt, verdicts, _holdsAnswered, foundCounts, foundMore,
                _readersHoldOf, _foundAnswered, walkersFoundCounts, walkersFoundMore, _readersWalkHoldOf, _foundWalkerAnswered,
            ]);
    }

    /// <summary>
    /// <b>Whether the readers' layer this tick's decisions read holds only the holds asked for</b> — the one landing this
    /// tick (<see cref="LandWhatWasSent"/>), or the one landed before it.
    /// </summary>
    public bool AsksForTheHolds(LaneOccupancy readers) => _landing == Landing.Sent ? !LandsEveryHold : !readers.KnowsEveryHold;

    /// <summary>Whether a layer lands this tick, so a hold is asked for by the number its holder reads once it has (<see cref="Landing"/>).</summary>
    public bool LandsThisTick => _landing == Landing.Sent;

    /// <summary>A hold the town reads this tick, whatever the device decides; nothing for none.</summary>
    public void AskTheHold(int hold) => Ask(hold, 0u);

    /// <summary>A car's hold the town reads this tick only where the device hands the car's decision to it.</summary>
    public void AskTheHoldIfDecided(int hold, int car) => Ask(hold, AskIfDecided | (uint)car);

    /// <summary>A walker's hold the town reads this tick only where the device hands the walker's decision to it.</summary>
    public void AskTheWalkersHoldIfDecided(int hold, int person) => Ask(hold, AskIfDecided | AskOfAWalker | (uint)person);

    void Ask(int hold, uint whose)
    {
        if (hold == LaneOccupancy.NoHold) return;

        _holdsAsked.Span<UInt2>()[_holdsAskedCount++] = new UInt2((uint)hold, whose);
    }

    /// <summary>
    /// <b>The holds asked for this tick handed back</b>, recorded after the decisions put off are asked of the device
    /// (<c>car_decide.glsl</c>, <c>walkers_decide.comp</c>): off the readers' copies the last layer sent back left
    /// (<see cref="SendTheLayerBack"/>). Nothing where none was asked.
    /// </summary>
    /// <param name="carStamp">The car tick's stamp this tick (<see cref="DeviceCarTick.Stamp"/>).</param>
    /// <param name="walkerStamp">The walkers' decisions' stamp this tick (<see cref="DeviceWalkers.DecideStamp"/>).</param>
    /// <param name="ofTheFound">
    /// Whether the hold of each car the tick found more than its clock without the host naming it is handed back too
    /// (<see cref="DeviceCarTick.FindsTheDecisionsDue"/>), where <see cref="MirrorTheFoundHolds"/> reads it.
    /// </param>
    /// <param name="ofTheFoundWalkers">And of each walker found so (<see cref="DeviceWalkers.FindsTheDecisionsDue"/>), where <see cref="MirrorTheFoundWalkerHolds"/> reads it.</param>
    public void RecordTheHoldsAsked(CommandBuffer commands, uint carStamp, uint walkerStamp, bool ofTheFound, bool ofTheFoundWalkers)
    {
        if (_holdsAskedCount == 0 && !ofTheFound && !ofTheFoundWalkers) return;

        _vk.AfterTheSubmitsBefore(commands);
        var push = new HoldsAskedPush((uint)_holdsAskedCount, carStamp, walkerStamp, ++_holdsStamp, HoldsAskedPush.Asked);
        if (_holdsAskedCount > 0) _holdsAskedKernel.Dispatch(commands, _holdsAskedCount, push);
        if (ofTheFound) _holdsAskedKernel.Dispatch(commands, _cars, push with { Count = (uint)_cars, Mode = HoldsAskedPush.Found });
        if (ofTheFoundWalkers)
        {
            _holdsAskedKernel.Dispatch(
                commands, _walkersRoom, push with { Count = (uint)_walkersRoom, Mode = HoldsAskedPush.FoundWalkers });
        }
    }

    /// <summary>
    /// <b>The hold of each walker the decisions found more than its clock written into the readers' layer</b>, as
    /// <see cref="MirrorTheFoundHolds"/> writes a car's.
    /// </summary>
    /// <param name="checks">Whether to throw where the device numbers a walker's hold other than the town does.</param>
    public void MirrorTheFoundWalkerHolds(TownWorld town, ReadOnlySpan<int> found, bool checks)
    {
        var answers = _foundWalkerAnswered.Span<HoldAnswered>();
        foreach (var person in found)
        {
            ref readonly var said = ref answers[person];
            if (said.Stamp != _holdsStamp) throw new InvalidOperationException($"Walker {person}'s hold was found and never handed back.");

            var hold = said.Hold == FoundNoHold ? LaneOccupancy.NoHold : (int)said.Hold;
            if (checks && hold != town.WalkHold(person, landing: false))
            {
                throw new InvalidOperationException(
                    $"Walker {person}'s hold is {town.WalkHold(person, landing: false)} in the town and {hold} as the device numbers it.");
            }

            if (hold != LaneOccupancy.NoHold) MirrorTheHold(town.ReadersOccupancy, hold, said.Held, said.Cutter);
        }
    }

    /// <summary>
    /// <b>The hold of each car the tick found more than its clock written into the readers' layer</b>, by the number the
    /// readers' layer gives it — once the submit that handed them back is waited for and any layer landing this tick has
    /// landed (<see cref="RecordTheHoldsAsked"/>).
    /// </summary>
    /// <param name="checks">Whether to throw where the device numbers a car's hold other than the town does.</param>
    public void MirrorTheFoundHolds(TownWorld town, ReadOnlySpan<int> found, bool checks)
    {
        var answers = _foundAnswered.Span<HoldAnswered>();
        foreach (var car in found)
        {
            ref readonly var said = ref answers[car];
            if (said.Stamp != _holdsStamp) throw new InvalidOperationException($"Car {car}'s hold was found and never handed back.");

            var hold = said.Hold == FoundNoHold ? LaneOccupancy.NoHold : (int)said.Hold;
            if (checks && hold != town.PlanHoldOf(car))
            {
                throw new InvalidOperationException($"Car {car}'s hold is {town.PlanHoldOf(car)} in the town and {hold} as the device numbers it.");
            }

            if (hold != LaneOccupancy.NoHold) MirrorTheHold(town.ReadersOccupancy, hold, said.Held, said.Cutter);
        }
    }

    /// <summary>
    /// <b>The holds asked for written into the readers' layer</b>, once the submit that handed them back is waited for and
    /// any layer landing this tick has landed; the asks cleared for the next tick.
    /// </summary>
    public void MirrorTheHoldsAsked(LaneOccupancy readers)
    {
        var answered = 0;
        foreach (ref readonly var said in _holdsAnswered.Span<HoldAnswered>()[.._holdsAskedCount])
        {
            if (said.Stamp != _holdsStamp) continue;

            MirrorTheHold(readers, (int)said.Hold, said.Held, said.Cutter);
            answered++;
        }

        HoldsAsked = (_holdsAskedCount, answered);
        _holdsAskedCount = 0;
    }

    /// <summary>
    /// <b>Every hold the readers' layer was not sent, read back and written into it</b> — for a town about to read any agent's
    /// hold, as its own tick does once the device lets it go. Nothing where the layer holds every hold, or while a layer is
    /// still to land, whose holds the readers' copies already are.
    /// </summary>
    public void LandEveryHold(LaneOccupancy readers)
    {
        if (readers.KnowsEveryHold || _landing != Landing.None) return;

        var holds = readers.HoldCount;
        var commands = _batch.Begin();
        _vk.AfterTheSubmitsBefore(commands);
        _claims.CopyPast<HoldOut>(commands, _readersHoldsOut, _holdsRead, 0, holds);
        _claims.CopyPast<ClaimRow>(commands, _readersHoldCutters, _holdCuttersRead, 0, holds);
        _batch.SubmitAndWait();

        var read = _holdsRead.Span<HoldOut>();
        var cutters = _holdCuttersRead.Span<ClaimRow>();
        for (var hold = 0; hold < holds; hold++) MirrorTheHold(readers, hold, read[hold], cutters[hold]);
    }

    /// <summary>One hold as the readers' layer keeps it: what it came to and the claim that cut it, as that stood when it did.</summary>
    static void MirrorTheHold(LaneOccupancy occupancy, int hold, in HoldOut held, in ClaimRow cutter)
    {
        var terms = new HolderTerms(held.KeptOffWhatItMayPassM, (held.Flags & HoldRow.OnACall) != 0, held.PassesBelowMps);
        var cutBy = held.CutBy == ClaimAnswer.NoRow ? LaneClaim.Nothing : cutter.Claim with { ToM = held.CutterToM };
        occupancy.MirrorTheHold(
            hold, 0, 0, held.StandingMarginM, (held.Flags & HoldRow.Answered) != 0, terms, held.CutLineM, held.MarginM, cutBy, held.CutOn);
    }
}
