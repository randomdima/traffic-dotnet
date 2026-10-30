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
/// shove — a moving one in the column its distance walked has reached, and a casualty in its stance, reddened (PER-18).
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P9)]
public class PersonSpriteTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Half a cycle walked: any walk column but the stance's.</summary>
    const int HalfACycle = PersonCatalog.WalkColumns / 2;

    static SpriteInstance Drawn(Vector2 velocityMps, bool down = false)
    {
        var catalog = PersonCatalog.Shared;
        var people = new PersonFleet(1);
        var person = people.Add(Vector2.Zero, 0f, Config.Person.MassKg, Config.PersonRadiusM, 0, new Rng(1, 1), reckless: false);
        people.Walking[person] = true;
        people.DistanceWalkedM[person] = catalog.Variants[0].HeightM * 0.5f;
        people.VelocityMps[person] = velocityMps;
        people.Wounded[person] = down;

        var aspects = Enumerable.Repeat(1f, catalog.SheetCount).ToArray();
        var into = new SpriteInstance[1];
        Assert.Equal(1, PersonSprites.Fill(
            people, [person], catalog, aspects, Config.Person.DrawnStandingBelowMps, Vector2.Zero, new Vector2(10f), into,
            new PersonSprites.Depth[1]));
        return into[0];
    }

    [Fact]
    public void AWalkerHeldStillIsDrawnInItsStance() =>
        Assert.Equal(0, Drawn(Vector2.Zero).Cell.Column);

    [Fact]
    public void AWalkerMovingIsDrawnWhereItsStrideHasReached() =>
        Assert.Equal(HalfACycle, Drawn(new Vector2(Config.PersonWalkSpeedMps, 0f)).Cell.Column);

    /// <summary>Still sliding from the strike, and drawn in the stance of its own walk sheet, multiplied red.</summary>
    [Fact]
    public void ACasualtyIsDrawnInItsStanceReddened()
    {
        var casualty = Drawn(new Vector2(Config.PersonWalkSpeedMps, 0f), down: true);

        Assert.Equal(0, casualty.Cell.Column);
        Assert.Equal(PersonSprites.CasualtyTint, casualty.PackedTint);
        Assert.Equal(Drawn(Vector2.Zero).Sheet, casualty.Sheet);
    }
}
