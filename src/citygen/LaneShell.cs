using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The outside of the ground the town is driven over, as the closed lines it is</b>: every lane, every
/// movement through a box and every way into a bay laid as the ribbon of ground it covers
/// (<see cref="ArcRibbon"/>), and all of those ribbons merged into one shape (GEN-4b, TER-5).
/// </summary>
/// <remarks>
/// <para>
/// <b>A lane is an area and not a line.</b> The ground a line lays is the band half a width either side of
/// it with a square end at either end, and the town's driven ground is the union of those bands — so the
/// outside of the town is the outside of that union and nothing else. Said instead as the stretches of the
/// lines that happen to be outermost, the answer was a walk: every line sampled against every band near it,
/// the stops paired up by how near they stood, and the ring shut with whatever straight the pairing left
/// over.
/// </para>
/// <para>
/// <b>What comes back carries only ribbon in it.</b> Every piece of every ring is a piece of some ribbon's
/// own edge or of its square end, cut where another ribbon's boundary crosses it — so an arc stays the arc
/// it was, at its own radius, and nothing is a chord across a bend it was meant to follow. A boundary that
/// is straightened anywhere cuts inside the very ground it is the edge of.
/// </para>
/// <para>
/// <b>A piece of ribbon is the outside where nothing else covers it.</b> That is the whole of the merge:
/// each piece is cut at every crossing it has with every other ribbon, and what is left of it is kept where
/// the ground a hair outside it is on no ribbon at all. No pairing, no ends to match up, no straight drawn
/// across ground nothing is driven along — a place is inside the town or it is not, and the merge asks the
/// bands themselves rather than reasoning about which line ought to hand over to which.
/// </para>
/// <para>
/// <b>The ground is on the walker's right the whole way round</b> (TER-3c.9), on the ring round the town and
/// on the ring round every block it encloses alike, because every piece kept its own ribbon's order
/// (<see cref="ArcRibbon"/>) and a merge only ever drops pieces. So the inward normal of any place on the
/// boundary is read off the boundary's own direction, with nothing to look up and no ring to identify as
/// the outer one.
/// </para>
/// <para>
/// <b>A ring turns only where the ground does.</b> The merge cuts a ribbon at every crossing anything has
/// with it, most of which are places the boundary carries straight on down the same curve — so what comes
/// back is joined (<see cref="Spline.JoinedInto"/>): consecutive stretches of one circle are the one
/// stretch they are, a ring's seam included. A point in a ring is a place the town's edge really turns or
/// changes radius, and a reader counting them is counting corners.
/// </para>
/// <para>
/// <b>A run that will not close is handed back apart</b> (<see cref="Loose"/>) rather than being shut with a
/// straight or dropped in silence. A merge that leaves one is a merge that lost a crossing somewhere, and
/// the length of it is the reading that says so.
/// </para>
/// </remarks>
internal sealed partial class LaneShell
{
    readonly ArcSeg[][] _chains;
    readonly ArcSeg[][] _loose;

    LaneShell(ArcSeg[][] chains, ArcSeg[][] loose)
    {
        _chains = chains;
        _loose = loose;
    }

    /// <summary>
    /// <b>The outside of the town's driven ground as closed rings</b> — one round the outside of the town
    /// and one round every block it encloses, each walked with the driven ground on its right.
    /// </summary>
    public ReadOnlySpan<ArcSeg[]> Chains => _chains;

    /// <summary>
    /// <b>The runs the merge could not close</b>, each a chain with two ends. It is a fault in the merge
    /// rather than a shape of the town: the boundary of a union of closed bands is closed, so a run with
    /// ends is a crossing that was not found or a piece that was kept when it should have been covered.
    /// </summary>
    public ReadOnlySpan<ArcSeg[]> Loose => _loose;

    /// <summary>
    /// <b>The town's driven ground merged into one shape.</b> Laid on the first ask and not before
    /// (<see cref="Paving.Perimeter"/>): nothing the town needs to be laid reads it.
    /// </summary>
    public static LaneShell Of(Paving paving, SimConfig config)
    {
        var count = paving.DrivenCount;
        var halfM = new float[count];
        var lengthM = new float[count];
        var ribbons = new ArcSeg[count][];
        var mostHalfM = 0f;

        for (var line = 0; line < count; line++)
        {
            halfM[line] = paving.DrivenWidthM(line) * 0.5f;
            lengthM[line] = paving.DrivenLengthM(line);
            mostHalfM = MathF.Max(mostHalfM, halfM[line]);

            ribbons[line] = ArcRibbon.Of(paving.ArcsOfDriven(line), halfM[line], LineTolerance.RoundingM);
        }

        var merge = new Merge(paving, config, ribbons, halfM, lengthM, mostHalfM);
        var (chains, loose) = merge.Run();
        return new LaneShell(chains, loose);
    }
}
