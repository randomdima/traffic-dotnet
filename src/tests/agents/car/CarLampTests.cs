using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// CAR-14: what a car's lamps say. The indicator is the claim worth checking hardest — it is read off
/// the line the car is about to drive rather than announced by whatever manoeuvre laid that line, so
/// what the test stages is geometry and never an entry of the catalogue.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class CarLampTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly CarCatalog Catalogue = CarCatalog.Shared;

    /// <summary>How far ahead the junction is staged, which is inside the reach an indicator answers one at.</summary>
    const float JunctionAheadM = 30f;

    /// <summary>
    /// A car with a driver in it, standing on a straight line with its foot off everything, <b>approaching
    /// a junction it turns at</b> — which is the only state an indicator says anything in (CAR-14.1), so it
    /// is what every case about one is staged over.
    /// </summary>
    static CarFleet Rolling(float curvature = 0f, float lengthM = 100f, int variant = 0)
    {
        var fleet = new CarFleet(1, arcsPerCar: 2, CarBuilds.OfTheFleet(Config, Catalogue));
        fleet.Add(default, Vector2.Zero, 0f, (byte)variant, backsIntoBays: false, new Rng(1, 1));
        fleet.Driven[0] = true;
        fleet.Command[0] = DriveCommand.Idle;
        fleet.LineArcsOf(0)[0] = new ArcSeg(Vector2.Zero, 0f, lengthM, curvature);
        fleet.Line[0] = new DrivenLine(1, 1, lengthM);
        fleet.ToTheBoxM[0] = JunctionAheadM;
        fleet.TurningAtTheBox[0] = true;
        return fleet;
    }

    static CarLampSet Showing(CarFleet fleet, bool handAtTheWheel = false) =>
        CarLamps.Showing(fleet, 0, Config, handAtTheWheel);

    /// <summary>What this car's lenses are showing, and how many of them are lit.</summary>
    static int Lit(CarFleet fleet, float elapsedS, Span<ShownLamp> into)
    {
        var count = CarLamps.Shown(
            Catalogue.LensesOf(fleet.Variant[0]), Showing(fleet), Config, elapsedS, fleet.FuseJitter[0], into);

        return Lit(into[..count]);
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

    /// <summary>How many of these lenses are lit.</summary>
    static int Lit(ReadOnlySpan<ShownLamp> lamps)
    {
        var lit = 0;
        foreach (var lamp in lamps)
        {
            if (lamp.Lit) lit++;
        }

        return lit;
    }

    /// <summary>A lamp is something the car is doing (CAR-1), so a car nobody is in has none to show — and a parked car standing on its handbrake is not holding a pedal down.</summary>
    [Fact]
    public void ACarNobodyIsInAndAWreckShowNothing()
    {
        var fleet = Rolling();
        fleet.Driven[0] = false;
        fleet.Command[0] = DriveCommand.Parked;
        Assert.Equal(CarLampSet.None, Showing(fleet));

        fleet.Driven[0] = true;
        fleet.Broken[0] = true;
        fleet.Command[0] = DriveCommand.Locked;
        Assert.Equal(CarLampSet.None, Showing(fleet));
    }

    /// <summary>
    /// The turn the line makes and not the wheel the driver is holding: a car indicates on the approach,
    /// where the steering is still straight and the junction is thirty metres off.
    /// </summary>
    /// <param name="side">Which lamp, as the side of the body it is on: +1 the car's right, −1 its left, 0 neither.</param>
    [Theory]
    [InlineData(0f, 0)]
    [InlineData(0.05f, 1)]
    [InlineData(-0.05f, -1)]
    public void TheIndicatorNamesTheSideTheLineAheadBendsTo(float curvature, int side)
    {
        var expected = side switch
        {
            1 => CarLampSet.TurnRight,
            -1 => CarLampSet.TurnLeft,
            _ => CarLampSet.None,
        };

        Assert.Equal(expected, Showing(Rolling(curvature)));
    }

    /// <summary>A street's own bend is not a turn: below the threshold nothing is said, or every car indicates its way along a curved road.</summary>
    [Fact]
    public void AGentleBendSaysNothing()
    {
        var justUnderRad = (Config.Lamps.TurnDeg * MathF.PI / 180f) * 0.9f;
        Assert.Equal(CarLampSet.None, Showing(Rolling(justUnderRad / Config.Lamps.TurnAheadM)));
    }

    /// <summary>
    /// CAR-14.1 — <b>a bend with no junction at the end of it is a road and not a turn.</b> A circuit of
    /// constant radius bends past the threshold for ever, and a car announcing that all the way round is
    /// announcing something nobody can act on: there is nowhere else for it to go.
    /// </summary>
    [Fact]
    public void ABendWithNoJunctionAtTheEndOfItIsNotIndicated()
    {
        var fleet = Rolling(curvature: 0.05f);
        Assert.Equal(CarLampSet.TurnRight, Showing(fleet));

        fleet.ToTheBoxM[0] = float.PositiveInfinity;
        fleet.TurningAtTheBox[0] = false;
        Assert.Equal(CarLampSet.None, Showing(fleet));
    }

    /// <summary>And a junction the car goes straight on through is nothing to announce either.</summary>
    [Fact]
    public void GoingStraightOnThroughAJunctionSaysNothing()
    {
        var fleet = Rolling(curvature: 0.05f);
        fleet.TurningAtTheBox[0] = false;

        Assert.Equal(CarLampSet.None, Showing(fleet));
    }

    /// <summary>
    /// And it is announced on the approach rather than from two streets away: past the reach the lamp is
    /// dark, whatever the line does at the far end of it.
    /// </summary>
    [Fact]
    public void AJunctionTooFarOffIsNotAnnouncedYet()
    {
        var fleet = Rolling(curvature: 0.05f);
        fleet.ToTheBoxM[0] = Config.Lamps.JunctionAheadM * 1.1f;

        Assert.Equal(CarLampSet.None, Showing(fleet));
    }

    /// <summary>Only the stretch in front of the car counts: the turn it has already made is not one it is about to make.</summary>
    [Fact]
    public void ATurnAlreadyDrivenThroughIsNotIndicated()
    {
        var fleet = Rolling(curvature: 0.05f, lengthM: 200f);
        Assert.Equal(CarLampSet.TurnRight, Showing(fleet));

        fleet.LineArcsOf(0)[0] = new ArcSeg(Vector2.Zero, 0f, 40f, 0.05f);
        fleet.LineArcsOf(0)[1] = new ArcSeg(new Vector2(30f, 20f), 2f, 160f, 0f);
        fleet.Line[0] = new DrivenLine(2, 1, 200f);
        fleet.ProgressM[0] = 40f;

        Assert.Equal(CarLampSet.None, Showing(fleet));
    }

    /// <summary>
    /// A reverse line is laid the way the rear axle travels, so its bend is the body's the other way
    /// round. A car reversing out of a bay to its own left indicates left.
    /// </summary>
    [Fact]
    public void AReverseLinesBendIsReadInTheBodysOwnFrame()
    {
        var fleet = Rolling(curvature: 0.05f);
        fleet.LineIsReverse[0] = true;

        Assert.Equal(CarLampSet.TurnLeft, Showing(fleet) & (CarLampSet.TurnLeft | CarLampSet.TurnRight));
    }

    /// <summary>Half a turn is signed: differencing two headings would wrap it and lose which way round it went.</summary>
    [Fact]
    public void HalfATurnIsIndicatedTheWayItSweeps()
    {
        var fleet = Rolling();
        fleet.LineArcsOf(0)[0] = new ArcSeg(Vector2.Zero, 0f, 20f, -MathF.PI / 20f);
        fleet.Line[0] = new DrivenLine(1, 1, 20f);

        Assert.Equal(CarLampSet.TurnLeft, Showing(fleet));
    }

    [Fact]
    public void TheBrakeLampsAreThePedalAndTheReversingLampsAreTheGear()
    {
        var fleet = Rolling();
        fleet.Command[0] = new DriveCommand(0f, 0f, 4f, false, false);
        Assert.Equal(CarLampSet.Brake, Showing(fleet));

        fleet.Command[0] = new DriveCommand(0f, 2f, 0f, false, true);
        Assert.Equal(CarLampSet.Reverse, Showing(fleet));
    }

    /// <summary>AMB-4: the beacon is the priority the car is carrying, and an ambulance that is not on a call is ordinary traffic (AMB-4b).</summary>
    [Fact]
    public void TheBeaconIsThePriorityAndNotTheVehicle()
    {
        var fleet = Rolling();
        fleet.Ambulance[0] = true;
        Assert.Equal(CarLampSet.None, Showing(fleet));

        fleet.BlueLight[0] = true;
        Assert.Equal(CarLampSet.Beacon, Showing(fleet));
    }

    /// <summary>
    /// CTL-5c: a hand at the wheel runs the beacon for as long as it is held, and the art is what says
    /// which cars have one to run — a hand-driven hatchback shows nothing.
    /// </summary>
    [Fact]
    public void AHandAtTheWheelRunsTheBeaconOfACarThatHasOne()
    {
        var police = Rolling(variant: Catalogue.Police);
        Assert.Equal(CarLampSet.None, Showing(police));
        Assert.Equal(CarLampSet.Beacon, Showing(police, handAtTheWheel: true));

        Span<ShownLamp> lamps = stackalloc ShownLamp[CarLamps.MostLenses];
        var lit = CarLamps.Shown(
            Catalogue.LensesOf(police.Variant[0]), Showing(police, handAtTheWheel: true), Config, elapsedS: 0f,
            police.FuseJitter[0], lamps);

        Assert.Equal(2, Lit(lamps[..lit]));

        var hatchback = Rolling();
        var showing = Showing(hatchback, handAtTheWheel: true);
        lit = CarLamps.Shown(
            Catalogue.LensesOf(hatchback.Variant[0]), showing, Config, elapsedS: 0f, hatchback.FuseJitter[0], lamps);

        Assert.Equal(0, Lit(lamps[..lit]));
    }

    /// <summary>The indicator is dark for part of every period and the beacon is dark in none of them: what a beacon does is swap its two ends.</summary>
    [Fact]
    public void TheIndicatorFlashesAndTheBeaconSwapsInstead()
    {
        var indicating = Rolling(curvature: 0.05f);
        var beaconed = Rolling(variant: Catalogue.Ambulance);
        beaconed.BlueLight[0] = true;

        Span<ShownLamp> lamps = stackalloc ShownLamp[CarLamps.MostLenses];
        var dark = 0;
        var reds = 0;
        var blues = 0;
        const int Samples = 64;
        for (var sample = 0; sample < Samples; sample++)
        {
            var elapsedS = sample / (float)Samples / Config.Lamps.FlashHz;
            if (Lit(indicating, elapsedS, lamps) == 0) dark++;

            // Both of the ambulance's bars, and every lens of both of them, in every frame.
            Assert.Equal(6, Lit(beaconed, elapsedS, lamps));
            foreach (var lamp in lamps)
            {
                if (!lamp.Lit) continue;
                if (lamp.Colour == CarLamp.BeaconRed) reds++;
                else if (lamp.Colour == CarLamp.BeaconBlue) blues++;
            }
        }

        Assert.InRange(dark, 1, Samples - 1);
        Assert.Equal(3 * Samples, reds);
        Assert.Equal(3 * Samples, blues);
    }

    /// <summary>
    /// CAR-14.4: <b>a second bar runs against the first</b>. The ambulance draws one across its roof and
    /// one across its back doors, and at every instant the end of one is the colour the end beside it on
    /// the other is not — so a driver behind reads a bar that is changing whichever one they can see.
    /// </summary>
    [Fact]
    public void TheSecondBarCarriesWhatTheFirstOneIsNot()
    {
        var ambulance = Rolling(variant: Catalogue.Ambulance);
        ambulance.BlueLight[0] = true;

        Span<ShownLamp> lamps = stackalloc ShownLamp[CarLamps.MostLenses];
        const int Samples = 16;
        for (var sample = 0; sample < Samples; sample++)
        {
            var elapsedS = sample / (float)Samples / Config.Lamps.BeaconHz;
            var count = CarLamps.Shown(
                Catalogue.LensesOf(ambulance.Variant[0]), Showing(ambulance), Config, elapsedS,
                ambulance.FuseJitter[0], lamps);

            // The bars are told apart by where they are on the body, which is all the art says about
            // them: one is forward of the middle and the other behind it.
            foreach (var side in (float[])[-1f, 1f])
            {
                var roof = CarLamp.Indicator;
                var back = CarLamp.Indicator;
                for (var lamp = 0; lamp < count; lamp++)
                {
                    var lens = lamps[lamp].Lens;
                    if (lens.Fitting is not (CarLampFitting.BeaconRed or CarLampFitting.BeaconBlue)) continue;
                    if (MathF.Sign(lens.AtBodyM.Y) != side) continue;

                    Assert.True(lamps[lamp].Lit, "a beacon end went dark while the priority was carried");
                    if (lens.AtBodyM.X > 0f) roof = lamps[lamp].Colour;
                    else back = lamps[lamp].Colour;
                }

                Assert.NotEqual(CarLamp.Indicator, roof);
                Assert.NotEqual(CarLamp.Indicator, back);
                Assert.NotEqual(roof, back);
            }
        }
    }

    /// <summary>
    /// CAR-14.4: an amber bar has no second colour to swap to, so its two ends take the burn in turns —
    /// one end lit in every frame the priority is carried, and each of them dark for half of it. <b>An end
    /// is however many lenses the art draws there</b> and burns whole: a bar of two lamps a side is two
    /// ends and not four.
    /// </summary>
    [Fact]
    public void TheAmberBeaconTakesItsTwoEndsInTurns()
    {
        var evacuator = Rolling(variant: Catalogue.Evacuator);
        evacuator.BlueLight[0] = true;

        Span<ShownLamp> lamps = stackalloc ShownLamp[CarLamps.MostLenses];
        var left = 0;
        var right = 0;
        const int Samples = 64;
        for (var sample = 0; sample < Samples; sample++)
        {
            // Four periods of it, since each car flashes at its own rate and one nominal period of a
            // jittered one is not a whole one.
            var elapsedS = sample / (float)Samples * 4f / Config.Lamps.BeaconHz;
            var count = CarLamps.Shown(
                Catalogue.LensesOf(evacuator.Variant[0]), Showing(evacuator), Config, elapsedS,
                evacuator.FuseJitter[0], lamps);

            var leftLenses = 0;
            var leftLit = 0;
            var rightLenses = 0;
            var rightLit = 0;
            foreach (var lamp in lamps[..count])
            {
                if (lamp.Lens.Fitting != CarLampFitting.BeaconAmber) continue;

                Assert.Equal(CarLamp.BeaconAmber, lamp.Colour);
                if (lamp.Lens.AtBodyM.Y < 0f)
                {
                    leftLenses++;
                    if (lamp.Lit) leftLit++;
                }
                else
                {
                    rightLenses++;
                    if (lamp.Lit) rightLit++;
                }
            }

            Assert.True(leftLenses > 0 && rightLenses > 0, "the evacuator's art draws no amber bar.");
            Assert.True(
                (leftLit == leftLenses && rightLit == 0) || (rightLit == rightLenses && leftLit == 0),
                $"{leftLit}/{leftLenses} lenses lit at one end and {rightLit}/{rightLenses} at the other.");

            if (leftLit > 0) left++;
            else right++;
        }

        Assert.InRange(left, 1, Samples - 1);
        Assert.Equal(Samples, left + right);
    }

    /// <summary>
    /// CAR-14.6: <b>the amber bar is the job and not the priority</b>. It is up on the legs the town owes
    /// the truck nothing for — which is most of a recovery — and out again the moment the errand is over.
    /// </summary>
    [Fact]
    public void TheAmberBeaconIsTheWorkAndNotThePriority()
    {
        var evacuator = Rolling(variant: Catalogue.Evacuator);
        Assert.Equal(CarLampSet.None, Showing(evacuator));

        evacuator.AtWork[0] = true;
        Assert.Equal(CarLampSet.Works, Showing(evacuator));

        // One end of the bar, counted off the art: how many lamps a variant draws at each end is its own
        // business, and what is claimed here is that the bar is up at all.
        var end = AmberLenses(evacuator.Variant[0]) / 2;

        Span<ShownLamp> lamps = stackalloc ShownLamp[CarLamps.MostLenses];
        Assert.Equal(end, Lit(evacuator, elapsedS: 0f, lamps));

        evacuator.AtWork[0] = false;
        Assert.Equal(0, Lit(evacuator, elapsedS: 0f, lamps));
    }

    /// <summary>
    /// A beacon that is off is <b>its own colour, dulled</b>, and not whichever colour the swap was
    /// mid-way through: an ambulance standing at its hospital would otherwise sit there flickering
    /// between two dark colours for nobody.
    /// </summary>
    [Fact]
    public void ABeaconAtRestKeepsTheColourItsArtDraws()
    {
        var fleet = Rolling(variant: Catalogue.Ambulance);

        Span<ShownLamp> lamps = stackalloc ShownLamp[CarLamps.MostLenses];
        for (var sample = 0; sample < 16; sample++)
        {
            var count = CarLamps.Shown(
                Catalogue.LensesOf(fleet.Variant[0]), Showing(fleet), Config, sample / 8f, rateJitter: 1f, lamps);

            foreach (var lamp in lamps[..count])
            {
                Assert.False(lamp.Lit);
                if (lamp.Lens.Fitting == CarLampFitting.BeaconRed) Assert.Equal(CarLamp.BeaconRed, lamp.Colour);
                if (lamp.Lens.Fitting == CarLampFitting.BeaconBlue) Assert.Equal(CarLamp.BeaconBlue, lamp.Colour);
            }
        }
    }

    /// <summary>
    /// Two cars indicating do not blink together, whatever they are doing: each flashes at its own rate,
    /// so a queue of them drifts apart instead of locking into one signal.
    /// </summary>
    [Fact]
    public void TwoCarsIndicatingDoNotFlashInStep()
    {
        var one = Rolling(curvature: 0.05f);
        var other = Rolling(curvature: 0.05f);
        other.FuseJitter[0] = one.FuseJitter[0] * 1.1f;

        Span<ShownLamp> lamps = stackalloc ShownLamp[CarLamps.MostLenses];
        var apart = false;
        for (var sample = 0; sample < 64; sample++)
        {
            var elapsedS = sample / 8f;
            apart |= (Lit(one, elapsedS, lamps) == 0) != (Lit(other, elapsedS, lamps) == 0);
        }

        Assert.True(apart, "two cars indicating were lit and dark together for eight seconds");
    }

    /// <summary>
    /// <b>Every car in the fleet draws the lenses it needs, and every lens is on the body it is measured
    /// off</b> (CAR-14a): a rear pair and a front pair each, in from the outline rather than hanging off
    /// it, and none of them on the centreline. The places are authored art, so this is the gate that
    /// catches a lens typed a digit out.
    /// </summary>
    [Fact]
    public void EveryVariantDrawsItsLampsOnTheBodyTheyAreMeasuredOff()
    {
        for (var variant = 0; variant < Catalogue.SheetCount; variant++)
        {
            var car = Catalogue.Variants[variant];
            var lenses = Catalogue.LensesOf(variant);
            var halfM = car.FootprintM * 0.5f;
            var rears = 0;
            var indicators = 0;

            Assert.InRange(lenses.Length, 1, CarLamps.MostLenses);
            foreach (var lens in lenses)
            {
                if (lens.Fitting == CarLampFitting.Rear) rears++;
                if (lens.Fitting == CarLampFitting.Indicator) indicators++;

                var reach = lens.AtBodyM + (lens.SizeM * 0.5f);
                Assert.InRange(MathF.Abs(reach.X), 0f, halfM.X);
                Assert.InRange(MathF.Abs(reach.Y), 0f, halfM.Y);
                Assert.True(
                    MathF.Abs(lens.AtBodyM.Y) > lens.SizeM.Y * 0.5f,
                    $"{car.Id} draws a {lens.Fitting} lens across its own centreline");
            }

            Assert.Equal(2, rears);
            Assert.Equal(2, indicators);
        }
    }

    /// <summary>
    /// The rear pair is at the tail and the front pair at the nose, and each pair is a mirrored pair:
    /// a variant whose file has a sign wrong is a car indicating out of the boot.
    /// </summary>
    [Fact]
    public void TheRearPairIsBehindTheMiddleAndTheIndicatorsAreAheadOfIt()
    {
        for (var variant = 0; variant < Catalogue.SheetCount; variant++)
        {
            var car = Catalogue.Variants[variant];
            var lenses = Catalogue.LensesOf(variant);
            foreach (var lens in lenses)
            {
                var ahead = lens.AtBodyM.X > 0f;
                if (lens.Fitting == CarLampFitting.Rear) Assert.False(ahead, $"{car.Id} brakes from its nose");
                if (lens.Fitting == CarLampFitting.Indicator) Assert.True(ahead, $"{car.Id} indicates from its tail");

                var twin = Mirrored(lenses, lens);
                Assert.True(twin, $"{car.Id} draws a {lens.Fitting} lens with nothing on its other flank");
            }
        }

        static bool Mirrored(ReadOnlySpan<CarLens> lenses, CarLens lens)
        {
            foreach (var other in lenses)
            {
                if (MathF.Abs(other.AtBodyM.X - lens.AtBodyM.X) < 0.05f
                    && MathF.Abs(other.AtBodyM.Y + lens.AtBodyM.Y) < 0.05f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
