using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The containment contract (PHY-7, OBJ-4/5, CAR-2): who may be inside what, how the two container
/// kinds are the same rule, and what a body that is inside one is not.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class ContainmentTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// OBJ-5: a building holds its capacity and not one more, and the check is at the door.
    /// <b>A claim is not a place</b> — it keeps the crowd down and decides nothing.
    /// </summary>
    [Fact]
    public void ABuildingAdmitsItsCapacityAndRefusesPastIt()
    {
        var containers = new Containers([2], cars: 1, new Contained[4]);

        containers.Claim(0);
        Assert.Equal(1, containers.ClaimsOn(0));
        Assert.True(containers.LooksLikelyToHaveRoom(0));

        Assert.True(containers.TryAdmit(0, 0));
        Assert.True(containers.TryAdmit(0, 1));
        Assert.False(containers.TryAdmit(0, 2));
        Assert.Equal(2, containers.OccupantsOf(0));
        Assert.False(containers.LooksLikelyToHaveRoom(0));

        // A place freed is a place somebody waiting outside gets, which is what makes the dwell's bound
        // load-bearing rather than tidy.
        containers.LeaveBuilding(0, 0);
        Assert.True(containers.TryAdmit(0, 2));
        Assert.False(containers.WhereIs(0).Any);
        Assert.Equal(new Contained(ContainerKind.Building, 0), containers.WhereIs(2));
    }

    /// <summary>CAR-2: one driver, and taking the seat is atomic — a second asker is refused rather than queued.</summary>
    [Fact]
    public void ACarTakesOneDriverAndTheSeatIsTakenAtomically()
    {
        var containers = new Containers([1], cars: 1, new Contained[2]);

        Assert.True(containers.IsFree(0));
        Assert.True(containers.TryBoard(0, 0));
        Assert.False(containers.TryBoard(0, 1));
        Assert.Equal(0, containers.DriverOf(0));

        containers.Alight(0, 0);
        Assert.True(containers.IsFree(0));
        Assert.Equal(Containers.NoDriver, containers.DriverOf(0));
    }

    /// <summary>
    /// PHY-7 in the town: a contained person is not stepped, not drawn and not in anybody's way — and
    /// the roster's own field is what says so, so nothing has to ask a registry to find out.
    /// </summary>
    [Fact]
    public void SomebodyInsideIsNotInTheTown()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(3_600);

        var inside = 0;
        for (var person = 0; person < world.People.Count; person++)
        {
            if (!world.People.Inside[person].Any) continue;

            inside++;
            Assert.False(world.People.Walking[person], "a contained walker has no line to hold");
            Assert.Equal(0, world.People.WalkedCount[person]);
        }

        Assert.True(inside > 0, "a minute of the fixture map should have put somebody inside something");
    }

    /// <summary>
    /// OBJ-5 again, asked of the town rather than of the type: <b>no building ever holds more than its
    /// capacity</b>, however many people were walking to it at once.
    /// </summary>
    /// <remarks>
    /// The door is what refuses the one too many and <see cref="ABuildingAdmitsItsCapacityAndRefusesPastIt"/>
    /// is what asks it that; what is added here is the crowd, so it is asked of the towns that have one.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Towns.EveryTown), MemberType = typeof(Towns))]
    public void NoBuildingEverHoldsMoreThanItsCapacity(string map)
    {
        var plan = Towns.Of(map);
        using var world = new TownWorld(plan, Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        for (var window = 0; window < 6; window++)
        {
            loop.Advance(600);
            for (var building = 0; building < plan.Buildings.Count; building++)
            {
                Assert.InRange(world.Containment.OccupantsOf(building), 0, plan.Buildings.Capacity[building]);
            }
        }
    }
}
