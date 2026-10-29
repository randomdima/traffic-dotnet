using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The showroom's people (PER-30): a look on show stands still where the plan put it, facing the camera, until a hand
/// takes it — nothing on show walks, wanders or turns of its own.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P9)]
public class ShowroomWalkersTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Long enough for any look that walked of its own to have left its place.</summary>
    const float WatchedS = 10f;

    /// <summary>The sheet's front row: the facing the plan stands every look at.</summary>
    const int FrontRow = 4;

    [Fact]
    public void ALookOnShowStandsStillFacingTheCamera()
    {
        var plan = Towns.Of(ShowroomPlan.Name);
        using var world = new TownWorld(plan, Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        var placedM = world.People.PositionM.ToArray();
        loop.Advance((int)MathF.Ceiling(WatchedS / Config.TickSeconds));

        for (var person = 0; person < world.People.Count; person++)
        {
            var movedM = (world.People.PositionM[person] - placedM[person]).Length();
            Assert.True(movedM <= Config.PersonRadiusM, $"look {person} moved {movedM:F2} m on show");
            Assert.False(world.People.Walking[person], $"look {person} is walking on show");
            Assert.Equal(FrontRow, PersonCatalog.FacingRow(world.People.HeadingRad[person]));
        }
    }
}
