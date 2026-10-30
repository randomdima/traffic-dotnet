using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Multiplayer.Wire;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Multiplayer.Server;

/// <summary>What every player of one served town is told and measured by, fixed when the town is opened.</summary>
internal sealed class ServedTown(Welcome welcome, SimConfig config)
{
    public Welcome Welcome { get; } = welcome;

    public WireShape Shape { get; } = welcome.Shape;

    /// <summary>Half the square round a player a mover is taken up inside, in position quanta (MP-2).</summary>
    public int TakeUpQuanta { get; } = (int)MathF.Ceiling(config.PlayerAreaHalfSpanM / welcome.PositionQuantumM);

    /// <summary>And half the square it has to leave before it is let go of.</summary>
    public int LetGoQuanta { get; } = (int)MathF.Ceiling(config.PlayerLeaveHalfSpanM / welcome.PositionQuantumM);

    /// <summary>How many snapshots a player may be sent past its acknowledgement (MP-14), held to what a header can name.</summary>
    public uint MostUnacknowledged { get; } =
        (uint)Math.Clamp(config.Multiplayer.MostSnapshotsUnacknowledged, 1, Math.Min(config.Multiplayer.BaselinesKept - 1, (int)SnapshotHeader.MostBaselineGap));
}

/// <summary>
/// <b>A town served to players</b> (MP-1…13): the listener that lets them in, the seats the town's thread stands their
/// bodies up and hands their keys from, and the thread that sends each its area.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three kinds of thread and one rule between them</b>: the town's thread calls <see cref="BeforeTick"/> and
/// <see cref="AfterTick"/> and nothing else, and allocates nothing in either (MP-8); every socket is read on the pool;
/// one thread of the server's own encodes and sends. What crosses is a player's own volatile fields and the capture's
/// swap — no queue, no lock held across work.
/// </para>
/// <para>
/// <b>A seat is the player's slot</b>: the town's seats are as many as <see cref="MultiplayerFigures.MostSeats"/>, its
/// bodies laid with it (<c>served</c>), and a served town carries no scripted driver to share them with.
/// </para>
/// </remarks>
internal sealed class TownServer : IDisposable
{
    readonly SimConfig _config;
    readonly PageHost? _page;
    readonly ServedTown _town;
    readonly TownCapture _capture;
    readonly Player?[] _players;
    readonly TcpListener _listener;
    readonly CancellationTokenSource _stop = new();
    readonly AutoResetEvent _published = new(false);
    readonly Thread _sender;
    readonly Vector2 _middleM;
    readonly long _holdTicks;
    readonly long _releaseTicks;
    readonly long _respawnTicks;

    /// <summary>Each seat's newest input applied, laid down with the capture; the town's thread's own.</summary>
    readonly uint[] _inputApplied;
    long _tick;

    /// <param name="page">The published page to hand out on the same port (MP-11), or null to answer players alone.</param>
    public TownServer(TownWorld world, CityPlan plan, string map, SimConfig config, int port, PageHost? page = null)
    {
        _config = config;
        _page = page;
        var figures = config.Multiplayer;
        var movers = new MoverIds(world.Cars.Count, world.People.Count, world.World.View.Wagons.Length);
        _town = new ServedTown(
            new Welcome
            {
                Map = map,
                PlanDigest = plan.Digest(),
                TickRateHz = config.Sim.TickRateHz,
                TicksPerSnapshot = figures.TicksPerSnapshot,
                ServerTick = 0,
                WorldSizeM = plan.WorldSizeM,
                Movers = movers,
                PositionQuantumM = figures.PositionQuantumM,
                AngleQuantumDeg = figures.AngleQuantumDeg,
                SmallAngleQuantumDeg = figures.SmallAngleQuantumDeg,
                SmallMoveM = config.PlayerAreaMarginM,
                WidestViewM = figures.WidestViewM,
                InterpolationDelayS = figures.InterpolationDelayS,
                LongestExtrapolationS = figures.LongestExtrapolationS,
                Seats = Math.Min(figures.MostSeats, world.Seats),
            },
            config);
        _capture = new TownCapture(movers, _town.Welcome.Seats);
        _players = new Player?[_town.Welcome.Seats];
        _inputApplied = new uint[_players.Length];
        _middleM = plan.WorldSizeM * 0.5f;
        _holdTicks = SecondsAsTicks(figures.HoldAfterSilenceS);
        _releaseTicks = SecondsAsTicks(figures.ReleaseAfterSilenceS);
        _respawnTicks = SecondsAsTicks(figures.RespawnS);
        _listener = new TcpListener(IPAddress.Any, port);
        _sender = new Thread(Send) { IsBackground = true, Name = "players' snapshots" };
    }

    /// <summary>The port it listens on, once started: the one asked for, or the one the system gave for nought.</summary>
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>How many players hold a seat.</summary>
    public int Seated
    {
        get
        {
            var seated = 0;
            foreach (var player in _players)
            {
                if (player is { Closed: false, Mover: >= 0 }) seated++;
            }

            return seated;
        }
    }

    /// <summary>The snapshots and their bytes sent to every player so far, those gone included; on the town's thread.</summary>
    public (long Snapshots, long Bytes) Sent
    {
        get
        {
            var (snapshots, bytes) = (_snapshotsOfThoseGone, _bytesOfThoseGone);
            foreach (var player in _players)
            {
                if (player is null) continue;

                snapshots += player.SnapshotsSent;
                bytes += player.BytesSent;
            }

            return (snapshots, bytes);
        }
    }

    long _snapshotsOfThoseGone;
    long _bytesOfThoseGone;

    public void Start()
    {
        _listener.Start();
        _sender.Start();
        _ = Task.Run(Admit);
    }

    long SecondsAsTicks(float seconds) => (long)MathF.Ceiling(seconds * _config.Sim.TickRateHz);

    /// <summary>
    /// <b>Every seat brought to this tick</b>, on the town's thread before it steps: a player gone is taken offstage, a
    /// player ready is stood up on the pavement, a silent one is held and then let go of, one down is stood up afresh, and
    /// every other is handed its keys and its presses to get in or out (MP-3, MP-4, MP-13).
    /// </summary>
    public void BeforeTick(TownWorld world, long tick)
    {
        Volatile.Write(ref _tick, tick);
        for (var seat = 0; seat < _players.Length; seat++)
        {
            var player = _players[seat];
            if (player is null) continue;

            if (player.Closed)
            {
                world.LetThePlayerGo(seat);
                Volatile.Write(ref _players[seat], null);
                _snapshotsOfThoseGone += player.SnapshotsSent;
                _bytesOfThoseGone += player.BytesSent;
                player.Dispose();
                continue;
            }

            if (!player.Ready) continue;

            var silence = tick - player.HeardAtTick;
            if (silence > _releaseTicks)
            {
                player.Close();
                continue;
            }

            if (world.BodyOfSeat(seat) < 0) world.SpawnPlayer(seat, _middleM);
            else StandUpWhenDown(world, player, seat, tick);

            var heard = player.Heard;
            var hand = silence > _holdTicks
                ? new HandInput(Held: true, Throttle: 0f, Steer: 0f, Handbrake: true, WalkDirection: Vector2.Zero)
                : new HandInput(Held: true, heard.Throttle, heard.Steer, heard.Handbrake, heard.WalkDirection, heard.Boost);
            world.HandOnPlayer(seat, hand);
            if (heard.Acts != player.ActsTaken)
            {
                player.ActsTaken = heard.Acts;
                world.PlayerActs(seat);
            }

            player.InputApplied = heard.Sequence;
            player.Mover = MoverOf(world, world.ShownAs(seat));
            player.State = world.IsDown(seat) ? SeatState.Down : SeatState.Playing;
        }
    }

    /// <summary>
    /// A player down: its body is the town's to come for, and after <c>RespawnS</c> the player is stood up afresh off the
    /// nearest hospital's door to where it fell, or the town's middle in a town with none (MP-4).
    /// </summary>
    void StandUpWhenDown(TownWorld world, Player player, int seat, long tick)
    {
        if (!world.IsDown(seat))
        {
            player.DownAtTick = -1;
            return;
        }

        if (player.DownAtTick < 0) player.DownAtTick = tick;
        if (tick - player.DownAtTick < _respawnTicks) return;

        if (world.SpawnPlayer(seat, NearestHospitalM(world, world.People.PositionM[world.BodyOfSeat(seat)]))) player.DownAtTick = -1;
    }

    Vector2 NearestHospitalM(TownWorld world, Vector2 fellM)
    {
        var nearestM = _middleM;
        var bestSq = float.MaxValue;
        foreach (var hospital in world.Hospitals.Buildings)
        {
            var atM = world.Plan.Buildings.CentreM[hospital];
            var sq = Vector2.DistanceSquared(atM, fellM);
            if (sq >= bestSq) continue;

            nearestM = atM;
            bestSq = sq;
        }

        return nearestM;
    }

    /// <summary>What a player is drawn as, by its mover number, or −1 (<see cref="TownWorld.ShownAs"/>).</summary>
    int MoverOf(TownWorld world, Selection shown) => shown.Kind switch
    {
        SelectionKind.Person => _capture.Movers.OfPerson(shown.Index),
        SelectionKind.Car => _capture.Movers.OfCar(shown.Index),
        SelectionKind.Locomotive => _capture.Movers.OfWagon(world.WagonOf(shown)),
        _ => -1,
    };

    /// <summary>The town as it stands after a tick, laid down for the players on every snapshot's tick (MP-8).</summary>
    public void AfterTick(TownWorld world, long tick)
    {
        if (tick % _config.Multiplayer.TicksPerSnapshot != 0) return;

        for (var seat = 0; seat < _players.Length; seat++) _inputApplied[seat] = Volatile.Read(ref _players[seat])?.InputApplied ?? 0;
        _capture.Take(world, _town.Shape, (uint)tick, _inputApplied);
        _published.Set();
    }

    /// <summary>The sending thread: each frame the town lays down, offered to every player in turn.</summary>
    void Send()
    {
        while (!_stop.IsCancellationRequested)
        {
            if (!_published.WaitOne(TimeSpan.FromSeconds(_config.Multiplayer.HoldAfterSilenceS))) continue;

            var town = _capture.Newest();
            if (town is null) continue;

            for (var seat = 0; seat < _players.Length; seat++)
            {
                Volatile.Read(ref _players[seat])?.Pump(town, _players);
            }
        }
    }

    /// <summary>The listener's loop: every connection upgraded, said hello to and seated or turned away (MP-10).</summary>
    async Task Admit()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            _ = Task.Run(() => Welcome(client));
        }
    }

    async Task Welcome(TcpClient client)
    {
        client.NoDelay = true;
        WebSocket? socket = null;
        try
        {
            var stream = client.GetStream();
            var head = await RequestHead.Read(stream, _stop.Token);

            // MP-11: anything but a player asking in is a browser asking for the page, where this town hands one out.
            if (head is { IsUpgrade: false } && _page is not null) head = await ServeThePage(_page, stream, head);
            if (head is null || !head.IsUpgrade)
            {
                if (head is not null || _page is null) await WebSocketHandshake.Refuse(stream, _stop.Token);
                client.Dispose();
                return;
            }

            // No keep-alive of the socket's own: a player is heard every frame, and one that falls silent is the seat's to
            // hold and let go of (MP-4).
            socket = await WebSocketHandshake.Accept(stream, head, Timeout.InfiniteTimeSpan, _stop.Token);

            var buffer = new byte[_config.Multiplayer.LargestInboundBytes];
            var length = await Player.ReadMessage(socket, buffer, _stop.Token);
            if (length < 0 || !Protocol.ReadHello(buffer.AsSpan(0, length), out var version, out var card))
            {
                await Refuse(socket, Refusal.Malformed);
                return;
            }

            if (version != Protocol.Version)
            {
                await Refuse(socket, Refusal.Version);
                return;
            }

            var figures = _config.Multiplayer;
            card = card with { Rgb = SeatColour.Vivid(card.Rgb, figures.SeatLeastSaturation, figures.SeatLeastValue) };
            var seat = Array.FindIndex(_players, p => p is null);
            var player = seat < 0 ? null : new Player(seat, socket, _town, figures, card);
            if (player is null || Interlocked.CompareExchange(ref _players[seat], player, null) is not null)
            {
                player?.Dispose();
                await Refuse(socket, Refusal.Full);
                return;
            }

            var welcome = new byte[1024];
            var welcomeLength = _town.Welcome.Write(welcome);
            await socket.SendAsync(welcome.AsMemory(0, welcomeLength), WebSocketMessageType.Binary, true, _stop.Token);
            await player.Listen(_config.Multiplayer.LargestInboundBytes, () => Volatile.Read(ref _tick));
        }
        catch (Exception failed) when (failed is WebSocketException or IOException or OperationCanceledException or ObjectDisposedException)
        {
            socket?.Abort();
            if (socket is null) client.Dispose();
        }
    }

    /// <summary>
    /// <b>A browser's requests for the page answered on one connection</b> until it idles, closes or asks for it to close;
    /// a player asking in on it after them is handed back to be let in. Null for a connection done with.
    /// </summary>
    async Task<RequestHead?> ServeThePage(PageHost page, NetworkStream stream, RequestHead asked)
    {
        RequestHead? head = asked;
        while (head is { IsUpgrade: false } && await page.Answer(stream, head, _stop.Token))
        {
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            idle.CancelAfter(page.Idle);
            try
            {
                head = await RequestHead.Read(stream, idle.Token);
            }
            catch (OperationCanceledException) when (!_stop.IsCancellationRequested)
            {
                return null;
            }
        }

        return head is { IsUpgrade: true } ? head : null;
    }

    async Task Refuse(WebSocket socket, Refusal why)
    {
        var message = new byte[8];
        var length = Protocol.WriteRefused(message, why);
        await socket.SendAsync(message.AsMemory(0, length), WebSocketMessageType.Binary, true, _stop.Token);
        await socket.CloseOutputAsync(WebSocketCloseStatus.PolicyViolation, why.ToString(), _stop.Token);
        socket.Dispose();
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
        _published.Set();
        _sender.Join();
        foreach (var player in _players) player?.Dispose();
        _stop.Dispose();
        _published.Dispose();
    }
}
