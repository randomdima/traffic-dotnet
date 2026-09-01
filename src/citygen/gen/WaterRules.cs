using System.Numerics;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>What the water lets a road do</b> (GEN-14). Every road the town lays is asked, so the rules hold by
/// construction rather than by a sweep that finds a lane in the river afterwards.
/// </summary>
/// <remarks>
/// <para>
/// <b>The question is asked of the carriageway and not of the centreline.</b> A road whose middle runs a
/// metre inside the bank still has its far kerb and its pavement over the water, and the ground under them
/// is painted road exactly as the middle is — so what is walked is the chord and the two edges either side
/// of it.
/// </para>
/// <para>
/// <b>A chord is walked, and a road is a chain.</b> The two are the same thing for everything that may span
/// water, because a bridge is straight (GEN-14a) and a spoke is a ray; the orbital's own arc stands off its
/// chord by less than the edges this walks, which is why the arc is not sampled here.
/// </para>
/// </remarks>
internal sealed class WaterRules(
    GenRaster raster, TerrainStage.Water water, float longestDeckM, float abutmentM, float halfWidthM)
{
    public TerrainStage.Water Water => water;

    /// <summary>How far back from the bank a bridgehead stands: the ground its own junction takes.</summary>
    public float AbutmentM => abutmentM;

    /// <summary>How finely the water is walked, which is how finely it was cut.</summary>
    public float StepM => raster.CellSizeM;

    public bool Wet(Vector2 atM) => raster.At(atM) == Ground.Water;

    /// <summary>Whether the water stands anywhere under a road laid on this chord.</summary>
    public bool Wets(Vector2 fromM, Vector2 toM)
    {
        var runM = (toM - fromM).Length();
        if (runM <= 0f) return Wet(fromM);

        var sideM = Heading.RightOf((toM - fromM) / runM) * halfWidthM;
        var steps = Math.Max(2, (int)(runM / raster.CellSizeM));
        for (var step = 0; step <= steps; step++)
        {
            var atM = Vector2.Lerp(fromM, toM, step / (float)steps);
            if (Wet(atM) || Wet(atM + sideM) || Wet(atM - sideM)) return true;
        }

        return false;
    }

    /// <summary>
    /// Whether a road standing over the water may be laid at all: over a river rather than the sea, and no
    /// longer than the deck a town builds (GEN-14a).
    /// </summary>
    public bool Spans(Vector2 fromM, Vector2 toM) =>
        water.Bridgeable && (toM - fromM).Length() <= longestDeckM;

    /// <summary>
    /// Whether a road of this class may be laid between two nodes at all. <b>Dry ground takes anything but a
    /// bridge, and water takes nothing else</b> — which is the whole of GEN-14a, asked once of every road in
    /// the town.
    /// </summary>
    public bool Carries(Vector2 fromM, Vector2 toM, RoadClass roadClass) =>
        Wets(fromM, toM)
            ? roadClass == RoadClass.Bridge && Spans(fromM, toM)
            : roadClass != RoadClass.Bridge;
}
