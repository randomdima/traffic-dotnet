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
[Trait(Priority.Key, Priority.P9)]
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
        var fleet = new CarFleet(1, arcsPerCar: 1, CarBuilds.OfTheFleet(Config, Catalogue), Config.Patience.FuseJitterShare);
        fleet.Add(new Vector2(20f, 5f), HeadingRad, (byte)variant, backsIntoBays: false, new Rng(1, 1));
        fleet.Driven[0] = true;
        fleet.Command[0] = new DriveCommand(0f, 0f, 4f, false, false);
        fleet.BlueLight[0] = true;
        fleet.LineArcsOf(0)[0] = new ArcSeg(Vector2.Zero, 0f, 100f, 0.05f);
        fleet.SetLine(0, new DrivenLine(1, 1, 100f));

        // The turn the bend is for, since an indicator answers a junction and not a curve (CAR-14.1).
        fleet.ToTheBoxM[0] = 30f;
        fleet.TurningAtTheBox[0] = true;
        return fleet;
    }

    static SpriteInstance[] Drawn(CarFleet fleet, Vector2 viewCentreM, out int written, int handDrivenCar = -1)
    {
        var into = new SpriteInstance[fleet.Count * CarLamps.Most];
        Ticked(fleet, handDrivenCar);
        written = LampSprites.Fill(
            fleet, CarSpriteTests.Every(fleet), Catalogue, Config, LensSheet, GlowSheet, elapsedS: 0f, viewCentreM,
            new Vector2(200f, 200f), into);

        return into;
    }

    /// <summary>What each car's lamps say written into the fleet, as its tick writes them (<see cref="CarFleet.Lamps"/>) — a hand on one car, or on none.</summary>
    static CarFleet Ticked(CarFleet fleet, int handDrivenCar = -1)
    {
        for (var car = 0; car < fleet.Count; car++) fleet.Lamps[car] = CarLamps.Showing(fleet, car, Config, car == handDrivenCar);

        return fleet;
    }

    /// <summary>
    /// Which cells of the sheet these instances drew, read back through <see cref="LampAtlas.CellOf"/>: the column
    /// within its look's block (<see cref="LampAtlas.LensAt"/> says whose) and the look it is (<c>Row</c>).
    /// </summary>
    static HashSet<(int Column, int Row)> CellsOf(SpriteInstance[] drawn, int written)
    {
        var cells = new HashSet<(int, int)>();
        for (var lamp = 0; lamp < written; lamp++)
        {
            if (drawn[lamp].Sheet == GlowSheet) continue;

            var cell = drawn[lamp].Cell;
            cells.Add((cell.Column % LampAtlas.Columns, (cell.Row * LampAtlas.LooksARow) + (cell.Column / LampAtlas.Columns)));
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

        // The bar's burning end (CAR-14.4).
        var drawn = Drawn(police, police.PositionM[0], out var written, handDrivenCar: 0);
        var lenses = Catalogue.LensesOf(Catalogue.Police).ToArray();
        Assert.Equal(1, CellsOf(drawn, written).Count(cell => lenses[LampAtlas.LensAt(cell.Column, lenses).Lens].Fitting
            is CarLampFitting.BeaconRed or CarLampFitting.BeaconBlue));

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
        var lenses = Catalogue.LensesOf(Catalogue.Evacuator).ToArray();
        Assert.Equal(end, CellsOf(drawn, written).Count(cell =>
            lenses[LampAtlas.LensAt(cell.Column, lenses).Lens].Fitting == CarLampFitting.BeaconAmber));
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
            Config, elapsedS: 0f, lamps);

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

    /// <summary>
    /// CAR-14.8 through the renderer: <b>a driven car's headlamps glow as every lit lamp does</b>, so each head lens
    /// stands as two quads — its glow on the glow sheet, then its lens on the lens sheet over it.
    /// </summary>
    [Fact]
    public void AHeadlampBurnsItsLensOverItsGlow()
    {
        var variant = FirstWithHeadlamps();
        var fleet = Lit(variant);
        var drawn = Drawn(fleet, fleet.PositionM[0], out var written);

        var forward = new Vector2(MathF.Cos(HeadingRad), MathF.Sin(HeadingRad));
        var right = new Vector2(-forward.Y, forward.X);
        var heads = 0;
        foreach (var lens in Catalogue.LensesOf(variant))
        {
            if (lens.Fitting != CarLampFitting.Head) continue;

            heads++;
            var atM = fleet.PositionM[0] + (forward * lens.AtBodyM.X) + (right * lens.AtBodyM.Y);
            var standing = drawn.AsSpan(0, written).ToArray().Where(quad => Vector2.Distance(quad.CentreM, atM) < 1e-4f).ToArray();
            Assert.Equal([GlowSheet, LensSheet], standing.Select(quad => (int)quad.Sheet));
        }

        Assert.Equal(2, heads);
    }

    /// <summary>The first look of the fleet drawing a headlamp, whichever the shipped fleet holds.</summary>
    static int FirstWithHeadlamps()
    {
        for (var variant = 0; variant < Catalogue.SheetCount; variant++)
        {
            foreach (var lens in Catalogue.LensesOf(variant))
            {
                if (lens.Fitting == CarLampFitting.Head) return variant;
            }
        }

        throw new InvalidOperationException("no look in the fleet draws a headlamp");
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
            Assert.Equal(LampAtlas.SheetColumns, drawn[lamp].Cell.Columns);
            Assert.Equal(LampAtlas.SheetRows(rows), drawn[lamp].Cell.Rows);
        }

        // Every cell is this car's own block, and the lamps lit are the rear pair braking (each lens's own
        // column), the headlamps of a driven car, the indicators on the side it is turning to — its right, the
        // sixth and eighth lenses — and the left end of its bar, which burns first on a bar of blue alone (CAR-14.4).
        var cells = CellsOf(drawn, written);
        Assert.All(cells, cell => Assert.Equal(ambulance.Variant[0], cell.Row));
        Assert.Equal([0, 1, 2, 3, 5, 7, .. LeftEndCells(ambulance.Variant[0])], cells.Select(cell => cell.Column).Order());

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
    /// <b>The beacon's ends burn in turn, each its own colour</b> (CAR-14.4): the red ends draw their one cell, and half
    /// a period later the blue ends draw theirs. No end ever draws a cell of the other colour.
    /// </summary>
    [Fact]
    public void TheBeaconsEndsTakeTheBurnInTurn()
    {
        var police = Ticked(Lit(Catalogue.Police));
        var halfPeriodS = 0.5f * Config.Lamps.BeaconPeriodS;

        var into = new SpriteInstance[CarLamps.Most];
        var first = LampSprites.Fill(
            police, CarSpriteTests.Every(police), Catalogue, Config, LensSheet, GlowSheet, 0f, police.PositionM[0],
            new Vector2(200f, 200f), into);
        var atStart = BarCells(CellsOf(into, first), police.Variant[0]);

        var later = LampSprites.Fill(
            police, CarSpriteTests.Every(police), Catalogue, Config, LensSheet, GlowSheet, halfPeriodS, police.PositionM[0],
            new Vector2(200f, 200f), into);

        Assert.Equal(RedEndCells(police.Variant[0]), atStart);
        Assert.Equal(EndCells(police.Variant[0], CarLampFitting.BeaconBlue), BarCells(CellsOf(into, later), police.Variant[0]));
    }

    /// <summary>The first cells of a look's lenses of one beacon fitting, which is the one cell an end burns in.</summary>
    static int[] EndCells(int variant, CarLampFitting fitting)
    {
        var lenses = Catalogue.LensesOf(variant);
        var cells = new List<int>();
        for (var lens = 0; lens < lenses.Length; lens++)
        {
            if (lenses[lens].Fitting == fitting) cells.Add(lens);
        }

        return [.. cells];
    }

    static int[] RedEndCells(int variant) => EndCells(variant, CarLampFitting.BeaconRed);

    static int[] LeftEndCells(int variant) =>
        [.. EndCells(variant, CarLampFitting.BeaconBlue).Where(lens => Catalogue.LensesOf(variant)[lens].AtBodyM.Y < 0f)];

    /// <summary>The columns drawn for a look's beacon ends, in order.</summary>
    static int[] BarCells(IEnumerable<(int Column, int Row)> cells, int variant)
    {
        var lenses = Catalogue.LensesOf(variant).ToArray();
        return [.. cells.Select(cell => cell.Column)
            .Where(column => lenses[LampAtlas.LensAt(column, lenses).Lens].Fitting is CarLampFitting.BeaconRed or CarLampFitting.BeaconBlue)
            .Order()];
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
    /// The buffer is laid for a glow and a lens of every lens a car carries (<see cref="TownSprites.RoomFor"/>),
    /// and the fill spends it a whole car at a time: a car half written is a body showing its brake
    /// lamps and not the beacon that says why the traffic in front of it is pulling over.
    /// </summary>
    [Fact]
    public void ACarIsWrittenWholeOrNotAtAll()
    {
        var fleet = Ticked(Lit(Catalogue.Ambulance));
        var into = new SpriteInstance[(2 * Catalogue.LensesOf(Catalogue.Ambulance).Length) - 1];
        var written = LampSprites.Fill(
            fleet, CarSpriteTests.Every(fleet), Catalogue, Config, LensSheet, GlowSheet, elapsedS: 0f, fleet.PositionM[0],
            new Vector2(200f, 200f), into);

        Assert.Equal(0, written);
        Assert.All(into, instance => Assert.Equal(default, instance));
    }
}
