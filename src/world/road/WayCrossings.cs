namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>One place two ways are driven over each other</b>, in both ways' own metres: <see cref="FromM"/> to
/// <see cref="ToM"/> of the crossed one, and <see cref="MineFromM"/> to <see cref="MineToM"/> of the
/// crossing one. Either pair goes into the road's book as it stands.
/// </summary>
/// <remarks>
/// <b>Both sides, because a crossing is one event and a car passes it once.</b> The crossed metres are what
/// the car takes off the traffic on that way; its own metres are where it takes them — so the section is
/// spent once the car's tail is a clearance beyond <see cref="MineToM"/>, and a table that carried only the
/// far side would have nothing to say about when.
/// </remarks>
/// <param name="OnWay">
/// The way whose ground this is — the one being crossed, not the one crossing, numbered as the book numbers
/// ways (<see cref="LaneOccupancy.WayOfTurn"/>). <b>A way and not a movement</b>: a join is only ever driven
/// over another join, because the lanes are set back clear of the box (TER-5d), but a way laid off a
/// junction sweeps a lane's own metres and has to be able to say so.
/// </param>
internal readonly record struct CrossedSection(int OnWay, float FromM, float ToM, float MineFromM, float MineToM);

/// <summary>
/// <b>One run of a way's own line that the other ways are driven over it at</b>, in that way's metres: the
/// overlapping <see cref="CrossedSection.MineFromM"/>..<see cref="CrossedSection.MineToM"/> intervals
/// merged, and the metres between two crossings that touch neither left out.
/// </summary>
/// <remarks>
/// <b>It is the one thing a car committed to a crossing writes into the book</b>, and it is on the way that
/// car is itself driving. A driver's own road ahead is a braking distance and no more, which does not reach
/// the place two lines meet until it is nearly on top of the junction; the runs are that same ground held
/// from the moment the movement is committed to, so a car on any way crossing this one finds it there —
/// merged only so that one body cannot appear twice over one metre of one way.
/// </remarks>
internal readonly record struct OwnRun(float FromM, float ToM);

/// <summary>
/// <b>Which ground each way of the town takes off the ways it is driven over</b> (TER-5c), worked out once
/// from the lines themselves and never asked again: for every way, the section of every other way its own
/// line is driven over.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a table of ground and not a table of verdicts.</b> A relation saying two movements conflict
/// answers one question — may I go — and answers it for the whole junction at once, so a car crossing one
/// corner of a box shuts the far corner it never reaches. Sections say <em>where</em>, so a driver is
/// refused by whatever is standing on those metres and by nothing else.
/// </para>
/// <para>
/// <b>It is looked up and never written into</b> (TER-5c), and that is what keeps a reservation to the ways
/// a car is actually going to be on. A driver lays its road on the ways it drives; where its own way is
/// driven over another, it reads this table and asks that other way's own book what is standing on the
/// metres named there. Marked instead — a stretch written onto every way a movement crossed — one car
/// approaching a junction reserved a fan of ways it would never touch, and the ground of a box belonged to
/// whoever aimed at it first rather than to whoever was on it.
/// </para>
/// <para>
/// <b>It is indexed by way and holds the junction's joins and the ways laid off one alike</b>. A junction's
/// join is only ever driven over another join, because the lanes hand over clear of the box (TER-5d) — so
/// the lanes' own rows are empty and the table reads as the junction table it began as. What needs the wider
/// index is a way laid <em>along</em> a street rather than across a box: the line into a parking space
/// sweeps the oncoming lane's own metres, and a table that could only name joins could not say which ground
/// that was.
/// </para>
/// <para>
/// <b>Both ends of every section, because the lookup goes both ways.</b> The crossed metres are what this
/// movement has to ask about; its own metres are where the answer bites — the place its grant is cut, and
/// the place it is past once its tail is a clearance beyond.
/// </para>
/// <para>
/// <b>Nothing here is asserted, and that is the whole of the difference.</b> The relation this replaced
/// settled most of its pairs without measuring: a shared entry lane and a shared exit lane each came back
/// true on sight, and both of those are the road grant's job — the car in front out of one lane is a
/// headway and a merge is cut on the lane merged into. Measured instead, a street bending through a box
/// takes nothing off anybody and a turn across the oncoming stream takes the metres it really crosses.
/// </para>
/// <para>
/// <b>The section is the crossing movement's whole width of it</b>, at the clearance the lines are
/// compared at: two paths that pass within a car's width never touch each other's paint and still cannot
/// both be driven, so what is taken is every metre of the crossed way that comes that near.
/// </para>
/// <para>
/// <b>Being driven over is mutual, and the measurement is symmetric because of it.</b> The metres of B that
/// A crosses and the metres of B that B records as its own share of that same crossing are one interval,
/// recorded once and filed under both ways — so what one car holds on its own way (<see cref="OwnRun"/>)
/// is exactly what the other car finds when it looks this table up.
/// </para>
/// </remarks>
internal sealed class WayCrossings
{
    readonly int[] _offsets;
    readonly CrossedSection[] _sections;
    readonly int[] _ownOffsets;
    readonly OwnRun[] _ownRuns;

    public WayCrossings(int[] offsets, CrossedSection[] sections)
    {
        _offsets = offsets;
        _sections = sections;

        var ways = offsets.Length - 1;
        _ownOffsets = new int[ways + 1];
        var runs = new List<OwnRun>();
        var mine = new List<OwnRun>();
        for (var way = 0; way < ways; way++)
        {
            mine.Clear();
            foreach (ref readonly var section in Of(way))
            {
                mine.Add(new OwnRun(section.MineFromM, section.MineToM));
            }

            mine.Sort(static (first, second) => first.FromM.CompareTo(second.FromM));
            foreach (var run in mine)
            {
                if (runs.Count > _ownOffsets[way] && run.FromM <= runs[^1].ToM)
                {
                    runs[^1] = runs[^1] with { ToM = MathF.Max(runs[^1].ToM, run.ToM) };
                    continue;
                }

                runs.Add(run);
            }

            _ownOffsets[way + 1] = runs.Count;
            MostOwnRuns = Math.Max(MostOwnRuns, runs.Count - _ownOffsets[way]);
        }

        _ownRuns = [.. runs];
    }

    /// <summary>How many ways the table is laid over, which is the whole of the book's own numbering.</summary>
    public int WayCount => _offsets.Length - 1;

    /// <summary>
    /// <b>The same table over more ways</b>: this one's sections and the ones a slice laid off the road has
    /// measured — the ways at a bay, and the rows those give the lanes they sweep. Build-time, and it
    /// allocates freely, because nothing it produces is written to again.
    /// </summary>
    /// <remarks>
    /// <b>One table and not two.</b> A reader that had to ask which of two tables a way was in would be the
    /// second mechanism SIM-7 is about, and the one place it would be got wrong is the place a car park
    /// meets a street.
    /// </remarks>
    public WayCrossings Grown(int wayCount, IReadOnlyList<(int Way, CrossedSection Section)> laidOffTheRoad)
    {
        var offsets = new int[wayCount + 1];
        for (var way = 0; way < WayCount; way++) offsets[way + 1] = _offsets[way + 1] - _offsets[way];
        foreach (var (way, _) in laidOffTheRoad) offsets[way + 1]++;

        var most = 0;
        for (var way = 0; way < wayCount; way++)
        {
            most = Math.Max(most, offsets[way + 1]);
            offsets[way + 1] += offsets[way];
        }

        var sections = new CrossedSection[offsets[wayCount]];
        var cursor = (int[])offsets.Clone();
        for (var way = 0; way < WayCount; way++)
        {
            foreach (ref readonly var section in Of(way)) sections[cursor[way]++] = section;
        }

        foreach (var (way, section) in laidOffTheRoad) sections[cursor[way]++] = section;

        return new WayCrossings(offsets, sections) { MostCrossedByOne = most };
    }

    /// <summary>The ground this way takes off the others, one section per way it crosses.</summary>
    public ReadOnlySpan<CrossedSection> Of(int way) =>
        way < 0 || way + 1 >= _offsets.Length
            ? []
            : _sections.AsSpan(_offsets[way], _offsets[way + 1] - _offsets[way]);

    /// <summary>
    /// <b>The runs of a way's own line the same crossings fall on</b>, in that way's own metres — the ground
    /// a car committed to this movement holds, and what refuses a car coming the other way before either
    /// one's own road has reached the place the two lines meet. Empty where its line is driven over nothing.
    /// </summary>
    /// <remarks>
    /// <b>The places and not the span between them.</b> Held as one interval from the first crossing point
    /// to the last, a movement across a wide box took the whole of its own way through it — including the
    /// metres in the middle no other line comes near — and a car making a movement that crosses only those
    /// middle metres was refused ground nothing was ever going to be driven over. The runs are a fact about
    /// the lines and so are settled with them, rather than accumulated per car per tick.
    /// </remarks>
    public ReadOnlySpan<OwnRun> OwnRuns(int way) =>
        way < 0 || way + 1 >= _ownOffsets.Length
            ? []
            : _ownRuns.AsSpan(_ownOffsets[way], _ownOffsets[way + 1] - _ownOffsets[way]);

    /// <summary>
    /// How many ways the busiest movement in the town crosses — <b>how many ways one driver may have to
    /// look up</b>, and not how much room in the book it wants, since it writes to none of them.
    /// </summary>
    public int MostCrossedByOne { get; init; }

    /// <summary>
    /// And how many runs of its own way the busiest one holds, which <em>is</em> room in the book: it is
    /// the whole of what a car committed to a crossing lays beyond its own reservation.
    /// </summary>
    public int MostOwnRuns { get; }
}
