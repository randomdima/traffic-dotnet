using System.Numerics;
using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.World;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>
/// CTL-1's mark: <b>a ring carried by the very instance the unit is drawn as</b> (<see cref="SpriteRings"/>), so it cannot
/// stand off the picture it marks, rather than anything done to the picture's colour or drawn beside it.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class SpriteRingsTests(StandingTown town) : IClassFixture<StandingTown>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly TownSprites Looks = LoadLooks();

    static TownSprites LoadLooks()
    {
        var looks = TownSprites.Load(Config);
        looks.ReadAspects(SheetAtlas.Pack(looks.Sheets));
        return looks;
    }

    /// <summary>A town of the case's own, for a case that puts a hand on a body.</summary>
    static TownWorld Town() => new(Towns.Of(Towns.Fixture), Config);

    /// <summary>Every car's body, as a frame over the whole town fills it with the town's rings marked.</summary>
    static SpriteInstance[] Bodies(TownWorld world)
    {
        var words = new uint[SpriteRings.Length(world.People.Capacity, world.Cars.Capacity, world.World.View.Wagons.Length)];
        SpriteRings.Mark(world, words);
        var into = new SpriteInstance[world.Cars.Count];
        var written = CarSprites.Fill(
            world.Cars, [.. Enumerable.Range(0, world.Cars.Count)], Looks.Cars, Looks.FirstCarSheet, world.Cars.PositionM[0],
            new Vector2(4000f), into, rings: new SpriteRings(words, world.People.Capacity, world.Cars.Capacity));
        return into[..written];
    }

    static SpriteInstance[] Ringed(TownWorld world) => [.. Bodies(world).Where(instance => instance.Ring != SpriteRing.None)];

    [Fact]
    public void NothingSelectedIsNothingRinged()
    {
        var world = town.World;
        world.Select(default);

        Assert.Empty(Ringed(world));
    }

    /// <summary>
    /// <b>The ring is the car's own body instance asking for one</b> — its place, size, heading, cell, sheet and tilt as
    /// the frame draws it unselected — and no other body changes.
    /// </summary>
    [Fact]
    public void ASelectedCarsOwnBodyCarriesTheRing()
    {
        var world = town.World;
        world.Select(default);
        var plain = Bodies(world);

        world.Select(new Selection(SelectionKind.Car, 1));
        var ringed = Bodies(world);

        Assert.Equal(plain.Length, ringed.Length);
        var body = Assert.Single(Enumerable.Range(0, plain.Length).Where(at => plain[at] != ringed[at]));
        Assert.Equal(world.Cars.PositionM[1], ringed[body].CentreM);
        Assert.Equal(SpriteRing.Selected, ringed[body].Ring);
        Assert.Equal(
            (plain[body].CentreM, plain[body].HalfSizeM, plain[body].HeadingRad, plain[body].Cell, plain[body].Sheet, plain[body].Id),
            (ringed[body].CentreM, ringed[body].HalfSizeM, ringed[body].HeadingRad, ringed[body].Cell, ringed[body].Sheet, ringed[body].Id));
    }

    /// <summary>
    /// CTL-1b: <b>one ring a unit and no other shape</b>. A group is read off the rings on its members, so two units are
    /// two rings and nothing is drawn round the pair.
    /// </summary>
    [Fact]
    public void EverySelectedUnitWearsItsOwnRing()
    {
        var world = town.World;
        Assert.True(world.Cars.Count >= 2, "the fixture stands fewer cars than a group needs");

        world.Select(new Selection(SelectionKind.Car, 0));
        world.SelectAlso(new Selection(SelectionKind.Car, 1));

        Assert.Equal(2, world.SelectedCount);
        Assert.Equal(2, Ringed(world).Length);
    }

    /// <summary>
    /// CTL-5d: <b>a car a second driver is holding is ringed too</b>, in its own hue. And once the reader picks that car
    /// out themselves, the selection is the answer and the second hand's ring goes.
    /// </summary>
    [Fact]
    public void ACarUnderASecondHandWearsARingOfItsOwn()
    {
        using var world = Town();
        world.Select(default);
        world.HandOnCar(0, 1, new HandInput(Held: true, Throttle: 0f, Steer: 0f, Handbrake: false, WalkDirection: Vector2.Zero));

        Assert.Equal([SpriteRing.SecondHand], Ringed(world).Select(instance => instance.Ring));

        world.Select(new Selection(SelectionKind.Car, 1));
        Assert.Equal([SpriteRing.Selected], Ringed(world).Select(instance => instance.Ring));
    }
}
