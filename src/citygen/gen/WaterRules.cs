using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>What the water and the railway let a road do</b> (GEN-14, RAIL-5). Every road the town lays is asked, so the
/// rules hold by construction rather than by a sweep that finds a lane in the river afterwards.
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
/// <para>
/// <b>The railway is crossed and never stood near</b> (RAIL-5): a junction keeps its own ground and a clearance off the
/// ballast, and a road meets the ballast square enough to be a level crossing — which a road running along it, or
/// slanting over it, is not.
/// </para>
/// </remarks>
/// <param name="bridgeable">Whether the water is a river, which a road may span, rather than a sea or a lake (GEN-14b).</param>
/// <param name="railClearM">How far a junction's node stands off the ballast at the least: its own ground and the clearance.</param>
/// <param name="squareLeast">How square to a track a road meets it at the least, as the sine of the angle between them.</param>
internal sealed class WaterRules(
    GroundShapes ground, float stepM, bool bridgeable, float longestDeckM, float abutmentM,
    float halfWidthM, CityPlan.RailArrays rails, float railClearM, float squareLeast)
{
    /// <summary>How far back from the bank a bridgehead stands: the ground its own junction takes.</summary>
    public float AbutmentM => abutmentM;

    /// <summary>How finely the water is walked.</summary>
    public float StepM => stepM;

    public bool Wet(Vector2 atM) => ground.At(atM) == Ground.Water;

    /// <summary>Whether a junction may stand here at all: off the water, and clear of the railway (GEN-14, RAIL-5).</summary>
    public bool Holds(Vector2 atM) => !Wet(atM) && !ground.RailWithin(atM, railClearM);

    /// <summary>
    /// <b>Whether a road passing here on this heading may be laid</b> (RAIL-5): off the railway, or over it square enough
    /// — and never a bridge, whose deck is the river's.
    /// </summary>
    public bool Crosses(Vector2 atM, Vector2 heading, RoadClass roadClass)
    {
        if (ground.At(atM) != Ground.Rail) return true;
        if (roadClass == RoadClass.Bridge) return false;

        var along = RailLines.AlongNearest(rails, atM);
        return MathF.Abs((heading.X * along.Y) - (heading.Y * along.X)) >= squareLeast;
    }

    /// <summary>Whether the water stands anywhere under a road laid on this chord.</summary>
    public bool Wets(Vector2 fromM, Vector2 toM)
    {
        var runM = (toM - fromM).Length();
        if (runM <= 0f) return Wet(fromM);

        var sideM = Heading.RightOf((toM - fromM) / runM) * halfWidthM;
        var steps = Math.Max(2, (int)(runM / stepM));
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
        bridgeable && (toM - fromM).Length() <= longestDeckM;

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
