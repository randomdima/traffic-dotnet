using System.Numerics;
using Silk.NET.Input;
using TrafficSimulation.App.Camera;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Hud;
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
/// </remarks>
internal sealed class PlayerHands
{
    Vector2 _lastPointerPx;

    /// <summary>
    /// One frame's worth of the keys, turned into the hand the world reads every tick inside it.
    /// </summary>
    public HandInput ReadKeys(AppWindow window, TownWorld world)
    {
        if (!world.Selected.Any) return HandInput.None;

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
    /// A click on the town, once the panels have had their say. <b>The ruler is offered it before the
    /// selection layer</b>, so while the ruler is ticked a click measures rather than selecting or
    /// ordering.
    /// </summary>
    public static void Click(
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
                // Clicking nothing deselects, which is what makes the highlight readable as an answer.
                world.Selected = world.Pick(pointM);
                break;

            // CTL-2 and CTL-3: the order pins the goal the behaviour would otherwise have picked itself,
            // and everything below goal selection is untouched. For a walker the pointer decides which
            // goal that is — a building or a car is walked to and entered; for a car it is the bay its
            // driver would have chosen, so an empty car takes no order (CAR-1).
            case MouseButton.Right when world.Selected.Kind == SelectionKind.Person:
                world.Order(world.Selected.Index, pointM);
                break;

            case MouseButton.Right when world.Selected.Kind == SelectionKind.Car:
                world.OrderCar(world.Selected.Index, pointM);
                break;
        }
    }
}
