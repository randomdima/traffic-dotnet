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
    /// <b>The layer draws every shell the town's ground is made of, and it draws them alike</b>
    /// (<see cref="Boundaries"/>): the rings the merge closed, the runs it could not
    /// (<see cref="BandShell.Loose"/>), and the layers struck off that boundary that the picture is laid from
    /// (OBS-2u, <see cref="GroundRings"/>). Each is a set of chains walked with its own ground on its right,
    /// so each is a line and the normals beside it, at a colour of its own and nothing else of its own —
    /// <b>what tells them apart is what they are, not how they are drawn</b>. A further outline is a colour
    /// and a line in <see cref="Perimeter"/>.
    /// </para>
    /// <para>
    /// <b>The layers are the town's own and never a second set taken here</b> (<see cref="Paving.Rings"/>).
    /// A layer drawing an offset of its own would be a picture of a shape nobody is standing on, and the
    /// whole of what this layer is opened for is whether the ground the town was laid from is the ground it
    /// looks like.
    /// </para>
    /// <para>
    /// <b>One solid line per chain, and the normals beside it</b> (OBS-2p). The line says where the boundary
    /// is and the normals say which side of it the shape is on — an arrow every few metres, square off the
    /// line and turned to the side the outline claims (<see cref="PathMarks.Bounded"/>). Every chain here
    /// walks with its ground on its right (TER-3c.9), so a run of normals turned out at the grass is a
    /// corner that came out the wrong way round, and the boundary drawn there is as wrong as it looks
    /// however continuous it is. <b>Drawn on the line rather than beside it, the answer could be mistaken
    /// for a break in it</b>, which is the one reading a perimeter is for.
    /// </para>
    /// <para>
    /// <b>What an open run adds is its two ends and not a second way of drawing it.</b> A run with two ends
    /// is a fault — the boundary of a union of closed bands is closed — so it is drawn in the fault colour
    /// with a disc at each end, which is the whole of what a ring has not got.
    /// </para>
    /// <para>
    /// <b>The layers are drawn last, so they are the first thing the cache gives up</b> (OBS-2u): a layer is
    /// read against the boundary it was struck off, and the boundary is what the layer is opened for. The
    /// runs that would not close are drawn before them for the same reason the other way round — a fault
    /// truncated out of the picture is a fault nobody sees.
    /// </para>
    /// </remarks>
    void Perimeter(
        ref ScreenDraw draw, TownWorld world, SimConfig config, Vector2 viewCentreM, Vector2 viewSpanM,
        float pixelsPerMetre)
    {
        var sagM = PathMarks.SagPx / pixelsPerMetre;
        var pitchM = PathMarks.BarbPitchAt(pixelsPerMetre);
        var paving = world.Plan.Paving(config);
        var shell = paving.Perimeter(config);
        var rings = paving.Rings(config);

        Boundaries(
            ref draw, rings.Carriageway.Rings, Theme.Perimeter, false, viewCentreM, viewSpanM, sagM, pitchM);
        Boundaries(ref draw, shell.Loose, Theme.PerimeterLoose, true, viewCentreM, viewSpanM, sagM, pitchM);
        foreach (var layer in rings.Layers)
        {
            Boundaries(
                ref draw, layer.Loose, Theme.PerimeterLoose, true, viewCentreM, viewSpanM, sagM, pitchM);
            Boundaries(
                ref draw, layer.Rings, Theme.PerimeterOutset, false, viewCentreM, viewSpanM, sagM, pitchM);
        }
    }

    /// <summary>
    /// <b>One outline drawn</b>: every stretch of it that reaches the view, as the line it is and the
    /// normals that say which side of it its ground is on — with a disc at either end of each chain where
    /// the chains are runs rather than rings (<paramref name="ends"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Culled by the stretch and never by the chain.</b> A ring is the whole town: its two ends stand at
    /// one place and it reaches half its own length in every direction, so a cull asked of the chain passes
    /// every ring at every framing and the layer draws a city's hundred thousand stretches to show the
    /// dozen that are on the glass. That filled the cache (<see cref="TownQuadCapacity"/>) on the first
    /// outline at any zoom, which read as the layers after it being broken rather than as the budget being
    /// spent — <b>a boundary layer with room for one boundary can only ever draw one</b>.
    /// </para>
    /// <para>
    /// <b>Off its own start, and never off its middle.</b> No point of a piece stands further along it than
    /// its own length, so the start and the length bound it without an arc being walked to find out where
    /// it goes — and the barb is added because a normal stands off the line and may reach the glass from a
    /// stretch that does not.
    /// </para>
    /// </remarks>
    static void Boundaries(
        ref ScreenDraw draw, ReadOnlySpan<ArcSeg[]> chains, Vector4 colour, bool ends, Vector2 viewCentreM,
        Vector2 viewSpanM, float sagM, float pitchM)
    {
        foreach (var chain in chains)
        {
            if (chain.Length == 0) continue;

            foreach (var stretch in chain)
            {
                if (!OnScreen(stretch.StartM, viewCentreM, viewSpanM, stretch.LengthM + PathMarks.BarbM))
                {
                    continue;
                }

                PathMarks.Bounded(
                    ref draw, [stretch], 0f, stretch.LengthM, sagM, pitchM, PathMarks.PathLineM, colour,
                    Theme.PerimeterNormal);
            }

            if (!ends) continue;

            draw.DiscM(chain[0].StartM, PathMarks.JoinDiscM, colour);
            draw.DiscM(chain[^1].EndM, PathMarks.JoinDiscM, colour);
        }
    }

}
