using TrafficSimulation.Multiplayer.Wire;

namespace TrafficSimulation.Multiplayer.Server;

/// <summary>
/// <b>The movers one player is sent</b> (MP-1, MP-2): those drawn inside the square round the player, kept until they are
/// a margin further out. The square is the server's — where the player's own mover stands — and nothing a player says
/// moves it.
/// </summary>
internal sealed class PlayerArea(int movers)
{
    readonly bool[] _held = new bool[movers];

    /// <summary>
    /// The movers of <paramref name="town"/> in this player's area, in mover order, into <paramref name="into"/>; how
    /// many. A mover past the room <paramref name="into"/> has is not sent, and is not held.
    /// </summary>
    /// <param name="own">What the player is drawn as, by its mover number: its body, or what it is in.</param>
    /// <param name="takeUpQuanta">Half the square a mover is taken up inside, in position quanta.</param>
    /// <param name="letGoQuanta">And half the square it has to leave to be let go of.</param>
    public int Gather(CapturedTown town, int own, int takeUpQuanta, int letGoQuanta, Span<MoverState> into)
    {
        var centre = town.Movers[own];
        var count = 0;
        for (var id = 0; id < _held.Length; id++)
        {
            ref readonly var mover = ref town.Movers[id];
            var off = Math.Max(Math.Abs(mover.X - centre.X), Math.Abs(mover.Y - centre.Y));
            var inside = town.Shown[id] && off <= (_held[id] ? letGoQuanta : takeUpQuanta) && count < into.Length;
            _held[id] = inside;
            if (inside) into[count++] = mover;
        }

        return count;
    }

    /// <summary>Nothing held: the next area gathered is taken up afresh, as a player newly seated has it.</summary>
    public void Forget() => Array.Clear(_held);
}
