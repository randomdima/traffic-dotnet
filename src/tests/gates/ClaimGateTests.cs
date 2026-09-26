using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Gates;

/// <summary>
/// <b>What the shape of the claims on a way must be</b>, asked of every way of a town that is running rather
/// than of a staged pair: no metre of one in two claims at once (TER-4c.3), nobody holding one in two pieces
/// (TER-5c.2), and nobody holding road it was not granted (TER-4c.1). <b>Nothing held twice, nothing held
/// with a hole in it, and nothing held that was refused</b> — a way keeping all three is a way every reader of
/// the claims can be built on.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the rule the claims exist to keep</b> (TER-4c.1) — one that could grant the same metre twice
/// would be no mechanism at all — and until the claims were one table over one numbering it could not even
/// be asked: the carriageway and the footway were two records that nothing compared, so a walker held ground
/// a car was standing on and each of them was right about its own book.
/// </para>
/// <para>
/// <b>Every kind of way and both rosters</b>, because the whole point of one table is that a car on a kerb
/// and the walker beside it are the same kind of fact. What is asserted is exactly what
/// <see cref="LaneOccupancy"/> promises and nothing more: the stretches on a way are disjoint, they abut on
/// an exact metre, and a refused ask is not one of them (TER-5g — it is nobody's ground and binds nobody).
/// </para>
/// <para>
/// <b>Asked every tick and not at the end</b>, since the claims are rebuilt from nothing every tick and a
/// tick that laid two claims over one metre is a tick some driver read.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Perf)]
[Trait(Priority.Key, Priority.P0)]
[Collection(Simulation.SolverCollection.Name)]
public class ClaimGateTests
{
    /// <summary>
    /// Long enough for the queues, the junctions and the crossings to be busy at once — and <b>long enough for
    /// a car to be slowed on the approach to a box it already holds</b>, which is the shape the hold's own
    /// rule is about and which the laid city first reaches in its eleventh second.
    /// </summary>
    const int Ticks = 1_800;

    /// <summary>How many stretches one way may hold before the reading is a bound rather than the way.</summary>
    const int MostOnAWay = 64;

    [Theory]
    [MemberData(nameof(Towns.EveryMapWorthAGate), MemberType = typeof(Towns))]
    public void NoMetreOfAnyWayIsEverInTwoClaims(string map)
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Of(map), config);
        var loop = new SimLoop<TownWorld>(world, config);

        var laid = 0L;
        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance();
            laid += Disjoint(world, map, tick);
        }

        // The census, without which a town that claimed nothing would keep this perfectly.
        Assert.True(laid > 0, $"{map}: not one claim was laid in {Ticks} ticks");
    }

    /// <summary>
    /// TER-5c.2 as a gate: <b>no hold has a hole in it</b>. What one body holds of one way is the margin it
    /// keeps, itself, the road it is committed to and the ground beyond that it has been granted or stated,
    /// and between any two of those stretches there is never a metre belonging to nobody.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the shape a reader of the claims is entitled to</b>: a grant is cut at the first thing in
    /// front of it (TER-4c.1), so a body with unclaimed road inside its own hold is a body nothing can be
    /// cut at through the metres in the middle — it cannot be seen coming. A junction is where it showed: a
    /// car granted its way through a box held the crossings ahead of it and not the approach to them, and
    /// the lane between the two belonged to nobody.
    /// </para>
    /// <para>
    /// <b>What fills a gap is anybody's ground and not only the holder's own</b> (TER-4c.3). A claim is cut
    /// at what is in front of it and takes up again past it, so two of one car's stretches with the car in
    /// front of it between them are two stretches of an occupied way — and the town's own furniture fills a
    /// gap exactly as a body does, being ground the traffic is held off.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Towns.EveryMapWorthAGate), MemberType = typeof(Towns))]
    public void NoHoldHasAHoleInIt(string map)
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Of(map), config);
        var loop = new SimLoop<TownWorld>(world, config);

        var held = 0L;
        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance();
            held += Unbroken(world, map, tick);
        }

        Assert.True(held > 0, $"{map}: nobody held anything in {Ticks} ticks");
    }

    /// <summary>
    /// Every way walked once, its stretches cut into the runs of covered ground they make, and the count of
    /// the stretches looked at. <b>A holder in two of those runs is a hold with a hole in it.</b>
    /// </summary>
    static long Unbroken(TownWorld world, string map, int tick)
    {
        var held = 0L;
        Span<LaneClaim> slots = stackalloc LaneClaim[MostOnAWay];
        Span<int> run = stackalloc int[MostOnAWay];

        foreach (var way in world.Occupancy.OccupiedWays)
        {
            var count = world.Occupancy.CopyTo(way, slots);

            // The stretches come back near edge first and no two of them overlap (TER-4c.3), so the covered
            // ground is read off in one walk: a stretch beginning past where the last one ended opens a run.
            var runs = 0;
            var reachedM = float.NegativeInfinity;
            for (var at = 0; at < count; at++)
            {
                // A refused ask is nobody's ground and is laid over the very stretch the traffic holds
                // (TER-5g), so it neither opens a run nor fills one.
                if (slots[at].IsRejected)
                {
                    run[at] = -1;
                    continue;
                }

                if (slots[at].FromM > reachedM) runs++;
                run[at] = runs;
                reachedM = MathF.Max(reachedM, slots[at].ToM);
            }

            held += count;
            for (var one = 0; one < count; one++)
            {
                if (run[one] < 0 || slots[one].IsFurniture) continue;

                for (var next = one + 1; next < count; next++)
                {
                    if (run[next] == run[one] || run[next] < 0) continue;
                    if (slots[next].Occupant != slots[one].Occupant || slots[next].Of != slots[one].Of)
                    {
                        continue;
                    }

                    Assert.Fail(
                        $"{map}: at tick {tick}, {slots[one].Of} {slots[one].Occupant} holds "
                        + $"{world.Ways.KindOf(way)} way {way} in two pieces — "
                        + $"{slots[one].FromM:0.000}–{slots[one].ToM:0.000} m at "
                        + $"p{(int)slots[one].Priority} and {slots[next].FromM:0.000}–"
                        + $"{slots[next].ToM:0.000} m at p{(int)slots[next].Priority}, with road nobody "
                        + "holds between them");
                }
            }
        }

        return held;
    }

    /// <summary>
    /// TER-5c.2 across the ways of one line: <b>a car's hold runs from its tail to the far end of what it
    /// holds without a break</b>. A line is a lane, the join threaded after it and the lane past that, and
    /// the metres of it a car holds are one run however many ways they are numbered on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the same rule as <see cref="NoHoldHasAHoleInIt"/> and it is not the same question</b>, which
    /// is why it is asked separately: a hold that steps from a lane onto the join after it is two claims on
    /// two ways, and a walk of one way at a time cannot see the metres between them. What it showed was a
    /// car stopped at a crossing holding its own body, then unclaimed lane, then the whole of the box it had
    /// been granted.
    /// </para>
    /// <para>
    /// <b>The chain is anybody's ground and the finding is the car's own</b> (TER-4c.3): a claim cut at the
    /// body in front of it takes up again past that body, so what breaks the chain is road belonging to
    /// nobody and nothing else.
    /// </para>
    /// <para>
    /// <b>A millimetre of tolerance at the seams and no more.</b> A lane's far edge and the join's near edge
    /// are one metre of the world reached by two sums — the lane's own length, and the arcs of the line
    /// totalled — so the seam is exact to the last few bits rather than bitwise, while a hole is metres.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Towns.EveryMapWorthAGate), MemberType = typeof(Towns))]
    public void NoCarHoldsItsLineWithAHoleInIt(string map)
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Of(map), config);
        var loop = new SimLoop<TownWorld>(world, config);

        var held = 0L;
        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance();
            held += UnbrokenAlongTheLine(world, map, tick);
        }

        Assert.True(held > 0, $"{map}: no car held any road in {Ticks} ticks");
    }

    /// <summary>What a seam between two ways may be out by, which is the last bits of two sums.</summary>
    const float SeamM = 1e-3f;

    /// <summary>
    /// Every driving car's line walked once, its ways in the order it is driven, and the claims on them read
    /// as one chain of line metres.
    /// </summary>
    static long UnbrokenAlongTheLine(TownWorld world, string map, int tick)
    {
        var held = 0L;
        Span<LineWay> ways = stackalloc LineWay[TownWorld.MostWaysAlongALine];
        Span<LaneClaim> slots = stackalloc LaneClaim[MostOnAWay];

        for (var car = 0; car < world.Cars.Count; car++)
        {
            // A car that asked for no road holds its own body and nothing else, which is one stretch by
            // construction; where its ground begins is filled for every car, driven or not.
            if (world.Cars.ClaimToM[car] <= world.Cars.ClaimFromM[car]) continue;

            // <b>From the line's own origin where the body reaches behind it</b>: a claim begins a margin
            // behind the tail, and for a car just onto a new line those metres are on the lane it has left
            // — ground of a way this walk is not looking at rather than ground nobody holds.
            var fromM = MathF.Max(0f, world.Cars.ClaimFromM[car]);

            // <b>And out to where the hold ends and no further</b>: the road, what the car states, and a box
            // it holds. A route that comes back onto a way it has already run over — a loop round a block —
            // would otherwise have that way's claims read twice, once at the metres of each visit, and the
            // second reading is a hold in two pieces that nobody laid.
            var boxToM = world.TheMovementHoldEndsAtM(car);
            var holdToM = MathF.Max(
                MathF.Max(world.Cars.ClaimToM[car], world.Cars.StatedToM[car]),
                float.IsFinite(boxToM) ? boxToM : 0f);

            var count = world.WaysAlong(car, fromM, MathF.Min(holdToM, world.Cars.Line[car].LengthM), ways);

            // <b>A line redrawn since the claims were laid is not this question's</b> (CAR-11). The claims are
            // built at the top of a tick and a route is advanced in the decisions after it, so such a car's
            // stretches are on the ways of the line it had — read at the metres of the line it has now, they
            // are somebody else's road.
            if (count == 0 || !world.Occupancy.AlreadyHolds(ways[0].Way, ways[0].FromM, ways[0].ToM, car))
            {
                continue;
            }

            held++;

            var coveredToM = fromM;
            var brokeAtM = float.NaN;
            var openedAtM = float.NaN;

            for (var index = 0; index < count; index++)
            {
                ref readonly var way = ref ways[index];
                var onTheLineM = way.LineFromM - way.FromM;
                var on = world.Occupancy.CopyTo(way.Way, slots);

                for (var at = 0; at < on; at++)
                {
                    if (slots[at].IsRejected) continue;

                    var startsM = onTheLineM + slots[at].FromM;
                    if (startsM > coveredToM + SeamM && float.IsNaN(brokeAtM))
                    {
                        brokeAtM = coveredToM;
                        openedAtM = startsM;
                    }

                    coveredToM = MathF.Max(coveredToM, onTheLineM + slots[at].ToM);

                    if (float.IsNaN(brokeAtM) || slots[at].Occupant != car
                        || slots[at].Of != LaneRoster.Driving)
                    {
                        continue;
                    }

                    Assert.Fail(
                        $"{map}: at tick {tick} car {car} holds {startsM:0.000}–{coveredToM:0.000} m of its "
                        + $"line at p{(int)slots[at].Priority} on {world.Ways.KindOf(way.Way)} way "
                        + $"{way.Way}, with {openedAtM - brokeAtM:0.000} m of nobody's road behind it at "
                        + $"{brokeAtM:0.000}–{openedAtM:0.000} m — its body begins at {fromM:0.000} m, its "
                        + $"road ends at {world.Cars.ClaimToM[car]:0.000} m and it states to "
                        + $"{world.Cars.StatedToM[car]:0.000} m");
                }
            }
        }

        return held;
    }

    /// <summary>
    /// TER-4c.1 as a gate: <b>what a claim holds is the answer and never the question</b>. A car's road is
    /// asked for over the ways of its line before anything is answered, and once the answer is in, no stretch
    /// of that ask reaches past the metre the car was granted — on the way its body is on and on the ways ahead
    /// of the body alike.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The ways ahead of the body are the whole of what this is for.</b> A hold is one run of ways and only
    /// the first of them carries the body, so an answer written back to the stretch it can recognise leaves the
    /// far end of the ask standing on the ways past it — two cars granted one metre, which is the one thing the
    /// claims exist to make impossible.
    /// </para>
    /// <para>
    /// <b>The ground of a box is not road this car asked for</b> (TER-5c.1) and is what may reach past the
    /// answer: the junction gave it, long before the road got there (<c>TownWorld.LayTheMovement</c>). It is
    /// the way the car is crossing on, out to the far edge of the last crossing it has still to reach — and
    /// what a car holds there comes in as many stretches as the seam between its road and its box has moved
    /// (<c>TownWorld.ClaimWhatTheAnswerTook</c>), so what is asked is the metre each of them reaches and never
    /// how many there are. <b>Every other way of the line carries the road alone</b>, and there the answer is
    /// the whole of it.
    /// </para>
    /// <para>
    /// <b>And the road to a box a car can no longer stop short of reaches past the answer for the same
    /// reason the box does</b> (TER-5g.1). Such a car is going in whatever the town says, so the metres
    /// between it and the ground it was given are metres it cannot give back either — p0 on the lane as much
    /// as on the join, and bounded by that box's own far edge rather than by the grant.
    /// </para>
    /// <para>
    /// <b>Asked of the road and of nothing else</b>: a body is written wherever it stands and is nobody's to
    /// give back (TER-4c.2), a statement says where the car is going and reaches as far past the answer as the
    /// car can see (TER-5g), and a road an officer is holding is ground he was granted rather than road
    /// anybody asked for here (SRV-6) — so the walk is of the rank the committed road carries, with nothing
    /// standing in it.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Towns.EveryMapWorthAGate), MemberType = typeof(Towns))]
    public void NoCarHoldsRoadItWasNotGranted(string map)
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Of(map), config);
        var loop = new SimLoop<TownWorld>(world, config);

        var answered = 0L;
        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance();
            answered += WithinTheAnswer(world, map, tick);
        }

        Assert.True(answered > 0, $"{map}: no car was answered in {Ticks} ticks");
    }

    /// <summary>
    /// Every driving car's ask walked once — the ways of it, and the stretches of the road on each — and the
    /// count of the cars whose answer came back short of what they asked for.
    /// </summary>
    static long WithinTheAnswer(TownWorld world, string map, int tick)
    {
        var answered = 0L;
        Span<LineWay> ways = stackalloc LineWay[TownWorld.MostWaysAlongALine];
        Span<LaneClaim> slots = stackalloc LaneClaim[MostOnAWay];

        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Cars.ClaimToM[car] <= world.Cars.ClaimFromM[car]) continue;

            var grantedToM = world.GroundEndsAtM(car);
            var boxToM = world.TheMovementHoldEndsAtM(car);
            var count = world.WaysAlong(
                car, world.Cars.ClaimFromM[car], world.Cars.ClaimToM[car], ways);

            // A line redrawn since the claims were laid carries them at metres of the line before it
            // (<see cref="NoCarHoldsItsLineWithAHoleInIt"/>).
            if (count == 0 || !world.Occupancy.AlreadyHolds(ways[0].Way, ways[0].FromM, ways[0].ToM, car))
            {
                continue;
            }

            if (grantedToM < world.Cars.ClaimToM[car] - SeamM) answered++;

            for (var index = 0; index < count; index++)
            {
                ref readonly var way = ref ways[index];
                var onTheLineM = way.LineFromM - way.FromM;
                var on = world.Occupancy.CopyTo(way.Way, slots);

                for (var at = 0; at < on; at++)
                {
                    if (slots[at].Occupant != car || slots[at].Of != LaneRoster.Driving) continue;
                    if (slots[at].HasBody || slots[at].Priority != ClaimPriority.Hard) continue;

                    var endsAtM = onTheLineM + slots[at].ToM;
                    if (endsAtM <= grantedToM + SeamM) continue;
                    if (way.Way == world.Cars.MovementWay[car] && endsAtM <= boxToM + SeamM) continue;

                    // And the road up to a box this car can no longer stop short of, which is held at the
                    // box's own rank on whichever ways it runs over (TER-5g.1, TER-5e).
                    if (world.Cars.CommittedToTheBox[car] && endsAtM <= boxToM + SeamM) continue;

                    Assert.Fail(
                        $"{map}: at tick {tick} car {car} holds road to {endsAtM:0.000} m of its line on "
                        + $"{world.Ways.KindOf(way.Way)} way {way.Way}, having been granted "
                        + $"{grantedToM:0.000} m — it asked to {world.Cars.ClaimToM[car]:0.000} m, its body "
                        + $"begins at {world.Cars.ClaimFromM[car]:0.000} m and the box it holds ends at "
                        + $"{boxToM:0.000} m");
                }
            }
        }

        return answered;
    }

    /// <summary>
    /// TER-5g.1 as a gate: <b>the rung never grows along a hold</b>. A car's ground is one run from its body
    /// outward — itself, the road it is committed to, what it states, and the box it has been given at the end
    /// of that — and no stretch of it is held more strongly than a stretch behind it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A run whose rung grows has a hole in it the geometry cannot see</b> (TER-5c.2). The far piece is one
    /// every stronger movement must give way to and the near piece is one an equal right of way may simply
    /// take (TER-5g), so what the car is left holding is the far side of a junction and not the road it has to
    /// cross to reach it: continuous on the ground and broken in the arbitration, which is the same defect
    /// measured in the other of the two things a claim carries.
    /// </para>
    /// <para>
    /// <b>Out to the box and no further.</b> What a car states beyond ground it has been given is the weakest
    /// hold there is and so is the one end of a run that can never break this; the metres the rule is about
    /// are the ones between a body and a box (<c>TownWorld.LevelTheRungToTheBox</c>), and a car holding no box
    /// has nothing at the end of its road to be stronger than it.
    /// </para>
    /// <para>
    /// <b>A road an officer is holding is in nobody's run</b> (SRV-6): it is ground he was granted across a
    /// way he is not driving down, laid over whatever else is on that way rather than carried along a line.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Towns.EveryMapWorthAGate), MemberType = typeof(Towns))]
    public void NoCarHoldsGroundStrongerThanTheRoadToIt(string map)
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Of(map), config);
        var loop = new SimLoop<TownWorld>(world, config);

        var held = 0L;
        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance();
            held += Levelled(world, map, tick);
        }

        Assert.True(held > 0, $"{map}: no car held a box in {Ticks} ticks");
    }

    /// <summary>
    /// Every car holding a box walked once, from its own tail to the far edge of that box, and the count of
    /// them. <b>A stretch stronger than the weakest one behind it is a rung that grew.</b>
    /// </summary>
    static long Levelled(TownWorld world, string map, int tick)
    {
        var held = 0L;
        Span<LineWay> ways = stackalloc LineWay[TownWorld.MostWaysAlongALine];
        Span<LaneClaim> slots = stackalloc LaneClaim[MostOnAWay];

        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Cars.ClaimToM[car] <= world.Cars.ClaimFromM[car]) continue;

            var boxToM = world.TheMovementHoldEndsAtM(car);
            if (!float.IsFinite(boxToM)) continue;

            var fromM = MathF.Max(0f, world.Cars.ClaimFromM[car]);
            var toM = MathF.Min(boxToM, world.Cars.Line[car].LengthM);
            var count = world.WaysAlong(car, fromM, toM, ways);

            // A line redrawn since the claims were laid carries them at metres of the line before it
            // (<see cref="NoCarHoldsItsLineWithAHoleInIt"/>).
            if (count == 0 || !world.Occupancy.AlreadyHolds(ways[0].Way, ways[0].FromM, ways[0].ToM, car))
            {
                continue;
            }

            held++;

            var weakest = ClaimPriority.Hard;
            var weakestAtM = fromM;

            for (var index = 0; index < count; index++)
            {
                ref readonly var way = ref ways[index];
                var onTheLineM = way.LineFromM - way.FromM;
                var on = world.Occupancy.CopyTo(way.Way, slots);

                for (var at = 0; at < on; at++)
                {
                    if (slots[at].Occupant != car || slots[at].Of != LaneRoster.Driving) continue;
                    if (slots[at].IsRejected || slots[at].Priority == ClaimPriority.Closed) continue;

                    var startsM = onTheLineM + slots[at].FromM;
                    if (onTheLineM + slots[at].ToM <= fromM || startsM > toM) continue;

                    if (slots[at].Priority >= weakest)
                    {
                        if (slots[at].Priority > weakest) weakestAtM = startsM;
                        weakest = slots[at].Priority;
                        continue;
                    }

                    Assert.Fail(
                        $"{map}: at tick {tick} car {car} holds {startsM:0.000}–"
                        + $"{onTheLineM + slots[at].ToM:0.000} m of its line at p{(int)slots[at].Priority} on "
                        + $"{world.Ways.KindOf(way.Way)} way {way.Way}, behind which it holds only p"
                        + $"{(int)weakest} from {weakestAtM:0.000} m — its body begins at {fromM:0.000} m, "
                        + $"its road ends at {world.Cars.ClaimToM[car]:0.000} m and the box it holds ends at "
                        + $"{boxToM:0.000} m");
                }
            }
        }

        return held;
    }

    /// <summary>Every way of the town walked once, and the count of what was on them.</summary>
    static long Disjoint(TownWorld world, string map, int tick)
    {
        var laid = 0L;
        Span<LaneClaim> slots = stackalloc LaneClaim[MostOnAWay];
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            var count = world.Occupancy.CopyTo(way, slots);
            laid += count;

            for (var one = 0; one < count; one++)
            {
                if (slots[one].IsRejected) continue;

                for (var other = one + 1; other < count; other++)
                {
                    if (slots[other].IsRejected) continue;

                    // Touching is not overlapping: the stretches are half open, so a metre shared by two
                    // edges is the seam between them and belongs to the one in front.
                    if (slots[one].ToM <= slots[other].FromM || slots[other].ToM <= slots[one].FromM)
                    {
                        continue;
                    }

                    Assert.Fail(
                        $"{map}: at tick {tick}, {world.Ways.KindOf(way)} way {way} is claimed twice — "
                        + $"{slots[one].Of} {slots[one].Occupant} holds "
                        + $"{slots[one].FromM:0.000}–{slots[one].ToM:0.000} m at p{(int)slots[one].Priority} "
                        + $"and {slots[other].Of} {slots[other].Occupant} holds "
                        + $"{slots[other].FromM:0.000}–{slots[other].ToM:0.000} m at "
                        + $"p{(int)slots[other].Priority}");
                }
            }
        }

        return laid;
    }
}
