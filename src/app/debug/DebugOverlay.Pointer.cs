using System.Numerics;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.App.Screen;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// <b>The inspector: what the pointer is over, and the one thing the reader has pinned</b> (OBS-2t) — the part
/// of this overlay that answers a question about one thing rather than drawing everything there is at once.
/// </summary>
/// <remarks>
/// <para>
/// <b>One thing at a time, found in the layers that are on.</b> A body before a line and a thin line before a
/// broad one (<see cref="Under"/>), because the thing drawn over the others is the thing a reader is pointing
/// at. Where it is found it is drawn picked out, and what it is and what holds it is written on a card beside
/// the pointer — <b>the words are here and nowhere on the town</b>, so a layer is lines and nothing else until
/// somebody asks.
/// </para>
/// <para>
/// <b>A click pins it</b> (<see cref="DebugPick"/>): its card is docked under the corner buttons and it stays
/// picked out, while the pointer goes on asking about everything else.
/// </para>
/// <para>
/// <b>Laid every frame and never into the town's cache.</b> What it draws follows the pointer and the tick, and
/// the cache behind the town's own layers is re-laid only when the view or a switch moves.
/// </para>
/// <para>
/// <b>Every reading is off the producer</b>, like the rest of this slice: what a cell holds is the index's own
/// answer, a way and what holds it are the town's (<see cref="TownWorld.LineOfWay"/>,
/// <see cref="LaneOccupancy.CopyTo"/>), the ribbon is the band the merge was given (<see cref="ArcRibbon"/>),
/// and the boundary is the merge's own output (<see cref="LaneShell"/>).
/// </para>
/// </remarks>
internal sealed partial class DebugOverlay
{
    /// <summary>
    /// What a picked-out line is drawn at: <b>three times the width everything else is drawn at</b>, so what
    /// is picked is what is being read at a glance and no colour has to be learned to see it.
    /// </summary>
    const float PickedLineM = PathMarks.PathLineM * 3f;

    /// <summary>Held to this on screen, so a pick reads at a district framing as well as over a car.</summary>
    const float PickedLineFloorPx = 2.5f;

    /// <summary>How far off a boundary the pointer may stand and still be on it: a finger's width on the glass.</summary>
    const float ReachPx = 10f;

    /// <summary>What the pinned card is marked with, at the end of its title.</summary>
    const string PinnedTag = "pinned";

    /// <summary>
    /// Room for one card's words and lines — the longest is a way with every claim on it listed. <b>Held here
    /// and not on the stack</b>: the card is drawn into the frame's own buffer, and a span of the stack handed
    /// to that is one the compiler cannot prove does not outlive the call.
    /// </summary>
    readonly char[] _cardText = new char[1024];

    readonly int[] _cardEnds = new int[12];

    /// <summary>How many of the claims on one way a card lists before it counts the rest.</summary>
    const int MostClaimsListed = 6;

    /// <summary>
    /// The chains one cell holds, the lines a place could be on, and the ribbons of the lines picked out —
    /// <b>the working sets this pass answers out of, kept because it runs every frame</b>. The first two are
    /// sized to the town the first time it is asked about; a ribbon is laid again only when the line it is a
    /// ribbon of changes, one for the pin and one for the pointer so the two do not take turns.
    /// </summary>
    int[] _inCell = [];
    int[] _nearLines = [];
    float[] _nearAlongM = [];
    ArcSeg[] _pinnedRibbon = [];
    ArcSeg[] _hoveredRibbon = [];
    int _pinnedRibbonLine = -1;
    int _hoveredRibbonLine = -1;

    void Pointer(
        ref ScreenDraw draw, TownWorld world, SimConfig config, DebugSwitches switches, DebugPick pick,
        Vector2 pointerM, Vector2 pointerPx, Vector2 uiPx, float pixelsPerMetre)
    {
        var asked = pick.TakeAsked(out var askedM);
        if (!switches.AnyOn)
        {
            if (asked || pick.Pinned.Thing != DebugThing.None) pick.Clear();
            return;
        }

        var paving = world.Plan.Paving(config);
        Room(paving);

        if (asked) pick.Pin(Under(world, paving, config, switches, askedM, pixelsPerMetre));
        if (!Findable(world, switches, pick.Pinned)) pick.Pin(DebugTarget.None);

        // Off the glass is over a panel or no pointer at all (a shot takes none unless it is asked for one).
        var onGlass = pointerPx.X >= 0f && pointerPx.Y >= 0f && pointerPx.X <= uiPx.X && pointerPx.Y <= uiPx.Y;
        var hovered = onGlass
            ? Under(world, paving, config, switches, pointerM, pixelsPerMetre)
            : DebugTarget.None;
        pick.Hovered = hovered;
        var hoverShown = hovered.Thing != DebugThing.None && !hovered.Same(pick.Pinned);

        if (switches.Grid) Standing(ref draw, uiPx, pointerM, pointerPx);

        // Both marks before either card, so a card is never under a line it is about.
        var focus = new Focus(world, paving, config, switches, pixelsPerMetre);
        Highlight(ref draw, focus, pick.Pinned, pinned: true);
        if (hoverShown) Highlight(ref draw, focus, hovered, pinned: false);

        if (pick.Pinned.Thing != DebugThing.None)
        {
            var card = new InfoCard(_cardText, _cardEnds);
            Describe(ref card, focus, pick.Pinned, pinned: true);
            card.Draw(ref draw, DockedAt(uiPx, card.SizePx(PinnedTag.Length)), PinnedTag);
        }

        if (hoverShown)
        {
            var card = new InfoCard(_cardText, _cardEnds);
            Describe(ref card, focus, hovered, pinned: false);
            card.Draw(ref draw, InfoCard.Beside(pointerPx, card.SizePx(), uiPx));
        }
    }

    /// <summary>What every reading of one frame is asked against, gathered once.</summary>
    readonly record struct Focus(
        TownWorld World, Paving Paving, SimConfig Config, DebugSwitches Switches, float PixelsPerMetre)
    {
        public float LineM => MathF.Max(PickedLineM, PickedLineFloorPx / PixelsPerMetre);

        public float SagM => PathMarks.SagPx / PixelsPerMetre;
    }

    /// <summary>
    /// <b>The pinned card's place: under the corner buttons, against the window's trailing edge.</b> Out of the
    /// way of the pointer and of the town it is about, and where the menu that threw the layers hangs — so a
    /// reader opening the menu to change what is drawn covers the card rather than the town.
    /// </summary>
    static Vector2 DockedAt(Vector2 uiPx, Vector2 sizePx) =>
        new(
            MathF.Max(Theme.MarginPx, uiPx.X - Theme.MarginPx - sizePx.X),
            Theme.MarginPx + Theme.GearPx + (Theme.GapPx * 2f));

    /// <summary>
    /// <b>The one thing the layers that are on draw at a place</b>, or nothing: a body first, then a stretch of
    /// boundary, then a way and what holds it, then a ribbon, then a cell.
    /// </summary>
    /// <remarks>
    /// <b>The order is what is drawn over what.</b> A body stands on the lines, a boundary is a hairline at the
    /// edge of the ribbons it is the outside of, and a cell is a ruling under all of them — so a thin thing
    /// is found before a broad one that covers it, or it could never be pointed at at all. A way is found
    /// before a ribbon only while the claims are on, since that is the layer whose whole reading is one way.
    /// </remarks>
    DebugTarget Under(
        TownWorld world, Paving paving, SimConfig config, DebugSwitches switches, Vector2 pointM,
        float pixelsPerMetre)
    {
        if (CarsFindable(switches))
        {
            var car = world.CarAt(pointM);
            if (car >= 0) return new DebugTarget(DebugThing.Car, car);
        }

        if (WalkersFindable(switches))
        {
            var person = world.PersonAt(pointM);
            if (person >= 0) return new DebugTarget(DebugThing.Walker, person);
        }

        if (switches.Perimeter)
        {
            var found = BoundaryAt(paving, config, pointM, pixelsPerMetre);
            if (found.Chain is not null) return new DebugTarget(DebugThing.Boundary, found.Piece, found.At, pointM);
        }

        if (switches.Claims && WayAt(world, pointM, out _) is var claimed and >= 0)
        {
            return new DebugTarget(DebugThing.Way, Way: claimed, AtM: pointM);
        }

        if (switches.Ribbons && LineUnder(paving, config, pointM) is var line and >= 0)
        {
            return new DebugTarget(DebugThing.Line, line);
        }

        if (switches.Nodes && WayAt(world, pointM, out _) is var way and >= 0)
        {
            return new DebugTarget(DebugThing.Way, Way: way, AtM: pointM);
        }

        if (switches.SolverGrid && SolverCellAt(world, pointM) > 0)
        {
            return new DebugTarget(DebugThing.SolverCell, AtM: pointM);
        }

        if (switches.Grid && GeometryCellAt(paving, config, pointM, out var atX, out var atY))
        {
            return new DebugTarget(DebugThing.GeometryCell, atX, atY, pointM);
        }

        return DebugTarget.None;
    }

    static bool CarsFindable(DebugSwitches switches) =>
        switches.CarLines || switches.TurnCircles || switches.Collision || switches.Claims;

    static bool WalkersFindable(DebugSwitches switches) =>
        switches.WalkerLines || switches.Collision || switches.Claims;

    /// <summary>Whether a pinned thing is still one the layers that are on could find, in a roster that still has it.</summary>
    static bool Findable(TownWorld world, DebugSwitches switches, in DebugTarget target) => target.Thing switch
    {
        DebugThing.None => true,
        DebugThing.Car => CarsFindable(switches) && target.Index < world.Cars.Count,
        DebugThing.Walker => WalkersFindable(switches) && target.Index < world.People.Count,
        DebugThing.Boundary => switches.Perimeter,
        DebugThing.Way => switches.Claims || switches.Nodes,
        DebugThing.Line => switches.Ribbons,
        DebugThing.SolverCell => switches.SolverGrid,
        _ => switches.Grid,
    };

    void Highlight(ref ScreenDraw draw, in Focus focus, in DebugTarget target, bool pinned)
    {
        switch (target.Thing)
        {
            case DebugThing.Car: FocusCar(ref draw, focus, target.Index); break;
            case DebugThing.Walker: FocusWalker(ref draw, focus, target.Index); break;
            case DebugThing.Boundary: FocusBoundary(ref draw, focus, target.AtM); break;
            case DebugThing.Way: FocusWay(ref draw, focus, target.Way, target.AtM); break;
            case DebugThing.Line: FocusRibbon(ref draw, focus, target.Index, pinned); break;
            case DebugThing.SolverCell: FocusSolverCells(ref draw, focus, target.AtM); break;
            case DebugThing.GeometryCell: FocusGeometryCell(ref draw, focus, target.AtM); break;
        }
    }

    void Describe(ref InfoCard card, in Focus focus, in DebugTarget target, bool pinned)
    {
        switch (target.Thing)
        {
            case DebugThing.Car: DescribeCar(ref card, focus.World, target.Index); break;
            case DebugThing.Walker: DescribeWalker(ref card, focus.World, target.Index); break;
            case DebugThing.Boundary: DescribeBoundary(ref card, focus, target.AtM); break;
            case DebugThing.Way: DescribeWay(ref card, focus, target.Way, target.AtM); break;
            case DebugThing.Line: DescribeRibbon(ref card, focus, target.Index, pinned); break;
            case DebugThing.SolverCell: DescribeSolverCells(ref card, focus.World, target.AtM); break;
            case DebugThing.GeometryCell: DescribeGeometryCell(ref card, focus, target.AtM); break;
        }
    }

    /// <summary>
    /// <b>A body picked out</b>: its outline on a dark casing, and its own two pieces of route drawn at the
    /// picked weight whichever of the layers found it — the route is what a reader pointing at a car is asking
    /// about, and it stands out of the crowd of the others only when it is the one drawn heavier.
    /// </summary>
    static void FocusCar(ref ScreenDraw draw, in Focus focus, int car)
    {
        var cars = focus.World.Cars;
        ref readonly var build = ref cars.BuildOf(car);
        var sizeM = new Vector2(build.LengthM, build.WidthM);
        var lineM = focus.LineM;
        draw.BoxM(cars.PositionM[car], sizeM, cars.HeadingRad[car], lineM * PathMarks.CasingWidthFactor, Theme.Casing);
        draw.BoxM(cars.PositionM[car], sizeM, cars.HeadingRad[car], lineM, Theme.DebugPicked);

        CarRoute(
            ref draw, focus.World, car, PathMarks.MarkPitchAt(focus.Config.Grid, focus.PixelsPerMetre), focus.SagM, lineM,
            Theme.AgentLine(car));
    }

    static void FocusWalker(ref ScreenDraw draw, in Focus focus, int person)
    {
        var people = focus.World.People;
        var lineM = focus.LineM;
        var radiusM = people.RadiusM[person] + lineM;
        draw.RingM(people.PositionM[person], radiusM, lineM * PathMarks.CasingWidthFactor, Theme.Casing);
        draw.RingM(people.PositionM[person], radiusM, lineM, Theme.DebugPicked);

        WalkerRoute(
            ref draw, focus.World, person, PathMarks.MarkPitchAt(focus.Config.Grid, focus.PixelsPerMetre), lineM, Theme.AgentLine(person));
    }

    static void DescribeCar(ref InfoCard card, TownWorld world, int car)
    {
        var cars = world.Cars;
        ref readonly var build = ref cars.BuildOf(car);

        var line = card.Next();
        line.Add("car ");
        line.Add(car);
        line.Add(": ");

        // <b>A car whose wheel is held over is named by the command and not by the catalogue.</b> It is in no
        // manoeuvre and holds no line — a hand at the wheel substitutes the whole behaviour (CTL-5) — so the
        // words its own controller uses would call it parked, which is the one thing a car circling on full
        // lock is not.
        if (world.WheelIsHeldOver(car)) WheelWords(cars.Command[car], build, ref line);
        else line.Add(CarName(cars, car));
        card.Keep(in line);

        line = card.Next();
        line.Add("speed ");
        line.Add(cars.VelocityMps[car].Length() * 3.6f, "F0");
        if (cars.Driven[car] && cars.PlannedMps[car] > 0f && float.IsFinite(cars.PlannedMps[car]))
        {
            line.Add(" of ");
            line.Add(cars.PlannedMps[car] * 3.6f, "F0");
        }

        line.Add(" km/h");
        card.Keep(in line);

        // The follower's own figures, the ring and the bar the route is drawn with.
        var context = cars.Context[car];
        line = card.Next();
        line.Add("granted ");
        if (float.IsFinite(context.AuthorityM))
        {
            line.Add(context.AuthorityM, "F1");
            line.Add(" m from the nose");
        }
        else
        {
            line.Add("clear");
        }

        card.Keep(in line);

        if (float.IsFinite(cars.LightAheadM[car]))
        {
            line = card.Next();
            line.Add("light in ");
            line.Add(cars.LightAheadM[car], "F1");
            line.Add(" m");
            card.Keep(in line);
        }

        if (cars.LineOf(car).Length > 0)
        {
            var totalM = cars.Line[car].LengthM;
            line = card.Next();
            line.Add("line ");
            line.Add(Math.Clamp(cars.ProgressM[car], 0f, totalM), "F1");
            line.Add(" of ");
            line.Add(totalM, "F1");
            line.Add(" m driven");
            card.Keep(in line);
        }
    }

    static void DescribeWalker(ref InfoCard card, TownWorld world, int person)
    {
        var people = world.People;
        var line = card.Next();
        line.Add("walker ");
        line.Add(person);
        line.Add(": ");
        line.Add(WalkingWords.WalkName(people, person));
        card.Keep(in line);

        line = card.Next();
        line.Add("speed ");
        line.Add(people.VelocityMps[person].Length(), "F1");
        line.Add(" m/s");
        card.Keep(in line);

        var at = people.RouteAt(person);
        var count = people.RouteCount[person];
        if (!people.Walking[person] || at < 0 || at >= count) return;

        line = card.Next();
        line.Add("way ");
        line.Add(at + 1);
        line.Add(" of ");
        line.Add(count);
        line.Add(", ");
        line.Add(people.OnWayM[person], "F1");
        line.Add(" m along it");
        card.Keep(in line);
    }

    /// <summary>
    /// <b>The way the pointer is on</b> — any of the town's ways, whatever ground it is laid on — and how far
    /// along it the pointer stands, or −1. The nearest where several cover the place, which is the one the
    /// reader is pointing at; nothing off either end, since a way's ground has square ends.
    /// </summary>
    /// <remarks>
    /// <b>Every way is weighed, and most in one comparison.</b> A way further from the place than its own
    /// length and half its width from where it starts cannot reach it, which turns away everything outside a
    /// street's length of the pointer before anything is projected onto.
    /// </remarks>
    static int WayAt(TownWorld world, Vector2 pointM, out float alongM)
    {
        var ways = world.Ways;
        var best = -1;
        var bestSq = float.MaxValue;
        alongM = 0f;
        for (var way = 0; way < ways.Count; way++)
        {
            var arcs = world.LineOfWay(way, out var widthM);
            if (arcs.Length == 0) continue;

            var halfM = widthM * 0.5f;
            var lengthM = ways.LengthM(way);
            var reachM = lengthM + halfM;
            if (Vector2.DistanceSquared(arcs[0].StartM, pointM) > reachM * reachM) continue;

            var atM = Spline.ProjectM(arcs, pointM, lengthM * 0.5f, reachM, out var offSq);
            if (atM <= 0f || atM >= lengthM || offSq >= halfM * halfM || offSq >= bestSq) continue;

            best = way;
            bestSq = offSq;
            alongM = atM;
        }

        return best;
    }

    /// <summary>How far along one way a place stands, which is what a pinned way is asked again at every frame.</summary>
    static float AlongWay(TownWorld world, int way, Vector2 pointM)
    {
        var arcs = world.LineOfWay(way, out _);
        var lengthM = world.Ways.LengthM(way);
        return arcs.Length == 0 ? 0f : Spline.ProjectM(arcs, pointM, lengthM * 0.5f, lengthM);
    }

    /// <summary>The way whole at the picked weight, and a bar across it where the pointer stood.</summary>
    static void FocusWay(ref ScreenDraw draw, in Focus focus, int way, Vector2 atM)
    {
        var arcs = focus.World.LineOfWay(way, out var widthM);
        var lengthM = focus.World.Ways.LengthM(way);
        var lineM = focus.LineM;
        PathMarks.Casing(ref draw, arcs, 0f, lengthM, focus.SagM, lineM);
        PathMarks.Banded(ref draw, arcs, 0f, lengthM, focus.SagM, lineM, Theme.DebugPicked);

        var at = Spline.SampleAt(arcs, AlongWay(focus.World, way, atM));
        var halfM = widthM * 0.5f;
        draw.LineM(at.PositionM - (at.Right * halfM), at.PositionM + (at.Right * halfM), lineM, Theme.DebugPicked);
    }

    /// <summary>
    /// <b>What a way is and who holds it</b>: its kind, its length and width, where along it the pointer
    /// stands, and — with the claims on — every stretch of it somebody holds, <b>marked where it covers the
    /// pointer</b>, since what a reader at a junction wants is who has the ground under the cursor and how
    /// strongly, which the wash alone says only for the top one of several.
    /// </summary>
    static void DescribeWay(ref InfoCard card, in Focus focus, int way, Vector2 atM)
    {
        var world = focus.World;
        var ways = world.Ways;
        world.LineOfWay(way, out var widthM);
        var alongM = AlongWay(world, way, atM);

        var line = card.Next();
        var kind = ways.KindOf(way);
        line.Add(kind == WayKind.Lane && world.Roads.IsARoadside(ways.RoadLaneOf(way)) ? "roadside" : WayWords[(int)kind]);
        line.Add(' ');
        line.Add(way);
        card.Keep(in line);

        line = card.Next();
        line.Add(ways.LengthM(way), "F1");
        line.Add(" m long, ");
        line.Add(widthM, "F2");
        line.Add(" m wide, pointer at ");
        line.Add(alongM, "F1");
        line.Add(" m");
        card.Keep(in line);

        if (!focus.Switches.Claims) return;

        Span<LaneClaim> slots = stackalloc LaneClaim[MostDrawnSlotsOnAWay];
        var count = world.Occupancy.CopyTo(way, slots);
        if (count == 0)
        {
            line = card.Next();
            line.Add("nothing holds it");
            card.Keep(in line);
            return;
        }

        for (var slot = 0; slot < count && slot < MostClaimsListed; slot++)
        {
            ref readonly var claim = ref slots[slot];
            line = card.Next();
            line.Add(claim.FromM <= alongM && alongM <= claim.ToM ? "> " : "  ");
            if (claim.Occupant == LaneOccupancy.Nobody) line.Add("furniture");
            else
            {
                line.Add(RosterWords[(int)claim.Of]);
                line.Add(claim.Occupant);
            }

            line.PadTo(14);
            line.Add(PriorityWords[(int)claim.Priority]);
            line.PadTo(28);
            line.Add(claim.FromM, "F1");
            line.Add(" - ");
            line.Add(claim.ToM, "F1");
            line.Add(" m");
            if (claim.Secondary) line.Add(", crossed");
            card.Keep(in line);
        }

        if (count <= MostClaimsListed) return;

        line = card.Next();
        line.Add("and ");
        line.Add(count - MostClaimsListed);
        line.Add(" more");
        card.Keep(in line);
    }

    /// <summary>What each kind of way is called on a card, by <see cref="WayKind"/>.</summary>
    static readonly string[] WayWords = ["lane", "join", "bay way", "footway", "corner"];

    /// <summary>
    /// What each rung of the ladder is called on a card, by <see cref="ClaimPriority"/>. <b>A table and not the
    /// enum's own names</b>: those are written for code and cost a string each time one is asked for.
    /// </summary>
    static readonly string[] PriorityWords =
        ["body", "committed", "special", "closed", "light", "crossing", "firm straight", "firm", "firm across"];

    /// <summary>What an occupant of each roster is called on a card, by <see cref="LaneRoster"/>.</summary>
    static readonly string[] RosterWords = ["car ", "walker ", "light "];

    /// <summary>
    /// Room for a town this size, and the widest band in it — both taken once, because a town is laid
    /// before it is pointed at and neither moves afterwards.
    /// </summary>
    void Room(Paving paving)
    {
        var lines = paving.DrivenCount;
        if (_nearLines.Length >= lines && _inCell.Length >= lines) return;

        _inCell = new int[lines];
        _nearLines = new int[lines];
        _nearAlongM = new float[lines];
        _widestHalfM = 0f;
        for (var line = 0; line < lines; line++)
        {
            _widestHalfM = MathF.Max(_widestHalfM, paving.DrivenWidthM(line) * 0.5f);
        }
    }

    float _widestHalfM;

    /// <summary>
    /// <b>Where the pointer stands on the town, in the town's own metres</b> (OBS-2t), written in the corner
    /// above the scale bar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>In the corner and not beside the cursor</b>, unlike the readings about what is under it. A
    /// coordinate is read while looking somewhere else — it is written down, typed into a command line,
    /// compared against a figure in a log — so it belongs where the eye can go back to it, which is the
    /// corner the scale bar has already made the place for saying how big things are (OBS-2e).
    /// </para>
    /// <para>
    /// <b>It is the grid's</b>, because a lattice is the one layer read in coordinates: every other layer
    /// draws a thing to be looked at, and this one draws where the things <em>are</em>.
    /// </para>
    /// </remarks>
    static void Standing(ref ScreenDraw draw, Vector2 uiPx, Vector2 pointerM, Vector2 pointerPx)
    {
        if (pointerPx.X < 0f || pointerPx.Y < 0f || pointerPx.X > uiPx.X || pointerPx.Y > uiPx.Y) return;

        Span<char> text = stackalloc char[32];
        var said = new TextBuffer(text);
        said.Add(pointerM.X, "F2");
        said.Add(", ");
        said.Add(pointerM.Y, "F2");
        said.Add(" m");

        // Right-aligned to the legend's own margin, so the two read as one column rather than as two
        // things that happen to be in the same corner.
        draw.OutlinedText(
            new Vector2(
                uiPx.X - CornerMarginPx - GlyphSheet.WidthPx(said.Length, Theme.TextPx),
                uiPx.Y - AboveTheLegendPx),
            said.Written, Theme.TextPx);
    }

    /// <summary>The corner the scale bar keeps (OBS-2e), held to here so a reading over it lines up with it.</summary>
    const float CornerMarginPx = 18f;

    /// <summary>
    /// How far off the bottom that reading's own top stands: clear of the scale bar's stack — its margin,
    /// the bar, a large graduation and the figures over them — with a line's daylight above that.
    /// <b>A figure and not a measurement of the legend</b>, which belongs to another slice and is not this
    /// one's to ask about; what keeps the two apart is that both are laid off the same corner.
    /// </summary>
    const float AboveTheLegendPx = CornerMarginPx + 5f + 13f + Theme.SmallTextPx + Theme.TextPx + 8f;

    /// <summary>The cell of the geometry grid a place is in, numbered on the grid, held to the index's window.</summary>
    static bool GeometryCellAt(Paving paving, SimConfig config, Vector2 pointM, out int atX, out int atY)
    {
        var window = paving.DrivenLines(config).Window;
        atX = atY = 0;
        if (window.IsEmpty) return false;

        atX = window.ClampX(window.Level.CellOf(pointM.X));
        atY = window.ClampY(window.Level.CellOf(pointM.Y));
        return true;
    }

    /// <summary>
    /// <b>The cell picked out, and every line the index holds in it</b>: the cell's own square, and each of
    /// those lines drawn whole at the picked weight.
    /// </summary>
    /// <remarks>
    /// <b>Whole lines and not the piece of each inside the cell.</b> What a cell says is which lines a
    /// question asked there is narrowed to, and a line is offered as a candidate in its entirety however
    /// little of it reaches the cell — so lighting only the part inside would be a picture of the cell rather
    /// than of the answer it gives.
    /// </remarks>
    void FocusGeometryCell(ref ScreenDraw draw, in Focus focus, Vector2 atM)
    {
        if (!GeometryCellAt(focus.Paving, focus.Config, atM, out var atX, out var atY)) return;

        var grid = focus.Paving.DrivenLines(focus.Config);
        var level = grid.Window.Level;
        var cellM = level.CellM;
        var middleM = level.MiddleM(atX, atY);
        var held = grid.ChainsInCell(atX, atY, _inCell);

        draw.BoxM(middleM, new Vector2(cellM), 0f, focus.LineM, Theme.DebugPicked);
        for (var at = 0; at < held && at < _inCell.Length; at++)
        {
            var line = _inCell[at];
            PathMarks.Banded(
                ref draw, focus.Paving.ArcsOfDriven(line), 0f, focus.Paving.DrivenLengthM(line), focus.SagM,
                focus.LineM, Theme.DebugHeld);
        }
    }

    static void DescribeGeometryCell(ref InfoCard card, in Focus focus, Vector2 atM)
    {
        if (!GeometryCellAt(focus.Paving, focus.Config, atM, out var atX, out var atY)) return;

        var grid = focus.Paving.DrivenLines(focus.Config);
        var line = card.Next();
        line.Add("geometry cell ");
        line.Add(atX);
        line.Add(", ");
        line.Add(atY);
        card.Keep(in line);

        line = card.Next();
        line.Add("holds ");
        line.Add(grid.ChainsInCell(atX, atY));
        line.Add(" lines");
        card.Keep(in line);

        var window = grid.Window;
        line = card.Next();
        line.Add(window.Width);
        line.Add(" x ");
        line.Add(window.Height);
        line.Add(" cells of ");
        line.Add(window.Level.CellM, "F1");
        line.Add(" m");
        card.Keep(in line);
    }

    /// <summary>How many bodies the three solver grids hold between them at a place, which is whether there is a cell there to ask about.</summary>
    static int SolverCellAt(TownWorld world, Vector2 pointM)
    {
        var physics = world.PhysicsForInstruments;
        return BodiesAt(physics.MovingIndex, pointM, out _, out _) + BodiesAt(physics.FrozenIndex, pointM, out _, out _)
               + BodiesAt(physics.StaticIndex, pointM, out _, out _);
    }

    /// <summary>How many bodies one index holds in the grid's cell over a place, and which cell that is — none off its window.</summary>
    static int BodiesAt(CellGrid grid, Vector2 pointM, out int atX, out int atY)
    {
        (atX, atY) = grid.Window.Level.CellOf(pointM);
        return grid.Items(atX, atY).Length;
    }

    /// <summary>
    /// <b>Every solver cell over a place, the town's furniture in its hue and the bodies in theirs, frozen or not</b>
    /// (OBS-2x): the stores are on one grid at one level (SIM-8), so where several hold bodies the boxes are one
    /// square drawn again.
    /// </summary>
    static void FocusSolverCells(ref ScreenDraw draw, in Focus focus, Vector2 atM)
    {
        var physics = focus.World.PhysicsForInstruments;
        SolverCell(ref draw, physics.StaticIndex, atM, focus.LineM, Theme.SolverStaticEdge with { W = 1f });
        SolverCell(ref draw, physics.FrozenIndex, atM, focus.LineM, Theme.SolverMovingEdge with { W = 1f });
        SolverCell(ref draw, physics.MovingIndex, atM, focus.LineM, Theme.SolverMovingEdge with { W = 1f });
    }

    static void SolverCell(ref ScreenDraw draw, CellGrid grid, Vector2 atM, float lineM, Vector4 colour)
    {
        if (BodiesAt(grid, atM, out var atX, out var atY) == 0) return;

        var cellM = grid.Window.Level.CellM;
        var middleM = grid.Window.Level.MiddleM(atX, atY);
        draw.BoxM(middleM, new Vector2(cellM), 0f, lineM * PathMarks.CasingWidthFactor, Theme.Casing);
        draw.BoxM(middleM, new Vector2(cellM), 0f, lineM, colour);
    }

    static void DescribeSolverCells(ref InfoCard card, TownWorld world, Vector2 atM)
    {
        var physics = world.PhysicsForInstruments;
        var line = card.Next();
        line.Add("solver cells here");
        card.Keep(in line);

        SolverRow(ref card, "moving", physics.MovingIndex, atM);
        SolverRow(ref card, "frozen", physics.FrozenIndex, atM);
        SolverRow(ref card, "static", physics.StaticIndex, atM);
    }

    static void SolverRow(ref InfoCard card, string name, CellGrid grid, Vector2 atM)
    {
        var bodies = BodiesAt(grid, atM, out var atX, out var atY);
        var line = card.Next();
        line.Add(name);
        line.PadTo(8);
        if (!grid.Window.Holds(atX, atY))
        {
            line.Add("off the grid");
            card.Keep(in line);
            return;
        }

        line.Add("cell ");
        line.Add(atX);
        line.Add(", ");
        line.Add(atY);
        line.Add(" of ");
        line.Add(grid.Window.Level.CellM, "F1");
        line.Add(" m holds ");
        line.Add(bodies);
        card.Keep(in line);
    }

    /// <summary>
    /// <b>The ribbon a driven line lays</b>: the band of ground that line covers, drawn as the closed chain of
    /// lines it is (<see cref="ArcRibbon"/>) and nothing else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The ribbon the merge is given, and cut where the merge cuts it.</b> Its sections are an edge piece
    /// for each arc of the line, walked out and back, and a square end at either end of it — so a reader
    /// looking at where a boundary went wrong is looking at the pieces the merge weighed, not at a drawing of
    /// the band's outline.
    /// </para>
    /// <para>
    /// <b>The line alone, with no mark standing off it.</b> How many sections the chain has is written on the
    /// card, and a bar at each joint said the same thing a second time over the one thing the reading is for —
    /// the path the edge takes round a corner, which a comb of marks across it is what hides.
    /// </para>
    /// </remarks>
    void FocusRibbon(ref ScreenDraw draw, in Focus focus, int line, bool pinned)
    {
        foreach (var piece in RibbonOf(focus.Paving, line, pinned))
        {
            PathMarks.Banded(ref draw, [piece], 0f, piece.LengthM, focus.SagM, focus.LineM, Theme.DebugPicked);
        }
    }

    /// <summary>A driven line's ribbon, laid again only when the line asked about changes — this runs every frame.</summary>
    ReadOnlySpan<ArcSeg> RibbonOf(Paving paving, int line, bool pinned)
    {
        ref var laid = ref pinned ? ref _pinnedRibbon : ref _hoveredRibbon;
        ref var laidLine = ref pinned ? ref _pinnedRibbonLine : ref _hoveredRibbonLine;
        if (laidLine != line)
        {
            laid = ArcRibbon.Of(paving.ArcsOfDriven(line), paving.DrivenWidthM(line) * 0.5f, LineTolerance.RoundingM);
            laidLine = line;
        }

        return laid;
    }

    void DescribeRibbon(ref InfoCard card, in Focus focus, int line, bool pinned)
    {
        var paving = focus.Paving;
        var pieces = RibbonOf(paving, line, pinned);
        var roundM = 0f;
        foreach (var piece in pieces) roundM += piece.LengthM;

        var said = card.Next();
        said.Add(KindOf(paving, line));
        said.Add(' ');
        said.Add(line);
        card.Keep(in said);

        said = card.Next();
        said.Add(paving.DrivenLengthM(line), "F2");
        said.Add(" m driven, ");
        said.Add(paving.DrivenWidthM(line), "F2");
        said.Add(" m wide");
        card.Keep(in said);

        said = card.Next();
        said.Add(pieces.Length);
        said.Add(" sections round ");
        said.Add(roundM, "F2");
        said.Add(" m");
        card.Keep(in said);
    }

    /// <summary>
    /// Which driven line's own band a place stands on, or −1 — the nearest line where several cover it,
    /// which is the one the reader is pointing at.
    /// </summary>
    int LineUnder(Paving paving, SimConfig config, Vector2 pointM)
    {
        var found = paving.DrivenLines(config).Near(pointM, _widestHalfM, _nearLines, _nearAlongM);
        var best = -1;
        var bestM = float.MaxValue;
        for (var at = 0; at < found && at < _nearLines.Length; at++)
        {
            var line = _nearLines[at];

            // A band has square ends (TER-3c.6), so the ground off the end of a line is not the line's
            // however near it stands.
            if (_nearAlongM[at] <= 0f || _nearAlongM[at] >= paving.DrivenLengthM(line)) continue;

            var offM = Vector2.Distance(
                Spline.SampleAt(paving.ArcsOfDriven(line), _nearAlongM[at]).PositionM, pointM);
            if (offM >= paving.DrivenWidthM(line) * 0.5f || offM >= bestM) continue;

            bestM = offM;
            best = line;
        }

        return best;
    }

    /// <summary>What a driven line is, in the one numbering they all share (<see cref="Paving.DrivenCount"/>).</summary>
    static string KindOf(Paving paving, int line) =>
        line < paving.Lanes.LaneCount ? paving.Lanes.IsRoadside(line) ? "roadside" : "lane"
        : line < paving.Lanes.LaneCount + paving.Lanes.ConnectorCount ? "movement" : "bay way";

    /// <summary>
    /// <b>The one stretch of boundary nearest a place</b>, within a finger's width of it on the glass — and which
    /// outline it belongs to, which chain of that one, and which stretch of that.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every outline the layer draws is searched and they are searched alike</b>: the rings the merge
    /// closed, the runs it could not, and the layers struck off the boundary (OBS-2u). A reading offered over
    /// one of them and not the others would say that the pointer had found nothing wherever it stood over
    /// another, which is the one answer a pointer must not give — and a layer's outer edge is exactly the
    /// outline a reader most wants to ask about, being the one whose corners are a construction rather than a
    /// fact.
    /// </para>
    /// <para>
    /// <b>It reads the layers the picture drew and never a set of its own</b> (<see cref="Paving.Rings"/>).
    /// Struck again here they would be a second answer about the same shape, and the stretch under the
    /// pointer would be a stretch of a line nobody can see.
    /// </para>
    /// </remarks>
    static Picked BoundaryAt(Paving paving, SimConfig config, Vector2 pointM, float pixelsPerMetre)
    {
        var shell = paving.Perimeter(config);
        var rings = paving.Rings(config);
        var found = new Picked { OffM = ReachPx / MathF.Max(pixelsPerMetre, 0.001f) };

        Nearest(rings.Carriageway.Rings, rings.Carriageway.Named, Ring, pointM, ref found);
        Nearest(shell.Loose, rings.Carriageway.Named, OpenRun, pointM, ref found);
        foreach (var layer in rings.Layers)
        {
            Nearest(layer.Rings, layer.Named, Ring, pointM, ref found);
            Nearest(layer.Loose, layer.Named, OpenRun, pointM, ref found);
        }

        return found;
    }

    /// <summary>
    /// <b>A stretch and not the whole chain</b>, drawn at the picked weight with a disc at each of its ends. A
    /// ring is most of a district and lighting all of it says nothing; what a reader following a boundary
    /// wants is which piece they are on and where that piece stops — which is the whole of the question at an
    /// open run's two ends, and at every corner an outset put in.
    /// </summary>
    static void FocusBoundary(ref ScreenDraw draw, in Focus focus, Vector2 atM)
    {
        var found = BoundaryAt(focus.Paving, focus.Config, atM, focus.PixelsPerMetre);
        if (found.Chain is null) return;

        var stretch = found.Chain[found.Piece];
        PathMarks.Banded(ref draw, [stretch], 0f, stretch.LengthM, focus.SagM, focus.LineM, Theme.DebugPicked);
        draw.DiscM(stretch.StartM, PathMarks.JoinDiscM * 2f, Theme.DebugPicked);
        draw.DiscM(stretch.EndM, PathMarks.JoinDiscM * 2f, Theme.DebugPicked);
    }

    static void DescribeBoundary(ref InfoCard card, in Focus focus, Vector2 atM)
    {
        var found = BoundaryAt(focus.Paving, focus.Config, atM, focus.PixelsPerMetre);
        if (found.Chain is null) return;

        var stretch = found.Chain[found.Piece];
        var said = card.Next();
        said.Add(found.Outline);
        said.Add(' ');
        said.Add(found.Kind);
        said.Add(' ');
        said.Add(found.At);
        card.Keep(in said);

        said = card.Next();
        said.Add("stretch ");
        said.Add(found.Piece);
        said.Add(" of ");
        said.Add(found.Chain.Length);
        said.Add(", ");
        said.Add(stretch.LengthM, "F3");
        said.Add(" m");
        card.Keep(in said);

        // <b>What the stretch reads as running along, at each of its two ends, on a line of its own</b>
        // (<see cref="CityGen.KerbEnds"/>) — which is the whole of why a kerb end stands at one joint and
        // not at the next. <b>One line for each end</b>: the rounding joins consecutive stretches of one
        // circle, so a straight kerb is one stretch for every road it is straight through, and the two ends
        // of it answer two different things at two different distances back along them.
        Said(ref card, focus.Paving, focus.Config, stretch, fromStart: true);
        Said(ref card, focus.Paving, focus.Config, stretch, fromStart: false);
    }

    /// <summary>One end of a stretch of boundary: what it reads as running along, on a row of its own.</summary>
    static void Said(ref InfoCard card, Paving paving, SimConfig config, in ArcSeg stretch, bool fromStart)
    {
        var said = card.Next();
        said.Add(fromStart ? "from " : "to   ");

        var lane = CityGen.KerbEnds.LaneUnder(paving, config, stretch, fromStart);
        if (lane < 0)
        {
            said.Add("no road");
            card.Keep(in said);
            return;
        }

        var road = CityGen.KerbEnds.RoadOf(paving, lane);
        said.Add("lane ");
        said.Add(lane);
        if (road == CityGen.KerbEnds.Park)
        {
            said.Add(" of a car park");
            card.Keep(in said);
            return;
        }

        said.Add(" of road ");
        said.Add(road);

        // <b>And how far out of the box along that road this end stands</b>, which is the figure that
        // decides which end of a round is called the further of the two: a reader asking why one end took
        // that name can read both of them and see.
        said.Add(", ");
        said.Add(CityGen.KerbEnds.OutM(paving, lane, stretch.PointAtM(fromStart ? 0f : stretch.LengthM)), "F1");
        said.Add(" m out");
        card.Keep(in said);
    }

    /// <summary>
    /// The stretch of boundary nearest the pointer as the search stands: what outline it is in, which chain
    /// of that outline and which stretch of that chain, and how far off the pointer stood.
    /// </summary>
    /// <remarks>
    /// <b>The chain itself and not the outline it came out of</b>, so that what is drawn and counted is the
    /// thing that won rather than something looked up again out of whichever set it was found in.
    /// </remarks>
    struct Picked
    {
        public float OffM;
        public string Outline;
        public string Kind;
        public ArcSeg[]? Chain;
        public int At;
        public int Piece;
    }

    /// <summary>
    /// What a chain is, said apart from which outline it belongs to — <b>so the two are never joined into a
    /// string</b>, this running every frame and the steady state allocating nothing.
    /// </summary>
    const string Ring = "ring";

    const string OpenRun = "open run";

    /// <summary>
    /// The nearest stretch of one outline to a place, kept where it beats what already stands — which is
    /// what lets several outlines be searched one after another and the best of all of them come back.
    /// </summary>
    static void Nearest(
        ReadOnlySpan<ArcSeg[]> chains, string outline, string kind, Vector2 pointM, ref Picked found)
    {
        for (var at = 0; at < chains.Length; at++)
        {
            var chain = chains[at];
            for (var piece = 0; piece < chain.Length; piece++)
            {
                var stretch = chain[piece];

                // <b>Rejected on its own start before it is projected onto</b>: a city's boundary is a
                // hundred thousand stretches and this runs every frame, while nothing further from the
                // pointer than its own length plus the reach can possibly win.
                var reachM = stretch.LengthM + found.OffM;
                if (Vector2.DistanceSquared(stretch.StartM, pointM) > reachM * reachM) continue;

                var alongM = Spline.ProjectM([stretch], pointM, stretch.LengthM * 0.5f, stretch.LengthM);
                var offM = Vector2.Distance(stretch.PointAtM(alongM), pointM);
                if (offM >= found.OffM) continue;

                found.OffM = offM;
                found.Outline = outline;
                found.Kind = kind;
                found.Chain = chain;
                found.At = at;
                found.Piece = piece;
            }
        }
    }
}
