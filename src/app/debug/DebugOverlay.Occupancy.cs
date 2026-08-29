using System.Numerics;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// <b>The lane index drawn on the lanes</b> — every stretch of road the town's book says is spoken for, as
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
/// <b>A reservation is drawn ahead of the car that holds it and that is not a fault.</b> It is the ground
/// the driver will come to rest on rather than the ground it is on, so at speed it stands a stopping
/// distance up the road — and the gap between one car's block and the next one's is the whole of what
/// keeps them apart.
/// </para>
/// <para>
/// <b>One wash, and the pieces told apart by a bar across the end of each.</b> A block is drawn per stretch
/// and a body's ground is regularly several of them — a lane, the join after it, the ground beyond its own
/// road it has committed to — which butt exactly and are one continuous band under one wash. So the joints
/// are marked rather than shaded: a patch of darker colour means two blocks lying over each other, and stays
/// a thing worth looking at rather than something the palette does on purpose.
/// </para>
/// <para>
/// <b>Drawn straightened back out.</b> The index holds a stretch as two numbers along a way's arclength,
/// so the layer walks the way's own arcs between them rather than joining the two ends — a block across a
/// junction join drawn as a chord is a reservation over ground nobody drives.
/// </para>
/// <para>
/// <b>At the lane's own width</b>, which is the road's declared width halved and is the same number the
/// lane's line was offset by half of (<see cref="RoadGraph.LaneWidthM"/>). It is the model's figure and
/// not a drawing one: the follower is held to a quarter of it, the pavement band starts at the edge of it
/// and the tarmac is laid to it. A block a metre wider than the ground it stands for would be a picture
/// arguing with the town.
/// </para>
/// <para>
/// <b>It is a layer of its own</b> (OBS-2c), and belongs to neither kind of body, because <b>a reservation
/// is a fact about the ground</b>: what cuts a driver's grant is as often a walker standing in the lane as
/// it is another car, and the pavement's book (<see cref="FootIndex"/>) is drawn beside the road's because
/// a body crossing is on both networks at once. Drawn with the cars it could not show either without the
/// car switch on, which is the reading the block exists for. The colour still says whose the ground is —
/// the two rosters share <see cref="Theme.AgentLine"/>, and which body a stretch belongs to is what its own
/// layer says.
/// </para>
/// <para>
/// <b>And not the node layer's either, though both are the town's.</b> The graphs are the ground the town
/// was laid with and are cached because they never move; the books are what this tick did to that ground.
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

    /// <summary>
    /// What a block is let down to. Transparent enough that the tarmac, the paint and the body standing on
    /// the stretch all read through it — the block says which ground is spoken for, and hiding the ground
    /// to say it would defeat the point.
    /// </summary>
    static readonly Vector4 Washed = new(1f, 1f, 1f, 0.22f);

    /// <summary>
    /// And what its own edges are drawn at — the same colour, up rather than down, so a joint reads as a
    /// line on the ground instead of as a change of shade.
    /// </summary>
    static readonly Vector4 Edged = new(1f, 1f, 1f, 0.5f);

    /// <summary>
    /// How thick that edge is: under the standard debug line, because it is a boundary between two pieces
    /// of one body's ground and not a thing in its own right.
    /// </summary>
    const float BlockEdgeM = PathMarks.PathLineM * 0.7f;

    static void LaneIndex(
        ref ScreenDraw draw, TownWorld world, Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre)
    {
        var index = world.Occupancy;
        var sagM = PathMarks.SagPx / pixelsPerMetre;

        Span<LaneSlot> slots = stackalloc LaneSlot[MostDrawnSlotsOnAWay];
        foreach (var way in index.OccupiedWays)
        {
            // A join belongs to two lanes and takes the width of the one it arrives on, which is the
            // ground the car crossing it is heading for; a way at a bay takes the width of the lane it
            // leaves. Which band a way is in is the town's to say (<see cref="TownWorld.LineOfWay"/>).
            var arcs = world.LineOfWay(way, out var widthM);
            if (arcs.Length == 0) continue;

            var count = index.CopyTo(way, slots);
            for (var slot = 0; slot < count; slot++)
            {
                // Ground somebody is only *waiting* for is not ground anybody has (TER-5e), and this layer
                // is whose the road is. Drawn in the asker's own colour it reads as a band that walker
                // holds, which is the one thing about a refusal that is not true; where the wait is is the
                // pavement's book, below.
                if (slots[slot].Use == LaneUse.Awaited) continue;

                // Clamped to the way rather than skipped: a stretch that runs off the end of a lane is a
                // car halfway into the junction, and the half of it that is on this way is worth seeing.
                var fromM = MathF.Max(0f, slots[slot].FromM);
                var toM = MathF.Min(index.WayLengthM(way), slots[slot].ToM);
                if (toM <= fromM) continue;

                Block(ref draw, arcs, fromM, toM, sagM, widthM, Colour(slots[slot]), viewCentreM, viewSpanM);
            }
        }
    }

    /// <summary>
    /// <b>The pavement's book drawn on the pavement</b> — the same picture of the same arithmetic over the
    /// other network: every stretch of footway a walker has taken, at the width of the lane it is a stretch
    /// of, which is half a band because the two directions have half of it each.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A walker's block is drawn ahead of the body that holds it and that is not a fault either</b>, for
    /// the same reason a driver's is — it is the ground that walker may come to rest on. It is a short block
    /// where a car's is a long one, because a walker loses its pace inside a fifth of its own body and the
    /// whole of what it asks for is the gap it keeps.
    /// </para>
    /// <para>
    /// <b>Only walkers are ever in this book</b> (TER-5c.1). A car's ground on a zebra is a stretch of the
    /// lane it is driving, drawn by <see cref="LaneIndex"/> as the block it is — where a copy of it laid on
    /// the walk drew one body twice and said the paint was a thing the traffic held rather than ground with
    /// a lane under it.
    /// </para>
    /// <para>
    /// <b>At the width the network laid, and not the width the figures asked for.</b> A stretch too tight for
    /// a full lane is walked at whatever offset fits it (<see cref="WalkingNetwork.LaneOffsetM"/>), so a
    /// block drawn at the shipped figure would be one wider than the ground it stands for — and, on a stretch
    /// squeezed to half of it, two of them lying over each other where the town has them side by side.
    /// </para>
    /// </remarks>
    static void FootIndex(
        ref ScreenDraw draw, TownWorld world, Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre)
    {
        var index = world.Footfall;
        var walking = world.Walking;
        var sagM = PathMarks.SagPx / pixelsPerMetre;

        Span<LaneSlot> slots = stackalloc LaneSlot[MostDrawnSlotsOnAWay];
        foreach (var way in index.OccupiedWays)
        {
            // A mitre belongs to the stretch it leads onto and is walked at that lane's width, which is the
            // same reading the road's side takes of a join.
            var lane = index.WayIsLane(way)
                ? index.WayIndex(way)
                : walking.TurnToEdge(index.WayIndex(way));

            var line = index.WayIsLane(way)
                ? walking.LaneOf(index.WayIndex(way))
                : walking.JoinArcs(index.WayIndex(way));

            if (line.Length == 0) continue;

            var widthM = walking.LaneOffsetM(lane) * 2f;

            var count = index.CopyTo(way, slots);
            for (var slot = 0; slot < count; slot++)
            {
                var fromM = MathF.Max(0f, slots[slot].FromM);
                var toM = MathF.Min(index.WayLengthM(way), slots[slot].ToM);
                if (toM <= fromM) continue;

                Block(ref draw, line, fromM, toM, sagM, widthM, Colour(slots[slot]), viewCentreM, viewSpanM);
            }
        }
    }

    /// <summary>
    /// <b>The bays that are spoken for, drawn as the bays they are</b> — washed where a body is standing in
    /// one, outlined where a leg has only booked it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the one hold in the town that is not a piece of road</b> (GEN-4g), which is why it is the one
    /// thing on this layer that is not a stretch of a way. A bay's ways are drawn to the rear axle and stop
    /// there, so the block the book lays for a parked car covers the ground behind that axle and none of
    /// the two metres of car in front of it — the picture of a taken bay has to come off the register that
    /// takes it. What the block beside it then says is the narrower thing it has always said: which metres of
    /// the way the traffic is held off.
    /// </para>
    /// <para>
    /// <b>Washed is a body and outlined is a booking</b>, and that is a difference in what is being claimed
    /// rather than a shade on one claim: somebody is standing here, against somebody is on their way and
    /// nobody else may take it. A booking is minutes of walking long and holds no ground at all, so drawn
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
            var bay = standingIn >= 0 ? standingIn : parking.BookingOf(car);
            if (bay < 0) continue;

            var centreM = parking.CentreM(bay);
            if (!OnScreen(centreM, viewCentreM, viewSpanM, config.ParkingSpaceLengthM)) continue;

            var colour = Theme.AgentLine(car);
            var headingRad = parking.HeadingRad(bay);
            if (standingIn >= 0)
            {
                var alongM = Heading.Unit(headingRad) * (config.ParkingSpaceLengthM * 0.5f);
                draw.BandM(centreM - alongM, centreM + alongM, 0f, config.ParkingSpaceWidthM, colour * Washed);
            }

            draw.BoxM(centreM, sizeM, headingRad, BlockEdgeM, colour * Edged);
        }
    }

    /// <summary>
    /// One stretch, as the piece of lane it is: a run of quads down the way at the lane's full width,
    /// butted end to end so the block bends with the ground under it — and <b>a thin bar across either
    /// end of it</b>.
    /// </summary>
    /// <remarks>
    /// <b>The bars are how a reservation laid over several ways can still be read as several.</b> One body's
    /// ground is one colour (<see cref="Colour"/>), so the joints would otherwise be invisible: a lane, the
    /// join after it and the ground the body has committed to beyond its own road all butt exactly, and a
    /// continuous wash says nothing about which of them is which or where one ends. Said with shade instead,
    /// the pieces read as different <em>kinds</em> of ground, which is a stronger claim than the picture has
    /// any business making.
    /// </remarks>
    static void Block(
        ref ScreenDraw draw, scoped ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, float sagM, float widthM,
        Vector4 colour, Vector2 viewCentreM, Vector2 viewSpanM)
    {
        var headM = Spline.SampleAt(arcs, fromM).PositionM;
        var tailM = Spline.SampleAt(arcs, toM).PositionM;

        // The coarse cull before the fine sampling, on the two ends: a stretch is short, but there is one
        // per car in the town and only the handful on screen are worth walking.
        var reachM = ((tailM - headM).Length() * 0.5f) + widthM;
        if (!OnScreen((headM + tailM) * 0.5f, viewCentreM, viewSpanM, reachM)) return;

        PathMarks.Banded(ref draw, arcs, fromM, toM, sagM, widthM, colour * Washed);
        Cap(ref draw, arcs, fromM, widthM, colour * Edged);
        Cap(ref draw, arcs, toM, widthM, colour * Edged);
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
    /// <b>And the same colour in either book, because it is the same body.</b> A walker's band of a lane and
    /// its stretch of the pavement are one person's ground; drawn by which network the ground belongs to
    /// instead, one body was two colours and the block could not be followed from the pavement onto the road
    /// it is crossing.
    /// </para>
    /// <para>
    /// <b>And one wash, whatever the ground is being held for.</b> A claim and a reservation are the same
    /// body's ground — the road it is driving and the ground beyond that road it is committed to — and they
    /// butt on the same way, so drawn a wash apart the pair read as two different kinds of thing standing on
    /// one street. Where each piece stops is the bar across it (<see cref="Block"/>), which is the reading
    /// the shade was standing in for and a better answer to it: <em>this</em> is where my ground ends, and
    /// not <em>that</em> is a lesser sort of ground.
    /// </para>
    /// <para>
    /// <b>What is deliberately not drawn is the ground a car is driven <em>over</em>.</b> A movement's
    /// crossing points are the town's own table and are read rather than reserved (TER-5c), so the block on
    /// a join is the one car that is going down it — where a fan of claims over every way through a box said
    /// nothing about which of them anybody was on.
    /// </para>
    /// <para>
    /// What is left with a colour of its own is what belongs to no body at all: the town's own furniture.
    /// </para>
    /// </remarks>
    static Vector4 Colour(in LaneSlot slot) =>
        slot.Occupant == LaneOccupancy.Nobody ? Theme.LaneObstruction : Theme.AgentLine(slot.Occupant);
}
