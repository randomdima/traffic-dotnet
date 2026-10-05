using System.Numerics;
using TrafficSimulation.App.Screen;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Debug;

internal sealed partial class DebugOverlay
{
    /// <summary>
    /// <b>The driven ground itself, as the ribbons it is merged out of</b> (OBS-2s): every lane, movement
    /// and bay drawn whole, at that line's own width — the area the boundary beside it
    /// (<see cref="Perimeter"/>) is the outside of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The merge's input rather than its answer</b>, which is what makes the pair worth having: the two
    /// layers are the same shape drawn twice by different arithmetic, so a boundary that does not follow the
    /// outside of the bands under it is a merge fault — and a picture of the boundary alone cannot show one,
    /// the line being as continuous where it is wrong as where it is right.
    /// </para>
    /// <para>
    /// <b>The band is the ribbon and not a drawing of it.</b> A ribbon is the line's own curve offset half a
    /// width either side and cut square at both ends (<see cref="ArcRibbon"/>), and that is exactly the
    /// shape a band down the line at the full width is (<see cref="PathMarks.Banded"/>) — so nothing here
    /// lays a second copy of the geometry to draw the first one with.
    /// </para>
    /// <para>
    /// <b>Nothing merges them here either.</b> They are drawn over one another at a wash, so ground two
    /// ribbons both cover comes out deeper — which is the reading at a junction, where what the boundary
    /// runs along is whichever band reaches past the rest.
    /// </para>
    /// <para>
    /// <b>A line on the level above is drawn into <paramref name="above"/></b> (<see cref="Paving.DrivenLevel"/>), over
    /// the deck it is the ribbon of.
    /// </para>
    /// </remarks>
    static void Ribbons(
        ref ScreenDraw draw, ref ScreenDraw above, TownWorld world, SimConfig config, Vector2 viewCentreM,
        Vector2 viewSpanM, float pixelsPerMetre)
    {
        var sagM = PathMarks.SagPx / pixelsPerMetre;
        var paving = world.Plan.Paving(config);

        for (var line = 0; line < paving.DrivenCount; line++)
        {
            var arcs = paving.ArcsOfDriven(line);
            if (arcs.Length == 0) continue;

            var lengthM = paving.DrivenLengthM(line);
            var headM = arcs[0].StartM;
            var tailM = arcs[^1].EndM;
            if (!OnScreen((headM + tailM) * 0.5f, viewCentreM, viewSpanM, lengthM * 0.5f)) continue;

            ref var into = ref paving.DrivenLevel(line) != CityPlan.RoadArrays.Ground ? ref above : ref draw;
            PathMarks.Banded(ref into, arcs, 0f, lengthM, sagM, paving.DrivenWidthM(line), Theme.Ribbon);
        }
    }
}
