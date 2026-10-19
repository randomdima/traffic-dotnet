using System.Numerics;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// <b>The claims' wash, and the one hold that is not a piece of way</b>: the stretches every claim holds are drawn by the
/// engine layers straight off the claims table (<see cref="EngineLayers"/>, <c>kernels/engine_ways.comp</c>) — whose
/// colour, how strong, and an outline where a stretch is only crossed — and what is left here is the ladder they are washed
/// by and the bays somebody has taken.
/// </summary>
/// <remarks>
/// <para>
/// <b>A claim is drawn ahead of the car that holds it and that is not a fault.</b> It is the ground the driver will come to
/// rest on rather than the ground it is on, so at speed it stands a stopping distance up the road — and the gap between one
/// car's block and the next one's is the whole of what keeps them apart.
/// </para>
/// <para>
/// <b>The wash is how strong the hold is</b> (<see cref="Wash"/>), <b>the pieces of one hold are told apart by a bar across
/// the end of each</b>, and <b>a secondary claim is an outline with no wash</b>: one body's ground is several stretches at
/// one strength that butt exactly, so the joints are marked rather than shaded, and the shade is left to say which of the
/// asks lying over a busy junction anybody would actually give up.
/// </para>
/// <para>
/// <b>It is a layer of its own</b> (OBS-2c), and belongs to no kind of body, because <b>a claim is a fact about the
/// ground</b>: what cuts a driver's grant is as often a walker standing in the lane as another car.
/// </para>
/// </remarks>
internal sealed partial class DebugOverlay
{
    /// <summary>What the strongest claim there is (<see cref="ClaimPriority.Hard"/>) is let down to.</summary>
    const float StrongestWash = 0.5f;

    /// <summary>And the weakest (<see cref="ClaimPriority.Backing"/>), which is the faintest anything is drawn.</summary>
    const float WeakestWash = 0.1f;

    /// <summary>
    /// How much stronger a block's own edges are than its wash — the same colour, up rather than down, so a
    /// joint reads as a line on the ground instead of as a change of shade.
    /// </summary>
    internal const float EdgeOverWash = 2f;

    /// <summary>
    /// How thick that edge is: under the standard debug line, because it is a boundary between two pieces
    /// of one body's ground and not a thing in its own right.
    /// </summary>
    const float BlockEdgeM = PathMarks.PathLineM * 0.7f;

    /// <summary>
    /// <b>What a block of a given strength is let down to</b> — the ladder (<see cref="ClaimPriority"/>)
    /// read straight off as a wash, so the road a body can no longer give back is half solid and the road a
    /// driver has only said it means to use is nearly gone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Transparent throughout, whatever the strength</b>, because the tarmac, the paint and the body
    /// standing on the stretch all have to read through it.
    /// </para>
    /// <para>
    /// <b>Off the ladder's own numbers and not off a table of its own.</b> A rung added between two that
    /// exist takes the wash its number earns, and a level nothing claims yet costs the layer nothing.
    /// </para>
    /// </remarks>
    internal static Vector4 Wash(ClaimPriority priority)
    {
        var rung = (float)priority / (float)ClaimPriority.Backing;
        return new Vector4(1f, 1f, 1f, StrongestWash + ((WeakestWash - StrongestWash) * rung));
    }

    /// <summary>
    /// And the bars across its ends, which ride their own block's wash rather than standing at a figure of
    /// their own: a claim faint enough to look through keeps ends faint enough to look through.
    /// </summary>
    static Vector4 Edge(ClaimPriority priority)
    {
        var wash = Wash(priority);
        return wash with { W = MathF.Min(1f, wash.W * EdgeOverWash) };
    }

    /// <summary>
    /// <b>The bays that are spoken for, drawn as the bays they are</b> — washed where a body is standing in
    /// one, outlined where a leg has only claimed it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the one hold in the town that is not a piece of road</b> (GEN-4g), which is why it is the one
    /// thing on this layer that is not a stretch of a way. A claim on a bay holds no ground — a body standing
    /// in one is laid on the bay's own lane like on any other — so the picture of a taken bay has to come off
    /// the register that takes it.
    /// </para>
    /// <para>
    /// <b>Washed is a body and outlined is a bay claimed</b>, and that is a difference in what is being
    /// claimed rather than a shade on one claim: somebody is standing here, against somebody is on their way
    /// and nobody else may take it.
    /// </para>
    /// </remarks>
    static void TakenBays(
        ref ScreenDraw draw, TownWorld world, SimConfig config, Vector2 viewCentreM, Vector2 viewSpanM)
    {
        var parking = world.Parking;
        var sizeM = new Vector2(config.ParkingSpaceLengthM, config.ParkingSpaceWidthM);

        // Walked over the cars and not over the bays: what makes a bay worth drawing is a car, and a town
        // has more bays than it has cars to put in them.
        for (var car = 0; car < world.Cars.Count; car++)
        {
            var standingIn = parking.BayOf(car);
            var bay = standingIn >= 0 ? standingIn : parking.ClaimedBayOf(car);
            if (bay < 0) continue;

            var centreM = parking.CentreM(bay);
            if (!OnScreen(centreM, viewCentreM, viewSpanM, config.ParkingSpaceLengthM)) continue;

            // A bay is a place in a register rather than a rung of the ladder, and both holds on one are
            // absolute: nobody takes a bay from the car standing in it or from the leg walking to it. So
            // both are drawn at the strength of a body, and what tells them apart is the fill (GEN-4g).
            var colour = Theme.AgentLine(car);
            var headingRad = parking.HeadingRad(bay);
            if (standingIn >= 0)
            {
                var alongM = Heading.Unit(headingRad) * (config.ParkingSpaceLengthM * 0.5f);
                draw.BandM(
                    centreM - alongM, centreM + alongM, 0f, config.ParkingSpaceWidthM,
                    colour * Wash(ClaimPriority.Hard));
            }

            draw.BoxM(centreM, sizeM, headingRad, BlockEdgeM, colour * Edge(ClaimPriority.Hard));
        }
    }
}
