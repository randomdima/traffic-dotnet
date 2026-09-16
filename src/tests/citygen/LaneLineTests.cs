using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>The lines a car is driven on, as the pieces they are written in</b> (<see cref="LaneLines"/>).
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P9)]
[Collection(TownGeometryCollection.Name)]
public class LaneLineTests
{
    /// <summary>
    /// <b>A movement through a box turns once.</b> It is drawn as the biarc between two poses
    /// (<see cref="Spline.BiarcInto"/>), and the construction gives the two halves of a symmetric pair —
    /// which is every turn between two lanes of one width — the same radius: one arc, cut down the middle,
    /// with a joint in it the car does not turn at.
    /// </summary>
    /// <remarks>
    /// <b>A lane and a way into a bay are not asked, and neither is left out for tidiness.</b> How much of a
    /// lane's own end is curved is read off its pieces and decides the setbacks (TER-5b), so joining two of
    /// them moves where the lane hands over; and a way into a bay is one band of a car park's bundle, which
    /// the boundary settles inside two millimetres — where a chain is cut decides the nearest point read off
    /// it to about one. Both are measured in citygen's decision log.
    /// </remarks>
    [Theory]
    [InlineData(Towns.Fixture)]
    [InlineData(Towns.City)]
    public void AMovementThroughABoxTurnsOnce(string map)
    {
        var config = SimConfig.Shipped();
        var lanes = Towns.Of(map).Paving(config).Lanes;
        var carriedOn = 0;
        var pieces = 0;
        var firstM = Vector2.Zero;

        for (var movement = 0; movement < lanes.ConnectorCount; movement++)
        {
            var line = lanes.ArcsOfConnector(movement);
            pieces += line.Length;
            for (var piece = 1; piece < line.Length; piece++)
            {
                if (!Spline.CarriesOn(line[piece - 1], line[piece], LineTolerance.RoundingM, out _)) continue;

                if (carriedOn++ == 0) firstM = line[piece].StartM;
            }
        }

        Assert.True(pieces > 0, $"{map} laid no movement to ask about");
        Assert.True(
            carriedOn == 0,
            $"{map}: {carriedOn} of {pieces} pieces of the town's movements carry on from the piece before "
            + $"them rather than turning at it, the first at {firstM}");
    }
}
