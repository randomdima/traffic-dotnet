using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.World.Town;

/// <summary>What kind of unit the interface has picked out, if any.</summary>
internal enum SelectionKind : byte
{
    None,
    Person,
    Car,
}

/// <summary>
/// CTL-1: the one unit the interface is talking about. A kind and an index into that fleet's roster,
/// and nothing else — a selection that held a reference would outlive the town it was taken on.
/// </summary>
internal readonly record struct Selection(SelectionKind Kind, int Index)
{
    public static Selection Of(SelectionKind kind, int index) => index >= 0 ? new Selection(kind, index) : default;

    public bool Any => Kind != SelectionKind.None && Index >= 0;

    /// <summary>Whether a run of selected units holds this one — the membership test every hot path asks.</summary>
    /// <remarks>
    /// A scan and not a lookup: it is walked over what is actually selected rather than over the bound,
    /// so a town with nothing picked out pays one comparison and a town with one car picked out pays two.
    /// </remarks>
    public static bool Holds(ReadOnlySpan<Selection> units, SelectionKind kind, int index)
    {
        for (var i = 0; i < units.Length; i++)
        {
            if (units[i].Kind == kind && units[i].Index == index) return true;
        }

        return false;
    }
}

/// <summary>
/// <b>CTL-1b: the units the interface is talking about, as a set.</b> In the order they were taken,
/// bounded by <see cref="ViewFigures.SelectionMaxUnits"/>, and laid once with the town — so picking a
/// district's worth of cars out allocates nothing.
/// </summary>
/// <remarks>
/// It holds indices and not bodies for the same reason one <see cref="Selection"/> does, and it is the
/// town's to change: every way in is a method on <see cref="TownWorld"/>, because a set changed behind
/// the town's back would leave a hand on a unit nobody is looking at.
/// </remarks>
internal sealed class SelectionSet(int capacity)
{
    readonly Selection[] _units = new Selection[Math.Max(1, capacity)];
    int _count;

    public int Count => _count;

    public int Capacity => _units.Length;

    public bool Any => _count > 0;

    public bool Full => _count >= _units.Length;

    public ReadOnlySpan<Selection> Units => _units.AsSpan(0, _count);

    /// <summary>The first unit taken, which is what a read-out with room for one unit says.</summary>
    public Selection Lead => _count > 0 ? _units[0] : default;

    public bool Holds(SelectionKind kind, int index) => Selection.Holds(Units, kind, index);

    public int CountOf(SelectionKind kind)
    {
        var found = 0;
        for (var i = 0; i < _count; i++)
        {
            if (_units[i].Kind == kind) found++;
        }

        return found;
    }

    /// <summary>Takes a unit in, and says whether the set changed — a full set and a unit already in it both refuse.</summary>
    public bool Add(Selection unit)
    {
        if (!unit.Any || Full || Holds(unit.Kind, unit.Index)) return false;

        _units[_count++] = unit;
        return true;
    }

    public bool Remove(Selection unit)
    {
        for (var i = 0; i < _count; i++)
        {
            if (_units[i].Kind != unit.Kind || _units[i].Index != unit.Index) continue;

            // The order they were taken in is what the read-outs read, so the tail shuffles down rather
            // than the last entry being swapped into the hole.
            _units.AsSpan(i + 1, _count - i - 1).CopyTo(_units.AsSpan(i));
            _count--;
            return true;
        }

        return false;
    }

    /// <summary>Empties it, and says whether it had anything in it.</summary>
    public bool Clear()
    {
        var had = _count > 0;
        _count = 0;
        return had;
    }
}

/// <summary>
/// <b>CTL-5: the hand at the wheel, as one tick's worth of what is being held down.</b> The same
/// record serves both agent kinds, because what a hand does is ask for motion and the two bodies
/// differ in how they answer.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is pushed through the seam every tick</b> (CTL-6), so the agent loop cannot tell a
/// hand-driven agent from any other. Nothing here drives a body behind its driver's back: what a hand
/// produces is the same command a follower produces, and everything under it — the tyre model, the
/// speed caps, the turning circle, terrain, collisions and damage — is untouched.
/// </para>
/// <para>
/// <b>Nothing held is not the same as no hand.</b> Releasing the keys coasts; it does not hand the
/// unit back (CTL-5b), so <see cref="Held"/> stays true while the wheel is taken and the unit simply
/// asks for nothing.
/// </para>
/// </remarks>
internal readonly record struct HandInput(bool Held, float Throttle, float Steer, bool Handbrake, Vector2 WalkDirection)
{
    /// <summary>Nobody at the wheel: the unit decides for itself again.</summary>
    public static HandInput None => default;
}
