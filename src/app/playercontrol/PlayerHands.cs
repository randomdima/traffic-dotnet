using System.Numerics;
using Silk.NET.Input;
using TrafficSimulation.App.Camera;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.PlayerControl;

/// <summary>
/// <b>CTL-6: one slice owns picking, selection state, order translation and the drive keys.</b> What
/// it produces is a goal or a command, handed to the world through its own seam and read there every
/// tick — so the agent loop cannot tell a hand-driven agent from any other, and nothing here drives a
/// body behind its driver's back.
/// </summary>
/// <remarks>
/// <para>
/// <b>The input is offered to the layers in a fixed order</b>, and the order is the whole of what
/// makes each of them a tool rather than a mode: the panels first, since a click on a panel is not a
/// click on the town; then the ruler, which takes the mouse for as long as it is ticked; then
/// selection and orders.
/// </para>
/// <para>
/// <b>CTL-5b — holding a drive key takes the wheel and keeps it.</b> Releasing coasts; the wheel is
/// given up by an order, by the reset, by a change of selection or by a terminal state. Which is why
/// the arrows pan the camera only while nothing is being driven.
/// </para>
/// <para>
/// <b>CTL-1b — the left button picks out one unit or many, and which it was is known on the way up.</b>
/// A press starts a box rather than selecting: a drag and a click begin identically, and a layer that
/// selected on the way down would select whatever the box was started on top of and then select it
/// again. Shift is read at the release for the same reason — it is the gesture that is modified.
/// </para>
/// </remarks>
internal sealed class PlayerHands
{
    Vector2 _lastPointerPx;

    /// <summary>Where the left button went down on the town, while it is still down.</summary>
    Vector2 _fromPx;

    bool _dragging;

    /// <summary>
    /// One frame's worth of the keys, turned into the hand the world reads every tick inside it.
    /// </summary>
    public HandInput ReadKeys(AppWindow window, TownWorld world)
    {
        if (world.SelectedCount == 0) return HandInput.None;

        var throttle = 0f;
        if (window.IsKeyDown(Key.W)) throttle += 1f;
        if (window.IsKeyDown(Key.S)) throttle -= 1f;

        var steer = 0f;
        if (window.IsKeyDown(Key.A)) steer -= 1f;
        if (window.IsKeyDown(Key.D)) steer += 1f;

        var handbrake = window.IsKeyDown(Key.Space);
        var walk = new Vector2(steer, -throttle);

        // Taking the wheel is a press; keeping it is not. A frame in which nothing is held still
        // reports a hand, because letting go coasts rather than handing the unit back.
        var pressed = throttle != 0f || steer != 0f || handbrake;
        var held = pressed || world.HandsOn;
        return held ? new HandInput(true, throttle, steer, handbrake, walk) : HandInput.None;
    }

    /// <summary>
    /// The camera, and the one rule that decides whether it moves at all: <b>the arrows pan whenever
    /// no unit is being driven</b> (CTL-5b), and the wheel and the middle drag always do.
    /// </summary>
    public void DriveCamera(AppWindow window, Camera2D camera, Vector2 uiPx, float seconds, bool handsOn)
    {
        if (!handsOn)
        {
            var pan = Vector2.Zero;
            if (window.IsKeyDown(Key.Left)) pan.X -= 1f;
            if (window.IsKeyDown(Key.Right)) pan.X += 1f;
            if (window.IsKeyDown(Key.Up)) pan.Y -= 1f;
            if (window.IsKeyDown(Key.Down)) pan.Y += 1f;
            camera.Pan(pan, seconds);
        }

        var pointerPx = window.PointerPx;
        if (window.IsMouseDown(MouseButton.Middle)) camera.PanByPixels(pointerPx - _lastPointerPx);
        _lastPointerPx = pointerPx;

        camera.Zoom(window.TakeScroll(), pointerPx, uiPx);
    }

    /// <summary>
    /// A press on the town, once the panels have had their say. <b>The ruler is offered it before the
    /// selection layer</b>, so while the ruler is ticked a click measures rather than selecting or
    /// ordering. A left press starts a box and picks nothing — that is <see cref="Pointer"/>'s, on the
    /// way back up.
    /// </summary>
    public void Click(
        MouseButton button, Vector2 atPx, Camera2D camera, Vector2 uiPx, TownWorld world,
        DebugSwitches switches, Ruler ruler)
    {
        var pointM = camera.WorldAt(atPx, uiPx);

        if (switches.Ruler)
        {
            if (button == MouseButton.Right) ruler.Clear();
            else if (button == MouseButton.Left) ruler.Click(pointM);

            return;
        }

        switch (button)
        {
            case MouseButton.Left:
                _dragging = true;
                _fromPx = atPx;
                break;

            // CTL-2, CTL-3 and CTL-8: the order pins the goal the behaviour would otherwise have picked
            // itself, and everything below goal selection is untouched. <b>The pointer decides which goal
            // that is</b>, for both kinds — for a walker a building or a car is walked to and entered; for
            // a car it is another car to follow, a bay to park in, a place on the road to stand at, or a
            // place off it to park near and walk the rest of. Every selected unit takes the same order at
            // the same point (CTL-1b).
            case MouseButton.Right:
                foreach (var unit in world.Selected)
                {
                    if (unit.Kind == SelectionKind.Person) world.Order(unit.Index, pointM);
                    else world.OrderCar(unit.Index, pointM);
                }

                break;
        }
    }

    /// <summary>
    /// The pointer between the presses: <b>the box while the button is down, and what it caught on the
    /// way up</b> (CTL-1b). A gesture that never left the spot it started on is a click and picks the one
    /// unit under it; anything longer is a box and picks every unit inside it.
    /// </summary>
    /// <remarks>
    /// A release is read off the button's state rather than off an event, so a press and a release inside
    /// one frame still resolve — as a click, which is what a press and a release in the same spot is.
    /// </remarks>
    public void Pointer(AppWindow window, Camera2D camera, Vector2 uiPx, SimConfig config, TownWorld world)
    {
        if (!_dragging || window.IsMouseDown(MouseButton.Left)) return;

        _dragging = false;
        var toPx = window.PointerPx;
        var alsoKeep = window.IsKeyDown(Key.ShiftLeft) || window.IsKeyDown(Key.ShiftRight);

        if (!IsDrag(_fromPx, toPx, config.View.SelectionDragPx))
        {
            // Clicking nothing deselects, which is what makes the mark readable as an answer; with shift
            // it adds the unit under the pointer, or drops it if it was already picked out.
            var unit = world.Pick(camera.WorldAt(_fromPx, uiPx));
            if (alsoKeep) world.SelectAlso(unit);
            else world.Select(unit);

            return;
        }

        world.SelectIn(camera.WorldAt(_fromPx, uiPx), camera.WorldAt(toPx, uiPx), alsoKeep);
    }

    /// <summary>Whether the pointer travelled far enough for the gesture to be a box rather than a click.</summary>
    public static bool IsDrag(Vector2 fromPx, Vector2 toPx, float thresholdPx) =>
        (toPx - fromPx).LengthSquared() > thresholdPx * thresholdPx;

    /// <summary>The box as it stands this frame, for the interface to draw, or an empty one.</summary>
    public Rect MarqueePx(Vector2 pointerPx, float thresholdPx) =>
        _dragging && IsDrag(_fromPx, pointerPx, thresholdPx) ? Marquee.Between(_fromPx, pointerPx) : default;

    /// <summary>The town has changed under the gesture, so there is nothing left for it to have been about.</summary>
    public void TownChanged() => _dragging = false;
}
