using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Person;

/// <summary>
/// <b>What a walker does with a town</b>: the line it follows (PER-25) and the two claims it lays
/// (PER-26).
/// </summary>
/// <remarks>
/// <b>Asked of <see cref="Towns.Built"/></b>, the one town the suite lays that stands anybody up — a
/// walker begins inside a building (GEN-7) and the fixture has no buildings to begin in.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P4)]
public class WalkingTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>GEN-7: everybody the plan stands is inside a building before the first tick.</b> A body stood at
    /// a way in walks through it as the town is stood up, so the first thing any walker does is a dwell and
    /// not a leg nothing drew.
    /// </summary>
    [Fact]
    public void EverybodyTheTownStandsBeginsInsideABuilding()
    {
        using var world = Stood(out var plan);

        Assert.Equal(PeopleStood(plan), world.People.Count);
        for (var person = 0; person < world.People.Count; person++)
        {
            Assert.Equal(TripStage.Dwelling, world.People.Stage[person]);
            Assert.True(world.People.Inside[person].Any, $"walker {person} was stood outside every door");
        }
    }

    /// <summary>
    /// <b>PER-26's first claim: a walker out of doors holds the ground it is standing on.</b> Every way its
    /// own box is over carries a stretch of that way under this body's own number, at the one rank nothing
    /// takes (TER-5g, p0) — which is the whole of what stops a driver reaching it.
    /// </summary>
    /// <remarks>
    /// <b>Asked of the way the body is stationed on</b> and not of every way in the town: where a walker
    /// stands on the network is what the town itself read off the line (<see cref="PersonFleet.OnWay"/>),
    /// so a walker on a way that held nothing there is the claim missing rather than the reading differing.
    /// </remarks>
    [Fact]
    public void AWalkerOutOfDoorsHoldsTheGroundItStandsOn()
    {
        using var world = Walking(out var afoot);

        foreach (var person in afoot)
        {
            var way = world.People.OnWay[person];
            var alongM = world.People.OnWayM[person];

            Assert.True(
                Holds(world, way, person, ClaimPriority.Hard, alongM),
                $"walker {person} stands at {alongM:F1} m of way {way} and holds none of it");
        }
    }

    /// <summary>
    /// <b>PER-26's second claim: what a walker states is ground in front of it.</b> A statement runs from
    /// the body's own front to where it is aiming, so no stretch of it reaches back over the ground the
    /// body is standing on — which is the one thing that would make it two answers about one piece of the
    /// world.
    /// </summary>
    /// <remarks>
    /// <b>How far it reaches is not asked</b>, because it is data and asserting it would be the arithmetic
    /// written out twice (VER-12). <b>And a statement cut to nothing is the ordinary answer</b> — stronger
    /// ground in front takes it (TER-5g) — so what is asked is where the ones that survive begin.
    /// </remarks>
    [Fact]
    public void WhatAWalkerStatesBeginsInFrontOfIt()
    {
        using var world = Walking(out var afoot);

        var stated = 0;
        foreach (var person in afoot)
        {
            var way = world.People.OnWay[person];
            var alongM = world.People.OnWayM[person];

            foreach (var claim in ClaimsOf(world, way, person, ClaimPriority.Soft))
            {
                Assert.True(
                    claim.FromM >= alongM,
                    $"walker {person} stands at {alongM:F1} m of way {way} and states from {claim.FromM:F1} m");
                stated++;
            }
        }

        Assert.True(stated > 0, "nobody in the town was walking a way of its own, so nothing was stated");
    }

    /// <summary>
    /// <b>PER-26: a body on no way of the network states nothing</b>, there being no way to state it on. It
    /// is the other half of the walk off the network (PER-25) — such a body is walking straight at the
    /// pavement over ground the town does not number, and what it holds while it does is the box it is
    /// standing in.
    /// </summary>
    [Fact]
    public void ABodyOnNoWayOfTheNetworkStatesNothing()
    {
        using var world = Walking(out _);

        Span<LaneClaim> claims = stackalloc LaneClaim[MostClaimsOnAWay];
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            var count = world.Occupancy.CopyTo(way, claims);
            for (var at = 0; at < count; at++)
            {
                ref readonly var claim = ref claims[at];
                if (claim.Of != LaneRoster.Walking || claim.Priority != ClaimPriority.Soft) continue;
                if (claim.Right == RightOfWay.OnThePaint) continue;

                Assert.True(
                    world.People.OnWay[claim.Occupant] != PersonFleet.NoWay,
                    $"walker {claim.Occupant} is on no way of the network and states {way}");
            }
        }
    }

    /// <summary>
    /// <b>PER-25: a walk begins on the network.</b> Whatever ground a body is standing on when its line is
    /// laid, the first point of that line is a place on the pavement's own network — which is what makes
    /// the leg off it a straight back onto the walk rather than a line struck out across the town.
    /// </summary>
    [Fact]
    public void EveryWalkBeginsAtAPointOfTheNetwork()
    {
        using var world = Walking(out var afoot);

        var walked = 0;
        foreach (var person in afoot)
        {
            if (world.People.WalkedCount[person] == 0) continue;

            var ways = world.People.WalkedWayOf(person);
            Assert.True(
                ways[0] != WalkedLine.NoWay,
                $"walker {person} was laid a line whose first point stands on no way of the network");
            walked++;
        }

        Assert.True(walked > 0, "nobody in the town was carrying a line to be asked about");
    }

    /// <summary>Whether this body holds a stretch of that way covering that metre, at that rank.</summary>
    static bool Holds(TownWorld world, int way, int person, ClaimPriority priority, float alongM)
    {
        foreach (var claim in ClaimsOf(world, way, person, priority))
        {
            if (claim.FromM <= alongM && alongM <= claim.ToM) return true;
        }

        return false;
    }

    /// <summary>Every stretch of one way this walker holds at one rank.</summary>
    static List<LaneClaim> ClaimsOf(TownWorld world, int way, int person, ClaimPriority priority)
    {
        var found = new List<LaneClaim>();
        if (way == PersonFleet.NoWay) return found;

        var claims = new LaneClaim[MostClaimsOnAWay];
        var count = world.Occupancy.CopyTo(way, claims);
        for (var at = 0; at < count; at++)
        {
            ref readonly var claim = ref claims[at];
            if (claim.Occupant != person || claim.Of != LaneRoster.Walking) continue;
            if (claim.Priority != priority) continue;

            found.Add(claim);
        }

        return found;
    }

    /// <summary>More stretches than any way of a pavement carries, which is a handful of bodies at a corner.</summary>
    const int MostClaimsOnAWay = 64;

    /// <summary>The town stood up and not yet ticked, which is where GEN-7 is asked.</summary>
    static TownWorld Stood(out CityPlan plan)
    {
        plan = Towns.Built;
        return new TownWorld(plan, Config);
    }

    /// <summary>
    /// And the same town run on until its people are out of their first dwell and walking — with the
    /// walkers that are out of doors and standing on a way of the network, which is who PER-26 is asked of.
    /// </summary>
    static TownWorld Walking(out List<int> afoot)
    {
        var world = Stood(out _);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(SettleTicks);

        afoot = [];
        for (var person = 0; person < world.People.Count; person++)
        {
            if (world.People.Inside[person].Any || world.People.Wounded[person]) continue;
            if (world.People.OnWay[person] == PersonFleet.NoWay) continue;

            afoot.Add(person);
        }

        Assert.True(afoot.Count > 0, "no walker of the town got out of a door and onto the pavement");
        return world;
    }

    /// <summary>
    /// Long enough that the longest dwell a building hands out has run and the bodies are on the pavement,
    /// and short enough to stay a town-tier case.
    /// </summary>
    static int SettleTicks => (int)(Config.Building.DwellMaxS * Config.Sim.TickRateHz) + 120;

    /// <summary>How many the plan actually stood, which is the brief's count bounded by the doors there were.</summary>
    static int PeopleStood(CityPlan plan)
    {
        var stood = 0;
        for (var spawn = 0; spawn < plan.Spawns.Count; spawn++)
        {
            if (plan.Spawns.Kind[spawn] == SpawnKindPerson) stood++;
        }

        return stood;
    }

    const byte SpawnKindPerson = 0;
}
