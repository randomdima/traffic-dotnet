using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Runtime;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Debug;

/// <summary>What is drawn over one agent: its line, and the chevrons along it. <b>No words</b> — what a body is doing is the inspector's card (OBS-2t).</summary>
internal sealed partial class DebugOverlay
{
    /// <summary>
    /// <b>An agent's line is held to this on the glass</b>, however far out the camera is: under about a
    /// pixel and a half a route is a dotted thread over the tarmac, and the one thing this layer is read for is
    /// following it.
    /// </summary>
    const float AgentLineFloorPx = 1.5f;

    static float AgentLineM(float pixelsPerMetre) => MathF.Max(PathMarks.PathLineM, AgentLineFloorPx / pixelsPerMetre);

    /// <summary>
    /// The walkers: the line each is actually holding, drawn ahead of it as chevrons, and the place
    /// it is holding it to.
    /// </summary>
    /// <remarks>
    /// The line runs from the body and not from where the leg started, so the picture answers "where is
    /// it going". What is drawn is the walk that is left and not the point in hand: a picture of the aim
    /// point alone says a walker is heading into a building whenever the pavement bends round one, and
    /// the swerves and cut corners this layer exists to show are all in the run <em>after</em> it.
    /// </remarks>
    static void WalkerLines(
        ref ScreenDraw draw, TownWorld world, SimConfig config, Vector2 viewCentreM, Vector2 viewSpanM,
        float pixelsPerMetre)
    {
        var pitchM = PathMarks.MarkPitchAt(pixelsPerMetre);
        var widthM = AgentLineM(pixelsPerMetre);
        var people = world.People;
        for (var person = 0; person < people.Count; person++)
        {
            if (!OnScreen(people.PositionM[person], viewCentreM, viewSpanM, config.PersonDiameterM)) continue;

            WalkerRoute(ref draw, world, person, pitchM, widthM, Theme.AgentLine(person));
        }
    }

    /// <summary>
    /// <b>OBS-2h for a walker</b>, which is OBS-2h for a car in the walking network's own words: the way it is
    /// walking and the one that way leads onto, and the dot between them is where it hands over — laid on a
    /// dark casing first, so it reads over tarmac and grass alike.
    /// </summary>
    /// <remarks>
    /// A walker holds a route as ways and is held on each way's own arc, so what is drawn is sampled off that
    /// arc rather than read out of the body.
    /// </remarks>
    static void WalkerRoute(
        ref ScreenDraw draw, TownWorld world, int person, float pitchM, float widthM, Vector4 colour)
    {
        if (!world.People.Walking[person]) return;

        WalkerPass(ref draw, world, person, float.PositiveInfinity, widthM * PathMarks.CasingWidthFactor, Theme.Casing);
        WalkerPass(ref draw, world, person, pitchM, widthM, colour);
    }

    static void WalkerPass(
        ref ScreenDraw draw, TownWorld world, int person, float pitchM, float widthM, Vector4 colour)
    {
        var people = world.People;
        var route = people.RouteOf(person);
        var at = people.RouteAt(person);
        var count = people.RouteCount[person];
        if (at < 0 || at >= count) return;

        var walking = world.Walking;
        var discs = widthM / PathMarks.PathLineM;
        var fromM = people.PositionM[person];
        draw.DiscM(fromM, PathMarks.EndDiscM * discs, colour);

        for (var slot = at; slot < count && slot < at + StretchesDrawn; slot++)
        {
            var way = route[slot];
            walking.SpanOfWay(
                slot > 0 ? route[slot - 1] : WalkingNetwork.NoLane, way,
                slot + 1 < count ? route[slot + 1] : WalkingNetwork.NoLane, out var startM, out var endM);

            if (slot == count - 1) endM = MathF.Min(endM, people.RouteToM[person]);
            if (slot == at) startM = MathF.Max(startM, people.OnWayM[person]);
            if (slot > at) draw.DiscM(fromM, PathMarks.JoinDiscM * discs, colour);

            fromM = Chevroned(ref draw, walking.WayArcs(way), startM, endM, fromM, pitchM, widthM, colour);
        }

        draw.DiscM(fromM, PathMarks.EndDiscM * discs, colour);
    }

    /// <summary>
    /// Chevrons along one way's own arc between two distances, and answers where it got to. <b>Sampled for
    /// the picture</b>: a screen draws straights and the ground is arcs, which is the one place that
    /// difference belongs.
    /// </summary>
    static Vector2 Chevroned(
        ref ScreenDraw draw, ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, Vector2 atM, float pitchM,
        float widthM, Vector4 colour)
    {
        if (arcs.Length == 0) return atM;

        for (var alongM = fromM; alongM < toM;)
        {
            alongM = MathF.Min(alongM + DrawnStepM, toM);
            var pointM = Spline.SampleAt(arcs, alongM).PositionM;
            PathMarks.Chevroned(ref draw, atM, pointM, pitchM, colour, widthM);
            atM = pointM;
        }

        return atM;
    }

    /// <summary>
    /// How far apart a drawn way is sampled, which is a picture's tolerance and not the town's — <b>the
    /// same step the interface stations a whole walk at</b> (<see cref="WalkedLine.StepM"/>), because it is
    /// the same ground converted from arcs to straights for the same reason.
    /// </summary>
    const float DrawnStepM = WalkedLine.StepM;

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
    /// (<see cref="Agents.Car.Body.CarFleet.Context"/>): a layer asking the claims itself would be drawing
    /// a second opinion beside the car, and one that agreed would be the more misleading of the two.
    /// </para>
    /// <para>
    /// <b>The steering aim point is deliberately not drawn.</b> Everything else this layer marks is
    /// something the world did to the car — ground somebody has taken, a body in front, a place it is held
    /// at — and a ring standing out on the line among them reads as a thing the car <em>found</em> there.
    /// It is not: it is where the wheel is pointed (<see cref="Agents.Car.Control.CarFollower.Steer"/>),
    /// an output and not a reading, and it is a time ahead of the body rather than a place, so on a bend
    /// it sits off the car's own line and looks like a detection that has drifted. What a driver can see
    /// is the claims, and they are drawn as the ground they are.
    /// </para>
    /// </remarks>
    static void CarLines(
        ref ScreenDraw draw, TownWorld world, SimConfig config, Vector2 viewCentreM, Vector2 viewSpanM,
        float pixelsPerMetre)
    {
        var pitchM = PathMarks.MarkPitchAt(pixelsPerMetre);
        var widthM = AgentLineM(pixelsPerMetre);
        var sagM = PathMarks.SagPx / pixelsPerMetre;
        var cars = world.Cars;
        for (var car = 0; car < cars.Count; car++)
        {
            if (!OnScreen(cars.PositionM[car], viewCentreM, viewSpanM, cars.BuildOf(car).LengthM)) continue;

            CarRoute(ref draw, world, car, pitchM, sagM, widthM, Theme.AgentLine(car));
        }
    }

    /// <summary>
    /// One car's two pieces of route on a dark casing, and what it was told about the road ahead of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every figure is the car's own</b> (CAR-11), so a layer drawn over a truck is drawn at the truck's
    /// dimensions and reports what the truck was told.
    /// </para>
    /// <para>
    /// The line starts under the middle of the body and not at the end the route was planned from: the ground
    /// already covered is where the car has been, and a layer drawing it is answering a question about the
    /// past. The progress itself is the rear axle's, which is a car's length of line behind the body it
    /// belongs to. Line and marks are the one colour, because they are the one line — what tells this car's
    /// route from the next car's is the colour it is drawn in.
    /// </para>
    /// </remarks>
    static void CarRoute(
        ref ScreenDraw draw, TownWorld world, int car, float pitchM, float sagM, float widthM, Vector4 colour)
    {
        var cars = world.Cars;
        var line = cars.LineOf(car);
        if (line.Length == 0) return;

        ref readonly var build = ref cars.BuildOf(car);
        var totalM = cars.Line[car].LengthM;
        var progressM = Math.Clamp(cars.ProgressM[car], 0f, totalM);
        var underTheCarM = MathF.Min(progressM + build.CentreAheadOfAxleM, totalM);

        // The piece being driven and the piece it leads onto: the rest of this lane and the junction off the
        // end of it, or the junction being crossed and the lane it lands on. Both are the one chain the
        // assembler wove, so they are drawn as one run of line and the dot between them is where the car
        // changes what it is doing.
        var joinM = PieceEndM(cars, car, underTheCarM, totalM);
        var untilM = PieceEndM(cars, car, joinM, totalM);
        var discs = widthM / PathMarks.PathLineM;

        PathMarks.Casing(ref draw, line, underTheCarM, untilM, sagM, widthM);
        PathMarks.Chained(
            ref draw, line, underTheCarM, joinM, pitchM, bothWays: false, sagM, colour, MarkClaims.None, widthM);
        PathMarks.Chained(
            ref draw, line, joinM, untilM, pitchM, bothWays: false, sagM, colour, MarkClaims.None, widthM);

        draw.DiscM(Spline.SampleAt(line, underTheCarM).PositionM, PathMarks.EndDiscM * discs, colour);
        draw.DiscM(Spline.SampleAt(line, untilM).PositionM, PathMarks.EndDiscM * discs, colour);
        if (joinM > underTheCarM && joinM < untilM)
        {
            draw.DiscM(Spline.SampleAt(line, joinM).PositionM, PathMarks.JoinDiscM * discs, colour);
        }

        // What is claimed in front of the car, and where the car must be stopped by — both the follower's own
        // figures rather than this layer's arithmetic.
        var context = cars.Context[car];
        if (float.IsFinite(context.HeadwayM))
        {
            // From the nose, which is where the reading is measured from.
            var seenM = progressM + build.NoseAheadOfAxleM + context.HeadwayM;
            draw.RingM(Spline.SampleAt(line, seenM).PositionM, build.FlankM, widthM, Theme.HeldLine, segments: 10);
        }

        if (float.IsFinite(context.StopAtM))
        {
            var stopAt = Spline.SampleAt(line, progressM + context.StopAtM);
            draw.LineM(
                stopAt.PositionM - stopAt.Right * build.WidthM * 0.6f,
                stopAt.PositionM + stopAt.Right * build.WidthM * 0.6f, widthM * 2f, Theme.HeldLine);
        }
    }

    /// <summary>
    /// How far along the line the piece the car is on runs out: the end of the lane it is in, the end of
    /// the junction join it is crossing, or the end of the line where a manoeuvre laid its own geometry —
    /// a bay template, a recovery straight — and there are no lanes under it at all.
    /// </summary>
    /// <remarks>
    /// <b>The boundaries are the assembler's own</b> (<see cref="Agents.Car.Body.CarFleet.LaneStartsOf"/>),
    /// which is what keeps this a section of the car's route rather than a second opinion about where a
    /// lane ends. Ascending along the line, the first boundary past the car is the end of what it is
    /// driving now. Between one lane's end and the next one's start is the join across the box.
    /// </remarks>
    static float PieceEndM(Agents.Car.Body.CarFleet cars, int car, float atM, float totalM)
    {
        var lanes = cars.Line[car].LaneCount;
        var starts = cars.LaneStartsOf(car);
        var ends = cars.LaneEndsOf(car);
        for (var slot = 0; slot < lanes; slot++)
        {
            if (starts[slot] > atM) return starts[slot];
            if (ends[slot] > atM) return ends[slot];
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
    /// <b>What a held wheel is asking for, in the terms it was asked in</b>: how much of this car's own
    /// lock is wound on and which way, and how much of its own pedal is down and in which gear. It is read
    /// off the command rather than off whatever set it, so it says the same thing about a car under a
    /// second driver's hand and about one under a player's (CTL-5d).
    /// </summary>
    /// <remarks>
    /// <b>Shares of the car's own figures and not the figures themselves</b> (CAR-11): "half the pedal" is
    /// the same instruction to a supercar and to a truck, and the m/s² each of them makes of it is the
    /// difference a reader is looking for rather than something to write over the body.
    /// </remarks>
    static void WheelWords(in DriveCommand command, in CarBuild build, ref TextBuffer into)
    {
        var lock100 = build.MaxSteerRad > 0f ? MathF.Abs(command.SteerRad) / build.MaxSteerRad * 100f : 0f;
        if (lock100 >= OnItsStopPercent) into.Add("full lock ");
        else
        {
            into.Add(lock100, "F0");
            into.Add("% lock ");
        }

        into.Add(command.SteerRad < 0f ? "left, " : "right, ");

        var pedalMps2 = command.ThrottleMps2 > 0f ? command.ThrottleMps2 : -command.BrakeMps2;
        if (pedalMps2 == 0f)
        {
            into.Add("coasting ");
        }
        else if (pedalMps2 < 0f)
        {
            into.Add("braking ");
        }
        else
        {
            into.Add(pedalMps2 / build.AccelerationMps2 * 100f, "F0");
            into.Add("% pedal ");
        }

        into.Add(command.Reverse ? "astern" : "ahead");
    }

    /// <summary>Where the rack counts as arrived, so a wheel a hair off its stop still reads as full lock.</summary>
    const float OnItsStopPercent = 99f;

}
