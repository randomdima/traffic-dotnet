using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>
/// A lit lamp is drawn over the section of the car its art draws a lens on, from the cell of the sheet
/// that section was cut into (CAR-14) — claims about numbers in an instance, and so checked as ones
/// rather than by looking at a town.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class LampSpriteTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly CarCatalog Catalogue = CarCatalog.Shared;

    const int LensSheet = 11;

    const int GlowSheet = 12;

    const float HeadingRad = 0.7f;

    /// <summary>One car with everything lit: braking, pulling round to its right, under a priority.</summary>
    static CarFleet Lit(int variant)
    {
        var fleet = new CarFleet(1, arcsPerCar: 1, CarBuilds.OfTheFleet(Config, Catalogue));
        fleet.Add(default, new Vector2(20f, 5f), HeadingRad, (byte)variant, backsIntoBays: false, new Rng(1, 1));
        fleet.Driven[0] = true;
        fleet.Command[0] = new DriveCommand(0f, 0f, 4f, false, false);
        fleet.BlueLight[0] = true;
        fleet.LineArcsOf(0)[0] = new ArcSeg(Vector2.Zero, 0f, 100f, 0.05f);
        fleet.Line[0] = new DrivenLine(1, 1, 100f);

        // The turn the bend is for, since an indicator answers a junction and not a curve (CAR-14.1).
        fleet.ToTheBoxM[0] = 30f;
        fleet.TurningAtTheBox[0] = true;
        fleet.FuseJitter[0] = 1f;
        return fleet;
    }

    static SpriteInstance[] Drawn(CarFleet fleet, Vector2 viewCentreM, out int written, int handDrivenCar = -1)
    {
        var into = new SpriteInstance[fleet.Count * CarLamps.Most];
        written = LampSprites.Fill(
            fleet, Catalogue, Config, LensSheet, GlowSheet, elapsedS: 0f, TheHandsOn(handDrivenCar), viewCentreM,
            new Vector2(200f, 200f), into);

        return into;
    }

    /// <summary>The selection a hand is on, as the fill reads it: one car, or nothing at all.</summary>
    static Selection[] TheHandsOn(int car) =>
        car >= 0 ? [new Selection(SelectionKind.Car, car)] : [];

    /// <summary>Which cells of the sheet these instances drew, as a column and a row apiece.</summary>
    static HashSet<(int Column, int Row)> CellsOf(SpriteInstance[] drawn, int written)
    {
        var rows = Catalogue.SheetCount;
        var cells = new HashSet<(int, int)>();
        for (var lamp = 0; lamp < written; lamp++)
        {
            if (drawn[lamp].Sheet == GlowSheet) continue;

            cells.Add((
                (int)MathF.Round(drawn[lamp].UvMin.X * LampAtlas.Columns),
                (int)MathF.Round(drawn[lamp].UvMin.Y * rows)));
        }

        return cells;
    }

    /// <summary>
    /// CTL-5c through the renderer: <b>the car the player has the wheel of runs its beacon</b>, standing
    /// down and carrying no priority. The frame is what the seam is for — the fact is the interface's, so
    /// a fill that never passes it on leaves the arithmetic right and the screen dark.
    /// </summary>
    [Fact]
    public void TheCarUnderAHandRunsItsBeacon()
    {
        var police = Lit(Catalogue.Police);
        police.Driven[0] = false;
        police.BlueLight[0] = false;
        police.Command[0] = DriveCommand.Parked;

        // The bar's two ends, which on this car are its fifth and sixth lenses.
        var drawn = Drawn(police, police.PositionM[0], out var written, handDrivenCar: 0);
        Assert.Equal(2, CellsOf(drawn, written).Count(cell => cell.Column >= 8));

        // And the same car with nobody's hand on it draws nothing at all: every lamp it has is off,
        // and an unlit lens is the car's own picture.
        Drawn(police, police.PositionM[0], out written);
        Assert.Equal(0, written);
    }

    /// <summary>
    /// CAR-14.4 through the renderer: an amber bar burns one end at a time, so an evacuator carrying the
    /// priority draws the cells of one of its two ends and none of the other's. The end that is dark is
    /// its own dull glass, already on screen as part of the car.
    /// </summary>
    [Fact]
    public void TheAmberBarDrawsOnlyTheEndThatIsBurning()
    {
        var evacuator = Lit(Catalogue.Evacuator);
        var drawn = Drawn(evacuator, evacuator.PositionM[0], out var written);

        // Counted off the art rather than written down, since how many lamps an end is made of is the
        // variant's business and this claim is about ends.
        var end = AmberLenses(Catalogue.Evacuator) / 2;
        Assert.Equal(end, CellsOf(drawn, written).Count(cell => cell.Column >= 8));
    }

    /// <summary>How many amber beacon lenses this variant's art draws, which is its bar's two ends.</summary>
    static int AmberLenses(int variant)
    {
        var lenses = 0;
        foreach (var lens in Catalogue.LensesOf(variant))
        {
            if (lens.Fitting == CarLampFitting.BeaconAmber) lenses++;
        }

        return lenses;
    }

    /// <summary>
    /// <b>The renderer asks the car where its lenses are.</b> Two constructions that agree are the more
    /// misleading of the two: they agree until one of them is changed.
    /// </summary>
    [Fact]
    public void EveryLitLampIsDrawnWhereTheArtSaysItIs()
    {
        var fleet = Lit(Catalogue.Ambulance);
        var drawn = Drawn(fleet, fleet.PositionM[0], out var written);

        Span<ShownLamp> lamps = stackalloc ShownLamp[CarLamps.MostLenses];
        var count = CarLamps.Shown(
            Catalogue.LensesOf(fleet.Variant[0]), CarLamps.Showing(fleet, 0, Config, handAtTheWheel: false),
            Config, elapsedS: 0f, rateJitter: 1f, lamps);

        var forward = new Vector2(MathF.Cos(HeadingRad), MathF.Sin(HeadingRad));
        var right = new Vector2(-forward.Y, forward.X);
        var lit = 0;
        for (var lamp = 0; lamp < count; lamp++)
        {
            if (lamps[lamp].Lit) lit++;
        }

        var instance = 0;
        for (var lamp = 0; lamp < count; lamp++)
        {
            // A lit lamp is the glow under it and the lens itself; a lamp that is off is the car's own
            // art and costs nothing here.
            if (!lamps[lamp].Lit) continue;

            var atBody = lamps[lamp].Lens.AtBodyM;
            var expectedM = fleet.PositionM[0] + (forward * atBody.X) + (right * atBody.Y);

            // The glow in the car's first run of instances and the lens in its second, so the two stand
            // a lit lamp apart rather than side by side.
            AssertQuadAt(drawn[instance], expectedM);
            AssertQuadAt(drawn[instance + lit], expectedM);
            instance++;
        }

        Assert.Equal(written, lit * 2);
    }

    /// <summary>A quad standing on this spot of the town, turned the way the car is.</summary>
    static void AssertQuadAt(SpriteInstance instance, Vector2 atM)
    {
        Assert.Equal(atM.X, instance.CentreM.X, 1e-4f);
        Assert.Equal(atM.Y, instance.CentreM.Y, 1e-4f);
        Assert.Equal(HeadingRad, instance.HeadingRad, 1e-4f);
    }

    /// <summary>
    /// One sheet for every lamp in the town: what makes a brake lamp a brake lamp is which cell of it is
    /// sampled, so a frame's crossings do not count the lamps (TEC-1).
    /// </summary>
    [Fact]
    public void TheLampsShareOneSheetAndDifferOnlyInTheCell()
    {
        var ambulance = Lit(Catalogue.Ambulance);
        var drawn = Drawn(ambulance, new Vector2(20f, 5f), out var written);

        var rows = Catalogue.SheetCount;
        var glows = 0;
        for (var lamp = 0; lamp < written; lamp++)
        {
            if (drawn[lamp].Sheet == GlowSheet)
            {
                glows++;
                continue;
            }

            Assert.Equal((uint)LensSheet, drawn[lamp].Sheet);
            Assert.Equal(1f / LampAtlas.Columns, drawn[lamp].UvSize.X, 1e-5f);
            Assert.Equal(1f / rows, drawn[lamp].UvSize.Y, 1e-5f);
        }

        // Every cell is this car's own row, and the lamps lit are the rear pair braking (the first
        // column of each), the indicator on the side it is turning to — its right, the fourth lens —
        // and the two ends of each of its two bars.
        var cells = CellsOf(drawn, written);
        Assert.All(cells, cell => Assert.Equal(ambulance.Variant[0], cell.Row));
        Assert.Equal([0, 2, 6, 9, 11, 13, 15, 17, 19], cells.Select(cell => cell.Column).Order());

        // One glow a lit lamp, and nothing is drawn for the lamps that are off.
        Assert.Equal(cells.Count, glows);
    }

    /// <summary>
    /// <b>A car's glows are all laid before any of its lenses.</b> Instances are drawn in the order they
    /// are written, so a glow written lamp by lamp lands on the glass of the lamp before it — which on a
    /// bar of two ends is one washed lamp beside one crisp one, trading places as the ends take turns.
    /// </summary>
    [Fact]
    public void EveryGlowOnACarIsLaidBeforeAnyOfItsLenses()
    {
        var evacuator = Lit(Catalogue.Evacuator);
        var drawn = Drawn(evacuator, evacuator.PositionM[0], out var written);

        var lenses = 0;
        for (var lamp = 0; lamp < written; lamp++)
        {
            if (drawn[lamp].Sheet == LensSheet) lenses++;
            else Assert.Equal(0, lenses);
        }

        // Two at the least, or the claim above is about a car that never draws a pair.
        Assert.True(lenses > 1, $"the evacuator drew {lenses} lit lamps, which proves nothing.");
    }

    /// <summary>
    /// <b>The beacon's two ends swap rather than blink</b> (CAR-14.4), and what swaps is the cell each of
    /// them draws: the end that was red draws the red cell and half a period later the blue one.
    /// </summary>
    [Fact]
    public void TheBeaconsEndsExchangeTheirCells()
    {
        var ambulance = Lit(Catalogue.Ambulance);
        var halfPeriodS = 0.5f / Config.Lamps.BeaconHz;

        var into = new SpriteInstance[CarLamps.Most];
        var first = LampSprites.Fill(
            ambulance, Catalogue, Config, LensSheet, GlowSheet, 0f, TheHandsOn(-1), ambulance.PositionM[0],
            new Vector2(200f, 200f), into);
        var atStart = CellsOf(into, first).Where(cell => cell.Column >= 8).Select(cell => cell.Column).Order().ToArray();

        var swapped = LampSprites.Fill(
            ambulance, Catalogue, Config, LensSheet, GlowSheet, halfPeriodS, TheHandsOn(-1), ambulance.PositionM[0],
            new Vector2(200f, 200f), into);
        var later = CellsOf(into, swapped).Where(cell => cell.Column >= 8).Select(cell => cell.Column).Order().ToArray();

        // The bars are the last six lenses, so their cells are columns 8 to 19: one instant draws every
        // end in the other's colour and the next draws each in its own. Which of the two the clock
        // starts on is not the claim; that half a period exchanges them is.
        Assert.Equal([9, 11, 13, 15, 17, 19], atStart);
        Assert.Equal([8, 10, 12, 14, 16, 18], later);
    }

    /// <summary>
    /// <b>A lamp that is off is drawn by nobody</b> (CAR-14a): the dull lens is the section of the car's
    /// own picture the lamp was cut from, and it is already on screen. A car saying nothing costs the
    /// lamp pass nothing at all.
    /// </summary>
    [Fact]
    public void ACarSayingNothingDrawsNoLamps()
    {
        var quiet = Lit(Catalogue.Ambulance);
        quiet.Driven[0] = false;
        quiet.BlueLight[0] = false;
        quiet.Command[0] = DriveCommand.Parked;

        Drawn(quiet, quiet.PositionM[0], out var written);
        Assert.Equal(0, written);
    }

    /// <summary>A wreck shows nothing at all (CAR-14.5) — its art is not the car the lenses were measured off — and a car out of frame is culled with its body.</summary>
    [Fact]
    public void AWreckAndACarOutOfFrameAreBothFree()
    {
        var broken = Lit(0);
        broken.Broken[0] = true;
        Drawn(broken, broken.PositionM[0], out var written);
        Assert.Equal(0, written);

        Drawn(Lit(0), new Vector2(2_000f, 2_000f), out var offScreen);
        Assert.Equal(0, offScreen);
    }

    /// <summary>
    /// The buffer is laid for <see cref="CarLamps.Most"/> a car (<see cref="TownSprites.CapacityFor"/>),
    /// and the fill spends it a whole car at a time: a car half written is a body showing its brake
    /// lamps and not the beacon that says why the traffic in front of it is pulling over.
    /// </summary>
    [Fact]
    public void ACarIsWrittenWholeOrNotAtAll()
    {
        var fleet = Lit(Catalogue.Ambulance);
        var into = new SpriteInstance[CarLamps.Most - 1];
        var written = LampSprites.Fill(
            fleet, Catalogue, Config, LensSheet, GlowSheet, elapsedS: 0f, TheHandsOn(-1),
            fleet.PositionM[0], new Vector2(200f, 200f), into);

        Assert.Equal(0, written);
        Assert.All(into, instance => Assert.Equal(default, instance));
    }
}
