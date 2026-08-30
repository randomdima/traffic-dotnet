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
    /// <b>The pace scale carries through the whole person model, and the square of it through every
    /// acceleration.</b> Distances in this town are real and its pace is not, so a walker's grip is not a
    /// figure that can be authored beside a pace that has moved: watching the town twice as fast is a body
    /// that stops in the same ground, which is four times the grip.
    /// </summary>
    /// <remarks>
    /// <b>What it is really protecting is the casualty band</b>, which is the one place the factor has
    /// bitten. The band comes off the sliding grip and the walk comes off the pace, so a grip left at real
    /// scale beside a pace that is not puts the band <em>below</em> walking speed — where somebody becomes a
    /// casualty by arriving at a parked car. Asked at two scales, because one is a number and two is a
    /// relation.
    /// </remarks>
    [Fact]
    public void ThePersonModelCarriesItsPaceScaleThroughEveryAcceleration()
    {
        var config = SimConfig.Shipped();
        var faster = new SimConfig { Person = new PersonFigures { PaceScale = config.Person.PaceScale * 2f } };

        Assert.Equal(config.PersonWalkSpeedMps * 2f, faster.PersonWalkSpeedMps, 1e-3f);
        Assert.Equal(config.PersonTurnRateDegPerS * 2f, faster.PersonTurnRateDegPerS, 1e-3f);
        Assert.Equal(config.PersonFootGripMps2 * 4f, faster.PersonFootGripMps2, 1e-2f);

        foreach (var figures in new[] { config, faster })
        {
            var stoppingM = figures.PersonWalkSpeedMps * figures.PersonWalkSpeedMps / (2f * figures.PersonFootGripMps2);
            Assert.Equal(figures.PersonDiameterM * figures.Person.StopsWithinDiameters, stoppingM, 1e-3f);

            // The speed a body has to be met at to be put down, which must stay clear of the speed this
            // town walks at — nothing about a contact says who was carrying the closing speed (PER-23).
            var bandMps = MathF.Sqrt(2f * figures.PersonCasualtyKj * 1000f / figures.Person.MassKg);
            Assert.True(
                bandMps > figures.PersonWalkSpeedMps,
                $"a casualty is made at {bandMps:F1} m/s and this town walks at {figures.PersonWalkSpeedMps:F1}");
        }
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
