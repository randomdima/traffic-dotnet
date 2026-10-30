namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The bodies a served town's players are</b> (MP-4): a block at the end of the roster, the same few to each seat, and
/// which of its own each seat is now — with the keys it holds and the train it drives, if it drives one (MP-13).
/// </summary>
/// <remarks>
/// <b>A seat's bodies are its own and nobody else's</b>, so which seat a body is laid for is arithmetic on its number and
/// nothing is looked up; a body is a seat's <em>now</em> only while that seat names it, and the ones it has left lying in
/// the road are the town's until they come back offstage.
/// </remarks>
internal sealed class PlayerBodies
{
    readonly int[] _bodyOf;
    readonly HandInput[] _hand;
    readonly int[] _seatOfTrain;
    readonly int _perSeat;

    /// <param name="bodies">How many are laid in all: <paramref name="seats"/> times the bodies a seat has, or none.</param>
    public PlayerBodies(int seats, int bodies, int trains)
    {
        _bodyOf = new int[seats];
        _hand = new HandInput[seats];
        _seatOfTrain = new int[trains];
        Array.Fill(_bodyOf, -1);
        Array.Fill(_seatOfTrain, -1);
        _perSeat = seats > 0 ? bodies / seats : 0;
        Count = bodies;
    }

    /// <summary>How many bodies are laid, which is none in a town nobody is served.</summary>
    public int Count { get; }

    /// <summary>The first of them on the roster, once they are stood.</summary>
    public int First { get; set; } = -1;

    /// <summary>The bodies laid for a seat, as a run of the roster.</summary>
    public (int From, int To) BodiesOf(int seat) => (First + (seat * _perSeat), First + ((seat + 1) * _perSeat));

    /// <summary>The person a seat is now, or −1.</summary>
    public int BodyOf(int seat) => _bodyOf[seat];

    /// <summary>The seat a person is the body of now, or −1.</summary>
    public int SeatOf(int person)
    {
        if (First < 0 || person < First || person >= First + Count) return -1;

        var seat = (person - First) / _perSeat;
        return _bodyOf[seat] == person ? seat : -1;
    }

    /// <summary>Whether a person is one of the players' bodies, a seat's now or not.</summary>
    public bool IsOne(int person) => First >= 0 && person >= First && person < First + Count;

    /// <summary>A seat named somebody, or nobody for −1.</summary>
    public void Become(int seat, int person) => _bodyOf[seat] = person;

    public HandInput HandOf(int seat) => _hand[seat];

    public void Hold(int seat, HandInput hand) => _hand[seat] = hand;

    /// <summary>The seat at a train's controls, or −1.</summary>
    public int SeatOfTrain(int train) => _seatOfTrain[train];

    /// <summary>A seat put at a train's controls, or taken from whichever it was at for −1.</summary>
    public void Drive(int seat, int train)
    {
        for (var t = 0; t < _seatOfTrain.Length; t++)
        {
            if (_seatOfTrain[t] == seat) _seatOfTrain[t] = -1;
        }

        if (train >= 0) _seatOfTrain[train] = seat;
    }
}
