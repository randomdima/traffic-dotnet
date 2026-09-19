using System.Numerics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.Bench;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;

using TrafficSimulation.World.Statics;

namespace TrafficSimulation.App.Shot;

/// <summary>
/// One frame of a town drawn into an image with no window under it and written out as a PNG.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is one shot path and this is it.</b> <c>--shot</c>, <c>--sheet</c> and the end-to-end
/// visual tests in <c>src/tests/e2e/</c> all come through here, because a frame taken through a second
/// drawing path would be a picture of that path rather than of the game: the panels and the layers are
/// drawn by the same <see cref="Hud.Interface"/> the windowed game draws them with.
/// </para>
/// <para>
/// It needs no display, no compositor and nothing to steal focus, and the picture is exactly the
/// size it was asked for rather than whatever the desktop's scale factor made of a window.
/// </para>
/// <para>
/// <b>What it writes is the game's own picture and nothing else</b> (SHT-1). A caption, a scale bar or
/// a cell label belongs under the frame and is <see cref="Caption"/>'s, so that what a reviewer judges
/// and what the game draws are the same pixels.
/// </para>
/// </remarks>
internal static class ShotRun
{
    /// <summary>
    /// Stage one scenario and photograph it. Everything that decides what the frame contains is in
    /// <paramref name="ask"/>, so the same request reproduces the same picture: the map and the
    /// tick are the town, and the camera is pinned rather than left wherever it was.
    /// </summary>
    public static ShotReport Take(ShotRequest ask, SimConfig config)
    {
        TownStanding? alone = null;
        try
        {
            return Take(ask, config, ref alone);
        }
        finally
        {
            alone?.Dispose();
        }
    }

    /// <summary>
    /// The same frame, with the town it is of <b>kept between frames of the same town</b>
    /// (<see cref="TownStanding"/>) — what a sheet of nine cells wants, since standing a city up is most of
    /// a minute and drawing it is a fraction of a second.
    /// </summary>
    public static ShotReport Take(ShotRequest ask, SimConfig config, ref TownStanding? standing)
    {
        var ui = ShotStage.Dressed(ask, config);

        // GEN-1b in a picture: the start menu stands over the idle ring, so a picture of it is a picture of
        // the map that was asked for with the panel on top. Which map that is, is the request's.
        var plan = Maps.Plan(ask.Map, config, BuildingCatalog.Roofs);
        using var stage = ShotStage.For(plan, config, ask.WidthPx, ask.HeightPx, ask.Validate);

        standing = TownStanding.For(ask, config, plan, standing);
        var world = standing.World;

        // A shot of a town that has never ticked is a town of walkers standing on their spawns, which
        // is a picture of the plan rather than of the simulation. Seconds says how far in.
        var loop = new SimLoop<TownWorld>(world, config);
        loop.Timed = ui.Status.Open;
        world.Timed = ui.Status.Open;

        // What the map claims about itself is answered a tick at a time, so the run is advanced one at a
        // time and watched — which is what makes a picture of either panel a picture of the same run.
        var scenario = Scenarios.For(world, config);
        var ticks = (int)(ask.Seconds * config.Sim.TickRateHz);
        for (var tick = 0; tick < ticks; tick++)
        {
            loop.Advance();
            foreach (var watch in scenario) watch.Saw(world);
        }

        standing.Ticked(ticks);

        // The phases and nothing else: there is no window to time on this path, so the read-out says the
        // frame was not measured rather than printing the zero it would come to.
        return stage.Draw(
            ask, ui, world, loop.Tick, scenario, new FrameFigures { Phases = loop.Phases, Sub = world.Sub });
    }
}

/// <summary>
/// <b>A town stood up for a frame, held on to while the frames are of the same town.</b> Standing a city up
/// is most of a minute — the walking side of it alone is three quarters of that — and drawing a frame of one
/// is a fraction of a second, so a sheet that stood its town up per cell spent nine tenths of itself laying
/// the same town nine times.
/// </summary>
/// <remarks>
/// <b>Only a town nobody has ticked is handed on</b>, which is what a review sheet asks for and what the
/// default <c>--seconds</c> is. A frame is drawn off a town without changing it, so every cell of a sheet of
/// an unticked town is a frame of one town at one tick; a cell that asks for seconds is a run of the
/// simulation and gets a town of its own, because a town cannot be wound back to where the last cell left it
/// and a caption that said otherwise would be the one thing a review picture may not do (SHT-2).
/// </remarks>
internal sealed class TownStanding : IDisposable
{
    TownStanding(string map, TownWorld world)
    {
        Map = map;
        World = world;
    }

    public TownWorld World { get; }

    string Map { get; }

    bool Ran { get; set; }

    public static TownStanding For(in ShotRequest ask, SimConfig config, CityPlan plan, TownStanding? standing)
    {
        if (standing is { Ran: false } kept && string.Equals(kept.Map, ask.Map, StringComparison.Ordinal))
        {
            return kept;
        }

        standing?.Dispose();
        return new TownStanding(ask.Map, new TownWorld(plan, config));
    }

    /// <summary>Told how far the frame ran the town on, since one that ran at all is nobody else's to draw.</summary>
    public void Ticked(int ticks) => Ran |= ticks > 0;

    public void Dispose() => World.Dispose();
}

/// <summary>
/// What to photograph. <see cref="ViewM"/> is the span across the frame's <b>short</b> side, as
/// <c>--view</c> asks for it, and <see cref="AtM"/> is where the camera is pinned — a named place of
/// the map, never a search of the town. <see cref="Ui"/> is the <c>--ui</c> word list, matched whole
/// by <see cref="Interface.Apply"/>: <c>none</c> is a bare frame of the town, an empty list is the
/// ordinary interface, and the rest name a layer or a menu page.
/// </summary>
/// <param name="Seconds">How far into a seeded run the frame is taken. Zero is the plan rather than
/// the simulation: every walker still standing on its spawn.</param>
/// <param name="TurnDeg">
/// How far the town is turned in the frame, clockwise from north-up (OBS-1c), about the middle of it —
/// which is what makes the turn a thing a picture can be judged on rather than only a thing a hand can
/// do. Zero is the ordinary north-up frame and is what every reference frame is taken at.
/// </param>
internal readonly record struct ShotRequest(
    string Map,
    string Path,
    int WidthPx,
    int HeightPx,
    float ViewM = 0f,
    float TurnDeg = 0f,
    Vector2? AtM = null,
    string[]? Ui = null,
    float UiScale = 0f,
    double Seconds = 0,
    IReadOnlyList<Vector2>? RulerPointsM = null,
    Vector2? PointerM = null,
    Vector2? PickedM = null,
    bool Validate = false);

/// <summary>What the frame turned out to be — the census a caller prints or asserts on.</summary>
internal readonly record struct ShotReport(
    string Map,
    string Path,
    int WidthPx,
    int HeightPx,
    Vector2 SpanM,
    Vector2 CentreM,
    int Triangles,
    int Sprites,
    int SpriteCapacity,
    long Tick,

    /// <summary>Both buffers' worth: the ground marks drawn under the bodies and everything drawn over them.</summary>
    int InterfaceQuads,
    long Crossings,

    /// <summary>The plan's own seed, which is what makes the picture reproducible from the caption alone.</summary>
    ulong Seed)
{
    /// <summary>
    /// What a metre is worth on this frame — the one figure a review quotes when it says a thing is
    /// too small to judge, and the figure the caption's scale bar is graduated against.
    /// </summary>
    public float PxPerM => SpanM.X > 0f ? WidthPx / SpanM.X : 0f;
}
