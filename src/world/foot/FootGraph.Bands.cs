using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.World.Foot;

/// <summary>The pavement, laid by wrapping the tarmac — the whole of it, in one construction.</summary>
internal sealed partial class FootGraph
{
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
    /// turned round the square end the road stops at (TER-3c.6). Each is cut by the one rule, and each
    /// meets its neighbours at the point their two lines cross — which is the point both are half a walk
    /// from both pieces, so nothing has to be matched to anything or pushed onto anything.
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
        var runs = new List<Kerbs.Wrap>();
        kerbs.Shell(bandM * 0.5f, weldM, pointM => terrain.At(pointM).Walkable, runs);

        foreach (var (_, line, onlyWhereTheKerbIsOpen) in runs)
        {
            builder.AddStrand(line, bandM, FootEdgeKind.Pavement, onlyWhereTheKerbIsOpen);
        }
    }

    /// <summary>
    /// The most a joint may be open by before two stretches are no longer one line
    /// (<see cref="Builder.RunOn"/>) — the same rounding the outline itself is cut with
    /// (<see cref="Kerbs.RoundingM"/>).
    /// </summary>
    const float RoundingM = Kerbs.RoundingM;

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
            if (!kerbs.Clear(centreM + (alongM * outwardM), outM))
            {
                insideM = outwardM;
                continue;
            }

            var outsideM = outwardM;
            for (var halving = 0; halving < BisectionRounds; halving++)
            {
                var middleM = (insideM + outsideM) * 0.5f;
                if (kerbs.Clear(centreM + (alongM * middleM), outM)) outsideM = middleM;
                else insideM = middleM;
            }

            atM = centreM + (alongM * (insideM + outsideM) * 0.5f);
            return true;
        }

        return false;
    }
}
