namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>One mark: a stretch of one way whose ribbon lies over a section of another's</b>, in both ways' own
/// metres — <see cref="MineFromM"/> to <see cref="MineToM"/> of the way it is filed under, and
/// <see cref="FromM"/> to <see cref="ToM"/> of <see cref="OnWay"/>.
/// </summary>
/// <remarks>
/// <b>Both sides, because a shared piece of ground is one place and a holder passes it once.</b> Its own
/// metres are where a hold on the filed way needs the other section; the other metres are the section it
/// needs (TER-5c.1).
/// </remarks>
/// <param name="OnWay">The way whose section it is, numbered as the reservations number ways (<see cref="TownWays"/>).</param>
internal readonly record struct CrossedSection(int OnWay, float FromM, float ToM, float MineFromM, float MineToM);

/// <summary>
/// <b>One run of a way's own line that other ways' ribbons lie over</b>, in that way's metres: the
/// overlapping <see cref="CrossedSection.MineFromM"/>..<see cref="CrossedSection.MineToM"/> intervals
/// merged, and the metres between two marks that touch neither left out.
/// </summary>
/// <remarks>
/// <b>Where a body at rest shuts somebody else's ground</b>: a car waiting with any part of itself inside
/// one of these is across a way another movement uses, which is what keeps a queue short of a box.
/// </remarks>
internal readonly record struct OwnRun(float FromM, float ToM);

/// <summary>
/// <b>The marks</b> (TER-5c.1): for every way of the town, the section of every other way whose ribbon its
/// own lies over, worked out once when the atlas is laid and never asked again.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a table of ground and not a table of verdicts.</b> A relation saying two movements conflict
/// answers one question — may I go — and answers it for the whole junction at once, so a car crossing one
/// corner of a box shuts the far corner it never reaches. Sections say <em>where</em>, so a holder is settled
/// against whoever plans those metres and against nobody else.
/// </para>
/// <para>
/// <b>It is looked up and never written into.</b> A hold is laid on the ways its holder drives or walks;
/// where the ground of one of them is shared, the linked section is taken with it or the hold is cut short
/// of the mark (<see cref="LaneOccupancy"/>), and that is the whole of how two ways that share ground meet.
/// </para>
/// <para>
/// <b>Being over each other is mutual, and the table is symmetric because of it.</b> A pair of ribbons makes
/// one pair of sections, filed under both ways — so what one holder needs of the other's way is exactly the
/// ground the other finds linked back to its own.
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

    /// <summary>
    /// <b>A town whose ways share no ground</b> — a fixture that stands one carriageway and nothing else. It is
    /// an empty table and never an absent one, so that the reservations ask the same question of a laboratory
    /// map as of a city and no reader has to know which it is on.
    /// </summary>
    public static WayCrossings None { get; } = new([0], []);

    /// <summary>How many ways the table is laid over, which is the whole of the reservations' own numbering.</summary>
    public int WayCount => _offsets.Length - 1;

    /// <summary>The marks along one way, one per other way its ribbon lies over, nearest first.</summary>
    public ReadOnlySpan<CrossedSection> Of(int way) =>
        way < 0 || way + 1 >= _offsets.Length
            ? []
            : _sections.AsSpan(_offsets[way], _offsets[way + 1] - _offsets[way]);

    /// <summary>
    /// <b>The runs of a way's own line the marks fall on</b>, in that way's own metres. Empty where its ribbon
    /// lies over nothing.
    /// </summary>
    public ReadOnlySpan<OwnRun> OwnRuns(int way) =>
        way < 0 || way + 1 >= _ownOffsets.Length
            ? []
            : _ownRuns.AsSpan(_ownOffsets[way], _ownOffsets[way + 1] - _ownOffsets[way]);

    /// <summary>
    /// How many marks the busiest way carries — <b>how many linked sections one piece of a hold may write</b>,
    /// which sizes the planned layer.
    /// </summary>
    public int MostCrossedByOne { get; init; }

    /// <summary>And how many runs of its own way the busiest one has.</summary>
    public int MostOwnRuns { get; }
}
