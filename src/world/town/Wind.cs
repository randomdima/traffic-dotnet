using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The air over the town</b> (<see cref="WindFigures"/>): which way and how fast it moves at a moment of the town's
/// clock. It carries what is light enough to be carried — a car's smoke (CAR-54), a chimney's (OBJ-7) — and pushes nothing
/// that stands.
/// </summary>
/// <remarks>
/// <para>
/// <b>A function of the clock and nothing kept</b>, of two terms that answer to nothing of each other: which way it blows
/// (<see cref="BearingRad"/>) — a way the town's seed picks, swung slowly either side of it over
/// <see cref="WindFigures.TurnPeriodS"/> — and how hard (<see cref="SpeedMps"/>), gusting over
/// <see cref="WindFigures.GustPeriodS"/>, each from a phase of its own the seed picks too. So the same town at the same
/// moment has the same wind however it got there, and a paused town's wind stands.
/// </para>
/// <para>
/// <b>What it carries goes where the air of every moment since took it</b> (<see cref="Back"/>), not the air of now for as
/// long: a gust carries the smoke out in it further and the smoke out before it not at all, and a turn bends a plume from
/// the pot outwards rather than swinging it whole.
/// </para>
/// </remarks>
internal sealed class Wind
{
    readonly WindFigures _figures;
    readonly float _bearingRad, _turnPhaseRad, _gustPhaseRad;

    public Wind(WindFigures figures, ulong seed)
    {
        _figures = figures;
        var draw = new Rng(seed, Stream);
        _bearingRad = draw.NextFloat(0f, MathF.Tau);
        _turnPhaseRad = draw.NextFloat(0f, MathF.Tau);
        _gustPhaseRad = draw.NextFloat(0f, MathF.Tau);
    }

    /// <summary>The draw the wind's three angles are taken from, apart from every other the seed feeds.</summary>
    const ulong Stream = 0x77696e64;

    /// <summary>The way the town's seed has the air move towards, from +x towards +y, which <see cref="BearingRad"/> swings either side of.</summary>
    public float PrevailingRad => _bearingRad;

    /// <summary>The way the air moves towards at <paramref name="elapsedS"/> of the town's clock, from +x towards +y.</summary>
    public float BearingRad(float elapsedS) =>
        _bearingRad +(float.DegreesToRadians(_figures.TurnDeg) * MathF.Sin((MathF.Tau * elapsedS / _figures.TurnPeriodS) + _turnPhaseRad));

    /// <summary>How fast the air moves at <paramref name="elapsedS"/> of the town's clock.</summary>
    public float SpeedMps(float elapsedS) =>
        _figures.SpeedMps * (1f + (_figures.GustShare * MathF.Sin((MathF.Tau * elapsedS / _figures.GustPeriodS) + _gustPhaseRad)));

    /// <summary>The air's velocity at <paramref name="elapsedS"/> of the town's clock.</summary>
    public Vector2 AtMps(float elapsedS)
    {
        var (sin, cos) = MathF.SinCos(BearingRad(elapsedS));
        return new Vector2(cos, sin) * SpeedMps(elapsedS);
    }

    /// <summary>
    /// <b>The air at each of <paramref name="into"/>'s moments back from <paramref name="elapsedS"/></b>, one
    /// <paramref name="stepS"/> apart and the first now: how far it has carried what it held then, and its velocity then.
    /// </summary>
    /// <remarks>The carry is Simpson's rule a step, so a step is to be short against <see cref="WindFigures.GustPeriodS"/>.</remarks>
    public void Back(float elapsedS, float stepS, Span<AirBack> into)
    {
        var laterMps = AtMps(elapsedS);
        var sinceM = Vector2.Zero;
        into[0] = new AirBack(sinceM, laterMps);
        for (var back = 1; back < into.Length; back++)
        {
            var thenS = elapsedS - (back * stepS);
            var thenMps = AtMps(thenS);
            sinceM += (thenMps + (4f * AtMps(thenS + (stepS / 2f))) + laterMps) * (stepS / 6f);
            into[back] = new AirBack(sinceM, thenMps);
            laterMps = thenMps;
        }
    }
}

/// <summary>The air at a moment back from now (<see cref="Wind.Back"/>): how far it has carried since what it held then, and its velocity then.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct AirBack(Vector2 SinceM, Vector2 Mps);
