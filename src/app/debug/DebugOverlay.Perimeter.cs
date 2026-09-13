using System.Numerics;
using TrafficSimulation.App.Screen;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Debug;

internal sealed partial class DebugOverlay
{
    /// <summary>
    /// <b>The outside of the town's driven ground, as the merge of the ribbons its lines lay</b> (OBS-2p):
    /// every lane, movement and bay way taken as the band of ground it covers, all of those merged into one
    /// shape, and the boundary of that shape drawn (<see cref="LaneShell"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Read off the town and not worked out here</b>, which is the rule the rest of this slice keeps: the
    /// merge belongs to the ground rather than to the picture. What the layer adds is the stroke and the
    /// normals.
    /// </para>
    /// <para>
    /// <b>One solid line per ring, and the normals beside it</b> (OBS-2p). The line says where the boundary
    /// is and the normals say which side of it the shape is on — an arrow every few metres, square off the
    /// line and turned to the side the merge believes is ground (<see cref="PathMarks.Normals"/>). A ring
    /// walks with the driven ground on its right throughout (TER-3c.9), so a run of normals turned out at
    /// the grass is the merge having got a corner the wrong way round, and the boundary drawn there is as
    /// wrong as it looks however continuous it is. <b>Drawn on the line rather than beside it, the answer
    /// could be mistaken for a break in it</b>, which is the one reading a perimeter is for.
    /// </para>
    /// <para>
    /// <b>A run with two ends is a fault, and every one the merge has is drawn</b>
    /// (<see cref="LaneShell.Loose"/>) — in the fault colour, because the boundary of a union of closed
    /// bands is closed and a run that does not shut is a crossing that was missed rather than a shape the
    /// town has.
    /// </para>
    /// </remarks>
    static void Perimeter(
        ref ScreenDraw draw, TownWorld world, SimConfig config, Vector2 viewCentreM, Vector2 viewSpanM,
        float pixelsPerMetre)
    {
        var sagM = PathMarks.SagPx / pixelsPerMetre;
        var barbPitchM = PathMarks.BarbPitchAt(pixelsPerMetre);
        var shell = world.Plan.Paving(config).Perimeter(config);

        foreach (var ring in shell.Chains)
        {
            if (!Drawn(ring, viewCentreM, viewSpanM, out var lengthM)) continue;

            PathMarks.Banded(ref draw, ring, 0f, lengthM, sagM, PathMarks.PathLineM, Theme.Perimeter);

            // A ring is walked with the town's ground on the walker's right (TER-3c.9), so that is the hand
            // the normal is turned to and the picture says whether the merge came out that way.
            PathMarks.Normals(
                ref draw, ring, 0f, lengthM, barbPitchM, toTheRight: true, PathMarks.PathLineM,
                Theme.PerimeterNormal);
        }

        foreach (var run in shell.Loose)
        {
            if (!Drawn(run, viewCentreM, viewSpanM, out var lengthM)) continue;

            PathMarks.Banded(ref draw, run, 0f, lengthM, sagM, PathMarks.PathLineM, Theme.PerimeterLoose);
            draw.DiscM(run[0].StartM, PathMarks.JoinDiscM, Theme.PerimeterLoose);
            draw.DiscM(run[^1].EndM, PathMarks.JoinDiscM, Theme.PerimeterLoose);
        }
    }

    /// <summary>Whether one chain is worth walking at all: it has pieces, and it reaches the view.</summary>
    static bool Drawn(ReadOnlySpan<ArcSeg> chain, Vector2 viewCentreM, Vector2 viewSpanM, out float lengthM)
    {
        lengthM = 0f;
        if (chain.Length == 0) return false;

        lengthM = Spline.TotalLengthM(chain);
        return OnScreen((chain[0].StartM + chain[^1].EndM) * 0.5f, viewCentreM, viewSpanM, lengthM * 0.5f);
    }
}
