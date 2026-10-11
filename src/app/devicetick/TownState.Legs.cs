using System.Runtime.InteropServices;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>What the drive leg, the line and parking read of the figures, and count of what they did</b>
/// (<c>world/town/kernels/town_legs_figures.glsl</c>, ids 171–172): one row of <see cref="SimConfig"/>'s figures, and one of
/// counts that start at nought — what the device adds to the town's own instruments.
/// </summary>
internal sealed partial class TownState
{
    const int LegsFiguresId = 171;
    const int LegsCountsId = 172;

    /// <summary>The figures (<c>LegsFigures</c>), field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct LegsFiguresRow(
        float StopSpeedMps, float CarPatienceS, int ReroutesPerLeg, float LightQueueInCarLengths, float CarBlockedWayPriceM,
        float CarBlockedWayLifeS, float PersonWalkWorthM, float TurnAtALotWithinM, float ProjectionWindowInCarLengths,
        float CarOffPathM, float GripMargin, float BrakingMargin, float TurnPriceNearSideCarLengths,
        float TurnPriceAcrossOncomingCarLengths, float NominalCarLengthM, float LaneSwitchPriceCarLengths)
    {
        public static LegsFiguresRow Of(SimConfig config) => new(
            config.Driving.StopSpeedMps, config.CarPatienceS, config.Patience.ReroutesPerLeg, config.Patience.LightQueueInCarLengths,
            config.CarBlockedWayPriceM, config.CarBlockedWayLifeS, config.PersonWalkWorthM, config.TurnAtALotWithinM,
            config.Driving.ProjectionWindowInCarLengths, config.CarOffPathM, config.Driving.GripMargin, config.Driving.BrakingMargin,
            config.Driving.TurnPriceNearSideCarLengths, config.Driving.TurnPriceAcrossOncomingCarLengths,
            config.Driving.NominalCarLengthM, config.Driving.LaneSwitchPriceCarLengths);
    }

    /// <summary>The counts (<c>LegsCounts</c>), field for field: what the device added to each of the town's own.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct LegsCountsRow(
        uint LegsGivenUp, uint ReroutesTaken, uint PlacesGivenUp, uint TurnsAtALotBegun, uint LinesReacquired, uint BaysParkedIn,
        uint RouteArrivals, uint RouteSearches);

    void LayTheLegs(TownWorld town)
    {
        LayOne(LegsFiguresId, LegsFiguresRow.Of(town.Config));
        LayOne(LegsCountsId, default(LegsCountsRow));
    }
}
