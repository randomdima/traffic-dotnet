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
/// <b>The reservations at a junction</b>: how far into a box a plan reaches and from where, what it may
/// write on the ways it only crosses, what a light's hold leaves it — and which joins a body standing in a
/// box is on.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P1)]
public class JunctionClaimTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// Two minutes of the town: a fleet whose cars accelerate and brake at their own rates (CAR-11) arrives
    /// at the junctions irregularly, so the rarer cases need a longer watch to turn up at all.
    /// </summary>
    const int Ticks = 7_200;

    public static TheoryData<string> Maps => Towns.EveryTown();

    /// <summary>The suite's own towns that have traffic to cross a box with.</summary>
    public static TheoryData<string> CrossedMaps
    {
        get
        {
            var maps = new TheoryData<string>();
            foreach (var map in (string[])[Towns.Fixture, Towns.City])
            {
                if (Towns.AnythingDrives(map)) maps.Add(map);
            }

            return maps;
        }
    }

    /// <summary>
    /// <b>One run of one map, watched by every question below at once.</b> Each holds the first tick it was
    /// broken on, or null, beside the census that says the run had anything to say about it at all.
    /// </summary>
    sealed class Watched
    {
        public string? PastARed, MissedTheNearEdge, LaidOnAWayItCrosses, CutFromBehind;

        public int AtARed, Reaching, Crossing, HeldByAPlan;
    }

    static readonly ConcurrentDictionary<string, Watched> Runs = new();

    static Watched Of(string map) => Runs.GetOrAdd(map, Watch);

    /// <summary>
    /// The town ticked, with the reservations laid again before they are read each tick — so that what is
    /// read is one rebuild and the fleet it was laid from, and not a rebuild and the tick driven after it.
    /// </summary>
    static Watched Watch(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var found = new Watched();

        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            world.RebuildProximityIndex();

            NothingStoppedAtARedHoldsTheBox(world, map, tick, found);
            APlanReachesIntoTheBoxFromItsMouth(world, map, tick, found);
            NoPieceIsLaidOnAWayOnlyCrossed(world, map, tick, found);
            NoPlanCutsAGrantFromBehindTheNose(world, map, tick, found);
        }

        return found;
    }

    /// <summary>
    /// <b>Nothing a light holds short of a box holds the box</b> (TLT-1, TER-5c.2): a car's plan cut at a
    /// light's hold is one stretch of its line, and gives up everything past the cut — so a car held at the bar
    /// of the box ahead of it is given no movement through that box. A phase greens the arms that do not
    /// conflict, and a box kept by a car stopped at a bar is the phase's own decision undone. A car whose plan
    /// runs through the box ahead and is held at the next one's bar is not this case.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NothingStoppedAtARedHoldsTheBoxBeyondIt(string map)
    {
        var run = Of(map);

        Assert.True(run.PastARed is null, run.PastARed);
    }

    static void NothingStoppedAtARedHoldsTheBox(TownWorld world, string map, int tick, Watched found)
    {
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (!OnARoute(world, car) || world.Cars.GrantCutBy[car] != HeadwayKind.Light) continue;
            if (world.Cars.InsideTheBox[car] || world.Cars.AuthorityM[car] >= world.Cars.ToTheBoxM[car]) continue;

            found.AtARed++;
            if (found.PastARed is not null) continue;

            var join = TheJoinItPlans(world, car);
            if (join < 0) continue;

            found.PastARed =
                $"{map}: car {car} plans join way {join} at tick {tick} with a light "
                + $"holding it {world.Cars.AuthorityM[car]:0.00} m on, {world.Cars.ToTheBoxM[car]:0.00} m short of the box";
        }
    }

    /// <summary>A join this car holds a main claim on, or −1 — which is what having a movement through a box is.</summary>
    static int TheJoinItPlans(TownWorld world, int car)
    {
        var claims = new LaneClaim[64];
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            if (world.Ways.KindOf(way) != WayKind.Connector) continue;

            var count = world.Occupancy.CopyPlannedTo(way, claims);
            for (var at = 0; at < count; at++)
            {
                ref readonly var claim = ref claims[at];
                if (claim.Occupant == car && claim.Of == LaneRoster.Driving && !claim.Secondary) return way;
            }
        }

        return -1;
    }

    /// <summary>
    /// <b>A plan that reaches past the mouth of a box holds the join from its near edge</b> (TER-4c.1): the
    /// ways of a plan are the ways of its holder's line, laid in the order it drives them, so the join is on
    /// it from the metre the lane hands over — and a car that plans none of a box is one nothing standing in
    /// that box is weighed against.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void APlanIntoABoxHoldsTheJoinFromItsNearEdge(string map)
    {
        var run = Of(map);

        Assert.True(run.MissedTheNearEdge is null, run.MissedTheNearEdge);
    }

    static void APlanReachesIntoTheBoxFromItsMouth(TownWorld world, string map, int tick, Watched found)
    {
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (!OnARoute(world, car) || world.Cars.Line[car].LaneCount < 2) continue;

            var chain = world.Cars.ChainOf(car);
            var join = world.Roads.ConnectorBetween(chain[0], chain[1]);
            if (join == RoadGraph.NoConnector || world.Roads.ConnectorLengthM(join) <= 0f) continue;

            // Short of the mouth, with the plan it came to — cut or not — reaching past it.
            var mouthM = world.Cars.LaneEndsOf(car)[0];
            if (world.Cars.ClaimFromM[car] >= mouthM) continue;

            var endsAtM = MathF.Min(
                world.Cars.ClaimToM[car], world.Occupancy.HoldEndsAtM(world.DriveHold(car), out _, out _));
            if (endsAtM <= mouthM + Tolerance) continue;

            found.Reaching++;
            var way = world.Ways.OfRoadConnector(join);
            if (found.MissedTheNearEdge is not null
                || world.Occupancy.PlannedToM(way, 0f, car, LaneRoster.Driving) > Tolerance)
            {
                continue;
            }

            found.MissedTheNearEdge =
                $"{map}: car {car}'s plan runs to {endsAtM:0.00} m of its line at tick {tick}, past the mouth "
                + $"of join {join} at {mouthM:0.00} m, and holds none of the join from its near edge";
        }
    }

    /// <summary>
    /// <b>A plan's main claims are laid on the ways its holder drives and on no other</b> (TER-5c.1): what it
    /// holds of a way it only crosses is a secondary claim over the section a mark links there, and never a
    /// stretch of that way's line — or a car approaching a box would hold a fan of joins it is never going to be
    /// on.
    /// </summary>
    [Theory]
    [MemberData(nameof(CrossedMaps))]
    public void APlanLaysNoPieceOnAWayItOnlyCrosses(string map)
    {
        var run = Of(map);

        Assert.True(run.LaidOnAWayItCrosses is null, run.LaidOnAWayItCrosses);
        Assert.True(run.Crossing > 0, $"{map}: no plan ever placed a secondary claim on a way it crosses");
    }

    static void NoPieceIsLaidOnAWayOnlyCrossed(TownWorld world, string map, int tick, Watched found)
    {
        Span<LaneClaim> slots = stackalloc LaneClaim[128];
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            var count = world.Occupancy.CopyPlannedTo(way, slots);
            for (var slot = 0; slot < count; slot++)
            {
                ref readonly var piece = ref slots[slot];
                if (piece.Of != LaneRoster.Driving || world.Cars.Line[piece.Occupant].LaneCount == 0) continue;

                if (piece.Secondary)
                {
                    found.Crossing++;
                    continue;
                }

                if (found.LaidOnAWayItCrosses is not null || OnItsLine(world, piece.Occupant, way)) continue;

                found.LaidOnAWayItCrosses =
                    $"{map}: car {piece.Occupant} holds {piece.FromM:0.00}–{piece.ToM:0.00} m of "
                    + $"{world.Ways.KindOf(way)} way {way} at tick {tick}, which is on no line it drives";
            }
        }
    }

    /// <summary>
    /// <b>A plan is never cut behind the nose that laid it</b> (TER-4c.1): it is laid from the nose forward,
    /// so whatever takes ground off it takes it at or past the nose, and a car held by another plan is held at
    /// most its own margin past the place it was cut. A grant of minus a car's length is a car no clear road
    /// in front of it can ever release.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoPlanIsCutBehindTheNoseThatLaidIt(string map)
    {
        var run = Of(map);

        Assert.True(run.CutFromBehind is null, run.CutFromBehind);
    }

    static void NoPlanCutsAGrantFromBehindTheNose(TownWorld world, string map, int tick, Watched found)
    {
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Cars.GrantCutBy[car] != HeadwayKind.Claimed) continue;

            found.HeldByAPlan++;
            var marginM = Config.Driving.StandOffM;
            if (found.CutFromBehind is not null || world.Cars.AuthorityM[car] >= -marginM - Tolerance) continue;

            found.CutFromBehind =
                $"{map}: car {car} was cut to {world.Cars.AuthorityM[car]:0.00} m at tick {tick} by a plan, "
                + $"against a margin of {marginM:0.00} m";
        }
    }

    /// <summary>
    /// <b>A body standing in a junction is on every join that runs under it</b> (TER-4c.2) — which is the
    /// whole of what holds the traffic crossing the box off it. A wreck makes no movement, so what is left to
    /// say where it is is the ground it is lying on.
    /// </summary>
    [Theory]
    [MemberData(nameof(CrossedMaps))]
    public void ABodyStandingInAJunctionIsOnTheJoinsThatCrossIt(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);
        var (join, crossing, atM) = WhereTwoJoinsCross(world);
        var car = ACarOffTheParking(world, map);

        world.Cars.Driven[car] = false;
        world.Cars.Broken[car] = true;
        world.Cars.PositionM[car] = atM;
        world.Cars.VelocityMps[car] = Vector2.Zero;
        world.RebuildProximityIndex();

        Assert.True(IsABodyOn(world, join, car), $"{map}: join {join}, which it is lying on, does not have it");
        Assert.True(IsABodyOn(world, crossing, car), $"{map}: join {crossing}, which crosses that one, does not have it");
    }

    /// <summary>
    /// <b>And on no join it is standing clear of, wherever in the box it is put.</b> A body is on a way where
    /// its collider is over that way's ribbon, so no body is ever on a join whose line is further from it than
    /// its own corner and the join's half width — measured here by walking the line, which is a different
    /// sum from the lattice that laid it.
    /// </summary>
    [Theory]
    [MemberData(nameof(CrossedMaps))]
    public void NoBodyIsOnAJoinItStandsClearOf(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);
        var (_, _, atM) = WhereTwoJoinsCross(world);
        var car = ACarOffTheParking(world, map);

        world.Cars.Driven[car] = false;
        world.Cars.Broken[car] = true;
        world.Cars.VelocityMps[car] = Vector2.Zero;

        var halfM = world.Cars.BuildOf(car).CollisionSizeM * 0.5f;
        var cornerM = halfM.Length();
        var overM = Config.IntersectionReachM;

        for (var down = -overM; down <= overM; down += halfM.X)
        {
            for (var across = -overM; across <= overM; across += halfM.X)
            {
                world.Cars.PositionM[car] = atM + new Vector2(across, down);
                world.RebuildProximityIndex();

                foreach (var way in world.Occupancy.OccupiedWays)
                {
                    if (world.Ways.KindOf(way) != WayKind.Connector) continue;

                    var join = world.Ways.RoadConnectorOf(way);
                    if (!IsABodyOn(world, join, car)) continue;

                    var apartM = ToChainM(
                        world.Roads.ConnectorArcs(join), world.Roads.ConnectorLengthM(join), world.Cars.PositionM[car]);
                    var reachM = cornerM + (world.Roads.ConnectorWidthM(join) * 0.5f) + StepM;
                    Assert.True(
                        apartM <= reachM,
                        $"{map}: a body at {across:0.0},{down:0.0} m from {atM} is on join {join} and stands "
                        + $"{apartM:0.00} m off its line, past the {reachM:0.00} m a body covers");
                }
            }
        }
    }

    /// <summary>How finely a join's line is walked for its distance from a body — well under a ribbon's width.</summary>
    const float StepM = 0.25f;

    /// <summary>Ground on a join is metres, and a plan is arithmetic on floats: a centimetre is not a finding.</summary>
    const float Tolerance = 1e-2f;

    static float ToChainM(ReadOnlySpan<ArcSeg> arcs, float lengthM, Vector2 pointM)
    {
        var leastM = float.PositiveInfinity;
        for (var alongM = 0f; alongM <= lengthM; alongM += StepM)
        {
            leastM = MathF.Min(leastM, (Spline.SampleAt(arcs, alongM).PositionM - pointM).Length());
        }

        return leastM;
    }

    /// <summary>Whether the car is one the road is driving down a route of its own.</summary>
    static bool OnARoute(TownWorld world, int car) =>
        world.Cars.Driven[car] && !world.Cars.Broken[car] && world.Cars.Line[car].LaneCount > 0;

    /// <summary>Whether a way is one of the lanes of a car's chain or a join between two of them.</summary>
    static bool OnItsLine(TownWorld world, int car, int way)
    {
        var chain = world.Cars.ChainOf(car);
        var lanes = world.Cars.Line[car].LaneCount;
        for (var index = 0; index < lanes; index++)
        {
            if (world.Ways.OfRoadLane(chain[index]) == way) return true;
            if (index == lanes - 1) break;

            var join = world.Roads.ConnectorBetween(chain[index], chain[index + 1]);
            if (join != RoadGraph.NoConnector && world.Ways.OfRoadConnector(join) == way) return true;
        }

        return false;
    }

    /// <summary>Whether this car is a body on one join.</summary>
    static bool IsABodyOn(TownWorld world, int join, int car)
    {
        Span<LaneClaim> slots = stackalloc LaneClaim[32];
        var count = world.Occupancy.CopyBodiesTo(world.Ways.OfRoadConnector(join), slots);
        for (var at = 0; at < count; at++)
        {
            if (slots[at].Occupant == car && slots[at].Of == LaneRoster.Driving) return true;
        }

        return false;
    }

    /// <summary>
    /// A car standing on the road rather than in a bay, which is the one a body can be put anywhere.
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

        throw new InvalidOperationException($"{map} kept every one of its cars in a bay for two minutes");
    }

    /// <summary>
    /// Two joins of one box whose ribbons the atlas marked against each other, and a place in the ground they
    /// share — read straight off the marks, which are the only thing that has an opinion about it.
    /// </summary>
    static (int Join, int Crossing, Vector2 AtM) WhereTwoJoinsCross(TownWorld world)
    {
        for (var join = 0; join < world.Roads.ConnectorCount; join++)
        {
            foreach (ref readonly var mark in world.Atlas.Marks.Of(world.Ways.OfRoadConnector(join)))
            {
                if (world.Ways.KindOf(mark.OnWay) != WayKind.Connector) continue;

                var crossed = world.Ways.RoadConnectorOf(mark.OnWay);
                var arcs = world.Roads.ConnectorArcs(crossed);
                if (arcs.Length == 0) continue;

                return (crossed, join, Spline.SampleAt(arcs, (mark.FromM + mark.ToM) * 0.5f).PositionM);
            }
        }

        throw new InvalidOperationException("the town has no two joins whose ribbons share ground");
    }
}
