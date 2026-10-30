namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The hands that name their cars</b> (CTL-5d): one a seat — a script, a bot, a test, a player served the town —
/// each holding one car by its number, and kept beside the player's own hand rather than inside the selection, because
/// the whole of what they are for is to be held while somebody else picks units out.
/// </summary>
/// <remarks>
/// <b>A car is one seat's at a time</b>: a seat naming a car another already holds holds nothing, so two drivers never
/// fight over one wheel and the earlier keeps it. What a car is asked is answered from the car's side
/// (<see cref="SeatOf"/>), which keeps every per-car question a load however many seats there are.
/// </remarks>
internal sealed class NamedHands
{
    readonly int[] _carOf;
    readonly HandInput[] _hand;
    readonly int[] _seatOf;

    /// <param name="seats">How many seats there are (<c>MultiplayerFigures.MostSeats</c>).</param>
    /// <param name="cars">The fleet's capacity.</param>
    public NamedHands(int seats, int cars)
    {
        _carOf = new int[seats];
        _hand = new HandInput[seats];
        _seatOf = new int[cars];
        Array.Fill(_carOf, -1);
        Array.Fill(_seatOf, -1);
    }

    public int Count => _carOf.Length;

    /// <summary>
    /// What one seat holds this tick. <b>Named every tick</b>, as the hand is, so a seat that stops being told has let
    /// go only when it is told so: −1, or <see cref="HandInput.None"/>.
    /// </summary>
    /// <param name="car">The car in the fleet, or −1 for none — and one off the fleet is none.</param>
    public void Hold(int seat, int car, HandInput input)
    {
        var was = _carOf[seat];
        if (was >= 0 && _seatOf[was] == seat) _seatOf[was] = -1;

        var free = car >= 0 && car < _seatOf.Length && _seatOf[car] < 0;
        _carOf[seat] = free ? car : -1;
        _hand[seat] = free ? input : default;
        if (free) _seatOf[car] = seat;
    }

    /// <summary>The car a seat holds, or −1.</summary>
    public int CarOf(int seat) => _carOf[seat];

    /// <summary>The seat holding a car, or −1.</summary>
    public int SeatOf(int car) => _seatOf[car];

    /// <summary>The hand a seat holds its car with.</summary>
    public HandInput HandOf(int seat) => _hand[seat];

    /// <summary>Whether a seat's hand is on this car's wheel.</summary>
    public bool Holds(int car) => _seatOf[car] is var seat and >= 0 && _hand[seat].Held;

    /// <summary>The hand on this car from a seat, or nothing held.</summary>
    public HandInput On(int car) => _seatOf[car] is var seat and >= 0 ? _hand[seat] : default;

    /// <summary>Whether any seat holds a wheel this tick.</summary>
    public bool AnyHeld
    {
        get
        {
            foreach (var hand in _hand)
            {
                if (hand.Held) return true;
            }

            return false;
        }
    }
}
