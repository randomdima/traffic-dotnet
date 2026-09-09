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
/// leaves, a car park set back off the street it fronts.
/// </para>
/// <para>
/// <b>A junction has no piece here, because a junction has no shape</b> (TER-5): the ground inside a box is
/// the ground its own movements take (<see cref="LaneLines"/>) and the fillets that round the wedges
/// between its arms, and each of those is wrapped like any other piece of tarmac.
/// </para>
/// <para>
/// <b>Nothing here is the picture's.</b> The drawing is a stack of layers and a layer is the union of the
/// shapes in it (TER-7b), so it takes the same pieces at three sizes and works nothing out about how they
/// meet — where it once needed each road cut into the stretches its sides did not change over, each
/// junction's box walked as one outline, and every hand-over between two runs closed with a wedge, a round
/// or a bridge.
/// </para>
/// </remarks>
internal sealed class Paving
{
    Paving(float walkM, GroundPieces pieces, LaneLines lanes, Kerbs kerbs, PavedRun[] walk)
    {
        WalkM = walkM;
        Of = pieces;
        Lanes = lanes;
        Kerbs = kerbs;
        Walk = walk;
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
    /// How wide the band is. <b>The map's own figure where it has one</b>, and the town's where it does not,
    /// so a map laid without a pavement of its own is walked at the same width it is drawn.
    /// </summary>
    public float WalkM { get; }

    public static Paving Lay(GroundPieces pieces, SimConfig config)
    {
        var walkM = pieces.PavementWidthM > 0f ? pieces.PavementWidthM : config.PavementWidthM;
        var lanes = LaneLines.Of(pieces, config);

        // The junctions a road runs through as one line (<see cref="RoadCuts.RunsThrough"/>): a movement
        // through one stands inside the two arms' own bands, so it is not a piece of the outline.
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

            // Which side of the line the tarmac lies on, read a quarter of a walk either way at the middle
            // of the run: the near side is the kerb and the far one the shell against the grass.
            var on = Spline.SampleAt(line, lengthM * 0.5f);
            var side = kerbs.OffTheTarmacM(on.PositionM + (on.Right * walkM * 0.25f))
                       <= kerbs.OffTheTarmacM(on.PositionM - (on.Right * walkM * 0.25f))
                ? 1f
                : -1f;

            walk[run] = new PavedRun(line, lengthM, side);
        }

        return new Paving(walkM, pieces, lanes, kerbs, walk);
    }
}

/// <summary>
/// <b>Two ends that stop where their lines cross are made to stop at one point.</b> Cut a centimetre inside
/// one another's bands they stand a little apart, so the band the answer measures round one of them reaches
/// the other's a centimetre short of its start — a sliver the width of that offset and a walk long that the
/// pavement is not, at every graze in the town.
/// </summary>
static class Corner
{
    /// <summary>
    /// <b>How far apart two ends stop where their lines cross</b>: each line is cut where it is a joining's
    /// width inside the other's band (<see cref="Kerbs.JoinedM"/>), which is that far past the crossing
    /// along its own line, so the two ends stand that apart and up to root two of it at a right angle —
    /// and a rounding on top.
    /// </summary>
    static readonly float CrossedM = (Kerbs.JoinedM * MathF.Sqrt(2f)) + Kerbs.RoundingM;

    /// <summary>
    /// The end that stops second is moved onto the first, keeping its arc's curvature and its other end
    /// where they were.
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

    static (int X, int Y) Cell(Vector2 atM) =>
        ((int)MathF.Floor(atM.X / Kerbs.OnePlaceM), (int)MathF.Floor(atM.Y / Kerbs.OnePlaceM));
}

/// <summary>
/// One run of the line the pavement is walked down, its length, and the side of it the tarmac lies on —
/// which is what tells the kerb from the shell against the grass. The band is everything within half a walk
/// of the line (TER-3c.3), which is the whole of what a run says about the ground.
/// </summary>
internal readonly record struct PavedRun(ArcSeg[] Line, float LengthM, float RoadSide);
