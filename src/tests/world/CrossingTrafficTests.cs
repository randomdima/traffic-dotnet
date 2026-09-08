using System.Collections.Concurrent;
using System.Numerics;
using TrafficSimulation.Agents.Car.Control;
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
/// The yield as it is actually arrived at (TER-4c.1, TER-5e): <b>a body crossing the road claims the ground
/// it is on, and the drivers read that claim</b>. What is asked of a running town is that the claim is laid,
/// that there is room for it, and that it binds somebody.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P1)]
public class CrossingTrafficTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    const int Ticks = 3_600;

    /// <summary>
    /// <b>Somebody crosses, and the road knows.</b> A crossing rule reading ground nothing ever claims
    /// is a rule that costs a tick and yields to nobody, and no assertion about the arithmetic would
    /// notice.
    /// </summary>
    [Fact]
    public void ABodyOnThePaintIsInTheRoadsBook() =>
        Assert.True(Of(Towns.Fixture).OnThePaint > 0, "nobody in a minute of the fixture town was on a crossing");

    /// <summary>
    /// <b>The road's claims are never full</b>, for either of the two kinds of thing that lays one. Past its
    /// bound a stretch is dropped: a dropped car is one its followers cannot name, which reads as an
    /// obstruction, and a dropped body on a crossing is one no driver can see at all, since the question it
    /// answers has no geometry behind it any more.
    /// </summary>
    /// <remarks>
    /// Taken at the fullest they ever got over the run and not at the last tick, which is the same claim
    /// asked where it can actually fail.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void TheBookNeverReachesItsBound(string map)
    {
        var run = Of(map);

        Assert.True(
            run.MostSlots < run.Capacity,
            $"{map}: the road held {run.MostSlots} of {run.Capacity} stretches");
    }

    /// <summary>
    /// <b>And the traffic gives way to them where nothing else governs the crossing</b> (TER-5e): a driver
    /// approaching an uncontrolled zebra with somebody at its kerb is stopped short of the paint, and a body
    /// stopped short holds none of what is beyond it (TER-4c.1) — so the band the walker was refused is the
    /// walker's on the next tick.
    /// </summary>
    /// <remarks>
    /// Asked of the map with the most uncontrolled crossings on it. A town whose every junction is lit
    /// proves nothing here: the signal has already decided whose turn it is, and a second gate on top of it
    /// would be the duplicate SIM-7 is about.
    /// </remarks>
    [Fact]
    public void TheTrafficGivesWayAtAnUncontrolledCrossing() =>
        Assert.True(
            Of(Towns.City).GaveWay > 0,
            "no driver in a minute of Odesa gave way to somebody standing at an uncontrolled crossing");

    /// <summary>
    /// <b>A car on the paint claims the road and nothing else</b> (TER-5c.1). A zebra is a walk
    /// laid over a carriageway, so the ground under it has two names and one owner: what a car has of it is a
    /// stretch of the lane it is driving. Marked on the walk as well, one body held one piece of ground
    /// twice, under two claims that could disagree about it — and the picture said a crossing was a thing the
    /// traffic holds rather than ground with a lane under it.
    /// </summary>
    /// <remarks>
    /// <b>It is the shared ground and not the roster</b> (TER-4c.2). A car standing on a footway is in the
    /// walkers' claims like anything else on that ground, and has to be, or a walk goes straight through it;
    /// what is refused here is a second record of ground the road's claim already answers for.
    /// </remarks>
    [Fact]
    public void NoCarEverClaimsThePavementOnAZebra() => Assert.Null(Of(Towns.City).CarInTheWalkersClaims);

    /// <summary>What <see cref="NoCarIsEverInThePavementsBookOnAZebra"/> watches for.</summary>
    static void NothingButWalkersIsOnTheWalksSideOfAZebra(TownWorld world, Watched found)
    {
        if (found.CarInTheWalkersClaims is not null) return;

        var walking = world.Walking;
        Span<LaneClaim> slots = stackalloc LaneClaim[64];
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            var kind = world.Ways.KindOf(way);
            if (kind is not (WayKind.Footway or WayKind.Mitre)) continue;

            // A mitre is the ground of the stretch it leads onto, which is the same reading the drawing and
            // the write both take of one.
            var edge = kind == WayKind.Footway
                ? world.Ways.FootwayOf(way)
                : walking.TurnToEdge(world.Ways.MitreOf(way));
            if (walking.Foot.KindOf(edge) != FootEdgeKind.Crossing) continue;

            var count = world.Occupancy.CopyTo(way, slots);
            for (var at = 0; at < count; at++)
            {
                if (slots[at].Of == LaneRoster.Walking) continue;

                found.CarInTheWalkersClaims =
                    $"car {slots[at].Occupant} holds {slots[at].FromM:0.00}–{slots[at].ToM:0.00} m of "
                    + $"crossing way {way}";
                return;
            }
        }
    }

    /// <summary>
    /// <b>And a body refused a lane of a zebra is granted no further than that lane's edge</b> (`PER-15`,
    /// TER-4c.1). The refusal is made once, when the band is asked for against the road's claims, and read
    /// back into the walk's own metres — so what holds a walker at a lane's edge and what holds a car off
    /// the body in front of it are one arrangement asked from two sides.
    /// </summary>
    [Fact]
    public void ABodyRefusedALaneIsGrantedNoFurtherThanItsEdge()
    {
        var run = Of(Towns.City);

        Assert.Null(run.GrantedPastTheEdge);
    }

    /// <summary>What <see cref="ABodyRefusedALaneIsGrantedNoFurtherThanItsEdge"/> watches for.</summary>
    static void ARefusedBodyIsGrantedNoFurtherThanTheEdge(TownWorld world, Watched found)
    {
        for (var person = 0; person < world.People.Count; person++)
        {
            // The way the refusal was made on and the way the body is standing on, which are the same
            // way for anybody already on the paint and the only case a grant is measured in.
            var way = world.People.RefusedWay[person];
            if (way == PersonFleet.NoWay || way != world.People.OnWay[person]) continue;

            found.Refused++;
            var grantedToM = world.People.OnWayM[person] + world.People.AuthorityM[person]
                             + Config.PersonStandstillGapM + world.People.RadiusM[person];
            if (found.GrantedPastTheEdge is not null
                || grantedToM <= world.People.RefusedAtM[person] + ToleranceM)
            {
                continue;
            }

            found.GrantedPastTheEdge =
                $"walker {person} was refused a lane and granted to {grantedToM:0.00} m of way {way}, "
                + $"where that lane's band begins at {world.People.RefusedAtM[person]:0.00} m";
        }
    }

    /// <summary>
    /// <b>And the refusal is spent the moment it is lifted.</b> A body granted the band in front of it walks
    /// into it — the one thing a cut taken from the wrong record gets wrong, since a grant that re-asked the
    /// claims, or read the patience the wait is clocked on, would hold a body at the edge of ground it had just
    /// been given and nobody would ever finish crossing.
    /// </summary>
    [Fact]
    public void ABodyRefusedALaneWalksIntoItOnceItIsGranted() =>
        Assert.True(
            Of(Towns.City).GotIn > 0,
            "nobody in a minute of Odesa got into a lane of a crossing they were refused");

    /// <summary>What <see cref="ABodyRefusedALaneWalksIntoItOnceItIsGranted"/> watches for.</summary>
    static void ARefusedBodyWalksInOnceItIsGranted(TownWorld world, Watched found)
    {
        for (var person = 0; person < world.People.Count; person++)
        {
            var way = world.People.OnWay[person];
            if (way != PersonFleet.NoWay && way == found.WayRefusedOn[person]
                && world.People.OnWayM[person] > found.MetreRefusedAt[person])
            {
                found.GotIn++;
                found.WayRefusedOn[person] = PersonFleet.NoWay;
                continue;
            }

            if (world.People.RefusedWay[person] == PersonFleet.NoWay) continue;

            found.WayRefusedOn[person] = world.People.RefusedWay[person];
            found.MetreRefusedAt[person] = world.People.RefusedAtM[person];
        }
    }

    /// <summary>Ground on a way is metres and a grant is arithmetic on floats: a millimetre is not a finding.</summary>
    const float ToleranceM = 1e-2f;

    /// <summary>
    /// <b>A body on a crossing holds the lane it is standing in and no others</b> (`PER-15`), exactly as a
    /// car holds the lane it is driving — <b>and the one in front of it, which it has been granted and is
    /// about to be in</b>. Never a second one beyond that, whatever the body is doing: a zebra is crossed a
    /// lane at a time, and a lane is asked for when the body is at its edge and not when it sets out.
    /// </summary>
    /// <remarks>
    /// Counted against the body rather than against the rule's own arithmetic: how many lanes a walker has
    /// a stretch of, against how many of them it is actually standing in.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ABodyHoldsTheLaneItIsStandingIn(string map) => Assert.Null(Of(map).HeldALaneItIsNotIn);

    /// <summary>What <see cref="ABodyHoldsTheLaneItIsStandingIn"/> watches for.</summary>
    static void ABodyHoldsNoLaneItIsNotStandingIn(TownWorld world, string map, Watched found)
    {
        if (found.HeldALaneItIsNotIn is not null) return;

        Array.Clear(found.Held);
        Array.Clear(found.StandingIn);
        Span<LaneClaim> slots = stackalloc LaneClaim[64];

        foreach (var way in world.Occupancy.OccupiedWays)
        {
            if (world.Ways.KindOf(way) != WayKind.Lane) continue;

            var lane = world.Ways.RoadLaneOf(way);
            var count = world.Occupancy.CopyTo(way, slots);
            for (var at = 0; at < count; at++)
            {
                if (!(slots[at].HasBody && slots[at].Of == LaneRoster.Walking)) continue;

                found.Held[slots[at].Occupant]++;
                if (IsStandingInTheLane(world, slots[at], lane)) found.StandingIn[slots[at].Occupant]++;
            }
        }

        for (var person = 0; person < found.Held.Length; person++)
        {
            if (found.Held[person] <= found.StandingIn[person] + 1) continue;

            found.HeldALaneItIsNotIn =
                $"{map}: walker {person} holds {found.Held[person]} lanes and is standing in "
                + $"{found.StandingIn[person]} of them, {LanesHeld(world, person)}";
            return;
        }
    }

    /// <summary>
    /// Whether the body a stretch belongs to is inside that lane's own band — its own reach across the line
    /// included, since a body that merely clips a band is in it, and the slack two touching bands were
    /// rounded out by, since a body inside that much of a boundary is standing in both lanes.
    /// </summary>
    static bool IsStandingInTheLane(TownWorld world, in LaneClaim slot, int lane)
    {
        var alongM = Math.Clamp((slot.FromM + slot.ToM) * 0.5f, 0f, world.Roads.LaneLengthM[lane]);
        var on = Spline.SampleAt(world.Roads.ArcsOf(lane), alongM);
        var offM = world.People.PositionM[slot.Occupant] - on.PositionM;

        return MathF.Abs(Vector2.Dot(offM, on.Right))
               <= (world.Roads.LaneWidthM[lane] * 0.5f)
               + world.People.RadiusM[slot.Occupant]
               + BandSlackM;
    }

    /// <summary>What the two edges of a pair of touching bands were rounded outward by when they were projected.</summary>
    const float BandSlackM = 0.5f;

    /// <summary>Which lanes a body has a stretch of and how far off each one's line it stands, for a failure to name.</summary>
    static string LanesHeld(TownWorld world, int person)
    {
        var text = new System.Text.StringBuilder();
        Span<LaneClaim> slots = stackalloc LaneClaim[64];
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            if (world.Ways.KindOf(way) != WayKind.Lane) continue;

            var lane = world.Ways.RoadLaneOf(way);
            var count = world.Occupancy.CopyTo(way, slots);
            for (var at = 0; at < count; at++)
            {
                if (!(slots[at].HasBody && slots[at].Of == LaneRoster.Walking) || slots[at].Occupant != person) continue;

                var alongM = Math.Clamp(
                    (slots[at].FromM + slots[at].ToM) * 0.5f, 0f, world.Roads.LaneLengthM[lane]);
                var on = Spline.SampleAt(world.Roads.ArcsOf(lane), alongM);
                var acrossM = Vector2.Dot(world.People.PositionM[person] - on.PositionM, on.Right);
                text.Append(
                    $"[lane {lane} width {world.Roads.LaneWidthM[lane]:0.00} m, body {acrossM:0.00} m off it] ");
            }
        }

        return text.ToString();
    }

    public static TheoryData<string> Maps => Towns.EveryTown();

    /// <summary>
    /// <b>One run of one map, watched by every claim in this class at once</b> — the counts a claim needs a
    /// census of, and the first tick each of the others was broken on, or null.
    /// </summary>
    sealed class Watched(int people)
    {
        /// <summary>Walker-ticks spent on paint the road knew about, the fullest the claims got, and car-ticks a crossing bound.</summary>
        public int OnThePaint, MostSlots, HeldByPaint;

        /// <summary>How many claims there is room for, read off the world the run was taken on rather than off a second one.</summary>
        public int Capacity;

        public int Refused, GotIn;

        /// <summary>Walker-ticks whose ask for a band was claimed on the road, and the town's own give-way count.</summary>
        public int Waiting;
        public long GaveWay;
        public string? CarInTheWalkersClaims, GrantedPastTheEdge, HeldALaneItIsNotIn;

        /// <summary>Per-tick working sets, and the refusal one claim carries between ticks.</summary>
        public readonly int[] Held = new int[people];
        public readonly int[] StandingIn = new int[people];
        public readonly int[] WayRefusedOn = new int[people];
        public readonly float[] MetreRefusedAt = new float[people];
    }

    static readonly ConcurrentDictionary<string, Watched> Runs = new();

    /// <summary>The run this map's claims are all read off, taken once.</summary>
    static Watched Of(string map) => Runs.GetOrAdd(map, Watch);

    /// <summary>A minute of the town, watched tick by tick by every claim above.</summary>
    static Watched Watch(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var found = new Watched(world.People.Count) { Capacity = world.Occupancy.Capacity };
        Array.Fill(found.WayRefusedOn, PersonFleet.NoWay);

        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            found.MostSlots = Math.Max(found.MostSlots, world.Occupancy.SlotCount);

            foreach (var way in world.Occupancy.OccupiedWays)
            {
                var lengthM = world.Occupancy.WayLengthM(way);
                if (world.Occupancy.AnybodyOnFoot(way, 0f, lengthM)) found.OnThePaint++;
                if (world.Occupancy.AnybodyWaitingFor(way, 0f, lengthM)) found.Waiting++;
            }

            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (world.Cars.Hold[car] == DrivingHold.Crossing) found.HeldByPaint++;
            }

            NothingButWalkersIsOnTheWalksSideOfAZebra(world, found);
            ARefusedBodyIsGrantedNoFurtherThanTheEdge(world, found);
            ARefusedBodyWalksInOnceItIsGranted(world, found);
            ABodyHoldsNoLaneItIsNotStandingIn(world, map, found);
        }

        found.GaveWay = world.GaveWayAtAKerb;
        return found;
    }
}
