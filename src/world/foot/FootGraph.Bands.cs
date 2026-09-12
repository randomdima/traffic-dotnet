using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.World.Foot;

/// <summary>The pavement, laid by extruding the town's own boundary — the whole of it, in one construction.</summary>
internal sealed partial class FootGraph
{
    const int BisectionRounds = 12;

    /// <summary>
    /// How finely a ring is walked when asking whether the ground under it will carry a pavement. A
    /// quarter-metre, which is what the boundary it was struck from is walked at.
    /// </summary>
    const float StationM = 0.25f;

    /// <summary>
    /// <b>The pavement is the town's boundary moved out by half a walk, and it is a ring</b> (TER-3c.3):
    /// one closed lane round the outside of the driven ground and one round every block it encloses
    /// (<see cref="GroundRings"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A cycle has no ends, so there is nothing to join.</b> The three hardest questions a wrap of
    /// separate pieces asked — which loose end meets which, which line leads somewhere and which
    /// dead-ends, and which pavement is another one said twice — were all the same question in different
    /// clothes: <em>where does this piece of line carry on to?</em> A ring answers it by construction. What
    /// is laid here is the ring, and the builder's joining, dropping and de-duplicating now have only the
    /// crossings and the few cuts below to work on.
    /// </para>
    /// <para>
    /// <b>The ground still has a veto, and it is the only one</b>: a lane over water, or off the map, is not
    /// a lane however far it stands from the nearest kerb. It is asked of the ground the town answers with
    /// and not of a second reading of the plan, and it is what turns a ring back into runs where it bites.
    /// </para>
    /// <para>
    /// <b>A ring nothing vetoes is still laid in two.</b> A stretch whose two ends are one node is ground no
    /// walk can be stationed along and a corner nothing can be turned on
    /// (<see cref="Builder.AddStrand"/>), so an uncut ring is halved — two stretches meeting at two nodes,
    /// which is the same cycle said in the terms the graph keeps.
    /// </para>
    /// </remarks>
    static void Wrap(GroundRings rings, GroundLocator terrain, Builder builder, float bandM, float weldM)
    {
        var runs = new List<(float FromM, float ToM)>();
        var room = new ArcSeg[64];
        foreach (var ring in rings.At(bandM * 0.5f))
        {
            if (ring.Length == 0) continue;

            var lengthM = Spline.TotalLengthM(ring);
            if (lengthM <= weldM) continue;

            Carried(ring, lengthM, weldM, pointM => terrain.At(pointM).Walkable, runs);
            if (room.Length < ring.Length + 2) room = new ArcSeg[ring.Length + 2];

            foreach (var (fromM, toM) in runs)
            {
                var arcs = Spline.SubChainInto(ring, fromM, toM, room);
                if (arcs > 0) builder.AddStrand(room.AsSpan(0, arcs), bandM, FootEdgeKind.Pavement);
            }
        }
    }

    /// <summary>
    /// The runs of one ring the ground will carry, as distances along it — <b>and a ring the ground carries
    /// whole comes back in two</b>, since a run from a point to itself is no run.
    /// </summary>
    static void Carried(
        ReadOnlySpan<ArcSeg> ring, float lengthM, float weldM, Func<Vector2, bool> carried,
        List<(float FromM, float ToM)> into)
    {
        into.Clear();

        var stations = Math.Max(1, (int)MathF.Ceiling(lengthM / StationM));
        var was = carried(ring[0].StartM);
        var openedAtM = was ? 0f : -1f;
        for (var station = 1; station <= stations; station++)
        {
            var alongM = lengthM * station / stations;
            var stands = carried(Spline.SampleAt(ring, alongM).PositionM);
            if (stands == was) continue;

            var edgeM = Crossing(ring, carried, lengthM * (station - 1) / stations, alongM, stands);
            if (stands) openedAtM = edgeM;
            else if (openedAtM >= 0f && edgeM - openedAtM > weldM) into.Add((openedAtM, edgeM));

            was = stands;
        }

        if (was && openedAtM >= 0f && lengthM - openedAtM > weldM) into.Add((openedAtM, lengthM));

        if (into.Count == 1 && into[0].FromM <= 0f && into[0].ToM >= lengthM)
        {
            into[0] = (0f, lengthM * 0.5f);
            into.Add((lengthM * 0.5f, lengthM));
        }
    }

    /// <summary>Where along the ring the ground changed its mind, bisected between the two stations it changed between.</summary>
    static float Crossing(
        ReadOnlySpan<ArcSeg> ring, Func<Vector2, bool> carried, float wasM, float isM, bool standsAtIs)
    {
        for (var halving = 0; halving < BisectionRounds; halving++)
        {
            var middleM = (wasM + isM) * 0.5f;
            if (carried(Spline.SampleAt(ring, middleM).PositionM) == standsAtIs) isM = middleM;
            else wasM = middleM;
        }

        return (wasM + isM) * 0.5f;
    }

    /// <summary>
    /// The most a joint may be open by before two stretches are no longer one line
    /// (<see cref="Builder.RunOn"/>) — the same rounding the boundary itself is cut with
    /// (<see cref="Kerbs.RoundingM"/>).
    /// </summary>
    const float RoundingM = Kerbs.RoundingM;

    /// <summary>
    /// <b>Where the pavement stands on one side of a crossing</b>: out along the crossing's own square, to
    /// the first metre that stands <paramref name="outM"/> off the kerb.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the same question the lane itself was struck by, asked along a ray instead of read off a ring
    /// — one distance compared against one figure (<see cref="GroundRings.OffTheKerbM"/>) — so the mouth
    /// lands <em>on</em> the lane rather than at whatever node happened to pass nearest the point the
    /// crossing's own span reaches to. Snapped to the nearest node instead, a zebra beside a bend came out
    /// skewed to its own paint and ran a couple of metres past it onto the road.
    /// </para>
    /// <para>
    /// <b>A ray that never gets clear of the tarmac has no mouth</b>, and says so. Answered with the point
    /// it gave up at, a crossing that reaches no pavement still split whatever pavement happened to lie
    /// within a walk of that point — which left a stretch ending in the middle of a car park's mouth, half
    /// a metre off the kerb, on a line that had been laid clear of it.
    /// </para>
    /// </remarks>
    static bool Mouth(
        GroundRings rings, Vector2 centreM, Vector2 alongM, float mostM, float outM, out Vector2 atM)
    {
        const float StepM = 0.05f;

        atM = centreM;
        var insideM = 0f;
        for (var outwardM = StepM; outwardM <= mostM; outwardM += StepM)
        {
            if (rings.OffTheKerbM(centreM + (alongM * outwardM)) < outM)
            {
                insideM = outwardM;
                continue;
            }

            var outsideM = outwardM;
            for (var halving = 0; halving < BisectionRounds; halving++)
            {
                var middleM = (insideM + outsideM) * 0.5f;
                if (rings.OffTheKerbM(centreM + (alongM * middleM)) >= outM) outsideM = middleM;
                else insideM = middleM;
            }

            atM = centreM + (alongM * (insideM + outsideM) * 0.5f);
            return true;
        }

        return false;
    }
}
