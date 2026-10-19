using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Rail;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>The railway as a generated town lays it</b> (RAIL-2, RAIL-4, RAIL-5): the suite's own city with a double track
/// across it and buildings on it.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P6)]
[Collection(TownGeometryCollection.Name)]
public class RailwayTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly Lazy<CityPlan> Laid = new(() => Towns.LayFresh(Towns.Brief(Towns.CitySeed, buildings: 120, railTracks: 2)));

    static CityPlan Plan => Laid.Value;

    [Fact]
    public void NoJunctionStandsWithinItsClearanceOfTheBallast()
    {
        var ground = new GroundShapes(Plan.Paving(Config), Config);
        var clearM = Lattice.CorridorM(Config) + Config.Rail.JunctionClearanceM;
        Assert.NotEmpty(Plan.Rails.PointM);

        for (var junction = 0; junction < Plan.Junctions.Count; junction++)
        {
            Assert.False(ground.RailWithin(Plan.Junctions.CentreM[junction], clearM), $"junction {junction} at {Plan.Junctions.CentreM[junction]}");
        }
    }

    [Fact]
    public void EveryRoadCrossesATrackSquareEnough()
    {
        var crossings = LevelCrossings.Of(Plan, Config);
        var leastSin = MathF.Cos(Config.Rail.CrossingSkewMostDeg * MathF.PI / 180f);
        Assert.NotEqual(0, crossings.Count);

        for (var meeting = 0; meeting < crossings.Count; meeting++)
        {
            Assert.True(crossings.SinAcross[meeting] >= leastSin, $"road {crossings.Road[meeting]} meets the track at a sine of {crossings.SinAcross[meeting]}");
        }
    }

    [Fact]
    public void NoBuildingStandsOnTheBallast()
    {
        var ground = new GroundShapes(Plan.Paving(Config), Config);
        Assert.NotEqual(0, Plan.Buildings.Count);

        for (var building = 0; building < Plan.Buildings.Count; building++)
        {
            var axis = Core.Geometry.Heading.Unit(Plan.Buildings.HeadingRad[building]);
            var side = new Vector2(-axis.Y, axis.X);
            var halfM = Plan.Buildings.SizeM[building] * 0.5f;
            foreach (var (along, across) in (ReadOnlySpan<(float, float)>)[(0f, 0f), (1f, 1f), (1f, -1f), (-1f, 1f), (-1f, -1f)])
            {
                var atM = Plan.Buildings.CentreM[building] + (axis * halfM.X * along) + (side * halfM.Y * across);
                Assert.True(ground.At(atM) != Ground.Rail, $"building {building} stands on the ballast at {atM}");
            }
        }
    }

    [Fact]
    public void AnAuthoredTrackBendsNoTighterThanItsLeastRadius()
    {
        var draw = new Rng(Towns.CitySeed, Streams.Railway);
        var tracks = RailLines.Author(Towns.Brief(Towns.CitySeed, railTracks: 1) , Config, ref draw);
        var lineM = tracks.TrackOf(0);

        // The radius of the circle through three points a chord apart, which on a smooth line is its radius there.
        for (var point = 1; point + 1 < lineM.Length; point++)
        {
            var (a, b, c) = (lineM[point - 1], lineM[point], lineM[point + 1]);
            double abX = b.X - a.X, abY = b.Y - a.Y, acX = c.X - a.X, acY = c.Y - a.Y, bcX = c.X - b.X, bcY = c.Y - b.Y;
            var twiceArea = Math.Abs((abX * acY) - (abY * acX));
            if (twiceArea == 0) continue;

            var radiusM = Math.Sqrt(((abX * abX) + (abY * abY)) * ((bcX * bcX) + (bcY * bcY)) * ((acX * acX) + (acY * acY))) / (2 * twiceArea);
            Assert.True(radiusM >= Config.Rail.LeastRadiusM, $"point {point} bends at {radiusM:F0} m");
        }
    }
}
