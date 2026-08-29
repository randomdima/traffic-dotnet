using System.Numerics;
using TrafficSimulation.App.Screen;

namespace TrafficSimulation.App.Hud;

/// <summary>
/// <b>CTL-1b: the box a drag draws over the town</b>, while the button is down. It is drawn on screen
/// and not on the ground, because it is a gesture rather than a place: it stands where the pointer has
/// been, and a camera that panned under it would drag the box with it.
/// </summary>
/// <remarks>
/// A wash and an outline, and nothing inside it. What the box is about is the units under it, so a
/// figure or a count drawn in the middle of it would be the interface writing over the answer.
/// </remarks>
internal static class Marquee
{
    const float EdgePx = 1f;

    /// <summary>The box between two corners, in the pixels the interface is laid out in.</summary>
    public static Rect Between(Vector2 onePx, Vector2 otherPx) =>
        new(Vector2.Min(onePx, otherPx), Vector2.Abs(otherPx - onePx));

    public static void Draw(ref ScreenDraw draw, Rect boxPx)
    {
        if (boxPx.SizePx.X <= 0f || boxPx.SizePx.Y <= 0f) return;

        draw.Rect(boxPx.AtPx, boxPx.SizePx, Theme.SelectionBox);
        draw.Outline(boxPx.AtPx, boxPx.SizePx, EdgePx, Theme.SelectionBoxEdge);
    }
}
