using System.Numerics;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// `P-12` as it is actually arrived at: <b>a body crossing the road writes itself into the road's book,
/// and the drivers read it there</b>. What is asked of a running town is that the writing happens, that
/// the book has room for it, and that it binds somebody.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class CrossingTrafficTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    const int Ticks = 3_600;

    /// <summary>
    /// <b>Somebody crosses, and the road knows.</b> A crossing rule reading a book nothing is ever written
    /// into is a rule that costs a tick and yields to nobody, and no assertion about the arithmetic would
    /// notice.
    /// </summary>
    [Fact]
    public void ABodyOnThePaintIsInTheRoadsBook()
    {
        var (onThePaint, _, _) = Watch(Towns.Fixture);

        Assert.True(onThePaint > 0, "nobody in a minute of the fixture town was on a crossing");
    }

    /// <summary>
    /// <b>The book is never full.</b> Past its bound a stretch is dropped — and unlike a car's, a dropped
    /// stretch here is a body on a crossing that no driver can see, since the question it answers has no
    /// geometry behind it any more.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void TheBookNeverReachesItsBound(string map)
    {
        var (_, mostSlots, _) = Watch(map);
        using var world = new TownWorld(Towns.Fresh(map), Config);

        Assert.True(
            mostSlots < world.Occupancy.Capacity,
            $"{map}: the road's book held {mostSlots} of {world.Occupancy.Capacity} stretches");
    }

    /// <summary>And the reading binds: somewhere in a busy town a driver is held by paint.</summary>
    [Fact]
    public void ADriverIsHeldByACrossing()
    {
        var (_, _, heldByPaint) = Watch("Odesa");

        Assert.True(heldByPaint > 0, "no driver in a minute of Odesa was held by a crossing");
    }

    /// <summary>
    /// <b>A car writes into the road's book and into no other</b> (TER-5c.1). A zebra is a walk laid over a
    /// carriageway, so the ground under it has two names and one owner: what a car has of it is a stretch of
    /// the lane it is driving. Marked on the walk as well, one body held one piece of ground twice, in two
    /// books that could disagree about it — and the picture said a crossing was a thing the traffic holds
    /// rather than ground with a lane under it.
    /// </summary>
    [Fact]
    public void NoCarIsEverInThePavementsBook()
    {
        using var world = new TownWorld(Towns.Fresh("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        Span<LaneSlot> slots = stackalloc LaneSlot[64];
        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            foreach (var way in world.Footfall.OccupiedWays)
            {
                var count = world.Footfall.CopyTo(way, slots);
                for (var at = 0; at < count; at++)
                {
                    Assert.True(
                        slots[at].Of == LaneRoster.Walking,
                        $"car {slots[at].Occupant} holds {slots[at].FromM:0.00}–{slots[at].ToM:0.00} m of "
                        + $"walking way {way}");
                }
            }
        }
    }

    /// <summary>
    /// <b>And a body refused a lane of a zebra is granted no further than that lane's edge</b> (`PER-15`,
    /// TER-4c.1). The refusal is made once, when the band is asked for against the road's book, and read
    /// back into the walk's own metres — so what holds a walker at a lane's edge and what holds a car off
    /// the body in front of it are one arrangement asked from two sides.
    /// </summary>
    [Fact]
    public void ABodyRefusedALaneIsGrantedNoFurtherThanItsEdge()
    {
        using var world = new TownWorld(Towns.Fresh("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        var refused = 0;
        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            for (var person = 0; person < world.People.Count; person++)
            {
                // The way the refusal was made on and the way the body is standing on, which are the same
                // way for anybody already on the paint and the only case a grant is measured in.
                var way = world.People.RefusedWay[person];
                if (way == PersonFleet.NoWay || way != world.People.OnWay[person]) continue;

                refused++;
                var grantedToM = world.People.OnWayM[person] + world.People.AuthorityM[person]
                                 + Config.PersonStandstillGapM + world.People.RadiusM[person];

                Assert.True(
                    grantedToM <= world.People.RefusedAtM[person] + ToleranceM,
                    $"walker {person} was refused a lane and granted to {grantedToM:0.00} m of way {way}, "
                    + $"where that lane's band begins at {world.People.RefusedAtM[person]:0.00} m");
            }
        }

        Assert.True(refused > 0, "nobody in a minute of Odesa was refused a lane of a crossing it was on");
    }

    /// <summary>
    /// <b>And the refusal is spent the moment it is lifted.</b> A body granted the band in front of it walks
    /// into it — the one thing a cut taken from the wrong record gets wrong, since a grant that re-asked the
    /// book, or read the patience the wait is clocked on, would hold a body at the edge of ground it had just
    /// been given and nobody would ever finish crossing.
    /// </summary>
    [Fact]
    public void ABodyRefusedALaneWalksIntoItOnceItIsGranted()
    {
        using var world = new TownWorld(Towns.Fresh("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        var wayRefusedOn = new int[world.People.Count];
        var metreRefusedAt = new float[world.People.Count];
        Array.Fill(wayRefusedOn, PersonFleet.NoWay);

        var gotIn = 0;
        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            for (var person = 0; person < world.People.Count; person++)
            {
                var way = world.People.OnWay[person];
                if (way != PersonFleet.NoWay && way == wayRefusedOn[person]
                    && world.People.OnWayM[person] > metreRefusedAt[person])
                {
                    gotIn++;
                    wayRefusedOn[person] = PersonFleet.NoWay;
                    continue;
                }

                if (world.People.RefusedWay[person] == PersonFleet.NoWay) continue;

                wayRefusedOn[person] = world.People.RefusedWay[person];
                metreRefusedAt[person] = world.People.RefusedAtM[person];
            }
        }

        Assert.True(gotIn > 0, "nobody in a minute of Odesa got into a lane of a crossing they were refused");
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
    public void ABodyHoldsTheLaneItIsStandingIn(string map)
    {
        using var world = new TownWorld(Towns.Fresh(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        var held = new int[world.People.Count];
        var standingIn = new int[world.People.Count];
        Span<LaneSlot> slots = stackalloc LaneSlot[64];

        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            Array.Clear(held);
            Array.Clear(standingIn);

            foreach (var way in world.Occupancy.OccupiedWays)
            {
                if (!world.Occupancy.WayIsLane(way)) continue;

                var lane = world.Occupancy.WayIndex(way);
                var count = world.Occupancy.CopyTo(way, slots);
                for (var at = 0; at < count; at++)
                {
                    if (slots[at].Use != LaneUse.OnFoot) continue;

                    held[slots[at].Occupant]++;
                    if (IsStandingInTheLane(world, slots[at], lane)) standingIn[slots[at].Occupant]++;
                }
            }

            for (var person = 0; person < held.Length; person++)
            {
                Assert.True(
                    held[person] <= standingIn[person] + 1,
                    $"{map}: walker {person} holds {held[person]} lanes and is standing in "
                    + $"{standingIn[person]} of them, {LanesHeld(world, person)}");
            }
        }
    }

    /// <summary>
    /// Whether the body a stretch belongs to is inside that lane's own band — the margin a body on a road
    /// is owed included, since that is the reach its claim is laid with, and the slack two touching bands
    /// were rounded out by, since a body inside that much of a boundary is standing in both lanes.
    /// </summary>
    static bool IsStandingInTheLane(TownWorld world, in LaneSlot slot, int lane)
    {
        var alongM = Math.Clamp((slot.FromM + slot.ToM) * 0.5f, 0f, world.Roads.LaneLengthM[lane]);
        var on = Spline.SampleAt(world.Roads.ArcsOf(lane), alongM);
        var offM = world.People.PositionM[slot.Occupant] - on.PositionM;

        return MathF.Abs(Vector2.Dot(offM, on.Right))
               <= (world.Roads.LaneWidthM[lane] * 0.5f)
               + (world.People.RadiusM[slot.Occupant] * Config.Person.RoadClaimMargin)
               + BandSlackM;
    }

    /// <summary>What the two edges of a pair of touching bands were rounded outward by when they were projected.</summary>
    const float BandSlackM = 0.5f;

    /// <summary>Which lanes a body has a stretch of and how far off each one's line it stands, for a failure to name.</summary>
    static string LanesHeld(TownWorld world, int person)
    {
        var text = new System.Text.StringBuilder();
        Span<LaneSlot> slots = stackalloc LaneSlot[64];
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            if (!world.Occupancy.WayIsLane(way)) continue;

            var lane = world.Occupancy.WayIndex(way);
            var count = world.Occupancy.CopyTo(way, slots);
            for (var at = 0; at < count; at++)
            {
                if (slots[at].Use != LaneUse.OnFoot || slots[at].Occupant != person) continue;

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

    public static TheoryData<string> Maps => Towns.EveryShippedMap();

    /// <summary>
    /// A run, watched tick by tick: how many walker-ticks were spent on paint the road knew about, the
    /// fullest the road's book ever got, and how many car-ticks a crossing was the term that bound.
    /// </summary>
    static (int OnThePaint, int MostSlots, int HeldByPaint) Watch(string map)
    {
        using var world = new TownWorld(Towns.Fresh(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        var onThePaint = 0;
        var mostSlots = 0;
        var heldByPaint = 0;
        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            mostSlots = Math.Max(mostSlots, world.Occupancy.SlotCount);

            foreach (var way in world.Occupancy.OccupiedWays)
            {
                if (world.Occupancy.AnybodyOnFoot(way, 0f, world.Occupancy.WayLengthM(way))) onThePaint++;
            }

            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (world.Cars.Hold[car] == DrivingHold.Crossing) heldByPaint++;
            }
        }

        return (onThePaint, mostSlots, heldByPaint);
    }
}
