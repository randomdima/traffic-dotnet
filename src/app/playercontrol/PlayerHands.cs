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
/// <b>CTL-1b — the left button drags the town, and a press that did not travel picks a unit out.</b>
/// A press starts a gesture rather than selecting: a drag and a click begin identically, and a layer
/// that selected on the way down would select whatever the drag was started on top of and then select
/// it again. <b>Shift is read at the press</b>, because it is what decides which gesture this is — held,
/// the drag lays a box over the town instead of moving it, and a click that never travelled adds to the
/// selection instead of replacing it.
/// </para>
/// <para>
/// <b>CTL-9 — a finger is the left button, and two of them are the camera.</b> One touch drags, taps and
/// picks through the very code a mouse does; a second landing ends whatever the first had started and
/// hands the camera to <see cref="TouchGesture"/> until both are lifted.
/// </para>
/// </remarks>
internal sealed class PlayerHands
{
    /// <summary>What the left button is doing to the town while it is down (CTL-1b).</summary>
    enum Gesture
    {
        /// <summary>Nothing: the button is up, or what it started was taken over by a second finger.</summary>
        None,

        /// <summary>The town is being dragged under the pointer, which is what a plain press does.</summary>
        Panning,

        /// <summary>A box is being laid over the town, which is what a press with shift held does.</summary>
        Boxing,
    }

    readonly TouchGesture _touch = new();

    Vector2 _lastPointerPx;

    /// <summary>Where the left button went down on the town, while it is still down.</summary>
    Vector2 _fromPx;

    Gesture _gesture;

    /// <summary>Whether shift was held at the press, which is what the release does with what it caught.</summary>
    bool _alsoKeep;

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
    /// Two fingers on the glass, offered the camera before anything else is (CTL-9). <b>The frame two of
    /// them are down is a frame the one-finger gesture is not</b>: whatever the first finger had started
    /// is dropped, so a pinch cannot also be laying a box or picking a unit out on the way up.
    /// </summary>
    public void ReadTouches(AppWindow window, Camera2D camera, Vector2 uiPx, SimConfig config)
    {
        Span<Vector2> touchesPx = stackalloc Vector2[2];
        if (!_touch.Read(touchesPx[..window.Touches(touchesPx)], camera, uiPx, config)) return;

        _gesture = Gesture.None;
    }

    /// <summary>
    /// The camera, and the one rule that decides whether the keys move it at all: <b>the arrows pan
    /// whenever no unit is being driven</b> (CTL-5b). The drags and the wheel always do.
    /// </summary>
    /// <remarks>
    /// <b>The wheel zooms, and with control held it turns</b> (OBS-1c) — the desktop's answer to the
    /// twist between two fingers, and the same movement about the same point.
    /// </remarks>
    public void DriveCamera(
        AppWindow window, Camera2D camera, Vector2 uiPx, SimConfig config, float seconds, bool handsOn)
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
        var travelledPx = pointerPx - _lastPointerPx;
        _lastPointerPx = pointerPx;

        // CTL-1b: the left button drags the town under the pointer unless shift asked for a box instead.
        // The middle button keeps doing it whichever the left is doing, since it can be neither.
        if (_gesture == Gesture.Panning || window.IsMouseDown(MouseButton.Middle)) camera.PanByPixels(travelledPx);

        var scrolled = window.TakeScroll();
        if (window.IsKeyDown(Key.ControlLeft) || window.IsKeyDown(Key.ControlRight))
        {
            camera.Turn(scrolled * float.DegreesToRadians(config.View.CameraTurnPerNotchDeg), pointerPx, uiPx);
            return;
        }

        camera.Zoom(scrolled, pointerPx, uiPx);
    }

    /// <summary>
    /// A press on the town, once the panels have had their say. <b>The ruler is offered it before the
    /// selection layer</b>, so while the ruler is ticked a click measures rather than selecting or
    /// ordering. A left press starts a drag and picks nothing — that is <see cref="Pointer"/>'s, on the
    /// way back up.
    /// </summary>
    /// <param name="alsoKeep">
    /// Whether shift was held as the button went down, which is the whole of what says whether this is a
    /// pan or a box (CTL-1b) — and, if it turns out to have been a click, whether the unit under it joins
    /// the selection or replaces it.
    /// </param>
    public void Click(
        MouseButton button, Vector2 atPx, bool alsoKeep, Camera2D camera, Vector2 uiPx, TownWorld world,
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
                _gesture = alsoKeep ? Gesture.Boxing : Gesture.Panning;
                _alsoKeep = alsoKeep;
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
    /// The pointer between the presses: <b>what the gesture caught on the way up</b> (CTL-1b). A gesture
    /// that never left the spot it started on is a click and picks the one unit under it; anything longer
    /// has already moved the town or laid a box, and what it does here is what that box caught.
    /// </summary>
    /// <remarks>
    /// A release is read off the button's state rather than off an event, so a press and a release inside
    /// one frame still resolve — as a click, which is what a press and a release in the same spot is.
    /// </remarks>
    public void Pointer(AppWindow window, Camera2D camera, Vector2 uiPx, SimConfig config, TownWorld world)
    {
        if (_gesture == Gesture.None || window.IsMouseDown(MouseButton.Left)) return;

        var was = _gesture;
        _gesture = Gesture.None;
        var toPx = window.PointerPx;

        if (!IsDrag(_fromPx, toPx, config.View.PointerDragPx))
        {
            // Clicking nothing deselects, which is what makes the mark readable as an answer; with shift
            // it adds the unit under the pointer, or drops it if it was already picked out.
            var unit = world.Pick(camera.WorldAt(_fromPx, uiPx));
            if (_alsoKeep) world.SelectAlso(unit);
            else world.Select(unit);

            return;
        }

        // A pan is finished the moment it is let go of: the town moved under the hand as it went, and
        // there is nothing left for the release to be about.
        if (was != Gesture.Boxing) return;

        // The box is turned because the window is (OBS-1c). Its middle and its size are the gesture's own
        // pixels put into metres, and its lie on the ground is the window's axes in the town's.
        world.SelectIn(
            camera.WorldAt((_fromPx + toPx) * 0.5f, uiPx),
            Vector2.Abs(toPx - _fromPx) / camera.PixelsPerMetre, -camera.TurnRad, _alsoKeep);
    }

    /// <summary>Whether the pointer travelled far enough for the gesture to be a drag rather than a click.</summary>
    public static bool IsDrag(Vector2 fromPx, Vector2 toPx, float thresholdPx) =>
        (toPx - fromPx).LengthSquared() > thresholdPx * thresholdPx;

    /// <summary>The box as it stands this frame, for the interface to draw, or an empty one.</summary>
    public Rect MarqueePx(Vector2 pointerPx, float thresholdPx) =>
        _gesture == Gesture.Boxing && IsDrag(_fromPx, pointerPx, thresholdPx)
            ? Marquee.Between(_fromPx, pointerPx)
            : default;

    /// <summary>The town has changed under the gesture, so there is nothing left for it to have been about.</summary>
    public void TownChanged() => _gesture = Gesture.None;
}
