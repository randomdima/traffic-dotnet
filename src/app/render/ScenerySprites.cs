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
/// all a frame needs of one is its place, its look and its girth. Its look is chosen here, once, as a standing
/// prop's is; its size off that look's aspect when it is drawn.
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
    readonly Piece[] _pieces;

    /// <summary>
    /// Where each row of cells starts in <see cref="_pieces"/>, and no finer: the cells in view along a row are found by
    /// their column, so the index is a row's worth of integers and not a cell's — a city's cells are millions.
    /// </summary>
    readonly int[] _rowOffsets;

    /// <summary>The cull grid is the town's own (SIM-8), as <see cref="StandingSprites"/>' is.</summary>
    readonly GridWindow _window;

    /// <summary>How far the widest piece reaches from its centre, which a view is grown by.</summary>
    readonly float _reachM;

    /// <summary>Each look's half-size for a metre of diameter: its longest side the diameter, the other its aspect's.</summary>
    readonly Vector2[] _halfSizePerM;

    readonly int _firstSheet;
    readonly int _mostDrawn;

    ScenerySprites(
        Piece[] pieces, int[] rowOffsets, GridWindow window, float reachM, Vector2[] halfSizePerM, int firstSheet,
        int mostDrawn)
    {
        _pieces = pieces;
        _rowOffsets = rowOffsets;
        _window = window;
        _reachM = reachM;
        _halfSizePerM = halfSizePerM;
        _firstSheet = firstSheet;
        _mostDrawn = mostDrawn;
    }

    public static ScenerySprites Nothing { get; } =
        new([], [0], GridWindow.Of(new WorldGrid(1f).Main, 0, 0, 0, 0), 0f, [], 0, 0);

    public int Count => _pieces.Length;

    /// <summary>How many instances a frame of the town's scenery needs at most, which is what the buffer is laid for.</summary>
    public static int CapacityFor(CityPlan plan, int mostDrawn) => Math.Min(plan.Scenery.Count, mostDrawn);

    /// <param name="worldSizeM">The town's extent, which the cull grid covers.</param>
    /// <param name="level">The level of the grid the pieces are filed at — its main one.</param>
    public static ScenerySprites Lay(
        CityPlan.SceneryArrays scenery, Vector2 worldSizeM, PropCatalog props, int firstPropSheet, ReadOnlySpan<float> aspects,
        GridLevel level, int mostDrawn)
    {
        if (scenery.Count == 0) return Nothing;

        var halfSizePerM = new Vector2[props.Count];
        for (var variant = 0; variant < halfSizePerM.Length; variant++)
        {
            var aspect = aspects[firstPropSheet + variant];
            halfSizePerM[variant] = new Vector2(aspect, 1f) * (0.5f / MathF.Max(aspect, 1f));
        }

        // A counting sort, as the standing instances are filed: the offsets are the running total, and the second
        // pass drops each piece where its cell says. What is kept of them is where each row starts.
        var window = GridWindow.Over(level, Vector2.Zero, Vector2.Max(worldSizeM, Vector2.Zero));
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

        var pieces = new Piece[scenery.Count];
        var reachM = 0f;
        for (var at = 0; at < scenery.Count; at++)
        {
            var diameterM = scenery.RadiusM[at] * 2f;
            var variant = props.Look((int)PropKind.WildNature, diameterM, at);
            reachM = MathF.Max(reachM, (halfSizePerM[variant] * diameterM).Length());
            pieces[offsets[cellOf[at]]++] = new Piece(scenery.CentreM[at], (ushort)variant, (Half)diameterM);
        }

        return new ScenerySprites(pieces, rowOffsets, window, reachM, halfSizePerM, firstPropSheet, mostDrawn);
    }

    /// <summary>Writes the scenery the view touches, thinned to the budget, and answers how many.</summary>
    public int Fill(Vector2 viewCentreM, Vector2 viewSpanM, Span<SpriteInstance> into)
    {
        var most = Math.Min(_mostDrawn, into.Length);
        if (_pieces.Length == 0 || most <= 0) return 0;

        var half = (viewSpanM * 0.5f) + new Vector2(_reachM);
        _window.TryRange(viewCentreM - half, viewCentreM + half, out var inView);

        var inViewCount = 0L;
        for (var row = inView.FromY; row <= inView.ToY; row++)
        {
            var (start, end) = InView(row, inView);
            inViewCount += end - start;
        }

        var every = 1;
        while (inViewCount > (long)most * every) every <<= 1;

        var written = 0;
        for (var row = inView.FromY; row <= inView.ToY && written < most; row++)
        {
            var (start, end) = InView(row, inView);
            for (var piece = (start + every - 1) / every * every; piece < end && written < most; piece += every)
            {
                ref readonly var drawn = ref _pieces[piece];
                into[written++] = new SpriteInstance(
                    drawn.CentreM, _halfSizePerM[drawn.Variant] * (float)drawn.DiameterM, Vector2.Zero, Vector2.One,
                    PersonSprites.Plain, (uint)(_firstSheet + drawn.Variant), 0f);
            }
        }

        return written;
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

    /// <summary>One prop of scenery as it is kept: where it stands, the look it was given and its girth.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    readonly record struct Piece(Vector2 CentreM, ushort Variant, Half DiameterM);
}
