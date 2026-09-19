using System.Numerics;
using TrafficSimulation.App.Screen;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// <b>What the pointer is over, and what a picked cell holds</b> (OBS-2t) — the one part of this overlay
/// that answers a question the reader asked rather than drawing everything there is at once.
/// </summary>
/// <remarks>
/// <para>
/// <b>Laid every frame and never into the town's cache.</b> What it draws follows the pointer, and the cache
/// behind the town's own layers is re-laid only when the view or a switch moves — held there, a highlight
/// would stand where the pointer was several frames ago.
/// </para>
/// <para>
/// <b>Every reading is off the producer</b>, like the rest of this slice: what a cell holds is the index's
/// own answer (<see cref="ChainIndex.ChainsInCell(int, int, Span{int})"/>), the ribbon is the band the merge
/// was given (<see cref="ArcRibbon"/>), and the boundary is the merge's own output
/// (<see cref="LaneShell"/>). Nothing here re-derives a shape in order to point at it.
/// </para>
/// <para>
/// <b>And each reading is drawn only where its own layer is.</b> A highlight over a layer that is switched
/// off is a mark with nothing under it to be read against, which is the one thing a pointer reading cannot
/// afford to be.
/// </para>
/// </remarks>
internal sealed partial class DebugOverlay
{
    /// <summary>
    /// What a picked-out line is drawn at: <b>three times the width everything else is drawn at</b>, so what
    /// is picked is what is being read at a glance and no colour has to be learned to see it.
    /// </summary>
    const float PickedLineM = PathMarks.PathLineM * 3f;

    /// <summary>Held to this on screen, so a pick reads at a district framing as well as over a car.</summary>
    const float PickedLineFloorPx = 2.5f;

    /// <summary>How far off a boundary the pointer may stand and still be on it: a finger's width on the glass.</summary>
    const float ReachPx = 10f;

    /// <summary>Where a reading stands against the pointer, and how far the next one is stacked under it.</summary>
    static readonly Vector2 LabelOffsetPx = new(14f, 10f);

    const float LabelStepPx = Theme.TextPx + 4f;

    /// <summary>
    /// The chains one cell holds, the lines a place could be on, and the ribbon of the line under the
    /// pointer — <b>the working sets this pass answers out of, kept because it runs every frame</b>. The
    /// first two are sized to the town the first time it is asked about; the ribbon is laid again only when
    /// the pointer moves onto a different line.
    /// </summary>
    int[] _inCell = [];
    int[] _nearLines = [];
    float[] _nearAlongM = [];
    ArcSeg[] _hovered = [];
    int _hoveredLine = -1;

    void Pointer(
        ref ScreenDraw draw, TownWorld world, SimConfig config, DebugSwitches switches, DebugPick pick,
        Vector2 pointerM, Vector2 pointerPx, Vector2 uiPx, float pixelsPerMetre)
    {
        var paving = world.Plan.Paving(config);
        Room(paving);

        var lineM = MathF.Max(PickedLineM, PickedLineFloorPx / pixelsPerMetre);
        var sagM = PathMarks.SagPx / pixelsPerMetre;
        var labels = 0;

        if (switches.Grid)
        {
            Standing(ref draw, uiPx, pointerM, pointerPx);
            PickedCell(ref draw, paving, config, pick, pointerPx, lineM, sagM, ref labels);
        }

        if (switches.Ribbons) RibbonUnder(ref draw, paving, config, pointerM, pointerPx, lineM, sagM, ref labels);

        if (switches.Perimeter)
        {
            BoundaryUnder(ref draw, paving, config, pointerM, pointerPx, pixelsPerMetre, lineM, sagM, ref labels);
        }
    }

    /// <summary>
    /// Room for a town this size, and the widest band in it — both taken once, because a town is laid
    /// before it is pointed at and neither moves afterwards.
    /// </summary>
    void Room(Paving paving)
    {
        var lines = paving.DrivenCount;
        if (_nearLines.Length >= lines && _inCell.Length >= lines) return;

        _inCell = new int[lines];
        _nearLines = new int[lines];
        _nearAlongM = new float[lines];
        _widestHalfM = 0f;
        for (var line = 0; line < lines; line++)
        {
            _widestHalfM = MathF.Max(_widestHalfM, paving.DrivenWidthM(line) * 0.5f);
        }
    }

    float _widestHalfM;

    /// <summary>
    /// <b>Where the pointer stands on the town, in the town's own metres</b> (OBS-2t), written in the corner
    /// above the scale bar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>In the corner and not beside the cursor</b>, unlike the readings about what is under it. A
    /// coordinate is read while looking somewhere else — it is written down, typed into a command line,
    /// compared against a figure in a log — so it belongs where the eye can go back to it, which is the
    /// corner the scale bar has already made the place for saying how big things are (OBS-2e).
    /// </para>
    /// <para>
    /// <b>It is the grid's</b>, because a lattice is the one layer read in coordinates: every other layer
    /// draws a thing to be looked at, and this one draws where the things <em>are</em>.
    /// </para>
    /// </remarks>
    static void Standing(ref ScreenDraw draw, Vector2 uiPx, Vector2 pointerM, Vector2 pointerPx)
    {
        // Off the glass is over a panel or no pointer at all (a shot takes none): a coordinate for a place
        // nobody is pointing at is a figure the reader cannot account for.
        if (pointerPx.X < 0f || pointerPx.Y < 0f || pointerPx.X > uiPx.X || pointerPx.Y > uiPx.Y) return;

        Span<char> text = stackalloc char[32];
        var said = new TextBuffer(text);
        said.Add(pointerM.X, "F2");
        said.Add(", ");
        said.Add(pointerM.Y, "F2");
        said.Add(" m");

        // Right-aligned to the legend's own margin, so the two read as one column rather than as two
        // things that happen to be in the same corner.
        draw.OutlinedText(
            new Vector2(
                uiPx.X - CornerMarginPx - GlyphSheet.WidthPx(said.Length, Theme.TextPx),
                uiPx.Y - AboveTheLegendPx),
            said.Written, Theme.TextPx);
    }

    /// <summary>The corner the scale bar keeps (OBS-2e), held to here so a reading over it lines up with it.</summary>
    const float CornerMarginPx = 18f;

    /// <summary>
    /// How far off the bottom that reading's own top stands: clear of the scale bar's stack — its margin,
    /// the bar, a large graduation and the figures over them — with a line's daylight above that.
    /// <b>A figure and not a measurement of the legend</b>, which belongs to another slice and is not this
    /// one's to ask about; what keeps the two apart is that both are laid off the same corner.
    /// </summary>
    const float AboveTheLegendPx = CornerMarginPx + 5f + 13f + Theme.SmallTextPx + Theme.TextPx + 8f;

    /// <summary>
    /// <b>The cell a reader picked, and every line the index holds in it</b>: the cell's own square, and each
    /// of those lines drawn whole at the picked weight.
    /// </summary>
    /// <remarks>
    /// <b>Whole lines and not the piece of each inside the cell.</b> What a cell says is which lines a
    /// question asked there is narrowed to, and a line is offered as a candidate in its entirety however
    /// little of it reaches the cell — so lighting only the part inside would be a picture of the cell rather
    /// than of the answer it gives.
    /// </remarks>
    void PickedCell(
        ref ScreenDraw draw, Paving paving, SimConfig config, DebugPick pick, Vector2 pointerPx, float lineM,
        float sagM, ref int labels)
    {
        if (pick.AtM is not { } atM) return;

        var grid = paving.DrivenLines(config);
        if (grid.Width <= 0 || grid.Height <= 0) return;

        var cellM = grid.CellM;
        var atX = Cell(atM.X - grid.OriginM.X, cellM, grid.Width);
        var atY = Cell(atM.Y - grid.OriginM.Y, cellM, grid.Height);
        var middleM = grid.OriginM + new Vector2((atX + 0.5f) * cellM, (atY + 0.5f) * cellM);
        var held = grid.ChainsInCell(atX, atY, _inCell);

        draw.BoxM(middleM, new Vector2(cellM), 0f, lineM, Theme.DebugPicked);
        for (var at = 0; at < held && at < _inCell.Length; at++)
        {
            var line = _inCell[at];
            PathMarks.Banded(
                ref draw, paving.ArcsOfDriven(line), 0f, paving.DrivenLengthM(line), sagM, lineM,
                Theme.DebugHeld);
        }

        Span<char> text = stackalloc char[64];
        var said = new TextBuffer(text);
        said.Add("cell ");
        said.Add(atX);
        said.Add(", ");
        said.Add(atY);
        said.Add(" of ");
        said.Add(grid.Width);
        said.Add("x");
        said.Add(grid.Height);
        said.Add(" at ");
        said.Add(cellM, "F1");
        said.Add(" m holds ");
        said.Add(held);
        said.Add(" lines");
        Label(ref draw, pointerPx, said.Written, ref labels);
    }

    /// <summary>
    /// <b>The ribbon the pointer stands on</b>: the band of ground that line covers, drawn as the closed
    /// chain of lines it is (<see cref="ArcRibbon"/>) and nothing else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The ribbon the merge is given, and cut where the merge cuts it.</b> Its sections are an edge piece
    /// for each arc of the line, walked out and back, and a square end at either end of it — so a reader
    /// looking at where a boundary went wrong is looking at the pieces the merge weighed, not at a drawing of
    /// the band's outline.
    /// </para>
    /// <para>
    /// <b>The line alone, with no mark standing off it.</b> How many sections the chain has is written beside
    /// the pointer, and a bar at each joint said the same thing a second time over the one thing the reading
    /// is for — the path the edge takes round a corner, which a comb of marks across it is what hides.
    /// </para>
    /// </remarks>
    void RibbonUnder(
        ref ScreenDraw draw, Paving paving, SimConfig config, Vector2 pointerM, Vector2 pointerPx, float lineM,
        float sagM, ref int labels)
    {
        var line = LineUnder(paving, config, pointerM);
        if (line < 0)
        {
            _hoveredLine = -1;
            return;
        }

        // Laid again only when the pointer moves onto another line: this runs every frame, and a ribbon is
        // a fact about the line rather than about the frame.
        if (line != _hoveredLine)
        {
            _hovered = ArcRibbon.Of(
                paving.ArcsOfDriven(line), paving.DrivenWidthM(line) * 0.5f, LineTolerance.RoundingM);
            _hoveredLine = line;
        }

        var roundM = 0f;
        foreach (var piece in _hovered)
        {
            PathMarks.Banded(ref draw, [piece], 0f, piece.LengthM, sagM, lineM, Theme.DebugPicked);
            roundM += piece.LengthM;
        }

        Span<char> text = stackalloc char[80];
        var said = new TextBuffer(text);
        said.Add(KindOf(paving, line));
        said.Add(' ');
        said.Add(line);
        said.Add(": ");
        said.Add(paving.DrivenLengthM(line), "F2");
        said.Add(" m driven, ");
        said.Add(paving.DrivenWidthM(line), "F2");
        said.Add(" m wide, ");
        said.Add(_hovered.Length);
        said.Add(" sections round ");
        said.Add(roundM, "F2");
        said.Add(" m");
        Label(ref draw, pointerPx, said.Written, ref labels);
    }

    /// <summary>
    /// Which driven line's own band a place stands on, or −1 — the nearest line where several cover it,
    /// which is the one the reader is pointing at.
    /// </summary>
    int LineUnder(Paving paving, SimConfig config, Vector2 pointM)
    {
        var found = paving.DrivenLines(config).Near(pointM, _widestHalfM, _nearLines, _nearAlongM);
        var best = -1;
        var bestM = float.MaxValue;
        for (var at = 0; at < found && at < _nearLines.Length; at++)
        {
            var line = _nearLines[at];

            // A band has square ends (TER-3c.6), so the ground off the end of a line is not the line's
            // however near it stands.
            if (_nearAlongM[at] <= 0f || _nearAlongM[at] >= paving.DrivenLengthM(line)) continue;

            var offM = Vector2.Distance(
                Spline.SampleAt(paving.ArcsOfDriven(line), _nearAlongM[at]).PositionM, pointM);
            if (offM >= paving.DrivenWidthM(line) * 0.5f || offM >= bestM) continue;

            bestM = offM;
            best = line;
        }

        return best;
    }

    /// <summary>What a driven line is, in the one numbering they all share (<see cref="Paving.DrivenCount"/>).</summary>
    static string KindOf(Paving paving, int line) =>
        line < paving.Lanes.LaneCount ? "lane"
        : line < paving.Lanes.LaneCount + paving.Lanes.ConnectorCount ? "movement" : "bay way";

    /// <summary>
    /// <b>The one stretch of boundary the pointer is over</b>, drawn at the picked weight with a disc at each
    /// of its ends — and which outline it belongs to, which chain of that one, and which stretch of that.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every outline the layer draws is searched and they are searched alike</b>
    /// (<see cref="Boundaries"/>): the rings the merge closed, the runs it could not, and the layers struck
    /// off the boundary (OBS-2u). A reading offered over one of them and not the others would say that the
    /// pointer had found nothing wherever it stood over another, which is the one answer a pointer must not
    /// give — and a layer's outer edge is exactly the outline a reader most wants to ask about, being the
    /// one whose corners are a construction rather than a fact.
    /// </para>
    /// <para>
    /// <b>A stretch and not the whole chain.</b> A ring is most of a district and lighting all of it says
    /// nothing; what a reader following a boundary wants is which piece they are on and where that piece
    /// stops — which is the whole of the question at an open run's two ends, and at every corner an outset
    /// put in.
    /// </para>
    /// <para>
    /// <b>It reads the layers the picture drew and never a set of its own</b> (<see cref="Paving.Rings"/>).
    /// Struck again here they would be a second answer about the same shape, and the stretch under the
    /// pointer would be a stretch of a line nobody can see.
    /// </para>
    /// </remarks>
    void BoundaryUnder(
        ref ScreenDraw draw, Paving paving, SimConfig config, Vector2 pointerM, Vector2 pointerPx,
        float pixelsPerMetre, float lineM, float sagM, ref int labels)
    {
        var shell = paving.Perimeter(config);
        var rings = paving.Rings(config);
        var found = new Picked { OffM = ReachPx / MathF.Max(pixelsPerMetre, 0.001f) };

        Nearest(rings.Carriageway.Rings, rings.Carriageway.Named, Ring, pointerM, ref found);
        Nearest(shell.Loose, rings.Carriageway.Named, OpenRun, pointerM, ref found);
        foreach (var layer in rings.Layers)
        {
            Nearest(layer.Rings, layer.Named, Ring, pointerM, ref found);
            Nearest(layer.Loose, layer.Named, OpenRun, pointerM, ref found);
        }

        if (found.Chain is null) return;

        var stretch = found.Chain[found.Piece];
        PathMarks.Banded(ref draw, [stretch], 0f, stretch.LengthM, sagM, lineM, Theme.DebugPicked);
        draw.DiscM(stretch.StartM, PathMarks.JoinDiscM * 2f, Theme.DebugPicked);
        draw.DiscM(stretch.EndM, PathMarks.JoinDiscM * 2f, Theme.DebugPicked);

        Span<char> text = stackalloc char[160];
        var said = new TextBuffer(text);
        said.Add(found.Outline);
        said.Add(' ');
        said.Add(found.Kind);
        said.Add(' ');
        said.Add(found.At);
        said.Add(", stretch ");
        said.Add(found.Piece);
        said.Add(" of ");
        said.Add(found.Chain.Length);
        said.Add(", ");
        said.Add(stretch.LengthM, "F3");
        said.Add(" m");
        Label(ref draw, pointerPx, said.Written, ref labels);

        // <b>What the stretch reads as running along, at each of its two ends, on a line of its own</b>
        // (<see cref="CityGen.KerbEnds"/>) — which is the whole of why a kerb end stands at one joint and
        // not at the next. <b>One line for each end</b>: the rounding joins consecutive stretches of one
        // circle, so a straight kerb is one stretch for every road it is straight through, and the two ends
        // of it answer two different things at two different distances back along them.
        Said(ref draw, pointerPx, paving, config, stretch, fromStart: true, ref labels);
        Said(ref draw, pointerPx, paving, config, stretch, fromStart: false, ref labels);
    }

    /// <summary>One end of a stretch of boundary: what it reads as running along, on a label of its own.</summary>
    static void Said(
        ref ScreenDraw draw, Vector2 pointerPx, Paving paving, SimConfig config, in ArcSeg stretch,
        bool fromStart, ref int labels)
    {
        Span<char> text = stackalloc char[96];
        var said = new TextBuffer(text);
        said.Add(fromStart ? "  from " : "  to   ");

        var lane = CityGen.KerbEnds.LaneUnder(paving, config, stretch, fromStart);
        if (lane < 0)
        {
            said.Add("no road");
            Label(ref draw, pointerPx, said.Written, ref labels);
            return;
        }

        var road = CityGen.KerbEnds.RoadOf(paving, lane);
        said.Add("lane ");
        said.Add(lane);
        if (road == CityGen.KerbEnds.Park)
        {
            said.Add(" of a car park");
            Label(ref draw, pointerPx, said.Written, ref labels);
            return;
        }

        said.Add(" of road ");
        said.Add(road);

        // <b>And how far out of the box along that road this end stands</b>, which is the figure that
        // decides which end of a round is called the further of the two: a reader asking why one end took
        // that name can read both of them and see.
        said.Add(", ");
        said.Add(CityGen.KerbEnds.OutM(paving, lane, stretch.PointAtM(fromStart ? 0f : stretch.LengthM)), "F1");
        said.Add(" m out");
        Label(ref draw, pointerPx, said.Written, ref labels);
    }

    /// <summary>
    /// The stretch of boundary nearest the pointer as the search stands: what outline it is in, which chain
    /// of that outline and which stretch of that chain, and how far off the pointer stood.
    /// </summary>
    /// <remarks>
    /// <b>The chain itself and not the outline it came out of</b>, so that what is drawn and counted is the
    /// thing that won rather than something looked up again out of whichever set it was found in.
    /// </remarks>
    struct Picked
    {
        public float OffM;
        public string Outline;
        public string Kind;
        public ArcSeg[]? Chain;
        public int At;
        public int Piece;
    }

    /// <summary>
    /// What a chain is, said apart from which outline it belongs to — <b>so the two are never joined into a
    /// string</b>, this running every frame and the steady state allocating nothing.
    /// </summary>
    const string Ring = "ring";

    const string OpenRun = "open run";

    /// <summary>
    /// The nearest stretch of one outline to a place, kept where it beats what already stands — which is
    /// what lets several outlines be searched one after another and the best of all of them come back.
    /// </summary>
    static void Nearest(
        ReadOnlySpan<ArcSeg[]> chains, string outline, string kind, Vector2 pointM, ref Picked found)
    {
        for (var at = 0; at < chains.Length; at++)
        {
            var chain = chains[at];
            for (var piece = 0; piece < chain.Length; piece++)
            {
                var stretch = chain[piece];

                // <b>Rejected on its own start before it is projected onto</b>: a city's boundary is a
                // hundred thousand stretches and this runs every frame, while nothing further from the
                // pointer than its own length plus the reach can possibly win.
                var reachM = stretch.LengthM + found.OffM;
                if (Vector2.DistanceSquared(stretch.StartM, pointM) > reachM * reachM) continue;

                var alongM = Spline.ProjectM([stretch], pointM, stretch.LengthM * 0.5f, stretch.LengthM);
                var offM = Vector2.Distance(stretch.PointAtM(alongM), pointM);
                if (offM >= found.OffM) continue;

                found.OffM = offM;
                found.Outline = outline;
                found.Kind = kind;
                found.Chain = chain;
                found.At = at;
                found.Piece = piece;
            }
        }
    }

    /// <summary>
    /// One reading, written beside the pointer and stacked under the last. <b>Beside the pointer rather than
    /// at the thing it names</b>: what it is about is under the cursor already, and a label laid on the town
    /// covers the picture it is a reading of.
    /// </summary>
    static void Label(ref ScreenDraw draw, Vector2 pointerPx, scoped ReadOnlySpan<char> said, ref int labels)
    {
        draw.OutlinedText(pointerPx + LabelOffsetPx + new Vector2(0f, labels * LabelStepPx), said, Theme.TextPx);
        labels++;
    }
}
