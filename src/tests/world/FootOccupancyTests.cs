using System.Collections.Concurrent;
using System.Numerics;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The pavement's claims asked of a running town: that it describes one, that a walker's place in it is the
/// place its own line put it, and that the grant holds one body off the next exactly as the road's does.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class FootOccupancyTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>Every walker on a line of its own is in the claims.</b> One that is not is a body nobody behind it
    /// can see at all, which is the misreading the whole index exists to remove.
    /// </summary>
    [Fact]
    public void EveryWalkerOnALineOfItsOwnHasClaimedIt()
    {
        var world = Run("Odesa");

        var onALine = 0;
        for (var person = 0; person < world.People.Count; person++)
        {
            if (world.People.OnWay[person] != PersonFleet.NoWay) onALine++;
        }

        Assert.True(
            world.Occupancy.SlotCount >= onALine,
            $"{onALine} walkers were on a line and the town held {world.Occupancy.SlotCount} stretches");
    }

    /// <summary>
    /// <b>The claims are never full.</b> Past its bound a stretch is dropped, and a dropped stretch is a body
    /// the walker behind it is granted the ground of.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void TheClaimsNeverReachTheirBound(string map)
    {
        var world = Run(map);
        if (world.People.Count == 0) return;

        Assert.True(
            world.Occupancy.SlotCount < world.Occupancy.Capacity,
            $"the town held {world.Occupancy.SlotCount} of {world.Occupancy.Capacity} stretches");
    }

    /// <summary>
    /// <b>Every claim is a stretch of one line, and lies on it</b> (OBS-2d). A way with no
    /// line under it, or ground held off the end of the one it names, is a claim the picture cannot
    /// account for: the nodes layer draws each way's line whole and clamps nothing, so what it cannot draw
    /// is a block standing on no ground at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryClaimStandsOnALineThatIsDrawn(string map)
    {
        var world = Run(map);
        var walking = world.Walking;
        var claims = world.Occupancy;

        Span<LaneClaim> slots = stackalloc LaneClaim[64];
        foreach (var way in claims.OccupiedWays)
        {
            var kind = world.Ways.KindOf(way);
            if (kind is not (WayKind.Footway or WayKind.Mitre)) continue;

            var lane = kind == WayKind.Footway;
            var index = lane ? world.Ways.FootwayOf(way) : world.Ways.MitreOf(way);
            var line = lane ? walking.LaneOf(index) : walking.JoinArcs(index);
            var lengthM = claims.WayLengthM(way);
            var what = $"{(lane ? "stretch" : "mitre")} {index}";

            Assert.True(
                line.Length > 0 && lengthM > 0f,
                $"{map}: way {way} — {what} — is spoken for and has {line.Length} arcs over {lengthM:F2} m");

            // What the layer would draw of each stretch, under the clamp it draws them under: a body's box
            // reaches past the end of a short way and the block is trimmed to it, which is honest — a
            // stretch trimmed to nothing is not, being ground held that no reader can be shown.
            var count = claims.CopyTo(way, slots);
            for (var slot = 0; slot < count; slot++)
            {
                var fromM = MathF.Max(0f, slots[slot].FromM);
                var toM = MathF.Min(lengthM, slots[slot].ToM);

                Assert.True(
                    toM > fromM,
                    $"{map}: {what} carries {slots[slot].FromM:F2}–{slots[slot].ToM:F2} m for walker "
                    + $"{slots[slot].Occupant} and is {lengthM:F2} m long, so none of it is drawn");
            }
        }
    }

    /// <summary>
    /// <b>A walker's place is the place its own line put it.</b> It is read off the point being
    /// walked at rather than searched for, so the one thing that could go wrong is the arithmetic: a way
    /// or a distance carried home through the wrong offset puts a body somewhere nobody is standing, and
    /// every grant taken against it is then a grant over ground nobody is on.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void AWalkersClaimIsWhereItsBodyActuallyStands(string map)
    {
        var world = Run(map);
        var walking = world.Walking;

        var placed = 0;
        Span<ArcSeg> scratch = stackalloc ArcSeg[MostArcs];
        for (var person = 0; person < world.People.Count; person++)
        {
            var way = world.People.OnWay[person];
            if (way == PersonFleet.NoWay) continue;

            var onAStretch = world.Ways.KindOf(way) == WayKind.Footway;
            var line = onAStretch
                ? walking.LaneOf(world.Ways.FootwayOf(way))
                : walking.JoinArcs(world.Ways.MitreOf(way));
            if (line.Length == 0) continue;

            placed++;
            var alongM = Math.Clamp(world.People.OnWayM[person], 0f, world.Occupancy.WayLengthM(way));
            var onTheWayM = Spline.SampleAt(line, alongM).PositionM;

            Assert.True(
                (onTheWayM - world.People.PositionM[person]).Length() <= OffItsLaneM,
                $"walker {person} stands {(onTheWayM - world.People.PositionM[person]).Length():0.00} m from "
                + $"{alongM:0.00} m along way {way}, which is where its claim has it");
        }

        Assert.True(placed > 0 || OnALine(world) == 0, "nobody walking a line of the network was placed on a way");
    }

    /// <summary>
    /// <b>Nobody is granted ground somebody else under way will still be standing on once they have
    /// stopped</b> — the same property the road's claims are for, asked of the pavement's, and the whole of
    /// what holds one walker off the next.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asked of the ways rather than of the walkers, because that is where two grants would meet. A grant
    /// that came out empty is skipped: a body already inside somebody else is a contact, and standing still
    /// is all the walker can do about it.
    /// </para>
    /// <para>
    /// <b>And of the bodies on a line of their own alone.</b> This asks how far one walker was granted
    /// against where the next will come to rest, and a body laid from its pose has no rest to come to —
    /// what it holds is where it already is, which is asked instead by TER-4c.3's own gate.
    /// </para>
    /// <para>
    /// <b>Of the walkers standing on the way</b>, and not of the ones whose ask merely reaches it. A walk
    /// has no origin two walkers could be compared against — it is re-laid from wherever the body has got
    /// to — so a grant is only in the same measure as a way's own metres for the body actually on that way
    /// (<see cref="GrantedToM"/>). Read against a stretch laid by somebody a way further back, the two
    /// zeros are a stride apart and the comparison is between different lines.
    /// </para>
    /// </remarks>
    [Fact]
    public void NobodyIsGrantedGroundSomebodyElseWillStopOn()
    {
        var world = Run("Odesa");
        var claims = world.Occupancy;

        Span<LaneClaim> slots = stackalloc LaneClaim[64];
        foreach (var way in claims.OccupiedWays)
        {
            var count = claims.CopyTo(way, slots);
            for (var behind = 0; behind < count; behind++)
            {
                // The walkers' own stretches, of the one table both rosters are in: a driver's grant is the
                // driving side's question and is asked by its own tests.
                if (slots[behind].Of != LaneRoster.Walking) continue;
                if (!slots[behind].OnItsLine || !slots[behind].HasBody) continue;
                if (world.People.OnWay[slots[behind].Occupant] != way) continue;

                // A walker granted nothing is not being sent anywhere, and the standstill gap this adds back
                // is then the footprint it is standing in rather than ground it may walk into. A queue that
                // closes tighter than the gap — a body still stopping when the one ahead of it is held at a
                // kerb — is that and not a grant, and standing still is all the walker can do about it.
                if (world.People.AuthorityM[slots[behind].Occupant] <= 0f) continue;

                var grantedToM = GrantedToM(world, slots[behind]);
                if (grantedToM <= slots[behind].FromM) continue;

                for (var ahead = behind + 1; ahead < count; ahead++)
                {
                    if (slots[ahead].Of != LaneRoster.Walking) continue;
                    if (slots[ahead].Occupant == slots[behind].Occupant) continue;

                    // <b>Of the bodies on a line of their own</b>, for the reason above: the question is
                    // where the one in front will stop, and a body laid from its pose is already stopped.
                    if (!slots[ahead].OnItsLine || !slots[ahead].HasBody) continue;

                    // <b>In front is a fact about the bodies and not about the near edges</b> (TER-5c.2):
                    // every stretch begins a margin behind its owner and a stretch clipped at a way's start
                    // begins further back still, so a slot later in the list can belong to a body this one
                    // has already walked past.
                    //
                    // <b>And level is not in front either</b> (<see cref="LaneOccupancy.GrantedOn"/>). The
                    // end of a way clamps every body past it onto one metre, so a pair there have the same
                    // front and neither is ahead of the other — asked about, each would have to be held off
                    // ground the other is standing on, and the pair never move again.
                    if (slots[ahead].StandsToM <= slots[behind].StandsToM) continue;

                    // Where that body comes to rest: its own stopping distance past its back, worked out
                    // against the grip the *asking* walker has, since what the ground is doing under
                    // somebody else is not something a body can see.
                    var gripMps2 = Config.PersonFootGripMps2 * world.People.GroundCoefficient[slots[behind].Occupant];
                    var restingM = MathF.Max(0f, slots[ahead].AlongMps * slots[ahead].AlongMps / (2f * gripMps2));

                    Assert.True(
                        grantedToM <= slots[ahead].FromM + restingM + ToleranceM,
                        $"walker {slots[behind].Occupant} was granted to {grantedToM:0.00} m of way {way}, "
                        + $"where walker {slots[ahead].Occupant} comes to rest at "
                        + $"{slots[ahead].FromM + restingM:0.00} m");
                }
            }
        }
    }

    /// <summary>
    /// Where a grant ends, <b>in the metres of the way the body is standing on</b>. What is claimed
    /// is what the walker asked for — the cut is taken off it afterwards — so the granted end is built back
    /// up from the place the claim has the body and the ground it was given in front of it.
    /// </summary>
    /// <remarks>
    /// Only the body on the way can be measured this way, and that is a fact about a walk rather than a
    /// shortcut: <see cref="LineWay.LineFromM"/> on the walking side is a distance from the body, so way
    /// metres and walk metres share an origin for exactly one walker per stretch.
    /// </remarks>
    static float GrantedToM(TownWorld world, in LaneClaim asked)
    {
        var person = asked.Occupant;
        return world.People.OnWayM[person] + world.People.AuthorityM[person] + world.People.RadiusM[person];
    }

    /// <summary>
    /// How many walkers are walking a line of the network at all. A map whose pavement cannot be routed
    /// over leaves them striking out at their goals with no line at all, and there is then nothing here to
    /// be asked about — <c>Zebras</c> is such a map, being five people and a crossing.
    /// </summary>
    static int OnALine(TownWorld world)
    {
        var count = 0;
        for (var person = 0; person < world.People.Count; person++)
        {
            var at = world.People.WalkedAt(person);
            if (world.People.Walking[person] && at >= 0 && at < world.People.WalkedCount[person]) count++;
        }

        return count;
    }

    /// <summary>Ground on a way is metres and a grant is arithmetic on floats: a millimetre is not a finding.</summary>
    const float ToleranceM = 1e-2f;

    /// <summary>
    /// How far from the line it is held on a walker may stand and still be the body claimed there.
    /// It is a whole pavement band, because a body is pushed about: what is being checked is that the place
    /// carried through is the body's own place and not an arithmetic error, and those are metres
    /// out and regularly whole streets.
    /// </summary>
    static float OffItsLaneM => Config.PavementWidthM;

    /// <summary>Room for one stretch's arcs. More than any stretch of a shipped town's pavement is drawn with.</summary>
    const int MostArcs = 64;

    /// <summary>A minute of a town, sampled every tick of it.</summary>
    const int Ticks = 3_600;

    public static TheoryData<string> Maps => Towns.EveryTown();

    /// <summary>A minute of one map: the state it arrives at, and the one census that can only be taken on the way.</summary>
    sealed class Minute
    {
        public required TownWorld World { get; init; }

        /// <summary>
        /// How many walker-ticks of it were spent held behind somebody else — a body walking, not waiting at
        /// a kerb, and granted nothing. It is a count over the run and not a state at the end of it, which is
        /// why it is taken here rather than read off <see cref="World"/>.
        /// </summary>
        public int Held;
    }

    static readonly ConcurrentDictionary<string, Minute> Ran = new();

    /// <summary>
    /// <b>The town a minute in, taken once per map and read by every claim that asks about the same
    /// moment.</b> Nothing here writes to the world it is handed, and five questions about one minute are
    /// one run of the town.
    /// </summary>
    static Minute Of(string map) => Ran.GetOrAdd(map, opened =>
    {
        var minute = new Minute { World = new TownWorld(Towns.Of(opened), Config) };
        var loop = new SimLoop<TownWorld>(minute.World, Config);
        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            for (var person = 0; person < minute.World.People.Count; person++)
            {
                if (minute.World.People.Walking[person] && !minute.World.People.HeldAtTheKerb[person]
                    && minute.World.People.AuthorityM[person] <= 0f)
                {
                    minute.Held++;
                }
            }
        }

        return minute;
    });

    static TownWorld Run(string map) => Of(map).World;

    /// <summary>
    /// <b>A body standing in a junction is on the joins that run under it</b> (TER-4c.2), whichever roster it
    /// is in. A driver crossing a box reads the box's own ways and no lane at all, so a person written onto
    /// the nearest lane alone is a person nobody crossing that box can see.
    /// </summary>
    /// <remarks>
    /// <b>It is the reach test that made it invisible and not the walk.</b> A body inside the disc is past
    /// the end of every lane there — the box reaches further than the paint is wide — so the projection
    /// clamps to a lane end and the along test throws it out; asked of the lanes alone there was nothing
    /// left to write it onto, and the road under the body belonged to no way at all.
    /// </remarks>
    [Fact]
    public void ABodyOnFootInAJunctionIsOnTheJoinsUnderIt()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(600);

        var (node, join) = TheBusiestNode(world);

        // Somebody actually in the world: a body inside a container is no body at all (PHY-7), and one
        // walking a crossing takes that crossing's bands instead of the ground under it.
        var person = -1;
        for (var body = 0; body < world.People.Count && person < 0; body++)
        {
            if (!world.People.Inside[body].Any) person = body;
        }

        Assert.True(person >= 0, "a busy town had nobody standing outside a building");

        // Off its own walk, because a body on a crossing takes that crossing's bands instead of the ground
        // under it — this is the other half of PlaceTheWalkerOnTheRoad and not that one.
        // On a join's own line, which is where a driver crossing the box is actually driven.
        world.People.Walking[person] = false;
        world.People.PositionM[person] =
            Spline.SampleAt(world.Roads.JoinArcs(join), world.Roads.JoinLengthM(join) * 0.5f).PositionM;
        world.People.VelocityMps[person] = Vector2.Zero;
        world.RebuildProximityIndex();

        var joins = 0;
        Span<LaneClaim> slots = stackalloc LaneClaim[64];
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            if (world.Ways.KindOf(way) != WayKind.Join) continue;

            var count = world.Occupancy.CopyTo(way, slots);
            for (var slot = 0; slot < count; slot++)
            {
                if (slots[slot].Occupant == person && slots[slot].Of == LaneRoster.Walking) joins++;
            }
        }

        Assert.True(joins > 0, $"a body standing on node {node} is on none of the joins that cross it");
    }

    /// <summary>
    /// <b>A car standing on a footway claims the pavement</b> (TER-4c.2). A body holds the ground it
    /// occupies whatever it is, and which of the town's two networks that ground is numbered in is a fact about
    /// the ground rather than about the kind of body on it — so a car that has mounted a kerb is a stretch of
    /// the pavement under it exactly as a person in a lane is a stretch of that lane.
    /// </summary>
    /// <remarks>
    /// <b>Claimed nowhere it is a thing nothing walking there can see</b> (TER-4c), and the pavement is
    /// the whole of what a walker looks at: the step round it (PER-24) is taken off this stretch, so a car
    /// missing from it is one a walk goes straight through.
    /// </remarks>
    [Fact]
    public void ACarStandingOnAFootwayHoldsTheGroundUnderIt()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(1);

        var lane = AStretchOfItsOwn(world, FootEdgeKind.Pavement);
        Assert.True(lane >= 0, "the town has no stretch of pavement long enough to stand a car on");

        var alongM = world.Walking.LaneLengthM(lane) * 0.5f;
        var car = StandTheCarOn(world, world.Walking.LaneOf(lane), alongM);
        Assert.Equal(car, HolderOn(world.Occupancy, world.Ways.OfFootway(lane), alongM));
    }

    /// <summary>
    /// <b>A car standing beside a walk's own line is still in the way of it</b> (TER-4c.2): what says a body
    /// can be got past is that it stands clear of the line by more than the walker's own half-width
    /// (<see cref="SimConfig.WalkPassableAsideM"/>), which is the bar the road holds a car to said in the
    /// walking side's figures.
    /// </summary>
    /// <remarks>
    /// Read against nought instead, a body stopped being in the way the moment it was a hair clear of the
    /// line — and since a walker takes half its own width either side of the line it walks, a car parked on a
    /// footway beside that line was a car the walk went straight through.
    /// </remarks>
    [Fact]
    public void ACarStandingBesideAWalkIsInItsWay()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(600);

        var person = AWalkerWithTheWalkToItself(world, out var lane);
        Assert.True(person >= 0, "a busy town had nobody walking a clear stretch of pavement");

        var line = world.Walking.LaneOf(lane);
        var alongM = InFrontOfM(world, person, Config.Car.LengthM * 0.5f);
        var car = StandTheCarOn(world, line, alongM);

        // Clear of the line by half of what a walk needs to pass one, which is a car parked alongside the
        // walk rather than on it.
        var on = Spline.SampleAt(line, alongM);
        world.Cars.PositionM[car] = on.PositionM
                                    + (Heading.RightOf(on.Direction)
                                       * ((Config.Car.WidthM * 0.5f) + (Config.WalkPassableAsideM * 0.5f)));

        // Walking *past* it and not at it: a body standing where the walk is going is the one thing no step
        // gets round (PER-24), so a fixture that left the aim on the car would be asking the other question.
        WalkOnPast(world, person, line, alongM);
        world.RebuildProximityIndex();

        Assert.Equal(car, world.People.StepsRound[person]);
        Assert.Equal(LaneRoster.Driving, world.People.StepsRoundOf[person]);
    }

    /// <summary>
    /// <b>And a car driving across the footway is waited for rather than stepped round</b> (PER-24): going
    /// nowhere is the body's own movement, and never how its claim was laid. <b>The same car
    /// standing on the same ground is stepped round</b>, which is what says the movement and nothing else
    /// decided it.
    /// </summary>
    /// <remarks>
    /// <b>Both cut the walk down the line</b> (TER-4c.3) — either car is standing on ground the walk wanted.
    /// What the movement decides is the reply: one coming through is waited for where it stands, and one
    /// going nowhere is stepped past, which is this walker asking for the pavement <em>beside</em> the car
    /// and being granted it. So the one that is waited for holds this body and the one that is stepped past
    /// stops holding it, and that difference is the step having been taken rather than merely aimed at.
    /// </remarks>
    [Fact]
    public void ACarComingAcrossAFootwayHoldsTheWalkUp()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(600);

        var person = AWalkerWithTheWalkToItself(world, out var lane);
        Assert.True(person >= 0, "a busy town had nobody walking a clear stretch of pavement");

        // In front of the body by less than it is asking for, so the near edge of the car's own stretch
        // falls inside the ground this walker is asking to walk into — and square across the walk, which is
        // a car that has mounted the kerb in front of somebody rather than one parked along it.
        var line = world.Walking.LaneOf(lane);
        var alongM = InFrontOfM(world, person, Config.Car.WidthM * 0.5f);
        var car = StandTheCarOn(world, line, alongM);
        world.Cars.HeadingRad[car] += MathF.PI * 0.5f;

        // Walking *past* it and not at it, as above: what is asked here is which of two replies the car's
        // own movement picks, and a car sitting on the aim is answered by neither of them.
        WalkOnPast(world, person, line, alongM);

        world.Cars.VelocityMps[car] = Heading.Unit(world.Cars.HeadingRad[car]) * Config.PersonWalkSpeedMps;
        world.RebuildProximityIndex();

        Assert.Equal(car, world.People.HeldBy[person]);
        Assert.Equal(LaneRoster.Driving, world.People.HeldByOf[person]);
        Assert.Equal(PersonFleet.NoBody, world.People.StepsRound[person]);

        // And the same car standing on the same ground is stepped past instead: the walk down the line is
        // cut at it either way, and what a step is, is the same walk asked for again from an offset across
        // the way — so the body it is getting past is named, the offset is real, and the ground the walker
        // was granted is no longer anybody's.
        world.Cars.VelocityMps[car] = Vector2.Zero;
        world.RebuildProximityIndex();

        Assert.Equal(car, world.People.StepsRound[person]);
        Assert.Equal(LaneRoster.Driving, world.People.StepsRoundOf[person]);
        Assert.True(
            MathF.Abs(world.People.StepsAcrossM[person]) > 0f,
            "the walker named the car as the body to get past and then stepped nowhere");
        Assert.Equal(PersonFleet.NoBody, world.People.HeldBy[person]);
    }

    /// <summary>
    /// <b>A walker on no way at all is granted only ground nobody is standing on</b> (PER-13, TER-4c.3).
    /// A body off every line holds where it lies and has no stretch in front of it to be cut on, so its
    /// permission is asked of the ground itself — and it is the commonest state in the town rather than a
    /// corner of it, since every walk begins before its first point is taken.
    /// </summary>
    /// <remarks>
    /// Left unanswered, the grant stayed at what a walker asks for with nothing in front of it at all: the
    /// tick a walk was re-laid, the walker stopped being held by anything and walked through whatever it had
    /// been standing at.
    /// </remarks>
    [Fact]
    public void AWalkerOnNoWayIsHeldOffGroundABodyStandsOn()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(600);

        var person = AWalkerWithTheWalkToItself(world, out var lane);
        Assert.True(person >= 0, "a busy town had nobody walking a clear stretch of pavement");

        var line = world.Walking.LaneOf(lane);
        var alongM = InFrontOfM(world, person, Config.Car.WidthM * 0.5f);
        var car = StandTheCarOn(world, line, alongM);
        world.Cars.HeadingRad[car] += MathF.PI * 0.5f;
        WalkOnPast(world, person, line, alongM);

        // Back to before the first point of its own line was taken, which is where a walk starts and where
        // the clock that gives one up puts a walker again.
        world.People.WalkedTaken[person] = 0;
        world.RebuildProximityIndex();

        Assert.Equal(PersonFleet.NoWay, world.People.OnWay[person]);
        Assert.Equal(car, world.People.HeldBy[person]);
        Assert.Equal(LaneRoster.Driving, world.People.HeldByOf[person]);
        Assert.True(
            world.People.IsHeldByTheClaims(person, world.StopsInM(person)),
            "a walker on no way was let walk into ground a car was standing on");
    }

    /// <summary>
    /// <b>And a car on a crossing claims the road and nothing else</b> (TER-5c.1). A zebra is
    /// carriageway a walk runs over, so the ground under it has two names and one owner: what holds a walker
    /// off the car is that car's stretch of the <em>lane</em>, looked up where the crossing crosses it.
    /// </summary>
    /// <remarks>
    /// <b>Marked on the walk as well it is one body held twice over one piece of ground</b>, under two claims free
    /// to disagree — and the one a walker reads would say the paint may be stepped round, which is a body
    /// walking into the lane beside a car it was already being held off.
    /// </remarks>
    [Fact]
    public void ACarOnACrossingHoldsTheLaneAndNotTheWalk()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(1);

        var lane = AStretchOfItsOwn(world, FootEdgeKind.Crossing);
        Assert.True(lane >= 0, "the town has no crossing long enough to stand a car on");

        var alongM = world.Walking.LaneLengthM(lane) * 0.5f;
        var car = StandTheCarOn(world, world.Walking.LaneOf(lane), alongM);

        Assert.Equal(LaneOccupancy.Nobody, HolderOn(world.Occupancy, world.Ways.OfFootway(lane), alongM));
        Assert.True(
            OnAnyWayOf(world.Occupancy, car), $"car {car} standing on the paint is on no way of the road");
    }

    /// <summary>
    /// <b>And a body on foot standing on a crossing holds the walk under it</b> (TER-4c.2), which is the half
    /// of the paint the car's exclusion does not reach: the look-up that answers for a car asks what traffic
    /// is <em>coming</em>, and somebody standing in a lane is not an answer to it.
    /// </summary>
    /// <remarks>
    /// The walkers' claims are the whole of what a walk reads, so a body missing from it there is one the step
    /// round (PER-24) never sees and the grant is never cut at — a walk straight through somebody standing on
    /// the paint.
    /// </remarks>
    [Fact]
    public void ABodyOnFootOnACrossingHoldsTheWalkUnderIt()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(600);

        var lane = AStretchOfItsOwn(world, FootEdgeKind.Crossing);
        Assert.True(lane >= 0, "the town has no crossing long enough to stand a body on");

        var alongM = world.Walking.LaneLengthM(lane) * 0.5f;
        var atM = Spline.SampleAt(world.Walking.LaneOf(lane), alongM).PositionM;

        var person = StandTheWalkerAt(world, atM);
        Assert.Equal(person, HolderOn(world.Occupancy, world.Ways.OfFootway(lane), alongM, LaneRoster.Walking));
    }

    /// <summary>
    /// <b>A walker standing about holds the pavement under its own body</b> (TER-4c.2) — the same box against
    /// the same band a car mounting the kerb is laid by (<see cref="ACarStandingOnAFootwayHoldsTheGroundUnderIt"/>),
    /// and not a reading of its own.
    /// </summary>
    /// <remarks>
    /// A lane of pavement is its stretch's curve moved a quarter of the band aside and carries the corner off
    /// its own end, so it is neither the stretch's length nor laid from the stretch's zero. Placed by the share
    /// of the stretch walked read as the same share of the lane, a body standing about was claimed tens
    /// of metres from where it stood — ground nobody was on, held against everybody walking towards it.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void AWalkerStandingAboutHoldsThePavementUnderIt(string map)
    {
        var world = Run(map);
        var walking = world.Walking;

        var standing = 0;
        var offEveryWay = 0;
        Span<LaneClaim> slots = stackalloc LaneClaim[64];
        for (var person = 0; person < world.People.Count; person++)
        {
            if (world.People.Inside[person].Any) continue;
            if (world.People.OnWay[person] != PersonFleet.NoWay) continue;

            offEveryWay++;
            var bodyM = world.People.PositionM[person];
            var nearestM = float.PositiveInfinity;
            foreach (var way in world.Occupancy.OccupiedWays)
            {
                var kind = world.Ways.KindOf(way);
                if (kind is not (WayKind.Footway or WayKind.Mitre)) continue;

                var line = kind == WayKind.Footway
                    ? walking.LaneOf(world.Ways.FootwayOf(way))
                    : walking.JoinArcs(world.Ways.MitreOf(way));
                if (line.Length == 0) continue;

                var count = world.Occupancy.CopyTo(way, slots);
                for (var slot = 0; slot < count; slot++)
                {
                    if (slots[slot].Occupant != person || slots[slot].Of != LaneRoster.Walking) continue;

                    var midM = Math.Clamp(
                        (slots[slot].FromM + slots[slot].ToM) * 0.5f, 0f, world.Occupancy.WayLengthM(way));
                    nearestM = MathF.Min(nearestM, (Spline.SampleAt(line, midM).PositionM - bodyM).Length());
                }
            }

            if (float.IsPositiveInfinity(nearestM)) continue;

            standing++;
            Assert.True(
                nearestM <= StandsOffItsLaneM,
                $"{map}: walker {person} stands {nearestM:0.00} m from the middle of the nearest ground the "
                + "its claim holds for it");
        }

        // A body off every way of the pavement holds the ground under it or it holds nothing anywhere, which
        // is the whole of the claim. Whether this map had one of them at this minute is the town's business:
        // a walker is on the way its own line begins on now, so being on none of them is the rarer state.
        Assert.True(standing > 0 || offEveryWay == 0, $"{map}: somebody was on no way and held no pavement");
    }

    /// <summary>
    /// How far the middle of a standing body's own stretch may be from the body: it is laid at the body's
    /// projection onto the lane's line, so the two stand apart by however far across that line the body is —
    /// half a walking lane, and the body's own radius where it is only clipping one.
    /// </summary>
    static float StandsOffItsLaneM => (Config.WalkingLaneWidthM + Config.PersonDiameterM) * 0.5f;

    /// <summary>
    /// <b>And a walker whose body reaches into a lane holds a stretch of it, wherever the ground under its
    /// own middle is painted</b> (TER-4c.2). What decides which way a body is on is that way's band against
    /// the body's box, which is the one test every other body in the town is laid by.
    /// </summary>
    /// <remarks>
    /// The terrain grid is a classifier at a metre a cell, so a body within half a cell of a kerb regularly
    /// stands on ground it calls pavement while its box is well inside the carriageway. Asked of the grid
    /// first, that body claimed no lane at all — somebody stepping off a kerb in front of traffic with
    /// nothing to read.
    /// </remarks>
    [Fact]
    public void AWalkerPastTheKerbWhoseBodyReachesALaneHoldsAStretchOfIt()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(600);

        var lane = TheLongestLane(world);

        // The furthest out it can stand and still be on the lane: its near edge a crossing inside the band's
        // own edge (<c>SimConfig.CrossesOntoAWayM</c>), which puts its middle past the kerb.
        var acrossM = (world.Roads.LaneWidthM[lane] * 0.5f) + (Config.PersonDiameterM * 0.5f)
                      - Config.CrossesOntoAWayM - ToleranceM;

        Assert.True(
            acrossM > world.Roads.LaneWidthM[lane] * 0.5f,
            $"lane {lane} is wide enough that this body's middle is still on the carriageway");

        // And at a metre of it the grid calls something other than carriageway, which is what the claim is
        // about: the two disagree by up to half a cell wherever a kerb runs, and the band is what settles it.
        var alongM = WherePastTheKerbIsNotDrivable(world, lane, acrossM, out var atM);
        Assert.True(alongM >= 0f, $"the grid calls the whole kerb of lane {lane} drivable, so this proves nothing");

        var person = StandTheWalkerAt(world, atM);
        Assert.Equal(
            person, HolderOn(world.Occupancy, world.Ways.OfRoadLane(lane), alongM, LaneRoster.Walking));
    }

    /// <summary>
    /// Where along this lane a point <paramref name="acrossM"/> out on the kerb side stands on ground the
    /// terrain grid does not call drivable, or <c>-1</c> where it calls the whole of that kerb road.
    /// </summary>
    static float WherePastTheKerbIsNotDrivable(TownWorld world, int lane, float acrossM, out Vector2 atM)
    {
        var arcs = world.Roads.ArcsOf(lane);
        var lengthM = world.Roads.LaneLengthM[lane];
        for (var alongM = lengthM * 0.25f; alongM <= lengthM * 0.75f; alongM += Config.Terrain.GroundStepM)
        {
            var on = Spline.SampleAt(arcs, alongM);
            atM = on.PositionM + (Heading.RightOf(on.Direction) * acrossM * Config.RoadSideSign);
            if (!world.Terrain.At(atM).Drivable) return alongM;
        }

        atM = Vector2.Zero;
        return -1f;
    }

    /// <summary>The lane with the most of itself away from a node, so that neither end decides the answer.</summary>
    static int TheLongestLane(TownWorld world)
    {
        var best = 0;
        for (var lane = 1; lane < world.Roads.LaneCount; lane++)
        {
            if (world.Roads.LaneLengthM[lane] > world.Roads.LaneLengthM[best]) best = lane;
        }

        return best;
    }

    /// <summary>
    /// One of the town's walkers stood still at a place, off any line of its own, with both networks' claims rebuilt
    /// around it. A body inside a building is no body at all (PHY-7), so the one taken is one outside.
    /// </summary>
    static int StandTheWalkerAt(TownWorld world, Vector2 atM)
    {
        var person = -1;
        for (var body = 0; body < world.People.Count && person < 0; body++)
        {
            if (!world.People.Inside[body].Any) person = body;
        }

        Assert.True(person >= 0, "a busy town had nobody standing outside a building");

        world.People.Walking[person] = false;
        world.People.PositionM[person] = atM;
        world.People.VelocityMps[person] = Vector2.Zero;
        world.RebuildProximityIndex();
        return person;
    }

    /// <summary>
    /// <b>One of the town's walkers walking a stretch of pavement nobody else is on</b>, with room in front
    /// of it for a car and nothing already holding it there — so that what cuts its grant afterwards is the
    /// car put there and cannot be anything else.
    /// </summary>
    static int AWalkerWithTheWalkToItself(TownWorld world, out int lane)
    {
        lane = -1;
        Span<LaneClaim> slots = stackalloc LaneClaim[8];
        for (var person = 0; person < world.People.Count; person++)
        {
            var way = world.People.OnWay[person];
            if (way == PersonFleet.NoWay || world.Ways.KindOf(way) != WayKind.Footway) continue;
            if (!world.People.Walking[person] || world.People.HeldAtTheKerb[person]) continue;
            if (world.People.HeldBy[person] != PersonFleet.NoBody) continue;
            if (world.People.StepsRound[person] != PersonFleet.NoBody) continue;

            var edge = world.Ways.FootwayOf(way);
            if (world.Walking.Foot.KindOf(edge) != FootEdgeKind.Pavement) continue;
            if (world.Occupancy.CopyTo(way, slots) != 1) continue;
            if (RoomAheadOf(world, person) + Config.Car.LengthM > world.Walking.LaneLengthM(edge)) continue;

            lane = edge;
            return person;
        }

        return -1;
    }

    /// <summary>Where on its own lane a walker's ask runs out, which is the last metre a body in front of it can cut.</summary>
    static float RoomAheadOf(TownWorld world, int person) =>
        world.People.OnWayM[person] + world.People.RadiusM[person] + world.People.ClaimAheadM[person];

    /// <summary>
    /// Where to stand a car on that lane for it to be in front of this walker: <b>inside the ground the
    /// walker is asking for</b> and clear of the body itself, which is however much of the car lies along
    /// the lane further on again.
    /// </summary>
    static float InFrontOfM(TownWorld world, int person, float halfAlongM) =>
        RoomAheadOf(world, person) - (world.People.ClaimAheadM[person] * 0.5f) + halfAlongM;

    /// <summary>
    /// <b>Aim this walker down its own lane past the body standing at <paramref name="pastM"/></b>, by a car's
    /// length again. A body within a clearance of where a walk is going is not one the walk is trying to get
    /// past (PER-24) — it is where the walk was going — so a fixture asking about a step has to put the aim
    /// somewhere beyond the thing being stepped round.
    /// </summary>
    static void WalkOnPast(TownWorld world, int person, ReadOnlySpan<ArcSeg> line, float pastM) =>
        world.People.DestinationM[person] =
            Spline.SampleAt(line, pastM + Config.Car.LengthM).PositionM;

    /// <summary>
    /// A stretch of the walking network of one kind that is long enough to stand a body in the middle of
    /// without the ends of it deciding the answer, and that nobody is on.
    /// </summary>
    static int AStretchOfItsOwn(TownWorld world, FootEdgeKind kind)
    {
        Span<LaneClaim> slots = stackalloc LaneClaim[1];
        for (var edge = 0; edge < world.Walking.Foot.EdgeCount; edge++)
        {
            if (world.Walking.Foot.KindOf(edge) != kind) continue;
            if (world.Walking.LaneLengthM(edge) < Config.Car.LengthM * 2f) continue;
            if (world.Occupancy.CopyTo(world.Ways.OfFootway(edge), slots) != 0) continue;

            return edge;
        }

        return -1;
    }

    /// <summary>
    /// One of the town's cars stood still at a place on a line, facing the way the line runs, with the claims
    /// rebuilt around it.
    /// </summary>
    static int StandTheCarOn(TownWorld world, ReadOnlySpan<ArcSeg> line, float alongM)
    {
        const int car = 0;
        var on = Spline.SampleAt(line, alongM);

        world.Cars.Driven[car] = false;
        world.Cars.Broken[car] = true;
        world.Cars.VelocityMps[car] = Vector2.Zero;
        world.Cars.PositionM[car] = on.PositionM;
        world.Cars.HeadingRad[car] = MathF.Atan2(on.Direction.Y, on.Direction.X);
        world.RebuildProximityIndex();
        return car;
    }

    /// <summary>Whose the ground at one place on one way is, or <see cref="LaneOccupancy.Nobody"/>.</summary>
    static int HolderOn(LaneOccupancy claims, int way, float alongM, LaneRoster of = LaneRoster.Driving)
    {
        Span<LaneClaim> slots = stackalloc LaneClaim[32];
        var count = claims.CopyTo(way, slots);
        for (var at = 0; at < count; at++)
        {
            if (slots[at].Of == of && slots[at].FromM <= alongM && slots[at].ToM >= alongM)
            {
                return slots[at].Occupant;
            }
        }

        return LaneOccupancy.Nobody;
    }

    /// <summary>Whether this car holds ground on any way of a network, which is what says it claimed one at all.</summary>
    static bool OnAnyWayOf(LaneOccupancy claims, int car)
    {
        Span<LaneClaim> slots = stackalloc LaneClaim[64];
        foreach (var way in claims.OccupiedWays)
        {
            var count = claims.CopyTo(way, slots);
            for (var at = 0; at < count; at++)
            {
                if (slots[at].Of == LaneRoster.Driving && slots[at].Occupant == car) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The node the most movements are made through — which is where a body standing has the most to be seen
    /// by — and one of the joins across it to stand on.
    /// </summary>
    static (int Node, int Join) TheBusiestNode(TownWorld world)
    {
        var best = 0;
        var bestJoin = 0;
        var most = -1;
        for (var node = 0; node < world.Roads.NodeCount; node++)
        {
            var turns = 0;
            var join = -1;
            foreach (var lane in world.Roads.LanesIn(node))
            {
                turns += world.Roads.TurnsFrom(lane).Length;
                for (var turn = 0; turn < world.Roads.TurnsFrom(lane).Length && join < 0; turn++)
                {
                    var slot = world.Roads.TurnSlotAt(lane, turn);
                    if (world.Roads.JoinArcs(slot).Length > 0) join = slot;
                }
            }

            if (turns <= most || join < 0) continue;

            best = node;
            bestJoin = join;
            most = turns;
        }

        return (best, bestJoin);
    }
}
