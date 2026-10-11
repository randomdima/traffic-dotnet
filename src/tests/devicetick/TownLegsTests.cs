using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Town;
using Xunit;
using Network = TrafficSimulation.App.DeviceTick.DeviceRoutes.Network;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>The drive leg, the line and parking on the device, held to the town's own</b> (<c>town_legs.glsl</c>,
/// <c>town_legs_check.comp</c>): the suite's towns ticked until their cars are mid-route, laid on the device as they stand
/// (<see cref="LegsOnTheDevice"/>), and each probe run over a list of cars on the device and by the town in the same order —
/// every word a car is left with the same, to the bit.
/// </summary>
/// <remarks>
/// What the leg asks of another port — an errand, a car's action, how a car pulls into a bay — is answered on the device by
/// what the town answered before the probe, so what is held here is the leg's own arithmetic.
/// </remarks>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class TownLegsTests(Lavapipe device, TownLegsTests.Warmed warmed) : IClassFixture<Lavapipe>, IClassFixture<TownLegsTests.Warmed>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>How long a town is run before it is asked: a minute, by which its owners are driving to their bays.</summary>
    const int WarmTicks = 3_600;

    /// <summary>The suite's built town run to mid-route once, for the probes that change nothing.</summary>
    public sealed class Warmed : IDisposable
    {
        internal TownWorld Town { get; } = Staged(Towns.Built);

        public void Dispose() => Town.Dispose();
    }

    float DecisionS => Config.Sim.AgentDecisionIntervalS;


    [Fact]
    public void ALineLaidOffTheQueueIsTheTownsLine()
    {
        using var town = Staged(Towns.Built);
        var cars = RouteDrivers(town);
        using var onTheDevice = new LegsOnTheDevice(device, town, Listed(cars));
        var read = onTheDevice.Run(LegsOnTheDevice.LayLine, fromTheQueue: true);
        foreach (var car in cars) town.LayLine(car, town.Cars.Line[car].LaneCount, town.Cars.ProgressM[car], fromTheQueue: true);

        AssertRowsAlike(read, onTheDevice.Stride, cars, row => LegsOnTheDevice.RowOf(town, cars[row], onTheDevice.Stride));
    }

    /// <summary>
    /// <b>A line laid over the route, asked whole, is the town's</b> — for every car whose laying the town answers without
    /// a search (a spare of the town says which): one that does is asked of the route service, and answers on a later
    /// decision.
    /// </summary>
    [Fact]
    public void ALineLaidWithoutASearchIsTheTownsLine()
    {
        using var spare = Staged(Towns.Built);
        using var town = Staged(Towns.Built);
        var cars = new List<int>();
        foreach (var car in RouteDrivers(spare))
        {
            var searchedBefore = spare.RouteSearches;
            spare.LayLine(car, spare.Cars.Line[car].LaneCount, spare.Cars.ProgressM[car]);
            if (spare.RouteSearches == searchedBefore) cars.Add(car);
        }

        using var onTheDevice = new LegsOnTheDevice(device, town, Listed(cars));
        var read = onTheDevice.Run(LegsOnTheDevice.LayLine);
        foreach (var car in cars) town.LayLine(car, town.Cars.Line[car].LaneCount, town.Cars.ProgressM[car]);

        AssertRowsAlike(read, onTheDevice.Stride, cars, row => LegsOnTheDevice.RowOf(town, cars[row], onTheDevice.Stride));
    }

    /// <summary>
    /// <b>A route asked for at the end of a car's line is laid into its queue as the town lays its own search</b>
    /// (TryPlan, ExpandRoute, LayRouteLanes): asked of the route service on one decision, searched, and taken on the next —
    /// the same queue, where it ends, the turn it comes back on and what TryPlan answers.
    /// </summary>
    [Fact]
    public void ARouteLandedIsLaidAsTheTownLaysItsSearch()
    {
        using var town = Staged(Towns.Built);
        var cars = RouteDrivers(town).Where(car => town.Cars.HasDestination[car]).Take(DeviceRoutes.Slots).ToList();
        var listed = cars.Select(car => (car, town.Cars.ChainOf(car)[town.Cars.Line[car].LaneCount - 1])).ToList();
        using var onTheDevice = new LegsOnTheDevice(device, town, listed);
        var read = onTheDevice.Run(LegsOnTheDevice.TryPlan, landed: true);
        var found = listed.Select(asked => (uint)town.TryPlan(asked.car, asked.Item2)).ToList();

        AssertRowsAlike(read, onTheDevice.Stride, cars, row =>
        {
            var wanted = LegsOnTheDevice.RowOf(town, cars[row], onTheDevice.Stride);
            wanted[LegsOnTheDevice.Answer] = found[row];
            return wanted;
        });
    }

    [Fact]
    public void WhetherADecisionIsTheClockAloneIsTheTownsAnswer()
    {
        var town = warmed.Town;
        var cars = Enumerable.Range(0, town.Cars.Count).ToList();
        using var onTheDevice = new LegsOnTheDevice(device, town, Listed(cars));
        var read = onTheDevice.Run(LegsOnTheDevice.TakesOnlyTheClock, DecisionS);

        AssertWordsAlike(read, onTheDevice.Stride, cars, [16, LegsOnTheDevice.Answer], row => Words(
            (16, 0u), (LegsOnTheDevice.Answer, town.TakesOnlyTheClock(cars[row], DecisionS) ? 1u : 0u)));
    }

    /// <summary>
    /// <b>A leg's clock that does not run out on this decision runs as the town's</b> (WatchTheProgress: noted, or held at
    /// a light) — every car driving its route whose clock is more than a decision short of its patience.
    /// </summary>
    [Fact]
    public void AClockShortOfItsPatienceRunsAsTheTowns()
    {
        using var town = Staged(Towns.Built);
        var cars = RouteDrivers(town).Where(car => town.DriveClocks.Of(car).SinceS + DecisionS < town.ClockTermsOf(car).PatienceS).ToList();
        using var onTheDevice = new LegsOnTheDevice(device, town, Listed(cars));
        var read = onTheDevice.Run(LegsOnTheDevice.WatchTheProgress, DecisionS);
        foreach (var car in cars) town.WatchTheProgress(car, DecisionS);

        AssertRowsAlike(read, onTheDevice.Stride, cars, row => LegsOnTheDevice.RowOf(town, cars[row], onTheDevice.Stride));
    }

    /// <summary>
    /// <b>A leg whose clock has run out with a reroute left prices the way it is blocked entering up and routes again, as
    /// the town's does</b> (MarkTheWayBlocked): the same marks laid in the same slots, a reroute spent and the clock begun
    /// again — every car driving its route, its clock run out.
    /// </summary>
    [Fact]
    public void AClockRunOutPricesTheWayUpAsTheTownDoes()
    {
        using var town = Staged(Towns.Built);
        var cars = RouteDrivers(town).Where(car => town.Cars.Reroutes[car] < Config.Patience.ReroutesPerLeg).ToList();
        foreach (var car in cars) town.DriveClocks.Take(car, town.ClockTermsOf(car).PatienceS, 0f);

        using var onTheDevice = new LegsOnTheDevice(device, town, Listed(cars));
        var read = onTheDevice.Run(LegsOnTheDevice.WatchTheProgress, DecisionS);
        var reroutedBefore = town.ReroutesTaken;
        foreach (var car in cars) town.WatchTheProgress(car, DecisionS);

        AssertWordsAlike(read, onTheDevice.Stride, cars, [21, 22, 23], row => Words(
            (21, Bits(town.DriveClocks.Of(cars[row]).SinceS)), (22, Bits(town.DriveClocks.Of(cars[row]).ClosestM)),
            (23, town.Cars.Reroutes[cars[row]])));

        var surcharges = town.DriveSurcharges;
        var marks = new (int Link, float PriceM, float UntilS)[DeviceRoutes.MostMarks];
        var (count, laid, _) = onTheDevice.Routes.SurchargesOf(Network.Driving, marks);
        var townMarks = Enumerable.Range(0, surcharges.SlotsMarked).Select(slot => Marked(surcharges.MarkIn(slot))).ToList();
        var deviceMarks = marks[..count].Select(Marked).ToList();
        Assert.Equal(
            (surcharges.SlotsMarked, surcharges.MarksLaid, town.ReroutesTaken - reroutedBefore, string.Join(" ", townMarks)),
            (count, laid, (long)read[(cars.Count * onTheDevice.Stride) + 1], string.Join(" ", deviceMarks)));
    }

    static string Marked((int Link, float PriceM, float UntilS) mark) => $"{mark.Link}:{Bits(mark.PriceM):X8}:{Bits(mark.UntilS):X8}";

    [Fact]
    public void TheFreeBayNearACarIsTheTowns()
    {
        var town = warmed.Town;
        var cars = Enumerable.Range(0, town.Cars.Count).ToList();
        using var onTheDevice = new LegsOnTheDevice(device, town, Listed(cars));
        var read = onTheDevice.Run(LegsOnTheDevice.FreeBayNear);

        AssertWordsAlike(read, onTheDevice.Stride, cars, [LegsOnTheDevice.Answer, LegsOnTheDevice.Answer + 1], row =>
        {
            var atM = town.Cars.PositionM[cars[row]];
            return Words(
                (LegsOnTheDevice.Answer, (uint)town.FreeBayNear(atM, Config.PersonWalkWorthM)),
                (LegsOnTheDevice.Answer + 1, (uint)town.FreeBayNear(atM, Config.PersonWalkWorthM, town.Cars.DestinationM[cars[row]])));
        });
    }

    /// <summary>
    /// <b>Bays are claimed and given back as the town's register does</b> (ParkingRegistry.Claim, Release): every car of a
    /// town just stood asks for a bay — some of them the same one, some taken — then every second one gives its claim back,
    /// and each claim's answer, each car's bay and each bay's claimant are the town's.
    /// </summary>
    [Fact]
    public void BaysAreClaimedAndGivenBackAsTheTownsRegisterDoes()
    {
        using var town = new TownWorld(Towns.Built, Config);
        var parking = town.Parking;
        var listed = Enumerable.Range(0, town.Cars.Count).Select(car => (car, ((7 * car) + 3) % parking.BayCount)).ToList();
        using var onTheDevice = new LegsOnTheDevice(device, town, listed);
        var read = onTheDevice.Run(LegsOnTheDevice.Claims);
        var claimed = listed.Select(asked => parking.Claim(asked.car, asked.Item2)).ToList();
        for (var row = 1; row < listed.Count; row += 2) parking.Release(listed[row].car);

        var cars = listed.Select(asked => asked.car).ToList();
        AssertWordsAlike(read, onTheDevice.Stride, cars, [1, 20, LegsOnTheDevice.Answer], row => Words(
            (1, (uint)parking.Kept.ClaimedBy[listed[row].Item2]), (20, (uint)parking.ClaimedBayOf(listed[row].car)),
            (LegsOnTheDevice.Answer, claimed[row] ? 1u : 0u)));
    }

    /// <summary>
    /// <b>The lane nearest a car is the town's</b> (RoadGraph's lane indexes, ChainIndex.Nearest): of every lane, and of the
    /// carriageway on the car's own level — and how far along it, to the bit.
    /// </summary>
    [Fact]
    public void TheLaneNearestACarIsTheTowns()
    {
        var town = warmed.Town;
        var cars = Enumerable.Range(0, town.Cars.Count).ToList();
        using var onTheDevice = new LegsOnTheDevice(device, town, Listed(cars));
        var read = onTheDevice.Run(LegsOnTheDevice.Nearest);

        AssertWordsAlike(read, onTheDevice.Stride, cars, [0, LegsOnTheDevice.Answer, LegsOnTheDevice.Answer + 1, LegsOnTheDevice.Answer + 2], row =>
        {
            var atM = town.Cars.PositionM[cars[row]];
            var lane = town.Roads.NearestLane(atM, out var alongM);
            var street = town.Roads.NearestStreetLane(atM, town.Cars.LevelOf(cars[row]), out var streetAlongM);
            return Words((0, Bits(streetAlongM)), (LegsOnTheDevice.Answer, (uint)lane), (LegsOnTheDevice.Answer + 1, Bits(alongM)),
                (LegsOnTheDevice.Answer + 2, (uint)street));
        });
    }

    /// <summary>A town of this plan run until its owners are mid-route.</summary>
    static TownWorld Staged(CityPlan plan)
    {
        var town = new TownWorld(plan, Config);
        new SimLoop<TownWorld>(town, Config).Advance(WarmTicks);
        return town;
    }

    /// <summary>Every car driving its route's own line with a line in hand, which is what a laying of its line is asked of.</summary>
    static List<int> RouteDrivers(TownWorld town)
    {
        var cars = town.Cars;
        return Enumerable.Range(0, cars.Count).Where(car =>
            cars.Driven[car] && !cars.Broken[car] && cars.Line[car].LaneCount > 0
            && cars.Action[car] is CarAction.Follow or CarAction.Overtake or CarAction.Switch or CarAction.BackUp).ToList();
    }

    static List<(int Car, int Argument)> Listed(IEnumerable<int> cars) => cars.Select(car => (car, 0)).ToList();

    static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);

    /// <summary>A row of words, as many as the furthest named.</summary>
    static uint[] Words(params (int Word, uint Value)[] words)
    {
        var row = new uint[words.Max(word => word.Word) + 1];
        foreach (var (word, value) in words) row[word] = value;
        return row;
    }

    static void AssertRowsAlike(uint[] read, int stride, List<int> cars, Func<int, uint[]> expected)
    {
        Assert.True(cars.Count > 0, "the staged town gave the probe no car to ask");
        var unlike = LegsOnTheDevice.Unlike(read, stride, Enumerable.Range(0, cars.Count).ToList(), expected);
        Assert.True(unlike.Count == 0, $"{cars.Count} cars asked: {string.Join("; ", unlike)}");
    }

    static void AssertWordsAlike(uint[] read, int stride, List<int> cars, int[] words, Func<int, uint[]> expected)
    {
        Assert.True(cars.Count > 0, "the staged town gave the probe no car to ask");
        var unlike = new List<string>();
        for (var row = 0; row < cars.Count && unlike.Count < 8; row++)
        {
            var wanted = expected(row);
            foreach (var word in words)
            {
                var got = read[(row * stride) + word];
                if (got != wanted[word]) unlike.Add($"car {cars[row]} word {word}: device {got:X8}, town {wanted[word]:X8}");
            }
        }

        Assert.True(unlike.Count == 0, $"{cars.Count} cars asked: {string.Join("; ", unlike)}");
    }
}
