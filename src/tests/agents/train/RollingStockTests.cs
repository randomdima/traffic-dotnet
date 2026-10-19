using TrafficSimulation.Agents.Train.Body;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Train;

/// <summary>
/// <b>What the shipped rolling stock owes the track it is laid on</b>: room on the run-in for every train a warning short
/// of the map (RAIL-1, TRN-7), and couplings that keep two vehicles apart on the tightest curve (TRN-8).
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P3)]
public class RollingStockTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static TrainCatalog Catalog => TrainCatalog.Shared;

    static float LengthM(VehicleKind lead, VehicleKind wagon, int wagons) =>
        Catalog.ShapeOf(lead).FootprintM.X + (wagons * Catalog.ShapeOf(wagon).FootprintM.X) + (wagons * Config.Train.CouplingGapM);

    /// <summary>
    /// <b>Every train fits on the run-in a warning short of the map</b> (RAIL-1): put down there, its tail is still on the
    /// track, so every crossing by the map's edge closes ahead of it.
    /// </summary>
    [Theory]
    [InlineData("Electric", "Coach")]
    [InlineData("Diesel", "Tank")]
    [InlineData("Diesel", "Coal")]
    public void EveryTrainFitsOnTheRunInAWarningShortOfTheMap(string leadKind, string wagonKind)
    {
        var (lead, wagon) = (Enum.Parse<VehicleKind>(leadKind), Enum.Parse<VehicleKind>(wagonKind));
        var figures = Config.Train;
        var (wagons, speedMps) = wagon switch
        {
            VehicleKind.Coach => (figures.IntercityCoaches, figures.IntercitySpeedMps),
            VehicleKind.Tank => (figures.OilWagons, figures.FreightSpeedMps),
            _ => (figures.CoalWagons, figures.FreightSpeedMps),
        };

        var neededM = LengthM(lead, wagon, wagons) + (speedMps * Config.Rail.WarningS);
        Assert.True(neededM <= Config.Rail.RunOffM, $"a train needs {neededM:F1} m of run-in, and the track runs {Config.Rail.RunOffM:F0} m past the map");
    }

    /// <summary>
    /// <b>Two coupled vehicles stand clear of each other on the tightest curve a track is laid round</b> (TRN-8): what the
    /// inside corners close by as the two bodies turn against each other is less than the coupling's gap.
    /// </summary>
    [Fact]
    public void CoupledVehiclesStandClearOfEachOtherOnTheTightestCurve()
    {
        foreach (var one in Catalog.Vehicles)
        {
            foreach (var other in Catalog.Vehicles)
            {
                var turnRad = ((one.FootprintM.X + other.FootprintM.X) * 0.5f + Config.Train.CouplingGapM) / Config.Rail.LeastRadiusM;
                var closingM = MathF.Max(one.FootprintM.Y, other.FootprintM.Y) * 0.5f * turnRad;
                Assert.True(closingM < Config.Train.CouplingGapM, $"{one.Id} and {other.Id} close by {closingM:F3} m on the tightest curve");
            }
        }
    }
}
