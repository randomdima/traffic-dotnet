using System.Numerics;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The showroom's walkers (PER-30): a look on show walks round a ring of its own, so in one lap it is drawn in every
/// facing its sheet has — and a look that stood still, wandered off or walked a line would show the eye one row of it.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P9)]
public class ShowroomWalkersTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>One lap of a ring at the walk's own pace, and a tenth over for the turn onto it.</summary>
    static int ALap => (int)MathF.Ceiling(
        1.1f * 2f * MathF.PI * Config.Person.ShowroomRingM / Config.PersonWalkSpeedMps / Config.TickSeconds);

    [Fact]
    public void ALookOnShowIsDrawnInEveryFacingRoundItsRingAndKeepsToIt()
    {
        var plan = Towns.Of(ShowroomPlan.Name);
        using var world = new TownWorld(plan, Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        var centresM = new List<Vector2>();
        for (var spawn = 0; spawn < plan.Spawns.Count; spawn++)
        {
            if (plan.Spawns.Kind[spawn] == IdlePlan.SpawnKindPerson) centresM.Add(plan.Spawns.PositionM[spawn]);
        }

        var rows = new bool[world.People.Count, PersonCatalog.FacingRows];
        for (var tick = 0; tick < ALap; tick++)
        {
            loop.Advance(1);
            for (var person = 0; person < world.People.Count; person++)
            {
                rows[person, PersonCatalog.FacingRow(world.People.HeadingRad[person])] = true;
                var offM = Vector2.Distance(world.People.PositionM[person], centresM[person]) - Config.Person.ShowroomRingM;
                Assert.True(MathF.Abs(offM) <= 2f * Config.PersonRadiusM, $"walker {person} is {offM:F2} m off its ring");
            }
        }

        for (var person = 0; person < world.People.Count; person++)
        {
            for (var row = 0; row < PersonCatalog.FacingRows; row++)
            {
                Assert.True(rows[person, row], $"walker {person} was never drawn in facing row {row} round its ring");
            }
        }
    }
}
