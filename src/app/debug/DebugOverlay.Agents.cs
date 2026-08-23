using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Runtime;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Debug;

/// <summary>What is drawn over one agent: its name, its line, and the chevrons along it.</summary>
internal sealed partial class DebugOverlay
{
    static void WalkerLines(
        ref ScreenDraw draw, TownWorld world, SimConfig config, Vector2 viewCentreM, Vector2 viewSpanM,
        float pixelsPerMetre)
    {
        var pitchM = MarkPitchAt(pixelsPerMetre);
        var people = world.People;
        for (var person = 0; person < people.Count; person++)
        {
            var atM = people.PositionM[person];
            if (!OnScreen(atM, viewCentreM, viewSpanM, config.PersonDiameterM)) continue;

            var colour = Theme.AgentLine(person);

            // A body held at a kerb is marked where it stands: standing still with a line ahead of it is
            // the one state this layer could otherwise not tell from walking. The body itself is not
            // ringed — the sprite is already there, and a mark on it says nothing.
            if (people.HeldAtTheKerb[person]) draw.RingM(atM, people.RadiusM[person] * 1.6f, PathLineM, colour, segments: 12);

            Label(ref draw, atM, WalkName(people, person), viewCentreM, viewSpanM, pixelsPerMetre);

            if (!people.Walking[person]) continue;

            // OBS-2h for a walker, which is OBS-2h for a car in the walking network's own words: the points
            // of a walked line carry which crossing each stands on, so a run sharing one crossing is one
            // pavement up to the kerb it stops at or one crossing of one road — the walker's lane. Two of
            // them, the one being walked and the one it leads onto, and the point between them is a dot.
            var count = people.WalkedCount[person];
            var at = people.WalkedAt(person);
            if (at < 0 || at >= count) continue;

            var points = people.WalkedLineOf(person);
            var crossings = people.WalkedCrossingOf(person);

            // A body standing on the point it is walking at has finished that stretch and is about to be
            // handed the next one — a kerb is where this is always true, because that is what waiting to
            // cross is. Drawn from the point underfoot the stretch is a dot, so the stretch in hand is the
            // one starting at the first point the body is not already standing on.
            while (at < count - 1 && (points[at] - atM).Length() <= people.RadiusM[person]) at++;

            // Every point of the line carries how far along its own way it stands, so the marks are counted
            // in the way's metres — the same count the town layer makes down the same pavement, and one
            // that stands still while the body walks through it.
            var alongsM = people.WalkedAlongOf(person);
            var crossing = crossings[at];
            var stretches = 1;
            var fromM = atM;
            draw.DiscM(fromM, EndDiscM, colour);
            Chevroned(ref draw, fromM, points[at], pitchM, alongsM[at] - (points[at] - atM).Length(), colour);
            fromM = points[at];
            for (var point = at + 1; point < count; point++)
            {
                if (crossings[point] != crossing)
                {
                    if (++stretches > StretchesDrawn) break;

                    draw.DiscM(fromM, JoinDiscM, colour);
                    crossing = crossings[point];
                }

                // Measured back from the point the leg lands on, so a leg that carries the body onto a new
                // way is counted in that way's metres and not in the one behind it.
                Chevroned(
                    ref draw, fromM, points[point], pitchM, alongsM[point] - (points[point] - fromM).Length(), colour);
                fromM = points[point];
            }

            draw.DiscM(fromM, EndDiscM, colour);
        }
    }

    /// <summary>
    /// What a walker is doing, in the words its own follower uses. A state and not a manoeuvre: the
    /// walker's catalogue is unbuilt, and a label naming one of its entries would claim behaviour that
    /// is not there.
    /// </summary>
    static ReadOnlySpan<char> WalkName(Agents.Person.Body.PersonFleet people, int person)
    {
        if (people.Dead[person]) return "dead";
        if (people.HeldAtTheKerb[person]) return "held at the kerb";

        // What the trip is doing outranks what the body is doing, because a body standing still is the
        // one thing several of these states have in common.
        if (!people.Walking[person])
        {
            return people.Stage[person] switch
            {
                Agents.Person.Control.TripStage.WaitingForAPlace => "waiting for a place",
                Agents.Person.Control.TripStage.Alighting => "getting out",
                Agents.Person.Control.TripStage.UnderOrders => "awaiting orders",
                Agents.Person.Control.TripStage.StandingBy => "standing by",
                _ => "standing",
            };
        }

        // Standing still with a line ahead of it and no kerb in front: the ground it wanted is somebody
        // else's, which is the other state this layer could not otherwise tell from walking. The lane it
        // was refused is worth naming apart from the pavement it is queueing on — one is traffic and the
        // other is a crowd, and they look identical from here.
        if (people.IsHeldByTheBook(person))
        {
            return people.RefusedWay[person] == Agents.Person.Body.PersonFleet.NoWay
                ? "waiting behind somebody"
                : "waiting for a lane";
        }

        var taken = people.WalkedTaken[person];
        var line = people.WalkedCrossingOf(person);
        if (taken > 0 && taken <= line.Length && line[taken - 1] >= 0) return "on the crossing";

        return people.Stage[person] == Agents.Person.Control.TripStage.WalkingToTheCar ? "walking to a car" : "walking";
    }

    /// <summary>
    /// One run of the line, with chevrons down it rather than an arrowhead on the end: they say which way
    /// it runs along its whole length, which is what tells a walker on its line from a walker beside it.
    /// </summary>
    /// <param name="sinceAnchorM">
    /// How far this run's start stands past the mark the pitch is counted from — the start of the piece
    /// being drawn, which is a place on the ground and not a place on a body. <b>The marks stand still
    /// while the agent walks through them</b>, and two bodies on one stretch put their marks in the same
    /// places rather than each carrying its own comb along.
    /// </param>
    static void Chevroned(
        ref ScreenDraw draw, Vector2 fromM, Vector2 toM, float pitchM, float sinceAnchorM, Vector4 colour)
    {
        draw.LineM(fromM, toM, PathLineM, colour);

        var alongM = toM - fromM;
        var lengthM = alongM.Length();
        if (lengthM <= 1e-3f) return;

        for (var at = FirstMarkM(sinceAnchorM, pitchM); at < lengthM; at += pitchM)
        {
            draw.ChevronM(
                fromM + (alongM / lengthM * at), alongM, pitchM * MarkSizeFraction, PathLineM * MarkWidthFactor,
                colour);
        }
    }

    /// <summary>
    /// The cars: the two pieces of route each is driving, what it found ahead of it and where it
    /// must be stopped by.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>OBS-2h — the stretch and not the route.</b> A car's line is laid over several lanes at a time,
    /// and drawn whole it says a car is committed to ground the entry in charge has not reached. The
    /// junction a car holds is not drawn a second time on top of that: the ground of the claim is the
    /// ground of the join the route runs through, and a bold line over it says the two are different
    /// things.
    /// </para>
    /// <para>
    /// Every line is drawn for the rear axle, because that is the line the follower actually holds —
    /// from the middle of the body it would show a car cutting every corner it is taking correctly.
    /// </para>
    /// <para>
    /// Everything the driver was told is read off the car and not recomputed
    /// (<see cref="Agents.Car.Body.CarFleet.Context"/>): a layer asking the book itself would be drawing
    /// a second opinion beside the car, and one that agreed would be the more misleading of the two.
    /// </para>
    /// <para>
    /// <b>The steering aim point is deliberately not drawn.</b> Everything else this layer marks is
    /// something the world did to the car — ground somebody has taken, a body in front, a place it is held
    /// at — and a ring standing out on the line among them reads as a thing the car <em>found</em> there.
    /// It is not: it is where the wheel is pointed (<see cref="Agents.Car.Control.CarFollower.Steer"/>),
    /// an output and not a reading, and it is a time ahead of the body rather than a place, so on a bend
    /// it sits off the car's own path and looks like a detection that has drifted. What a driver can see
    /// is the book, and the book is drawn as the ground it is.
    /// </para>
    static void CarLines(
        ref ScreenDraw draw, TownWorld world, SimConfig config, Vector2 viewCentreM, Vector2 viewSpanM,
        float pixelsPerMetre)
    {
        var pitchM = MarkPitchAt(pixelsPerMetre);
        var cars = world.Cars;
        for (var car = 0; car < cars.Count; car++)
        {
            var atM = cars.PositionM[car];
            if (!OnScreen(atM, viewCentreM, viewSpanM, config.Car.LengthM)) continue;

            var colour = Theme.AgentLine(car);
            Label(ref draw, atM, CarName(cars, car), viewCentreM, viewSpanM, pixelsPerMetre);

            var line = cars.LineOf(car);
            if (line.Length == 0) continue;

            // The line starts under the middle of the body and not at the end the route was planned from:
            // the ground already covered is where the car has been, and a layer drawing it is answering a
            // question about the past. The progress itself is the rear axle's, which is a car's length of
            // line behind the body it belongs to. Line and marks are the one colour, because they are the
            // one line — what tells this car's route from the next car's is the colour it is drawn in.
            var totalM = cars.Line[car].LengthM;
            var progressM = Math.Clamp(cars.ProgressM[car], 0f, totalM);
            var underTheCarM = MathF.Min(progressM + config.CarCentreAheadOfAxleM, totalM);

            // The piece being driven and the piece it leads onto: the rest of this lane and the junction
            // off the end of it, or the junction being crossed and the lane it lands on. Both are the one
            // chain the assembler wove, so they are drawn as one run of line and the dot between them is
            // where the car changes what it is doing. Each piece counts its marks from its own start,
            // which is a place on the road — so the marks stand still while the car drives through them
            // and every car on the lane puts its own on the same stones.
            var joinM = PieceEndM(cars, world.Roads, car, underTheCarM, totalM, out var anchorM);
            var untilM = PieceEndM(cars, world.Roads, car, joinM, totalM, out var beyondAnchorM);
            var sagM = SagPx / pixelsPerMetre;
            Chained(ref draw, line, underTheCarM, joinM, anchorM, pitchM, sagM, colour);
            Chained(ref draw, line, joinM, untilM, beyondAnchorM, pitchM, sagM, colour);

            draw.DiscM(Spline.SampleAt(line, underTheCarM).PositionM, EndDiscM, colour);
            draw.DiscM(Spline.SampleAt(line, untilM).PositionM, EndDiscM, colour);
            if (joinM > underTheCarM && joinM < untilM) draw.DiscM(Spline.SampleAt(line, joinM).PositionM, JoinDiscM, colour);

            // What the book has in front of the car, and where the car must be stopped by — both the
            // follower's own figures rather than this layer's arithmetic.
            var context = cars.Context[car];
            if (float.IsFinite(context.HeadwayM))
            {
                // From the nose, which is where the reading is measured from.
                var seenM = progressM + config.CarNoseAheadOfAxleM + context.HeadwayM;
                draw.RingM(
                    Spline.SampleAt(line, seenM).PositionM, config.Car.WidthM * 0.5f, PathLineM, Theme.HeldLine,
                    segments: 10);
            }

            if (float.IsFinite(context.StopAtM))
            {
                var stopAt = Spline.SampleAt(line, progressM + context.StopAtM);
                draw.LineM(
                    stopAt.PositionM - stopAt.Right * config.Car.WidthM * 0.6f,
                    stopAt.PositionM + stopAt.Right * config.Car.WidthM * 0.6f, PathLineM * 2f, Theme.HeldLine);
            }

        }
    }

    /// <summary>
    /// How far along the line the piece the car is on runs out: the end of the lane it is in, the end of
    /// the junction join it is crossing, or the end of the line where a manoeuvre laid its own geometry —
    /// a bay template, a recovery straight — and there are no lanes under it at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The boundaries are the assembler's own</b> (<see cref="Agents.Car.Body.CarFleet.LaneStartsOf"/>),
    /// which is what keeps this a section of the car's route rather than a second opinion about where a
    /// lane ends. Ascending along the line, the first boundary past the car is the end of what it is
    /// driving now.
    /// </para>
    /// <para>
    /// <paramref name="anchorM"/> is where the piece's <em>own</em> metres begin under the line's, which
    /// is what the marks are counted from. A lane the line was assembled into through a junction begins
    /// a setback short of where the line picks it up (<see cref="RoadGraph.JoinedAtM"/>), so the anchor
    /// is that much behind the cut — and the marks then fall on the same stones the town layer draws them
    /// on, whichever car is driving and wherever its own line happens to have started.
    /// </para>
    /// </remarks>
    static float PieceEndM(
        Agents.Car.Body.CarFleet cars, RoadGraph roads, int car, float atM, float totalM, out float anchorM)
    {
        anchorM = 0f;
        var lanes = cars.Line[car].LaneCount;
        if (lanes == 0) return totalM;

        var chain = cars.ChainOf(car);
        var starts = cars.LaneStartsOf(car);
        var ends = cars.LaneEndsOf(car);
        for (var slot = 0; slot < lanes; slot++)
        {
            // Between the last lane's end and this one's start is the join across the box, whose own
            // metres begin where the lane before it left off.
            if (starts[slot] > atM) return starts[slot];

            anchorM = starts[slot] - (slot == 0 ? 0f : roads.JoinedAtM(chain[slot]));
            if (ends[slot] > atM) return ends[slot];

            anchorM = ends[slot];
        }

        return totalM;
    }

    /// <summary>
    /// What a car is doing, in the words its own controller uses — the same words the run's own read-out
    /// writes.
    /// </summary>
    static ReadOnlySpan<char> CarName(Agents.Car.Body.CarFleet cars, int car) =>
        Agents.Car.Control.DrivingWords.CarName(cars, car);

    /// <summary>
    /// The one thing a layer writes rather than draws: what the body under it is doing. Text carries
    /// only what geometry cannot, and a state has no shape. Dropped below a framing at which the body
    /// is a few pixels across, where a label is a bar of unreadable text over the thing it names.
    /// </summary>
    static void Label(
        ref ScreenDraw draw, Vector2 atM, scoped ReadOnlySpan<char> text, Vector2 viewCentreM, Vector2 viewSpanM,
        float pixelsPerMetre)
    {
        if (pixelsPerMetre < LabelPixelsPerMetre) return;

        const float PaddingPx = 3f;
        var uiPx = viewSpanM * pixelsPerMetre;
        var atPx = ((atM - viewCentreM) * pixelsPerMetre) + (uiPx * 0.5f);
        var sizePx = new Vector2(
            GlyphSheet.WidthPx(text.Length, Theme.SmallTextPx) + (PaddingPx * 2f), Theme.SmallTextPx + (PaddingPx * 2f));
        var box = new Rect(atPx - new Vector2(sizePx.X * 0.5f, sizePx.Y + (Theme.SmallTextPx * 0.8f)), sizePx);
        Theme.Frame(ref draw, box);
        draw.Text(box.AtPx + new Vector2(PaddingPx), text, Theme.SmallTextPx, Theme.Text);
    }
}
