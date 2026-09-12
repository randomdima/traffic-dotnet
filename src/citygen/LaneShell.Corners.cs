using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

internal sealed partial class LaneShell
{
    /// <summary>
    /// <b>The same rings with every corner the ground turns outwards turned on an arc</b> (TER-5), so that
    /// the kerb the boundary is moved out to comes out tangent to both carriageways instead of mitred to a
    /// point.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Turned here and not where the distance is taken.</b> A ring moved by an offset rounds its own
    /// corners on that offset (<see cref="Extrusion.Round"/>), so a kerb struck off an unturned ring turns
    /// on half a lane — a metre and a bit, where the town is laid to turn on a car's width times over. Given
    /// the corner on the ring, every distance inherits it and each comes out at its own radius: the kerb at
    /// the radius the junction was laid at, the pavement at that plus a walk. <b>One corner, and every line
    /// in the town concentric about it.</b>
    /// </para>
    /// <para>
    /// <b>The radius is the junction's own and the reach bounds it</b>
    /// (<see cref="SimConfig.JunctionFilletRadiusM"/>): two arms crossing at a skew leave a corner that
    /// turns through very little, and a full-sized radius there runs back along both arms further than a
    /// kerb transition may reach. The figure that says how far is the one the town is laid with, asked here
    /// of the angle the ring actually turns rather than of an arm the ring knows nothing about.
    /// </para>
    /// <para>
    /// <b>A corner is never turned into more than half of either piece it stands between.</b> A ring turns
    /// corners a metre apart round the back of a car park, and a radius that ate both of them would leave a
    /// ring whose arcs run backwards.
    /// </para>
    /// </remarks>
    public LaneShell Rounded(SimConfig config)
    {
        if (_rounded is not null) return _rounded;
        if (_turned) return this;

        var chains = new ArcSeg[_chains.Length][];
        var lineOfArc = new int[_chains.Length][];
        var arcs = new List<ArcSeg>();
        var ranAlong = new List<int>();
        for (var ring = 0; ring < _chains.Length; ring++)
        {
            Turn(config, _chains[ring], _lineOfArc[ring], _halfOfArc[ring], arcs, ranAlong);
            chains[ring] = [.. arcs];
            lineOfArc[ring] = [.. ranAlong];
        }

        return _rounded = new LaneShell(chains, lineOfArc, _halfM, Reading, turned: true);
    }

    LaneShell? _rounded;

    /// <summary>
    /// One ring walked, each piece cut back at both ends by whatever the corners there take and the arc
    /// that turns the corner laid between.
    /// </summary>
    static void Turn(
        SimConfig config, ArcSeg[] ring, int[] lineOfArc, float[] halfOfArc, List<ArcSeg> into,
        List<int> ranAlong)
    {
        into.Clear();
        ranAlong.Clear();
        if (ring.Length < 3) return;

        // Both ends of every piece are settled before any of it is cut, because a corner's reach is bounded
        // by the pieces either side of it as they stand and not as the corner before them left them.
        var backM = new float[ring.Length];
        var onM = new float[ring.Length];
        var radiusM = new float[ring.Length];
        for (var at = 0; at < ring.Length; at++)
        {
            var next = (at + 1) % ring.Length;
            radiusM[at] = Corner(
                config, ring[at], ring[next], MathF.Min(halfOfArc[at], halfOfArc[next]), out var runM);
            backM[at] = runM;
            onM[next] = runM;
        }

        for (var at = 0; at < ring.Length; at++)
        {
            var arc = ring[at];
            var fromM = onM[at];
            var toM = arc.LengthM - backM[at];
            if (toM > fromM)
            {
                into.Add(Piece(arc, fromM, toM));
                ranAlong.Add(lineOfArc[at]);
            }

            if (radiusM[at] <= 0f || backM[at] <= 0f) continue;

            var next = (at + 1) % ring.Length;
            into.Add(Arc(arc, backM[at], ring[next], onM[next], radiusM[at]));

            // The corner belongs to the boundary rather than to either line, exactly as a bridge does.
            ranAlong.Add(Nowhere);
        }
    }

    /// <summary>
    /// <b>How far back along each piece the corner between two of them runs, and on what radius</b> — nought
    /// where the ring turns into the ground rather than out of it, since a corner turned that way is one the
    /// two sides of an offset run past each other at and nothing is drawn between them.
    /// </summary>
    static float Corner(SimConfig config, in ArcSeg arriving, in ArcSeg leaving, float halfM, out float runM)
    {
        runM = 0f;

        // Out is the walker's left, so a corner the offset is carried round is one the ring turns left at.
        var turnRad = Spline.WrapRad(leaving.HeadingRad - arriving.HeadingAtRad(arriving.LengthM));
        if (turnRad <= 0f) return 0f;

        // What the kerb turns on, less the half-band the kerb already stands out by: moved out, the arc laid
        // here comes back at the radius the junction was laid at.
        var kerbM = config.JunctionFilletRadiusM(MathF.PI - turnRad) - halfM;
        if (kerbM <= 0f) return 0f;

        var reachM = kerbM * MathF.Tan(turnRad * 0.5f);
        var mostM = MathF.Min(arriving.LengthM, leaving.LengthM) * 0.5f;
        if (reachM > mostM)
        {
            reachM = mostM;
            kerbM = reachM / MathF.Tan(turnRad * 0.5f);
        }

        if (reachM <= Kerbs.RoundingM) return 0f;

        runM = reachM;
        return kerbM;
    }

    /// <summary>One piece of a ring between two distances along it, keeping the curvature it was laid with.</summary>
    static ArcSeg Piece(in ArcSeg arc, float fromM, float toM) =>
        new(arc.PointAtM(fromM), arc.HeadingAtRad(fromM), toM - fromM, arc.Curvature);

    /// <summary>The arc that turns one piece onto the next, tangent to both where each was cut back to.</summary>
    static ArcSeg Arc(in ArcSeg arriving, float backM, in ArcSeg leaving, float onM, float radiusM)
    {
        var fromM = arriving.LengthM - backM;
        var startM = arriving.PointAtM(fromM);
        var startRad = arriving.HeadingAtRad(fromM);
        var sweepRad = Spline.WrapRad(leaving.HeadingAtRad(onM) - startRad);
        return new ArcSeg(startM, startRad, radiusM * MathF.Abs(sweepRad), MathF.Sign(sweepRad) / radiusM);
    }
}
