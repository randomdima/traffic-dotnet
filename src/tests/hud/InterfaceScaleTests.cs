using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using Xunit;

namespace TrafficSimulation.Tests.Hud;

/// <summary>
/// OBS-2k: how dense the interface is drawn. <b>The display's own factor until the panels would run off
/// the glass</b>, and then whatever leaves them on it — which is the whole of what makes the same
/// interface readable on a desktop and usable on a handset.
/// </summary>
/// <remarks>
/// It is beside <see cref="PointerSpaceTests"/> because it is the other half of the same question: that
/// one is what space the pointer arrives in, this one is how big that space is.
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
public class InterfaceScaleTests
{
    static readonly Vector2 LeastUiPx =
        new(SimConfig.Shipped().View.InterfaceLeastWidthPx, SimConfig.Shipped().View.InterfaceLeastHeightPx);

    /// <summary>
    /// <b>An ordinary desktop window is not capped at all</b>, which is what keeps every reference frame
    /// the picture it already was: the cap binds where the panels would not fit, and on 1600 × 900 at 1×
    /// they fit several times over.
    /// </summary>
    [Fact]
    public void AnUnscaledDesktopWindowIsLeftAlone()
    {
        Assert.Equal(1f, Fitted(displayScale: 1f, framebufferPx: new Vector2(1600f, 900f)));
        Assert.Equal(2f, Fitted(displayScale: 2f, framebufferPx: new Vector2(3200f, 1800f)));
    }

    /// <summary>
    /// And a handset is: three device pixels to the point over a 390-point viewport is 390 interface
    /// pixels across, and the menu alone is wider than that. What comes back is the density that leaves
    /// the window exactly as many interface pixels as the panels were laid for.
    /// </summary>
    [Fact]
    public void AHandsetComesDownToWhatThePanelsNeed()
    {
        var framebufferPx = new Vector2(1170f, 2532f);

        var scale = Fitted(displayScale: 3f, framebufferPx);

        Assert.True(scale < 3f, "a handset was laid out at its own device pixel ratio");
        Assert.Equal(LeastUiPx.X, framebufferPx.X / scale, tolerance: 1e-2f);
        Assert.True(framebufferPx.Y / scale >= LeastUiPx.Y, "the short side was fitted and the long one was not");
    }

    /// <summary>The other way up: a phone on its side is capped by its height rather than its width.</summary>
    [Fact]
    public void WhicheverSideIsShortIsTheOneThatBinds()
    {
        var framebufferPx = new Vector2(2532f, 1170f);

        var scale = Fitted(displayScale: 3f, framebufferPx);

        Assert.Equal(LeastUiPx.Y, framebufferPx.Y / scale, tolerance: 1e-2f);
        Assert.True(framebufferPx.X / scale >= LeastUiPx.X);
    }

    /// <summary>
    /// <b><c>--ui-scale</c> is not capped.</b> Naming one at all says the guess underneath was wrong,
    /// and a figure asked for and then quietly moved is a switch that does nothing.
    /// </summary>
    [Fact]
    public void AnAskedForScaleIsHandedBackWhole()
    {
        Assert.Equal(3f, InterfaceScale.Fitted(3f, displayScale: 1f, new Vector2(1170f, 2532f), LeastUiPx));
    }

    /// <summary>A window with no size divides by nothing, and a run with no figures asks for no cap.</summary>
    [Fact]
    public void NoWindowAndNoFigureBothLeaveTheDisplaysOwn()
    {
        Assert.Equal(2f, InterfaceScale.Fitted(0f, displayScale: 2f, Vector2.Zero, LeastUiPx));
        Assert.Equal(3f, InterfaceScale.Fitted(0f, displayScale: 3f, new Vector2(390f, 844f), Vector2.Zero));
    }

    static float Fitted(float displayScale, Vector2 framebufferPx) =>
        InterfaceScale.Fitted(0f, displayScale, framebufferPx, LeastUiPx);
}
