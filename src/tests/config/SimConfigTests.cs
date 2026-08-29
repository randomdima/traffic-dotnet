using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Config;

/// <summary>
/// <b>The relations between the figures, and never the figures.</b> What must survive a retune is the
/// form: that every size is the car's width, that the grip is whatever stops a body inside its own
/// diameter, that a derived figure cannot be authored over. A test that quotes a number back asserts
/// nothing about this engine — it fails the day somebody tunes the town, which is the day it was meant
/// to be tuned, and it passes every other day whatever the arithmetic between the numbers is doing.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class SimConfigTests
{
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

    /// <summary>
    /// <b>An authored figure wins over the shipped one, and the ones it does not name are left alone.</b>
    /// It is asked of a file written here rather than of the shipped one, whose contents are a tuning and
    /// not a claim: read against that, this would fail the next time somebody retuned the town.
    /// </summary>
    [Fact]
    public void TheSharedFileIsAppliedOverTheShippedFigures()
    {
        var path = Scratch.Write("one-figure.json", """{ "car": { "parkedHandbrake": false } }""");
        var applied = SharedFiguresReader.Apply(SimConfig.Shipped(), path);

        Assert.False(applied.Car.ParkedHandbrake);
        Assert.True(SimConfig.Shipped().Car.ParkedHandbrake);
        Assert.Equal(SimConfig.Shipped().Car.LengthM, applied.Car.LengthM);
    }

    /// <summary>And the shipped file is the one <see cref="SimConfig.Load"/> reads, wherever it is run from.</summary>
    [Fact]
    public void TheFiguresTheGameRunsOnAreTheSharedFileAppliedToTheShippedOnes() =>
        Assert.Equal(
            SharedFiguresReader.Apply(SimConfig.Shipped(), ProjectPaths.SharedFiguresFile).Car.ParkedHandbrake,
            SimConfig.Load().Car.ParkedHandbrake);

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
