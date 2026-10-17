using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Parking;

/// <summary>
/// <b>How square the town's cars come to rest in their places</b> (GEN-4i): every parking's body against the place it was
/// parked in — how far its heading stands off the place's line, either way round, how far its middle stands to one side of
/// that line and along it — totalled apart for a painted bay and a kerb. An instrument, read by nothing that decides.
/// </summary>
internal sealed class ParkedSquareness
{
    /// <summary>The parkings each figure is over: a painted bay's at 0, a kerb's at 1.</summary>
    public readonly long[] Count = new long[2];

    public readonly double[] SkewDegSum = new double[2];

    public readonly float[] SkewDegMost = new float[2];

    /// <summary>Parkings whose heading stood further off the place's line than the figure the probe reads them against.</summary>
    public readonly long[] Crooked = new long[2];

    public readonly double[] AsideMSum = new double[2];

    public readonly float[] AsideMMost = new float[2];

    public readonly double[] AlongMSum = new double[2];

    public readonly float[] AlongMMost = new float[2];

    /// <summary>One parking: the body's middle and heading against the place's middle and line.</summary>
    /// <param name="crookedDeg">The skew a parking is counted crooked past.</param>
    public void Note(bool kerb, Vector2 bodyM, float bodyHeadingRad, Vector2 placeM, float placeHeadingRad, float crookedDeg)
    {
        var at = kerb ? 1 : 0;
        var skewRad = MathF.Abs(Spline.WrapRad(bodyHeadingRad - placeHeadingRad));
        var skewDeg = float.RadiansToDegrees(MathF.Min(skewRad, MathF.PI - skewRad));
        var along = Heading.Unit(placeHeadingRad);
        var offM = bodyM - placeM;
        var asideM = MathF.Abs(Spline.Cross(along, offM));
        var alongM = MathF.Abs(Vector2.Dot(along, offM));

        Count[at]++;
        SkewDegSum[at] += skewDeg;
        SkewDegMost[at] = MathF.Max(SkewDegMost[at], skewDeg);
        if (skewDeg > crookedDeg) Crooked[at]++;
        AsideMSum[at] += asideM;
        AsideMMost[at] = MathF.Max(AsideMMost[at], asideM);
        AlongMSum[at] += alongM;
        AlongMMost[at] = MathF.Max(AlongMMost[at], alongM);
    }

    /// <summary>One kind's figures as a line of a report.</summary>
    public string Said(bool kerb)
    {
        var at = kerb ? 1 : 0;
        var n = Math.Max(1, Count[at]);
        return $"{Count[at]} parked, skew mean {SkewDegSum[at] / n:F1}° most {SkewDegMost[at]:F1}° ({Crooked[at]} crooked), " +
               $"aside mean {AsideMSum[at] / n:F2} m most {AsideMMost[at]:F2} m, along mean {AlongMSum[at] / n:F2} m most {AlongMMost[at]:F2} m";
    }
}
