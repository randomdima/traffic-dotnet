using System.Net.WebSockets;
using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Multiplayer.Server;
using TrafficSimulation.Multiplayer.Wire;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Multiplayer.Server;

/// <summary>
/// <b>A town served over a real socket to a player in the same process</b>: stood up as somebody, sent the movers of its
/// area as the town stands them, never sent one outside it, walked by its keys, and taken offstage when it goes.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P2)]
public class TownServerTests
{
    /// <summary>A view narrow enough that the fixture's movers are not all in one player's area.</summary>
    static SimConfig Narrow() => new() { Multiplayer = new MultiplayerFigures { WidestViewM = 16f, MostPanM = 0f } };

    /// <summary>MP-4, MP-7: the movers of a player's snapshots stand where the town stood them at that tick.</summary>
    [Fact]
    public async Task APlayerIsSentItsAreaAsTheTownStandsIt()
    {
        using var served = new Served(SimConfig.Shipped());
        using var player = await TestPlayer.Join(served.Server.Port);

        served.TickUntil(() => player.Count >= 10);

        var shape = player.Welcome.Shape;
        foreach (var (header, movers) in player.Received())
        {
            Assert.True(header.OwnMover >= 0, "a snapshot came before the player was anybody");
            var stood = served.MoversAt[header.ServerTick];
            foreach (var mover in movers)
            {
                if (shape.Movers.KindOf(mover.Id) == MoverKind.Wagon) continue;

                var sentM = shape.FromPosition(mover.X, mover.Y);
                var atM = Vector2.Clamp(stood[mover.Id], Vector2.Zero, player.Welcome.WorldSizeM);
                Assert.True(Vector2.Distance(sentM, atM) <= shape.PositionQuantumM, $"mover {mover.Id} sent {Vector2.Distance(sentM, atM)} m off");
            }
        }
    }

    /// <summary>MP-14: <b>a player that acknowledges nothing is sent as many snapshots as it may run ahead by, and then none</b>.</summary>
    [Fact]
    public async Task APlayerThatAcknowledgesNothingIsSentNoMoreThanItMayRunAheadBy()
    {
        var config = SimConfig.Shipped();
        var mostAhead = config.Multiplayer.MostSnapshotsUnacknowledged;
        using var served = new Served(config);
        using var player = await TestPlayer.Join(served.Server.Port, acknowledges: false);
        served.TickUntil(() => player.Count >= mostAhead);

        var ticks = 0;
        served.TickUntil(() => ++ticks > 4 * mostAhead * config.Multiplayer.TicksPerSnapshot);

        Assert.Equal(mostAhead, player.Count);
    }

    /// <summary>MP-1: <b>no snapshot carries a mover further from the player than its area lets go at</b>.</summary>
    [Fact]
    public async Task NothingOutsideAPlayersAreaIsSent()
    {
        var config = Narrow();
        using var served = new Served(config);
        using var player = await TestPlayer.Join(served.Server.Port);

        served.TickUntil(() => player.Count >= 30);

        var shape = player.Welcome.Shape;
        var letGoM = config.PlayerLeaveHalfSpanM + shape.PositionQuantumM;
        var outside = 0;
        foreach (var (header, movers) in player.Received())
        {
            var stood = served.MoversAt[header.ServerTick];
            var ownM = stood[header.OwnMover];
            foreach (var atM in stood)
            {
                if (Chebyshev(atM, ownM) > letGoM) outside++;
            }

            foreach (var mover in movers)
            {
                var off = Chebyshev(shape.FromPosition(mover.X, mover.Y), shape.FromPosition(shape.ToPosition(ownM.X), shape.ToPosition(ownM.Y)));
                Assert.True(off <= letGoM, $"mover {mover.Id} sent {off} m from the player, past {letGoM} m");
            }
        }

        Assert.True(outside > 0, "the fixture stood every mover inside the area, so nothing was asked");
    }

    /// <summary>MP-3, MP-4: <b>a player's forward key walks its body up the screen</b>, which is north.</summary>
    [Fact]
    public async Task APlayersForwardKeyWalksItsBodyNorth()
    {
        using var served = new Served(SimConfig.Shipped());
        using var player = await TestPlayer.Join(served.Server.Port);
        served.TickUntil(() => player.Count >= 1);
        var body = served.World.BodyOfSeat(0);
        var fromM = served.World.People.PositionM[body];

        await player.Send(throttle: 1f, steer: 0f, handbrake: false);
        served.TickUntil(() => fromM.Y - served.World.People.PositionM[body].Y > 1f);

        var movedM = served.World.People.PositionM[body] - fromM;
        Assert.True(-movedM.Y > 1f && MathF.Abs(movedM.X) < -movedM.Y, $"the body moved {movedM} m");
    }

    /// <summary>MP-12: <b>a player's body is sent as its seat's</b>, and every other mover as nobody's.</summary>
    [Fact]
    public async Task APlayersBodyIsSentAsItsSeats()
    {
        using var served = new Served(SimConfig.Shipped());
        using var player = await TestPlayer.Join(served.Server.Port);
        served.TickUntil(() => player.Count >= 1);
        var seat = 0;
        var body = player.Welcome.Movers.OfPerson(served.World.BodyOfSeat(seat));

        var (_, movers) = player.Received().Last();

        var held = movers.Where(mover => mover.Driver != 0).Select(mover => (mover.Id, (int)mover.Driver));
        Assert.Equal([(body, seat + 1)], held);
    }

    /// <summary>MP-15: <b>a player is told its own seat's card</b>, its name as it said it and its grey made vivid.</summary>
    [Fact]
    public async Task APlayerIsToldItsOwnCardItsColourMadeVivid()
    {
        var config = SimConfig.Shipped();
        using var served = new Served(config);
        using var player = await TestPlayer.Join(served.Server.Port, card: new SeatCard(0x808080, "Jane Doe"));

        served.TickUntil(() => player.Card(0, out _));

        var figures = config.Multiplayer;
        Assert.True(player.Card(0, out var card));
        Assert.Equal(new SeatCard(SeatColour.Vivid(0x808080, figures.SeatLeastSaturation, figures.SeatLeastValue), "Jane Doe"), card);
    }

    /// <summary>
    /// MP-15, MP-1: <b>a player is told nothing of a seat whose player stands outside its area</b>, and that seat's card
    /// once its player is stood inside it.
    /// </summary>
    [Fact]
    public async Task APlayerIsToldAnotherSeatsCardOnlyOnceItIsInItsArea()
    {
        var config = Narrow();
        using var served = new Served(config);
        var world = served.World;
        using var far = await TestPlayer.Join(served.Server.Port, card: new SeatCard(0x00C000, "Jane Doe"));
        served.TickUntil(() => far.Count >= 1);
        world.HoldAgents = true;
        var middleM = world.People.PositionM[world.BodyOfSeat(0)];
        var awayM = Enumerable.Range(0, world.Cars.Count).Select(car => world.Cars.PositionM[car])
            .First(atM => Chebyshev(atM, middleM) > 3f * config.PlayerLeaveHalfSpanM);
        Assert.True(world.SpawnPlayer(0, awayM), "nowhere free on the pavement far off");

        using var near = await TestPlayer.Join(served.Server.Port);
        served.TickUntil(() => near.Count >= 10);
        Assert.False(near.Card(0, out _), "told of a seat outside the area");

        Assert.True(world.SpawnPlayer(0, world.People.PositionM[world.BodyOfSeat(1)]), "nowhere free on the pavement by the player");
        served.TickUntil(() => near.Card(0, out _));

        Assert.True(near.Card(0, out var card));
        Assert.Equal("Jane Doe", card.Name);
    }

    /// <summary>
    /// MP-13: <b>a press beside a car is sent as the car, and a press in it as the body beside it</b> — the player's own
    /// mover the one each snapshot says, and that mover in the snapshot.
    /// </summary>
    [Fact]
    public async Task APlayerGotInAndOutIsSentAsTheCarAndThenAsItsBody()
    {
        using var served = new Served(SimConfig.Shipped());
        using var player = await TestPlayer.Join(served.Server.Port);
        served.TickUntil(() => player.Count >= 1);
        var world = served.World;
        world.HoldAgents = true;
        var car = Enumerable.Range(0, world.Cars.Count).First(car => !world.Cars.Broken[car]);
        Assert.True(world.SpawnPlayer(0, world.Cars.PositionM[car]), "nowhere free on the pavement by the car");
        var body = world.BodyOfSeat(0);
        var movers = player.Welcome.Movers;

        await player.Act();
        served.TickUntil(() => player.Received().Last().Header.OwnMover == movers.OfCar(car));
        Assert.Equal(movers.OfCar(car), player.Received().Last().Header.OwnMover);

        await player.Act();
        served.TickUntil(() => player.Received().Last().Header.OwnMover == movers.OfPerson(body));

        var (header, sent) = player.Received().Last();
        Assert.Equal(movers.OfPerson(body), header.OwnMover);
        Assert.Contains(sent, mover => mover.Id == movers.OfPerson(body));
    }

    /// <summary>MP-4: a player gone is taken offstage, and its seat is nobody.</summary>
    [Fact]
    public async Task APlayerGoneIsTakenOffstage()
    {
        using var served = new Served(SimConfig.Shipped());
        var player = await TestPlayer.Join(served.Server.Port);
        served.TickUntil(() => player.Count >= 1);
        var body = served.World.BodyOfSeat(0);

        player.Dispose();
        served.TickUntil(() => served.World.BodyOfSeat(0) < 0);

        Assert.Equal(-1, served.World.BodyOfSeat(0));
        Assert.Equal(ContainerKind.Offstage, served.World.People.Inside[body].Kind);
        Assert.Equal(0, served.Server.Seated);
    }

    /// <summary>MP-8: <b>a seated player's ticks allocate nothing on the town's thread</b> — its keys applied, the town laid down.</summary>
    [Fact]
    public async Task ServingAPlayerAllocatesNothingOnTheTownsThread()
    {
        using var served = new Served(SimConfig.Shipped());
        using var player = await TestPlayer.Join(served.Server.Port);
        // Walked for a few seconds first: the world's thread grows its pools once, the first time a body under a hand walks.
        await player.Send(throttle: 0.5f, steer: 0.2f, handbrake: false);
        served.TickUntil(() => player.Count >= 60);

        var before = GC.GetAllocatedBytesForCurrentThread() + served.World.AllocatedAsideBytes;
        for (var i = 0; i < 120; i++)
        {
            served.Server.BeforeTick(served.World, served.Loop.Tick);
            served.Loop.Advance();
            served.Server.AfterTick(served.World, served.Loop.Tick);
        }

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread() + served.World.AllocatedAsideBytes);
    }

    /// <summary>MP-11, MP-15: <b>a bare address is the page itself</b>, marked as a town's so it opens on the lobby.</summary>
    [Fact]
    public async Task ABareAddressIsThePageMarkedAsATownsOwn()
    {
        using var page = new Page();
        using var served = new Served(SimConfig.Shipped(), page.Host);
        using var http = Http();

        var answer = await http.GetAsync($"http://127.0.0.1:{served.Server.Port}/");

        Assert.Equal(
            ("<html>town</html>", true),
            (await answer.Content.ReadAsStringAsync(), answer.Headers.Contains(PageHost.ServedTown)));
    }

    /// <summary>MP-11: a file of the page is handed out as it is published, and its brotli copy in its place where taken.</summary>
    [Fact]
    public async Task APageFileIsHandedOutAndItsBrotliCopyInItsPlace()
    {
        using var page = new Page();
        using var served = new Served(SimConfig.Shipped(), page.Host);
        using var http = Http();
        var at = $"http://127.0.0.1:{served.Server.Port}";

        var plain = await http.GetAsync($"{at}/index.html?join=x");
        var request = new HttpRequestMessage(HttpMethod.Get, $"{at}/_framework/dotnet.wasm");
        request.Headers.AcceptEncoding.ParseAdd("br");
        var squeezed = await http.SendAsync(request);

        Assert.Equal("<html>town</html>", await plain.Content.ReadAsStringAsync());
        Assert.Equal("br", Assert.Single(squeezed.Content.Headers.ContentEncoding));
        Assert.Equal("application/wasm", squeezed.Content.Headers.ContentType?.MediaType);
        Assert.Equal([9, 9], await squeezed.Content.ReadAsByteArrayAsync());
    }

    /// <summary>MP-11: <b>a file asked for again with the tag it was handed out under is not sent again</b>.</summary>
    [Fact]
    public async Task AFileAskedForAgainWithItsTagIsNotSentAgain()
    {
        using var page = new Page();
        using var served = new Served(SimConfig.Shipped(), page.Host);
        using var http = Http();
        var at = $"http://127.0.0.1:{served.Server.Port}/index.html?join=x";
        var first = await http.GetAsync(at);
        var again = new HttpRequestMessage(HttpMethod.Get, at);
        again.Headers.IfNoneMatch.Add(first.Headers.ETag!);

        var answer = await http.SendAsync(again);

        Assert.Equal(System.Net.HttpStatusCode.NotModified, answer.StatusCode);
    }

    /// <summary>MP-11: <b>a framework file named by its contents' hash is kept for good</b>, and the page itself is asked about again.</summary>
    [Fact]
    public async Task OnlyAFileNamedByItsHashIsKeptForGood()
    {
        using var page = new Page();
        using var served = new Served(SimConfig.Shipped(), page.Host);
        using var http = Http();
        var at = $"http://127.0.0.1:{served.Server.Port}";

        var hashed = await http.GetAsync($"{at}/_framework/System.Linq.wb2117b3pc.wasm");
        var named = await http.GetAsync($"{at}/_framework/dotnet.wasm");

        Assert.True(hashed.Headers.CacheControl?.Extensions.Any(each => each.Name == "immutable"));
        Assert.True(named.Headers.CacheControl?.NoCache);
    }

    /// <summary>MP-11: <b>nothing outside the published folder is handed out</b>.</summary>
    [Fact]
    public async Task NothingOutsideThePageIsHandedOut()
    {
        using var page = new Page();
        using var served = new Served(SimConfig.Shipped(), page.Host);

        // Written by hand: an HTTP client folds the dots away before it sends anything.
        using var client = new System.Net.Sockets.TcpClient("127.0.0.1", served.Server.Port);
        var stream = client.GetStream();
        await stream.WriteAsync("GET /../secret.txt HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n"u8.ToArray());
        var answer = await new StreamReader(stream).ReadLineAsync();

        Assert.True(File.Exists(Path.Combine(page.Root, "..", "secret.txt")));
        Assert.Equal("HTTP/1.1 404 Not Found", answer);
    }

    static HttpClient Http() =>
        new(new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = System.Net.DecompressionMethods.None });

    /// <summary>A published page of three files under <c>.tmp/</c>, and a file beside it that is not the page's.</summary>
    sealed class Page : IDisposable
    {
        readonly string _beside = Path.Combine(
            TrafficSimulation.Core.Config.ProjectPaths.Root, ".tmp", "tests", "page-" + Guid.NewGuid().ToString("N"));

        public Page()
        {
            Root = Path.Combine(_beside, "wwwroot");
            Directory.CreateDirectory(Path.Combine(Root, "_framework"));
            File.WriteAllText(Path.Combine(Root, "index.html"), "<html>town</html>");
            File.WriteAllBytes(Path.Combine(Root, "_framework", "dotnet.wasm"), [1, 2, 3]);
            File.WriteAllBytes(Path.Combine(Root, "_framework", "dotnet.wasm.br"), [9, 9]);
            File.WriteAllBytes(Path.Combine(Root, "_framework", "System.Linq.wb2117b3pc.wasm"), [4, 5]);
            File.WriteAllText(Path.Combine(_beside, "secret.txt"), "not the page's");
            Host = new PageHost(Root, SimConfig.Shipped().Multiplayer.PageIdleS);
        }

        public string Root { get; }

        public PageHost Host { get; }

        public void Dispose() => Directory.Delete(_beside, recursive: true);
    }

    static float Chebyshev(Vector2 a, Vector2 b) => MathF.Max(MathF.Abs(a.X - b.X), MathF.Abs(a.Y - b.Y));

    /// <summary>The fixture served on a port of the system's choosing, ticked by the test, every car's and walker's place kept by tick.</summary>
    sealed class Served : IDisposable
    {
        const int MostTicks = 3_000;

        public Served(SimConfig config, PageHost? page = null)
        {
            World = new TownWorld(Towns.Of(Towns.Fixture), config, served: true);
            Loop = new SimLoop<TownWorld>(World, config);
            Server = new TownServer(World, Towns.Of(Towns.Fixture), Towns.Fixture, config, port: 0, page);
            Server.Start();
        }

        public TownWorld World { get; }

        public SimLoop<TownWorld> Loop { get; }

        public TownServer Server { get; }

        /// <summary>Where each car and then each walker stood after a tick, by mover number.</summary>
        public Dictionary<uint, Vector2[]> MoversAt { get; } = [];

        /// <summary>Ticks until the condition holds, a millisecond a tick so the sockets keep up, or the bound.</summary>
        public void TickUntil(Func<bool> done)
        {
            for (var i = 0; i < MostTicks && !done(); i++)
            {
                Server.BeforeTick(World, Loop.Tick);
                Loop.Advance();
                MoversAt[(uint)Loop.Tick] = [.. World.Cars.PositionM[..World.Cars.Count], .. World.People.PositionM[..World.People.Count]];
                Server.AfterTick(World, Loop.Tick);
                Thread.Sleep(1);
            }
        }

        public void Dispose()
        {
            Server.Dispose();
            World.Dispose();
        }
    }

    /// <summary>A player as a page is one: hello, the welcome, ready, then snapshots read against what it holds.</summary>
    sealed class TestPlayer : IDisposable
    {
        readonly ClientWebSocket _socket = new();
        readonly List<(SnapshotHeader, MoverState[])> _received = [];
        ReceivedSnapshots _snapshots = null!;
        uint _sequence;

        public Welcome Welcome { get; private set; } = null!;

        public int Count
        {
            get
            {
                lock (_received) return _received.Count;
            }
        }

        public (SnapshotHeader Header, MoverState[] Movers)[] Received()
        {
            lock (_received) return [.. _received];
        }

        /// <summary>Whether it acknowledges each snapshot as it reads it, as a page does with its next keys.</summary>
        bool _acknowledges = true;

        /// <summary>Who the server has said <paramref name="seat"/>'s player is, so far (MP-15).</summary>
        public bool Card(int seat, out SeatCard card)
        {
            lock (_received) return _snapshots.Card(seat, out card);
        }

        public static async Task<TestPlayer> Join(int port, bool acknowledges = true, SeatCard? card = null)
        {
            var player = new TestPlayer { _acknowledges = acknowledges };
            await player._socket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/"), CancellationToken.None);
            var buffer = new byte[64 * 1024];
            await player.SendRaw(buffer, Protocol.WriteHello(buffer, card ?? new SeatCard(0xFF0000, "John Smith")));
            var length = await Player.ReadMessage(player._socket, buffer, CancellationToken.None);
            player.Welcome = Welcome.Read(buffer.AsSpan(0, length)) ?? throw new InvalidOperationException("no welcome");
            player._snapshots = new ReceivedSnapshots(player.Welcome.Shape, kept: 32, mostMovers: 1024);
            await player.SendRaw(buffer, Protocol.WriteReady(buffer));
            _ = Task.Run(player.Listen);
            return player;
        }

        /// <summary>Keys held from now on: every input after this one says them again, as a page's frames do.</summary>
        public Task Send(float throttle, float steer, bool handbrake)
        {
            _held = (throttle, steer, handbrake);
            return SendHeld();
        }

        (float Throttle, float Steer, bool Handbrake) _held;

        /// <summary>A press of the way in and out (MP-13), counted as a page counts it and sent with the keys.</summary>
        public Task Act()
        {
            _acts++;
            return SendHeld();
        }

        byte _acts;

        readonly SemaphoreSlim _sending = new(1);

        async Task SendHeld()
        {
            var buffer = new byte[32];
            await _sending.WaitAsync();
            try
            {
                var input = new InputMessage(++_sequence, _snapshots.Newest, _held.Throttle, _held.Steer, _held.Handbrake, _acts);
                await SendRaw(buffer, input.Write(buffer));
            }
            finally
            {
                _sending.Release();
            }
        }

        Task SendRaw(byte[] buffer, int length) =>
            _socket.SendAsync(buffer.AsMemory(0, length), WebSocketMessageType.Binary, true, CancellationToken.None).AsTask();

        async Task Listen()
        {
            var buffer = new byte[64 * 1024];
            try
            {
                while (_socket.State == WebSocketState.Open)
                {
                    var length = await Player.ReadMessage(_socket, buffer, CancellationToken.None);
                    if (length < 0) continue;

                    lock (_received)
                    {
                        if (!_snapshots.Take(buffer.AsSpan(0, length))) continue;

                        var held = _snapshots.Find(_snapshots.Newest)!;
                        _received.Add((held.Header, held.Held.ToArray()));
                    }

                    // Acknowledged at once, as a page acknowledges with its next frame's keys.
                    if (_acknowledges) await SendHeld();
                }
            }
            catch (WebSocketException)
            {
            }
        }

        public void Dispose() => _socket.Dispose();
    }
}
