using System.Numerics;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>What one laying of the shell came to</b> (<see cref="LaneShell"/>): how the stretches were paired,
/// which runs closed, and what was thrown away.
/// </summary>
/// <remarks>
/// <para>
/// <b>It exists because the answer is lossy.</b> What <see cref="LaneShell.Chains"/> hands over is the
/// rings that shut, so a reader looking at a lane the layer does not mark cannot tell a lane that was
/// never the outside from one whose whole run was dropped for a fault half a town away — and a run is
/// most of a ring road's lanes.
/// </para>
/// <para>
/// <b>A reading and not a claim.</b> Nothing here gates anything and nothing is asserted off it; it is
/// what <c>--bench shell</c> prints so a fault in the shell is looked at rather than guessed at.
/// </para>
/// </remarks>
internal sealed class ShellReading
{
    readonly List<ShellRun> _runs = [];
    readonly List<ShellLooseEnd> _loose = [];
    readonly List<ShellStraight> _handed = [];
    readonly List<ShellStretch> _stood = [];
    readonly List<float> _met = [];

    /// <summary>How many driven lines were walked, and how many stretches of them came back as the outside.</summary>
    public int Lines { get; private set; }

    public int Stretches { get; private set; }

    /// <summary>How many stretches were handed on to another at a crossing of two band edges, which is the ordinary case.</summary>
    public int MetAtACrossing { get; private set; }

    /// <summary>And how many over ground nothing is driven along — the back of a car park, the end of a road.</summary>
    public int JoinedAcrossACut { get; private set; }

    /// <summary>
    /// <b>How many hand-overs came to one point</b>, and how many left a straight for the ring to cover.
    /// </summary>
    /// <remarks>
    /// A stretch carried to where its own line crosses the next one stops on the very point that one
    /// starts from, so a straight is what is left where there was no crossing to carry to — two lines that
    /// only ever touch, or that cross too far off to be this corner. <b>It is the reading that says how
    /// much of the shell is solved and how much is drawn across</b>, and the longest straight is the worst
    /// one length of it.
    /// <b>One point is one place and not one float</b> (<see cref="Kerbs.OnePlaceM"/>): a crossing solved
    /// at a town's own coordinates is right to a few millimetres, which is all seven figures carry out
    /// there, so counted to the rounding a thousand corners that met exactly read as straights.
    /// </remarks>
    public int MetAtAPoint { get; private set; }

    public int LeftAStraight { get; private set; }

    /// <summary>
    /// <b>How far apart the two ends of every meeting actually stood</b>, in the order they were paired.
    /// </summary>
    /// <remarks>
    /// Two stretches that meet at a crossing of two band edges stop at <em>one point</em>, so this is the
    /// error in finding it and nothing else. <b>It is what says whether the cut is solved or searched
    /// for</b>: the radius the pairing offers (<c>LaneShell.MeetM</c>) has to cover the worst of these, and a
    /// radius wide enough to cover a decimetre is wide enough to wire a car park's bays to one another.
    /// </remarks>
    public IReadOnlyList<float> MetAtM => _met;

    public void Met(float apartM) => _met.Add(apartM);

    /// <summary>
    /// <b>How many of the cuts were solved against a band's own boundary and how many were bisected</b>
    /// (<see cref="LaneShell.Crossing"/>). What is left to the walk is the ground no line lays a band on —
    /// the apron rounding a junction corner — so a town whose bisected count is more than a few has lines
    /// whose edges could not be offset at all.
    /// </summary>
    public int CutSolved { get; private set; }

    public int CutWalked { get; private set; }

    public void Cut(bool solved)
    {
        if (solved) CutSolved++;
        else CutWalked++;
    }

    /// <summary>Every hand-over strung into a run, with the straight it left and the turn the ring takes at it.</summary>
    public IReadOnlyList<ShellStraight> Handovers => _handed;

    /// <summary>Every run walked, in the order they were walked, whether or not it was kept.</summary>
    public IReadOnlyList<ShellRun> Runs => _runs;

    /// <summary>
    /// <b>Every stretch as the carry left it</b>, which is what a reader looking at one place needs: the
    /// stretches standing there, what each one carries the outside on to, and what it got for a corner.
    /// </summary>
    public IReadOnlyList<ShellStretch> Stood => _stood;

    public void Stands(ShellStretch stretch) => _stood.Add(stretch);

    /// <summary>Every stretch the pairing left carrying the outside on to nothing.</summary>
    public IReadOnlyList<ShellLooseEnd> LooseEnds => _loose;

    public void Walked(int lines, int stretches)
    {
        Lines = lines;
        Stretches = stretches;
    }

    /// <summary>
    /// <b>How many stretches were carried away to nothing</b>: cut back at both ends until no metres of
    /// their own were left. The ring walks straight past one, so the straight it draws there spans a
    /// hand-over nobody matched — a notch with a corner recorded on either side of it.
    /// </summary>
    public int CarriedAway { get; set; }

    public void Paired(int metAtACrossing, int joinedAcrossACut)
    {
        MetAtACrossing = metAtACrossing;
        JoinedAcrossACut = joinedAcrossACut;
    }

    public void Loose(ShellLooseEnd end) => _loose.Add(end);

    /// <summary>One hand-over, as the straight it left the ring to cover and the turn the ring takes at it.</summary>
    public void Handed(
        int line, int other, Vector2 fromM, Vector2 toM, float turnRad, bool acrossACut, ShellCorner corner,
        float carriedToM)
    {
        var straightM = Vector2.Distance(fromM, toM);
        _handed.Add(
            new ShellStraight(line, other, fromM, toM, straightM, turnRad, acrossACut, corner, carriedToM));
        if (straightM <= Kerbs.OnePlaceM) MetAtAPoint++;
        else LeftAStraight++;
    }

    /// <summary>One run recorded as it is walked, and the handle its verdict is set through once it has one.</summary>
    public int Ran(ShellRun run)
    {
        _runs.Add(run);
        return _runs.Count - 1;
    }

    public void Verdict(int run, bool kept, string why)
    {
        _runs[run].Kept = kept;
        _runs[run].Why = why;
    }

    /// <summary>
    /// <b>The lines that are the outside somewhere and are in no ring that was kept.</b> It is the reading
    /// the whole instrument is opened for: a lane missing from the layer is either one of these or one that
    /// never had an edge on the boundary at all.
    /// </summary>
    public int[] Lost()
    {
        var kept = new HashSet<int>();
        var dropped = new HashSet<int>();
        foreach (var run in _runs)
        {
            foreach (var line in run.Lines) (run.Kept ? kept : dropped).Add(line);
        }

        dropped.ExceptWith(kept);
        var lost = dropped.ToArray();
        Array.Sort(lost);
        return lost;
    }
}

/// <summary>One run of stretches strung end to end, and what became of it.</summary>
internal sealed class ShellRun
{
    /// <summary>How many stretches were strung into it, and which driven lines those came off.</summary>
    public int Stretches { get; init; }

    public int[] Lines { get; init; } = [];

    public float LengthM { get; init; }

    /// <summary>How far its last arc stopped from where its first set off, before anything was done about it.</summary>
    public float GapM { get; init; }

    /// <summary>
    /// And where those two stand — <b>the place to point a picture at</b>, since a run that would not close
    /// is a length of the town's edge nothing accounts for and the fault is in the ground there.
    /// </summary>
    public Vector2 HeadM { get; init; }

    public Vector2 TailM { get; init; }

    /// <summary>Whether that gap was shut with a straight rather than being closed by the walk itself.</summary>
    public bool ShutOverAGap { get; init; }

    /// <summary>
    /// And how many metres of that straight run over ground nothing is driven on. <b>A figure with no rule
    /// behind it</b>: every other join in the shell is refused unless the ground carries it
    /// (<c>LaneShell.Carries</c>) and the one that shuts a ring is asked nothing, so this says what that
    /// costs on a real town.
    /// </summary>
    public float BareGapM { get; init; }

    public bool Kept { get; set; }

    public string Why { get; set; } = "not judged";
}

/// <summary>
/// One end the pairing left over, with how far off the nearest end that could have taken the outside over
/// stands.
/// </summary>
/// <remarks>
/// <b>The distance is the diagnosis.</b> A partner a millimetre off that was not taken is the pairing's
/// fault; one a street off is a hole in the ground under the reading.
/// </remarks>
internal readonly record struct ShellLooseEnd(
    int Line, Vector2 AtM, float NearestM, int NearestLine, bool NearestWasTaken);

/// <summary>
/// One hand-over inside a run: the line it left, the one it arrived on, the two points between them, and
/// the turn the ring takes there.
/// </summary>
/// <remarks>
/// <b>Two faults read off one record.</b> A stretch carried to where its own line crosses the next one
/// stops on the very point that one starts from, so a <see cref="LengthM"/> above the rounding is a
/// crossing that was not solved and every metre of it is perimeter with no lane under it. And a
/// <see cref="TurnRad"/> near a half turn is the ring doubling back on itself — a line carried past the
/// meeting with a straight coming back to it, which is a spike out of a corner however short it is.
/// <b>Both readings are only faults where the two lines were meant to cross</b>
/// (<see cref="AcrossACut"/>): the outside goes up one bay of a car park and back down the next, and that
/// is a half turn over a straight because the ground there really is cut.
/// </remarks>
/// <param name="CarriedToM">
/// <b>How far apart the carry left the two ends</b>, which is what <see cref="LengthM"/> would be if the
/// ring were strung exactly the way the carry left it. The two disagreeing is a straight the stringing
/// made rather than the corner.
/// </param>
internal readonly record struct ShellStraight(
    int Line, int OtherLine, Vector2 FromM, Vector2 ToM, float LengthM, float TurnRad, bool AcrossACut,
    ShellCorner Corner, float CarriedToM);

/// <summary>
/// <b>One stretch of the walk, as the carry left it</b>: which line it is of and over which of its metres,
/// where its own two band edges stopped, what it carries the outside on to and what that corner came to.
/// </summary>
/// <remarks>
/// <b>The edges are what the pairing saw and the metres are what is drawn</b>, and a fault in the shell is
/// nearly always the two disagreeing: a hand-over is offered on how near two <em>edges</em> stand and drawn
/// as the straight between two <em>lines</em>, so a pair that met exactly can still leave a band's width of
/// straight behind it.
/// </remarks>
/// <param name="Follows">The stretch this one carries the outside on to, or -1 where it carries it to nothing.</param>
internal readonly record struct ShellStretch(
    int At,
    int Line,
    bool OnTheLeft,
    float FromM,
    float ToM,
    Vector2 ArrivesAtM,
    Vector2 LeavesAtM,
    int Follows,
    ShellCorner Corner,
    bool AcrossACut,
    bool Dropped,
    float GapM);

/// <summary>
/// <b>What the two lines of one hand-over had to offer as a corner.</b> A straight was drawn instead of a
/// point either because there was no crossing to carry to or because the crossing there was belonged to
/// some other corner, and the two are different faults with different fixes.
/// </summary>
internal enum ShellCorner
{
    /// <summary>The two lines cross and the ring was carried onto the crossing, so there is no straight.</summary>
    Crossed,

    /// <summary>The two lines never cross, run on past their own ends and all.</summary>
    Apart,

    /// <summary>
    /// The two distances that came back name two places rather than one, so the pieces disagree about where
    /// their own crossing is and neither answer is a point the ring could turn on.
    /// </summary>
    Scattered,

    /// <summary>
    /// The crossing stands further from one of the two stops than the wedge between the bands reaches, so it
    /// is some other corner's and not this one's.
    /// </summary>
    Wedged,

    /// <summary>
    /// One of the two lines still has its own edge on the boundary between its stop and the crossing, so a
    /// length of the outside stands in front of it and the corner belongs past that.
    /// </summary>
    Covered,

    /// <summary>
    /// Both lines had stopped before the crossing, so it stands where their two extensions meet and on
    /// neither of them — the corner between two arms that both end at a junction, which the fillet rounds
    /// and no line runs along.
    /// </summary>
    Beyond,
}
