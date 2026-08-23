namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>One place two ways through a junction are driven over each other</b>, in both joins' own metres:
/// <see cref="FromM"/> to <see cref="ToM"/> of the crossed one, and <see cref="MineFromM"/> to
/// <see cref="MineToM"/> of the crossing one. Either pair goes into the road's book as it stands.
/// </summary>
/// <remarks>
/// <b>Both sides, because a crossing is one event and a car passes it once.</b> The crossed metres are what
/// the car takes off the traffic on that join; its own metres are where it takes them — so the section is
/// spent once the car's tail is a clearance beyond <see cref="MineToM"/>, and a table that carried only the
/// far side would have nothing to say about when.
/// </remarks>
/// <param name="OnTurn">The turn slot whose ground this is — the join being crossed, not the one crossing.</param>
internal readonly record struct CrossedSection(int OnTurn, float FromM, float ToM, float MineFromM, float MineToM);

/// <summary>
/// <b>One run of a movement's own join that the other ways through the box are driven over it at</b>, in
/// that join's metres: the overlapping <see cref="CrossedSection.MineFromM"/>..<see cref="CrossedSection.MineToM"/>
/// intervals merged, and the metres between two crossings that touch neither left out.
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
/// <b>Which ground each way through a junction takes off the other ways through it</b> (TER-5c), worked
/// out once from the lines themselves and never asked again: for every movement, the section of every
/// other movement's join that its own line is driven over.
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
/// metres named there. Marked instead — a stretch written onto every join a movement crossed — one car
/// approaching a junction reserved a fan of ways it would never touch, and the ground of a box belonged to
/// whoever aimed at it first rather than to whoever was on it.
/// </para>
/// <para>
/// <b>Both ends of every section, because the lookup goes both ways.</b> The crossed metres are what this
/// movement has to ask about; its own metres are where the answer bites — the place its grant is cut, and
/// the place it is past once its tail is a clearance beyond.
/// </para>
/// <para>
/// <b>Nothing here is asserted, and that is the whole of the difference.</b> The relation this replaced
/// settled three quarters of its pairs without measuring: a shared entry lane, a shared exit lane and a
/// turn-around each came back true on sight. Two of those are the road grant's job — the car in front out
/// of one lane is a headway and a merge is cut on the lane merged into — and the third made every
/// turn-around shut its whole junction. Measured instead, a turn-around sweeping the disc takes a section
/// of everything because it is driven over everything, and a street bending through a box takes none.
/// </para>
/// <para>
/// <b>The section is the crossing movement's whole width of it</b>, at the clearance the lines are
/// compared at: two paths that pass within a car's width never touch each other's paint and still cannot
/// both be driven, so what is taken is every metre of the crossed join that comes that near.
/// </para>
/// <para>
/// <b>Being driven over is mutual, and the measurement is symmetric because of it.</b> The metres of B that
/// A crosses and the metres of B that B records as its own share of that same crossing are one interval,
/// recorded once and filed under both movements — so what one car holds on its own join
/// (<see cref="OwnRun"/>) is exactly what the other car finds when it looks this table up.
/// </para>
/// </remarks>
internal sealed class JunctionCrossings
{
    readonly int[] _offsets;
    readonly CrossedSection[] _sections;
    readonly int[] _ownOffsets;
    readonly OwnRun[] _ownRuns;

    public JunctionCrossings(int[] offsets, CrossedSection[] sections)
    {
        _offsets = offsets;
        _sections = sections;

        var movements = offsets.Length - 1;
        _ownOffsets = new int[movements + 1];
        var runs = new List<OwnRun>();
        var mine = new List<OwnRun>();
        for (var slot = 0; slot < movements; slot++)
        {
            mine.Clear();
            foreach (ref readonly var section in Of(slot))
            {
                mine.Add(new OwnRun(section.MineFromM, section.MineToM));
            }

            mine.Sort(static (first, second) => first.FromM.CompareTo(second.FromM));
            foreach (var run in mine)
            {
                if (runs.Count > _ownOffsets[slot] && run.FromM <= runs[^1].ToM)
                {
                    runs[^1] = runs[^1] with { ToM = MathF.Max(runs[^1].ToM, run.ToM) };
                    continue;
                }

                runs.Add(run);
            }

            _ownOffsets[slot + 1] = runs.Count;
            MostOwnRuns = Math.Max(MostOwnRuns, runs.Count - _ownOffsets[slot]);
        }

        _ownRuns = [.. runs];
    }

    /// <summary>The ground this movement takes off the others at its junction, one section per join it crosses.</summary>
    public ReadOnlySpan<CrossedSection> Of(int slot) =>
        slot < 0 || slot + 1 >= _offsets.Length
            ? []
            : _sections.AsSpan(_offsets[slot], _offsets[slot + 1] - _offsets[slot]);

    /// <summary>
    /// <b>The runs of a movement's own join the same crossings fall on</b>, in that join's own metres — the
    /// ground a car committed to this movement holds, and what refuses a car coming the other way before
    /// either one's own road has reached the place the two lines meet. Empty where its line is driven over
    /// nothing.
    /// </summary>
    /// <remarks>
    /// <b>The places and not the span between them.</b> Held as one interval from the first crossing point
    /// to the last, a movement across a wide box took the whole of its own way through it — including the
    /// metres in the middle no other line comes near — and a car making a movement that crosses only those
    /// middle metres was refused ground nothing was ever going to be driven over. The runs are a fact about
    /// the lines and so are settled with them, rather than accumulated per car per tick.
    /// </remarks>
    public ReadOnlySpan<OwnRun> OwnRuns(int slot) =>
        slot < 0 || slot + 1 >= _ownOffsets.Length
            ? []
            : _ownRuns.AsSpan(_ownOffsets[slot], _ownOffsets[slot + 1] - _ownOffsets[slot]);

    /// <summary>
    /// How many joins the busiest movement in the town crosses — <b>how many ways one driver may have to
    /// look up</b>, and not how much room in the book it wants, since it writes to none of them.
    /// </summary>
    public int MostCrossedByOne { get; init; }

    /// <summary>
    /// And how many runs of its own join the busiest one holds, which <em>is</em> room in the book: it is
    /// the whole of what a car committed to a crossing lays beyond its own reservation.
    /// </summary>
    public int MostOwnRuns { get; }
}
