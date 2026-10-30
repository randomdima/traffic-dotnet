using System.Net.WebSockets;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Multiplayer.Wire;

namespace TrafficSimulation.Multiplayer.Server;

/// <summary>
/// <b>One player of a served town</b>: its socket, what it last said, the seat it holds and what it is drawn as, and the
/// snapshots it has been sent. Three threads touch it, each its own part: the socket's reader writes what was heard, the town's
/// thread writes the seat (<see cref="TownServer.BeforeTick"/>), and the sender writes the snapshots
/// (<see cref="Pump"/>).
/// </summary>
internal sealed class Player : IDisposable
{
    readonly WebSocket _socket;
    readonly ServedTown _town;
    readonly CancellationTokenSource _stop = new();
    readonly object _heardGate = new();
    InputMessage _heard;

    readonly PlayerArea _area;
    readonly Sent[] _sent;
    readonly byte[] _buffer;
    readonly MoverState[] _none = [];
    uint _sequence;
    ValueTask _sending;
    bool _awaitingSend;

    /// <summary>The player each seat was held by when this one was told of it (MP-15), or null for a seat it was not; the sender's own.</summary>
    readonly Player?[] _told;

    public Player(int seat, WebSocket socket, ServedTown town, MultiplayerFigures figures, SeatCard card)
    {
        Seat = seat;
        Card = card;
        _socket = socket;
        _town = town;
        _area = new PlayerArea(town.Shape.Movers.Count);
        _sent = new Sent[figures.BaselinesKept];
        for (var i = 0; i < _sent.Length; i++) _sent[i] = new Sent(figures.MostMoversSent);
        _told = new Player?[figures.MostSeats];
        _buffer = new byte[SnapshotBytes(town.Shape, figures.MostMoversSent, figures.MostSeats)];
    }

    public int Seat { get; }

    /// <summary>Who it said it is, its colour made one a map can show (<see cref="SeatColour.Vivid"/>).</summary>
    public SeatCard Card { get; }

    /// <summary>Whether it has laid the town and said so; written by the reader.</summary>
    public bool Ready => Volatile.Read(ref _ready);

    bool _ready;

    /// <summary>Whether it is gone — closed, failed or let go of; the town's thread frees its seat on seeing it.</summary>
    public bool Closed => Volatile.Read(ref _closed);

    bool _closed;

    /// <summary>The tick it was last heard at.</summary>
    public long HeardAtTick => Volatile.Read(ref _heardAtTick);

    long _heardAtTick;

    /// <summary>
    /// What it is drawn as, by its mover number — its body, or what it is in — or −1 before it is anybody; written by the
    /// town's thread, and what its area is round (MP-1).
    /// </summary>
    public int Mover
    {
        get => Volatile.Read(ref _mover);
        set => Volatile.Write(ref _mover, value);
    }

    int _mover = -1;

    public SeatState State
    {
        get => (SeatState)Volatile.Read(ref _state);
        set => Volatile.Write(ref _state, (int)value);
    }

    int _state;

    /// <summary>The newest input the town has applied, which every snapshot says back.</summary>
    public uint InputApplied
    {
        get => Volatile.Read(ref _inputApplied);
        set => Volatile.Write(ref _inputApplied, value);
    }

    uint _inputApplied;

    /// <summary>The tick its body was found down at, or −1; the town's thread's own.</summary>
    public long DownAtTick { get; set; } = -1;

    /// <summary>The count of its presses to get in or out the town has acted on (<see cref="InputMessage.Acts"/>); the town's thread's own.</summary>
    public byte ActsTaken { get; set; }

    /// <summary>What it last said it holds.</summary>
    public InputMessage Heard
    {
        get
        {
            lock (_heardGate) return _heard;
        }
    }

    /// <summary>Let go of: the socket is dropped, and the seat freed on the town's next tick.</summary>
    public void Close()
    {
        Volatile.Write(ref _closed, true);
        _stop.Cancel();
        _socket.Abort();
    }

    /// <summary>
    /// The socket read until it closes: a ready, then the keys, each judged whole (MP-10). It is the welcome's sender's
    /// to have said hello first (<see cref="TownServer"/>).
    /// </summary>
    public async Task Listen(int largestInboundBytes, Func<long> tick)
    {
        var buffer = new byte[largestInboundBytes];
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var length = await ReadMessage(_socket, buffer, _stop.Token);
                if (length < 0) return;

                var message = buffer.AsSpan(0, length);
                if (!Ready && Protocol.ReadReady(message))
                {
                    Volatile.Write(ref _heardAtTick, tick());
                    Volatile.Write(ref _ready, true);
                }
                else if (Ready && InputMessage.Read(message, out var input))
                {
                    lock (_heardGate)
                    {
                        // An input overtaken by a later one is the later one's past (MP-3).
                        if (input.Sequence > _heard.Sequence) _heard = input;
                    }

                    Volatile.Write(ref _heardAtTick, tick());
                }
                else
                {
                    return;
                }
            }
        }
        catch (Exception failed) when (failed is WebSocketException or OperationCanceledException or IOException or ObjectDisposedException)
        {
        }
        finally
        {
            Close();
        }
    }

    /// <summary>
    /// <b>One message read whole into <paramref name="buffer"/></b>, its length; or −1 for a close, a text frame, or one
    /// longer than the buffer — which is a connection closed and never a buffer grown (MP-10).
    /// </summary>
    internal static async ValueTask<int> ReadMessage(WebSocket socket, byte[] buffer, CancellationToken stop)
    {
        var got = 0;
        ValueWebSocketReceiveResult read;
        do
        {
            if (got == buffer.Length) return -1;

            read = await socket.ReceiveAsync(buffer.AsMemory(got), stop);
            got += read.Count;
        }
        while (!read.EndOfMessage);

        return read.MessageType == WebSocketMessageType.Binary ? got : -1;
    }

    /// <summary>
    /// <b>The town's newest frame sent, if the last one has gone</b> — never queued behind it, since a snapshot not sent
    /// is one the next replaces (MP-8). On the sending thread, <paramref name="seated"/> the town's seats.
    /// </summary>
    public void Pump(CapturedTown town, Player?[] seated)
    {
        if (Closed || !SendDone()) return;

        var own = Mover;
        if (!Ready || own < 0) return;

        // MP-14: a player that has fallen this far behind acknowledging is sent nothing until it catches up — an ack past
        // anything sent wraps to the same answer.
        var ack = Heard.Ack;
        if (_sequence - ack >= _town.MostUnacknowledged) return;

        var sequence = ++_sequence;
        var into = _sent[(int)(sequence % (uint)_sent.Length)];

        // The baseline is the snapshot the player says it holds, if it is still kept and is not the one about to be
        // written over; otherwise the area is sent whole.
        var baseline = _none.AsSpan();
        var baselineSequence = 0u;
        var acked = _sent[(int)(ack % (uint)_sent.Length)];
        if (ack != 0 && acked.Sequence == ack && acked != into && sequence - ack <= SnapshotHeader.MostBaselineGap)
        {
            baseline = acked.Movers.AsSpan(0, acked.Count);
            baselineSequence = ack;
        }

        into.Sequence = sequence;
        into.Count = _area.Gather(town, own, _town.TakeUpQuanta, _town.LetGoQuanta, into.Movers);

        var w = new BitWriter(_buffer);
        new SnapshotHeader(sequence, baselineSequence, town.Tick, InputApplied, own, State).Write(ref w, _town.Shape);
        TellTheSeats(ref w, into.Movers.AsSpan(0, into.Count), seated);
        SnapshotCodec.Write(ref w, _town.Shape, into.Movers.AsSpan(0, into.Count), baseline);
        Send(Protocol.Done(ref w));
    }

    /// <summary>
    /// <b>The card of every seat in the area this player has not been told of</b> as it is held now (MP-15): its mover is
    /// in <paramref name="movers"/>, so nothing is said of a seat outside the area (MP-1). Every send arrives, in order,
    /// so a seat is told once a holder.
    /// </summary>
    void TellTheSeats(ref BitWriter w, ReadOnlySpan<MoverState> movers, Player?[] seated)
    {
        foreach (ref readonly var mover in movers)
        {
            var seat = mover.Driver - 1;
            if ((uint)seat >= (uint)seated.Length || (uint)seat >= (uint)_told.Length) continue;

            var holder = Volatile.Read(ref seated[seat]);
            if (holder is null || ReferenceEquals(_told[seat], holder)) continue;

            w.Write(true);
            w.Write((uint)seat, SeatCard.SeatBits);
            holder.Card.Write(ref w);
            _told[seat] = holder;
        }

        w.Write(false);
    }

    /// <summary>The snapshots and their bytes sent so far, read by whoever reports on the feed; on the sending thread.</summary>
    public long SnapshotsSent => Volatile.Read(ref _snapshotsSent);

    public long BytesSent => Volatile.Read(ref _bytesSent);

    long _snapshotsSent;
    long _bytesSent;

    void Send(int length)
    {
        if (length < 0) return;

        Volatile.Write(ref _snapshotsSent, _snapshotsSent + 1);
        Volatile.Write(ref _bytesSent, _bytesSent + length);
        try
        {
            _sending = _socket.SendAsync(_buffer.AsMemory(0, length), WebSocketMessageType.Binary, endOfMessage: true, _stop.Token);
            _awaitingSend = true;
        }
        catch (Exception failed) when (failed is WebSocketException or ObjectDisposedException or InvalidOperationException)
        {
            Close();
        }
    }

    /// <summary>Whether the last send has finished, its outcome taken once; a failed one closes the player.</summary>
    bool SendDone()
    {
        if (!_awaitingSend) return true;
        if (!_sending.IsCompleted) return false;

        _awaitingSend = false;
        try
        {
            _sending.GetAwaiter().GetResult();
        }
        catch (Exception failed) when (failed is WebSocketException or OperationCanceledException or IOException or ObjectDisposedException)
        {
            Close();
            return false;
        }

        return true;
    }

    /// <summary>The longest a snapshot of so many movers and seats can be.</summary>
    static int SnapshotBytes(in WireShape shape, int movers, int seats) =>
        (SnapshotHeader.MostBits(shape) + SeatCard.MostSectionBits(seats) + SnapshotCodec.MostBits(shape, movers) + 7) / 8 + 1;

    public void Dispose()
    {
        Close();
        _socket.Dispose();
    }

    /// <summary>One snapshot sent, kept to be named as a baseline.</summary>
    sealed class Sent(int movers)
    {
        public uint Sequence;
        public int Count;
        public readonly MoverState[] Movers = new MoverState[movers];
    }
}
