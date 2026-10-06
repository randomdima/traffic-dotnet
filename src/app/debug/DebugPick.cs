using System.Numerics;

namespace TrafficSimulation.App.Debug;

/// <summary>What kind of thing the pointer can be asked about (OBS-2t).</summary>
internal enum DebugThing : byte
{
    None,
    Car,
    Walker,
    Boundary,
    Way,
    Line,
    SolverCell,
    GeometryCell,
    Zone,
}

/// <summary>
/// <b>One thing a layer draws, as the pointer found it</b> (OBS-2t) — a body by its index, a way by its number,
/// a stretch of boundary or a cell by where it was found.
/// </summary>
/// <param name="Index">The car, the walker or the driven line; a cell's column; a stretch's place in its chain.</param>
/// <param name="Way">The way; a cell's row; the chain a stretch is in.</param>
/// <param name="AtM">
/// Where it was found. <b>A boundary, a way and a cell are asked again there every frame</b>: what holds a way
/// changes every tick, and a stretch or a cell is the answer to a place rather than a thing with an index of
/// its own.
/// </param>
internal readonly record struct DebugTarget(DebugThing Thing, int Index = -1, int Way = -1, Vector2 AtM = default)
{
    public static DebugTarget None => default;

    /// <summary>Whether two finds are the one thing, wherever on it each was made.</summary>
    public bool Same(in DebugTarget other) => Thing == other.Thing && Index == other.Index && Way == other.Way;
}

/// <summary>
/// <b>What the reader is pointing at, and the one thing they have pinned</b> (OBS-2t). A click on the town pins
/// whatever the layers that are on find under it, and a click on nothing they draw lets it go.
/// </summary>
/// <remarks>
/// <para>
/// <b>A click is held as the place it landed and answered by the overlay on its next frame</b>, which is the one
/// place that knows which layers are drawn and what each of them holds there. The click itself goes on to do
/// what a click on the town does — a unit under it is still selected — because the pin is a reading and not a
/// mode: the reader who pinned a lane has not stopped being able to pick a car.
/// </para>
/// <para>
/// <b>It is dropped when the town is</b>, on the same terms the ruler's tapes are: a way on a town that no
/// longer exists is a number into nothing. It is dropped too when every layer that could find it is switched
/// off, since a card about a thing nothing on the glass is drawing is a reading with nothing to be read against.
/// </para>
/// </remarks>
internal sealed class DebugPick
{
    Vector2? _askedM;

    public DebugTarget Pinned { get; private set; }

    /// <summary>What the pointer was over on the last frame drawn, for a caller asking what a click would pin.</summary>
    public DebugTarget Hovered { get; internal set; }

    public void Click(Vector2 pointM) => _askedM = pointM;

    /// <summary>The place a click asked about since the overlay last looked, taken once.</summary>
    public bool TakeAsked(out Vector2 atM)
    {
        atM = _askedM ?? default;
        if (_askedM is null) return false;

        _askedM = null;
        return true;
    }

    public void Pin(in DebugTarget target) => Pinned = target;

    public void Clear()
    {
        Pinned = DebugTarget.None;
        Hovered = DebugTarget.None;
        _askedM = null;
    }

    public void TownChanged() => Clear();
}
