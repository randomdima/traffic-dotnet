using System.Numerics;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// <b>The pass an agent is on or has asked for</b> (CAR-46, PER-28), drawn with the rest of its line: the
/// manoeuvre it is executing, with every place that manoeuvre owns (OBS-2d).
/// </summary>
internal sealed partial class DebugOverlay
{
    /// <summary>
    /// <b>A car's pass</b>: the line it aims the rear axle along, from where the step out begins to where it is back
    /// in its lane, and — while the car is on a step — the circle the pass bends round where the car is.
    /// </summary>
    /// <remarks>
    /// <b>The circle is the reading</b>: its centre and the nearest rear wheel's ring, drawn as the car's own turn
    /// circle is (OBS-2j), so a car driving the pass it holds the ground of has the two laid one over the other,
    /// and one off it has them apart.
    /// </remarks>
    static void CarPass(ref ScreenDraw draw, TownWorld world, int car, float widthM, float hairlineM, Vector4 colour)
    {
        var pass = world.Cars.Pass[car];
        if (!pass.Any) return;

        var line = world.Cars.LineOf(car);
        pass.PoseAtM(line, pass.OutM, out var fromM, out _);
        for (var atM = pass.OutM; atM < pass.EndsM;)
        {
            atM = MathF.Min(atM + PassDrawnStepM, pass.EndsM);
            pass.PoseAtM(line, atM, out var toM, out _);
            draw.LineM(fromM, toM, widthM, colour);
            fromM = toM;
        }

        var progressM = world.Cars.ProgressM[car];
        if (!pass.Begun || progressM <= pass.OutM || progressM >= pass.EndsM) return;

        var bend = pass.BendAtM(line, progressM);
        var radiusM = 1f / MathF.Abs(bend);
        if (radiusM > TurnCircle.WidestM) return;

        pass.PoseAtM(line, progressM, out var axleM, out var forward);
        var centreM = axleM + (Heading.RightOf(forward) * (1f / bend));
        var wheelM = radiusM - world.Cars.BuildOf(car).HalfTrackM;
        draw.DiscM(centreM, PathMarks.JoinDiscM, colour);
        draw.RingM(centreM, wheelM, hairlineM, colour, SegmentsFor(wheelM));
    }

    /// <summary>
    /// <b>A walker's pass</b>: what is left of it, as the places it walks through — across onto the lane beside, down
    /// it and back onto its route (<see cref="TownWorld.SidestepPathM"/>).
    /// </summary>
    static void WalkerSidestep(ref ScreenDraw draw, TownWorld world, int person, float widthM, Vector4 colour)
    {
        Span<Vector2> path = stackalloc Vector2[MostSidestepPlaces];
        var count = world.SidestepPathM(person, path);
        for (var at = 1; at < count; at++)
        {
            draw.LineM(path[at - 1], path[at], widthM, colour);
            draw.DiscM(path[at], PathMarks.JoinDiscM, colour);
        }
    }

    /// <summary>
    /// How far apart a pass is sampled for the picture: fine enough that the drawn arc lies on the ring drawn round
    /// it at any framing the rings can be read at.
    /// </summary>
    const float PassDrawnStepM = 0.1f;

    /// <summary>The most places a walker's pass is drawn through — a bound on a stack span, and a pass longer than this is drawn short.</summary>
    const int MostSidestepPlaces = 48;
}
