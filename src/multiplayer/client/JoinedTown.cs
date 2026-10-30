using TrafficSimulation.Core.Config;
using TrafficSimulation.Multiplayer.Wire;
using TrafficSimulation.Runtime;

namespace TrafficSimulation.Multiplayer.Client;

/// <summary>Where a player stands with the town it asked to join.</summary>
internal enum JoinPhase
{
    /// <summary>The link opening, or the hello said and the welcome not yet come.</summary>
    Connecting,

    /// <summary>Welcomed: the town named is being laid.</summary>
    Welcomed,

    /// <summary>Laid, ready said, and snapshots coming.</summary>
    Playing,

    /// <summary>Turned away, for <see cref="JoinedTown.Refused"/>.</summary>
    Refused,

    /// <summary>The town laid here is not the server's (MP-5).</summary>
    Mismatch,

    /// <summary>The link closed, or the server said something that was not a message.</summary>
    Lost,
}

/// <summary>
/// <b>A player of a served town</b> (MP-3…7): the hello, the welcome, the ready once the town named is laid to the
/// server's digest, then every frame the snapshots read into the <see cref="Replica"/> and the keys sent back with the
/// newest of them acknowledged. It is the whole of what a player says, and it says nothing else.
/// </summary>
/// <remarks>
/// <b>The keys go when they say something new</b> (MP-3): a key or a press changed, or a newer snapshot to acknowledge —
/// never more often than the server ticks, and at least every <see cref="MultiplayerFigures.InputKeepAliveS"/> however
/// still the player holds. A frame faster than the tick says nothing the tick would read.
/// </remarks>
/// <param name="card">Who the player says it is in its hello (MP-15), its name as <see cref="SeatCard.Clean"/> leaves it.</param>
internal sealed class JoinedTown(ServerLink link, SimConfig config, SeatCard card)
{
    readonly byte[] _out = new byte[64];
    bool _helloSaid;
    uint _inputs;
    ServerClock? _clock;
    InputMessage _held;
    InputMessage _sent;
    double _sentAtS = double.NegativeInfinity;

    public JoinPhase Phase { get; private set; }

    public Welcome? Welcome { get; private set; }

    public Refusal Refused { get; private set; }

    public Replica? Replica { get; private set; }

    /// <summary>
    /// What arrived since the last frame read, at the local <paramref name="localS"/>: the welcome, a refusal, or the
    /// snapshots — and then the keys held, if they are due. Called once a frame, after the pump and the keys.
    /// </summary>
    public void Read(double localS)
    {
        Receive(localS);
        SendTheKeys(localS);
    }

    void Receive(double localS)
    {
        if (!_helloSaid && link.State == LinkState.Open) _helloSaid = link.Send(_out.AsSpan(0, Protocol.WriteHello(_out, card)));
        if (link.State == LinkState.Closed && Phase is JoinPhase.Connecting or JoinPhase.Welcomed or JoinPhase.Playing)
            Phase = JoinPhase.Lost;

        while (link.TryRead(out var message))
        {
            switch (Protocol.KindOf(message))
            {
                case MessageKind.Welcome when Phase == JoinPhase.Connecting:
                    Welcome = Welcome.Read(message);
                    Phase = Welcome is null ? JoinPhase.Lost : JoinPhase.Welcomed;
                    break;
                case MessageKind.Refused:
                    Phase = JoinPhase.Refused;
                    Refused = Protocol.ReadRefused(message, out var why) ? why : Refusal.Malformed;
                    break;
                case MessageKind.Snapshot when Phase == JoinPhase.Playing:
                    if (Replica!.Take(message, out var serverTick)) _clock!.Arrived(serverTick / (double)Welcome!.TickRateHz, localS);
                    break;
            }
        }
    }

    /// <summary>
    /// <b>The town named laid here</b>, and whether it is the server's (MP-5): if it is, ready is said and snapshots are
    /// read from now on; if not, the player stops here rather than draw a town that is not the one being driven.
    /// </summary>
    public bool Laid(ulong planDigest)
    {
        if (Welcome is null || planDigest != Welcome.PlanDigest)
        {
            Phase = JoinPhase.Mismatch;
            return false;
        }

        var figures = config.Multiplayer;
        Replica = new Replica(Welcome, config);
        _clock = new ServerClock(figures.BaselinesKept, Welcome.InterpolationDelayS, figures.PlayoutSlewShare);
        link.Send(_out.AsSpan(0, Protocol.WriteReady(_out)));
        Phase = JoinPhase.Playing;
        return true;
    }

    /// <summary>The keys held this frame, sent at the frame's <see cref="Read"/> with the newest snapshot held and the presses to get in or out so far (MP-3, MP-13).</summary>
    public void Hold(float throttle, float steer, bool handbrake, bool boost = false) =>
        _held = new InputMessage(0, 0, throttle, steer, handbrake, _acts, boost);

    void SendTheKeys(double localS)
    {
        if (Phase != JoinPhase.Playing) return;

        var input = _held with { Ack = Replica!.Newest, Acts = _acts };
        var sinceS = localS - _sentAtS;
        var news = !input.HoldsTheSameAs(_sent) || input.Ack != _sent.Ack;
        if (sinceS * Welcome!.TickRateHz < 1d || (!news && sinceS < config.Multiplayer.InputKeepAliveS)) return;

        input = input with { Sequence = ++_inputs };
        if (!link.Send(_out.AsSpan(0, input.Write(_out)))) return;

        _sent = input;
        _sentAtS = localS;
    }

    /// <summary>A press to get into the nearest vehicle, or out of the one the player is in (MP-13), counted into every input from now on.</summary>
    public void Act() => _acts++;

    byte _acts;

    /// <summary>
    /// <b>The area posed at the instant drawn</b> (MP-7) and that instant on the server's clock in seconds, or NaN before
    /// the first snapshot.
    /// </summary>
    public double Pose(double localS)
    {
        if (Phase != JoinPhase.Playing) return double.NaN;

        var drawnS = _clock!.Drawn(localS);
        if (!double.IsNaN(drawnS)) Replica!.Pose(drawnS * Welcome!.TickRateHz);
        return drawnS;
    }

    /// <summary>What a player is told of where it stands, for the page's own banner; empty while it is playing.</summary>
    public string Said => Phase switch
    {
        JoinPhase.Connecting => "joining the town…",
        JoinPhase.Welcomed => $"laying {Welcome!.Map}…",
        JoinPhase.Playing when Replica!.OwnMover < 0 => "stepping into the town…",
        JoinPhase.Playing when Replica!.Seat == SeatState.Down => "knocked down — somebody new shortly",
        JoinPhase.Playing => string.Empty,
        JoinPhase.Refused => Refused switch
        {
            Refusal.Full => "the town is full",
            Refusal.Version => "this page is another build than the town's",
            _ => "the town turned this page away",
        },
        JoinPhase.Mismatch => "this page laid another town than the server's — another build",
        _ => "the link to the town is lost",
    };
}
