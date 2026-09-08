using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The pavement, laid once as the band the walk runs down</b> (TER-3c): the town's own tarmac —
/// <see cref="Kerbs"/>'s list — wrapped at half a walk, and the runs of that wrap that are really the
/// outside of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a step and not a derivation two readers each do for themselves.</b> The shape of the pavement
/// used to be written down twice — once as the draw calls that lay it (<c>GroundMesh.Build</c>) and once as
/// the coverage tests that answer what the ground is at a point (<see cref="GroundShapes"/>) — in two
/// slices, at two tiers, kept in step by nobody but whoever remembered. That is the figure that exists in
/// two places and eventually disagrees with itself: a band widened in the picture and not in the answer is
/// a walker refused ground it can see it is standing on. Laid here, the two read one list.
/// </para>
/// <para>
/// <b>The pavement is the ground within half a walk of <see cref="Walk"/>, and it is nothing else</b>
/// (TER-3c.3). That line is the town's tarmac wrapped at half a walk and cut to the runs no tarmac stands
/// nearer to — <em>the same line the walking lanes are laid on</em> — so the band is a walk wide the whole
/// way round, the kerb is its inner edge, the shell against the grass is its outer edge, and a walker walks
/// down the middle of it.
/// </para>
/// <para>
/// <b>The carriageway ends where the pavement starts</b> (TER-3c.7). Everything inside the kerb is tarmac,
/// including the pockets the town's own pieces leave between them — a movement narrower than the arm it
/// leaves, a car park set back off the street it fronts. Drawn as the tarmac's own outline instead, the
/// kerb stepped and chamfered its way round every one of those while the shell and the lane beside it ran
/// straight past, and the band came out a different width at every mouth in the town.
/// </para>
/// <para>
/// <b>A junction has no piece here, because a junction has no shape</b> (TER-5): the ground inside a box is
/// the ground its own movements take (<see cref="LaneLines"/>) and the fillets that round the wedges
/// between its arms, and each of those is wrapped like any other piece of tarmac.
/// </para>
/// </remarks>
internal sealed class Paving
{
    Paving(
        float walkM, GroundPieces pieces, LaneLines lanes, Kerbs kerbs, PavedRun[] walk, ArcSeg[][] corners,
        PavedCap[] caps, PavedSection[] sections, bool[] through, bool[] openEnds,
        PavedBox[] boxes, float[] enterM, float[] exitM, PavedStub[] stubs, int[] next, int[] turnFrom)
    {
        WalkM = walkM;
        Of = pieces;
        Lanes = lanes;
        Kerbs = kerbs;
        Walk = walk;
        Corners = corners;
        Caps = caps;
        Sections = sections;
        Through = through;
        OpenEnds = openEnds;
        Boxes = boxes;
        EnterM = enterM;
        ExitM = exitM;
        Stubs = stubs;
        Next = next;
        TurnFrom = turnFrom;
    }

    /// <summary>The shapes the pavement was laid off, for a reader that wants the road a band belongs to.</summary>
    public GroundPieces Of { get; }

    /// <summary>
    /// <b>The lines the town is driven on</b>, laid once here and read by everything that needs the tarmac's
    /// own shape rather than the records it was drawn from.
    /// </summary>
    public LaneLines Lanes { get; }

    /// <summary>The tarmac as one shape, for whoever wants to ask how far off it a point stands.</summary>
    public Kerbs Kerbs { get; }

    /// <summary>
    /// <b>The line the pavement is walked down</b>: the tarmac's outline at half a walk, cut into the runs
    /// of it that are really the outside. The band is everything within half a walk of these.
    /// </summary>
    public PavedRun[] Walk { get; }

    /// <summary>
    /// <b>Where two runs give way to one another, the line the band's inner edge turns on</b>: half a walk
    /// about the place each of them stops at, <b>from the point one run's kerb stops at to the point the next
    /// one's starts</b> — so the inner edge is one line the whole way round the town, and every piece of it
    /// begins where the piece before it ended.
    /// </summary>
    /// <remarks>
    /// <b>The inner edge is an offset, and two offsets of a corner do not meet.</b> Where the tarmac turns a
    /// concave corner — a car park set back off the street it fronts, a mouth between two arms — the wrap at
    /// half a walk turns the same corner sharply, and each run's edge stops <em>half a walk short of it</em>
    /// along its own arm. Struck run by run, the kerb line came out missing an L of itself, two metres on a
    /// side, at every car park in the town.
    /// <para>
    /// The band itself never was: a run's end is closed with the half-round the answer measures there
    /// (<c>GroundShapes.Paved</c>), and this is those rounds' own rim. <b>Two rounds and not one</b>, because
    /// the two runs stop as much as <see cref="Kerbs.OnePlaceM"/> apart — so a turn is one arc where they
    /// stopped at one point and two where they did not, the second taking over where the two rounds cross.
    /// One arc laid through both ends instead would stand half that outside the band in the middle, and the
    /// pavement drawn would be wider than the pavement answered.
    /// </para>
    /// </remarks>
    public ArcSeg[][] Corners { get; }

    /// <summary>
    /// <b>The ends the band really stops at, and the way it faces there</b>: what closes a run with the
    /// half-round the answer measures past it (<c>GroundShapes.Paved</c>).
    /// </summary>
    /// <remarks>
    /// <b>An end another run sets off from is not one of them.</b> Most of the ends in a town are seams and
    /// not stops — a wrapping line that closes on itself is cut in two so that it can be walked
    /// (<c>Kerbs.Spans</c>), and a line cut where it dives inside a neighbour resumes where it comes back
    /// out — and a round struck at a seam is a circle of pavement laid inside pavement, which is nothing on
    /// the ground and a full fan in the mesh.
    /// </remarks>
    public PavedCap[] Caps { get; }

    /// <summary>
    /// <b>What stands either side of a road, as the stretches over which the answer does not change</b>:
    /// whether the pavement there is that road's own offset, and whether its outer edge is the town's own
    /// outline. Every road is covered end to end, by one section where nothing beside it changes and by
    /// several where something does.
    /// </summary>
    /// <remarks>
    /// <b>It is the same question <see cref="Walk"/> is cut by</b> (<see cref="Kerbs.Clear"/>), asked along
    /// the road instead of along the offset — so a run that wraps a road and the sections of that road are
    /// two readings of one predicate and cannot disagree about where the pavement is.
    /// <para>
    /// What it is for is the picture. <b>A road's whole cross-section is struck off the road's own arcs at
    /// one set of stations</b> (TER-7b): the carriageway, the two kerb lines, the two bands of pavement and
    /// the two rims are bands between consecutive offsets of one curve, so no two of them overlap and no
    /// seam between them can open. Drawn as separate shapes each sampled to its own curvature, the kerb
    /// line and the carriageway it abuts stand a chord's sag apart, and the only ways to close that are to
    /// overlap them or to show the ground beneath.
    /// </para>
    /// </remarks>
    public PavedSection[] Sections { get; }

    /// <summary>
    /// <b>The junctions a road runs through as one line</b>, one answer per junction of the plan
    /// (<see cref="RoadCuts.RunsThrough"/>). Such a box paves no ground of its own: the two sections meet
    /// edge to edge, every movement through it runs inside them, and nothing of it is a piece of the
    /// tarmac's outline (<see cref="Kerbs"/>) or of the picture.
    /// </summary>
    public bool[] Through { get; }

    /// <summary>
    /// <b>Which ends of which roads reach the town's outline</b>, two answers per road — its start, then
    /// its end: whether any of the line that turns round that end at half a walk (TER-3c.6) is a run of the
    /// pavement. An end that hands over to a box wider than itself, or to a street across it, is buried in
    /// what it meets, and what stands round it is that ground's own.
    /// </summary>
    public bool[] OpenEnds { get; }

    /// <summary>
    /// <b>Every junction's own ground as one closed outline</b> (<see cref="CityGen.Boxes"/>): what the
    /// picture lays once where the arms' sections stop, so that the ground inside a box is covered once
    /// (TER-7b). A dead end and a junction the road runs through as one line have none.
    /// </summary>
    public PavedBox[] Boxes { get; }

    /// <summary>How far into each road's start its box reaches — where the road's own section begins.</summary>
    public float[] EnterM { get; }

    /// <summary>And how far into each road's end, where the section ends.</summary>
    public float[] ExitM { get; }

    /// <summary>The sides that run on past a cut into a box with their own concrete beside them (<see cref="PavedStub"/>).</summary>
    public PavedStub[] Stubs { get; }

    /// <summary>
    /// <b>Which end each end of each run hands over to</b> — both ends of every run, the start then the
    /// end, so end <c>2·run</c> is a run's start and <c>2·run + 1</c> its end — whether by carrying
    /// straight on or by a turn (<see cref="Corners"/>), or <see cref="CityPlan.NoRecord"/> where the band
    /// really stops (<see cref="Caps"/>). It is the kerb line as one line the whole way round the town,
    /// which is what a box's own outline is walked along.
    /// </summary>
    public int[] Next { get; }

    /// <summary>The turn laid from each end, as an index into <see cref="Corners"/>, or <see cref="CityPlan.NoRecord"/>.</summary>
    public int[] TurnFrom { get; }

    /// <summary>
    /// How wide the band is. <b>The map's own figure where it has one</b>, and the town's where it does not,
    /// so a map laid without a pavement of its own is walked at the same width it is drawn.
    /// </summary>
    public float WalkM { get; }

    public static Paving Lay(GroundPieces pieces, SimConfig config)
    {
        var walkM = pieces.PavementWidthM > 0f ? pieces.PavementWidthM : config.PavementWidthM;
        var lanes = LaneLines.Of(pieces, config);
        var through = RoadCuts.RunsThrough(pieces);
        var kerbs = Kerbs.Of(pieces, lanes, through);

        // <b>Welded at one place and not at a rounding</b> (<see cref="Kerbs.OnePlaceM"/>). A wrapping line
        // that meets another tangentially runs that far past the point they cross before it is a rounding
        // inside it, so every graze in the town left a span of a few centimetres standing as outline — a run
        // whose two ends are one place, which is a walk-wide round of pavement struck off nothing. Odesa laid
        // thirteen hundred of them, a third of all its runs, for a tenth of a percent of its pavement.
        var wraps = new List<Kerbs.Wrap>();
        kerbs.Shell(walkM * 0.5f, Kerbs.OnePlaceM, null, wraps);
        Corner.WeldTheEnds(wraps);

        var walk = new PavedRun[wraps.Count];
        for (var run = 0; run < wraps.Count; run++)
        {
            var line = wraps[run].Line;
            var lengthM = Spline.TotalLengthM(line);
            var on = Spline.SampleAt(line, lengthM * 0.5f);
            var side = kerbs.OffTheTarmacM(on.PositionM + (on.Right * walkM * 0.25f))
                       <= kerbs.OffTheTarmacM(on.PositionM - (on.Right * walkM * 0.25f))
                ? 1f
                : -1f;

            // A run along a road's side is the road's own cross-section to draw; one that turns round a
            // road's end — a dead end's head — is a run like any other, and its concrete and rim are its own.
            var aRoad = wraps[run].Piece < kerbs.Roads;
            walk[run] = new PavedRun(
                line, lengthM, side,
                kerbs.OffTheTarmacM(on.PositionM - (on.Right * side * ((walkM * 0.5f) + AHairM))) > walkM,
                aRoad && wraps[run].End == Kerbs.Wrap.ASide, aRoad ? kerbs.IndexOf(wraps[run].Piece) : CityPlan.NoRecord);
        }

        // Which arms' ends reach the town's outline: an end is drawn grown by a walk only where the line
        // that turns round it is somewhere the outline (TER-3c.5, TER-3c.6).
        var openEnds = new bool[pieces.Roads.Count * 2];
        foreach (var wrap in wraps)
        {
            if (wrap.Piece < kerbs.Roads && wrap.End != Kerbs.Wrap.ASide) openEnds[(kerbs.IndexOf(wrap.Piece) * 2) + wrap.End] = true;
        }

        var corners = Corner.Turns(walk, walkM * 0.5f, out var turned, out var next, out var turnFrom);
        var caps = Corner.Stops(walk, turned, next);
        var enterM = new float[pieces.Roads.Count];
        var exitM = new float[pieces.Roads.Count];
        var stubs = new List<PavedStub>();
        var boxes = CityGen.Boxes.Lay(pieces, config, through, walk, corners, next, turnFrom, walkM * 0.5f, enterM, exitM, stubs);
        return new Paving(
            walkM, pieces, lanes, kerbs, walk, corners, caps, Cut(pieces, kerbs, walkM), through, openEnds,
            boxes, enterM, exitM, stubs.ToArray(), next, turnFrom);
    }


    /// <summary>
    /// Every road cut into the stretches over which what stands beside it does not change
    /// (<see cref="Sections"/>).
    /// </summary>
    /// <remarks>
    /// <b>Walked at the same pitch the outline is and cut by the same bisection</b> (<see cref="Kerbs"/>),
    /// so where a section ends and where the run beside it ends are the same millimetre. Asked of the pair
    /// of sides at once: the two answers change within a station of each other wherever anything changes at
    /// all, and a section per side would cut the carriageway between them into two sets of stations, which
    /// is the very seam this exists to close.
    /// </remarks>
    static PavedSection[] Cut(GroundPieces pieces, Kerbs kerbs, float walkM)
    {
        var sections = new List<PavedSection>();
        for (var road = 0; road < pieces.Roads.Count; road++)
        {
            var arcs = pieces.Roads.SegmentsOf(road);
            var lengthM = Spline.TotalLengthM(arcs);
            if (arcs.Length == 0 || lengthM <= 0f) continue;

            var halfM = pieces.Roads.WidthM[road] * 0.5f;
            var stations = Math.Max(1, (int)MathF.Ceiling(lengthM / Kerbs.StationM));
            var openedAtM = 0f;
            var was = Beside(kerbs, arcs, halfM, walkM, 0f);
            for (var station = 1; station <= stations; station++)
            {
                var alongM = lengthM * station / stations;
                var here = Beside(kerbs, arcs, halfM, walkM, alongM);
                if (here == was) continue;

                var edgeM = Crossing(
                    kerbs, arcs, halfM, walkM, lengthM * (station - 1) / stations, alongM, was);
                sections.Add(new PavedSection(road, openedAtM, edgeM, was.Left, was.Right));
                openedAtM = edgeM;
                was = here;
            }

            sections.Add(new PavedSection(road, openedAtM, lengthM, was.Left, was.Right));
        }

        return sections.ToArray();
    }

    /// <summary>What stands either side of one point of a road, read at that point and nowhere else.</summary>
    static (PavedEdge Left, PavedEdge Right) Beside(
        Kerbs kerbs, ReadOnlySpan<ArcSeg> arcs, float halfM, float walkM, float alongM)
    {
        var on = Spline.SampleAt(arcs, alongM);
        return (Edge(kerbs, on.PositionM, -on.Right, halfM, walkM), Edge(kerbs, on.PositionM, on.Right, halfM, walkM));
    }

    static PavedEdge Edge(Kerbs kerbs, Vector2 onM, Vector2 outward, float halfM, float walkM)
    {
        if (!kerbs.Clear(onM + (outward * (halfM + (walkM * 0.5f))), walkM * 0.5f)) return PavedEdge.None;

        return kerbs.Clear(onM + (outward * (halfM + walkM)), walkM) ? PavedEdge.WalkAndRim : PavedEdge.Walk;
    }

    /// <summary>Where between two stations the answer changed, bisected to the rounding the outline is cut at.</summary>
    static float Crossing(
        Kerbs kerbs, ReadOnlySpan<ArcSeg> arcs, float halfM, float walkM, float fromM, float toM,
        (PavedEdge Left, PavedEdge Right) was)
    {
        for (var round = 0; round < Kerbs.BisectionRounds; round++)
        {
            var middleM = (fromM + toM) * 0.5f;
            if (Beside(kerbs, arcs, halfM, walkM, middleM) == was) fromM = middleM;
            else toM = middleM;
        }

        return (fromM + toM) * 0.5f;
    }

    /// <summary>
    /// A step past the edge of the band, for asking what is on the other side of it. Small enough that a
    /// point that far beyond the shell is beyond nothing else, and large enough to clear the rounding two
    /// computations of one distance disagree by (<see cref="Kerbs.RoundingM"/>).
    /// </summary>
    const float AHairM = 0.05f;
}

/// <summary>
/// <b>One stretch of one road, and what stands either side of it</b> — the unit the picture strikes a
/// road's cross-section over (<see cref="Paving.Sections"/>). The distances are along the road's own arcs.
/// </summary>
public readonly record struct PavedSection(int Road, float FromM, float ToM, PavedEdge Left, PavedEdge Right);

/// <summary>What one side of a road carries over one stretch of it.</summary>
public enum PavedEdge : byte
{
    /// <summary>
    /// <b>Something else stands against the kerb</b> — a car park set back off the street, the ground
    /// between two of the town's own pieces. What is there is tarmac and not concrete (TER-3c.7).
    /// </summary>
    None,

    /// <summary>The pavement, with more of the town beyond it rather than the verge.</summary>
    Walk,

    /// <summary>The pavement, and the edge line that rims the town where the grass begins.</summary>
    WalkAndRim,
}

/// <summary>
/// One end of one run as the kerb it hands over: the place the run stops at, which way the road lies from
/// there, and whether the arc that carries the band's inner edge round that place sets off here or arrives
/// here (<see cref="Paving.Corners"/>).
/// </summary>
/// <remarks>
/// <b>Which of the two it is falls out of the run's own bearing.</b> The edge leaves the place along the
/// run, so the arc has to meet it going the same way: at a kerb end standing to the road's side of the
/// line, that is the turn's own direction where the road lies a quarter turn <em>behind</em> the way the
/// run sets off, and against it where the road lies ahead.
/// </remarks>
readonly record struct Corner(int Run, Vector2 PlaceM, Vector2 RoadwardM, Vector2 AwayM, bool SetsOff)
{
    /// <summary>
    /// <b>Half a turn is not a corner between two runs.</b> Two runs that hand over make a wedge of tarmac
    /// between them and the turn crosses it, which is less than half the circle however sharply they meet;
    /// a turn past that is an end paired with one it does not meet at all.
    /// </summary>
    const float MostRad = MathF.PI;

    /// <summary>
    /// <b>How far two bearings may stand apart and still be one bearing</b>, as the length of the two unit
    /// vectors' sum where they face opposite ways. A wrap and the runs it is cut into are one line, so two
    /// runs that carry on from one another agree to the last bits of a float — a thousandth is a couple of
    /// hundred times that, and still a hundredth of the way to the nearest pair that only <em>nearly</em>
    /// line up. What it costs where it is wrong is half a walk times this: two millimetres of wedge.
    /// </summary>
    const float OneBearing = 0.001f;

    /// <summary>
    /// <b>How far apart two ends stop where their lines cross</b>: each line is cut where it is a joining's
    /// width inside the other's band (<see cref="Kerbs.JoinedM"/>), which is that far past the crossing
    /// along its own line, so the two ends stand that apart and up to root two of it at a right angle —
    /// and a rounding on top.
    /// </summary>
    static readonly float CrossedM = (Kerbs.JoinedM * MathF.Sqrt(2f)) + Kerbs.RoundingM;

    /// <summary>
    /// <b>Two ends that stop where their lines cross are made to stop at one point.</b> Cut a centimetre
    /// inside one another's bands they stand up to <see cref="CrossedM"/> apart, and a turn struck about one
    /// of them meets the other's band a centimetre short of its start: a sliver the width of that offset
    /// and a walk long that nothing draws. The end that stops second is moved onto the first, keeping its
    /// arc's curvature and its other end where they were.
    /// </summary>
    public static void WeldTheEnds(List<Kerbs.Wrap> wraps)
    {
        var ends = new List<(int Run, bool AtStart, Vector2 PlaceM)>(wraps.Count * 2);
        for (var run = 0; run < wraps.Count; run++)
        {
            ends.Add((run, true, wraps[run].Line[0].StartM));
            ends.Add((run, false, wraps[run].Line[^1].EndM));
        }

        var cells = new Dictionary<(int X, int Y), List<int>>();
        for (var end = 0; end < ends.Count; end++)
        {
            var cell = Cell(ends[end].PlaceM);
            if (!cells.TryGetValue(cell, out var here)) cells[cell] = here = [];
            here.Add(end);
        }

        var welded = new bool[ends.Count];
        for (var end = 0; end < ends.Count; end++)
        {
            if (welded[end]) continue;

            var cell = Cell(ends[end].PlaceM);
            var nearest = -1;
            var nearestM = CrossedM;
            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    if (!cells.TryGetValue((cell.X + x, cell.Y + y), out var here)) continue;

                    foreach (var other in here)
                    {
                        if (ends[other].Run == ends[end].Run || welded[other]) continue;

                        var apartM = Vector2.Distance(ends[other].PlaceM, ends[end].PlaceM);
                        if (apartM <= 0f || apartM > nearestM) continue;

                        nearestM = apartM;
                        nearest = other;
                    }
                }
            }

            if (nearest < 0) continue;

            var (run, atStart, _) = ends[nearest];
            var line = wraps[run].Line;
            var ontoM = ends[end].PlaceM;
            if (atStart) line[0] = Through(line[0].Curvature, ontoM, line[0].EndM);
            else line[^1] = Through(line[^1].Curvature, line[^1].StartM, ontoM);
            ends[nearest] = (run, atStart, ontoM);
            welded[end] = true;
            welded[nearest] = true;
        }
    }

    /// <summary>The arc of one curvature from one point to another: its heading and length off the chord.</summary>
    static ArcSeg Through(float curvature, Vector2 fromM, Vector2 toM)
    {
        var chordM = toM - fromM;
        var chordLengthM = chordM.Length();
        var chordRad = MathF.Atan2(chordM.Y, chordM.X);
        if (MathF.Abs(curvature) < 1e-6f) return new ArcSeg(fromM, chordRad, chordLengthM, 0f);

        var lengthM = 2f / curvature * MathF.Asin(Math.Clamp(curvature * chordLengthM * 0.5f, -1f, 1f));
        return new ArcSeg(fromM, chordRad - (curvature * lengthM * 0.5f), lengthM, curvature);
    }

    /// <param name="turned">
    /// Which of the runs' ends — both ends of every run, in <see cref="EveryEnd"/>'s order — a turn was
    /// laid from or onto. Such an end is closed by the turn's own wedge and not by a round of its own.
    /// </param>
    /// <param name="next">Which end each turned end hands over to (<see cref="Paving.Next"/>), or <see cref="CityPlan.NoRecord"/>.</param>
    /// <param name="turnFrom">The turn laid from each end (<see cref="Paving.TurnFrom"/>), or <see cref="CityPlan.NoRecord"/>.</param>
    public static ArcSeg[][] Turns(
        PavedRun[] walk, float halfWalkM, out bool[] turned, out int[] next, out int[] turnFrom)
    {
        var ends = EveryEnd(walk);
        turned = new bool[ends.Count];
        next = new int[ends.Count];
        turnFrom = new int[ends.Count];
        Array.Fill(next, CityPlan.NoRecord);
        Array.Fill(turnFrom, CityPlan.NoRecord);
        if (halfWalkM <= 0f) return [];

        var places = PlacesOf(ends);

        // <b>An end is turned onto once.</b> Four runs meet at a place where two streets and a car park's
        // wrap all give way together, and two of them left to pick the shortest turn each picked the same
        // one — which laid that corner twice and left the other kerb stopping dead.
        var taken = new bool[ends.Count];
        var turns = new List<ArcSeg[]>();
        for (var end = 0; end < ends.Count; end++)
        {
            if (!ends[end].SetsOff) continue;

            var onto = ends[end].Onto(ends, taken, places, halfWalkM);
            if (onto < 0) continue;

            // Handed over is handed over whether or not the turn has any length: where the two kerbs are
            // one line — an arm's and the fillet's at their tangent — the bands abut and there is no wedge,
            // and a round struck at each end anyway was two fans over one point at every corner.
            taken[onto] = true;
            turned[end] = true;
            turned[onto] = true;
            next[end] = onto;
            next[onto] = end;

            var turn = ends[end].To(ends[onto], halfWalkM);
            if (turn.Length == 0) continue;

            turnFrom[end] = turns.Count;
            turns.Add(turn);
        }

        return turns.ToArray();
    }

    /// <summary>
    /// <b>The rounds that close the band</b>, one for every end a run really stops at
    /// (<see cref="Paving.Caps"/>) — which is every end no other run sets off from and no turn hands over.
    /// </summary>
    /// <remarks>
    /// <b>An end a turn hands over is closed by the turn</b> (<see cref="Paving.Corners"/>): the wedge
    /// between the two bands is the sector of the round the turn's arc rims, and the rest of the round
    /// stands inside the next run's own band. A round struck there as well was two fans over one corner
    /// at every mouth in the town.
    /// </remarks>
    /// <param name="next">Written: which end each end that carries straight on hands over to (<see cref="Paving.Next"/>).</param>
    public static PavedCap[] Stops(PavedRun[] walk, bool[] turned, int[] next)
    {
        var ends = EveryEnd(walk);
        var places = PlacesOf(ends);
        var caps = new List<PavedCap>(ends.Count);
        for (var end = 0; end < ends.Count; end++)
        {
            if (turned[end]) continue;

            var onto = ends[end].CarriedOnBy(walk, ends, places);
            if (onto != CityPlan.NoRecord)
            {
                next[end] = onto;
                continue;
            }

            caps.Add(new PavedCap(ends[end].Run, ends[end].PlaceM, -ends[end].AwayM));
        }

        return caps.ToArray();
    }

    /// <summary>
    /// <b>Whether the band runs on through this end</b>: another run setting off from the place this one
    /// stops at, along the line this one arrived on, with the road on the same side and the same much or
    /// little of it left for the shell's shadow. The two are one line cut in two, and what closes this end
    /// is the next run's own concrete.
    /// </summary>
    /// <remarks>
    /// <b>Both the place and the bearing are asked to a rounding and no more</b>
    /// (<see cref="Kerbs.RoundingM"/>, <see cref="OneBearing"/>). Two runs cut from one line at one point
    /// stand at one point and set off along one bearing to the last bit of a float; two that <em>meet</em>
    /// stand as much as <see cref="Kerbs.OnePlaceM"/> apart and leave a wedge between them that only a round
    /// fills. Read with a place's worth of grace instead, a run that resumed a few centimetres past a graze
    /// lost the round that closes the gap.
    /// </remarks>
    int CarriedOnBy(PavedRun[] walk, List<Corner> ends, Dictionary<(int X, int Y), List<int>> places)
    {
        var cell = Cell(PlaceM);
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                if (!places.TryGetValue((cell.X + x, cell.Y + y), out var here)) continue;

                foreach (var end in here)
                {
                    var other = ends[end];
                    if (other.Run == Run) continue;
                    if ((other.PlaceM - PlaceM).Length() > Kerbs.RoundingM) continue;
                    if ((other.AwayM + AwayM).Length() > OneBearing) continue;
                    if (Vector2.Dot(other.RoadwardM, RoadwardM) <= 0f) continue;
                    if (walk[other.Run].Outline != walk[Run].Outline) continue;

                    return end;
                }
            }
        }

        return CityPlan.NoRecord;
    }

    /// <summary>Both ends of every run, as the kerb each of them hands over.</summary>
    static List<Corner> EveryEnd(PavedRun[] walk)
    {
        var ends = new List<Corner>(walk.Length * 2);
        for (var run = 0; run < walk.Length; run++)
        {
            var head = walk[run].Line[0];
            var tail = walk[run].Line[^1];
            ends.Add(At(run, walk[run], head.StartM, head.HeadingRad, alongTheRun: 1f));
            ends.Add(At(run, walk[run], tail.EndM, tail.HeadingAtRad(tail.LengthM), alongTheRun: -1f));
        }

        return ends;
    }

    /// <summary>The ends binned by the place they stand at, so a question about one is asked of its own.</summary>
    static Dictionary<(int X, int Y), List<int>> PlacesOf(List<Corner> ends)
    {
        var places = new Dictionary<(int X, int Y), List<int>>();
        for (var end = 0; end < ends.Count; end++)
        {
            var cell = Cell(ends[end].PlaceM);
            if (!places.TryGetValue(cell, out var here)) places[cell] = here = [];
            here.Add(end);
        }

        return places;
    }

    static Corner At(int run, in PavedRun walk, Vector2 placeM, float headingRad, float alongTheRun)
    {
        var awayM = Heading.Unit(headingRad) * alongTheRun;
        var roadwardM = Heading.RightOf(Heading.Unit(headingRad)) * walk.RoadSide;
        return new Corner(run, placeM, roadwardM, awayM, Cross(roadwardM, awayM) < 0f);
    }

    /// <summary>
    /// <b>The kerb this one hands over to</b>: of the ends standing at the same place, the one the shortest
    /// way round reaches — which where two runs give way is the only one there is.
    /// </summary>
    /// <remarks>
    /// <b>Judged by the way round the turn actually goes and not by the two bearings.</b> Two runs that lie
    /// along one another stand at one bearing, so by their bearings alone they hand over through nothing —
    /// but where the next kerb starts <em>behind</em> the one this ends, the edge that joins them goes the
    /// whole way round the place, and what that laid was a ring of kerb sitting on the pavement beside every
    /// crossing in the town.
    /// </remarks>
    int Onto(List<Corner> ends, bool[] taken, Dictionary<(int X, int Y), List<int>> places, float halfWalkM)
    {
        var onto = -1;
        var shortestRad = MostRad;
        var cell = Cell(PlaceM);
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                if (!places.TryGetValue((cell.X + x, cell.Y + y), out var here)) continue;

                foreach (var end in here)
                {
                    if (ends[end].SetsOff || taken[end]) continue;
                    if ((ends[end].PlaceM - PlaceM).LengthSquared() > Kerbs.OnePlaceM * Kerbs.OnePlaceM) continue;

                    var roundRad = TurnRad(ends[end], halfWalkM);
                    if (roundRad >= MostRad && AlongOneAnother(ends[end], halfWalkM)) roundRad = 0f;
                    if (roundRad >= shortestRad) continue;

                    shortestRad = roundRad;
                    onto = end;
                }
            }
        }

        return onto;
    }

    /// <summary>
    /// <b>The turn between the two kerbs</b>: the rim of the round that closes this run's end, as far as the
    /// point where the next run's own round takes over, and then the rim of that one to where its kerb
    /// starts. One arc where the two runs stopped at one point, and two where they did not.
    /// </summary>
    ArcSeg[] To(in Corner onto, float halfWalkM)
    {
        var laid = new List<ArcSeg>(2);
        var apartM = onto.PlaceM - PlaceM;
        if (TurnRad(onto, halfWalkM) >= MostRad && AlongOneAnother(onto, halfWalkM))
        {
            // Two runs lying along one another a place apart: the kerbs are one line with a step in it no
            // wider than that, and the edge crosses the step straight rather than round either place —
            // walked with the concrete on its across side, as every round is, whichever kerb that starts it
            // from.
            var fromM = PlaceM + (RoadwardM * halfWalkM);
            var toM = onto.PlaceM + (onto.RoadwardM * halfWalkM);
            var stepM = toM - fromM;
            if (stepM.Length() <= Kerbs.RoundingM) return laid.ToArray();

            if (Vector2.Dot(Heading.RightOf(stepM), RoadwardM) > 0f) (fromM, toM, stepM) = (toM, fromM, -stepM);
            laid.Add(new ArcSeg(fromM, MathF.Atan2(stepM.Y, stepM.X), stepM.Length(), 0f));
            return laid.ToArray();
        }

        if (OnePlace(apartM))
        {
            Lay(laid, Round(RoadwardM, SweepRad(RoadwardM, onto.RoadwardM, halfWalkM), halfWalkM));
        }
        else
        {
            var overM = Over(onto, apartM, halfWalkM);
            var toOverM = (overM - PlaceM) / halfWalkM;
            var fromOverM = (overM - onto.PlaceM) / halfWalkM;
            Lay(laid, Round(RoadwardM, SweepRad(RoadwardM, toOverM, halfWalkM), halfWalkM));
            Lay(laid, onto.Round(fromOverM, SweepRad(fromOverM, onto.RoadwardM, halfWalkM), halfWalkM));
        }

        // One round about this place ends a place short of the next kerb, and a leg read as nothing
        // (<see cref="SweepRad"/>) leaves a turn a hair short of it; the last of the turn is laid straight
        // there, so that it still arrives where the next kerb starts.
        var reachedM = laid.Count > 0 ? laid[^1].EndM : PlaceM + (RoadwardM * halfWalkM);
        var kerbM = onto.PlaceM + (onto.RoadwardM * halfWalkM);
        var shortM = kerbM - reachedM;
        if (shortM.Length() > Kerbs.RoundingM) laid.Add(new ArcSeg(reachedM, MathF.Atan2(shortM.Y, shortM.X), shortM.Length(), 0f));
        return laid.ToArray();
    }

    /// <summary>
    /// How far round the place — or the two places — the turn onto this kerb goes altogether.
    /// <b>One round where the two places are one place</b> (<see cref="OnePlace"/>), since the crossing of
    /// two rounds about places that close stands wherever the rounding put it; two rounds otherwise
    /// (<see cref="Over"/>).
    /// </summary>
    float TurnRad(in Corner onto, float halfWalkM)
    {
        var apartM = onto.PlaceM - PlaceM;
        return OnePlace(apartM)
            ? SweepRad(RoadwardM, onto.RoadwardM, halfWalkM)
            : WayRoundRad(Over(onto, apartM, halfWalkM), onto, halfWalkM);
    }

    /// <summary>
    /// Whether two places are one place for a round: as far apart as two ends stop where their lines cross
    /// (<see cref="CrossedM"/>) — which, welded (<see cref="WeldTheEnds"/>), is no distance at all — since
    /// the two rounds about places that close cross wherever the rounding put them. A kink at which the two
    /// stopped a hair over a centimetre apart had its rounds cross square off the line between them, the
    /// way round through either crossing was most of a circle, and no turn was laid.
    /// </summary>
    static bool OnePlace(Vector2 apartM) => apartM.Length() <= CrossedM;

    /// <summary>
    /// <b>Whether two ends lie along one another</b>: the road on the same side of both, the two running
    /// on from one another, and their kerbs no further apart than their places are and a place more. Such
    /// a pair hands over straight from kerb to kerb (<see cref="To"/>): the rounds about their two places
    /// cross off to one side of both kerbs, and the way round through that crossing was never a turn —
    /// read as one, an arm's kerb handing over to a movement's a few degrees off it was closed with a
    /// round at each end instead. A pair whose kerbs stand further apart than that meet at an angle, and
    /// a straight step between them would leave the wedge between their bands open.
    /// </summary>
    bool AlongOneAnother(in Corner onto, float halfWalkM) =>
        Vector2.Dot(RoadwardM, onto.RoadwardM) > 0f
        && Vector2.Dot(AwayM, onto.AwayM) < 0f
        && Vector2.Distance(PlaceM + (RoadwardM * halfWalkM), onto.PlaceM + (onto.RoadwardM * halfWalkM))
           <= Vector2.Distance(PlaceM, onto.PlaceM) + Kerbs.OnePlaceM;

    /// <summary>
    /// One piece of a turn, unless it turns through nothing — which happens where a round takes over exactly
    /// at a kerb, and what it would lay is a point and a triangle with no area to it.
    /// </summary>
    static void Lay(List<ArcSeg> laid, ArcSeg arc)
    {
        if (arc.LengthM > Kerbs.RoundingM) laid.Add(arc);
    }

    /// <summary>
    /// <b>Where the next run's round takes over from this one's</b>: the two rounds are one radius about two
    /// places, so they cross at the two points square to the line between them — and it is the one the turn
    /// reaches first, which is the one the two sweeps to it and on are shortest round.
    /// </summary>
    Vector2 Over(in Corner onto, Vector2 apartM, float halfWalkM)
    {
        var acrossM = Heading.RightOf(apartM / apartM.Length())
                      * MathF.Sqrt(MathF.Max(0f, (halfWalkM * halfWalkM) - (apartM.LengthSquared() * 0.25f)));
        var middleM = (PlaceM + onto.PlaceM) * 0.5f;

        return WayRoundRad(middleM + acrossM, onto, halfWalkM) <= WayRoundRad(middleM - acrossM, onto, halfWalkM)
            ? middleM + acrossM
            : middleM - acrossM;
    }

    /// <summary>How far round the two places the turn goes if it changes rounds at this crossing of them.</summary>
    float WayRoundRad(Vector2 overM, in Corner onto, float halfWalkM) =>
        SweepRad(RoadwardM, (overM - PlaceM) / halfWalkM, halfWalkM)
        + SweepRad((overM - onto.PlaceM) / halfWalkM, onto.RoadwardM, halfWalkM);

    /// <summary>One piece of the round about this run's own place, from one bearing off it through a sweep.</summary>
    ArcSeg Round(Vector2 fromM, float sweepRad, float halfWalkM)
    {
        var alongM = Heading.RightOf(fromM);
        return new ArcSeg(
            PlaceM + (fromM * halfWalkM), MathF.Atan2(alongM.Y, alongM.X), sweepRad * halfWalkM,
            1f / halfWalkM);
    }

    /// <summary>
    /// How far round the place the edge goes between two bearings off it, the way the round turns.
    /// </summary>
    /// <remarks>
    /// <b>A hair against the way the round turns is nothing, and not nearly the whole way round.</b> Two
    /// runs that lie along one another and stop a place apart hand over across two rounds that cross on the
    /// bisector between them, and from either kerb the crossing stands off the kerb's own bearing by the
    /// angle the places' separation subtends at the round — on whichever side the rounding put it. Read
    /// the way the round turns alone, an arm's kerb handing over to its fillet's at the tangent went the
    /// whole way round the place a hair short, no turn was shorter than half a circle, and both ends were
    /// closed with a round of their own. That much and no more is read as nothing.
    /// </remarks>
    static float SweepRad(Vector2 fromM, Vector2 toM, float halfWalkM)
    {
        var sweepRad = MathF.Atan2(Cross(fromM, toM), Vector2.Dot(fromM, toM));
        if (sweepRad < 0f && sweepRad >= -MathF.Asin(MathF.Min(1f, Kerbs.OnePlaceM / (2f * halfWalkM)))) return 0f;

        return sweepRad < 0f ? sweepRad + MathF.Tau : sweepRad;
    }

    static (int X, int Y) Cell(Vector2 atM) =>
        ((int)MathF.Floor(atM.X / Kerbs.OnePlaceM), (int)MathF.Floor(atM.Y / Kerbs.OnePlaceM));

    static float Cross(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);
}

/// <summary>
/// One run of the line the pavement is walked down, with its length, the side of it the tarmac lies on —
/// which is what tells the kerb line from the shell against the grass — whether the band it carries
/// reaches the outside of the town at all, and whether what it wraps is a carriageway.
/// </summary>
/// <remarks>
/// <b><paramref name="AlongARoad"/> is the picture's and nothing else's.</b> Such a run is a road's own
/// offset, so the band it carries is drawn with that road's cross-section
/// (<see cref="Paving.Sections"/>) rather than as a shape of its own — which is what keeps the concrete
/// and the asphalt it meets on one set of stations (TER-7b). Every other reader takes the run as it
/// always was.
/// </remarks>
internal readonly record struct PavedRun(
    ArcSeg[] Line, float LengthM, float RoadSide, bool Outline, bool AlongARoad, int Road = CityPlan.NoRecord);

/// <summary>
/// <b>One end the band stops at</b>: the run that stops there, the place it stops at and the way it faces
/// past it. The band is closed with the half-round the answer measures there, and the half it faces is the
/// half the run's own concrete does not already cover.
/// </summary>
internal readonly record struct PavedCap(int Run, Vector2 PlaceM, Vector2 OutwardM);
