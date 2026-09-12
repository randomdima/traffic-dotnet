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
    /// <b>The outside of the town's driven ground, as the unbroken lines it is</b> (OBS-2p): the outer
    /// boundary of the union of the bands every lane, movement and bay way lays (<see cref="LaneShell"/>),
    /// which on a plain street is its outer lanes and at a junction is whichever movements reach past the
    /// rest.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Read off the town and not worked out here</b>, which is the rule the rest of this slice keeps: the
    /// answer belongs to the ground rather than to the picture, and the pavement is meant to be laid off the
    /// same one. What the layer adds is the stroke.
    /// </para>
    /// <para>
    /// <b>One solid line per ring, and nothing on it.</b> A perimeter is continuous and the reading taken
    /// from it is <em>where it is not</em>, so anything drawn along the line itself could be mistaken for a
    /// break in it. What stands beside it is another matter: a barb every few metres, square off the line and
    /// to the side the shell believes the town is on (<see cref="PathMarks.Barbed"/>), leaves the line whole
    /// and answers the question a line alone cannot — <b>which side of it the ring thinks is the ground</b>.
    /// A ring walks with the town on one hand throughout (<see cref="LaneShell"/>), so a run of barbs turned
    /// out at the grass is the walk having got a corner the wrong way round, and the boundary drawn there is
    /// as wrong as it looks however continuous it is.
    /// </para>
    /// <para>
    /// <b>And the town's named lines struck off it, in white</b> (OBS-2q, <see cref="GroundLine"/>) — the
    /// roadside perimeter at half a lane, which is a line the town has rather than a distance chosen to be
    /// looked at. What a second line says that the first cannot is whether the extrusion <em>kept</em> its
    /// distance: white crossing blue, or running inside it through a corner, is the fold rule having let a
    /// lap of the offset stand. <b>A test line at a made-up distance stood here before</b>, five metres out
    /// in red, and it was worth exactly as much as a line nothing is laid at — the reading is the same and
    /// what it is taken of is now real.
    /// </para>
    /// <para>
    /// <b>A dot is a fault, and every one the answer has is drawn.</b> The shell shuts every ring on itself,
    /// so a chain with two ends is one it could not close — and that is a length of the town's edge nothing
    /// accounts for, to be fixed in the ground rather than passed over here.
    /// </para>
    /// </remarks>
    static void Perimeter(
        ref ScreenDraw draw, TownWorld world, SimConfig config, Vector2 viewCentreM, Vector2 viewSpanM,
        float pixelsPerMetre)
    {
        var sagM = PathMarks.SagPx / pixelsPerMetre;
        var barbPitchM = PathMarks.BarbPitchAt(pixelsPerMetre);

        var paving = world.Plan.Paving(config);
        var shell = paving.Boundary(config);

        // Under the perimeter itself, since the reading is taken against that line: where the two touch, the
        // one that is meant to be kept clear of is the one to be able to see. <b>Off the town's own boundary
        // and at the town's own distance</b>, both of which are cached on the paving — so a layer drawn every
        // frame strikes the line once for the life of the town.
        foreach (var ring in shell.Extruded(
            GroundRings.OutM(GroundLine.Roadside, config), GroundRings.SmoothM))
        {
            if (ring.Length == 0) continue;

            var outM = Spline.TotalLengthM(ring);
            if (!OnScreen((ring[0].StartM + ring[^1].EndM) * 0.5f, viewCentreM, viewSpanM, outM * 0.5f)) continue;

            PathMarks.Banded(ref draw, ring, 0f, outM, sagM, PathMarks.PathLineM, Theme.GroundLine);
        }

        foreach (var chain in shell.Chains)
        {
            if (chain.Length == 0) continue;

            var headM = chain[0].StartM;
            var tailM = chain[^1].EndM;
            var lengthM = Spline.TotalLengthM(chain);
            if (!OnScreen((headM + tailM) * 0.5f, viewCentreM, viewSpanM, lengthM * 0.5f)) continue;

            PathMarks.Banded(ref draw, chain, 0f, lengthM, sagM, PathMarks.PathLineM, Theme.Perimeter);

            // A ring is walked with the town's ground on the walker's right (<see cref="LaneShell"/>), so
            // that is the hand the barb is turned to and the picture says whether the walk was right.
            PathMarks.Barbed(
                ref draw, chain, 0f, lengthM, barbPitchM, toTheRight: true, PathMarks.PathLineM,
                Theme.Perimeter);

            if (Vector2.DistanceSquared(headM, tailM) <= Kerbs.RoundingM * Kerbs.RoundingM) continue;

            draw.DiscM(headM, PathMarks.JoinDiscM, Theme.Perimeter);
            draw.DiscM(tailM, PathMarks.JoinDiscM, Theme.Perimeter);
        }
    }
}
