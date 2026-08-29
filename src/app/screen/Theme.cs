using System.Numerics;

namespace TrafficSimulation.App.Screen;

/// <summary>A rectangle on screen, in interface pixels from the top-left.</summary>
/// <remarks>
/// A panel lays its rows out once a frame and keeps them, so what was drawn and what a click is
/// tested against are the same rectangles. Two copies of a layout eventually disagree, and a button
/// that is not where it is drawn is the way that disagreement shows.
/// </remarks>
internal readonly record struct Rect(Vector2 AtPx, Vector2 SizePx)
{
    public float Right => AtPx.X + SizePx.X;

    public float Bottom => AtPx.Y + SizePx.Y;

    public bool Contains(Vector2 pointPx) =>
        pointPx.X >= AtPx.X && pointPx.X < Right && pointPx.Y >= AtPx.Y && pointPx.Y < Bottom;

    public Rect Inset(float px) => new(AtPx + new Vector2(px), SizePx - new Vector2(px * 2f));
}

/// <summary>
/// What the interface is drawn in. One place, because the alternative is a colour invented per panel
/// and an interface that reads as several.
/// </summary>
/// <remarks>
/// Every figure here is about the interface and not about the town, so none belongs in
/// <see cref="Shared.Config.SimConfig"/>: a panel's padding is not a fact about traffic.
/// </remarks>
internal static class Theme
{
    public static readonly Vector4 Panel = new(0.06f, 0.07f, 0.10f, 0.94f);
    public static readonly Vector4 PanelEdge = new(0.32f, 0.37f, 0.46f, 0.85f);

    /// <summary>Under a panel and offset from it, which is the only depth a flat quad can carry.</summary>
    public static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.35f);

    public static readonly Vector4 Text = new(0.90f, 0.92f, 0.95f, 1f);
    public static readonly Vector4 Dim = new(0.58f, 0.62f, 0.68f, 1f);
    public static readonly Vector4 Heading = new(1f, 0.83f, 0.42f, 1f);

    /// <summary>
    /// What a row is when nothing is happening to it — a face, so a list reads as rows rather than as
    /// outlines. <b>The three row colours are opaque</b>, unlike the panel they sit on: a rounded row
    /// is its outline with its face laid over the top, and a face that let the outline through would
    /// be a row tinted by its own border.
    /// </summary>
    public static readonly Vector4 RowRest = new(0.11f, 0.13f, 0.17f, 1f);

    public static readonly Vector4 RowHover = new(0.18f, 0.22f, 0.28f, 1f);
    public static readonly Vector4 RowPicked = new(0.12f, 0.24f, 0.23f, 1f);

    /// <summary>The one bright colour on the chrome: the strip down a picked tab and the edge of a hovered row.</summary>
    public static readonly Vector4 Accent = new(0.36f, 0.78f, 0.69f, 1f);

    public static readonly Vector4 Danger = new(0.62f, 0.23f, 0.22f, 1f);

    /// <summary>
    /// A claim the town on screen has broken, as <em>text</em> — <see cref="Danger"/> is a face to put a
    /// word on and is far too dark to read one in. It is the only red the chrome writes with, so a panel
    /// read down its left edge has exactly one thing in it that catches the eye.
    /// </summary>
    public static readonly Vector4 Broken = new(0.95f, 0.38f, 0.36f, 1f);

    /// <summary>The hairline under a panel's title, which is what keeps the title off the first row.</summary>
    public static readonly Vector4 Rule = new(1f, 1f, 1f, 0.08f);

    /// <summary>
    /// The instruments' colours, one per layer, so two layers on at once stay tellable apart.
    /// <b>Crimson is a constraint and never a route</b> — what a driver was told to stop at or found
    /// ahead of it. A route is drawn in the body's own colour from <see cref="AgentLine"/>, line and
    /// marks alike.
    /// </summary>
    public static readonly Vector4 HeldLine = new(0.85f, 0.22f, 0.24f, 0.95f);

    /// <summary>
    /// <b>One colour per body, taken by its index, and the whole of its route drawn in it</b> — the line,
    /// the marks down the line and the discs at either end. Which body a line belongs to is the question
    /// the agent layers exist to answer, and six colours in a crowd answer it where one colour for every
    /// car cannot.
    /// </summary>
    /// <remarks>
    /// Six hues spread round the wheel, avoiding the green of grass and the grey of tarmac, and held at a
    /// mid lightness: the layer sits over a town somebody is also looking at, so a route has to be
    /// followable without being the brightest thing on screen. Crimson is left out of it — that is
    /// <see cref="HeldLine"/>, and a route in it would read as a constraint.
    /// </remarks>
    public static Vector4 AgentLine(int agent) => AgentLines[agent % AgentLines.Length];

    static readonly Vector4[] AgentLines =
    [
        new(0.95f, 0.68f, 0.28f, 0.95f),
        new(0.80f, 0.78f, 0.36f, 0.95f),
        new(0.30f, 0.80f, 0.72f, 0.95f),
        new(0.38f, 0.62f, 0.95f, 0.95f),
        new(0.72f, 0.52f, 0.95f, 0.95f),
        new(0.95f, 0.50f, 0.42f, 0.95f),
    ];

    public static readonly Vector4 DrivingNodes = new(1f, 0.55f, 0.15f, 0.95f);

    /// <summary>
    /// Ground of the driving network a car covers <em>backwards</em> — at present the way out of a bay,
    /// which is the town's one reversing movement (GEN-4f). <b>A shade of the driving colour and not a
    /// colour of its own</b>: it is a way of the same book, and all the shade adds is which end of the car
    /// leads down it.
    /// </summary>
    public static readonly Vector4 DrivingReverse = new(1f, 0.83f, 0.45f, 0.95f);

    public static readonly Vector4 WalkingNodes = new(0.30f, 0.90f, 0.45f, 0.95f);
    public static readonly Vector4 Collision = new(0.95f, 0.35f, 0.85f, 0.85f);

    /// <summary>
    /// The circle a car's steering says it is turning (OBS-2j). <b>Cold, and its own colour</b>: it is the
    /// one thing this overlay draws that the simulation did not produce, and it is read against a black
    /// tyre track on grey tarmac — so it has to be told from both at a glance.
    /// </summary>
    public static readonly Vector4 TurnCircle = new(0.35f, 0.85f, 1f, 0.95f);

    /// <summary>
    /// The stretches of either book that belong to no body at all — the town's own furniture, standing on
    /// the lanes it stands on. <b>Ground a body is on, was granted or has claimed takes that body's own
    /// colour</b> from <see cref="AgentLine"/>, in whichever book it is written: a block, the line running
    /// out of it and the body's own sprite are one walker's or one car's. One wash covers all of it — where
    /// one stretch of that ground stops and the next starts is a bar drawn across the way, never a hue and
    /// never a second wash.
    /// </summary>
    public static readonly Vector4 LaneObstruction = new(0.74f, 0.42f, 0.98f, 0.95f);

    public static readonly Vector4 RulerTape = new(1f, 0.95f, 0.35f, 1f);

    /// <summary>The brackets round the selected unit — the chrome's own accent, so the one mark standing on the town reads as the interface talking rather than as something the town is doing.</summary>
    public static readonly Vector4 SelectionMark = Accent;

    /// <summary>
    /// The box a drag lays over the town (CTL-1b), as the wash inside it and the line round it. The
    /// same accent the brackets wear, since it is the same question being asked of several units at
    /// once — and a wash light enough that what is inside it stays the thing being looked at.
    /// </summary>
    public static readonly Vector4 SelectionBox = new(Accent.X, Accent.Y, Accent.Z, 0.14f);

    public static readonly Vector4 SelectionBoxEdge = Accent;

    /// <summary>
    /// The selected unit's own path (CTL-1a), in the same accent the brackets wear: the line and the unit
    /// it runs out of are one answer to one question, and a colour of its own would read as a third layer
    /// standing beside the two the switches already draw.
    /// </summary>
    public static readonly Vector4 SelectionPath = Accent;

    /// <summary>
    /// And the mark on the end of it. <b>The one green in the interface</b>, because it is the only thing
    /// the chrome draws that is an <em>end</em> rather than a way — brighter and far bolder than the
    /// walking network's hairline web (<see cref="WalkingNodes"/>), which is what keeps the two apart with
    /// both on screen.
    /// </summary>
    public static readonly Vector4 SelectionGoal = new(0.25f, 1f, 0.38f, 1f);

    public static readonly Vector4 Legend = new(0.97f, 0.97f, 0.97f, 1f);
    public static readonly Vector4 LegendShadow = new(0.05f, 0.05f, 0.06f, 0.85f);

    public const float TextPx = 15f;
    public const float SmallTextPx = 13f;
    public const float HeadingPx = 22f;
    public const float EdgePx = 1f;

    /// <summary>
    /// <b>The five figures every panel is laid out from</b>, and no panel here holds a sixth of its
    /// own: what a panel keeps clear of the window's edge, what it keeps clear of its own, what it
    /// keeps between two things inside it, how far into a row its text starts, and how tall a row is.
    /// Two panels that pick their own spacing read as two interfaces, which is exactly what the shared
    /// colours above are here to prevent.
    /// </summary>
    /// <remarks>
    /// <see cref="MarginPx"/> is the one that is a fact about the <em>window</em> rather than about a
    /// panel: a popup hung off a corner button has to reach the same edge that button does, and two
    /// figures for it is a panel that cannot line up with what opened it.
    /// </remarks>
    public const float MarginPx = 12f;

    public const float PaddingPx = 14f;

    public const float GapPx = 6f;
    public const float InsetPx = 12f;
    public const float RowPx = 32f;

    /// <summary>A row that carries a name and the line under it saying what it is.</summary>
    public const float TallRowPx = 48f;

    /// <summary>The strip down the leading edge of a picked tab or a hovered row.</summary>
    public const float AccentPx = 3f;

    /// <summary>
    /// The settings gear in the top-right corner. It is chrome rather than one panel's own figure
    /// because everything hung under that corner measures off it, and two of them are not the same
    /// slice.
    /// </summary>
    public const float GearPx = 30f;

    /// <summary>
    /// How far the corners are taken off — the panel's, and the smaller one every row inside it takes.
    /// <b>Small on purpose</b>: the curve is a quarter of the glyph sheet's disc, and a radius large
    /// enough to read as a shape of its own would be drawing a 7-pixel disc twenty pixels across.
    /// </summary>
    public const float PanelRadiusPx = 8f;

    public const float RowRadiusPx = 5f;

    /// <summary>What a row's own text is inset by, either side, so a caller can measure what will fit in one.</summary>
    public static float FitWidthPx(Rect row) => row.SizePx.X - InsetPx * 2f;

    /// <summary>
    /// Where a popup hung off a corner button goes: under it, aligned to its trailing edge, and never
    /// off the window. <b>One site</b>, so two popups on one corner cannot line up differently.
    /// </summary>
    public static Vector2 PopupAt(Rect anchor, Vector2 uiPx, float widthPx) => new(
        Math.Clamp(anchor.Right - widthPx, MarginPx, MathF.Max(MarginPx, uiPx.X - MarginPx - widthPx)),
        anchor.Bottom + GapPx);

    /// <summary>The panel over the town: a shadow under it, its outline, and its face laid inside that.</summary>
    public static void Frame(ref ScreenDraw draw, Rect box)
    {
        draw.RoundedRect(box.AtPx + new Vector2(ShadowOffsetPx), box.SizePx, PanelRadiusPx, Shadow);
        Outlined(ref draw, box, PanelRadiusPx, Panel, PanelEdge);
    }

    const float ShadowOffsetPx = 4f;

    /// <summary>
    /// A rounded rectangle with a hairline round it, which is the outline drawn first and the face
    /// laid a pixel inside it — the only way this pipeline can put a border on a curve.
    /// </summary>
    static void Outlined(ref ScreenDraw draw, Rect box, float radiusPx, Vector4 face, Vector4 edge)
    {
        draw.RoundedRect(box.AtPx, box.SizePx, radiusPx, edge);
        draw.RoundedRect(box.Inset(EdgePx).AtPx, box.Inset(EdgePx).SizePx, radiusPx - EdgePx, face);
    }

    /// <summary>The hairline under a panel's title, drawn across its content column.</summary>
    public static void Separator(ref ScreenDraw draw, Vector2 atPx, float widthPx) =>
        draw.Rect(atPx, new Vector2(widthPx, EdgePx), Rule);

    /// <summary>
    /// One pressable row, wherever it is: a list row, a tab, a footer button. <b>Every one of them is
    /// drawn here</b>, so the inset its label sits at and the way it answers a pointer cannot differ
    /// between two pages.
    /// </summary>
    public static void Face(
        ref ScreenDraw draw, Rect box, Vector2 pointerPx, Vector4? rest = null, bool picked = false,
        Vector4? hover = null)
    {
        var hovered = box.Contains(pointerPx);
        Outlined(
            ref draw, box, RowRadiusPx, hovered ? hover ?? RowHover : rest ?? RowRest,
            hovered || picked ? Accent : PanelEdge);

        // The strip stops short of both corners, since the corner it would run into is no longer there.
        if (hovered || picked)
        {
            draw.Rect(
                box.AtPx + new Vector2(0f, RowRadiusPx), new Vector2(AccentPx, box.SizePx.Y - RowRadiusPx * 2f), Accent);
        }
    }

    /// <summary>
    /// A row with one line of text in it, centred on its height and inset from its leading edge.
    /// <b><paramref name="rest"/> is what it is, not what it does under a pointer</b>: a button that
    /// only turns red once the pointer is on it has said nothing to whoever was deciding where to put
    /// the pointer.
    /// </summary>
    public static void Button(
        ref ScreenDraw draw, Rect box, Vector2 pointerPx, scoped ReadOnlySpan<char> label, Vector4? rest = null,
        float textPx = TextPx)
    {
        Face(ref draw, box, pointerPx, rest);
        draw.TextFitted(
            box.AtPx + new Vector2(InsetPx, (box.SizePx.Y - textPx) * 0.5f), label, textPx, Text, FitWidthPx(box));
    }
}
