using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>The life of a way through a junction</b>, which the table it is counted in cannot answer for on its
/// own: when a car takes one, what it is told it holds, when it gives one back — and what holds the
/// traffic crossing a box off a body standing in one that is making no movement at all.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class JunctionClaimTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    const int Ticks = 3_600;

    public static TheoryData<string> Maps => Towns.EveryShippedMap();

    /// <summary>
    /// Every shipped map that has a junction two movements are driven across each other in. <b>A map
    /// without one owns no question about the ground in a box</b>: Zebras is mid-block crossings, which
    /// pave nothing of their own (TER-5b), and Track and Drunk are circuits nothing turns off — so on those
    /// three every claim about a body standing where two ways through meet is vacuous rather than checked.
    /// </summary>
    public static TheoryData<string> CrossedMaps
    {
        get
        {
            var maps = new TheoryData<string>();
            foreach (var map in Towns.Shipped)
            {
                if (WhereTwoMovementsCross(RoadGraph.Build(Towns.Of(map), Config)) is not null) maps.Add(map);
            }

            return maps;
        }
    }

    /// <summary>How finely two joins are measured against each other — well under the width they are compared at.</summary>
    const float StepM = 0.25f;

    /// <summary>
    /// <b>A car holds a way through exactly while it is told it does.</b> The two are one fact read by two
    /// readers — the registry, which refuses the traffic crossing it, and the driver, whose catalogue entry
    /// turns on it — and a tick in which they disagree is either a junction shut against a car nobody is
    /// going to use it, or a driver refusing itself ground it already owns.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void AWayThroughIsHeldExactlyWhileTheDriverIsToldItIs(string map)
    {
        using var world = new TownWorld(Towns.Fresh(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (!OnARoute(world, car)) continue;

                Assert.Equal(world.Cars.Crossing[car] != CarFleet.NoMovement, world.Cars.BoxIsOurs[car]);
            }
        }
    }

    /// <summary>
    /// <b>Nothing on the approach to a junction is given ground another car is already crossing on.</b>
    /// This is the property the whole mechanism exists for, and the one an eighty-per-cent conflict table
    /// used to buy by refusing four movements in five.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asked of the pair rather than of the arithmetic that produced it — the sections, the book and the
    /// order the walk happens to take are all on the far side of this — and counted as well as asserted,
    /// since a run in which no two cars were ever in one junction together proves nothing.
    /// </para>
    /// <para>
    /// <b>What a car inside a box holds is a fact and not a grant</b>, and the pair of them is deliberately
    /// left out. A driver past the point it could have stopped at goes in whatever the book says, and one
    /// that stalls in there is standing on that ground however it got there — a town that said otherwise
    /// would be describing itself wrongly. Two bodies in one box is the collision layer's question (PHY-1),
    /// not this one's; what is asked here is that nothing was ever <em>waved</em> into one.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NothingOnTheApproachIsGivenGroundAnotherCarIsCrossingOn(string map)
    {
        using var world = new TownWorld(Towns.Fresh(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var held = new bool[world.Cars.Count];
        var granted = 0;

        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            for (var car = 0; car < world.Cars.Count; car++)
            {
                var crossing = world.Cars.Crossing[car];
                var isNew = crossing >= 0 && !held[car];
                held[car] = crossing >= 0;
                if (!isNew || world.Cars.InsideTheBox[car]) continue;

                granted++;
                for (var other = 0; other < world.Cars.Count; other++)
                {
                    var theirs = world.Cars.Crossing[other];
                    if (other == car || theirs < 0 || theirs == crossing || world.Cars.InsideTheBox[other]) continue;

                    foreach (ref readonly var section in world.Roads.Crossings.Of(crossing))
                    {
                        Assert.True(
                            section.OnTurn != theirs,
                            $"{map}: car {car} was given {crossing} at tick {tick} while car {other} held "
                            + $"{theirs}, and {crossing} is driven over "
                            + $"{section.FromM:0.0}–{section.ToM:0.0} m of {theirs}");
                    }
                }
            }
        }

        Assert.Equal(world.Cars.Count > 0, granted > 0);
    }

    /// <summary>
    /// <b>Nothing waiting at a red holds the ground beyond it.</b> A phase greens the arms that do not
    /// conflict, so a claim kept by a car stopped at a bar is the phase's own decision undone — the arm
    /// with the green refused by the arm with the red, which is the duplicate SIM-7 was written about.
    /// </summary>
    /// <remarks>
    /// Asked of the car short of the paint and not of the one past it: a car that entered on a green and is
    /// still crossing when the phase turns holds its way through as a statement of fact, and is exactly the
    /// car the arms that cross it must be refused for.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NothingStoppedAtARedHoldsAWayThroughTheJunctionBeyondIt(string map)
    {
        using var world = new TownWorld(Towns.Fresh(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (!OnARoute(world, car) || world.Cars.InsideTheBox[car]) continue;
                if (world.Cars.Crossing[car] == CarFleet.NoMovement) continue;

                // A car past the point it could have stopped at is not waiting at anything: it is going
                // in, and the ground it is going over is its own until it is out the far side. At the rate
                // the profile actually brakes at on the ground under this car, which is what the town
                // sizes every other stretch of road by and what it asks this same question at.
                var brakingMps2 = CarFollower.BrakingMps2(Config, world.Cars.GroundCoefficient[car]);
                var alongMps = MathF.Max(0f, world.Cars.AlongMps[car]);
                if (world.Cars.ToTheBoxM[car] <= alongMps * alongMps / (2f * brakingMps2)) continue;

                Assert.True(
                    float.IsPositiveInfinity(world.Cars.LightAheadM[car]),
                    $"{map}: car {car} holds movement {world.Cars.Crossing[car]} with a light "
                    + $"stopping it {world.Cars.LightAheadM[car]:0.00} m short of the box");
            }
        }
    }

    /// <summary>
    /// <b>A car reserves the box it is about to cross, from the edge of it.</b> The ground under a junction
    /// is the join between the two lanes, and a reservation that reaches into one covers some of it — so a
    /// driver holds it from the moment its own stretch reaches the boundary, not from the moment the
    /// stretch has cleared the whole junction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the same walk that says what a driver can <em>see</em> (<c>WaysAlong</c>), so a car that
    /// reserves none of a box is also a car nothing standing in that box is visible to.
    /// </para>
    /// <para>
    /// <b>The book is re-laid before it is read.</b> A tick lays it in phase 2 and then hands the drivers
    /// their lines in phase 3, and a car that crossed a boundary in between has had its chain shifted under
    /// it — so a reservation taken from the finished tick would be measured against a line it was never
    /// laid over. Asked again from the state as it stands, the two are one frame.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ACarReservesTheBoxFromItsNearEdge(string map)
    {
        using var world = new TownWorld(Towns.Fresh(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        var reaching = 0;
        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            world.RebuildProximityIndex();
            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (!OnARoute(world, car) || world.Cars.Line[car].LaneCount < 2) continue;

                var chain = world.Cars.ChainOf(car);
                var slot = world.Roads.TurnSlot(chain[0], chain[1]);
                var boundaryM = world.Cars.LaneEndsOf(car)[0];

                // A car that asked for no road at all holds a stretch of no length: where its ground would
                // begin is a fact about the body and is filled for every car, driven or not.
                if (world.Cars.ReserveToM[car] <= world.Cars.ReserveFromM[car]) continue;
                if (slot < 0 || world.Cars.ReserveToM[car] <= boundaryM) continue;
                if (world.Cars.ReserveFromM[car] >= world.Cars.LaneStartsOf(car)[1]) continue;

                reaching++;
                Assert.True(
                    Holds(world, slot, car, LaneUse.Reserved),
                    $"{map}: car {car} reserves {world.Cars.ReserveFromM[car]:0.00}–"
                    + $"{world.Cars.ReserveToM[car]:0.00} m past a boundary at {boundaryM:0.00} m, "
                    + $"and join {slot} has none of it");
            }
        }

        // A town with no cars proves nothing here, and that is a fact about the town rather than a pass.
        Assert.Equal(world.Cars.Count > 0, reaching > 0);
    }

    /// <summary>
    /// <b>A crossing is given back where it is passed.</b> A section is one place two lines meet, and a car
    /// whose tail is a clearance beyond that place on its own join is not going over it again — so the box
    /// empties behind a car as it works through it, rather than at the far side.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Asked of the reservation and the claim together, because between them they are the answer.</b>
    /// What holds a crossing point is whichever of the two has got to it — the road the car is driving where
    /// that road reaches, and the claim beyond it — and which one it is changes from tick to tick as the
    /// body slows and speeds up. A test that named one of them would be asserting the seam rather than the
    /// ground.
    /// </para>
    /// <para>
    /// <b>The near edge is where the giving back can be seen, and not the far one.</b> The runs of a busy
    /// junction merge into one that spans the whole join, and a car has advanced its chain — and dropped the
    /// crossing whole — before its tail is a clearance past the far end of that. What moves while the car is
    /// in there is the near edge: the metres behind the body are let go of continuously, and both sides of
    /// that are asserted here — held from there to the far end, and gone before it.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(CrossedMaps))]
    public void GroundIsGivenBackAsTheCarWorksThroughTheBox(string map)
    {
        using var world = new TownWorld(Towns.Fresh(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        var walkedUp = 0;
        var whole = 0;
        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            world.RebuildProximityIndex();
            for (var car = 0; car < world.Cars.Count; car++)
            {
                var crossing = world.Cars.Crossing[car];
                if (crossing < 0 || !world.Cars.InsideTheBox[car]) continue;

                var tailM = TailOnTheCrossingM(world, car, crossing);
                if (float.IsNegativeInfinity(tailM)) continue;

                // The same figure the town gives ground back on: the share of the margin a body keeps around
                // itself that its own reservation carries behind its tail. It is not the clearance the
                // sections were drawn at — those answer a different question.
                var pastM = tailM - Config.CarTailMarginM;
                foreach (ref readonly var run in world.Roads.Crossings.OwnRuns(crossing))
                {
                    if (run.ToM <= pastM) continue;

                    Assert.True(
                        HoldsAllOf(world, crossing, car, MathF.Max(run.FromM, pastM), run.ToM),
                        $"{map}: car {car} at {tailM:0.00} m into {crossing} at tick {tick} has let go of "
                        + $"{MathF.Max(run.FromM, pastM):0.0}–{run.ToM:0.0} m of its own join");

                    if (pastM <= run.FromM + Tolerance)
                    {
                        whole++;
                        continue;
                    }

                    walkedUp++;
                    Assert.False(
                        HoldsAllOf(world, crossing, car, run.FromM, pastM),
                        $"{map}: car {car} at {tailM:0.00} m into {crossing} at tick {tick} still holds "
                        + $"{run.FromM:0.0}–{pastM:0.0} m of its own join, which its body is off");
                }
            }
        }

        Assert.True(walkedUp > 0 && whole > 0, $"{map}: {walkedUp} runs walked up, {whole} held whole");
    }

    /// <summary>Ground on a join is metres, and a tail is arithmetic on floats: a millimetre is not a finding.</summary>
    const float Tolerance = 1e-2f;

    /// <summary>
    /// <b>A car reserves the ways it drives and no others</b> (TER-5c). A movement is driven over the other
    /// ways through its junction, and the ground where two of them meet used to be written onto both — so a
    /// car approaching a box took a fan of joins it was never going to be on, and the box belonged to
    /// whoever aimed at it rather than to whoever was in it.
    /// </summary>
    /// <remarks>
    /// Asked of the joins a car is driven over and not of every way in the town, because that is where the
    /// marking used to land: what is left on those joins is the traffic actually going down them, which a
    /// body standing in the box (<c>LieInTheBox</c>) is and a car merely crossing it is not.
    /// </remarks>
    [Theory]
    [MemberData(nameof(CrossedMaps))]
    public void ACarTakesNoGroundOnAWayItIsOnlyDrivenOver(string map)
    {
        using var world = new TownWorld(Towns.Fresh(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        var crossed = 0;
        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            world.RebuildProximityIndex();
            for (var car = 0; car < world.Cars.Count; car++)
            {
                var crossing = world.Cars.Crossing[car];
                if (crossing < 0 || !OnARoute(world, car)) continue;

                foreach (ref readonly var section in world.Roads.Crossings.Of(crossing))
                {
                    crossed++;
                    Assert.False(
                        Holds(world, section.OnTurn, car, LaneUse.Claimed),
                        $"{map}: car {car} crossing on {crossing} has claimed ground on join "
                        + $"{section.OnTurn}, which it is driven over at {section.FromM:0.0}–"
                        + $"{section.ToM:0.0} m and will never be on");
                }
            }
        }

        Assert.True(crossed > 0, $"{map}: no car ever held a movement that crosses another");
    }

    /// <summary>
    /// <b>The end of the whole arrangement: one body to a piece of ground, wherever the two ways under it
    /// meet.</b> A reservation is a lane's, but the ground it stands for is the town's — so a grant that
    /// reached a section of a join another body already has would be two cars given the same metre of the
    /// world, each of them reading its own way and finding it empty.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asked of the grant and never of the ask. What goes into the book is the road a car is committed to
    /// and the cut is taken off it afterwards, so a stretch read back out of the book has to be trimmed by
    /// the authority its holder was given before it says anything about who may be where.
    /// </para>
    /// <para>
    /// <b>A car inside the box is left out, on the asking side only.</b> What a body in there holds is a
    /// statement of fact rather than a grant — a driver past the point it could have stopped at goes in
    /// whatever the book says — while on the answering side it is exactly the body everything crossing that
    /// ground must be refused for.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(CrossedMaps))]
    public void NoGrantReachesGroundAnotherBodyHasOnACrossingWay(string map)
    {
        using var world = new TownWorld(Towns.Fresh(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var index = world.Occupancy;

        var reaching = 0;
        Span<LaneSlot> mine = stackalloc LaneSlot[32];
        Span<LaneSlot> theirs = stackalloc LaneSlot[32];
        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            world.RebuildProximityIndex();
            foreach (var way in index.OccupiedWays)
            {
                if (index.WayIsLane(way)) continue;

                var count = index.CopyTo(way, mine);
                for (var at = 0; at < count; at++)
                {
                    var asked = mine[at];
                    if (asked.Use != LaneUse.Reserved || asked.Of != LaneRoster.Driving) continue;
                    if (world.Cars.InsideTheBox[asked.Occupant]) continue;

                    var grantedToM = GrantedToM(world, asked);
                    foreach (ref readonly var section in world.Roads.Crossings.Of(index.WayIndex(way)))
                    {
                        // Reaching a section's near edge is the grant being cut at it, which is the whole
                        // point: what is asserted is that nothing was granted road *past* one.
                        if (section.MineFromM < asked.FromM
                            || section.MineFromM >= grantedToM - Tolerance)
                        {
                            continue;
                        }

                        reaching++;
                        var over = index.CopyTo(index.WayOfTurn(section.OnTurn), theirs);
                        for (var other = 0; other < over; other++)
                        {
                            if (theirs[other].Occupant == asked.Occupant && theirs[other].Of == asked.Of) continue;
                            if (theirs[other].ToM <= section.FromM || theirs[other].FromM >= section.ToM) continue;

                            Assert.Fail(
                                $"{map}: car {asked.Occupant} is granted to {grantedToM:0.00} m of way "
                                + $"{way} at tick {tick}, past a section at {section.MineFromM:0.00} m that "
                                + $"runs over {section.FromM:0.0}–{section.ToM:0.0} m of join "
                                + $"{section.OnTurn}, which {theirs[other].Of} {theirs[other].Occupant} "
                                + $"holds from {theirs[other].FromM:0.00} to {theirs[other].ToM:0.00} m");
                        }
                    }
                }
            }
        }

        Assert.True(reaching > 0, $"{map}: no car was ever granted road as far as one of its own crossings");
    }

    /// <summary>
    /// Where a grant actually ends on the way it was laid on. <b>What goes into the book is what the car
    /// asked for</b> — the cut is taken off it afterwards, so a stretch drawn from the book has to be read
    /// back through the grant the driver was given.
    /// </summary>
    /// <remarks>
    /// Off the ask and never off the car's progress: the ask is what the index was laid from, and the body
    /// has driven a tick further since. A way's metres and a line's run at the same rate, so an overshoot
    /// measured on the line is the same number of metres of the way.
    /// </remarks>
    static float GrantedToM(TownWorld world, in LaneSlot asked)
    {
        var car = asked.Occupant;
        var noseM = world.Cars.ReserveFromM[car] + Config.CarTailMarginM + Config.Car.LengthM;
        var overshotM = world.Cars.ReserveToM[car] - (world.Cars.AuthorityM[car] + noseM);

        return asked.ToM - MathF.Max(0f, overshotM);
    }

    /// <summary>
    /// <b>Whether this car holds every metre of one stretch of one join</b>, across all the stretches it has
    /// there — its road and its claim between them, which is what the traffic crossing that ground meets.
    /// </summary>
    /// <remarks>
    /// The stretches come back near edge first (<see cref="LaneOccupancy.CopyTo"/>), so the cover is walked
    /// rather than searched: anything left uncovered is a gap the far side would be driven through.
    /// </remarks>
    static bool HoldsAllOf(TownWorld world, int slot, int car, float fromM, float toM)
    {
        Span<LaneSlot> slots = stackalloc LaneSlot[32];
        var count = world.Occupancy.CopyTo(world.Occupancy.WayOfTurn(slot), slots);
        var reachedM = fromM;
        for (var at = 0; at < count && reachedM < toM; at++)
        {
            ref readonly var taken = ref slots[at];
            if (taken.Occupant != car || taken.Of != LaneRoster.Driving) continue;
            if (taken.FromM > reachedM + Tolerance) break;

            reachedM = MathF.Max(reachedM, taken.ToM);
        }

        return reachedM >= toM - Tolerance;
    }

    /// <summary>
    /// Where this car's tail stands in the own metres of the join it is crossing, or negative infinity
    /// where its line is not on that join — the town's own arithmetic, asked from outside it.
    /// </summary>
    static float TailOnTheCrossingM(TownWorld world, int car, int crossing)
    {
        var chain = world.Cars.ChainOf(car);
        var starts = world.Cars.LaneStartsOf(car);
        var progressM = world.Cars.ProgressM[car];

        var ahead = 0;
        while (ahead < world.Cars.Line[car].LaneCount - 1 && progressM >= starts[ahead + 1]) ahead++;
        if (ahead + 1 >= world.Cars.Line[car].LaneCount) return float.NegativeInfinity;
        if (world.Roads.TurnSlot(chain[ahead], chain[ahead + 1]) != crossing) return float.NegativeInfinity;

        return progressM + Config.CarNoseAheadOfAxleM - Config.Car.LengthM - world.Cars.LaneEndsOf(car)[ahead];
    }

    /// <summary>
    /// <b>A body standing in a junction is on every join of it that runs under the body</b> — which is the
    /// whole of what holds the traffic crossing the box off it. What a car is making is given back the
    /// moment nobody is driving it, so the registry has nothing to say about a wreck; the ground it is
    /// lying on is what is left, and a body left off the joins is one no crossing driver can see.
    /// </summary>
    [Theory]
    [MemberData(nameof(CrossedMaps))]
    public void ABodyStandingInAJunctionIsOnTheJoinsThatCrossIt(string map)
    {
        using var world = new TownWorld(Towns.Fresh(map), Config);
        var (slot, crossing, atM) = WhereTwoMovementsCross(world.Roads)!.Value;
        var car = ACarOffTheParking(world, map);

        world.Cars.Driven[car] = false;
        world.Cars.Broken[car] = true;
        world.Cars.PositionM[car] = atM;
        world.Cars.VelocityMps[car] = Vector2.Zero;
        world.RebuildProximityIndex();

        // A wreck is making nothing, so the registry has nothing to say about it — which is the whole
        // reason the ground under it has to.
        Assert.Equal(CarFleet.NoMovement, world.Cars.Crossing[car]);
        Assert.True(Holds(world, slot, car), $"{map}: join {slot}, which it is lying on, does not have it");
        Assert.True(Holds(world, crossing, car), $"{map}: join {crossing}, which crosses that one, does not have it");
    }

    /// <summary>
    /// <b>And on no join it is standing clear of, wherever in the box it is put.</b> A body is put on a way
    /// by projecting it onto that way's line, and a projection is clamped to the way's own ends — so a body
    /// merely lined up with a join answered at that end with no offset across the line at all, however far
    /// up the road it really stood. Since a body in a junction is asked of <em>every</em> join at the node,
    /// what that put in the book was one car shutting movements on the far side of a box it was nowhere
    /// near, which is the same defect as a table of verdicts arrived at from the geometry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Swept over the whole disc rather than asked at one place, because which join a clamp mislays a body
    /// onto is a fact about that junction's shape: the failing places are wherever a body stands in line
    /// with some join's end, and no single spot is the one that finds them.
    /// </para>
    /// <para>
    /// The bar is the plain distance from the body to the join's whole line, which is a different sum from
    /// the band arithmetic that laid it — half the widest lane and half a body across, half a body along.
    /// A body a clamp put on a join misses it by metres rather than by centimetres.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(CrossedMaps))]
    public void NoBodyIsOnAJoinItStandsClearOf(string map)
    {
        using var world = new TownWorld(Towns.Fresh(map), Config);
        var (_, _, atM) = WhereTwoMovementsCross(world.Roads)!.Value;
        var car = ACarOffTheParking(world, map);

        world.Cars.Driven[car] = false;
        world.Cars.Broken[car] = true;
        world.Cars.VelocityMps[car] = Vector2.Zero;

        var acrossM = (WidestLaneM(world.Roads) * 0.5f) + (Config.Car.WidthM * 0.5f);
        var endM = Config.Car.LengthM * 0.5f;
        var reachM = MathF.Sqrt((acrossM * acrossM) + (endM * endM)) + StepM;
        var overM = Config.IntersectionReachM;
        var lying = 0;

        Span<LaneSlot> slots = stackalloc LaneSlot[32];
        for (var down = -overM; down <= overM; down += endM)
        {
            for (var across = -overM; across <= overM; across += endM)
            {
                world.Cars.PositionM[car] = atM + new Vector2(across, down);
                world.RebuildProximityIndex();

                foreach (var way in world.Occupancy.OccupiedWays)
                {
                    if (world.Occupancy.WayIsLane(way)) continue;

                    var join = world.Occupancy.WayIndex(way);
                    var count = world.Occupancy.CopyTo(way, slots);
                    for (var at = 0; at < count; at++)
                    {
                        if (slots[at].Occupant != car || slots[at].Use != LaneUse.Obstruction) continue;

                        lying++;
                        var apartM = ToChainM(
                            world.Roads.JoinArcs(join), world.Roads.JoinLengthM(join),
                            world.Cars.PositionM[car]);

                        Assert.True(
                            apartM <= reachM,
                            $"{map}: a body at {across:0.0},{down:0.0} m from {atM} lies on join {join} and "
                            + $"stands {apartM:0.00} m off its line, past the {reachM:0.00} m a body covers");
                    }
                }
            }
        }

        Assert.True(lying > 0, $"{map}: the body was never laid on a join anywhere in the box");
    }

    static float WidestLaneM(RoadGraph roads)
    {
        var mostM = 0f;
        for (var lane = 0; lane < roads.LaneCount; lane++) mostM = MathF.Max(mostM, roads.LaneWidthM[lane]);

        return mostM;
    }

    static float ToChainM(ReadOnlySpan<ArcSeg> arcs, float lengthM, Vector2 pointM)
    {
        var leastM = float.PositiveInfinity;
        for (var alongM = 0f; alongM <= lengthM; alongM += StepM)
        {
            leastM = MathF.Min(leastM, (Spline.SampleAt(arcs, alongM).PositionM - pointM).Length());
        }

        return leastM;
    }

    /// <summary>Whether the car is one the road is driving down a route of its own, which is what writes both fields.</summary>
    static bool OnARoute(TownWorld world, int car) =>
        world.Cars.Driven[car] && !world.Cars.Broken[car] && world.Cars.Line[car].LaneCount > 0;

    /// <summary>Whether this join's own way has a stretch of this car on it, of the kind asked for.</summary>
    static bool Holds(TownWorld world, int slot, int car, LaneUse use = LaneUse.Obstruction)
    {
        Span<LaneSlot> slots = stackalloc LaneSlot[32];
        var count = world.Occupancy.CopyTo(world.Occupancy.WayOfTurn(slot), slots);
        for (var at = 0; at < count; at++)
        {
            if (slots[at].Occupant == car && slots[at].Use == use) return true;
        }

        return false;
    }

    /// <summary>
    /// A car standing on the road rather than in a bay, which is the one a body can be put anywhere: a bay
    /// stands off the kerb and what is in one is deliberately left out of the road's book.
    /// </summary>
    static int ACarOffTheParking(TownWorld world, string map)
    {
        var loop = new SimLoop<TownWorld>(world, Config);
        for (var tick = 0; tick < Ticks; tick++)
        {
            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (world.Parking.BayOf(car) < 0) return car;
            }

            loop.Advance(1);
        }

        throw new InvalidOperationException($"{map} kept every one of its cars in a bay for a minute");
    }

    /// <summary>
    /// Two movements through one junction that are driven over each other, and a place on the ground they
    /// share — read straight off the table, which is the only thing that has an opinion about it.
    /// </summary>
    static (int Slot, int Crossing, Vector2 AtM)? WhereTwoMovementsCross(RoadGraph roads)
    {
        for (var slot = 0; slot < roads.TurnCount; slot++)
        {
            foreach (ref readonly var section in roads.Crossings.Of(slot))
            {
                var arcs = roads.JoinArcs(section.OnTurn);
                if (arcs.Length == 0) continue;

                return (section.OnTurn, slot, Spline.SampleAt(arcs, (section.FromM + section.ToM) * 0.5f).PositionM);
            }
        }

        return null;
    }
}
