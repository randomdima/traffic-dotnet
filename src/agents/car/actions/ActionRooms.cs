namespace TrafficSimulation.Agents.Car.Actions;

/// <summary>
/// <b>Room for what only a few cars hold at once</b> — what a pass gets past: laid with the town for a share of the
/// fleet, taken by a car when its action first needs it and given back when the car leaves the action
/// (<see cref="CarActions.Enter"/>).
/// </summary>
/// <remarks>
/// <b>A bound on the tables and not a figure behaviour reads</b>: a car that finds no room left is answered as one whose
/// ground does not fit its table — a pass the road does not offer — and asks again on its clock. <see cref="Refused"/> counts it, so an instrument can say whether a town ever came near.
/// </remarks>
internal sealed class ActionRooms
{
    /// <summary>The room of a car that holds none.</summary>
    public const int None = -1;

    readonly int[] _roomOf;
    readonly int[] _free;
    int _freeCount;

    /// <param name="cars">How many cars may ask.</param>
    /// <param name="share">How many cars share a room: one room for each this many, and never fewer than <paramref name="least"/>.</param>
    /// <param name="least">The fewest rooms laid, so a small town has a room for every car that could want one.</param>
    public ActionRooms(int cars, int share, int least)
    {
        Count = Math.Min(cars, Math.Max(least, (cars + share - 1) / share));
        _roomOf = new int[cars];
        Array.Fill(_roomOf, None);
        _free = new int[Count];
        for (var room = 0; room < Count; room++) _free[room] = Count - 1 - room;
        _freeCount = Count;
    }

    /// <summary>How many rooms were laid — the size every table kept in them is laid for.</summary>
    public int Count { get; }

    /// <summary>How many times a car found no room left, since the town was laid.</summary>
    public long Refused { get; private set; }

    /// <summary>The room this car holds, or <see cref="None"/>.</summary>
    public int RoomOf(int car) => _roomOf[car];

    /// <summary>The room this car holds, a room taken for it where it holds none — false where none is left.</summary>
    public bool Take(int car, out int room)
    {
        room = _roomOf[car];
        if (room != None) return true;

        if (_freeCount == 0)
        {
            Refused++;
            return false;
        }

        room = _roomOf[car] = _free[--_freeCount];
        return true;
    }

    /// <summary>Whatever room this car holds, given back.</summary>
    public void Free(int car)
    {
        var room = _roomOf[car];
        if (room == None) return;

        _free[_freeCount++] = room;
        _roomOf[car] = None;
    }
}
