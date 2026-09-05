using System.Numerics;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// <b>The lane index drawn on the lanes</b> — every stretch of road somebody has claimed, as
/// a block of the lane it is a stretch of, coloured by whose it is and what it is.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the one thing about a driver that has no shape of its own.</b> A line, a lookahead and a claimed
/// box are all somewhere; whether the car in front counts as a queue or as something to get past, and how
/// much road each driver was granted, are readings — and until they are drawn the only way to see either
/// is to stop the run and look at an array.
/// </para>
/// <para>
/// <b>A claim is drawn ahead of the car that holds it and that is not a fault.</b> It is the ground
/// the driver will come to rest on rather than the ground it is on, so at speed it stands a stopping
/// distance up the road — and the gap between one car's block and the next one's is the whole of what
/// keeps them apart.
/// </para>
/// <para>
/// <b>The wash is how strong the hold is</b> (<see cref="Wash"/>), and <b>the pieces of one hold are told
/// apart by a bar across the end of each</b>. A block is drawn per stretch and a body's ground is regularly
/// several of them at one strength — a lane, the join after it, the ground beyond its own road it has
/// committed to — which butt exactly and are one continuous band. So those joints are marked rather than
/// shaded, and the shade is left to say the one thing a reader cannot get anywhere else: which of the asks
/// lying over a busy junction anybody would actually give up.
/// </para>
/// <para>
/// <b>Drawn straightened back out.</b> The index holds a stretch as two numbers along a way's arclength,
/// so the layer walks the way's own arcs between them rather than joining the two ends — a block across a
/// junction join drawn as a chord is a claim over ground nobody drives.
/// </para>
/// <para>
/// <b>At the lane's own width</b>, which is the road's declared width halved and is the same number the
/// lane's line was offset by half of (<see cref="RoadGraph.LaneWidthM"/>). It is the model's figure and
/// not a drawing one: the follower is held to a quarter of it, the pavement band starts at the edge of it
/// and the tarmac is laid to it. A block a metre wider than the ground it stands for would be a picture
/// arguing with the town.
/// </para>
/// <para>
/// <b>It is a layer of its own</b> (OBS-2c), and belongs to neither kind of body, because <b>a claim
/// is a fact about the ground</b>: what cuts a driver's grant is as often a walker standing in the lane as
/// it is another car, and the footways are drawn on the same layer as the carriageway because they are ways
/// of the same table (<see cref="ClaimIndex"/>). Drawn with the cars it could not show either without the
/// car switch on, which is the reading the block exists for. The colour still says whose the ground is —
/// the two rosters share <see cref="Theme.AgentLine"/>, and which body a stretch belongs to is what its own
/// layer says.
/// </para>
/// <para>
/// <b>And not the node layer's either, though both are the town's.</b> The graphs are the ground the town
/// was laid with and are cached because they never move; the claims are what this tick did to that ground.
/// Switched together, the reading either one is opened for came with the other underneath it — a lane full
/// of blocks over every movement through the box, or a fan of chevrons through every block.
/// </para>
/// </remarks>
internal sealed partial class DebugOverlay
{
    /// <summary>
    /// How many stretches of one way are drawn. A bound on a stack span and on the work, not a figure
    /// behaviour reads: past it a stretch goes undrawn, which is a lane whose picture is short rather than
    /// a town that behaves differently.
    /// </summary>
    const int MostDrawnSlotsOnAWay = 32;

    /// <summary>What the strongest claim there is (<see cref="ClaimPriority.Hard"/>) is let down to.</summary>
    const float StrongestWash = 0.5f;

    /// <summary>And the weakest (<see cref="ClaimPriority.Soft"/>), which is the faintest anything is drawn.</summary>
    const float WeakestWash = 0.1f;

    /// <summary>
    /// How much stronger a block's own edges are than its wash — the same colour, up rather than down, so a
    /// joint reads as a line on the ground instead of as a change of shade.
    /// </summary>
    const float EdgeOverWash = 2f;

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
    /// <b>It is the reading the layer is opened for on a busy street.</b> Every stretch drawn at one wash,
    /// a junction is a single band of overlapping asks with nothing to say which of them anybody would
    /// actually give up — where whose ground it is is the colour, what it is worth is the shade.
    /// </para>
    /// <para>
    /// <b>Transparent throughout, whatever the strength</b>, because the tarmac, the paint and the body
    /// standing on the stretch all have to read through it: the block says which ground is spoken for, and
    /// hiding the ground to say it would defeat the point.
    /// </para>
    /// <para>
    /// <b>Off the ladder's own numbers and not off a table of its own.</b> A rung added between two that
    /// exist takes the wash its number earns, and a level nothing claims yet costs the layer nothing.
    /// <see cref="ClaimPriority.Rejected"/> is past the weakest hold and washes with it.
    /// </para>
    /// </remarks>
    static Vector4 Wash(ClaimPriority priority)
    {
        var rung = MathF.Min((float)priority, (float)ClaimPriority.Soft) / (float)ClaimPriority.Soft;
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
    /// <b>Every claim in the town, on the ground it is a claim on</b> — one walk over one table
    /// (<see cref="TownWays"/>), whether the way under it is carriageway, a junction's join, a bay, a
    /// footway or the mitre at a corner.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One layer, because there is one set of claims.</b> A walker's stretch of pavement and its band of
    /// the lane it is crossing are one body's ground; drawn by which surface each of them is on, one body
    /// was two layers and no block could be followed from the pavement onto the road.
    /// </para>
    /// <para>
    /// <b>A block is drawn ahead of the body that holds it and that is not a fault</b>: it is the ground
    /// that body may come to rest on. A walker's is short where a driver's is long, because a walker loses
    /// its pace inside a fifth of its own body and the whole of what it asks for is the gap it keeps.
    /// </para>
    /// <para>
    /// <b>A car that has mounted a kerb is drawn on the footway</b> (TER-4c.2): a body holds the ground it
    /// stands on whatever kind of ground that is. <b>What is never on a walk is a car on a zebra</b>
    /// (TER-5c.1) — the paint is carriageway a walk runs over, so its ground is a stretch of the lane and is
    /// drawn as the lane's block. A copy of it on the walk drew one body twice.
    /// </para>
    /// <para>
    /// <b>Each way is drawn at the width its own ground was measured at</b>
    /// (<see cref="TownWorld.LineOfWay"/>): a join takes the lane it arrives on, a bay the space it serves,
    /// and a footway the offset its line was actually laid at rather than the figure the config asked for.
    /// </para>
    /// </remarks>
    static void ClaimIndex(
        ref ScreenDraw draw, TownWorld world, Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre)
    {
        var index = world.Occupancy;
        var sagM = PathMarks.SagPx / pixelsPerMetre;

        Span<LaneClaim> slots = stackalloc LaneClaim[MostDrawnSlotsOnAWay];
        foreach (var way in index.OccupiedWays)
        {
            var arcs = world.LineOfWay(way, out var widthM);
            if (arcs.Length == 0) continue;

            var count = index.CopyTo(way, slots);
            for (var slot = 0; slot < count; slot++)
            {
                // Ground somebody is only *waiting* for is not ground anybody has (TER-5e). Drawn in the
                // asker's own colour on the lane it was refused, it reads as a band that walker holds, which
                // is the one thing about a refusal that is not true.
                if (slots[slot].IsRejected) continue;

                // Clamped to the way rather than skipped: a stretch that runs off the end of a lane is a
                // car halfway into the junction, and the half of it that is on this way is worth seeing.
                var fromM = MathF.Max(0f, slots[slot].FromM);
                var toM = MathF.Min(index.WayLengthM(way), slots[slot].ToM);
                if (toM <= fromM) continue;

                Block(
                    ref draw, arcs, fromM, toM, sagM, widthM, Colour(slots[slot]), slots[slot].Priority,
                    viewCentreM, viewSpanM);
            }
        }
    }

    /// <summary>
    /// <b>The bays that are spoken for, drawn as the bays they are</b> — washed where a body is standing in
    /// one, outlined where a leg has only claimed it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the one hold in the town that is not a piece of road</b> (GEN-4g), which is why it is the one
    /// thing on this layer that is not a stretch of a way. A bay's ways are drawn to the rear axle and stop
    /// there, so the block a parked car's claim lays covers the ground behind that axle and none of
    /// the two metres of car in front of it — the picture of a taken bay has to come off the register that
    /// takes it. What the block beside it then says is the narrower thing it has always said: which metres of
    /// the way the traffic is held off.
    /// </para>
    /// <para>
    /// <b>Washed is a body and outlined is a bay claimed</b>, and that is a difference in what is being
    /// claimed rather than a shade on one claim: somebody is standing here, against somebody is on their way
    /// and nobody else may take it. Such a claim is minutes of walking long and holds no ground at all, so drawn
    /// filled it would be a picture of a car that is not there.
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

    /// <summary>
    /// One stretch, as the piece of lane it is: a run of quads down the way at the lane's full width,
    /// butted end to end so the block bends with the ground under it — and <b>a thin bar across either
    /// end of it</b>.
    /// </summary>
    /// <remarks>
    /// <b>The bars are how a claim laid over several ways can still be read as several.</b> One body's
    /// ground is one colour (<see cref="Colour"/>), so the joints would otherwise be invisible: a lane, the
    /// join after it and the ground the body has committed to beyond its own road all butt exactly, and a
    /// continuous wash says nothing about which of them is which or where one ends. Said with shade instead,
    /// the pieces read as different <em>kinds</em> of ground, which is a stronger claim than the picture has
    /// any business making.
    /// </remarks>
    static void Block(
        ref ScreenDraw draw, scoped ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, float sagM, float widthM,
        Vector4 colour, ClaimPriority priority, Vector2 viewCentreM, Vector2 viewSpanM)
    {
        var headM = Spline.SampleAt(arcs, fromM).PositionM;
        var tailM = Spline.SampleAt(arcs, toM).PositionM;

        // The coarse cull before the fine sampling, on the two ends: a stretch is short, but there is one
        // per car in the town and only the handful on screen are worth walking.
        var reachM = ((tailM - headM).Length() * 0.5f) + widthM;
        if (!OnScreen((headM + tailM) * 0.5f, viewCentreM, viewSpanM, reachM)) return;

        var edge = colour * Edge(priority);
        PathMarks.Banded(ref draw, arcs, fromM, toM, sagM, widthM, colour * Wash(priority));
        Cap(ref draw, arcs, fromM, widthM, edge);
        Cap(ref draw, arcs, toM, widthM, edge);
    }

    /// <summary>One end of a block, as a bar square across the way at that metre.</summary>
    static void Cap(
        ref ScreenDraw draw, scoped ReadOnlySpan<ArcSeg> arcs, float alongM, float widthM, Vector4 colour)
    {
        var on = Spline.SampleAt(arcs, alongM);
        var acrossM = new Vector2(-on.Direction.Y, on.Direction.X) * (widthM * 0.5f);
        draw.LineM(on.PositionM - acrossM, on.PositionM + acrossM, BlockEdgeM, colour);
    }

    /// <summary>
    /// Whose the ground is and what it is, at the wash it is drawn at. <b>Every stretch a body holds is
    /// drawn in that body's own colour</b> (<see cref="Theme.AgentLine"/>) — the colour its line and its
    /// marks are drawn in, so a block and the line running out of the front of it read as one body's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Which is what a block is looked at for.</b> What holds one car off the next is the gap between
    /// its block and the block in front, and in one colour for the whole fleet a queue is a single band
    /// with hairlines in it — the picture cannot say whose road runs out where. A body and the road it was
    /// granted share the colour on purpose: they are one car's, the grant runs on from the tail of the
    /// body, and the sprite is drawn over the body's own end of it.
    /// </para>
    /// <para>
    /// <b>And the same colour on either network, because it is the same body.</b> A walker's band of a lane and
    /// its stretch of the pavement are one person's ground; drawn by which network the ground belongs to
    /// instead, one body was two colours and the block could not be followed from the pavement onto the road
    /// it is crossing.
    /// </para>
    /// <para>
    /// <b>The colour is only ever whose, and never how strong.</b> How much of a hold a stretch is is the
    /// wash it is drawn at (<see cref="Wash"/>) and where it stops is the bar across it
    /// (<see cref="Block"/>); said in the colour instead, one body's ground was several colours and no
    /// block could be followed off the front of the body holding it.
    /// </para>
    /// <para>
    /// <b>What is deliberately not drawn is the ground a car is driven <em>over</em>.</b> A movement's
    /// crossing points are the town's own table and are read rather than claimed (TER-5c), so the block on
    /// a join is the one car that is going down it — where a fan of claims over every way through a box said
    /// nothing about which of them anybody was on.
    /// </para>
    /// <para>
    /// What is left with a colour of its own is what belongs to no body at all: the town's own furniture.
    /// </para>
    /// </remarks>
    static Vector4 Colour(in LaneClaim slot) =>
        slot.Occupant == LaneOccupancy.Nobody ? Theme.LaneObstruction : Theme.AgentLine(slot.Occupant);
}
