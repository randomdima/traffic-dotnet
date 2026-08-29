using System.Collections.Concurrent;
using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The ways at a bay, asked the questions a junction's joins are asked: <b>the line goes where the bay is,
/// and the table says which ground it takes off the street it crosses</b> (TER-5c, GEN-4). A bay whose way
/// is in no table is a car park nothing in the town can see.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class BayWayTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>The town lays its bays' ways for the nominal car (CAR-11a), so that is what they are read against.</summary>
    static readonly CarBuild Nominal = CarBuild.Nominal(Config, Config.Car.DrivenFrontShare);

    /// <summary>And the longest body that can turn up in one of the bays those ways serve.</summary>
    static readonly CarBuild Longest = LongestOfTheFleet();

    static CarBuild LongestOfTheFleet()
    {
        var catalogue = CarCatalog.Load();
        var builds = CarBuilds.OfTheFleet(Config, catalogue);
        var longest = builds.Of(0);
        for (var variant = 1; variant < catalogue.SheetCount; variant++)
        {
            if (builds.Of(variant).LengthM > longest.LengthM) longest = builds.Of(variant);
        }

        return longest;
    }

    /// <summary>
    /// The slack a bound that is reached rather than approached needs: two lines separating at the fastest
    /// rate the measurement allows sit exactly on it, and single-precision arithmetic then puts them a
    /// hair's breadth either side.
    /// </summary>
    const float AttainedBoundM = 0.01f;

    public static TheoryData<string> Maps => Towns.EveryShippedMap();

    /// <summary>
    /// <b>One map's road graph and the ways at its bays, built once and read by every claim about them.</b>
    /// Both are functions of the plan and the figures and nothing here writes to either, so thirteen claims
    /// over eight maps were a hundred rebuilds of the same eight networks.
    /// </summary>
    static (RoadGraph Roads, BayWays Ways) Laid(string map) => Built.GetOrAdd(map, at =>
    {
        var plan = Towns.Of(at);
        var roads = RoadGraph.Build(plan, Config);
        return (roads, BayWays.Build(plan, roads, Config));
    });

    static readonly ConcurrentDictionary<string, (RoadGraph Roads, BayWays Ways)> Built = new();

    static BayWays WaysOf(string map, out RoadGraph roads)
    {
        var laid = Laid(map);
        roads = laid.Roads;
        return laid.Ways;
    }

    /// <summary>
    /// <b>VER-2, read off the ways</b>: every bay of every shipped map has a way, and every standing it
    /// offers is one a car can both get into and get out of. It is the same claim <c>ParkingTests</c> makes
    /// of the templates, asked of the thing the town actually drives — a bay whose template lays and whose
    /// way was never laid is a bay no route can reach.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryBayHasAStandingItCanBeBothEnteredAndLeftIn(string map)
    {
        var ways = WaysOf(map, out _);

        for (var bay = 0; bay < ways.BayCount; bay++)
        {
            Assert.True(ways.CanBeReached(bay), $"{map}: bay {bay} has no way at all");
            Assert.True(
                ways.CanStand(bay, noseIn: true) || ways.CanStand(bay, noseIn: false),
                $"{map}: bay {bay} has ways but no standing both of them serve");

            for (var slot = 0; slot < ways.WayCountOf(bay); slot++)
            {
                var way = ways.WayOf(bay, slot);
                Assert.True(
                    ways.CanStand(bay, ways.IsNoseIn(way)),
                    $"{map}: the way {way} at bay {bay} serves a standing the bay cannot be left in");
            }
        }
    }

    /// <summary>
    /// <b>A shape is a pair of ways over one piece of ground</b> (GEN-4f), where it is laid in both
    /// directions at all: the same length and the same metre of the same lane, driven into the bay one way
    /// round and out of it the other. What tells the pair apart is the gear.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void APairOfWaysIsOneShapeDrivenBothWays(string map)
    {
        var ways = WaysOf(map, out _);

        for (var bay = 0; bay < ways.BayCount; bay++)
        {
            for (var slot = 0; slot < ways.WayCountOf(bay); slot++)
            {
                var way = ways.WayOf(bay, slot);
                var mate = -1;
                for (var other = 0; other < ways.WayCountOf(bay); other++)
                {
                    var candidate = ways.WayOf(bay, other);
                    if (candidate == way) continue;
                    if (ways.LaneOf(candidate) != ways.LaneOf(way)) continue;
                    if (ways.IsNoseIn(candidate) != ways.IsNoseIn(way)) continue;

                    mate = candidate;
                }

                // Off the far lane a shape is laid in one direction only, so a way there is allowed no mate.
                if (mate < 0) continue;

                Assert.NotEqual(ways.IsEntry(way), ways.IsEntry(mate));
                Assert.NotEqual(ways.IsDrivenInReverse(way), ways.IsDrivenInReverse(mate));
                Assert.Equal(ways.LengthM(way), ways.LengthM(mate), 3);
                Assert.Equal(ways.AtLaneM(way), ways.AtLaneM(mate), 3);
            }
        }
    }

    /// <summary>
    /// <b>Reversing happens between a bay and the lane beside it and nowhere else</b> (GEN-4j). Every way a
    /// car drives in reverse is off the bay's nearest lane; the far lane carries the two that are driven
    /// under power — the nose-first way in and the backed-in way out — and nothing else. A car reversing
    /// across a lane of moving traffic is the movement this rule exists to refuse.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NothingReversesAcrossTheCarriageway(string map)
    {
        var ways = WaysOf(map, out var roads);
        var plan = Towns.Of(map);
        var reversed = 0;

        for (var way = ways.FirstWay; way < ways.TotalWayCount; way++)
        {
            var bay = ways.BayOfWay(way);
            var axleM = BayTemplate.RearAxleOfBayM(
                Nominal, plan.ParkingLots.SpacePositionM[bay], plan.ParkingLots.SpaceHeadingRad[bay], noseIn: true);

            var nearLane = roads.NearestLane(axleM, out _);
            if (!ways.IsDrivenInReverse(way)) continue;

            reversed++;
            Assert.True(
                ways.LaneOf(way) == nearLane,
                $"{map}: the way {way} at bay {bay} is driven in reverse off lane {ways.LaneOf(way)}, which "
                + $"is not the lane {nearLane} the bay stands beside");
        }

        Assert.True(
            ways.WayCount == 0 || reversed > 0,
            $"{map}: no way at any bay is driven in reverse, so nothing parks by hand");
    }

    /// <summary>
    /// <b>A bay on a two-way street is driven into from either side of it</b> (GEN-4f): the far lane lays
    /// the way in a car crossing the carriageway takes. A bay reachable only off the lane it stands beside
    /// is one half the traffic passing it has to drive to a junction and turn round to use.
    /// </summary>
    /// <remarks>
    /// It is the near lane's own refusals this reads the far side of. The turn into a bay swings away from
    /// it first, so the shape off the lane beside it reaches out over the carriageway — and a bar on that
    /// reach costs the nose-in standing, which is the only one the far lane can be driven in on.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ABayOnATwoWayStreetIsDrivenIntoFromEitherLane(string map)
    {
        var ways = WaysOf(map, out var roads);
        var plan = Towns.Of(map);

        for (var bay = 0; bay < ways.BayCount; bay++)
        {
            var nearLane = roads.NearestLane(
                BayTemplate.RearAxleOfBayM(
                    Nominal, plan.ParkingLots.SpacePositionM[bay], plan.ParkingLots.SpaceHeadingRad[bay],
                    noseIn: true),
                out _);

            if (nearLane < 0 || roads.LaneReverse[nearLane] < 0) continue;

            var offTheFarLane = false;
            for (var slot = 0; slot < ways.WayCountOf(bay); slot++)
            {
                var way = ways.WayOf(bay, slot);
                offTheFarLane |= ways.IsEntry(way) && ways.LaneOf(way) != nearLane;
            }

            Assert.True(offTheFarLane, $"{map}: bay {bay} cannot be driven into from across the street");
        }
    }

    /// <summary>
    /// A way in <b>leaves its lane where the lane says it does and ends square in the bay</b>. Both ends
    /// matter: a line that starts anywhere but on the lane is a line no car assembled onto it can follow,
    /// and one that ends anywhere but at the bay's own pose is a car parked askew.
    /// </summary>
    /// <remarks>
    /// <b>Along the lane in whichever direction the axle is travelling</b> (GEN-4j): nose-first the car
    /// comes up the lane, and backing in it has driven past the bay and is travelling back down it. Either
    /// way the line leaves along the lane rather than across it, which is the claim here.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void AWayInLeavesItsLaneAndEndsSquareInTheBay(string map)
    {
        var ways = WaysOf(map, out var roads);
        var plan = Towns.Of(map);

        for (var way = ways.FirstWay; way < ways.TotalWayCount; way++)
        {
            if (!ways.IsEntry(way)) continue;

            var bay = ways.BayOfWay(way);
            var arcs = ways.ArcsOf(way);
            var leaves = Spline.SampleAt(roads.ArcsOf(ways.LaneOf(way)), ways.AtLaneM(way));
            var alongTheLane = ways.IsNoseIn(way) ? 1f : -1f;
            Assert.True(
                (arcs[0].StartM - leaves.PositionM).Length() < 0.01f,
                $"{map}: the way into bay {bay} does not begin on the lane it leaves");
            Assert.True(
                Vector2.Dot(Heading.Unit(arcs[0].HeadingRad), leaves.Direction) * alongTheLane > 0.999f,
                $"{map}: the way into bay {bay} leaves its lane across it rather than along it");

            var headingRad = plan.ParkingLots.SpaceHeadingRad[bay];
            var axleM = BayTemplate.RearAxleOfBayM(
                Nominal, plan.ParkingLots.SpacePositionM[bay], headingRad, ways.IsNoseIn(way));

            var ends = Spline.SampleAt(arcs, ways.LengthM(way));
            Assert.True(
                (ends.PositionM - axleM).Length() < 0.05f,
                $"{map}: the way into bay {bay} ends {(ends.PositionM - axleM).Length():F2} m off its pose");
            Assert.True(
                Vector2.Dot(ends.Direction, Heading.Unit(headingRad)) > 0.999f,
                $"{map}: the way into bay {bay} ends off square");
        }
    }

    /// <summary>
    /// And a way out <b>begins at the bay's own pose and ends on the lane it lands on</b>, travelled the
    /// way the rear axle goes — which while reversing is the way the car's nose is not pointing.
    /// </summary>
    /// <remarks>
    /// <b>On its centreline, exactly, and that is what one line driven both ways buys</b> (GEN-4f): the way
    /// out is the way in reversed, so it lands where that one left rather than wherever a second solve
    /// aimed back at the lane happened to reach. <b>And it ends with the car facing along the lane either
    /// way round</b> (GEN-4j) — reversing out, the axle travels back up the lane while the car points down
    /// it; driving out of a bay it backed into, the two agree.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void AWayOutBeginsInTheBayAndLandsOnItsLane(string map)
    {
        var ways = WaysOf(map, out var roads);
        var plan = Towns.Of(map);

        for (var way = ways.FirstWay; way < ways.TotalWayCount; way++)
        {
            if (ways.IsEntry(way)) continue;

            var bay = ways.BayOfWay(way);
            var headingRad = plan.ParkingLots.SpaceHeadingRad[bay];
            var axleM = BayTemplate.RearAxleOfBayM(
                Nominal, plan.ParkingLots.SpacePositionM[bay], headingRad, ways.IsNoseIn(way));

            var arcs = ways.ArcsOf(way);
            Assert.True(
                (arcs[0].StartM - axleM).Length() < 0.01f,
                $"{map}: the way out of bay {bay} does not begin at its pose");

            var lane = roads.ArcsOf(ways.LaneOf(way));
            var ends = Spline.SampleAt(arcs, ways.LengthM(way));
            var lands = Spline.SampleAt(lane, ways.AtLaneM(way));
            Assert.True(
                (ends.PositionM - lands.PositionM).Length() <= AttainedBoundM,
                $"{map}: the way out of bay {bay} lands {(ends.PositionM - lands.PositionM).Length():F3} m "
                + "off the lane it is recorded against");

            // The car ends facing along the lane. Reversing out, the rear axle travels against its heading,
            // so the line's own end direction is the lane's reversed; driving out, the two agree.
            var facing = ways.IsDrivenInReverse(way) ? -ends.Direction : ends.Direction;
            Assert.True(
                Vector2.Dot(facing, lands.Direction) > 0.99f,
                $"{map}: the way out of bay {bay} ends facing the wrong way down its lane");
        }
    }

    /// <summary>
    /// <b>What a parked body holds is the bay, and the bay is ground nothing else is driven over</b>
    /// (<see cref="BayStandings"/>): every metre of it, on both of the bay's ways, is clear of every crossing
    /// but the one its own other way makes. A stretch that reached into a crossing would be a parked car
    /// cutting the street beside it, or the neighbour working into the bay next door.
    /// </summary>
    /// <remarks>
    /// <b>And it is never shorter than the body's own tail</b>, which is the stretch a parked car has always
    /// laid: the floor is what stops a bay whose ways are crossed to the axle from holding nothing at all.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void WhatAParkedBodyHoldsIsGroundNothingElseIsDrivenOver(string map)
    {
        var ways = WaysOf(map, out var roads);
        var crossings = BayCrossings.Over(ways, roads, Config);
        var standings = BayStandings.Of(ways, crossings, Config);

        for (var way = ways.FirstWay; way < ways.TotalWayCount; way++)
        {
            var bay = ways.BayOfWay(way);

            // Which end of the body lies along the way is the standing's: nose-first the tail, backed in
            // the nose, over a way that runs that much deeper into the space (GEN-4j). <b>Asked of the
            // nominal car</b>, because that is the body the town laid these ways clear of (CAR-11a): a
            // longer one holds more, and what that costs the bay next door is
            // <see cref="ALongerBodyHoldsMoreOfItsBaysWay"/>.
            var bodyM = ways.IsNoseIn(way) ? Nominal.TailBehindAxleM : Nominal.NoseAheadOfAxleM;
            var (fromM, toM) = ways.WhereABodyInTheBayStandsM(way, standings.HoldsM(way, bodyM));
            Assert.True(
                toM - fromM >= bodyM - AttainedBoundM,
                $"{map}: the body in bay {bay} holds {toM - fromM:0.00} m of way {way}, less than the "
                + $"{bodyM:0.00} m of itself that lies along it");

            foreach (ref readonly var section in crossings.Of(way))
            {
                if (ways.IsBayWay(section.OnWay) && ways.BayOfWay(section.OnWay) == bay) continue;

                Assert.False(
                    section.MineFromM < toM && fromM < section.MineToM,
                    $"{map}: the body in bay {bay} holds {fromM:0.00}–{toM:0.00} m of way {way}, which "
                    + $"{section.MineFromM:0.00}–{section.MineToM:0.00} m of it is driven over by "
                    + $"{Named(ways, roads, section.OnWay)}");
            }
        }
    }

    /// <summary>
    /// <b>A longer body holds more of its bay's way</b> (CAR-11). The stretch a parked car writes into the
    /// book is what lies along the way of the car that is actually standing there, so a van's tail is ground
    /// the next car in is refused — where the nominal car's would have left it free.
    /// </summary>
    /// <remarks>
    /// This is the price of the town's bays being the nominal car's (CAR-11a) and it is paid the right way
    /// round: the neighbour <em>sees</em> the ground and waits for it, rather than driving into a body the
    /// book said was not there.
    /// </remarks>
    [Fact]
    public void ALongerBodyHoldsMoreOfItsBaysWay()
    {
        var ways = WaysOf("Odesa", out var roads);
        var standings = BayStandings.Of(ways, BayCrossings.Over(ways, roads, Config), Config);

        var grew = 0;
        for (var way = ways.FirstWay; way < ways.TotalWayCount; way++)
        {
            var noseIn = ways.IsNoseIn(way);
            var nominalM = noseIn ? Nominal.TailBehindAxleM : Nominal.NoseAheadOfAxleM;
            var longestM = noseIn ? Longest.TailBehindAxleM : Longest.NoseAheadOfAxleM;

            Assert.True(standings.HoldsM(way, longestM) >= standings.HoldsM(way, nominalM));
            if (longestM > nominalM && standings.HoldsM(way, longestM) > standings.HoldsM(way, nominalM)) grew++;
        }

        Assert.True(grew > 0, "no bay in Odesa holds more ground for the longest body in the fleet than for the nominal car");
    }

    /// <summary>
    /// <b>The mini-junction is wired</b>: a way at a bay is driven over the carriageway, and the lane it
    /// crosses says so in its own row. A table with the bay's half and not the lane's is a car working into
    /// a bay that the traffic cannot see.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryWayAtABayCrossesTheStreetAndTheStreetSaysSo(string map)
    {
        var ways = WaysOf(map, out var roads);
        var crossings = BayCrossings.Over(ways, roads, Config);
        var crossedALane = 0;

        for (var way = ways.FirstWay; way < ways.TotalWayCount; way++)
        {
            foreach (ref readonly var section in crossings.Of(way))
            {
                Assert.NotEqual(way, section.OnWay);

                var back = false;
                foreach (ref readonly var other in crossings.Of(section.OnWay)) back |= other.OnWay == way;

                Assert.True(
                    back,
                    $"{map}: the way {way} at bay {ways.BayOfWay(way)} takes ground off {section.OnWay} "
                    + "and not the other way round");

                if (section.OnWay < roads.LaneCount) crossedALane++;
            }
        }

        Assert.True(
            ways.WayCount == 0 || crossedALane > 0,
            $"{map}: no way at a bay is driven over any lane, so nothing on the street is held off one");
    }

    /// <summary>
    /// <b>A section begins and ends where the two lines are together.</b> Its ends are what the measurement
    /// promises — the last sample inside the clearance, opened out by the step it was found at — and a
    /// section that began or ended anywhere else would be a street shut over ground nothing is driven on.
    /// </summary>
    /// <remarks>
    /// <b>The metres between the ends are not asserted, and deliberately.</b> Two lines that come together
    /// twice take the ground between as one section (<see cref="LineOverlap"/>): the alternative is two
    /// sections with a gap in the middle that neither of the pair holds, which is a car let into the very
    /// ground the other is about to sweep.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EverySectionAtABayIsGroundTheTwoLinesShare(string map)
    {
        var ways = WaysOf(map, out var roads);
        var crossings = BayCrossings.Over(ways, roads, Config);
        var measured = 0;
        var worstM = 0f;
        var worst = string.Empty;

        for (var way = ways.FirstWay; way < ways.TotalWayCount; way++)
        {
            var mine = ways.ArcsOf(way);
            foreach (ref readonly var section in crossings.Of(way))
            {
                measured++;

                // <b>Two steps and not one</b>, because the table does not record which of the two ways a
                // section was found by (<see cref="LineOverlap.Measure"/>). Walked directly, an end stands
                // the clearance plus the step it was opened out by off the other line. Taken as the shadow
                // of a section found from the other side, the nearest sample is allowed the clearance and a
                // step of reach before it too is opened out by a step — so the sum is a bound the geometry
                // can sit exactly on, and is compared to as one.
                var stepM = StepOf(ways, roads, section.OnWay);
                var reachM = Config.JunctionCrossingClearanceM + (stepM * 2f) + AttainedBoundM;
                var crossed = LineOf(ways, roads, section.OnWay);
                var apartM = MathF.Max(OffTheLineM(mine, At(crossed, section.FromM)), OffTheLineM(mine, At(crossed, section.ToM)));
                if (apartM - reachM <= worstM) continue;

                worstM = apartM - reachM;
                worst =
                    $"{map}: the way {way} at bay {ways.BayOfWay(way)} takes {section.FromM:0.0}–"
                    + $"{section.ToM:0.0} m of {Named(ways, roads, section.OnWay)} at its own "
                    + $"{section.MineFromM:0.0}–{section.MineToM:0.0} m, and the further of those two ends "
                    + $"stands {apartM:0.00} m off it against a reach of {reachM:0.00} "
                    + $"(clearance {Config.JunctionCrossingClearanceM:0.00} m, step {stepM:0.00} m)";
            }
        }

        Assert.True(worstM <= 0f, worst);
        Assert.True(ways.WayCount == 0 || measured > 0, $"{map}: the bays take no ground off anything");
    }

    /// <summary>
    /// And the other half: <b>two ways at neighbouring bays that share ground are in the table</b>. Two
    /// bays of one lot are a car's width apart, so the lines into them cross — read only against the lanes,
    /// two cars work into neighbouring bays at once and meet between them.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void TwoWaysAtNeighbouringBaysThatShareGroundAreInTheTable(string map)
    {
        var ways = WaysOf(map, out var roads);
        var crossings = BayCrossings.Over(ways, roads, Config);

        for (var way = ways.FirstWay; way < ways.TotalWayCount; way++)
        {
            for (var other = way + 1; other < ways.TotalWayCount; other++)
            {
                if (ways.BayOfWay(way) == ways.BayOfWay(other)) continue;

                // Two ways whose starts stand further apart than the pair of them is long share nothing,
                // and a town has thousands of them: the fine measurement is for the ones that might.
                var reachM = ways.LengthM(way) + ways.LengthM(other) + Config.JunctionCrossingClearanceM;
                if ((ways.ArcsOf(way)[0].StartM - ways.ArcsOf(other)[0].StartM).Length() > reachM) continue;

                var apartM = FurthestApart(ways.ArcsOf(way), ways.ArcsOf(other), ways.LengthM(other));
                if (apartM > Config.JunctionCrossingClearanceM * 0.5f) continue;

                Assert.True(
                    Takes(crossings, way, other),
                    $"{map}: the ways {way} and {other} at bays {ways.BayOfWay(way)} and "
                    + $"{ways.BayOfWay(other)} pass {apartM:0.00} m apart and neither takes ground off the other");
            }
        }
    }

    static bool Takes(WayCrossings crossings, int way, int other)
    {
        foreach (ref readonly var section in crossings.Of(way))
        {
            if (section.OnWay == other) return true;
        }

        return false;
    }

    /// <summary>What a way is, for a message that has to say which of the three bands it came out of.</summary>
    static string Named(BayWays ways, RoadGraph roads, int way) =>
        ways.IsBayWay(way) ? $"the way {way} at bay {ways.BayOfWay(way)} ({ways.LengthM(way):0.0} m)"
        : way < roads.LaneCount ? $"lane {way} ({roads.LaneLengthM[way]:0.0} m)"
        : $"join {roads.TurnOfWay(way)} ({roads.JoinLengthM(roads.TurnOfWay(way)):0.0} m)";

    /// <summary>The line of any way of the town, whichever band it is in.</summary>
    static ReadOnlySpan<ArcSeg> LineOf(BayWays ways, RoadGraph roads, int way) =>
        ways.IsBayWay(way) ? ways.ArcsOf(way)
        : way < roads.LaneCount ? roads.ArcsOf(way)
        : roads.JoinArcs(roads.TurnOfWay(way));

    /// <summary>How coarsely that way was sampled, which is what a section's edges were rounded out by.</summary>
    static float StepOf(BayWays ways, RoadGraph roads, int way)
    {
        // A way at a bay is walked as finely as the sample budget allows, because the metres a parked body
        // may call its own are read off the answer (<see cref="BayCrossings"/>).
        if (ways.IsBayWay(way)) return ways.LengthM(way) / (LineOverlap.MostSamples - 1);

        // A lane is sampled over a window round the bay rather than whole, so its step is the clearance
        // itself; anything short enough to sample whole steps no wider than that either.
        var lengthM = way < roads.LaneCount ? roads.LaneLengthM[way] : roads.JoinLengthM(roads.TurnOfWay(way));
        return MathF.Max(Config.JunctionCrossingClearanceM, lengthM / (LineOverlap.MostSamples - 1));
    }

    static Vector2 At(ReadOnlySpan<ArcSeg> arcs, float alongM) => Spline.SampleAt(arcs, alongM).PositionM;

    /// <summary>How near two whole lines come anywhere along the second of them.</summary>
    static float FurthestApart(ReadOnlySpan<ArcSeg> over, ReadOnlySpan<ArcSeg> crossed, float lengthM)
    {
        var leastM = float.PositiveInfinity;
        for (var alongM = 0f; alongM <= lengthM; alongM += 0.1f)
        {
            leastM = MathF.Min(leastM, OffTheLineM(over, Spline.SampleAt(crossed, alongM).PositionM));
        }

        return leastM;
    }

    static float OffTheLineM(ReadOnlySpan<ArcSeg> arcs, Vector2 pointM)
    {
        var lengthM = Spline.TotalLengthM(arcs);
        var alongM = Spline.ProjectM(arcs, pointM, lengthM * 0.5f, lengthM);
        return (Spline.SampleAt(arcs, alongM).PositionM - pointM).Length();
    }


    /// <summary>
    /// <b>A stretch a leg may turn at is one some bay lays the pair of ways for</b> (GEN-4l): a way in off
    /// the lane the car comes down and a way out onto the lane running back, <em>in one standing</em> —
    /// because a car that noses in reverses out onto the lane beside the bay and has no other way out. The
    /// flags the router is priced off say exactly that, or say dead end (`P-19`).
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryStretchALegMayTurnAtIsOneABayOrADeadEndOffers(string map)
    {
        var ways = WaysOf(map, out var roads);
        var turns = BayWays.WhereALegMayTurn(roads, ways);

        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            var back = roads.LaneReverse[lane];
            if (!turns[lane])
            {
                Assert.False(
                    back >= 0 && roads.TurnsFrom(lane).Length == 0,
                    $"{map}: lane {lane} has no way out of it and no way back down it either");
                continue;
            }

            Assert.True(back >= 0, $"{map}: lane {lane} may be turned at and has no lane running back");
            if (roads.TurnsFrom(lane).Length == 0) continue;

            var way = ways.TheWayToTurnIn(TheBayThatTurns(ways, lane, back), lane, back);
            Assert.NotEqual(BayWays.NoWay, way);
            Assert.True(ways.IsEntry(way), $"{map}: the way a car turns in at lane {lane} is not a way in");
        }
    }

    /// <summary>The first bay off this lane the pair of ways is laid at, which the flag above says there is one of.</summary>
    static int TheBayThatTurns(BayWays ways, int lane, int back)
    {
        foreach (var bay in ways.BaysOffLane(lane))
        {
            if (ways.TheWayToTurnIn(bay, lane, back) != BayWays.NoWay) return bay;
        }

        return -1;
    }

    /// <summary>
    /// <b>And the way out of that pair lands on the lane running back</b>, which is the whole of what makes
    /// the park and the unpark a turn (GEN-4l): a car that came down one side of the street leaves down the
    /// other.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void TheWayOutOfATurnLandsOnTheLaneRunningBack(string map)
    {
        var ways = WaysOf(map, out var roads);
        var turns = 0;

        for (var lane = 0; lane < roads.LaneCount && turns < 20; lane++)
        {
            var back = roads.LaneReverse[lane];
            if (back < 0) continue;

            var bay = TheBayThatTurns(ways, lane, back);
            if (bay < 0) continue;

            var wayIn = ways.TheWayToTurnIn(bay, lane, back);
            Assert.Equal(lane, ways.LaneOf(wayIn));

            var noseIn = ways.IsNoseIn(wayIn);
            var wayOut = -1;
            for (var slot = 0; slot < ways.WayCountOf(bay); slot++)
            {
                var way = ways.WayOf(bay, slot);
                if (!ways.IsEntry(way) && ways.IsNoseIn(way) == noseIn && ways.LaneOf(way) == back) wayOut = way;
            }

            Assert.True(wayOut >= 0, $"{map}: bay {bay} is turned into off lane {lane} and has no way out onto {back}");
            turns++;
        }
    }
}
