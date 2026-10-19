namespace TrafficSimulation.World.Road;

/// <summary>
/// One stretch of one way a holder means to travel next, in the way's own metres from <see cref="FromM"/> to
/// <see cref="ToM"/> — down its metres where <see cref="ToM"/> is the lesser.
/// </summary>
/// <param name="Key">Whose (<see cref="LaneOccupancy.KeyOf"/>): its index in its roster, and the roster.</param>
internal readonly record struct WayIntent(int Key, int Way, float FromM, float ToM);

/// <summary>
/// <b>Where every holder means to go next</b>, as its own driver says it: the piece of way it is on and the piece after
/// it, for every roster that moves — in the one numbering every way of the town is in (<see cref="TownWays"/>), so whatever
/// reads it reads every kind of mover alike.
/// </summary>
/// <remarks>
/// <b>Gathered when asked and never kept up by the step</b> (<c>TownWorld.StateIntents</c>): it is an instrument's reading,
/// and a driver writing it every tick for nobody would be the step paying for a picture. The rows grow when a gathering
/// outruns them and are kept, so a steady gathering allocates nothing.
/// </remarks>
internal sealed class WayIntents
{
    /// <summary>How many pieces one holder states: the one it is on and the one after it, a way or two each.</summary>
    public const int MostEach = 4;

    WayIntent[] _rows = new WayIntent[64];
    int _count;

    public ReadOnlySpan<WayIntent> Rows => _rows.AsSpan(0, _count);

    public int Count => _count;

    public void Clear() => _count = 0;

    /// <summary>One stretch of way a holder means to travel; a stretch of no length says nothing and is not kept.</summary>
    public void Add(int occupant, LaneRoster roster, int way, float fromM, float toM)
    {
        if (fromM == toM || way < 0) return;

        if (_count == _rows.Length) Array.Resize(ref _rows, 2 * _rows.Length);

        _rows[_count++] = new WayIntent(LaneOccupancy.KeyOf(occupant, roster), way, fromM, toM);
    }
}
