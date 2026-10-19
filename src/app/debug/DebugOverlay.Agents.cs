using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Foot;
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
    /// The walkers' own manoeuvre: the line each steps out along to pass something, and back. Where a walker is going is
    /// the agent lines' (<see cref="EngineLayers"/>), stated by its driver like every other agent's.
    /// </summary>
    static void WalkerLines(
        ref ScreenDraw draw, TownWorld world, SimConfig config, Vector2 viewCentreM, Vector2 viewSpanM,
        float pixelsPerMetre)
    {
        var widthM = AgentLineM(pixelsPerMetre);
        var people = world.People;
        for (var person = 0; person < people.Count; person++)
        {
            if (!OnScreen(people.PositionM[person], viewCentreM, viewSpanM, config.PersonDiameterM)) continue;

            WalkerSidestep(ref draw, world, person, widthM, Theme.AgentLine(person));
        }
    }

    /// <summary>
    /// The cars' own manoeuvres: what each found ahead of it and where it must be stopped by, its pass, and the pieces of
    /// a manoeuvre it laid for itself at a bay. Where a car is going is the agent lines' (<see cref="EngineLayers"/>).
    /// </summary>
    /// <remarks>
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
        var widthM = AgentLineM(pixelsPerMetre);
        var hairlineM = MathF.Max(TurnLineM, TurnLineFloorPx / pixelsPerMetre);
        var cars = world.Cars;
        for (var car = 0; car < cars.Count; car++)
        {
            if (!OnScreen(cars.PositionM[car], viewCentreM, viewSpanM, cars.BuildOf(car).LengthM)) continue;

            CarHeld(ref draw, world, car, widthM);
            CarPass(ref draw, world, car, widthM, hairlineM, Theme.AgentLine(car));
        }

        BayApproaches(
            ref draw, world, PathMarks.SagPx / pixelsPerMetre, PathMarks.MarkPitchAt(config.Grid, pixelsPerMetre), viewCentreM,
            viewSpanM);
    }

    /// <summary>
    /// <b>What one car was told about the road ahead of it</b>: where the road it was granted ends, and where a light holds
    /// its road — both the town's own figures rather than this layer's arithmetic, at the car's own (CAR-11).
    /// </summary>
    static void CarHeld(ref ScreenDraw draw, TownWorld world, int car, float widthM)
    {
        var cars = world.Cars;
        var line = cars.LineOf(car);
        if (line.Length == 0) return;

        ref readonly var build = ref cars.BuildOf(car);
        var progressM = Math.Clamp(cars.ProgressM[car], 0f, cars.Line[car].LengthM);
        var context = cars.Context[car];
        if (float.IsFinite(context.AuthorityM))
        {
            // From the nose, which is where the grant is measured from.
            var grantedM = progressM + build.NoseAheadOfAxleM + MathF.Max(0f, context.AuthorityM);
            draw.RingM(Spline.SampleAt(line, grantedM).PositionM, build.FlankM, widthM, Theme.HeldLine, segments: 10);
        }

        // Where a light holds the road in front of it (TLT-1), from the nose like the reading above.
        if (world.LightAheadM(car) is var lightAheadM && float.IsFinite(lightAheadM))
        {
            var stopAt = Spline.SampleAt(line, progressM + build.NoseAheadOfAxleM + lightAheadM);
            draw.LineM(
                stopAt.PositionM - stopAt.Right * build.WidthM * 0.6f,
                stopAt.PositionM + stopAt.Right * build.WidthM * 0.6f, widthM * 2f, Theme.HeldLine);
        }
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
