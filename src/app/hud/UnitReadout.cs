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
/// <b>What the interface has to say about the unit picked out, as rows of text</b> (OBS-2m) — one name
/// padded into a column and one figure per row, written into a buffer the caller owns.
/// </summary>
/// <remarks>
/// <para>
/// <b>One machine and two readers</b>, on the split the claims are read on
/// (<see cref="Bench.ScenarioWatch"/>): <see cref="UnitPanel"/> draws these rows in the bottom-left
/// corner, and the script that drives a unit by hand prints them as its own feedback (<c>--drive</c>).
/// A second set of figures about the same body would be a second opinion nobody could settle by looking
/// at the town.
/// </para>
/// <para>
/// <b>Every figure is read off the body and none is worked out here.</b> What the driver was told is on
/// the car (<see cref="CarFleet.Context"/>), what a walker was granted is on the walker, and the words
/// for both are their own slices' (<see cref="DrivingWords"/>, <see cref="WalkingWords"/>).
/// </para>
/// </remarks>
internal static class UnitReadout
{
    /// <summary>Where a row's figure starts, in characters, which lines the column up without measuring a glyph.</summary>
    public const int ValueColumn = 11;

    /// <summary>
    /// How many rows one unit can come to, which is what the whole read-out is written into. It bounds the
    /// buffer rather than describing the layout: a unit with nothing to say about its junction writes no
    /// junction row, and the rows are counted from what was written rather than predicted.
    /// </summary>
    /// <remarks>
    /// <b>The watches' notes are written last and are what a full read-out drops</b>, since they are the
    /// rows a build can add to without touching this file.
    /// </remarks>
    public const int MostRows = 16;

    public const int RoomPerRow = 96;

    /// <param name="watching">
    /// The run's own watches, or empty on a map that claims nothing. <b>What a watch has against this one
    /// body is written here</b> (OBS-2i): a claim is a statement about the town, and a finding that names a
    /// car is the one thing on that panel that is about a unit.
    /// </param>
    /// <returns>How many rows the selection came to, and <paramref name="ends"/> where each of them ends.</returns>
    public static int Lines(
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
        // rows a full read-out drops should be those.
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
    /// watch with nothing to say writes nothing, so a healthy unit's read-out is its own figures alone.
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
    /// What a walker is doing: the trip it is on, the line it is walking, and where on the network it
    /// stands.
    /// </summary>
    static void Walker(ref RowWriter rows, TownWorld world, int person)
    {
        var people = world.People;

        var line = rows.Next("doing");
        line.Add(WalkingWords.WalkName(people, person));
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

        // Where on the network the body stands, which is what says which of PER-25's two walks it is on:
        // a way of the pavement, or none of them and a straight back onto it.
        line = rows.Next("on");
        if (people.OnWay[person] == PersonFleet.NoWay) line.Add("no way of the network");
        else
        {
            line.Add("way ");
            line.Add(people.OnWay[person]);
            line.Add(" at ");
            line.Add(people.OnWayM[person], "F1");
            line.Add(" m");
        }

        rows.Keep(in line);
    }

    /// <summary>
    /// The rows as they are written: one buffer for all of them, a name padded into its column at the head
    /// of each, and the end of each row kept so a draw or a print can cut them apart again.
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
        /// past the end of its buffer is: the count is a ceiling on the read-out and not a claim about it.
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
