using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.World.Foot;

/// <summary>The pavement, laid by wrapping the tarmac — the whole of it, in one construction.</summary>
internal sealed partial class FootGraph
{
    /// <summary>
    /// How finely a wrapping line is walked when asking what it runs past. A quarter-metre is the road
    /// tolerance, and where the answer changes between two stations the crossing is bisected off it — so
    /// what decides where a stretch ends is a millimetre and not a station.
    /// </summary>
    const float StationM = 0.25f;

    const int BisectionRounds = 12;


    /// <summary>
    /// <b>The pavement is the tarmac wrapped, and there is one rule for the whole of it</b> (TER-3c.3):
    /// every carriageway, junction, kerb fillet and car park offers the line that stands half a walk
    /// outside it, and a metre of such a line is pavement exactly where <b>nothing else stands nearer
    /// than that</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A junction is not a case.</b> The band round a kerb corner is the fillet's own arc read in by
    /// half a walk; the band past a car park is the box's; the band round a dead end is the road's own,
    /// ending where the road does. Each is cut by the one rule, and each meets its neighbours at the
    /// point their two lines cross — which is the point both are half a walk from both pieces, so nothing
    /// has to be matched to anything, pushed onto anything, or joined across a gap.
    /// </para>
    /// <para>
    /// <b>What a box is walked round is the arms that meet at it</b> (TER-3c.5). The lines cars are turned
    /// through it on are tarmac the arms enclose, and half a walk outside one of those is usually the
    /// middle of the pavement rather than the edge of it — so such a line is pavement only where it
    /// <b>leads somewhere</b>: joined to the rest of the shell at both ends it closes a gap the arms left
    /// open, and dead-ending it is the same pavement said twice
    /// (<see cref="Builder.DropTheLinesThatLeadNowhere"/>). Kept whatever it did, it laid a second line
    /// beside the arm's own at every mouth in the town, and a walk down the pavement crossed from one lane
    /// to the other and back.
    /// </para>
    /// <para>
    /// <b>Where two carriageways merge, the outer edge of the pair is what is wrapped</b>, because each
    /// one's line runs on into the other's tarmac and is cut there. Where one runs inside another — a
    /// street lying along a car park's mouth — its line is cut over the whole of it and nothing is laid.
    /// It is the one rule reaching a second answer and not a second rule.
    /// </para>
    /// <para>
    /// <b>The ground still has a veto</b>, and it is the only one: a line over water, or off the map, is
    /// not pavement however far it stands from the nearest kerb. It is asked of the ground the town
    /// answers with and not of a second reading of the plan.
    /// </para>
    /// </remarks>
    static void Wrap(Kerbs kerbs, GroundLocator terrain, Builder builder, float bandM, float weldM)
    {
        var outM = bandM * 0.5f;

        var wraps = new List<Kerbs.Wrap>();
        kerbs.Wrapping(outM, wraps);

        var run = new ArcSeg[2];
        var runs = new List<(float FromM, float ToM)>();
        foreach (var (_, line, onlyWhereTheKerbIsOpen) in wraps)
        {
            var lengthM = Spline.TotalLengthM(line);
            if (lengthM <= 0f) continue;

            Spans(kerbs, terrain, line, lengthM, outM, weldM, runs);
            if (run.Length < line.Length + 2) run = new ArcSeg[line.Length + 2];

            foreach (var (fromM, toM) in runs)
            {
                var arcCount = Spline.SubChainInto(line, fromM, toM, run);
                if (arcCount == 0) continue;

                builder.AddStrand(
                    run.AsSpan(0, arcCount), bandM, FootEdgeKind.Pavement, onlyWhereTheKerbIsOpen);
            }
        }
    }

    /// <summary>
    /// The spans of one wrapping line that are pavement, as distances along it. <b>A line that is clear
    /// end to end comes back cut in two</b>: a circle round a dead end and a box round a car park close on
    /// themselves, and a stretch whose two ends are one node is a piece no walk can be stationed along.
    /// </summary>
    static void Spans(
        Kerbs kerbs, GroundLocator terrain, ReadOnlySpan<ArcSeg> line, float lengthM, float outM,
        float weldM, List<(float FromM, float ToM)> into)
    {
        into.Clear();

        var stations = Math.Max(1, (int)MathF.Ceiling(lengthM / StationM));
        var was = Stands(kerbs, terrain, line, 0f, outM);
        var openedAtM = was ? 0f : -1f;
        for (var station = 1; station <= stations; station++)
        {
            var alongM = lengthM * station / stations;
            var stands = Stands(kerbs, terrain, line, alongM, outM);
            if (stands == was) continue;

            var edgeM = Crossing(
                kerbs, terrain, line, outM, lengthM * (station - 1) / stations, alongM, stands);
            if (stands) openedAtM = edgeM;
            else Keep(into, openedAtM, edgeM, weldM);

            was = stands;
        }

        if (was) Keep(into, openedAtM, lengthM, weldM);

        // Nothing gave way anywhere along it, so it is a closed line and both its ends are the same node.
        if (into.Count == 1 && into[0].FromM <= 0f && into[0].ToM >= lengthM)
        {
            into[0] = (0f, lengthM * 0.5f);
            into.Add((lengthM * 0.5f, lengthM));
        }
    }

    /// <summary>
    /// One span, kept unless it is shorter than the graph's own weld — in which case its two ends are the
    /// same node and what it would lay is a stretch running from a node to itself.
    /// </summary>
    static void Keep(List<(float FromM, float ToM)> into, float fromM, float toM, float weldM)
    {
        if (fromM >= 0f && toM - fromM > weldM) into.Add((fromM, toM));
    }

    /// <summary>Where along the line the answer changed, bisected between the two stations it changed between.</summary>
    static float Crossing(
        Kerbs kerbs, GroundLocator terrain, ReadOnlySpan<ArcSeg> line, float outM, float wasM,
        float isM, bool standsAtIs)
    {
        for (var halving = 0; halving < BisectionRounds; halving++)
        {
            var middleM = (wasM + isM) * 0.5f;
            if (Stands(kerbs, terrain, line, middleM, outM) == standsAtIs) isM = middleM;
            else wasM = middleM;
        }

        return (wasM + isM) * 0.5f;
    }

    /// <summary>
    /// Whether one metre of a wrapping line is pavement: <b>no tarmac nearer than the offset it
    /// was laid at</b>, and ground a person may stand on.
    /// </summary>
    static bool Stands(
        Kerbs kerbs, GroundLocator terrain, ReadOnlySpan<ArcSeg> line, float alongM, float outM)
    {
        var atM = Spline.SampleAt(line, alongM).PositionM;
        return Clear(kerbs, atM, outM) && terrain.At(atM).Walkable;
    }

    /// <summary>
    /// Whether a point stands the offset clear of every piece of tarmac — the one rule, asked of one point.
    /// </summary>
    /// <remarks>
    /// <b>Asked with a rounding's grace and no more.</b> A wrapping line stands the offset from its own
    /// piece exactly, and where two pieces are tangent it stands the offset from both of them exactly
    /// (<see cref="Kerbs.OffTheTarmacM"/>) — so compared without the grace, whether metres of pavement exist
    /// is settled by the last bit of a float, and the apron round every junction whose movements run edge to
    /// edge with its arms came out bare. <b>And a rounding and not a tolerance</b>, because a tolerance ε lets
    /// a line that meets another <em>tangentially</em> run √(2·R·ε) past the point they cross: at five
    /// centimetres that is better than half a metre each, which is how the pavement came apart into a piece
    /// per corner the first time round.
    /// </remarks>
    static bool Clear(Kerbs kerbs, Vector2 pointM, float outM) =>
        kerbs.OffTheTarmacM(pointM) >= outM - RoundingM;

    /// <summary>A millimetre: what offsetting a chain and measuring back to it disagree by, and nothing else.</summary>
    const float RoundingM = 0.001f;

    /// <summary>
    /// <b>Where the pavement stands on one side of a crossing</b>: out along the crossing's own square,
    /// to the first metre that is half a walk clear of the tarmac.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the same question the wrap itself is cut by, asked along a ray instead of along a line — so
    /// the mouth lands <em>on</em> the pavement's line rather than at whatever node happened to pass
    /// nearest the point the crossing's own span reaches to. Snapped to the nearest node instead, a zebra
    /// beside a bend came out skewed to its own paint and ran a couple of metres past it onto the road.
    /// </para>
    /// <para>
    /// <b>A ray that never gets clear of the tarmac has no mouth</b>, and says so. Answered with the point
    /// it gave up at, a crossing that reaches no pavement still split whatever pavement happened to lie
    /// within a walk of that point — which left a stretch ending in the middle of a car park's mouth, half
    /// a metre off the kerb, on a line that had been laid clear of it.
    /// </para>
    /// </remarks>
    static bool Mouth(Kerbs kerbs, Vector2 centreM, Vector2 alongM, float mostM, float outM, out Vector2 atM)
    {
        const float StepM = 0.05f;

        atM = centreM;
        var insideM = 0f;
        for (var outwardM = StepM; outwardM <= mostM; outwardM += StepM)
        {
            if (!Clear(kerbs, centreM + (alongM * outwardM), outM))
            {
                insideM = outwardM;
                continue;
            }

            var outsideM = outwardM;
            for (var halving = 0; halving < BisectionRounds; halving++)
            {
                var middleM = (insideM + outsideM) * 0.5f;
                if (Clear(kerbs, centreM + (alongM * middleM), outM)) outsideM = middleM;
                else insideM = middleM;
            }

            atM = centreM + (alongM * (insideM + outsideM) * 0.5f);
            return true;
        }

        return false;
    }
}
