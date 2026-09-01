using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Config;

/// <summary>
/// <b>What the figures refuse, and the one relation that has ever been got wrong.</b> A derived figure
/// cannot be authored over; a figure this engine does not hold is refused rather than ignored; and the
/// pace scale carries through the whole person model, which is the relation that put the casualty band
/// below walking speed once already.
/// </summary>
/// <remarks>
/// <b>Scaling a figure and asserting everything scaled with it is not a test of this engine</b> (VER-12):
/// the assertion is the derivation written out a second time, so it can only fail on the day somebody
/// changes the derivation on purpose — and on that day it is edited to match rather than read.
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
public class SimConfigTests
{
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
