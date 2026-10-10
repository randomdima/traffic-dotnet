using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Agents.Ambulance;
using TrafficSimulation.Agents.Evacuator;
using TrafficSimulation.Agents.Service;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The town's errands</b> (<c>world/town/kernels/town_errands.glsl</c>, ids 512–639): each duty's per-car arrays, the
/// rounds and orders, the lanes closed and the aprons, and what the interface has handed the town.
/// </summary>
internal sealed partial class TownState
{
    const int DutyStageId = 512;
    const int DutyCasualtyId = 513;
    const int DutyHospitalId = 514;
    const int DutyHomeBayId = 515;
    const int DutySinceSId = 516;
    const int DutyLoadedForSId = 517;
    const int BeatStageId = 518;
    const int BeatStationId = 519;
    const int BeatHomeBayId = 520;
    const int BeatSinceSId = 521;
    const int BeatCasualtyId = 522;
    const int BeatWreckId = 523;
    const int BeatClosedForSId = 524;
    const int BeatClosedLanesId = 525;
    const int BeatClosedCountId = 526;
    const int BeatSceneLaneId = 527;
    const int BeatPostMId = 528;
    const int BeatStandMId = 529;
    const int BeatOfficerId = 530;
    const int RecoveryStageId = 531;
    const int RecoveryWreckId = 532;
    const int RecoveryTowingId = 533;
    const int RecoveryOnTheHookOfId = 534;
    const int RecoveryHeldByTheTailId = 535;
    const int RecoveryDepotId = 536;
    const int RecoveryYardId = 537;
    const int RecoveryHomeBayId = 538;
    const int RecoveryHaulsLeftId = 539;
    const int RecoveryInTheYardId = 540;
    const int RecoverySinceSId = 541;
    const int RecoveryHitchedForSId = 542;
    const int RecoveryRepairedForSId = 543;
    const int ServiceBeatDistrictId = 544;
    const int ServiceBeatPatrolsId = 545;
    const int ServiceBeatSetsOutAtSId = 546;
    const int RoundStandSId = 547;
    const int RoundStoodSId = 548;
    const int CarOrdersManualId = 549;
    const int CarOrdersKindId = 550;
    const int CarOrdersPointMId = 551;
    const int CarOrdersLeadId = 552;
    const int CarOrdersAimedAtMId = 553;
    const int TownClosedLanesId = 554;
    const int TownApronBaysId = 555;
    const int TownSelectedId = 556;
    const int TownOrderedPeopleId = 557;
    const int TownOrderedToMId = 558;
    const int ErrandsStateId = 559;

    /// <summary>What a hand holds (<c>HandInput</c>), a flag a word.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct HandInputRow(uint Held, float Throttle, float Steer, uint Handbrake, Vector2 WalkDirection)
    {
        public static HandInputRow Of(HandInput hand) =>
            new(hand.Held ? 1u : 0u, hand.Throttle, hand.Steer, hand.Handbrake ? 1u : 0u, hand.WalkDirection);
    }

    /// <summary>One unit of the selection (<c>Selection</c>), its kind a word.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct SelectionRow(uint Kind, int Index)
    {
        public static SelectionRow Of(Selection unit) => new((uint)unit.Kind, unit.Index);
    }

    /// <summary>The town's errand scalars (<c>ErrandsState</c>), field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct ErrandsStateRow(
        HandInputRow Hands, HandInputRow OtherHand, int OtherCar, int Ordered, int SelectedCount, int WoundedCount, int WreckCount,
        int MendingCount, int OnTheBar, uint TownParks, uint ClosuresChanged, int ApronStride, int BeatMostLanesClosed, int Pad = 0);

    void LayTheErrands(TownWorld town)
    {
        var duty = town.Duty;
        LayWidened<RescueStage>(DutyStageId, duty.Stage);
        Lay<int>(DutyCasualtyId, duty.Casualty);
        Lay<int>(DutyHospitalId, duty.Hospital);
        Lay<int>(DutyHomeBayId, duty.HomeBay);
        Lay<float>(DutySinceSId, duty.SinceS);
        Lay<float>(DutyLoadedForSId, duty.LoadedForS);

        var beat = town.Beat;
        LayWidened<PatrolStage>(BeatStageId, beat.Stage);
        Lay<int>(BeatStationId, beat.Station);
        Lay<int>(BeatHomeBayId, beat.HomeBay);
        Lay<float>(BeatSinceSId, beat.SinceS);
        Lay<int>(BeatCasualtyId, beat.Casualty);
        Lay<int>(BeatWreckId, beat.Wreck);
        Lay<float>(BeatClosedForSId, beat.ClosedForS);
        Lay<int>(BeatClosedLanesId, beat.ClosedLanes);
        Lay<int>(BeatClosedCountId, beat.ClosedCount);
        Lay<int>(BeatSceneLaneId, beat.SceneLane);
        Lay<float>(BeatPostMId, beat.PostM);
        Lay<float>(BeatStandMId, beat.StandM);
        Lay<int>(BeatOfficerId, beat.Officer);

        var recovery = town.Recovery;
        LayWidened<RecoveryStage>(RecoveryStageId, recovery.Stage);
        Lay<int>(RecoveryWreckId, recovery.Wreck);
        Lay<int>(RecoveryTowingId, recovery.Towing);
        Lay<int>(RecoveryOnTheHookOfId, recovery.OnTheHookOf);
        LayFlags(RecoveryHeldByTheTailId, recovery.HeldByTheTail);
        Lay<int>(RecoveryDepotId, recovery.Depot);
        Lay<int>(RecoveryYardId, recovery.Yard);
        Lay<int>(RecoveryHomeBayId, recovery.HomeBay);
        Lay<int>(RecoveryHaulsLeftId, recovery.HaulsLeft);
        LayFlags(RecoveryInTheYardId, recovery.InTheYard);
        Lay<float>(RecoverySinceSId, recovery.SinceS);
        Lay<float>(RecoveryHitchedForSId, recovery.HitchedForS);
        Lay<float>(RecoveryRepairedForSId, recovery.RepairedForS);

        var serviceBeat = town.ServiceBeat;
        Lay<int>(ServiceBeatDistrictId, serviceBeat.District);
        LayFlags(ServiceBeatPatrolsId, serviceBeat.Patrols);
        Lay<float>(ServiceBeatSetsOutAtSId, serviceBeat.SetsOutAtS);

        var (round, townParks) = town.Rounds;
        Lay<float>(RoundStandSId, round.StandS);
        Lay<float>(RoundStoodSId, round.StoodS);

        var orders = town.CarOrders;
        LayFlags(CarOrdersManualId, orders.Manual);
        LayWidened<PlayerOrder>(CarOrdersKindId, orders.Kind);
        Lay<Vector2>(CarOrdersPointMId, orders.PointM);
        Lay<int>(CarOrdersLeadId, orders.Lead);
        Lay<Vector2>(CarOrdersAimedAtMId, orders.AimedAtM);

        var aprons = town.Aprons;
        LayFlags(TownClosedLanesId, town.ClosedLanes);
        Lay<int>(TownApronBaysId, aprons.Bays);

        var commands = town.Commands;
        var selected = new SelectionRow[commands.Selected.Capacity];
        var units = commands.Selected.Units;
        for (var at = 0; at < units.Length; at++) selected[at] = SelectionRow.Of(units[at]);

        Lay<SelectionRow>(TownSelectedId, selected);
        Lay<int>(TownOrderedPeopleId, commands.OrderedPeople);
        Lay<Vector2>(TownOrderedToMId, commands.OrderedToM);

        var wrecks = town.Wrecks;
        LayOne(ErrandsStateId, new ErrandsStateRow(
            HandInputRow.Of(commands.Hands), HandInputRow.Of(commands.OtherHand), commands.OtherCar, commands.Ordered, units.Length,
            town.WoundedWaiting, wrecks.Waiting, wrecks.Mending, wrecks.OnTheBar, townParks ? 1u : 0u, town.ClosuresChanged ? 1u : 0u,
            aprons.Stride, beat.MostLanesClosed));
    }
}
