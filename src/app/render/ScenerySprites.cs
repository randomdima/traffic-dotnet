using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Statics;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>The town's scenery as instances</b> (<see cref="CityPlan.SceneryArrays"/>): laid once when the plan is opened,
/// filed by cell as <see cref="StandingSprites"/> files what stands, and made into instances only where the view
/// is — at most <see cref="Core.Config.ViewFigures.SceneryMostDrawn"/> of them a frame.
/// </summary>
/// <remarks>
/// <para>
/// <b>Twelve bytes a prop and not an instance's fifty-six</b>: a traced city's scenery is millions of props, and
/// all a frame needs of one is its place and its look. The look is the plan's (GEN-6e), and the size it is drawn
/// at is the look's own, kept once a look rather than once a prop.
/// </para>
/// <para>
/// <b>A view holding more than the budget draws an even share of it</b>: every n-th prop in the filing, n the least
/// power of two that brings the view within the budget. The filing is by cell, so every n-th is an even thinning of
/// the ground; and the props drawn are picked by their own index, never by where the view starts, so n moves only as
/// the zoom crosses a doubling and a pan neither thins nor shimmers. A view that wide draws a tree a pixel or two
/// across.
/// </para>
/// </remarks>
internal sealed class ScenerySprites
{
    /// <summary>Filed by cell, row by row, and by column along a row.</summary>
    readonly SceneryPiece[] _pieces;

    /// <summary>
    /// Where each row of cells starts in <see cref="_pieces"/>, and no finer: the cells in view along a row are found by
    /// their column, so the index is a row's worth of integers and not a cell's — a city's cells are millions.
    /// </summary>
    readonly int[] _rowOffsets;

    /// <summary>The cull grid is the town's own (SIM-8), as <see cref="StandingSprites"/>' is.</summary>
    readonly GridWindow _window;

    /// <summary>How far the widest piece reaches from its centre, which a view is grown by.</summary>
    readonly float _reachM;

    /// <summary>Each look's half-size: the look's own (GEN-6e).</summary>
    readonly Vector2[] _halfSizeM;

    readonly int _firstSheet;
    readonly int _mostDrawn;

    /// <summary>A frame's spans as <see cref="Fill"/> walks them, laid a row of cells each the first time it is asked.</summary>
    ScenerySpan[] _spans = [];

    /// <summary>Each row's run in view as <see cref="Spans"/> counted it, so the pass that lays the spans does not search for it again.</summary>
    (int Start, int End)[] _rowsInView = [];

    ScenerySprites(
        SceneryPiece[] pieces, int[] rowOffsets, GridWindow window, float reachM, Vector2[] halfSizeM, int firstSheet,
        int mostDrawn)
    {
        _pieces = pieces;
        _rowOffsets = rowOffsets;
        _window = window;
        _reachM = reachM;
        _halfSizeM = halfSizeM;
        _firstSheet = firstSheet;
        _mostDrawn = mostDrawn;
    }

    public static ScenerySprites Nothing { get; } =
        new([], [0], GridWindow.Of(new WorldGrid(1f).Main, 0, 0, 0, 0), 0f, [], 0, 0);

    public int Count => _pieces.Length;

    /// <summary>How many instances a frame of the town's scenery needs at most, which is what the buffer is laid for.</summary>
    public static int CapacityFor(CityPlan plan, int mostDrawn) => Math.Min(plan.Scenery.Count, mostDrawn);

    /// <summary>How many rows of cells a town of this extent files its scenery over (<see cref="Rows"/>), known before it is laid.</summary>
    public static int RowsFor(Vector2 worldSizeM, GridLevel level) => Window(worldSizeM, level).Height;

    static GridWindow Window(Vector2 worldSizeM, GridLevel level) => GridWindow.Over(level, Vector2.Zero, Vector2.Max(worldSizeM, Vector2.Zero));

    /// <param name="worldSizeM">The town's extent, which the cull grid covers.</param>
    /// <param name="level">The level of the grid the pieces are filed at — its main one.</param>
    public static ScenerySprites Lay(
        CityPlan.SceneryArrays scenery, Vector2 worldSizeM, PropCatalog props, int firstPropSheet, GridLevel level, int mostDrawn)
    {
        if (scenery.Count == 0) return Nothing;

        var halfSizeM = new Vector2[props.Count];
        for (var variant = 0; variant < halfSizeM.Length; variant++) halfSizeM[variant] = props.Variants[variant].SizeM * 0.5f;

        // A counting sort, as the standing instances are filed: the offsets are the running total, and the second
        // pass drops each piece where its cell says. What is kept of them is where each row starts.
        var window = Window(worldSizeM, level);
        var cellOf = new int[scenery.Count];
        var offsets = new int[window.Count + 1];
        for (var at = 0; at < scenery.Count; at++)
        {
            cellOf[at] = window.IndexAt(scenery.CentreM[at]);
            offsets[cellOf[at] + 1]++;
        }

        for (var cell = 0; cell < window.Count; cell++) offsets[cell + 1] += offsets[cell];

        var rowOffsets = new int[window.Height + 1];
        for (var row = 0; row < window.Height; row++) rowOffsets[row] = offsets[row * window.Width];
        rowOffsets[window.Height] = scenery.Count;

        var pieces = new SceneryPiece[scenery.Count];
        var reachM = 0f;
        for (var at = 0; at < scenery.Count; at++)
        {
            var look = scenery.Look[at];
            reachM = MathF.Max(reachM, halfSizeM[look].Length());
            pieces[offsets[cellOf[at]]++] = new SceneryPiece(scenery.CentreM[at], look);
        }

        return new ScenerySprites(pieces, rowOffsets, window, reachM, halfSizeM, firstPropSheet, mostDrawn);
    }

    /// <summary>Every piece as it is kept, filed by cell: what the device is handed once (<see cref="TownRenderer.LayTown"/>).</summary>
    public ReadOnlySpan<SceneryPiece> Pieces => _pieces;

    /// <summary>Each look's half-size as it is drawn, by prop look.</summary>
    public ReadOnlySpan<Vector2> HalfSizeM => _halfSizeM;

    /// <summary>The sheet of the first prop look, which a piece's look counts from.</summary>
    public int FirstSheet => _firstSheet;

    /// <summary>How many rows of cells the scenery is filed over — the most spans a view can take (<see cref="Spans"/>).</summary>
    public int Rows => _window.Height;

    /// <summary>
    /// <b>The scenery the view touches, thinned to the budget, as runs of <see cref="Pieces"/></b>: per row in view, the
    /// first piece drawn and how many are drawn before it in the frame, every <paramref name="every"/>-th piece from there
    /// to the row's end — so instance <c>i</c> of a span is piece <c>First + (i − Before) × every</c>. Answers how many
    /// spans; <paramref name="drawn"/> is how many pieces they hold together, at most the budget and
    /// <paramref name="most"/>.
    /// </summary>
    public int Spans(Vector2 viewCentreM, Vector2 viewSpanM, int most, Span<ScenerySpan> into, out int every, out int drawn)
    {
        every = 1;
        drawn = 0;
        most = Math.Min(_mostDrawn, most);
        if (_pieces.Length == 0 || most <= 0) return 0;

        var half = (viewSpanM * 0.5f) + new Vector2(_reachM);
        _window.TryRange(viewCentreM - half, viewCentreM + half, out var inView);

        if (_rowsInView.Length < Rows) _rowsInView = new (int, int)[Rows];
        var inViewCount = 0L;
        for (var row = inView.FromY; row <= inView.ToY; row++)
        {
            var (start, end) = _rowsInView[row - inView.FromY] = InView(row, inView);
            inViewCount += end - start;
        }

        while (inViewCount > (long)most * every) every <<= 1;

        var spans = 0;
        for (var row = inView.FromY; row <= inView.ToY && drawn < most && spans < into.Length; row++)
        {
            var (start, end) = _rowsInView[row - inView.FromY];
            var first = (start + every - 1) / every * every;
            if (first >= end) continue;

            var count = Math.Min(((end - 1 - first) / every) + 1, most - drawn);
            into[spans++] = new ScenerySpan((uint)first, (uint)drawn);
            drawn += count;
        }

        return spans;
    }

    /// <summary>Writes the scenery the view touches, thinned to the budget, and answers how many — <see cref="Spans"/>, as instances.</summary>
    public int Fill(Vector2 viewCentreM, Vector2 viewSpanM, Span<SpriteInstance> into)
    {
        if (_spans.Length < Rows) _spans = new ScenerySpan[Rows];
        var spans = _spans;
        var count = Spans(viewCentreM, viewSpanM, into.Length, spans, out var every, out var drawn);
        for (var span = 0; span < count; span++)
        {
            var past = span + 1 < count ? (int)spans[span + 1].Before : drawn;
            for (var at = (int)spans[span].Before; at < past; at++)
            {
                ref readonly var piece = ref _pieces[(int)spans[span].First + ((at - (int)spans[span].Before) * every)];
                into[at] = new SpriteInstance(
                    piece.CentreM, _halfSizeM[piece.Look], SpriteCell.Whole, SpriteTint.Plain,
                    (uint)(_firstSheet + piece.Look), 0f);
            }
        }

        return drawn;
    }

    /// <summary>The run of one row's pieces whose cells the view's columns hold.</summary>
    (int Start, int End) InView(int row, CellRange inView)
    {
        var (first, past) = (_rowOffsets[row - _window.FromY], _rowOffsets[row - _window.FromY + 1]);
        var start = FirstFrom(first, past, inView.FromX);
        return (start, FirstFrom(start, past, inView.ToX + 1));
    }

    /// <summary>The first piece of a row's run from <paramref name="lo"/> standing in a column at or past <paramref name="column"/>.</summary>
    int FirstFrom(int lo, int hi, int column)
    {
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (_window.ClampX(_window.Level.CellOf(_pieces[mid].CentreM.X)) < column) lo = mid + 1;
            else hi = mid;
        }

        return lo;
    }

}

/// <summary>
/// One prop of scenery as it is kept: where it stands and the look the plan gave it — twelve bytes, as the device reads
/// it too (<c>scenery.vert</c>'s <c>Piece</c>).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal readonly record struct SceneryPiece(Vector2 CentreM, uint Look);

/// <summary>One row's run of the scenery a frame draws (<see cref="ScenerySprites.Spans"/>): its first piece, and how many the frame draws before it.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct ScenerySpan(uint First, uint Before);
