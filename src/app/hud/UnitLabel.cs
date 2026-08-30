using System.Numerics;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.App.Camera;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Bench;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Hud;

/// <summary>
/// <b>CTL-1: what the selected unit is doing, read where the unit is.</b> A small label standing beside
/// the box the brackets wrap (<see cref="SelectionMark"/>), carrying the unit's behaviour state and
/// whatever the run's own watches have to say about that one body.
/// </summary>
/// <remarks>
/// <para>
/// <b>It stands at the unit and not in a corner.</b> A read-out in the corner of the screen is a second
/// place to look: the eye is on the car it just picked out, and a line about that car five hundred pixels
/// away is a line read once and then ignored. It follows the unit, so what is being described is never in
/// doubt however many are on screen.
/// </para>
/// <para>
/// <b>A note is a figure and not a verdict.</b> What a claim is and whether the town is keeping it is the
/// claims section's (<see cref="StatusPanel"/>); what is here is the one body's own state, so nothing on
/// this label is coloured as a failure.
/// </para>
/// <para>
/// <b>A group is counted rather than described</b> (CTL-1b). One label has room for one unit's state, and
/// thirty labels each with a line of prose is the town covered in text — so what a selection of many says
/// is how many of each kind it holds, standing at the unit that was picked out first.
/// </para>
/// <para>
/// <b>Nothing off the picture is labelled.</b> A unit inside a building or a car is not drawn (PHY-7) and
/// wears no brackets either, and a unit the camera has left behind would put its label against a window
/// edge it is nowhere near. Both draw nothing rather than lie about where the unit is.
/// </para>
/// </remarks>
internal static class UnitLabel
{
    /// <summary>How far off the unit's own box the label stands, so the brackets are not written over.</summary>
    const float StandOffPx = 12f;

    /// <summary>How many notes one label has room for, which bounds the run over the watches without a list.</summary>
    const int MostNotes = 4;

    const float RowPitchPx = Theme.SmallTextPx + 4f;

    const float TitleRowPx = Theme.TextPx + 14f;

    /// <returns>The box the label was laid in, or an empty one where there was nothing to label.</returns>
    public static Rect Draw(
        ref ScreenDraw draw, Vector2 uiPx, TownWorld world, SimConfig config, Camera2D camera,
        ReadOnlySpan<ScenarioWatch> watching)
    {
        if (world.SelectedCount == 0) return default;

        var selection = world.Lead;
        if (!SelectionMark.BoxOf(world, config, selection, out var centreM, out var sizeM, out _)) return default;

        var atPx = camera.ScreenAt(centreM, uiPx);
        if (atPx.X < 0f || atPx.Y < 0f || atPx.X > uiPx.X || atPx.Y > uiPx.Y) return default;

        Span<char> text = stackalloc char[128];
        var head = new TextBuffer(text);
        State(ref head, world, selection);

        // The watches say nothing about a set: a note names one body, and the label of a group is already
        // saying that it is not talking about one.
        Span<char> noteText = stackalloc char[MostNotes * 96];
        Span<int> noteEnds = stackalloc int[MostNotes];
        var notes = world.SelectedCount == 1 ? Notes(noteText, noteEnds, watching, selection) : 0;

        var widthPx = GlyphSheet.WidthPx(head.Length, Theme.TextPx);
        for (var note = 0; note < notes; note++)
        {
            var lengthPx = GlyphSheet.WidthPx(noteEnds[note] - (note > 0 ? noteEnds[note - 1] : 0), Theme.SmallTextPx);
            widthPx = MathF.Max(widthPx, lengthPx);
        }

        var box = Placed(atPx, sizeM, camera.PixelsPerMetre, uiPx, widthPx + (Theme.PaddingPx * 2f),
            TitleRowPx + (notes > 0 ? (notes * RowPitchPx) + Theme.GapPx : 0f));
        Theme.Frame(ref draw, box);
        draw.Text(box.AtPx + new Vector2(Theme.PaddingPx, 7f), head.Written, Theme.TextPx, Theme.Text);

        for (var note = 0; note < notes; note++)
        {
            var from = note > 0 ? noteEnds[note - 1] : 0;
            draw.Text(
                box.AtPx + new Vector2(Theme.PaddingPx, TitleRowPx + (note * RowPitchPx)),
                noteText[from..noteEnds[note]], Theme.SmallTextPx, Theme.Text);
        }

        return box;
    }

    /// <summary>
    /// Where the label goes: clear of the unit's own box on the right, level with its middle, and never
    /// off the window. <b>The stand-off is taken from the box's longest side</b> rather than its width, so
    /// a car broadside on and a car end on are both cleared by the same label.
    /// </summary>
    static Rect Placed(Vector2 atPx, Vector2 sizeM, float pixelsPerMetre, Vector2 uiPx, float widthPx, float heightPx)
    {
        var clearPx = (MathF.Max(sizeM.X, sizeM.Y) * 0.5f * pixelsPerMetre) + StandOffPx;
        var wantedPx = atPx + new Vector2(clearPx, -heightPx * 0.5f);

        // Flipped to the other side rather than clamped when there is no room on this one: a label pinned
        // to the right edge would sit over the unit it is about.
        if (wantedPx.X + widthPx > uiPx.X - Theme.MarginPx) wantedPx.X = atPx.X - clearPx - widthPx;

        return new Rect(
            new Vector2(
                Math.Clamp(wantedPx.X, Theme.MarginPx, MathF.Max(Theme.MarginPx, uiPx.X - Theme.MarginPx - widthPx)),
                Math.Clamp(wantedPx.Y, Theme.MarginPx, MathF.Max(Theme.MarginPx, uiPx.Y - Theme.MarginPx - heightPx))),
            new Vector2(widthPx, heightPx));
    }

    /// <summary>What the unit is doing, in the one line the label is always at least as tall as.</summary>
    static void State(ref TextBuffer line, TownWorld world, Selection selection)
    {
        if (world.SelectedCount > 1)
        {
            Group(ref line, world);
        }
        else if (selection.Kind == SelectionKind.Person)
        {
            line.Add("walker ");
            line.Add(selection.Index);
            line.Add("   ");
            line.Add(world.People.Wounded[selection.Index] ? "wounded"
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
            if (world.IsUnderOrders(selection.Index)) line.Add(OrderWords(world.OrderOf(selection.Index)));
        }

        if (world.HandsOn) line.Add(world.SelectedCount > 1 ? "   hands at the wheel" : "   hand at the wheel");
    }

    /// <summary>
    /// Everything the run's watches have against this one body, each on its own row. <b>A watch with
    /// nothing to say writes nothing</b>, so a healthy unit's label is the one line of its state.
    /// </summary>
    /// <returns>How many notes were written, and <paramref name="ends"/> where each of them ends.</returns>
    static int Notes(
        scoped Span<char> into, scoped Span<int> ends, ReadOnlySpan<ScenarioWatch> watching, Selection selection)
    {
        var written = 0;
        var notes = 0;
        foreach (var watch in watching)
        {
            if (notes == MostNotes) break;

            var line = new TextBuffer(into[written..]);
            if (!watch.Notes(selection.Kind, selection.Index, ref line)) continue;

            written += line.Length;
            ends[notes] = written;
            notes++;
        }

        return notes;
    }

    /// <summary>
    /// <b>What a car under the player's orders is holding</b> (CTL-8), which is the same question CTL-1
    /// asks of its behaviour state — an ordered car's state is the order.
    /// </summary>
    /// <remarks>
    /// An order that is finished still says something, and what it says is CTL-4's: the car is the
    /// player's and is waiting to be told what to do next rather than deciding for itself.
    /// </remarks>
    static string OrderWords(PlayerOrder order) => order switch
    {
        PlayerOrder.DriveThere => "   ordered to a place",
        PlayerOrder.ParkThere => "   ordered to park",
        PlayerOrder.ParkAndWalkThere => "   ordered to park and walk on",
        PlayerOrder.FollowThatCar => "   ordered to follow",
        _ => "   awaiting orders",
    };

    /// <summary>How many units are picked out, and of what: the whole of what one line can say about many.</summary>
    static void Group(ref TextBuffer line, TownWorld world)
    {
        var cars = world.SelectedCountOf(SelectionKind.Car);
        var walkers = world.SelectedCountOf(SelectionKind.Person);

        line.Add(world.SelectedCount);
        line.Add(" units");

        if (cars > 0)
        {
            line.Add("   ");
            line.Add(cars);
            line.Add(cars == 1 ? " car" : " cars");
        }

        if (walkers <= 0) return;

        line.Add("   ");
        line.Add(walkers);
        line.Add(walkers == 1 ? " walker" : " walkers");
    }
}
