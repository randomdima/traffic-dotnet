using System.Runtime.InteropServices;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The ids of the tables the device's tick kept before the town was resident</b>, as
/// <c>world/town/kernels/town_rows.glsl</c> names them — the two held equal by <c>TownStateTests</c>.
/// </summary>
internal static class TownRows
{
    public const int Counts = 1;

    public const int CarsTicks = 64;
    public const int CarsTold = 65;
    public const int CarsGrants = 66;
    public const int CarsCutters = 67;
    public const int CarsHoldBits = 68;
    public const int CarsBoxes = 69;
    public const int CarsContexts = 70;
    public const int CarsBriefs = 71;
    public const int CarsLines = 72;
    public const int CarsArcs = 73;
    public const int CarsEntries = 74;
    public const int CarsChain = 75;
    public const int CarsStarts = 76;
    public const int CarsEnds = 77;
    public const int CarsFirstArcs = 78;
    public const int CarsBreaks = 79;
    public const int CarsPasses = 80;
    public const int CarsClocks = 81;
    public const int CarsPoses = 82;
    public const int CarsCommands = 83;
    public const int CarsTyreRows = 84;
    public const int CarsWheels = 85;
    public const int CarsWheelImpulses = 86;
    public const int CarsBuildOf = 87;
    public const int CarsBuilds = 88;
    public const int CarsBodyRows = 89;

    public const int PeopleTable = 256;
    public const int PeopleImpulses = 257;
    public const int PeopleBodies = 258;
    public const int PeopleWalks = 259;
    public const int PeopleRoutes = 260;
    public const int PeopleStraights = 261;
    public const int PeoplePasses = 262;
    public const int PeopleClocks = 263;

    /// <summary>How many of each the tables have room for, and each row's stride (<c>TownCounts</c>), field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct CountsRow(
        uint CarCapacity, uint PeopleCapacity, uint Cars, uint People, uint ArcsPerCar, uint MostLanes, uint Wheels,
        uint RouteLanesPerCar, uint RouteWaysPerPerson, uint MostRunsOfAStraight, uint MostRunsOfAPass, uint FirstCarAgent);
}
