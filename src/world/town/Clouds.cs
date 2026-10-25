using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The clouds over the town</b> (OBJ-8, <see cref="CloudFigures"/>): where their field stands over the town at a moment
/// of its clock, and how far through its changes of shape it is. Only their shadows are drawn (<c>CloudShadows</c>), and
/// they shade nothing the town reads.
/// </summary>
/// <remarks>
/// <b>A function of the clock and nothing kept</b>, as the <see cref="Wind"/> is: the field is carried at
/// <see cref="CloudFigures.SpeedMps"/> the way the wind blows about (<see cref="Wind.PrevailingRad"/>), from a place over
/// it the town's seed picks, so the same town at the same moment has the same sky however it got there.
/// </remarks>
internal sealed class Clouds
{
    readonly Vector2 _mps;
    readonly Vector2 _originM;
    readonly float _lifeS;

    public Clouds(CloudFigures figures, Wind wind, ulong seed)
    {
        var (sin, cos) = MathF.SinCos(wind.PrevailingRad);
        _mps = new Vector2(cos, sin) * figures.SpeedMps;
        _lifeS = figures.LifeS;

        // A few clouds' widths, so towns of two seeds are under two skies, and near enough that the field's own
        // coordinates stay as fine as a town's.
        var draw = new Rng(seed, Stream);
        var reachM = OriginReachInClouds * figures.AcrossM;
        _originM = new Vector2(draw.NextFloat(-reachM, reachM), draw.NextFloat(-reachM, reachM));
    }

    /// <summary>The draw the field's place is taken from, apart from every other the seed feeds.</summary>
    const ulong Stream = 0x636c6f75;

    const float OriginReachInClouds = 64f;

    /// <summary>
    /// Where the town's origin is in the field at <paramref name="elapsedS"/> of the town's clock: a point of the town at
    /// <c>p</c> is under the field's <c>p + FieldAtM</c>, which falls back the way the clouds move.
    /// </summary>
    public Vector2 FieldAtM(double elapsedS) =>
        _originM - new Vector2((float)(_mps.X * elapsedS), (float)(_mps.Y * elapsedS));

    /// <summary>How many of a cloud's lives have passed at <paramref name="elapsedS"/>: one, and every cloud has become another.</summary>
    public float LivesAt(double elapsedS) => (float)(elapsedS / _lifeS);
}
