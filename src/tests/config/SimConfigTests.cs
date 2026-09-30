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
[Trait(Priority.Key, Priority.P0)]
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
    /// <b>A bay's space is narrower than a lane</b> (GEN-4c). A rank stands its bays a lane apart, each on a
    /// lane's width of tarmac (GEN-53), and a body standing in one holds its space — so a space wider than
    /// that lane is one laid over its neighbour's and past its own tarmac at every bay in the town.
    /// </summary>
    /// <remarks>
    /// <b>It is a relation between two authored figures and not a derivation written twice</b> (VER-12):
    /// the side clearance and the lane are chosen independently, and read at the same margin as a bay's own
    /// ends — which a parallel bay is reversed into — the space came out 4.0 m against a 3.6 m lane. Odesa
    /// drew four hundred and sixty-one steps of perimeter onto that lip.
    /// </remarks>
    [Fact]
    public void ABaysSpaceIsNarrowerThanALane()
    {
        var config = SimConfig.Shipped();

        Assert.True(
            config.ParkingSpaceWidthM < config.LaneWidthM,
            $"a bay's space is {config.ParkingSpaceWidthM:F2} m wide and a lane "
            + $"{config.LaneWidthM:F2} m");
    }

    /// <summary>
    /// <b>A bay is longer than the longest vehicle the town draws</b> (GEN-53). A car park's bays are laid
    /// at one length whatever turns up to stand in them, so a vehicle longer than that is one parked across
    /// the ground the arm was drawn for.
    /// </summary>
    /// <remarks>
    /// <b>It is a relation between two authored figures and not a derivation written twice</b> (VER-12): the
    /// bay is a length of ground chosen for the town and the longest vehicle is a bound on the catalogue,
    /// which <c>CarCatalogTests</c> holds the drawn cars to.
    /// </remarks>
    [Fact]
    public void ABayIsLongerThanTheLongestVehicleInTheTown()
    {
        var config = SimConfig.Shipped();

        Assert.True(
            config.CarParkBayLengthM > config.Car.LongestLengthM,
            $"a bay is {config.CarParkBayLengthM:F2} m long and the longest vehicle in the town is "
            + $"{config.Car.LongestLengthM:F2} m");
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

    /// <summary>
    /// <b>The ribbon atlas is fine enough that a car straddling the line between two lanes is on both</b>
    /// (TER-4c.4): a lattice point is sampled and never a cell, so what one can miss is an overlap thinner
    /// than its diagonal — and a car half over the line overlaps the lane beside by half its width.
    /// </summary>
    [Fact]
    public void AnOverlapOfHalfACarAlwaysHoldsALatticePoint()
    {
        var config = SimConfig.Shipped();

        Assert.True(
            config.RibbonLatticeStepM * MathF.Sqrt(2f) < config.Car.WidthM * 0.5f,
            $"a lattice {config.RibbonLatticeStepM:F2} m apart can miss an overlap of half a car");
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
