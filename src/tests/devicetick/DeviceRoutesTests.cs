using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Bench;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Routing;
using TrafficSimulation.World.Town;
using Xunit;
using Ask = TrafficSimulation.Bench.RouteAsks.Ask;
using Network = TrafficSimulation.App.DeviceTick.DeviceRoutes.Network;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>The town's route searches on the device, held to the town's own</b> (<see cref="DeviceRoutes"/>): seeded requests over
/// both networks, made the way the town makes them (<see cref="RouteAsks"/>), each answered on the device and by
/// <see cref="RouteSearch"/> over the same cells — the same links, as many, the same goal, stopping short or searched over
/// the whole graph alike, as many links settled, and the cost to the bit.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class DeviceRoutesTests(RoutesOnTheDevice routes) : IClassFixture<RoutesOnTheDevice>
{
    /// <summary>How many requests a case asks of a network: three batches of slots, every way a search ends among them.</summary>
    const int Asked = 3 * DeviceRoutes.Slots;

    /// <summary>
    /// How many driving requests the suite's city is asked for the few whose window settles nothing: about one in five
    /// hundred does.
    /// </summary>
    const int AskedForAFallBack = 4_000;

    /// <summary>How many of a case's routes have a link of theirs priced up or closed.</summary>
    const int Marked = 48;

    /// <summary>The clock the marks are read at, by which some of them are spent.</summary>
    const float MarksReadAtS = 20f;

    [Fact]
    public void EveryDrivingSearchAnswersAsTheTownsOwn()
    {
        var town = routes.Built;
        var held = routes.Held(town, town.Asks.Make(Network.Driving, Asked, seed: 1), town.Asks.Unmarked());
        Assert.True(held.Kept, held.Said);
    }

    [Fact]
    public void EveryWalkingSearchAnswersAsTheTownsOwn()
    {
        var town = routes.Built;
        var held = routes.Held(town, town.Asks.Make(Network.Walking, Asked, seed: 2), town.Asks.Unmarked());
        Assert.True(held.Kept, held.Said);
    }

    /// <summary>
    /// <b>A search across ways priced up answers as the town's does</b>: a link of each of many routes marked — some marks
    /// spent by the clock the search reads them at, a second mark on a link the dearer answering — and a window holding one
    /// searched over the whole graph instead. Both networks in one batch, so a slot serves either in turn.
    /// </summary>
    [Fact]
    public void ASearchOverPricedUpWaysAnswersAsTheTownsOwn()
    {
        var town = routes.Built;
        var asked = Both(town, seed: 3);
        var marks = town.Asks.Unmarked();
        var random = new Random(5);
        marks.Drive.Advance(0f);
        marks.Walk.Advance(0f);
        foreach (var ask in FirstOfEach(asked))
        {
            var link = town.MiddleLinkOf(ask);
            if (link == TravelGraph.NoLink) continue;

            var surcharges = marks.Of(ask.Network);
            surcharges.Mark(link, PriceM(random), LifeS(random));
            if (random.Next(4) == 0) surcharges.Mark(link, PriceM(random), LifeS(random));
        }

        marks.Drive.Advance(MarksReadAtS);
        marks.Walk.Advance(MarksReadAtS);

        var held = routes.Held(town, asked, marks);
        Assert.True(held.Kept, held.Said);
    }

    /// <summary>
    /// <b>A search barred from closed links answers as the town's does</b> (SRV-10): a link of each of many routes closed,
    /// and every other request asked as a car on a call asks, with nothing closed to it.
    /// </summary>
    [Fact]
    public void ASearchBarredFromClosedLinksAnswersAsTheTownsOwn()
    {
        var town = routes.Built;
        var asked = Both(town, seed: 6);
        var marks = town.Asks.Unmarked();
        foreach (var ask in FirstOfEach(asked))
        {
            var link = town.MiddleLinkOf(ask);
            if (link != TravelGraph.NoLink) marks.ClosedOf(ask.Network)[link] = true;
        }

        var barred = asked.Select((ask, at) => ask with { Closed = at % 2 == 0 }).ToList();
        var held = routes.Held(town, barred, marks);
        Assert.True(held.Kept, held.Said);
    }

    /// <summary>
    /// <b>A window that settles nothing, a ring wider too, is searched again over the whole graph as the town's is</b> —
    /// every driving request of the suite's city whose own search fell back that way.
    /// </summary>
    [Fact]
    public void AWindowThatSettlesNothingFallsBackToTheWholeGraphAsTheTownsOwnDoes()
    {
        var town = routes.City;
        var marks = town.Asks.Unmarked();
        var fellBack = town.Asks.Make(Network.Driving, AskedForAFallBack, seed: 8)
            .Where(ask => town.Asks.Searched(ask, marks, out _, out _).OverTheWholeGraph).ToList();
        Assert.NotEmpty(fellBack);

        var held = routes.Held(town, fellBack, marks);
        Assert.True(held.Kept, held.Said);
    }

    /// <summary>
    /// <b>Only a pending slot is searched</b>: the one asked is answered, its asker and stamp echoed, and marked done; every
    /// other is left empty and unanswered.
    /// </summary>
    [Fact]
    public void OnlyAPendingSlotIsSearched()
    {
        const int Slot = 5;
        const int Owner = 17;
        const uint Stamp = 0xC0FFEEu;
        using var town = new RoutesOnTheDevice.Town(routes.Device, Towns.Of(Towns.Fixture));
        var ask = town.Asks.Make(Network.Driving, 1, seed: 9)[0];

        town.Routes.Ask(Slot, ask.Network, ask.Entries, ask.Goals, closed: false, nowS: 0f, Owner, Stamp);
        routes.Run(town);

        var answer = town.Routes.Answer(Slot);
        Assert.Equal((DeviceRoutes.Done, (uint)Owner, Stamp), (town.Routes.StateOf(Slot), answer.Owner, answer.Stamp));
        for (var slot = 0; slot < DeviceRoutes.Slots; slot++)
        {
            if (slot != Slot) Assert.Equal((DeviceRoutes.Empty, default(DeviceRoutes.ResultRow)), (town.Routes.StateOf(slot), town.Routes.Answer(slot)));
        }
    }

    static float PriceM(Random random) => 50f + ((float)random.NextDouble() * 950f);

    static float LifeS(Random random) => 5f + ((float)random.NextDouble() * 55f);

    /// <summary>Both networks' requests taken in turn, so every batch holds both.</summary>
    static List<Ask> Both(RoutesOnTheDevice.Town town, int seed)
    {
        var driving = town.Asks.Make(Network.Driving, Asked, seed);
        var walking = town.Asks.Make(Network.Walking, Asked, seed + 1);
        return [.. driving.Zip(walking).SelectMany(pair => new[] { pair.First, pair.Second })];
    }

    /// <summary>The first of each network's requests, as many as a case marks of each.</summary>
    static IEnumerable<Ask> FirstOfEach(List<Ask> asked) =>
        asked.Where(ask => ask.Network == Network.Driving).Take(Marked).Concat(asked.Where(ask => ask.Network == Network.Walking).Take(Marked));
}

/// <summary>
/// <b>The suite's towns' networks laid on the device</b> — the walked town, which has car parks and pavements, and the
/// suite's city, some of whose driving windows settle nothing — each with the town's own searches beside it, and a run of
/// requests asked of both: a batch of slots at a time on the device, one at a time on the host.
/// </summary>
public sealed class RoutesOnTheDevice : IDisposable
{
    /// <summary>What a run of requests came to: how many, and the first few the device answered differently.</summary>
    internal sealed class Answers
    {
        public readonly List<string> Differ = [];

        public int Count;

        public bool Kept => Differ.Count == 0;

        public string Said => $"{Differ.Count} of {Count} answered differently: {string.Join("; ", Differ.Take(6))}";
    }

    /// <summary>One town stood up, its networks laid on the device in a root of their own, and its requests made.</summary>
    internal sealed class Town : IDisposable
    {
        readonly TownRoot _root;

        public Town(Lavapipe device, CityPlan plan)
        {
            World = new TownWorld(plan, Config);
            _root = new TownRoot(device.Vk);
            Routes = DeviceRoutes.Of(device.Vk, _root, World);
            Asks = new RouteAsks(World, Config);
        }

        public TownWorld World { get; }

        public DeviceRoutes Routes { get; }

        public RouteAsks Asks { get; }

        /// <summary>The link halfway down the route the town finds unpriced and unbarred, or none where it finds none.</summary>
        public int MiddleLinkOf(Ask ask)
        {
            var search = Asks.Searched(ask, Asks.Unmarked(), out var count, out _);
            return count == 0 ? TravelGraph.NoLink : search.Links(count)[count / 2];
        }

        public void Dispose()
        {
            Routes.Dispose();
            _root.Dispose();
            World.Dispose();
        }
    }

    static readonly SimConfig Config = SimConfig.Shipped();

    readonly ComputeBatch _batch;

    public RoutesOnTheDevice()
    {
        _batch = new ComputeBatch(Device.Vk);
        Built = new Town(Device, Towns.Built);
        City = new Town(Device, Towns.Of(Towns.City));
    }

    internal Lavapipe Device { get; } = new();

    /// <summary>The walked town: car parks, bays and pavements.</summary>
    internal Town Built { get; }

    /// <summary>The suite's city, some of whose driving windows settle nothing.</summary>
    internal Town City { get; }

    /// <summary>Every pending slot of a town's searched, and waited for.</summary>
    internal void Run(Town town)
    {
        town.Routes.Record(_batch.Begin());
        _batch.SubmitAndWait();
    }

    /// <summary>
    /// <b>Every request asked of the device and of the town's own search</b>, under the same surcharges and closed links,
    /// and every answer held to the other's.
    /// </summary>
    internal Answers Held(Town town, IReadOnlyList<Ask> asked, RouteAsks.Marks marks)
    {
        var routes = town.Routes;
        routes.LaySurcharges(Network.Driving, marks.Drive);
        routes.LaySurcharges(Network.Walking, marks.Walk);
        routes.LayClosed(Network.Driving, marks.DriveClosed);
        routes.LayClosed(Network.Walking, marks.WalkClosed);

        var answers = new Answers();
        for (var first = 0; first < asked.Count; first += DeviceRoutes.Slots)
        {
            var batch = Math.Min(DeviceRoutes.Slots, asked.Count - first);
            for (var slot = 0; slot < batch; slot++)
            {
                var ask = asked[first + slot];
                routes.Ask(slot, ask.Network, ask.Entries, ask.Goals, ask.Closed, marks.Of(ask.Network).NowS, first + slot, (uint)(first + slot));
            }

            Run(town);
            for (var slot = 0; slot < batch; slot++)
            {
                answers.Count++;
                var differs = town.Asks.Differs(asked[first + slot], marks, routes, slot, first + slot, (uint)(first + slot));
                if (differs is not null) answers.Differ.Add(differs);
            }
        }

        return answers;
    }

    public void Dispose()
    {
        _batch.Dispose();
        Built.Dispose();
        City.Dispose();
        Device.Dispose();
    }
}
