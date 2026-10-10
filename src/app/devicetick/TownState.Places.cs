using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The town's places</b> (<c>world/town/kernels/town_places.glsl</c>, ids 448–511): the bay registry, who is inside
/// what, the plan's buildings and their ways in, what each is for, and the street each bay is worked off.
/// </summary>
internal sealed partial class TownState
{
    const int ParkingCentreMId = 448;
    const int ParkingHeadingRadId = 449;
    const int ParkingDoorMId = 450;
    const int ParkingBayOfCarId = 451;
    const int ParkingCarInBayId = 452;
    const int ParkingClaimedById = 453;
    const int ParkingClaimedBayOfCarId = 454;
    const int ParkingTurningInId = 455;
    const int ParkingTurnOfCarId = 456;
    const int ParkingHeldForId = 457;
    const int ParkingNearStartsId = 458;
    const int ParkingNearCountsId = 459;
    const int ParkingNearItemsId = 460;
    const int ContainersBuildingCapacityId = 461;
    const int ContainersBuildingOccupantsId = 462;
    const int ContainersBuildingClaimsId = 463;
    const int ContainersCarDriverId = 464;
    const int ContainersCarPassengerId = 465;
    const int ContainersCarCrewId = 466;
    const int BuildingsCentreMId = 467;
    const int BuildingsSizeMId = 468;
    const int BuildingsHeadingRadId = 469;
    const int BuildingsUseId = 470;
    const int BuildingsEntryOffsetsId = 471;
    const int BuildingsEntryPointMId = 472;
    const int TownBuildingOfDoorId = 473;
    const int TownDoorsStartsId = 474;
    const int TownDoorsCountsId = 475;
    const int TownDoorsItemsId = 476;
    const int UsesHospitalsId = 477;
    const int UsesPoliceStationsId = 478;
    const int UsesDepotsId = 479;
    const int BayStreetsLaneId = 480;
    const int BayStreetsAtLaneMId = 481;
    const int BayStreetsFirstBayOfLaneId = 482;
    const int BayStreetsBaysOffLaneId = 483;
    const int BayStreetsBayOnLaneId = 484;
    const int PlacesStateId = 485;

    /// <summary>A bucket grid's window, level and reach (<c>BucketGridRow</c>), field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct BucketGridRow(
        int FromX, int FromY, int Width, int Height, float PerCellM, float CellM, float MaxRadiusM, int Count);

    /// <summary>How many of each place, one bay's size and the two grids (<c>PlacesState</c>), field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct PlacesStateRow(
        int BayCount, int BuildingCount, int DoorCount, int Hospitals, int PoliceStations, int Depots, float SpaceLengthM,
        float SpaceWidthM, BucketGridRow ParkingNear, BucketGridRow Doors);

    void LayThePlaces(TownWorld town)
    {
        var capacity = town.Cars.Capacity;
        var parking = town.Parking.Kept;
        Lay<Vector2>(ParkingCentreMId, parking.CentreM);
        Lay<float>(ParkingHeadingRadId, parking.HeadingRad);
        Lay<Vector2>(ParkingDoorMId, parking.DoorM);
        Lay<int>(ParkingBayOfCarId, parking.BayOfCar);
        Lay<int>(ParkingCarInBayId, parking.CarInBay);
        Lay<int>(ParkingClaimedById, parking.ClaimedBy);
        Lay<int>(ParkingClaimedBayOfCarId, parking.ClaimedBayOfCar);
        Lay<int>(ParkingTurningInId, parking.TurningIn);
        Lay<int>(ParkingTurnOfCarId, parking.TurnOfCar);
        Lay<int>(ParkingHeldForId, parking.HeldFor);
        var parkingNear = LayGrid(ParkingNearStartsId, ParkingNearCountsId, ParkingNearItemsId, parking.Near);

        var buildings = town.Plan.Buildings;
        var containers = town.Containment;
        var occupants = new int[buildings.Count];
        var claims = new int[buildings.Count];
        for (var building = 0; building < occupants.Length; building++)
        {
            occupants[building] = containers.OccupantsOf(building);
            claims[building] = containers.ClaimsOn(building);
        }

        var drivers = new int[capacity];
        var passengers = new int[capacity];
        var crew = new int[capacity * Containers.CrewSeats];
        for (var car = 0; car < capacity; car++)
        {
            drivers[car] = containers.DriverOf(car);
            passengers[car] = containers.PassengerOf(car);
            for (var seat = 0; seat < Containers.CrewSeats; seat++) crew[(car * Containers.CrewSeats) + seat] = containers.CrewOf(car, seat);
        }

        Lay<int>(ContainersBuildingCapacityId, buildings.Capacity);
        Lay<int>(ContainersBuildingOccupantsId, occupants);
        Lay<int>(ContainersBuildingClaimsId, claims);
        Lay<int>(ContainersCarDriverId, drivers);
        Lay<int>(ContainersCarPassengerId, passengers);
        Lay<int>(ContainersCarCrewId, crew);

        Lay<Vector2>(BuildingsCentreMId, buildings.CentreM);
        Lay<Vector2>(BuildingsSizeMId, buildings.SizeM);
        Lay<float>(BuildingsHeadingRadId, buildings.HeadingRad);
        LayWidened<BuildingUse>(BuildingsUseId, buildings.Use);
        Lay<int>(BuildingsEntryOffsetsId, buildings.EntryOffsets);
        Lay<Vector2>(BuildingsEntryPointMId, buildings.EntryPointM);

        var doors = town.Doors;
        Lay<int>(TownBuildingOfDoorId, doors.BuildingOf);
        var doorsGrid = LayGrid(TownDoorsStartsId, TownDoorsCountsId, TownDoorsItemsId, doors.Grid);

        Lay<int>(UsesHospitalsId, town.Hospitals.Buildings);
        Lay<int>(UsesPoliceStationsId, town.PoliceStations.Buildings);
        Lay<int>(UsesDepotsId, town.Depots.Buildings);

        var streets = town.BayStreets.Kept;
        Lay<int>(BayStreetsLaneId, streets.Lane);
        Lay<float>(BayStreetsAtLaneMId, streets.AtLaneM);
        Lay<int>(BayStreetsFirstBayOfLaneId, streets.FirstBayOfLane);
        Lay<int>(BayStreetsBaysOffLaneId, streets.BaysOffLane);
        Lay<int>(BayStreetsBayOnLaneId, streets.BayOnLane);

        LayOne(PlacesStateId, new PlacesStateRow(
            town.Parking.BayCount, buildings.Count, buildings.EntryPointM.Length, town.Hospitals.Count, town.PoliceStations.Count,
            town.Depots.Count, parking.SpaceLengthM, parking.SpaceWidthM, parkingNear, doorsGrid));
    }

    /// <summary>
    /// <b>A bucket grid's tables as it stands</b>: each cell's start and count, nought where the bucket is not live, and the
    /// items filed in them — and its window, level and reach.
    /// </summary>
    BucketGridRow LayGrid(int startsId, int countsId, int itemsId, BucketGrid grid)
    {
        var kept = grid.Kept;
        var window = grid.Window;
        var starts = new int[window.Count];
        var counts = new int[window.Count];
        for (var cell = 0; cell < starts.Length; cell++)
        {
            if (kept.Stamp[cell] != kept.Generation) continue;

            starts[cell] = kept.BucketStart[cell];
            counts[cell] = kept.BucketCount[cell];
        }

        Lay<int>(startsId, starts);
        Lay<int>(countsId, counts);
        Lay<int>(itemsId, kept.Items.AsSpan(0, grid.Count));
        return new BucketGridRow(
            window.FromX, window.FromY, window.Width, window.Height, window.Level.PerCellM, window.Level.CellM, kept.MaxRadiusM, grid.Count);
    }
}
