using System.Numerics;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.App.Screen;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Hud;

/// <summary>
/// The furniture a run carries: the town and both its seeds, what the selected unit is doing, and the
/// button that opens the settings. What the run <em>costs</em> is the frame read-out's, which is a
/// switch rather than furniture (<see cref="FrameReadout"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>OBS-2 — both seeds stay visible during the run</b>, so a town somebody is watching can be
/// opened again. They are in the corner rather than behind the settings panel for that reason: a
/// figure nobody can see without opening a panel is a figure nobody quotes.
/// </para>
/// <para>
/// <b>The panels keep to the corners.</b> The middle of the view is the town's, which is a claim the
/// reference frame makes and the cheapest one to break by putting a read-out where the eye is.
/// </para>
/// </remarks>
internal sealed class Hud
{
    const float MarginPx = 12f;

    /// <summary>The gear in the top-right corner, which is the other way into the settings panel.</summary>
    public Rect Gear { get; private set; }

    public void Draw(
        ref ScreenDraw draw, Vector2 uiPx, Vector2 pointerPx, string mapName, ulong worldSeed, ulong agentSeed,
        TownWorld world, RunState run, long tick)
    {
        Span<char> text = stackalloc char[96];

        var line = new TextBuffer(text);
        line.Add(mapName);
        line.Add("   world ");
        line.Add(worldSeed);
        line.Add("   agent ");
        line.Add(agentSeed);
        var widthPx = GlyphSheet.WidthPx(line.Length, Theme.TextPx);
        var box = new Rect(new Vector2(MarginPx), new Vector2(widthPx + Theme.PaddingPx * 2f, Theme.TextPx + 14f));
        Theme.Frame(ref draw, box);
        draw.Text(box.AtPx + new Vector2(Theme.PaddingPx, 7f), line.Written, Theme.TextPx, Theme.Text);

        var second = new TextBuffer(text);
        second.Add("tick ");
        second.Add(tick);
        second.Add("   pace ");
        second.Add(run.TimeScale, "F1");
        second.Add('x');
        if (run.Frozen) second.Add("  frozen");
        if (run.AgentsHeld) second.Add("  agents held");

        var secondWidthPx = GlyphSheet.WidthPx(second.Length, Theme.SmallTextPx);
        var secondBox = new Rect(
            new Vector2(MarginPx, box.Bottom + 4f),
            new Vector2(secondWidthPx + Theme.PaddingPx * 2f, Theme.SmallTextPx + 12f));
        Theme.Frame(ref draw, secondBox);
        draw.Text(secondBox.AtPx + new Vector2(Theme.PaddingPx, 6f), second.Written, Theme.SmallTextPx, Theme.Dim);

        Gear = new Rect(new Vector2(uiPx.X - MarginPx - Theme.GearPx, MarginPx), new Vector2(Theme.GearPx));
        draw.Rect(Gear.AtPx, Gear.SizePx, Gear.Contains(pointerPx) ? Theme.RowHover : Theme.Panel);
        draw.Outline(Gear.AtPx, Gear.SizePx, Theme.EdgePx, Theme.PanelEdge);
        draw.Text(Gear.AtPx + new Vector2(9f, 6f), "=", Theme.HeadingPx, Theme.Heading);

        Selected(ref draw, uiPx, world);
    }

    /// <summary>
    /// <b>CTL-1: the selected unit's behaviour state shows in the interface.</b> One line, in the
    /// bottom-left, out of the way of the scale legend in the bottom-right.
    /// </summary>
    static void Selected(ref ScreenDraw draw, Vector2 uiPx, TownWorld world)
    {
        if (!world.Selected.Any) return;

        Span<char> text = stackalloc char[96];
        var line = new TextBuffer(text);
        var selection = world.Selected;
        if (selection.Kind == SelectionKind.Person)
        {
            line.Add("walker ");
            line.Add(selection.Index);
            line.Add("   ");
            line.Add(world.People.Dead[selection.Index] ? "dead"
                : world.People.Walking[selection.Index] ? "walking" : "standing");
            if (world.People.Manual[selection.Index]) line.Add("   under orders");
        }
        else
        {
            line.Add("car ");
            line.Add(selection.Index);
            line.Add("   ");
            line.Add(DrivingWords.CarName(world.Cars, selection.Index));
            line.Add("   ");
            line.Add(world.Cars.VelocityMps[selection.Index].Length() * 3.6f, "F0");
            line.Add(" km/h");
        }

        if (world.HandsOn) line.Add("   hand at the wheel");

        var widthPx = GlyphSheet.WidthPx(line.Length, Theme.TextPx);
        var box = new Rect(
            new Vector2(MarginPx, uiPx.Y - MarginPx - Theme.TextPx - 14f),
            new Vector2(widthPx + Theme.PaddingPx * 2f, Theme.TextPx + 14f));
        Theme.Frame(ref draw, box);
        draw.Text(box.AtPx + new Vector2(Theme.PaddingPx, 7f), line.Written, Theme.TextPx, Theme.Text);
    }
}
