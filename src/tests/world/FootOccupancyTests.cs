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
/// The pavement's book asked of a running town: that it describes one, that a walker's place in it is the
/// place its own line put it, and that the grant holds one body off the next exactly as the road's does.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class FootOccupancyTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>Every walker on a line of its own is in the book.</b> One that is not is a body nobody behind it
    /// can see at all, which is the misreading the whole index exists to remove.
    /// </summary>
    [Fact]
    public void EveryWalkerOnALineOfItsOwnIsInTheBook()
    {
        var world = Run("Odesa");

        var onALine = 0;
        for (var person = 0; person < world.People.Count; person++)
        {
            if (world.People.OnWay[person] != PersonFleet.NoWay) onALine++;
        }

        Assert.True(onALine > 0, "nobody in a busy town was walking a line of their own");
        Assert.True(
            world.Footfall.SlotCount >= onALine,
            $"{onALine} walkers were on a line and the book held {world.Footfall.SlotCount} stretches");
    }

    /// <summary>
    /// <b>The book is never full.</b> Past its bound a stretch is dropped, and a dropped stretch is a body
    /// the walker behind it is granted the ground of.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void TheBookNeverReachesItsBound(string map)
    {
        var world = Run(map);
        if (world.People.Count == 0) return;

        Assert.True(
            world.Footfall.SlotCount < world.Footfall.Capacity,
            $"the book held {world.Footfall.SlotCount} of {world.Footfall.Capacity} stretches");
    }

    /// <summary>
    /// <b>A walker's place in the book is the place its own line put it.</b> It is read off the point being
    /// walked at rather than searched for, so the one thing that could go wrong is the bookkeeping: a way
    /// or a distance carried home through the wrong offset puts a body somewhere nobody is standing, and
    /// every grant taken against it is then a grant over ground nobody is on.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void AWalkersPlaceInTheBookIsWhereItsBodyActuallyStands(string map)
    {
        var world = Run(map);
        var walking = world.Walking;

        var placed = 0;
        Span<ArcSeg> scratch = stackalloc ArcSeg[MostArcs];
        for (var person = 0; person < world.People.Count; person++)
        {
            var way = world.People.OnWay[person];
            if (way == PersonFleet.NoWay) continue;

            var index = world.Footfall.WayIndex(way);
            var line = world.Footfall.WayIsLane(way) ? walking.LaneOf(index) : walking.JoinArcs(index);
            if (line.Length == 0) continue;

            placed++;
            var alongM = Math.Clamp(world.People.OnWayM[person], 0f, world.Footfall.WayLengthM(way));
            var onTheWayM = Spline.SampleAt(line, alongM).PositionM;

            Assert.True(
                (onTheWayM - world.People.PositionM[person]).Length() <= OffItsLaneM,
                $"walker {person} stands {(onTheWayM - world.People.PositionM[person]).Length():0.00} m from "
                + $"{alongM:0.00} m along way {way}, which is where the book has it");
        }

        Assert.True(placed > 0 || OnALine(world) == 0, "nobody walking a line of the network was placed on a way");
    }

    /// <summary>
    /// <b>Nobody is granted ground somebody else under way will still be standing on once they have
    /// stopped</b> — the same property the road's book is for, asked of the pavement's, and the whole of
    /// what holds one walker off the next.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asked of the ways rather than of the walkers, because that is where two grants would meet. A grant
    /// that came out empty is skipped: a body already inside somebody else is a contact, and standing still
    /// is all the walker can do about it.
    /// </para>
    /// <para>
    /// <b>And of the bodies under way alone</b> (PER-24). A body going nowhere is ground a walk is granted
    /// straight through on purpose: what a walker does about one is step round it, and the grant it is
    /// cut at is the one thing that would stop it doing so.
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
        var book = world.Footfall;

        var granted = 0;
        Span<LaneSlot> slots = stackalloc LaneSlot[64];
        foreach (var way in book.OccupiedWays)
        {
            var count = book.CopyTo(way, slots);
            for (var behind = 0; behind < count; behind++)
            {
                if (slots[behind].Use != LaneUse.Reserved) continue;
                if (world.People.OnWay[slots[behind].Occupant] != way) continue;

                // A walker granted nothing is not being sent anywhere, and the standstill gap this adds back
                // is then the footprint it is standing in rather than ground it may walk into. A queue that
                // closes tighter than the gap — a body still stopping when the one ahead of it is held at a
                // kerb — is that and not a grant, and standing still is all the walker can do about it.
                if (world.People.AuthorityM[slots[behind].Occupant] <= 0f) continue;

                var grantedToM = GrantedToM(world, slots[behind]);
                if (grantedToM <= slots[behind].FromM) continue;

                granted++;
                for (var ahead = behind + 1; ahead < count; ahead++)
                {
                    if (slots[ahead].Occupant == slots[behind].Occupant) continue;

                    // <b>Of the bodies under way, which is what a walker is held off</b> (PER-24). A body
                    // going nowhere is ground a walk is granted straight through, because what a walker
                    // does about one is step round it — asked about here, the claim would be that nobody
                    // may be granted the ground it is the whole point of the rule to grant.
                    if (slots[ahead].Use != LaneUse.Reserved) continue;

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
                    var gripMps2 = Config.Person.FootGripMps2 * world.People.GroundCoefficient[slots[behind].Occupant];
                    var restingM = MathF.Max(0f, slots[ahead].AlongMps * slots[ahead].AlongMps / (2f * gripMps2));

                    Assert.True(
                        grantedToM <= slots[ahead].FromM + restingM + ToleranceM,
                        $"walker {slots[behind].Occupant} was granted to {grantedToM:0.00} m of way {way}, "
                        + $"where walker {slots[ahead].Occupant} comes to rest at "
                        + $"{slots[ahead].FromM + restingM:0.00} m");
                }
            }
        }

        Assert.True(granted > 0, "nobody in a busy town was granted any pavement at all");
    }

    /// <summary>
    /// <b>And somebody is actually held by it.</b> A book nothing ever cuts a grant against is a book that
    /// costs a tick and changes nothing, which no test of the arithmetic above would notice.
    /// </summary>
    [Fact]
    public void SomebodyInABusyTownIsHeldBehindSomebodyElse() =>
        Assert.True(Of("Odesa").Held > 0, "nobody in a minute of a busy town waited behind anybody");

    /// <summary>
    /// Where a grant ends, <b>in the metres of the way the body is standing on</b>. What goes into the book
    /// is what the walker asked for — the cut is taken off it afterwards — so the granted end is built back
    /// up from the place the book has the body and the ground it was given in front of it.
    /// </summary>
    /// <remarks>
    /// Only the body on the way can be measured this way, and that is a fact about a walk rather than a
    /// shortcut: <see cref="LineWay.LineFromM"/> on the walking side is a distance from the body, so way
    /// metres and walk metres share an origin for exactly one walker per stretch.
    /// </remarks>
    static float GrantedToM(TownWorld world, in LaneSlot asked)
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
    /// How far from the line it is held on a walker may stand and still be the body the book has there.
    /// It is a whole pavement band, because a body is pushed about: what is being checked is that the place
    /// carried through the book is the body's own place and not a bookkeeping error, and those are metres
    /// out and regularly whole streets.
    /// </summary>
    static float OffItsLaneM => Config.Road.PavementWidthM;

    /// <summary>Room for one stretch's arcs. More than any stretch of a shipped town's pavement is drawn with.</summary>
    const int MostArcs = 64;

    /// <summary>A minute of a town, sampled every tick of it.</summary>
    const int Ticks = 3_600;

    public static TheoryData<string> Maps => Towns.EveryShippedMap();

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
}
