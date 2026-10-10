using System.Runtime.InteropServices;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>What each walker's actions keep of it between ticks</b> (<c>world/town/kernels/town_walker_actions.glsl</c>, ids
/// 1152–1279): what <see cref="Agents.Person.Actions.Sidestepping"/> and <see cref="Agents.Person.Actions.WalkingGround"/>
/// keep that the walk row does not.
/// </summary>
internal sealed partial class TownState
{
    const int SidesteppingDecidedId = 1152;
    const int SidesteppingSweptStationAtMId = 1153;
    const int SidesteppingAskedRunsId = 1154;
    const int SidesteppingAskedCountId = 1155;
    const int SidesteppingAskedStationAtMId = 1156;
    const int SidesteppingPassedId = 1157;
    const int SidesteppingLooksInSId = 1158;
    const int SidesteppingWaitedSId = 1159;
    const int SidesteppingDecidesInSId = 1160;
    const int WalkingGroundStraightStationAtMId = 1161;
    const int WalkerActionsCountsId = 1162;

    /// <summary>A walker's pass (<c>Sidestep</c>), begun a word.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct SidestepRow(int Slot, float FromM, float ClearM, float AsideM, uint Begun)
    {
        public static SidestepRow Of(Sidestep pass) => new(pass.Slot, pass.FromM, pass.ClearM, pass.AsideM, pass.Begun ? 1u : 0u);
    }

    /// <summary>The walkers' actions' counts (<c>WalkerActionsCounts</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct WalkerActionsCountsRow(long SidesteppingAsked, long SidesteppingWithdrawn, long SidesteppingMade);

    void LayTheWalkerActions(TownWorld town)
    {
        var sidestepping = town.Sidestepping.Kept;
        Lay(SidesteppingDecidedId, sidestepping.Decided, SidestepRow.Of);
        Lay<float>(SidesteppingSweptStationAtMId, sidestepping.Swept.Kept.StationAtM);
        LaySwept(SidesteppingAskedRunsId, SidesteppingAskedCountId, SidesteppingAskedStationAtMId, sidestepping.AskedGround);
        var passed = new DeviceClaims.ClaimRow[sidestepping.Passed.Length];
        for (var at = 0; at < passed.Length; at++)
        {
            passed[at] = DeviceClaims.ClaimRow.Of(
                sidestepping.Passed[at], sidestepping.PassedOn[at], gone: false, sidestepping.Passed[at].FromM);
        }

        Lay<DeviceClaims.ClaimRow>(SidesteppingPassedId, passed);
        Lay<float>(SidesteppingLooksInSId, sidestepping.LooksInS);
        Lay<float>(SidesteppingWaitedSId, sidestepping.WaitedS);
        Lay<float>(SidesteppingDecidesInSId, sidestepping.DecidesInS);
        Lay<float>(WalkingGroundStraightStationAtMId, town.WalkingGround.StraightGround.Kept.StationAtM);

        var counts = town.Sidestepping;
        LayOne(WalkerActionsCountsId, new WalkerActionsCountsRow(counts.Asked, counts.Withdrawn, counts.Made));
    }
}
