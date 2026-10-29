using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Gen;

/// <summary>
/// <b>A town graded out to the country is passed and not entered</b> (GEN-63): each intercity road runs from the map's edge
/// round the town's solid middle and out past the edge again, one curve nothing cuts.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P5)]
public class BypassTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly Lazy<TownGen.Authored> Town = new(() =>
        TownGen.Author(Towns.Brief(Towns.CitySeed, buildings: 900, country: true), Config));

    [Fact]
    public void ATownBuiltAlikeToItsEdgeIsEnteredInstead()
    {
        var town = TownGen.Author(Towns.Brief(Towns.CitySeed, buildings: 900), Config);

        Assert.Equal(0f, town.Connectors.BypassM);
    }

    /// <summary><b>Every intercity road runs edge to edge</b>, and ends on no other road nor at the core.</summary>
    [Fact]
    public void EveryIntercityRoadRunsFromTheMapsEdgeToItsEdge()
    {
        var town = Town.Value;
        var extentM = town.Map.SizeM;
        Assert.NotEmpty(town.Connectors.Roads);
        foreach (var road in town.Connectors.Roads)
        {
            Assert.Equal(-1, road.EndsOn);
            Assert.True(OnTheEdge(road.Line.FirstM, extentM), $"a road enters at {road.Line.FirstM}, off the map's edge");
            Assert.True(OnTheEdge(road.Line.LastM, extentM), $"a road leaves at {road.Line.LastM}, off the map's edge");
        }
    }

    static bool OnTheEdge(Vector2 atM, Vector2 extentM) =>
        atM.X <= 0f || atM.Y <= 0f || atM.X >= extentM.X || atM.Y >= extentM.Y;

    /// <summary>
    /// <b>No intercity road comes into the town</b>: none stands nearer the core than the ground its routes were barred
    /// from, less the cell a route is found on and the tolerance its curve may stray by.
    /// </summary>
    [Fact]
    public void NoIntercityRoadComesInsideTheBypass()
    {
        var connectors = Town.Value.Connectors;
        var figures = Config.CityGen;
        var insideM = (connectors.BypassM * figures.BypassInsideShare) - figures.RouteCellM - figures.RouteToleranceM;
        foreach (var road in connectors.Roads)
        {
            foreach (var pointM in road.Line.PointsM)
            {
                var awayM = Vector2.Distance(pointM, connectors.CoreM);
                Assert.True(awayM >= insideM, $"an intercity road stands {awayM:F0} m from the core, inside {insideM:F0} m");
            }
        }
    }

    /// <summary>
    /// <b>A coastal town's roads all come in over land</b> (GEN-63): spread round the land's edge, a road passing the town
    /// for each two of the brief's ways in is laid — none is lost to a bearing that leaves the map through the sea.
    /// </summary>
    [Fact]
    public void ACoastalTownLaysEveryRoadItsBriefAsksFor()
    {
        var brief = Towns.Brief(Towns.CitySeed, WaterKind.Coast, buildings: 900, country: true);
        var town = TownGen.Author(brief, Config);

        Assert.Equal((brief.Connectors.IntercityRoads + 1) / 2, town.Connectors.Roads.Count);
        Assert.All(town.Connectors.Roads, road => Assert.False(town.Water.InTheSea(road.Line.FirstM), $"a road enters at {road.Line.FirstM}, in the sea"));
        Assert.All(town.Connectors.Roads, road => Assert.False(town.Water.InTheSea(road.Line.LastM), $"a road leaves at {road.Line.LastM}, in the sea"));
    }
}
