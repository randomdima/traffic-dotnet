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
    Paving(float walkM, GroundPieces pieces, LaneLines lanes, Kerbs kerbs, PavedRun[] walk, ArcSeg[][] corners)
    {
        WalkM = walkM;
        Of = pieces;
        Lanes = lanes;
        Kerbs = kerbs;
        Walk = walk;
        Corners = corners;
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
    /// How wide the band is. <b>The map's own figure where it has one</b>, and the town's where it does not,
    /// so a map laid without a pavement of its own is walked at the same width it is drawn.
    /// </summary>
    public float WalkM { get; }

    public static Paving Lay(GroundPieces pieces, SimConfig config)
    {
        var walkM = pieces.PavementWidthM > 0f ? pieces.PavementWidthM : config.PavementWidthM;
        var lanes = LaneLines.Of(pieces, config);
        var kerbs = Kerbs.Of(pieces, lanes);

        var wraps = new List<Kerbs.Wrap>();
        kerbs.Shell(walkM * 0.5f, Kerbs.RoundingM, null, wraps);

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

            walk[run] = new PavedRun(
                line, lengthM, side,
                kerbs.OffTheTarmacM(on.PositionM - (on.Right * side * ((walkM * 0.5f) + AHairM))) > walkM);
        }

        return new Paving(walkM, pieces, lanes, kerbs, walk, Corner.Turns(walk, walkM * 0.5f));
    }

    /// <summary>
    /// A step past the edge of the band, for asking what is on the other side of it. Small enough that a
    /// point that far beyond the shell is beyond nothing else, and large enough to clear the rounding two
    /// computations of one distance disagree by (<see cref="Kerbs.RoundingM"/>).
    /// </summary>
    const float AHairM = 0.05f;
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
readonly record struct Corner(Vector2 PlaceM, Vector2 RoadwardM, bool SetsOff)
{
    /// <summary>
    /// <b>Half a turn is not a corner between two runs.</b> Two runs that hand over make a wedge of tarmac
    /// between them and the turn crosses it, which is less than half the circle however sharply they meet;
    /// a turn past that is an end paired with one it does not meet at all.
    /// </summary>
    const float MostRad = MathF.PI;

    public static ArcSeg[][] Turns(PavedRun[] walk, float halfWalkM)
    {
        if (halfWalkM <= 0f) return [];

        var ends = new List<Corner>(walk.Length * 2);
        foreach (var run in walk)
        {
            var head = run.Line[0];
            var tail = run.Line[^1];
            ends.Add(At(run, head.StartM, head.HeadingRad, alongTheRun: 1f));
            ends.Add(At(run, tail.EndM, tail.HeadingAtRad(tail.LengthM), alongTheRun: -1f));
        }

        var places = new Dictionary<(int X, int Y), List<int>>();
        for (var end = 0; end < ends.Count; end++)
        {
            var cell = Cell(ends[end].PlaceM);
            if (!places.TryGetValue(cell, out var here)) places[cell] = here = [];
            here.Add(end);
        }

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

            taken[onto] = true;

            var turn = ends[end].To(ends[onto], halfWalkM);
            if (turn.Length > 0) turns.Add(turn);
        }

        return turns.ToArray();
    }

    static Corner At(in PavedRun run, Vector2 placeM, float headingRad, float alongTheRun)
    {
        var awayM = Heading.Unit(headingRad) * alongTheRun;
        var roadwardM = Heading.RightOf(Heading.Unit(headingRad)) * run.RoadSide;
        return new Corner(placeM, roadwardM, Cross(roadwardM, awayM) < 0f);
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
        if (apartM.Length() <= Kerbs.RoundingM)
        {
            Lay(laid, Round(RoadwardM, SweepRad(RoadwardM, onto.RoadwardM), halfWalkM));
            return laid.ToArray();
        }

        var overM = Over(onto, apartM, halfWalkM);
        var toOverM = (overM - PlaceM) / halfWalkM;
        var fromOverM = (overM - onto.PlaceM) / halfWalkM;

        Lay(laid, Round(RoadwardM, SweepRad(RoadwardM, toOverM), halfWalkM));
        Lay(laid, onto.Round(fromOverM, SweepRad(fromOverM, onto.RoadwardM), halfWalkM));
        return laid.ToArray();
    }

    /// <summary>How far round the place — or the two places — the turn onto this kerb goes altogether.</summary>
    float TurnRad(in Corner onto, float halfWalkM)
    {
        var apartM = onto.PlaceM - PlaceM;
        return apartM.Length() <= Kerbs.RoundingM
            ? SweepRad(RoadwardM, onto.RoadwardM)
            : WayRoundRad(Over(onto, apartM, halfWalkM), onto, halfWalkM);
    }

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
        SweepRad(RoadwardM, (overM - PlaceM) / halfWalkM)
        + SweepRad((overM - onto.PlaceM) / halfWalkM, onto.RoadwardM);

    /// <summary>One piece of the round about this run's own place, from one bearing off it through a sweep.</summary>
    ArcSeg Round(Vector2 fromM, float sweepRad, float halfWalkM)
    {
        var alongM = Heading.RightOf(fromM);
        return new ArcSeg(
            PlaceM + (fromM * halfWalkM), MathF.Atan2(alongM.Y, alongM.X), sweepRad * halfWalkM,
            1f / halfWalkM);
    }

    /// <summary>How far round the place the edge goes between two bearings off it, the way the round turns.</summary>
    static float SweepRad(Vector2 fromM, Vector2 toM)
    {
        var sweepRad = MathF.Atan2(Cross(fromM, toM), Vector2.Dot(fromM, toM));
        return sweepRad < 0f ? sweepRad + MathF.Tau : sweepRad;
    }

    static (int X, int Y) Cell(Vector2 atM) =>
        ((int)MathF.Floor(atM.X / Kerbs.OnePlaceM), (int)MathF.Floor(atM.Y / Kerbs.OnePlaceM));

    static float Cross(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);
}

/// <summary>
/// One run of the line the pavement is walked down, with its length, the side of it the tarmac lies on —
/// which is what tells the kerb line from the shell against the grass — and whether the band it carries
/// reaches the outside of the town at all.
/// </summary>
internal readonly record struct PavedRun(ArcSeg[] Line, float LengthM, float RoadSide, bool Outline);
