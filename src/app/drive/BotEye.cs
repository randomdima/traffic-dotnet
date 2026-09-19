using TrafficSimulation.App.Render;
using TrafficSimulation.App.Shot;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Drive;

/// <summary>
/// <b>DRV-8 — what a second driver can see.</b> One frame of the town it is driving in, drawn offscreen on
/// the device the window is already using, centred on its own car and <b>with no interface in it at all</b>
/// — no panel, no menu, no switch's overlay, no caption.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the one shot path</b> (SHT-1, <see cref="ShotStage"/>): a frame a bot is given is the frame the
/// game draws, at a camera of its own. That is the whole of why the eye exists — the window's camera, its
/// panels and its switches belong to whoever is watching, and a driver looking through them would be
/// driving on somebody else's screen.
/// </para>
/// <para>
/// <b>Bare is the point and not a default</b>: the debug layers are readings about a town rather than
/// things in it, so a driver shown the lines, the claims and the turn circles would be driving on what the
/// town knows about itself.
/// </para>
/// </remarks>
internal sealed class BotEye : IDisposable
{
    readonly ShotStage _stage;
    readonly Hud.Interface _bare;
    readonly SimConfig _config;
    readonly int _widthPx;
    readonly int _heightPx;

    public BotEye(
        Vk vk, CityPlan plan, GroundMesh ground, TownSprites looks, SimConfig config, int widthPx, int heightPx)
    {
        _config = config;
        _widthPx = widthPx;
        _heightPx = heightPx;
        _stage = ShotStage.On(vk, plan, ground, looks, config, widthPx, heightPx);

        // The interface a frame with `none` in it draws, which is none of it — built once, because what it
        // would draw never changes and a stage still wants one to ask about the ground.
        _bare = ShotStage.Dressed(
            new ShotRequest(
                Map: string.Empty, Path: string.Empty, WidthPx: widthPx, HeightPx: heightPx, Ui: ["none"]),
            config);
    }

    /// <summary>
    /// The frame a <c>shot</c> step asked for, written where the step said. <b>Everything about what is in
    /// it is the step's</b>: where to stand and how much town to span, and nothing of the window's.
    /// </summary>
    public string Draw(in DriveFrame wanted, TownWorld world, long tick)
    {
        var ask = new ShotRequest(
            Path: wanted.Path, WidthPx: _widthPx, HeightPx: _heightPx, Map: string.Empty, AtM: wanted.AtM,
            ViewM: wanted.ViewM, Ui: ["none"]);

        var report = _stage.Draw(ask, _bare, world, tick, [], default);
        return $"{report.Path} — {report.WidthPx}x{report.HeightPx}, {report.PxPerM:F1} px/m, no interface in it";
    }

    public void Dispose() => _stage.Dispose();
}
