using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen.Traced;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>A bridge over a road is driven ground of its own</b> (TER-7b, PHY-1a): the ground's boundary leaves it out and
/// the level above is its own shape, so what a point is depends on which of the two a body stands on — each asked
/// of one street carried over another (<see cref="TracedPlanTests.OverAStreet"/>).
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P4)]
public class LevelAboveTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>A point on a bridge is road to a body on the bridge and not to one under it</b>: halfway between its
    /// bridgehead and the street it passes over, the ground is the grass under the deck.
    /// </summary>
    [Fact]
    public void APointOnABridgeIsRoadOnTheBridgeAndGrassUnderIt()
    {
        var shapes = new GroundShapes(TracedPlanTests.OverAStreet().Paving(Config), Config);
        var onTheBridgeM = new Vector2(450f, 500f);

        Assert.Equal(Ground.Road, shapes.At(onTheBridgeM, CityPlan.RoadArrays.Over));
        Assert.Equal(Ground.Grass, shapes.At(onTheBridgeM));
    }

    /// <summary>
    /// <b>The ground runs on under a bridge past its bridgehead, and no further</b> (<see cref="SimConfig.UnderTheDeckM"/>):
    /// halfway along the run-on it is carriageway, and past it and the walk struck round its end it is grass.
    /// </summary>
    [Fact]
    public void TheGroundRunsOnUnderABridgePastItsBridgehead()
    {
        var plan = TracedPlanTests.OverAStreet();
        var shapes = new GroundShapes(plan.Paving(Config), Config);
        var bridge = plan.Bridges.Road[0];
        var head = Spline.SampleAt(plan.Roads.SegmentsOf(bridge), 0f);

        Assert.Equal(Ground.Road, shapes.At(head.PositionM + (head.Direction * Config.UnderTheDeckM * 0.5f)));

        var pastM = Config.UnderTheDeckM + Config.WalkKerbOuterM + Config.Road.KerbWidthM;
        Assert.Equal(Ground.Grass, shapes.At(head.PositionM + (head.Direction * pastM)));
    }
}
