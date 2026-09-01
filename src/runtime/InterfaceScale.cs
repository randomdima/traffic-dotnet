using System.Numerics;

namespace TrafficSimulation.Runtime;

/// <summary>
/// <b>OBS-2k — how dense the interface is drawn.</b> The display's own factor, capped by what the
/// panels need: an interface pixel is worth as many of the display's as the platform says, right up
/// until that would leave the window too few interface pixels for a panel to stand in.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is one file because both machines answer it</b>, unlike the two <c>AppWindow</c>s that ask.
/// A desktop at 1× and a 1600 × 900 window is never capped and every reference frame is what it was;
/// a phone reporting 3× on a 390-point viewport is, because 390 interface pixels is narrower than the
/// menu — and what the cap hands back there is the density that leaves the menu on the glass.
/// </para>
/// <para>
/// <b><c>--ui-scale</c> is not capped.</b> Naming one at all says the guess underneath was wrong, and
/// a figure asked for and then quietly moved is a switch that does nothing.
/// </para>
/// </remarks>
internal static class InterfaceScale
{
    /// <summary>
    /// Below this the glyphs are drawn under a pixel apiece and the interface is a smear. A window
    /// this small has no answer worth giving; it gets the least bad one rather than a division by a
    /// window nobody can read anyway.
    /// </summary>
    const float LeastScale = 0.5f;

    /// <param name="wantedUiScale">What <c>--ui-scale</c> asked for, or zero when it asked for nothing.</param>
    /// <param name="displayScale">The platform's own factor: the desktop's scale, or a page's device pixel ratio.</param>
    /// <param name="framebufferPx">The window in the display's own pixels, which is what the cap is measured against.</param>
    /// <param name="leastUiPx">The window the interface is never laid out on fewer pixels than, or zero for no cap.</param>
    public static float Fitted(float wantedUiScale, float displayScale, Vector2 framebufferPx, Vector2 leastUiPx)
    {
        if (wantedUiScale > 0f) return wantedUiScale;

        var scale = MathF.Max(LeastScale, displayScale);
        if (leastUiPx.X > 0f && framebufferPx.X > 0f) scale = MathF.Min(scale, framebufferPx.X / leastUiPx.X);
        if (leastUiPx.Y > 0f && framebufferPx.Y > 0f) scale = MathF.Min(scale, framebufferPx.Y / leastUiPx.Y);

        return MathF.Max(LeastScale, scale);
    }
}
