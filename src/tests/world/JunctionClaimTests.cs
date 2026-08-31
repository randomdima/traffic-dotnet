using System.Collections.Concurrent;
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

    /// <summary>
    /// Two minutes of the town. <b>A minute is not long enough to witness the exchange from both ends</b>
    /// (<see cref="ACrossingIsTakenFromAMovementThatGivesWayToIt"/>): a fleet whose cars accelerate and
    /// brake at their own rates (CAR-11) arrives at the junctions less regularly than one nominal car
    /// repeated, so the rarer half of the right-of-way trade needs a longer watch to turn up at all.
    /// </summary>
    const int Ticks = 7_200;

    public static TheoryData<string> Maps => Towns.EveryTown();

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
    /// <b>One run of one map, watched by every claim below at once.</b> Each of them holds the first tick
    /// it was broken on, or null, beside the census that says the run had anything to say about it at all.
    /// </summary>
    /// <remarks>
    /// A claim is recorded rather than thrown on, so one broken claim still lets the other five be answered
    /// off the same minute — and the field is written once, on the first tick that broke it, so what a red
    /// test carries is the earliest moment and not the last.
    /// </remarks>
    sealed class Watched(int cars)
    {
        /// <summary>Which cars held a way through as of the previous tick, so a grant can be told from a hold.</summary>
        public readonly bool[] Holding = new bool[cars];

        /// <summary>
        /// <b>How many of them were ever seen driving a route</b>, which is what says the run had anything
        /// to ask about a junction. It is not the size of the fleet: a map may stand cars that never drive
        /// the network at all — the skidpad holds every one of its wheels over — and counting those would
        /// read as a town whose junctions granted nothing.
        /// </summary>
        public int Drivers { get; private set; }

        readonly bool[] _drove = new bool[cars];

        public void Drove(int car)
        {
            if (_drove[car]) return;

            _drove[car] = true;
            Drivers++;
        }

        public string? Disagreed, Waved, PastARed, MissedTheNearEdge, KeptWhatItPassed, TookGroundItCrosses,
            Overlapped, CutFromBehind;

        public int Granted, Reaching, WalkedUp, Whole, Crossed, AsFarAsASection, HeldByAClaim;

        /// <summary>How many crossings were given to a movement over the ground a weaker one was holding (TER-5e).</summary>
        public int TakenFromAWeakerMovement;

        /// <summary>And how many were taken straight back off one by a movement that outranks it, in the same walk.</summary>
        public int GivenUpToAStrongerMovement;
    }

    static readonly ConcurrentDictionary<string, Watched> Runs = new();

    /// <summary>
    /// The run this map's claims are all read off, taken once. <b>Six claims over six maps was thirty-six
    /// minutes of town for six minutes of question</b>, and every one of them was watching the same six
    /// towns do the same thing.
    /// </summary>
    static Watched Of(string map) => Runs.GetOrAdd(map, Watch);

    /// <summary>
    /// A minute of the town, with the book re-laid before it is read each tick — which is what
    /// <see cref="ACarReservesTheBoxFromItsNearEdge"/> needs and what the claims that only read the fleet
    /// are indifferent to.
    /// </summary>
    static Watched Watch(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var found = new Watched(world.Cars.Count);

        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            world.RebuildProximityIndex();

            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (OnARoute(world, car)) found.Drove(car);
            }

            TheDriverIsToldWhatTheRegistryHolds(world, map, tick, found);
            NothingIsWavedOntoGroundAnotherCarIsCrossing(world, map, tick, found);
            NothingStoppedAtARedHoldsTheGroundBeyondIt(world, map, tick, found);
            AReservationReachesIntoTheBox(world, map, found);
            TheBoxEmptiesBehindTheBody(world, map, tick, found);
            NoGroundIsTakenOnAWayOnlyDrivenOver(world, map, found);
            NoGrantReachesGroundAnotherBodyHas(world, map, tick, found);
            NoClaimCutsAGrantFromBehindTheNose(world, map, tick, found);
        }

        return found;
    }

    /// <summary>
    /// <b>A car holds a way through exactly while it is told it does.</b> The two are one fact read by two
    /// readers — the registry, which refuses the traffic crossing it, and the driver, whose catalogue entry
    /// turns on it — and a tick in which they disagree is either a junction shut against a car nobody is
    /// going to use it, or a driver refusing itself ground it already owns.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void AWayThroughIsHeldExactlyWhileTheDriverIsToldItIs(string map) =>
        Assert.Null(Of(map).Disagreed);

    /// <summary>What <see cref="AWayThroughIsHeldExactlyWhileTheDriverIsToldItIs"/> watches for.</summary>
    static void TheDriverIsToldWhatTheRegistryHolds(TownWorld world, string map, int tick, Watched found)
    {
        if (found.Disagreed is not null) return;

        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (!OnARoute(world, car)) continue;
            if (MovementOf(world, car) != CarFleet.NoWay == world.Cars.BoxIsOurs[car]) continue;

            found.Disagreed =
                $"{map}: at tick {tick} the registry has car {car} on movement "
                + $"{MovementOf(world, car)} and the driver was told {world.Cars.BoxIsOurs[car]}";
            return;
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
        var run = Of(map);

        Assert.Null(run.Waved);
        Assert.Equal(run.Drivers > 0, run.Granted > 0);
    }

    /// <summary>
    /// <b>And a movement <em>is</em> given ground a weaker one was holding</b> (TER-5e). The claim above is
    /// the safety half and passes in a town where the ranks are never compared at all; this is the other
    /// half, and it is what says the right of way is running rather than merely written down.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Counted over the shipped towns rather than one of them</b>, because the exchange is a coincidence
    /// of two streams: a turn across the oncoming traffic and the traffic it crosses share one green phase,
    /// and which pair of cars meets in it is a fact about a fleet whose cars arrive at their own speeds
    /// (CAR-11).
    /// </para>
    /// <para>
    /// <b>One direction of the trade is asserted and the other is only counted.</b> The takeback is the
    /// behaviour; <see cref="Watched.GivenUpToAStrongerMovement"/> is the same event seen from the car that
    /// lost the ground <em>in the same walk that granted it</em>, which is a reporting artefact of judging
    /// a tick from its end. It stopped arising in the shipped towns when commitment began to be judged a
    /// decision ahead (<c>TownWorld.JunctionStopM</c>): a car near enough to be traded against is now
    /// generally committed already, so the pair no longer lands inside one walk.
    /// </para>
    /// </remarks>
    [Fact]
    public void ACrossingIsTakenFromAMovementThatGivesWayToIt()
    {
        var taken = 0;
        foreach (var map in Towns.Shipped) taken += Of(map).TakenFromAWeakerMovement;

        Assert.True(taken > 0, "no car in the shipped towns took a crossing off a movement that gives way to it");
    }

    /// <summary>What <see cref="NothingOnTheApproachIsGivenGroundAnotherCarIsCrossingOn"/> watches for.</summary>
    /// <remarks>
    /// <b>"Already" is measured from before the walk that granted, not from the end of the tick it landed
    /// in.</b> The town writes both halves of a tick's crossings in one walk, so the fleet read afterwards
    /// carries movements that were nobody's at the moment the one being judged was handed over — and a
    /// reading that took the end of the tick for the state of it would call the second grant of a pair a
    /// wave into the first.
    /// </remarks>
    static void NothingIsWavedOntoGroundAnotherCarIsCrossing(TownWorld world, string map, int tick, Watched found)
    {
        var heldBefore = (bool[])found.Holding.Clone();
        for (var car = 0; car < world.Cars.Count; car++)
        {
            var crossing = MovementOf(world, car);
            var isNew = crossing >= 0 && !found.Holding[car];
            found.Holding[car] = crossing >= 0;
            if (!isNew || world.Cars.InsideTheBox[car]) continue;

            found.Granted++;
            var waved = WhoWasAlreadyCrossingIt(world, map, tick, car, crossing, heldBefore, found);
            found.Waved ??= waved;
        }
    }

    /// <summary>
    /// The car that already held ground the one being waved in is about to be driven over, or nothing —
    /// which is the ordinary answer and the reason this is asked only of the tick a grant is made on.
    /// </summary>
    static string? WhoWasAlreadyCrossingIt(
        TownWorld world, string map, int tick, int car, int crossing, bool[] heldBefore, Watched found)
    {
        for (var other = 0; other < world.Cars.Count; other++)
        {
            var theirs = MovementOf(world, other);
            if (other == car || theirs < 0 || theirs == crossing || world.Cars.InsideTheBox[other]) continue;

            // A movement this one has the right of way over gives its ground up rather than being waited
            // behind (TER-5e) — while its holder can still stop short of the box. Past that it is
            // committed, and a right of way orders who waits and never who is driven into.
            //
            // <b>Asked without the town's own decision's lead</b> (CanStillStopShortOfTheBox): what makes
            // taking the ground unsafe is the car being unable to stop, and the town commits a car a
            // decision <em>before</em> that so the book carrying its rank is never behind the road. Asked
            // with the lead in it, every grant landing inside that lead reads as a car driven into — a
            // picture of the safety margin rather than of a town that has run out of one.
            var weaker = RankOf(world, other, theirs) < RankOf(world, car, crossing)
                         && CanStillStopShortOfTheBox(world, other);

            // And the same rule read from the other end. A movement that outranks this one and was on
            // nothing when the walk began was granted <em>after</em> this one in that same walk: the ground
            // was taken off this car rather than handed to it over that one, and this car gives it back on
            // its own next ask (TER-5e). Judged off the end of the tick the pair looks like a wave into a
            // car that was crossing, and the car that was crossing had not been given anything yet.
            var stronger = RankOf(world, other, theirs) > RankOf(world, car, crossing)
                           && !heldBefore[other];

            foreach (ref readonly var section in world.Roads.Crossings.Of(world.Roads.WayOfTurn(crossing)))
            {
                if (world.Roads.TurnOfWay(section.OnWay) != theirs) continue;
                if (stronger)
                {
                    found.GivenUpToAStrongerMovement++;
                    break;
                }

                if (weaker)
                {
                    found.TakenFromAWeakerMovement++;
                    break;
                }

                return $"{map}: car {car} was given {crossing} at tick {tick} while car {other} held "
                       + $"{theirs} {world.Cars.ToTheBoxM[other]:0.0} m off a box it needs "
                       + $"{StoppingM(world, other):0.0} m to stop short of, and {crossing} is driven over "
                       + $"{section.FromM:0.0}–{section.ToM:0.0} m of {theirs}";
            }
        }

        return null;
    }

    /// <summary>
    /// <b>The rank this car actually holds its movement at</b> (TER-5e): the turn's own, and
    /// <see cref="RightOfWay.Emergency"/> for anybody answering a call (AMB-4, EVA-4, SRV-6). <b>It is the
    /// car's and not the turn's</b>, because a blue light is exactly a rank a movement does not otherwise
    /// carry — read off the turn alone, every claim a rescue takes off ordinary traffic reads as a car
    /// waved into a junction.
    /// </summary>
    static RightOfWay RankOf(TownWorld world, int car, int turn) =>
        world.Cars.BlueLight[car] ? RightOfWay.Emergency : world.Roads.RightOfWayOfTurn(turn);

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
    public void NothingStoppedAtARedHoldsAWayThroughTheJunctionBeyondIt(string map) =>
        Assert.Null(Of(map).PastARed);

    /// <summary>What <see cref="NothingStoppedAtARedHoldsAWayThroughTheJunctionBeyondIt"/> watches for.</summary>
    static void NothingStoppedAtARedHoldsTheGroundBeyondIt(TownWorld world, string map, int tick, Watched found)
    {
        if (found.PastARed is not null) return;

        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (!OnARoute(world, car) || world.Cars.InsideTheBox[car]) continue;
            if (MovementOf(world, car) == CarFleet.NoWay) continue;

            // A car past the point it could have stopped at is not waiting at anything: it is going
            // in, and the ground it is going over is its own until it is out the far side. At the rate
            // the profile actually brakes at on the ground under this car, which is what the town
            // sizes every other stretch of road by and what it asks this same question at.
            if (CommittedAsTheTownReadsIt(world, car)) continue;
            if (float.IsPositiveInfinity(world.Cars.LightAheadM[car])) continue;

            found.PastARed =
                $"{map}: car {car} holds movement {MovementOf(world, car)} with a light "
                + $"stopping it {world.Cars.LightAheadM[car]:0.00} m short of the box at tick {tick}";
            return;
        }
    }

    /// <summary>
    /// <b>Whether the town is treating this car as committed to the box</b> — the same reading
    /// <see cref="CarFleet.CommittedToTheBox"/> is written from, recomputed here rather than read, so that
    /// what is asserted does not come out of the arithmetic that produced it.
    /// </summary>
    /// <remarks>
    /// It carries <b>a decision's lead</b>: the book that hands a car's rank to everybody else is laid
    /// before this tick's drivers decide, so a car that will be past stopping by the time the ranks are
    /// next compared counts as committed now.
    /// </remarks>
    static bool CommittedAsTheTownReadsIt(TownWorld world, int car)
    {
        var alongMps = MathF.Max(0f, world.Cars.AlongMps[car]);
        return world.Cars.ToTheBoxM[car] - (alongMps * Config.CarReactionS) <= StoppingM(world, car);
    }

    /// <summary>
    /// <b>And whether it can stop at all</b>, with no lead in it. <b>The two are different questions and
    /// the lead between them is the whole of the difference</b>: what makes ground unsafe to take is a car
    /// that <em>cannot</em> stop, and the town commits a car a decision before that on purpose. Read the
    /// led answer where the question is what the town believes, and this one where it is what the road
    /// allows.
    /// </summary>
    static bool CanStillStopShortOfTheBox(TownWorld world, int car) =>
        world.Cars.ToTheBoxM[car] > StoppingM(world, car);

    /// <summary>
    /// How much road this car needs to come to rest, <b>at the rate the profile actually brakes at on the
    /// ground under it</b> — which is what the town sizes every other stretch of road by.
    /// </summary>
    static float StoppingM(TownWorld world, int car)
    {
        var brakingMps2 = CarFollower.BrakingMps2(
            Config, world.Cars.BuildOf(car), world.Cars.GroundCoefficient[car]);
        var alongMps = MathF.Max(0f, world.Cars.AlongMps[car]);
        return alongMps * alongMps / (2f * brakingMps2);
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
        var run = Of(map);

        Assert.Null(run.MissedTheNearEdge);

        // A town where nothing ever drove a route proves nothing here, and that is a fact about the town
        // rather than a pass.
        Assert.Equal(run.Drivers > 0, run.Reaching > 0);
    }

    /// <summary>What <see cref="ACarReservesTheBoxFromItsNearEdge"/> watches for.</summary>
    static void AReservationReachesIntoTheBox(TownWorld world, string map, Watched found)
    {
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (!OnARoute(world, car) || world.Cars.Line[car].LaneCount < 2) continue;

            var chain = world.Cars.ChainOf(car);
            var slot = world.Roads.TurnSlot(chain[0], chain[1]);
            var boundaryM = world.Cars.LaneEndsOf(car)[0];

            // A car that asked for no road at all holds a stretch of no length: where its ground would
            // begin is a fact about the body and is filled for every car, driven or not.
            if (world.Cars.ReserveToM[car] <= world.Cars.ReserveFromM[car]) continue;

            // <b>Asked of the road the car got and not of the road it asked for</b> (TER-4c.1): the book
            // holds the answer, so a car whose ask reached the boundary and whose grant did not has nothing
            // in the box and is owed nothing there.
            if (slot < 0 || world.GroundEndsAtM(car) <= boundaryM) continue;
            if (world.Cars.ReserveFromM[car] >= world.Cars.LaneStartsOf(car)[1]) continue;

            // A place cut into a road (GEN-4h) is a boundary with no box behind it: its two lanes meet at
            // a point, so the join between them has no metres and a reservation over it holds nothing —
            // which is the whole of what "no ground is lost to a place" means.
            if (world.Roads.JoinLengthM(slot) <= 0f) continue;

            found.Reaching++;
            if (found.MissedTheNearEdge is not null) continue;

            // <b>The road or the claim beyond it, because the seam between them is not the question</b>
            // (<c>ClaimWhatTheAnswerTook</c>). What reaches into the box is the union of the two, and which
            // side of it a given metre falls on moves with the answer the car was given this tick.
            if (Holds(world, slot, car, LaneUse.Reserved) || Holds(world, slot, car, LaneUse.Claimed)) continue;

            found.MissedTheNearEdge =
                $"{map}: car {car} reserves {world.Cars.ReserveFromM[car]:0.00}–"
                + $"{world.GroundEndsAtM(car):0.00} m past a boundary at {boundaryM:0.00} m, "
                + $"and join {slot} has none of it";
        }
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
        var run = Of(map);

        Assert.Null(run.KeptWhatItPassed);
        Assert.True(
            run.WalkedUp > 0 && run.Whole > 0,
            $"{map}: {run.WalkedUp} runs walked up, {run.Whole} held whole");
    }

    /// <summary>What <see cref="GroundIsGivenBackAsTheCarWorksThroughTheBox"/> watches for.</summary>
    static void TheBoxEmptiesBehindTheBody(TownWorld world, string map, int tick, Watched found)
    {
        for (var car = 0; car < world.Cars.Count; car++)
        {
            var crossing = MovementOf(world, car);
            if (crossing < 0 || !world.Cars.InsideTheBox[car]) continue;

            var tailM = TailOnTheCrossingM(world, car, crossing);
            if (float.IsNegativeInfinity(tailM)) continue;

            // The same figure the town gives ground back on: the share of the margin a body keeps around
            // itself that its own reservation carries behind its tail. It is not the clearance the
            // sections were drawn at — those answer a different question.
            var pastM = tailM - world.Cars.BuildOf(car).TailMarginM;
            foreach (ref readonly var run in world.Roads.Crossings.OwnRuns(world.Roads.WayOfTurn(crossing)))
            {
                if (run.ToM <= pastM) continue;

                if (found.KeptWhatItPassed is null
                    && !HoldsAllOf(world, crossing, car, MathF.Max(run.FromM, pastM), run.ToM))
                {
                    found.KeptWhatItPassed =
                        $"{map}: car {car} at {tailM:0.00} m into {crossing} at tick {tick} has let go of "
                        + $"{MathF.Max(run.FromM, pastM):0.0}–{run.ToM:0.0} m of its own join";
                }

                if (pastM <= run.FromM + Tolerance)
                {
                    found.Whole++;
                    continue;
                }

                found.WalkedUp++;
                if (found.KeptWhatItPassed is not null || !HoldsAllOf(world, crossing, car, run.FromM, pastM)) continue;

                found.KeptWhatItPassed =
                    $"{map}: car {car} at {tailM:0.00} m into {crossing} at tick {tick} still holds "
                    + $"{run.FromM:0.0}–{pastM:0.0} m of its own join, which its body is off";
            }
        }
    }

    /// <summary>Ground on a join is metres, and a tail is arithmetic on floats: a millimetre is not a finding.</summary>
    const float Tolerance = 1e-2f;

    /// <summary>
    /// <b>A car reserves the ways it drives and no others</b> (TER-5c). A movement crosses the other ways
    /// through its junction, and writing it onto the ground where two of them meet would give a car
    /// approaching a box a fan of joins it is never going to be on — the box would belong to whoever aimed
    /// at it rather than to whoever is in it.
    /// </summary>
    /// <remarks>
    /// Asked of the joins a car is driven over and not of every way in the town, because that is where such
    /// a marking would land: what is left on those joins is the traffic actually going down them, which a
    /// body standing in the box (<c>LieInTheBox</c>) is and a car merely crossing it is not.
    /// </remarks>
    [Theory]
    [MemberData(nameof(CrossedMaps))]
    public void ACarTakesNoGroundOnAWayItIsOnlyDrivenOver(string map)
    {
        var run = Of(map);

        Assert.Null(run.TookGroundItCrosses);
        Assert.True(run.Crossed > 0, $"{map}: no car ever held a movement that crosses another");
    }

    /// <summary>What <see cref="ACarTakesNoGroundOnAWayItIsOnlyDrivenOver"/> watches for.</summary>
    static void NoGroundIsTakenOnAWayOnlyDrivenOver(TownWorld world, string map, Watched found)
    {
        for (var car = 0; car < world.Cars.Count; car++)
        {
            var crossing = MovementOf(world, car);
            if (crossing < 0 || !OnARoute(world, car)) continue;

            foreach (ref readonly var section in world.Roads.Crossings.Of(world.Roads.WayOfTurn(crossing)))
            {
                var crossed = world.Roads.TurnOfWay(section.OnWay);
                found.Crossed++;
                if (found.TookGroundItCrosses is not null || !Holds(world, crossed, car, LaneUse.Claimed))
                {
                    continue;
                }

                found.TookGroundItCrosses =
                    $"{map}: car {car} crossing on {crossing} has claimed ground on join "
                    + $"{crossed}, which it is driven over at {section.FromM:0.0}–"
                    + $"{section.ToM:0.0} m and will never be on";
            }
        }
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
        var run = Of(map);

        Assert.Null(run.Overlapped);
        Assert.True(
            run.AsFarAsASection > 0,
            $"{map}: no car was ever granted road as far as one of its own crossings");
    }

    /// <summary>What <see cref="NoGrantReachesGroundAnotherBodyHasOnACrossingWay"/> watches for.</summary>
    static void NoGrantReachesGroundAnotherBodyHas(TownWorld world, string map, int tick, Watched found)
    {
        var index = world.Occupancy;
        Span<LaneSlot> mine = stackalloc LaneSlot[32];
        Span<LaneSlot> theirs = stackalloc LaneSlot[32];

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
                foreach (ref readonly var section in world.Roads.Crossings.Of(way))
                {
                    // Reaching a section's near edge is the grant being cut at it, which is the whole
                    // point: what is asserted is that nothing was granted road *past* one.
                    //
                    // <b>From the body's own nose and not from the near edge of the ground it holds.</b> A
                    // stretch begins a margin behind its owner's tail, so a section between that edge and
                    // the nose is one the body is already standing over — a place it has arrived at rather
                    // than road it was granted, and one the traffic crossing there is refused by this very
                    // stretch. Asked from the near edge instead, the assertion is that a car half way
                    // through a turn must brake for the corner it came in by.
                    if (section.MineFromM < asked.StandsToM
                        || section.MineFromM >= grantedToM - Tolerance)
                    {
                        continue;
                    }

                    found.AsFarAsASection++;
                    if (found.Overlapped is not null) continue;

                    var over = index.CopyTo(section.OnWay, theirs);
                    for (var other = 0; other < over; other++)
                    {
                        if (theirs[other].Occupant == asked.Occupant && theirs[other].Of == asked.Of) continue;
                        if (theirs[other].ToM <= section.FromM || theirs[other].FromM >= section.ToM) continue;

                        // Ground held by a movement this one has the right of way over is ground given up
                        // rather than driven through (TER-5e): it is a claim, which is a piece of the world
                        // its holder has not reached and is not committed to. A body, and the road a body is
                        // committed to being able to stop in, still cut the grant here as they always did.
                        if (!LaneOccupancy.Binds(theirs[other], asked.Right)) continue;

                        found.Overlapped =
                            $"{map}: car {asked.Occupant} is granted to {grantedToM:0.00} m of way "
                            + $"{way} at tick {tick}, past a section at {section.MineFromM:0.00} m that "
                            + $"runs over {section.FromM:0.0}–{section.ToM:0.0} m of join "
                            + $"{section.OnWay}, which {theirs[other].Of} {theirs[other].Occupant} "
                            + $"holds from {theirs[other].FromM:0.00} to {theirs[other].ToM:0.00} m";
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// <b>A claim never cuts a grant behind the nose that asked for it</b> (TER-5e). A claim is ground its
    /// holder has not reached, so it is a place to be stopped a margin short of and never a body to be
    /// found already inside — and a car held at one is therefore held at most its own margin past it.
    /// </summary>
    /// <remarks>
    /// <b>It is the fault a car frozen on a junction it had all but crossed reports.</b> The car queueing
    /// behind for the same movement claims the run through the body in front of it, since a claim is laid
    /// from where its holder's own road was cut; read as a cut, that claim answers the leader from a car's
    /// length behind its own nose, and a grant of minus seven metres is a car no clear road in front of it
    /// can ever release. What it looked like on screen was `P-8` queueing with nothing there to queue behind.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoClaimCutsAGrantBehindTheNoseThatAskedForIt(string map) => Assert.Null(Of(map).CutFromBehind);

    /// <summary>
    /// The census the claim above is vacuous without. <b>Asked of the city and not of every map</b>: a
    /// circuit nothing turns off and a scenario laid for pedestrians hold no claim to be cut at.
    /// </summary>
    [Fact]
    public void ACarInTheCityIsHeldAtAClaim() =>
        Assert.True(Of("Odesa").HeldByAClaim > 0, "no car in the city was ever held by a claim");

    /// <summary>What <see cref="NoClaimCutsAGrantBehindTheNoseThatAskedForIt"/> watches for.</summary>
    static void NoClaimCutsAGrantFromBehindTheNose(TownWorld world, string map, int tick, Watched found)
    {
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Cars.GrantCutBy[car] != HeadwayKind.Claimed) continue;

            found.HeldByAClaim++;
            if (found.CutFromBehind is not null) continue;

            // The margin the asker keeps off a place is the whole of what a claim may take off it
            // (<c>LaneCredit.AtAPlaceM</c>), so anything further back was answered from behind the nose.
            var marginM = world.Cars.BuildOf(car).BodyMarginM;
            if (world.Cars.AuthorityM[car] >= -marginM - Tolerance) continue;

            found.CutFromBehind =
                $"{map}: car {car} was cut to {world.Cars.AuthorityM[car]:0.00} m at tick {tick} by a "
                + $"claim, against a margin of {marginM:0.00} m — held by {world.Cars.Hold[car]} "
                + $"at {world.Cars.AlongMps[car]:0.00} m/s";
        }
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
        var noseM = world.Cars.ReserveFromM[car] + world.Cars.BuildOf(car).TailMarginM
                    + world.Cars.BuildOf(car).LengthM;
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

        ref readonly var build = ref world.Cars.BuildOf(car);
        return progressM + build.NoseAheadOfAxleM - build.LengthM - world.Cars.LaneEndsOf(car)[ahead];
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
        using var world = new TownWorld(Towns.Of(map), Config);
        var (slot, crossing, atM) = WhereTwoMovementsCross(world.Roads)!.Value;
        var car = ACarOffTheParking(world, map);

        world.Cars.Driven[car] = false;
        world.Cars.Broken[car] = true;
        world.Cars.PositionM[car] = atM;
        world.Cars.VelocityMps[car] = Vector2.Zero;
        world.RebuildProximityIndex();

        // A wreck is making nothing, so the registry has nothing to say about it — which is the whole
        // reason the ground under it has to.
        Assert.Equal(CarFleet.NoWay, world.Cars.MovementWay[car]);
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
        using var world = new TownWorld(Towns.Of(map), Config);
        var (_, _, atM) = WhereTwoMovementsCross(world.Roads)!.Value;
        var car = ACarOffTheParking(world, map);

        world.Cars.Driven[car] = false;
        world.Cars.Broken[car] = true;
        world.Cars.VelocityMps[car] = Vector2.Zero;

        var acrossM = (WidestLaneM(world.Roads) * 0.5f) + world.Cars.BuildOf(car).FlankM;
        var endM = world.Cars.BuildOf(car).HalfLengthM;
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
                        if (apartM <= reachM) continue;

                        Assert.Fail(
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

    /// <summary>
    /// The junction movement this car is committed to, as the turn it is, or <see cref="CarFleet.NoWay"/>
    /// where it is making none. <b>A bay's way out is a movement of the same kind and is not this file's
    /// subject</b>: the town holds one field for both, and the tests below are about the box.
    /// </summary>
    static int MovementOf(TownWorld world, int car)
    {
        var way = world.Cars.MovementWay[car];
        return way == CarFleet.NoWay || world.Occupancy.WayIsLane(way) || world.BayWays.IsBayWay(way)
            ? CarFleet.NoWay
            : world.Roads.TurnOfWay(way);
    }

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
            foreach (ref readonly var section in roads.Crossings.Of(roads.WayOfTurn(slot)))
            {
                var crossed = roads.TurnOfWay(section.OnWay);
                var arcs = roads.JoinArcs(crossed);
                if (arcs.Length == 0) continue;

                return (crossed, slot, Spline.SampleAt(arcs, (section.FromM + section.ToM) * 0.5f).PositionM);
            }
        }

        return null;
    }
}
