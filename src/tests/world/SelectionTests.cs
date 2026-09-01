using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// CTL-1b: the selection as a set — what a click, a shift-click and a box over the town each leave it
/// holding, and that it never holds more than the town laid room for.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class SelectionTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static TownWorld Town(SimConfig? config = null) => new(Towns.Of(Towns.Fixture), config ?? Config);

    /// <summary>A box round a point, big enough to have a car's length of room either side of it.</summary>
    static (Vector2 CentreM, Vector2 SizeM) BoxRound(Vector2 atM, float reachM) =>
        (atM, new Vector2(reachM * 2f));

    /// <summary>A click replaces what was picked out; a shift-click takes a unit in and a second drops it.</summary>
    [Fact]
    public void ShiftTakesAUnitInAndTakesItOutAgain()
    {
        using var world = Town();
        Assert.True(world.Cars.Count >= 2, "the fixture stands fewer cars than a group needs");

        world.Select(new Selection(SelectionKind.Car, 0));
        world.SelectAlso(new Selection(SelectionKind.Car, 1));

        Assert.Equal(2, world.SelectedCount);
        Assert.True(world.IsSelected(SelectionKind.Car, 0));
        Assert.True(world.IsSelected(SelectionKind.Car, 1));

        // The first taken is the one a read-out with room for one unit names, and it stays that whatever
        // is added after it.
        Assert.Equal(new Selection(SelectionKind.Car, 0), world.Lead);

        world.SelectAlso(new Selection(SelectionKind.Car, 0));
        Assert.Equal(1, world.SelectedCount);
        Assert.False(world.IsSelected(SelectionKind.Car, 0));

        // And a plain click is still one unit, whatever was held before it.
        world.Select(new Selection(SelectionKind.Car, 0));
        Assert.Equal(1, world.SelectedCount);
    }

    /// <summary>
    /// A box takes every unit whose own footprint is inside it — the footprint a click tests, so what the
    /// box catches is what a reader can see it drawn over.
    /// </summary>
    [Fact]
    public void ABoxTakesEveryUnitInsideItAndNothingOutsideIt()
    {
        using var world = Town();
        var (centreM, sizeM) = BoxRound(world.Cars.PositionM[0], reachM: 6f);

        Assert.True(world.SelectIn(centreM, sizeM, turnRad: 0f, add: false) >= 1);
        Assert.True(world.IsSelected(SelectionKind.Car, 0), "a box drawn round a car did not catch it");

        var middleM = world.Cars.PositionM[0];
        foreach (var unit in world.Selected)
        {
            Vector2 atM;
            float reachM;
            if (unit.Kind == SelectionKind.Car)
            {
                ref readonly var build = ref world.Cars.BuildOf(unit.Index);
                atM = world.Cars.PositionM[unit.Index];
                reachM = new Vector2(build.LengthM, build.WidthM).Length() * 0.5f;
            }
            else
            {
                atM = world.People.PositionM[unit.Index];
                reachM = world.People.RadiusM[unit.Index];
            }

            // Inside the box's own furthest corner plus the body's own reach: a unit is caught by its
            // footprint and not by its middle, so a lorry hanging into the box is in it.
            var awayM = (atM - middleM).Length();
            Assert.True(
                awayM < (6f * MathF.Sqrt(2f)) + reachM,
                $"a unit {awayM:F1} m away was caught by a box reaching 6 m");
        }

        // A box over open country catches nothing, and catching nothing deselects — which is what makes
        // the marks readable as an answer.
        Assert.Equal(0, world.SelectIn(new Vector2(-2_000f), new Vector2(100f), turnRad: 0f, add: false));
        Assert.Equal(0, world.SelectedCount);
    }

    /// <summary>Shift through the drag keeps what was already picked out, which is how two boxes make one group.</summary>
    [Fact]
    public void ABoxWithShiftAddsToWhatIsAlreadyPickedOut()
    {
        using var world = Town();
        world.Select(new Selection(SelectionKind.Person, 0));

        var (centreM, sizeM) = BoxRound(world.Cars.PositionM[0], reachM: 6f);
        world.SelectIn(centreM, sizeM, turnRad: 0f, add: true);

        Assert.True(world.IsSelected(SelectionKind.Person, 0), "a box with shift dropped what was held");
        Assert.True(world.IsSelected(SelectionKind.Car, 0));
    }

    /// <summary>
    /// <b>The bound holds</b> (CTL-1b): the set is one array laid with the town, so a box round everything
    /// stops at what that array holds rather than growing one.
    /// </summary>
    [Fact]
    public void ABoxRoundTheWholeTownStopsAtTheBound()
    {
        var config = new SimConfig { View = new ViewFigures { SelectionMaxUnits = 2 } };
        using var world = Town(config);

        var caught = world.SelectIn(Vector2.Zero, new Vector2(20_000f), turnRad: 0f, add: false);

        Assert.Equal(2, caught);
        Assert.Equal(2, world.SelectedCount);
    }

    /// <summary>An index off the end of a fleet is not a unit, whichever way it arrives.</summary>
    [Fact]
    public void AStaleIndexIsNoUnitAtAll()
    {
        using var world = Town();

        world.Select(new Selection(SelectionKind.Car, world.Cars.Count + 10));
        Assert.Equal(0, world.SelectedCount);

        world.Select(new Selection(SelectionKind.Car, 0));
        world.SelectAlso(new Selection(SelectionKind.Person, world.People.Count + 10));
        Assert.Equal(1, world.SelectedCount);
    }

    /// <summary>
    /// CTL-5b: a change of selection gives up the wheel, and a set changes when a unit joins it as much as
    /// when one replaces it — a hand left on a unit nobody is looking at drives it out of sight.
    /// </summary>
    [Fact]
    public void EveryChangeOfTheSetGivesUpTheWheel()
    {
        using var world = Town();
        var held = new HandInput(Held: true, Throttle: 1f, Steer: 0f, Handbrake: false, WalkDirection: Vector2.Zero);

        world.Select(new Selection(SelectionKind.Car, 0));
        world.Hands(held);
        Assert.True(world.HandsOn);

        // Clicking what is already picked out is not a change of selection, so the wheel stays.
        world.Select(new Selection(SelectionKind.Car, 0));
        Assert.True(world.HandsOn, "clicking the car being driven took its own wheel away");

        world.SelectAlso(new Selection(SelectionKind.Car, 1));
        Assert.False(world.HandsOn, "a unit added to the selection left the hand where it was");

        world.Hands(held);
        world.SelectNone();
        Assert.False(world.HandsOn);
    }
}
