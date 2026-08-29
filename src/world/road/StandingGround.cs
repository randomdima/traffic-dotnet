namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The town's own furniture, as the stretches of way it stands on</b> — the immovable third of TER-4c.
/// A bollard in a lane is something a driver must be held off exactly as a wreck is, and the difference
/// between the two is only that this one was there when the town was laid and will be there when it is put
/// away.
/// </summary>
/// <remarks>
/// <para>
/// <b>Projected once and re-laid every tick.</b> The projection is the expensive half and nothing about it
/// can change — neither the prop nor the lane moves — so it is done with the town; the laying is a copy of
/// a handful of stretches into a book that is cleared every tick, which is what keeps <em>one</em> rule
/// about how the book is filled instead of two (<see cref="LaneOccupancy.Begin"/> drops everything, and
/// nothing has to be given back).
/// </para>
/// <para>
/// <b>The book is the driver's one place to look.</b> Asking the static tree instead would be a descent
/// whose every step is a cache miss, per driver per tick, for an answer that cannot change — and it would
/// leave a driver reading immovable ground out of one structure and everything else out of another.
/// </para>
/// <para>
/// <b>Props and not buildings</b> — the stated bound of TER-4c. A prop is a circle and a lane is a band, so
/// whether one covers the other is arithmetic; a building is a plot's worth of oriented box, and taken
/// conservatively as the circle round it, it would block every street it was merely built beside. <b>A
/// building standing on a carriageway is a broken town</b> rather than a case to be driven round, and what
/// catches one is the solver and the soak.
/// </para>
/// </remarks>
internal sealed class StandingGround
{
    readonly int[] _lane;
    readonly float[] _fromM;
    readonly float[] _toM;

    StandingGround(int[] lane, float[] fromM, float[] toM)
    {
        _lane = lane;
        _fromM = fromM;
        _toM = toM;
    }

    /// <summary>How many stretches of lane the town's furniture stands on, which is what a book needs room for.</summary>
    public int Count => _lane.Length;

    /// <summary>
    /// Everything immovable, into a book that has just been begun. <b>Nobody's</b>
    /// (<see cref="LaneOccupancy.Nobody"/>), because a prop is in no roster — and under a use of its own
    /// (<see cref="LaneUse.Furniture"/>), because <em>which</em> questions it answers is the whole of what
    /// it is: a driver's grant is cut at it like anything else on the lane, and a walker asking what is
    /// coming down that lane is asking about wheels.
    /// </summary>
    /// <remarks>
    /// Read as an <see cref="LaneUse.Obstruction"/> it was traffic, and what kept the walkers off that
    /// answer was that the exclusion they asked with named the same integer a prop stands under — one
    /// question's argument deciding another question's answer.
    /// </remarks>
    public void LayInto(LaneOccupancy book)
    {
        for (var at = 0; at < _lane.Length; at++)
        {
            book.Add(book.WayOfLane(_lane[at]), _fromM[at], _toM[at], 0f, LaneOccupancy.Nobody, LaneUse.Furniture);
        }
    }

    /// <summary>
    /// The stretches as they are found, before they are sealed into the arrays the tick reads. <b>Lanes and
    /// not ways</b>, so that what is projected needs no book to exist yet — the book has to be sized by how
    /// many of these there are.
    /// </summary>
    internal sealed class Builder
    {
        readonly List<(int Lane, float FromM, float ToM)> _found = [];

        public void Add(int lane, float fromM, float toM) => _found.Add((lane, fromM, toM));

        public StandingGround Seal()
        {
            var lane = new int[_found.Count];
            var fromM = new float[_found.Count];
            var toM = new float[_found.Count];
            for (var at = 0; at < _found.Count; at++) (lane[at], fromM[at], toM[at]) = _found[at];

            return new StandingGround(lane, fromM, toM);
        }
    }
}
