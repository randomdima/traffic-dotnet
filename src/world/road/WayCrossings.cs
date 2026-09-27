namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>One mark: a stretch of one way whose ribbon lies over a section of another's</b>, in both ways' own
/// metres — <see cref="MineFromM"/> to <see cref="MineToM"/> of the way it is filed under, and
/// <see cref="FromM"/> to <see cref="ToM"/> of <see cref="OnWay"/>.
/// </summary>
/// <remarks>
/// <b>Both sides, because a shared piece of ground is one place and a holder passes it once.</b> Its own
/// metres are where a main claim on the filed way places a secondary claim; the other metres are the section
/// that secondary claim covers (TER-5c.1).
/// </remarks>
/// <param name="OnWay">The way whose section it is, numbered as the reservations number ways (<see cref="TownWays"/>).</param>
internal readonly record struct CrossedSection(int OnWay, float FromM, float ToM, float MineFromM, float MineToM);

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
/// <b>It is looked up and never written into.</b> A hold's main claims are laid on the ways its holder drives
/// or walks; where the ground of one of them is shared, a secondary claim over the other way's section is
/// placed with it (<see cref="LaneOccupancy"/>), and that is the whole of how two ways that share ground meet.
/// </para>
/// <para>
/// <b>Being over each other is mutual, and the table is symmetric because of it.</b> A pair of ribbons makes
/// one pair of sections, each with ground in it, filed under both ways — so a main claim over one side of a
/// mark meets, on its own way, the secondary claim of whatever holds the other side. <b>That is what lets a
/// main claim be answered off its own way alone.</b>
/// </para>
/// </remarks>
internal sealed class WayCrossings
{
    readonly int[] _offsets;
    readonly CrossedSection[] _sections;

    public WayCrossings(int[] offsets, CrossedSection[] sections)
    {
        _offsets = offsets;
        _sections = sections;
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
    /// How many marks the busiest way carries — <b>how many secondary claims one main claim may place</b>,
    /// which sizes the planned layer.
    /// </summary>
    public int MostCrossedByOne { get; init; }
}
