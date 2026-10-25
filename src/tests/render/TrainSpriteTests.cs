using System.Numerics;
using TrafficSimulation.Agents.Train.Body;
using TrafficSimulation.App.Render;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>
/// <b>A wagon's fans turn from its own start</b> (TRN-13): what they start from is its ID (<see cref="SpriteInstance.Id"/>),
/// which is its place in the roster and not where a frame happens to draw it.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P9)]
public class TrainSpriteTests
{
    const int FirstSheet = 100;

    static TrainCatalog Catalog => TrainCatalog.Shared;

    [Fact]
    public void AWagonsIdIsItsPlaceInTheRosterWhateverIsDrawnBeforeIt()
    {
        var unseen = new Vector2(1000f, 0f);
        var seen = new Vector2(40f, 30f);
        var wagons = new[]
        {
            new WagonView { PositionM = unseen, Flags = WagonView.InWorld },
            new WagonView { PositionM = seen, Flags = WagonView.InWorld },
        };

        var into = new SpriteInstance[wagons.Length];
        var written = TrainSprites.Fill(wagons, Catalog, FirstSheet, seen, new Vector2(100f), onABridge: false, into);

        Assert.Equal(1, written);
        Assert.Equal(1, into[0].Id);
    }
}
