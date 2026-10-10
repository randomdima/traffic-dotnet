using System.Runtime.InteropServices;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>What each car's actions keep of it between ticks</b> (<c>world/town/kernels/town_car_actions.glsl</c>, ids
/// 1024–1151): every per-car array of <see cref="Agents.Car.Actions.Overtaking"/>, <see cref="Agents.Car.Actions.Switching"/>,
/// <see cref="Agents.Car.Actions.BackingUp"/>, <see cref="Agents.Car.Actions.BayManoeuvring"/>,
/// <see cref="Agents.Car.Control.Manoeuvres"/>, <see cref="Agents.Car.Actions.Rejoining"/> and the actions' own.
/// </summary>
internal sealed partial class TownState
{
    const int CarActionsEntriesId = 1024;
    const int DrivingGroundPlanHoldId = 1025;
    const int RejoiningLooksInSId = 1026;
    const int OvertakingShapesId = 1027;
    const int OvertakingSweptRunsId = 1028;
    const int OvertakingSweptCountId = 1029;
    const int OvertakingSweptStationAtMId = 1030;
    const int OvertakingAskedRunsId = 1031;
    const int OvertakingAskedCountId = 1032;
    const int OvertakingAskedStationAtMId = 1033;
    const int OvertakingPassedId = 1034;
    const int OvertakingPassedCountId = 1035;
    const int OvertakingAskingId = 1036;
    const int OvertakingLooksInSId = 1037;
    const int OvertakingWaitedSId = 1038;
    const int OvertakingDecidesInSId = 1039;
    const int OvertakingLooksId = 1040;
    const int OvertakingAskedAsId = 1041;
    const int OvertakingLaidForId = 1042;
    const int OvertakingRoomMId = 1043;
    const int OvertakingRoomLaneId = 1044;
    const int SwitchingSweptRunsId = 1045;
    const int SwitchingSweptCountId = 1046;
    const int SwitchingSweptStationAtMId = 1047;
    const int SwitchingAskedRunsId = 1048;
    const int SwitchingAskedCountId = 1049;
    const int SwitchingAskedStationAtMId = 1050;
    const int SwitchingInwardId = 1051;
    const int SwitchingAsideMId = 1052;
    const int SwitchingRoomToMId = 1053;
    const int SwitchingFromRestMId = 1054;
    const int SwitchingLooksInSId = 1055;
    const int SwitchingAskedAsId = 1056;
    const int SwitchingLaidForId = 1057;
    const int BackingUpAskedMId = 1058;
    const int BayManoeuvringPiecesRunsId = 1059;
    const int BayManoeuvringPiecesCountId = 1060;
    const int BayManoeuvringPiecesStationAtMId = 1061;
    const int BayManoeuvringAskedAsId = 1062;
    const int BayManoeuvringSweptForId = 1063;
    const int BayManoeuvringLaidForId = 1064;
    const int BayManoeuvringStandingOfId = 1065;
    const int BayManoeuvringStandingCoversId = 1066;
    const int ManoeuvresArcsId = 1067;
    const int ManoeuvresShapeId = 1068;
    const int ManoeuvresKindId = 1069;
    const int ManoeuvresStageId = 1070;
    const int ManoeuvresBayId = 1071;
    const int ManoeuvresLaneId = 1072;
    const int ManoeuvresPieceId = 1073;
    const int ManoeuvresStreetMId = 1074;
    const int CarActionsCountsId = 1075;

    /// <summary>A manoeuvre's shape (<c>ManoeuvreShape</c>): its two pieces' arcs and gears, a flag a word.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct ManoeuvreShapeRow(int FirstArcs, int SecondArcs, uint FirstReverse, uint SecondReverse)
    {
        public static ManoeuvreShapeRow Of(BayManoeuvre.Shape shape) =>
            new(shape.FirstArcs, shape.SecondArcs, shape.FirstReverse ? 1u : 0u, shape.SecondReverse ? 1u : 0u);
    }

    /// <summary>The actions' counts and the rebuild a kept station was read in (<c>CarActionsCounts</c>), field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct CarActionsCountsRow(
        long OvertakingAsked, long OvertakingWithdrawn, long OvertakingMade, long SwitchingAsked, long SwitchingWithdrawn,
        long SwitchingMade, long BackingUpCarTicksBackingUp, long BackingUpCarTicksBlocked, long BayManoeuvringAsked,
        long BayManoeuvringWithdrawn, long BayManoeuvringBegun, long BayManoeuvringParkedNoseIn, long BayManoeuvringParkedBackedIn,
        double BayManoeuvringStreetM, int BayManoeuvringRebuild, int Pad = 0);

    void LayTheCarActions(TownWorld town)
    {
        var capacity = town.Cars.Capacity;
        var entries = new uint[capacity];
        var planHold = new int[capacity];
        for (var car = 0; car < capacity; car++)
        {
            entries[car] = town.CarActions.EntriesOf(car);
            planHold[car] = town.PlanHoldOf(car);
        }

        Lay<uint>(CarActionsEntriesId, entries);
        Lay<int>(DrivingGroundPlanHoldId, planHold);
        Lay<float>(RejoiningLooksInSId, town.Rejoining.LooksInS);

        var overtaking = town.Overtaking.Kept;
        Lay(OvertakingShapesId, overtaking.Shapes, shape => DeviceCarTick.PassRow.Of(shape));
        LaySwept(OvertakingSweptRunsId, OvertakingSweptCountId, OvertakingSweptStationAtMId, overtaking.Swept);
        LaySwept(OvertakingAskedRunsId, OvertakingAskedCountId, OvertakingAskedStationAtMId, overtaking.AskedGround);
        var passed = new DeviceClaims.ClaimRow[overtaking.Passed.Length];
        for (var at = 0; at < passed.Length; at++)
        {
            passed[at] = DeviceClaims.ClaimRow.Of(overtaking.Passed[at], overtaking.PassedOn[at], gone: false, overtaking.Passed[at].FromM);
        }

        Lay<DeviceClaims.ClaimRow>(OvertakingPassedId, passed);
        Lay<int>(OvertakingPassedCountId, overtaking.PassedCount);
        Lay<int>(OvertakingAskingId, overtaking.Asking);
        Lay<float>(OvertakingLooksInSId, overtaking.LooksInS);
        Lay<float>(OvertakingWaitedSId, overtaking.WaitedS);
        Lay<float>(OvertakingDecidesInSId, overtaking.DecidesInS);
        LayFlags(OvertakingLooksId, overtaking.Looks);
        Lay<long>(OvertakingAskedAsId, overtaking.AskedAs);
        Lay<long>(OvertakingLaidForId, town.Overtaking.LaidFor);
        Lay<float>(OvertakingRoomMId, overtaking.RoomM);
        Lay<int>(OvertakingRoomLaneId, overtaking.RoomLane);

        var switching = town.Switching.Kept;
        LaySwept(SwitchingSweptRunsId, SwitchingSweptCountId, SwitchingSweptStationAtMId, switching.Swept);
        LaySwept(SwitchingAskedRunsId, SwitchingAskedCountId, SwitchingAskedStationAtMId, switching.AskedGround);
        LayFlags(SwitchingInwardId, switching.Inward);
        Lay<float>(SwitchingAsideMId, switching.AsideM);
        Lay<float>(SwitchingRoomToMId, switching.RoomToM);
        Lay<float>(SwitchingFromRestMId, switching.FromRestM);
        Lay<float>(SwitchingLooksInSId, switching.LooksInS);
        Lay<long>(SwitchingAskedAsId, switching.AskedAs);
        Lay<long>(SwitchingLaidForId, town.Switching.LaidFor);

        Lay<float>(BackingUpAskedMId, town.BackingUp.AskedM);

        var bays = town.Bays.Kept;
        LaySwept(BayManoeuvringPiecesRunsId, BayManoeuvringPiecesCountId, BayManoeuvringPiecesStationAtMId, bays.Pieces);
        Lay<long>(BayManoeuvringAskedAsId, bays.AskedAs);
        Lay<long>(BayManoeuvringSweptForId, bays.SweptFor);
        Lay<long>(BayManoeuvringLaidForId, town.Bays.LaidFor);
        Lay<Agents.Car.Actions.BayManoeuvring.Standing>(BayManoeuvringStandingOfId, bays.StandingOf);
        Lay<WayCover>(BayManoeuvringStandingCoversId, bays.StandingCovers);

        var manoeuvres = town.Manoeuvres;
        var arcs = new ArcRow[capacity * BayManoeuvre.MostArcs];
        for (var car = 0; car < capacity; car++)
        {
            var room = manoeuvres.RoomOf(car);
            for (var arc = 0; arc < room.Length; arc++) arcs[(car * BayManoeuvre.MostArcs) + arc] = ArcRow.Of(room[arc]);
        }

        Lay<ArcRow>(ManoeuvresArcsId, arcs);
        Lay(ManoeuvresShapeId, manoeuvres.Shape, ManoeuvreShapeRow.Of);
        LayWidened<Agents.Car.Control.ManoeuvreKind>(ManoeuvresKindId, manoeuvres.Kind);
        LayWidened<Agents.Car.Control.ManoeuvreStage>(ManoeuvresStageId, manoeuvres.Stage);
        Lay<int>(ManoeuvresBayId, manoeuvres.Bay);
        Lay<int>(ManoeuvresLaneId, manoeuvres.Lane);
        Lay<int>(ManoeuvresPieceId, manoeuvres.Piece);
        Lay<float>(ManoeuvresStreetMId, manoeuvres.StreetM);

        var (o, s, b, m) = (town.Overtaking, town.Switching, town.BackingUp, town.Bays);
        LayOne(CarActionsCountsId, new CarActionsCountsRow(
            o.Asked, o.Withdrawn, o.Made, s.Asked, s.Withdrawn, s.Made, b.CarTicksBackingUp, b.CarTicksBlocked, m.Asked, m.Withdrawn,
            m.Begun, m.ParkedNoseIn, m.ParkedBackedIn, m.StreetM, bays.Rebuild));
    }

    /// <summary>Ground swept once (<see cref="SweptGround"/>) as its three tables: the runs, how many each holder holds, and its last station.</summary>
    void LaySwept(int runsId, int countId, int stationAtMId, SweptGround ground)
    {
        var kept = ground.Kept;
        Lay<SweptRun>(runsId, kept.Runs);
        Lay<int>(countId, kept.Count);
        Lay<float>(stationAtMId, kept.StationAtM);
    }
}
