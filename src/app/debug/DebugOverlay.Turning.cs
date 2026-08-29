using System.Numerics;
using TrafficSimulation.App.Screen;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Debug;

/// <summary>The circle a car's steering asks for, drawn where the tyres are answering it.</summary>
internal sealed partial class DebugOverlay
{
    /// <summary>
    /// <b>OBS-2j — every car's turn circle, worked out from its axles rather than read off it.</b> The
    /// centre where the axles cross, the spokes that are the construction of it, and the arc the nearest
    /// rear wheel is being asked to trace.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The spokes are the rule and not decoration.</b> A ring on its own is a claim a reader has to take
    /// on trust; the lines from the centre to the patches show where it came from — one runs the length of
    /// the rear axle, through both of its wheels, because that is what "the centre is on the rear axle"
    /// means, and the other two stand square to the front wheels the sprite is drawn at. Ackermann is
    /// visible in the picture: where the geometry is right, both front spokes end on the same point.
    /// </para>
    /// <para>
    /// <b>The arc is drawn to the nearest rear wheel</b> and not to the middle of the axle, because that is
    /// the wheel whose track is on the ground beside it: the comparison this layer exists for is a drawn
    /// circle against a written one, and two circles that are half a track apart cannot be laid over each
    /// other by eye.
    /// </para>
    /// <para>
    /// <b>Nothing is drawn for a car whose wheels are straight</b> (<see cref="TurnCircle.WidestM"/>): the
    /// centre is then a kilometre off the side of the road and the arc through the car is a line, so the
    /// layer would draw a strip across the town for every car in it that happened to be going straight.
    /// </para>
    /// </remarks>
    static void TurnCircles(
        ref ScreenDraw draw, TownWorld world, Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre)
    {
        var lineM = MathF.Max(TurnLineM, TurnLineFloorPx / pixelsPerMetre);
        var cars = world.Cars;
        for (var car = 0; car < cars.Count; car++)
        {
            ref readonly var build = ref cars.BuildOf(car);
            var atM = cars.PositionM[car];

            // OBS-2b's cull, and the body's own reach rather than the circle's: what is drawn here is
            // about the car, so a car that is not on screen draws nothing however far its arc would reach.
            if (!OnScreen(atM, viewCentreM, viewSpanM, build.LengthM)) continue;

            if (!TurnCircle.Of(build, atM, cars.HeadingRad[car], cars.Command[car].SteerRad, out var turn)) continue;

            var colour = Theme.TurnCircle;
            draw.LineM(turn.CentreM, turn.RearOuterM, lineM, colour);
            draw.LineM(turn.CentreM, turn.FrontInnerM, lineM, colour);
            draw.LineM(turn.CentreM, turn.FrontOuterM, lineM, colour);
            draw.DiscM(turn.CentreM, PathMarks.JoinDiscM, colour);
            draw.RingM(turn.CentreM, turn.RadiusM, lineM, colour, SegmentsFor(turn.RadiusM));
        }
    }

    /// <summary>
    /// How many sides the arc is cut into: enough that the biggest circle on screen reads as a circle
    /// rather than as a polygon, and bounded so that a pad of a hundred cars is not a frame of rings.
    /// </summary>
    static int SegmentsFor(float radiusM) =>
        Math.Clamp((int)(radiusM * SegmentsPerMetre), LeastSegments, MostSegments);

    const float SegmentsPerMetre = 3f;

    const int LeastSegments = 16;

    const int MostSegments = 48;

    /// <summary>
    /// <b>A hairline, on the collision layer's terms</b> (<see cref="CollisionLineM"/>): the whole reading
    /// is where this circle falls against the track written under it, and a stroke wide enough to see from
    /// across the town is a stroke wide enough to hide the answer.
    /// </summary>
    const float TurnLineM = 0.06f;

    /// <summary>Held to this on screen however far out the camera is, since a line thinner than a pixel is a dotted one.</summary>
    const float TurnLineFloorPx = 1.5f;
}
