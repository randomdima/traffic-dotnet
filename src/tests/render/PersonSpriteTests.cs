using System.Numerics;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>
/// Which frame of its sheet a walker is drawn in (<see cref="PersonSprites"/>): <b>a body that is still is drawn in its
/// facing's stance, the first column</b>, whether or not it still means to walk — held at a kerb, in a queue or by a
/// shove — and a moving one in the column its distance walked has reached.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P9)]
public class PersonSpriteTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Half a cycle walked: any walk column but the stance's.</summary>
    const int HalfACycle = PersonCatalog.WalkColumns / 2;

    static int ColumnDrawn(Vector2 velocityMps)
    {
        var catalog = PersonCatalog.Shared;
        var people = new PersonFleet(1);
        var person = people.Add(Vector2.Zero, 0f, Config.Person.MassKg, Config.PersonRadiusM, 0, new Rng(1, 1), reckless: false);
        people.Walking[person] = true;
        people.DistanceWalkedM[person] = catalog.Variants[0].HeightM * 0.5f;
        people.VelocityMps[person] = velocityMps;

        var aspects = Enumerable.Repeat(1f, 2 * catalog.SheetCount).ToArray();
        var into = new SpriteInstance[1];
        Assert.Equal(1, PersonSprites.Fill(
            people, [person], catalog, aspects, catalog.SheetCount, Config.Person.DrawnStandingBelowMps, Vector2.Zero,
            new Vector2(10f), into, new PersonSprites.Depth[1]));
        return into[0].Cell.Column;
    }

    [Fact]
    public void AWalkerHeldStillIsDrawnInItsStance() =>
        Assert.Equal(0, ColumnDrawn(Vector2.Zero));

    [Fact]
    public void AWalkerMovingIsDrawnWhereItsStrideHasReached() =>
        Assert.Equal(HalfACycle, ColumnDrawn(new Vector2(Config.PersonWalkSpeedMps, 0f)));
}
