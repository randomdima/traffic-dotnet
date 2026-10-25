using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>
/// CAR-54's puffs drawn in between (<see cref="SmokeFigures.PuffSplits"/>): a puff is split by halving, so a car speeding up
/// draws more puffs between those it drew and none of them goes.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P9)]
public class SmokeSpriteTests
{
    [Fact]
    public void APuffIsSplitOnlyIntoAPowerOfTwo()
    {
        var config = new SimConfig { Smoke = new SmokeFigures { PuffSplits = 3 } };

        Assert.Throws<ArgumentException>(() => SmokeSprites.Look(config));
    }

    /// <summary>TRN-15: a stack's puff that would outlive the track its rail vehicle keeps is refused, however short a car's is.</summary>
    [Fact]
    public void AStacksPuffIsRefusedPastItsOwnTrack()
    {
        var shipped = new SmokeFigures();
        var stackTrackS = (shipped.StackTrackPoints - 1) * shipped.StackTrackEveryS;
        var config = new SimConfig { Smoke = new SmokeFigures { StackFullLifeS = stackTrackS + shipped.StackTrackEveryS } };

        Assert.Throws<ArgumentException>(() => SmokeSprites.Look(config));
    }
}
