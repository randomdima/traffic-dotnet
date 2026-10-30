using System.Numerics;
using TrafficSimulation.App.Screen;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.Multiplayer.Client;

/// <summary>
/// <b>A player's map</b> (MP-12), north up, drawn into a <see cref="MapFrame"/>. That frame is either the square in the
/// screen's lower left, showing <see cref="MultiplayerFigures.MapSpanM"/> round the player's car (<see cref="Corner"/>),
/// or the whole town fitted to the screen (<see cref="Whole"/>). The roads are lines, the water is a fill, and players
/// are arrows, each in its seat's own colour (<see cref="Theme.Player"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The map is laid once from the plan the player has already laid</b> (MP-5), as strokes and rows in metres. A frame
/// only places them and cuts them to the box, so it allocates nothing.
/// </para>
/// <para>
/// <b>A frame writes what its box shows and no more</b>. The corner's strokes are found through a grid of cells a corner
/// wide, so it visits the few cells round the player whatever the size of the town. The whole town is drawn off strokes
/// laid again for its scale — each road's run of strokes straightened wherever it bends less than a pixel — and its water
/// a row a pixel, so a traced city's map is a few thousand quads and not the overlay's whole room.
/// </para>
/// <para>
/// <b>Everything is cut to the box before it is written</b>, because the overlay has no scissor. A road is cut to a box
/// drawn in by half the road's width, so the stroke's corners stay inside the box too. The water is filled in rows
/// a corner map's pixel deep, with each row cut to the box.
/// </para>
/// </remarks>
internal sealed class Minimap
{
    readonly MultiplayerFigures _figures;
    readonly float _rowM;
    readonly Vector2 _worldSizeM;
    readonly Vector2[] _roadFromM;
    readonly Vector2[] _roadToM;
    readonly float[] _roadWidthM;

    /// <summary>Count + 1 entries: road run <c>r</c>'s strokes, end to end, are <c>_runFirst[r].._runFirst[r + 1]</c>.</summary>
    readonly int[] _runFirst;

    /// <summary>Count + 1 entries: water row <c>r</c>'s spans are <c>_waterM[_waterRows[r].._waterRows[r + 1]]</c>.</summary>
    readonly int[] _waterRows;

    /// <summary>One span of a row of water: its west end in X and its east end in Y.</summary>
    readonly Vector2[] _waterM;

    /// <summary>The grid the corner finds its strokes through: cells a corner wide, row by row from the town's corner.</summary>
    readonly float _cellM;
    readonly int _cellsX;
    readonly int _cellsY;

    /// <summary>Cells + 1 entries: cell <c>c</c>'s strokes are <c>_cellStrokes[_cellFirst[c].._cellFirst[c + 1]]</c>.</summary>
    readonly int[] _cellFirst;
    readonly int[] _cellStrokes;

    /// <summary>The frame each stroke was last drawn in, so a stroke in two cells is drawn once.</summary>
    readonly int[] _strokeDrawnAt;
    int _frames;

    /// <summary>The strokes straightened for <see cref="_straightMetresPerPx"/>, laid again only when that scale changes.</summary>
    readonly Vector2[] _straightFromM;
    readonly Vector2[] _straightToM;
    readonly float[] _straightWidthM;
    int _straightCount;
    float _straightMetresPerPx = float.NaN;

    public Minimap(CityPlan plan, SimConfig config)
    {
        _figures = config.Multiplayer;
        _rowM = config.MapMetresPerPx;
        _worldSizeM = plan.WorldSizeM;
        (_roadFromM, _roadToM, _roadWidthM, _runFirst) = Strokes(plan, _figures);
        (_waterRows, _waterM) = WaterRows(plan, _worldSizeM, _rowM);

        _cellM = _figures.MapSpanM;
        _cellsX = Math.Max(1, (int)MathF.Ceiling(_worldSizeM.X / _cellM));
        _cellsY = Math.Max(1, (int)MathF.Ceiling(_worldSizeM.Y / _cellM));
        (_cellFirst, _cellStrokes) = Cells();
        _strokeDrawnAt = new int[_roadWidthM.Length];
        _straightFromM = new Vector2[_roadWidthM.Length];
        _straightToM = new Vector2[_roadWidthM.Length];
        _straightWidthM = new float[_roadWidthM.Length];
    }

    /// <summary>The square in the screen's lower left, round <paramref name="centreM"/>.</summary>
    public MapFrame Corner(Vector2 uiPx, Vector2 centreM)
    {
        var sidePx = _figures.MapSidePx;
        var inPx = _figures.MapMarginPx + _figures.MapEdgePx;
        return new MapFrame(new Vector2(inPx, uiPx.Y - inPx - sidePx), new Vector2(sidePx), centreM, _rowM);
    }

    /// <summary>The whole town, as large as the screen holds it inside the margin, in the middle of the screen.</summary>
    public MapFrame Whole(Vector2 uiPx)
    {
        var roomPx = uiPx - new Vector2((_figures.MapMarginPx + _figures.MapEdgePx) * 2f);
        var metresPerPx = MathF.Max(_worldSizeM.X / roomPx.X, _worldSizeM.Y / roomPx.Y);
        var sizePx = _worldSizeM / metresPerPx;
        return new MapFrame((uiPx - sizePx) * 0.5f, sizePx, _worldSizeM * 0.5f, metresPerPx);
    }

    /// <summary>The map's frame, ground, water and roads.</summary>
    public void Draw(ref ScreenDraw draw, in MapFrame frame)
    {
        var edgePx = _figures.MapEdgePx;
        draw.Rect(frame.AtPx - new Vector2(edgePx), frame.SizePx + new Vector2(edgePx * 2f), Theme.MarkOutline);
        draw.Rect(frame.AtPx, frame.SizePx, Theme.MapOffTown);

        var townFromPx = Vector2.Max(frame.ToPx(Vector2.Zero), frame.AtPx);
        var townToPx = Vector2.Min(frame.ToPx(_worldSizeM), frame.EndPx);
        if (townFromPx.X < townToPx.X && townFromPx.Y < townToPx.Y) draw.Rect(townFromPx, townToPx - townFromPx, Theme.MapGround);

        DrawWater(ref draw, frame);
        DrawRoads(ref draw, frame);
    }

    /// <summary>
    /// A player's arrow at <paramref name="atM"/>, facing <paramref name="forward"/>, edged in dark. Nothing is drawn
    /// unless the whole arrow fits inside the box.
    /// </summary>
    public void Arrow(ref ScreenDraw draw, in MapFrame frame, Vector2 atM, Vector2 forward, Vector4 colour)
    {
        var edgePx = _figures.MapEdgePx;
        var lengthPx = _figures.MapArrowPx;
        var reachPx = new Vector2(lengthPx * 0.5f + edgePx);
        var middlePx = frame.ToPx(atM);
        if (!frame.Holds(middlePx - reachPx) || !frame.Holds(middlePx + reachPx)) return;

        Triangle(ref draw, middlePx, forward, lengthPx + edgePx * 2f, Theme.MarkOutline);
        Triangle(ref draw, middlePx, forward, lengthPx, colour);
    }

    /// <summary>
    /// A player's name beside its arrow at <paramref name="atM"/> (MP-15), cut short where the box ends to its right and
    /// not drawn where it would stand above or below the box.
    /// </summary>
    public void Name(ref ScreenDraw draw, in MapFrame frame, Vector2 atM, scoped ReadOnlySpan<char> name, Vector4 colour)
    {
        var heightPx = _figures.MapNamePx;
        var middlePx = frame.ToPx(atM);
        if (!frame.Holds(middlePx)) return;

        var atPx = middlePx + new Vector2(_figures.MapArrowPx * 0.5f + _figures.MapEdgePx * 2f, -heightPx * 0.5f);
        if (atPx.Y - 1f < frame.AtPx.Y || atPx.Y + heightPx + 1f > frame.EndPx.Y) return;

        draw.OutlinedTextFitted(atPx, name, heightPx, colour, frame.EndPx.X - atPx.X - 1f);
    }

    /// <summary>The water in rows, a row a pixel deep at most: where the frame is coarser than the rows, every few of them stand for the pixel.</summary>
    void DrawWater(ref ScreenDraw draw, in MapFrame frame)
    {
        var rows = _waterRows.Length - 1;
        var step = Math.Max(1, (int)(frame.MetresPerPx / _rowM));
        var firstRow = Math.Max(0, (int)MathF.Floor(frame.WorldAt(frame.AtPx).Y / _rowM)) / step * step;
        var lastRow = Math.Min(rows - 1, (int)MathF.Ceiling(frame.WorldAt(frame.EndPx).Y / _rowM));
        for (var row = firstRow; row <= lastRow; row += step)
        {
            var topPx = MathF.Max(frame.ToPx(new Vector2(0f, row * _rowM)).Y, frame.AtPx.Y);
            var bottomPx = MathF.Min(frame.ToPx(new Vector2(0f, (row + step) * _rowM)).Y, frame.EndPx.Y);
            if (bottomPx <= topPx) continue;

            for (var span = _waterRows[row]; span < _waterRows[row + 1]; span++)
            {
                var westPx = MathF.Max(frame.ToPx(new Vector2(_waterM[span].X, 0f)).X, frame.AtPx.X);
                var eastPx = MathF.Min(frame.ToPx(new Vector2(_waterM[span].Y, 0f)).X, frame.EndPx.X);
                if (eastPx > westPx) draw.Rect(new Vector2(westPx, topPx), new Vector2(eastPx - westPx, bottomPx - topPx), Theme.MapWater);
            }
        }
    }

    /// <summary>
    /// The roads in the box: at the corner's own scale or finer, the strokes of the cells the box covers; coarser, the
    /// strokes straightened for the frame's scale.
    /// </summary>
    void DrawRoads(ref ScreenDraw draw, in MapFrame frame)
    {
        if (frame.MetresPerPx > _rowM)
        {
            Straighten(frame.MetresPerPx);
            for (var stroke = 0; stroke < _straightCount; stroke++)
                DrawStroke(ref draw, frame, _straightFromM[stroke], _straightToM[stroke], _straightWidthM[stroke]);
            return;
        }

        _frames++;
        var leastM = Vector2.Max(frame.WorldAt(frame.AtPx), Vector2.Zero);
        var mostM = Vector2.Min(frame.WorldAt(frame.EndPx), _worldSizeM);
        var (fromX, fromY) = CellAt(leastM);
        var (toX, toY) = CellAt(mostM);
        for (var y = fromY; y <= toY; y++)
        {
            for (var x = fromX; x <= toX; x++)
            {
                var cell = y * _cellsX + x;
                for (var at = _cellFirst[cell]; at < _cellFirst[cell + 1]; at++)
                {
                    var stroke = _cellStrokes[at];
                    if (_strokeDrawnAt[stroke] == _frames) continue;

                    _strokeDrawnAt[stroke] = _frames;
                    DrawStroke(ref draw, frame, _roadFromM[stroke], _roadToM[stroke], _roadWidthM[stroke]);
                }
            }
        }
    }

    void DrawStroke(ref ScreenDraw draw, in MapFrame frame, Vector2 fromM, Vector2 toM, float roadWidthM)
    {
        var fromPx = frame.ToPx(fromM);
        var toPx = frame.ToPx(toM);
        var lengthPx = Vector2.Distance(fromPx, toPx);
        if (lengthPx <= 0f) return;

        // Each end carried on half the road's width, so a bend's strokes overlap at the joint rather than notch.
        var widthPx = MathF.Max(_figures.MapRoadLeastPx, roadWidthM / frame.MetresPerPx);
        var halfPx = widthPx * 0.5f;
        var along = (toPx - fromPx) / lengthPx;
        fromPx -= along * halfPx;
        toPx += along * halfPx;
        if (!Clipped(ref fromPx, ref toPx, frame.AtPx + new Vector2(halfPx), frame.EndPx - new Vector2(halfPx))) return;

        draw.LinePx(fromPx, toPx, widthPx, Theme.MapRoad);
    }

    (int X, int Y) CellAt(Vector2 pointM) => (
        Math.Clamp((int)(pointM.X / _cellM), 0, _cellsX - 1),
        Math.Clamp((int)(pointM.Y / _cellM), 0, _cellsY - 1));

    /// <summary>
    /// Every stroke filed in each cell its box touches — the box grown by the most a stroke's width and its end caps can
    /// reach at the corner's scale — so a cell's list holds every stroke that can show inside it.
    /// </summary>
    (int[] First, int[] Strokes) Cells()
    {
        var cells = _cellsX * _cellsY;
        var first = new int[cells + 1];
        for (var stroke = 0; stroke < _roadWidthM.Length; stroke++)
        {
            var (fromX, fromY, toX, toY) = CellsOf(stroke);
            for (var y = fromY; y <= toY; y++)
            {
                for (var x = fromX; x <= toX; x++) first[y * _cellsX + x + 1]++;
            }
        }

        for (var cell = 0; cell < cells; cell++) first[cell + 1] += first[cell];

        var strokes = new int[first[cells]];
        var filled = (int[])first.Clone();
        for (var stroke = 0; stroke < _roadWidthM.Length; stroke++)
        {
            var (fromX, fromY, toX, toY) = CellsOf(stroke);
            for (var y = fromY; y <= toY; y++)
            {
                for (var x = fromX; x <= toX; x++) strokes[filled[y * _cellsX + x]++] = stroke;
            }
        }

        return (first, strokes);
    }

    (int FromX, int FromY, int ToX, int ToY) CellsOf(int stroke)
    {
        var reachM = new Vector2(MathF.Max(_roadWidthM[stroke], _figures.MapRoadLeastPx * _rowM));
        var (fromX, fromY) = CellAt(Vector2.Min(_roadFromM[stroke], _roadToM[stroke]) - reachM);
        var (toX, toY) = CellAt(Vector2.Max(_roadFromM[stroke], _roadToM[stroke]) + reachM);
        return (fromX, fromY, toX, toY);
    }

    /// <summary>
    /// Each road's run of strokes laid again for <paramref name="metresPerPx"/>: a stroke carried on over the next while
    /// every point it passes stays within a pixel of it. Into the room laid with the map, so it allocates nothing.
    /// </summary>
    void Straighten(float metresPerPx)
    {
        if (metresPerPx == _straightMetresPerPx) return;

        _straightMetresPerPx = metresPerPx;
        _straightCount = 0;
        for (var run = 0; run + 1 < _runFirst.Length; run++)
        {
            var (first, end) = (_runFirst[run], _runFirst[run + 1]);
            var anchor = first;
            for (var stroke = first; stroke < end; stroke++)
            {
                var last = stroke == end - 1;
                if (!last && WithinAPixel(anchor, stroke + 1, metresPerPx)) continue;

                _straightFromM[_straightCount] = _roadFromM[anchor];
                _straightToM[_straightCount] = _roadToM[stroke];
                _straightWidthM[_straightCount++] = _roadWidthM[stroke];
                anchor = stroke + 1;
            }
        }
    }

    /// <summary>Whether the strokes from <paramref name="anchor"/> to <paramref name="to"/> all lie within a pixel of the one line from the first's start to the last's end.</summary>
    bool WithinAPixel(int anchor, int to, float metresPerPx)
    {
        var fromM = _roadFromM[anchor];
        var lineM = _roadToM[to] - fromM;
        var lengthM = lineM.Length();
        for (var stroke = anchor; stroke < to; stroke++)
        {
            var offM = _roadToM[stroke] - fromM;
            var awayM = lengthM > 0f ? MathF.Abs(offM.X * lineM.Y - offM.Y * lineM.X) / lengthM : offM.Length();
            if (awayM > metresPerPx) return false;
        }

        return true;
    }

    /// <summary>How wide an arrow's base is, as a share of its length.</summary>
    const float ArrowBaseShare = 0.5f;

    /// <summary>
    /// A filled triangle, nose along <paramref name="forward"/>, made from bars across it one pixel deep each, because
    /// the overlay draws quads and nothing else. Centred on <paramref name="middlePx"/>.
    /// </summary>
    static void Triangle(ref ScreenDraw draw, Vector2 middlePx, Vector2 forward, float lengthPx, Vector4 colour)
    {
        var across = new Vector2(-forward.Y, forward.X);
        var bars = Math.Max(1, (int)MathF.Ceiling(lengthPx));
        var deepPx = lengthPx / bars;
        var nosePx = middlePx + forward * (lengthPx * 0.5f);
        for (var bar = 0; bar < bars; bar++)
        {
            var share = (bar + 0.5f) / bars;
            var centrePx = nosePx - forward * (lengthPx * share);
            var halfPx = lengthPx * ArrowBaseShare * 0.5f * share;
            draw.LinePx(centrePx - across * halfPx, centrePx + across * halfPx, deepPx, colour);
        }
    }

    /// <summary>A segment cut to a box (Liang–Barsky), or false when none of it is inside.</summary>
    internal static bool Clipped(ref Vector2 fromPx, ref Vector2 toPx, Vector2 leastPx, Vector2 mostPx)
    {
        var along = toPx - fromPx;
        var enters = 0f;
        var leaves = 1f;
        if (!Edge(-along.X, fromPx.X - leastPx.X, ref enters, ref leaves) ||
            !Edge(along.X, mostPx.X - fromPx.X, ref enters, ref leaves) ||
            !Edge(-along.Y, fromPx.Y - leastPx.Y, ref enters, ref leaves) ||
            !Edge(along.Y, mostPx.Y - fromPx.Y, ref enters, ref leaves)) return false;

        var startPx = fromPx;
        fromPx = startPx + along * enters;
        toPx = startPx + along * leaves;
        return true;
    }

    static bool Edge(float towards, float room, ref float enters, ref float leaves)
    {
        if (towards == 0f) return room >= 0f;

        var at = room / towards;
        if (towards < 0f)
        {
            if (at > leaves) return false;
            enters = MathF.Max(enters, at);
        }
        else
        {
            if (at < enters) return false;
            leaves = MathF.Min(leaves, at);
        }

        return true;
    }

    /// <summary>
    /// Every road except a car park's bays as straight strokes, each bending no more than
    /// <see cref="MultiplayerFigures.MapChordTurnDeg"/>, carried on from each end to its junction's centre and drawn at
    /// the width of its lanes.
    /// </summary>
    /// <remarks>
    /// <b>A road stops at the edge of its junction's box</b>, and the box is ground of its own rather than a road, so a
    /// map of the roads alone has a hole at every junction. A stroke on to the centre closes it. <b>The lanes' width and not
    /// the carriageway's</b>, which carries a roadside's parking strip and would draw a street fatter where it has one.
    /// </remarks>
    static (Vector2[] FromM, Vector2[] ToM, float[] WidthM, int[] RunFirst) Strokes(CityPlan plan, MultiplayerFigures figures)
    {
        var mostTurnRad = figures.MapChordTurnDeg * (MathF.PI / 180f);
        var fromM = new List<Vector2>();
        var toM = new List<Vector2>();
        var widthM = new List<float>();
        var runFirst = new List<int> { 0 };
        var roads = plan.Roads;
        var junctionsM = plan.Junctions.CentreM;
        for (var road = 0; road < roads.Count; road++)
        {
            var pieces = roads.SegmentsOf(road);
            if (roads.IsABay(road) || pieces.IsEmpty) continue;

            var lanesM = roads.LanesWidthM(road);
            if (roads.FromJunction[road] is var first and >= 0) Add(junctionsM[first], pieces[0].StartM);

            foreach (var piece in pieces)
            {
                var strokes = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(piece.Curvature) * piece.LengthM / mostTurnRad));
                var lastM = piece.StartM;
                for (var stroke = 1; stroke <= strokes; stroke++)
                {
                    var atM = piece.PointAtM(piece.LengthM * stroke / strokes);
                    Add(lastM, atM);
                    lastM = atM;
                }
            }

            if (roads.ToJunction[road] is var last and >= 0) Add(pieces[^1].EndM, junctionsM[last]);
            runFirst.Add(fromM.Count);

            void Add(Vector2 aM, Vector2 bM)
            {
                fromM.Add(aM);
                toM.Add(bM);
                widthM.Add(lanesM);
            }
        }

        return (fromM.ToArray(), toM.ToArray(), widthM.ToArray(), runFirst.ToArray());
    }

    /// <summary>
    /// The water as one row <paramref name="rowM"/> deep for every such strip of the town: the spans between the
    /// crossings of the water's outline with the row's middle line, paired even–odd so an island inside the water is
    /// left dry.
    /// </summary>
    static (int[] Rows, Vector2[] SpansM) WaterRows(CityPlan plan, Vector2 worldSizeM, float rowM)
    {
        var outline = plan.Water.Outline;
        var rows = Math.Max(1, (int)MathF.Ceiling(worldSizeM.Y / rowM));
        var offsets = new int[rows + 1];
        var spans = new List<Vector2>();
        var crossings = new List<float>();
        for (var row = 0; row < rows; row++)
        {
            var yM = (row + 0.5f) * rowM;
            crossings.Clear();
            for (var ring = 0; ring < outline.Count; ring++)
            {
                var points = outline.RingOf(ring);
                for (var point = 0; point < points.Length; point++)
                {
                    var a = points[point];
                    var b = points[(point + 1) % points.Length];
                    if ((a.Y <= yM) != (b.Y <= yM)) crossings.Add(a.X + (yM - a.Y) / (b.Y - a.Y) * (b.X - a.X));
                }
            }

            crossings.Sort();
            for (var crossing = 0; crossing + 1 < crossings.Count; crossing += 2)
                spans.Add(new Vector2(crossings[crossing], crossings[crossing + 1]));

            offsets[row + 1] = spans.Count;
        }

        return (offsets, spans.ToArray());
    }
}

/// <summary>
/// Where a map is drawn: a box on the screen in interface pixels, the place in the town at its middle, and its scale.
/// </summary>
internal readonly record struct MapFrame(Vector2 AtPx, Vector2 SizePx, Vector2 CentreM, float MetresPerPx)
{
    public Vector2 EndPx => AtPx + SizePx;

    public Vector2 ToPx(Vector2 pointM) => AtPx + SizePx * 0.5f + (pointM - CentreM) / MetresPerPx;

    public Vector2 WorldAt(Vector2 pointPx) => CentreM + (pointPx - AtPx - SizePx * 0.5f) * MetresPerPx;

    /// <summary>Whether a point on the screen is inside the box, its edges included.</summary>
    public bool Holds(Vector2 pointPx) =>
        pointPx.X >= AtPx.X && pointPx.Y >= AtPx.Y && pointPx.X <= EndPx.X && pointPx.Y <= EndPx.Y;
}
