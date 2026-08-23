using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Config;

/// <summary>
/// The figures, and the relations between them. Each number is asserted so that
/// changing it there and not here is a failure rather than a drift; a <em>relation</em> is asserted
/// as a relation, because what must survive a retune is the form and not the value.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class SimConfigTests
{
    [Fact]
    public void ShippedFiguresAreTheOnesDataMdCarries()
    {
        var config = SimConfig.Shipped();

        Assert.Equal(4.0f, config.Car.LengthM);
        Assert.Equal(2.0f, config.Car.WidthM);
        Assert.Equal(1400f, config.Car.MassKg);
        Assert.Equal(6.6f, config.Person.WalkSpeedMps);
        Assert.Equal(110f, config.Person.FootGripMps2);
        Assert.Equal(0.1f, config.Sim.AgentDecisionIntervalS);
        Assert.Equal(15f, config.Signals.CycleS);
        Assert.Equal(60, config.Sim.TickRateHz);
    }

    [Fact]
    public void EverySizeIsDerivedFromTheCarsWidth()
    {
        var config = SimConfig.Shipped();
        var wider = new SimConfig { Car = new CarFigures { WidthM = config.Car.WidthM * 2f } };

        Assert.Equal(config.RoadWidthM * 2f, wider.RoadWidthM);
        Assert.Equal(config.PropDiameterM * 2f, wider.PropDiameterM);
        Assert.Equal(config.PersonDiameterM * 2f, wider.PersonDiameterM);
        Assert.Equal(config.IntersectionCornerRadiusM * 2f, wider.IntersectionCornerRadiusM);
    }

    [Fact]
    public void AStreetsOwnBendPutsItsInnerKerbOnAJunctionsFlareRadius()
    {
        var config = SimConfig.Shipped();

        Assert.Equal(config.IntersectionCornerRadiusM + config.RoadWidthM / 2f, config.RoadCornerRadiusM);
        Assert.Equal(config.RoadWidthM, config.IntersectionReachM);
    }

    /// <summary>
    /// The corner margin and the turning radius are both derived, and both are quoted
    /// with the value the shipped figures give — which is what makes a retune visible here first.
    /// </summary>
    [Fact]
    public void TheDerivedFiguresComeOutWhereDataMdSaysTheyDo()
    {
        var config = SimConfig.Shipped();

        Assert.Equal(0.28f, config.WalkerTightestTurnM, tolerance: 0.01f);
        Assert.Equal(3.9f, config.CarTurningRadiusM, tolerance: 0.05f);
        Assert.Equal(1.0f, config.WalkingLaneOffsetM, tolerance: 0.001f);
    }

    /// <summary>
    /// The relation that matters more than the number: whatever the walk
    /// speed is set to, the foot grip is whatever stops a body inside a fifth of its own diameter.
    /// </summary>
    [Fact]
    public void AWalkerStopsInsideAFifthOfItsOwnDiameter()
    {
        var config = SimConfig.Shipped();

        var stoppingDistanceM = config.Person.WalkSpeedMps * config.Person.WalkSpeedMps / (2f * config.Person.FootGripMps2);

        Assert.True(stoppingDistanceM <= config.PersonDiameterM / 5f,
            $"a walker takes {stoppingDistanceM:F2} m to stop, against a {config.PersonDiameterM:F2} m body");
    }

    [Fact]
    public void TheSharedFileIsAppliedOverTheShippedFigures()
    {
        var loaded = SimConfig.Load();

        // assets/shared/config/SimConfig.json is the one place a figure is retuned without a code
        // change, and this is the figure it currently carries.
        Assert.False(loaded.Car.ParkedHandbrake);
        Assert.True(SimConfig.Shipped().Car.ParkedHandbrake);
    }

    [Fact]
    public void AFigureThisEngineDoesNotHoldIsRefusedRatherThanIgnored()
    {
        var path = Scratch.Write("unknown-figure.json", """{ "notAFigure": 3.0 }""");

        Assert.Throws<FormatException>(() => SharedFiguresReader.Apply(SimConfig.Shipped(), path));
    }

    [Fact]
    public void ADerivedFigureCannotBeOverridden()
    {
        var path = Scratch.Write("derived-figure.json", """{ "roadWidthM": 12.0 }""");

        Assert.Throws<FormatException>(() => SharedFiguresReader.Apply(SimConfig.Shipped(), path));
    }
}
