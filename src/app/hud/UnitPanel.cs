using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Car.Maneuvers;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Bench;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Hud;

/// <summary>
/// <b>OBS-2m — the bottom-left corner is the selection, and it is the only place it is written.</b> A
/// title naming what is picked out over a body of rows: what the unit is, what it is doing, how fast, what
/// is claimed in front of it, how much of its trip is left, and what the run's own watches have against
/// it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing about the selection is written on the town</b> (CTL-1). What stands at the unit is the mark
/// and only the mark — the brackets and the path (<see cref="SelectionMark"/>, <see cref="SelectionPath"/>)
/// — because a shape says <em>which</em> one without covering the road it is about to drive down. Words
/// take room, and the room they take is the town.
/// </para>
/// <para>
/// <b>A group is counted and not described</b> (CTL-1b): there is no such thing as the speed of thirty
/// cars, so a set says how many of each kind it holds and nothing else.
/// </para>
/// <para>
/// <b>It does not need the unit on the picture.</b> Somebody indoors or riding in a car is not drawn and
/// wears no brackets (PHY-7), and a unit the camera has been panned off has nothing at it either — this
/// still says what it is doing and where.
/// </para>
/// <para>
/// <b>Every figure is read off the body and none is worked out here.</b> What the driver was told is on the
/// car (<see cref="CarFleet.Context"/>), what a walker was granted is on the walker, and the words for both
/// are their own slices' (<see cref="DrivingWords"/>, <see cref="WalkingWords"/>). A panel doing its own
/// arithmetic is a second opinion drawn beside the first.
/// </para>
/// </remarks>
internal sealed class UnitPanel
{
    /// <summary>Where a row's figure starts, in characters, which lines the column up without measuring a glyph.</summary>
    const int ValueColumn = 11;

    /// <summary>
    /// The longest line the body is budgeted for, and the longest the title is. <b>Both are budgets rather
    /// than measurements</b>, so the panel does not move when a speed gains a digit or a car enters a
    /// manoeuvre with a longer name — what overruns is fitted to the panel instead.
    /// </summary>
    const int WidestLine = ValueColumn + 32;

    const int TitleLine = 20;

    /// <summary>
    /// How many rows one unit can come to, which is what the whole panel is written into. It bounds the
    /// buffer rather than describing the layout: a unit with nothing to say about its junction writes no
    /// junction row, and the rows are counted from what was written rather than predicted.
    /// </summary>
    /// <remarks>
    /// <b>The watches' notes are written last and are what a full panel drops</b>, since they are the rows
    /// a build can add to without touching this file.
    /// </remarks>
    const int MostRows = 16;

    const int RoomPerRow = 96;

    const float RowPitchPx = Theme.SmallTextPx + 4f;

    const float TitleRowPx = Theme.TextPx + 10f;

    Rect _title;

    /// <summary>Whether the body is showing. <b>Open by default</b>: a panel that only appears when something is picked out has already been asked for.</summary>
    public bool Open { get; private set; } = true;

    /// <summary>The whole panel, or an empty box on a frame that drew none — a click on it is not a click on the town behind it.</summary>
    public Rect Box { get; private set; }

    /// <summary>How many rows the last draw wrote, which is what a test reads the layout off.</summary>
    public int Rows { get; private set; }

    /// <summary>A click on the panel: the title opens and shuts the body, and anywhere else on it is taken and dropped.</summary>
    public bool Click(Vector2 atPx)
    {
        if (!Box.Contains(atPx)) return false;

        if (_title.Contains(atPx)) Open = !Open;
        return true;
    }

    /// <param name="watching">
    /// The run's own watches, or empty on a map that claims nothing. <b>What a watch has against this one
    /// body is written here</b> (OBS-2i): a claim is a statement about the town, and a finding that names a
    /// car is the one thing on that panel that is about a unit.
    /// </param>
    public void Draw(
        ref ScreenDraw draw, Vector2 uiPx, Vector2 pointerPx, TownWorld world,
        ReadOnlySpan<ScenarioWatch> watching)
    {
        if (world.SelectedCount == 0)
        {
            // Nothing was laid, so nothing may be pressed and nothing may swallow a click on the town.
            Box = default;
            _title = default;
            Rows = 0;
            return;
        }

        var unit = world.Lead;
        Span<char> text = stackalloc char[MostRows * RoomPerRow];
        Span<int> ends = stackalloc int[MostRows];

        // Written before the panel is laid out rather than counted twice: what a row has to say is what
        // decides whether it is there at all, and a predicted count is a row drawn through the panel's own
        // bottom edge the day one of them learns a condition.
        Rows = Open ? Lines(world, unit, watching, text, ends) : 0;

        var widthPx = Open
            ? GlyphSheet.WidthPx(WidestLine, Theme.SmallTextPx) + Theme.PaddingPx * 2f
            : GlyphSheet.WidthPx(TitleLine, Theme.TextPx) + Theme.PaddingPx * 1.2f;
        var heightPx = StatusPanel.HeightFor(Rows);
        Box = new Rect(
            new Vector2(Theme.MarginPx, MathF.Max(Theme.MarginPx, uiPx.Y - Theme.MarginPx - heightPx)),
            new Vector2(widthPx, heightPx));
        Theme.Frame(ref draw, Box);

        Span<char> head = stackalloc char[TitleLine * 2];
        Title(ref draw, pointerPx, head, world, unit);
        if (Rows == 0) return;

        Theme.Separator(
            ref draw, Box.AtPx + new Vector2(Theme.PaddingPx * 0.6f, Theme.GapPx + TitleRowPx + Theme.GapPx),
            Box.SizePx.X - Theme.PaddingPx * 1.2f);

        for (var row = 0; row < Rows; row++)
        {
            var from = row > 0 ? ends[row - 1] : 0;
            draw.TextFitted(
                Box.AtPx + new Vector2(Theme.PaddingPx * 0.6f, StatusPanel.BodyTopPx + row * RowPitchPx),
                text[from..ends[row]], Theme.SmallTextPx, Theme.Text, Box.SizePx.X - Theme.PaddingPx);
        }
    }

    /// <summary>
    /// What this is talking about, on the one line that is there whether the body is open or shut: the
    /// unit, or how many of them there are.
    /// </summary>
    void Title(ref ScreenDraw draw, Vector2 pointerPx, scoped Span<char> into, TownWorld world, Selection unit)
    {
        _title = new Rect(
            Box.AtPx + new Vector2(Theme.EdgePx, Theme.GapPx),
            new Vector2(Box.SizePx.X - Theme.EdgePx * 2f, TitleRowPx));
        if (_title.Contains(pointerPx)) draw.RoundedRect(_title.AtPx, _title.SizePx, Theme.RowRadiusPx, Theme.RowHover);

        var line = new TextBuffer(into);
        line.Add(Open ? "- " : "+ ");
        if (world.SelectedCount > 1)
        {
            line.Add(world.SelectedCount);
            line.Add(" units");
        }
        else
        {
            line.Add(unit.Kind == SelectionKind.Car ? "car " : "walker ");
            line.Add(unit.Index);
        }

        draw.TextFitted(
            _title.AtPx + new Vector2(Theme.PaddingPx * 0.6f, (TitleRowPx - Theme.TextPx) * 0.5f), line.Written,
            Theme.TextPx, Theme.Heading, _title.SizePx.X - Theme.PaddingPx);
    }

    /// <returns>How many rows the selection came to, and <paramref name="ends"/> where each of them ends.</returns>
    static int Lines(
        TownWorld world, Selection unit, ReadOnlySpan<ScenarioWatch> watching, scoped Span<char> into,
        scoped Span<int> ends)
    {
        var rows = new RowWriter(into, ends);
        var group = world.SelectedCount > 1;
        if (group) Group(ref rows, world);
        else if (unit.Kind == SelectionKind.Car) Car(ref rows, world, unit.Index);
        else Walker(ref rows, world, unit.Index);

        // CTL-5: whose hands the units are in is a fact about the selection rather than about any one body,
        // so it is the one row a group and a single unit both write.
        if (world.HandsOn)
        {
            var held = rows.Next("wheel");
            held.Add(group ? "in the player's hands" : "in the player's hand");
            rows.Keep(in held);
        }

        // Last, because a watch is the one thing here a build adds to without touching this file, and the
        // rows a full panel drops should be those.
        if (!group) Notes(ref rows, watching, unit);

        return rows.Count;
    }

    /// <summary>
    /// <b>How many units are picked out and of what</b> (CTL-1b), which is the whole of what a set can be
    /// asked. Thirty cars have no speed, no destination and no manoeuvre between them.
    /// </summary>
    static void Group(ref RowWriter rows, TownWorld world)
    {
        var cars = rows.Next("cars");
        cars.Add(world.SelectedCountOf(SelectionKind.Car));
        rows.Keep(in cars);

        var walkers = rows.Next("walkers");
        walkers.Add(world.SelectedCountOf(SelectionKind.Person));
        rows.Keep(in walkers);
    }

    /// <summary>
    /// <b>Everything the run's watches have against this one body</b> (OBS-2i), each on its own row. A
    /// watch with nothing to say writes nothing, so a healthy unit's panel is its own figures alone.
    /// </summary>
    static void Notes(ref RowWriter rows, ReadOnlySpan<ScenarioWatch> watching, Selection unit)
    {
        foreach (var watch in watching)
        {
            if (rows.Full) return;

            var line = rows.Next("noted");
            if (watch.Notes(unit.Kind, unit.Index, ref line)) rows.Keep(in line);
        }
    }

    /// <summary>
    /// What a car is worth and what it is up against: its own figures, then the driver's reading of the
    /// road, then how much of the trip is left.
    /// </summary>
    static void Car(ref RowWriter rows, TownWorld world, int car)
    {
        var cars = world.Cars;

        var line = rows.Next("make");
        line.Add(CarCatalog.Shared.Variants[cars.Variant[car]].Id);
        line.Add(", ");
        line.Add(cars.MassKg[car], "F0");
        line.Add(" kg");
        if (cars.BlueLight[car]) line.Add(", on a call");
        else if (cars.AtWork[car]) line.Add(", at work");
        rows.Keep(in line);

        // What it is doing over what the profile asked for, which is the pair worth watching: the two apart
        // is a car being held, and the two together is a car with the road to itself. A car nobody is
        // driving planned nothing, so it is quoted on its own rather than against a nought.
        line = rows.Next("speed");
        line.Add(cars.VelocityMps[car].Length() * 3.6f, "F0");
        if (cars.Driven[car] && cars.PlannedMps[car] > 0f && float.IsFinite(cars.PlannedMps[car]))
        {
            line.Add(" of ");
            line.Add(cars.PlannedMps[car] * 3.6f, "F0");
        }

        line.Add(" km/h");
        rows.Keep(in line);

        line = rows.Next("doing");
        line.Add(DrivingWords.CarName(cars, car));
        if (cars.Doing[car] != Maneuver.None)
        {
            line.Add(", ");
            line.Add(cars.InManeuverS[car], "F1");
            line.Add(" s");
        }

        rows.Keep(in line);

        // CTL-8: an ordered car's state is the order, so it is written beside what the catalogue calls the
        // car rather than instead of it — a finished order still says the car is the player's and waiting.
        if (world.IsUnderOrders(car))
        {
            line = rows.Next("ordered");
            line.Add(OrderWords(world.OrderOf(car)));
            rows.Keep(in line);
        }

        // What was claimed in front of the nose, and how much room that left to stop in. Two readings and
        // not one: a queue eight metres off with room to stop is an ordinary follow, and the same queue with
        // no room is the car that is about to be the reason somebody looked at this panel.
        var context = cars.Context[car];
        line = rows.Next("ahead");
        if (float.IsFinite(context.HeadwayM))
        {
            line.Add(context.HeadwayM, "F1");
            line.Add(" m, ");
            line.Add(DrivingWords.AheadName(context.Ahead));
        }
        else
        {
            line.Add("clear");
        }

        rows.Keep(in line);

        line = rows.Next("room");
        if (float.IsFinite(cars.AuthorityM[car]))
        {
            line.Add(cars.AuthorityM[car], "F1");
            line.Add(" m, cut by ");
            line.Add(DrivingWords.AheadName(cars.GrantCutBy[car]));
        }
        else
        {
            line.Add("the road to itself");
        }

        rows.Keep(in line);

        line = rows.Next("off line");
        line.Add(cars.OffLineM[car], "F2");
        line.Add(" m");
        rows.Keep(in line);

        if (float.IsFinite(cars.ToTheBoxM[car]))
        {
            line = rows.Next("junction");
            line.Add(cars.ToTheBoxM[car], "F1");
            line.Add(cars.TurningAtTheBox[car] ? " m, turning, " : " m, straight on, ");
            line.Add(cars.BoxIsOurs[car] ? "ours" : "not ours");
            rows.Keep(in line);
        }

        line = rows.Next("route");
        line.Add(cars.RouteCount[car] - cars.RouteTaken[car]);
        line.Add(" lanes left");
        if (cars.RouteRunsOut[car]) line.Add(", runs out");
        rows.Keep(in line);

        line = rows.Next("to go");
        if (cars.HasDestination[car])
        {
            line.Add((cars.DestinationM[car] - cars.PositionM[car]).Length(), "F0");
            line.Add(" m");
        }
        else
        {
            line.Add("nowhere in particular");
        }

        rows.Keep(in line);
    }

    /// <summary>
    /// <b>What a car under the player's orders is holding</b> (CTL-8), which is the same question the
    /// catalogue answers of its behaviour. An order that is finished still says something, and what it says
    /// is CTL-4's: the car is the player's and is waiting to be told what to do next.
    /// </summary>
    static string OrderWords(PlayerOrder order) => order switch
    {
        PlayerOrder.DriveThere => "to a place",
        PlayerOrder.ParkThere => "to park",
        PlayerOrder.ParkAndWalkThere => "to park and walk on",
        PlayerOrder.FollowThatCar => "to follow",
        _ => "awaiting orders",
    };

    /// <summary>
    /// What a walker is doing and what is in its way: the trip it is on, the line it is holding, and the
    /// ground it has been granted to walk into.
    /// </summary>
    static void Walker(ref RowWriter rows, TownWorld world, int person)
    {
        var people = world.People;

        var line = rows.Next("doing");
        line.Add(WalkingWords.WalkName(people, person, world.StopsInM(person)));
        if (people.Manual[person]) line.Add(", under orders");
        rows.Keep(in line);

        line = rows.Next("speed");
        line.Add(people.VelocityMps[person].Length(), "F1");
        line.Add(" m/s");
        rows.Keep(in line);

        line = rows.Next("trip");
        line.Add(WalkingWords.StageName(people.Stage[person]));
        if (people.TimerS[person] > 0f)
        {
            line.Add(", ");
            line.Add(people.TimerS[person], "F1");
            line.Add(" s left");
        }

        rows.Keep(in line);

        if (people.Inside[person].Any)
        {
            var inside = people.Inside[person];
            line = rows.Next("inside");
            line.Add(inside.Kind == ContainerKind.Car ? "car " : "building ");
            line.Add(inside.Index);
            rows.Keep(in line);
        }

        // Off the goal and only while there is a walk on: a body between trips carries the goal of the one
        // it finished, and a distance to somewhere it has already been is worse than no row.
        line = rows.Next("to go");
        if (people.Walking[person])
        {
            line.Add((people.GoalM[person] - people.PositionM[person]).Length(), "F0");
            line.Add(" m");
        }
        else
        {
            line.Add("nowhere in particular");
        }

        rows.Keep(in line);

        line = rows.Next("line");
        line.Add(people.WalkedCount[person] - people.WalkedTaken[person]);
        line.Add(" points left");
        if (people.WalkedRunsOut[person]) line.Add(", runs out");
        rows.Keep(in line);

        line = rows.Next("room");
        if (float.IsFinite(people.AuthorityM[person]))
        {
            line.Add(people.AuthorityM[person], "F1");
            line.Add(" m");
        }
        else
        {
            line.Add("the pavement to itself");
        }

        rows.Keep(in line);

        line = rows.Next("held by");
        if (people.HeldBy[person] == PersonFleet.NoBody) line.Add("nothing");
        else
        {
            // The pavement holds whatever is standing on it (TER-4c.2), so the number is a number in one of
            // two fleets and the roster is what says which.
            line.Add(people.HeldByOf[person] == LaneRoster.Walking ? "walker " : "car ");
            line.Add(people.HeldBy[person]);
        }

        rows.Keep(in line);

        if (people.WaitingToCrossS[person] > 0f)
        {
            line = rows.Next("waiting");
            line.Add(people.WaitingToCrossS[person], "F1");
            line.Add(" s to cross");
            rows.Keep(in line);
        }
    }

    /// <summary>
    /// The rows as they are written: one buffer for all of them, a name padded into its column at the head
    /// of each, and the end of each row kept so the draw can cut them apart again.
    /// </summary>
    ref struct RowWriter(Span<char> into, Span<int> ends)
    {
        readonly Span<char> _into = into;
        readonly Span<int> _ends = ends;
        int _written;

        public int Count { get; private set; }

        /// <summary>Whether there is room for another row, which is what bounds a run over the watches.</summary>
        public readonly bool Full => Count == _ends.Length;

        /// <summary>A row under construction, opened with its name. It is kept only once <see cref="Keep"/> has it.</summary>
        public TextBuffer Next(string name)
        {
            var line = new TextBuffer(_into[_written..]);
            line.Add(name);
            line.PadTo(ValueColumn);
            return line;
        }

        /// <summary>
        /// That row, finished. <b>A row past the budget is dropped rather than thrown</b>, exactly as a line
        /// past the end of its buffer is: the count is a ceiling on the panel and not a claim about it.
        /// </summary>
        public void Keep(in TextBuffer line)
        {
            if (Full) return;

            _written += line.Length;
            _ends[Count] = _written;
            Count++;
        }
    }
}
