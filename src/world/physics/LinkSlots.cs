using System.Numerics;

namespace TrafficSimulation.World.Physics;

/// <summary>
/// <b>One link as a step solves it</b> (SOL-38): its two bodies, the point it holds on each in that body's own frame,
/// and how far apart it holds them — a pin at nought.
/// </summary>
internal readonly record struct LinkRow(int First, int Second, Vector2 AnchorFirstM, Vector2 AnchorSecondM, float RestM);

/// <summary>
/// <b>The links a world holds, by slot</b> (SOL-38), and the order a step solves them in: the slots holding one,
/// ascending. The slots are the caller's, numbered by whatever owns a link, and laid once — so coupling and uncoupling
/// allocate nothing. Kept by the solver and, for a step taken on the device, by the world that sends it the rows.
/// </summary>
internal sealed class LinkSlots(int slots)
{
    readonly LinkRow[] _rows = new LinkRow[slots];
    readonly bool[] _held = new bool[slots];
    readonly LinkRow[] _live = new LinkRow[slots];
    int _liveCount;
    bool _changed;

    public int Slots => _rows.Length;

    /// <summary>A slot's link, replacing whatever it held.</summary>
    public void Link(int slot, in LinkRow row)
    {
        _rows[slot] = row;
        _held[slot] = true;
        _changed = true;
    }

    /// <summary>A slot emptied; whether it held a link.</summary>
    public bool Unlink(int slot)
    {
        if (!_held[slot]) return false;

        _held[slot] = false;
        _changed = true;
        return true;
    }

    public bool IsLinked(int slot) => _held[slot];

    /// <summary>Whether the links have changed since <see cref="Live"/> was last asked.</summary>
    public bool Changed => _changed;

    /// <summary>Every link held, in slot order — the order a step solves them in.</summary>
    public ReadOnlySpan<LinkRow> Live
    {
        get
        {
            if (_changed)
            {
                _liveCount = 0;
                for (var slot = 0; slot < _rows.Length; slot++)
                {
                    if (_held[slot]) _live[_liveCount++] = _rows[slot];
                }

                _changed = false;
            }

            return _live.AsSpan(0, _liveCount);
        }
    }
}
