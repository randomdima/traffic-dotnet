using System.Numerics;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The cars' fields no row holds</b> (<c>world/town/kernels/town_cars.glsl</c>, ids 128–255): every per-car array of
/// <see cref="Agents.Car.Body.CarFleet"/> the tick's own rows do not carry, and the town's own per-car arrays.
/// </summary>
internal sealed partial class TownState
{
    const int CarsDrawId = 128;
    const int CarsOwnerId = 129;
    const int CarsDrivenId = 130;
    const int CarsActionId = 131;
    const int CarsBrokenId = 132;
    const int CarsChannelsId = 133;
    const int CarsAmbulanceId = 134;
    const int CarsBlueLightId = 135;
    const int CarsAtWorkId = 136;
    const int CarsLineLaidId = 137;
    const int CarsRouteLaidId = 138;
    const int CarsStopsForBayId = 139;
    const int CarsStopsAtItsPlaceId = 140;
    const int CarsTurnsBackOnId = 141;
    const int CarsClaimToMId = 142;
    const int CarsRouteLanesId = 143;
    const int CarsRouteCountId = 144;
    const int CarsRouteTakenId = 145;
    const int CarsRouteRunsOutId = 146;
    const int CarsRouteEndsOnId = 147;
    const int CarsRouteEndsAtMId = 148;
    const int CarsDestinationMId = 149;
    const int CarsHasDestinationId = 150;
    const int CarsReroutesId = 151;
    const int CarsFuseJitterId = 152;
    const int CarsBacksIntoBaysId = 153;
    const int CarsLineIsReverseId = 154;
    const int CarsBackRoomMId = 155;
    const int TownBehindTheBarId = 160;
    const int TownAsksTheRouteId = 161;
    const int TownWreckedWordsId = 162;
    const int StandingKeptId = 163;
    const int StandingFrozeAtStepId = 164;
    const int StandingEntriesId = 165;
    const int StandingPositionMId = 166;
    const int StandingHeadingRadId = 167;
    const int StandingAskedId = 168;
    const int StandingAskedCountId = 169;
    const int StandingReadOnId = 170;

    void LayTheCars(TownWorld town)
    {
        var cars = town.Cars;

        // A stream is laid as its bytes — its state, then its increment — which is rng.glsl's Rng word for word.
        Lay<Rng>(CarsDrawId, cars.Draw);
        Lay<int>(CarsOwnerId, cars.Owner);
        LayFlags(CarsDrivenId, cars.Driven);
        LayWidened<Agents.Car.Actions.CarAction>(CarsActionId, cars.Action);
        LayFlags(CarsBrokenId, cars.Broken);
        LayWidened<byte>(CarsChannelsId, cars.Channels);
        LayFlags(CarsAmbulanceId, cars.Ambulance);
        LayFlags(CarsBlueLightId, cars.BlueLight);
        LayFlags(CarsAtWorkId, cars.AtWork);
        Lay<uint>(CarsLineLaidId, cars.LineLaid);
        Lay<uint>(CarsRouteLaidId, cars.RouteLaid);
        Lay<int>(CarsStopsForBayId, cars.StopsForBay);
        LayFlags(CarsStopsAtItsPlaceId, cars.StopsAtItsPlace);
        Lay<int>(CarsTurnsBackOnId, cars.TurnsBackOn);
        Lay<float>(CarsClaimToMId, cars.ClaimToM);
        Lay<int>(CarsRouteLanesId, cars.RouteLanes);
        Lay<int>(CarsRouteCountId, cars.RouteCount);
        Lay<int>(CarsRouteTakenId, cars.RouteTaken);
        LayFlags(CarsRouteRunsOutId, cars.RouteRunsOut);
        Lay<int>(CarsRouteEndsOnId, cars.RouteEndsOn);
        Lay<float>(CarsRouteEndsAtMId, cars.RouteEndsAtM);
        Lay<Vector2>(CarsDestinationMId, cars.DestinationM);
        LayFlags(CarsHasDestinationId, cars.HasDestination);
        LayWidened<byte>(CarsReroutesId, cars.Reroutes);
        Lay<float>(CarsFuseJitterId, cars.FuseJitter);
        LayFlags(CarsBacksIntoBaysId, cars.BacksIntoBays);
        LayFlags(CarsLineIsReverseId, cars.LineIsReverse);
        Lay<float>(CarsBackRoomMId, cars.BackRoomM);

        var asksTheRoute = new bool[cars.Capacity];
        for (var car = 0; car < asksTheRoute.Length; car++) asksTheRoute[car] = town.AsksTheRoute(car);

        LayFlags(TownBehindTheBarId, town.BehindTheBar);
        LayFlags(TownAsksTheRouteId, asksTheRoute);
        LayWords(TownWreckedWordsId, town.Settled.Wrecked, cars.Capacity);

        var standing = town.StandingKept;
        LayFlags(StandingKeptId, standing.Kept);
        Lay<int>(StandingFrozeAtStepId, standing.FrozeAtStep);
        Lay<uint>(StandingEntriesId, standing.Entries);
        Lay<Vector2>(StandingPositionMId, standing.PositionM);
        Lay<float>(StandingHeadingRadId, standing.HeadingRad);
        Lay<int>(StandingAskedId, standing.Asked);
        Lay<int>(StandingAskedCountId, [standing.AskedCount]);
        LayWidened<byte>(StandingReadOnId, standing.ReadOn);
    }

    /// <summary>A set of agents as the device keeps one: a word 32 of them, agent <c>a</c> bit <c>a &amp; 31</c> of word <c>a &gt;&gt; 5</c>.</summary>
    void LayWords(int id, AgentSet set, int agents)
    {
        var words = new uint[(agents + 31) >> 5];
        set.CopyWordsTo(agents, words);
        Lay<uint>(id, words);
    }
}
