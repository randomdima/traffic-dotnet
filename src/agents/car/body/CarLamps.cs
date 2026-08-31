using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.Agents.Car.Body;

/// <summary>What a car is saying with its lamps, with no clock in it (CAR-14).</summary>
[Flags]
internal enum CarLampSet : byte
{
    None = 0,

    /// <summary>The side the line ahead bends to, which is the side the car is about to turn towards.</summary>
    TurnLeft = 1,
    TurnRight = 2,

    Brake = 4,
    Reverse = 8,

    /// <summary>The priority the car is carrying (AMB-4), or the hand at its wheel (CTL-5c).</summary>
    Beacon = 16,

    /// <summary>The job the vehicle is out on (CAR-14.6), which is what an amber bar says and what it grants.</summary>
    Works = 32,
}

/// <summary>
/// Which fitting on the body a lens belongs to, which is the whole of what it is able to show. It is
/// the variant's own art that says where each one is (<see cref="CarLens"/>).
/// </summary>
internal enum CarLampFitting : byte
{
    /// <summary>The rear cluster: red under the pedal, white in reverse gear.</summary>
    Rear,

    /// <summary>A front corner lamp, which flashes on the side the line ahead bends to.</summary>
    Indicator,

    /// <summary>One end of a beacon bar, red at rest. Lit, the bar's two ends swap colours (AMB-4).</summary>
    /// <remarks>
    /// <b>Which colour an end is at rest is the whole of a second bar's phase</b> (CAR-14.4): a car
    /// drawing two bars crosses the fittings on the second, so at every instant it carries the colours
    /// the first one is not.
    /// </remarks>
    BeaconRed,

    /// <summary>A bar's other end, blue at rest.</summary>
    BeaconBlue,

    /// <summary>
    /// One end of a works bar, amber at rest and amber lit: it has no second colour to swap to, so what
    /// it does instead is blink, and a pair of them blinks end for end (CAR-14.4).
    /// </summary>
    BeaconAmber,
}

/// <summary>
/// One lamp lens as the variant's art draws it: which fitting it is, where it sits in the body's own
/// frame — <c>+x</c> the nose, <c>+y</c> the car's right — and the section of bodywork it covers.
/// </summary>
/// <remarks>
/// <para>
/// Measured off the picture, as <see cref="CarVariantFile.TrackM"/> is, so a lamp lights the panel the
/// artist drew a lens on rather than a place arithmetic put it. A variant that draws no lens for a
/// fitting carries none, and that fitting says nothing on that car.
/// </para>
/// <para>
/// <b><see cref="AtBodyM"/> is on the art's texel grid</b> — the file's figure snapped to the nearest
/// corner of it as the catalogue reads it, half a texel at most. The cut lamp is whole texels of the
/// sprite and lands on a whole-texel grid inside its cell, so a centre anywhere else would draw the lit
/// lamp a fraction of a texel off the dull lens it was cut from, which is on screen beside it.
/// </para>
/// </remarks>
internal readonly record struct CarLens(CarLampFitting Fitting, Vector2 AtBodyM, Vector2 SizeM);

/// <summary>
/// One lens at one instant: the colour it is carrying and whether it is lit or dull. <b>Every lens is
/// drawn in every frame</b> — a lamp that is off is an unlit lens on the bodywork and not an absence,
/// which is the whole of why a parked ambulance no longer looks like one on a call.
/// </summary>
internal readonly record struct ShownLamp(CarLens Lens, CarLamp Colour, bool Lit);

/// <summary>Which lamp a lens is showing, which is what says what colour to draw it.</summary>
internal enum CarLamp : byte
{
    Indicator,
    Brake,
    Reverse,
    BeaconRed,
    BeaconBlue,
    BeaconAmber,
}

/// <summary>
/// A car's lamps (CAR-14): what each of them says, and which of its lenses are lit at one instant.
/// <b>Arithmetic over what the car already holds</b> — its command, its gear, its priority, the line in
/// front of it and whether the player has its wheel — so nothing here is state and nothing has to be
/// stepped.
/// </summary>
/// <remarks>
/// <para>
/// <b>The indicator answers a junction, and its side is read off the line</b> (CAR-14.1). What it is for
/// is telling the traffic at a junction which way out of it this car is taking, so it is asked only within
/// reach of one (<see cref="LampFigures.JunctionAheadM"/>) and only where the movement into it is a turn
/// rather than straight on (<see cref="CarFleet.TurningAtTheBox"/>). <b>Which</b> side is still the
/// geometry's and never the manoeuvre's: a car's intent is already written down as the line it is about to
/// drive, so no entry of the catalogue has to announce itself.
/// </para>
/// <para>
/// This is kept beside the body it is a fact about rather than in the renderer that draws it, on the
/// argument <see cref="TyreModel"/> is: where a lamp <em>is</em> is the car's — its variant's art, read
/// as <see cref="CarLens"/> (CAR-14a) — and what a lamp looks like is not.
/// </para>
/// </remarks>
internal static class CarLamps
{
    /// <summary>
    /// The most lenses one variant may draw — a rear pair, a front pair, a beacon bar's two ends and a
    /// second bar of four, which is the ambulance. The catalogue refuses a variant that draws more,
    /// since the buffer a frame is laid for is this many a car.
    /// </summary>
    public const int MostLenses = 10;

    /// <summary>The most quads one car can cost: every lens it has, and the glow under each lit one.</summary>
    public const int Most = MostLenses * 2;

    /// <summary>
    /// What this car's lamps say this tick. <b>A car nothing is driving and a wreck say nothing</b>: a
    /// lamp is something the car is doing (CAR-1), and a parked car standing on its handbrake is not
    /// holding a pedal down. A hand at its wheel is one of the things that drives it.
    /// </summary>
    /// <param name="handAtTheWheel">
    /// Whether the player has this car's wheel, which runs its beacon for as long as it is held
    /// (CTL-5c). It is passed in rather than read off the car because it is the interface's fact and
    /// not the car's, and a car whose art draws no beacon bar still shows nothing.
    /// </param>
    public static CarLampSet Showing(CarFleet cars, int car, SimConfig config, bool handAtTheWheel)
    {
        // A hand at the wheel is a driver (CTL-5c). Without this a car taken over from a stand shows
        // nothing at all — not the beacon and not its brake lamps — because standing down clears
        // <see cref="CarFleet.Driven"/> while its crew is still aboard.
        if (cars.Broken[car] || (!cars.Driven[car] && !handAtTheWheel)) return CarLampSet.None;

        var command = cars.Command[car];
        var set = CarLampSet.None;
        if (command.BrakeMps2 > config.Lamps.BrakeMps2) set |= CarLampSet.Brake;
        if (command.Reverse) set |= CarLampSet.Reverse;
        if (cars.BlueLight[car] || handAtTheWheel) set |= CarLampSet.Beacon;
        if (cars.AtWork[car]) set |= CarLampSet.Works;

        // CAR-14.1: an indicator answers a junction. A car with none in front of it, or one whose way
        // through the one in front is straight on, is announcing nothing — a constant-radius road is a road
        // and not a turn, and every car on one indicating its way round is the defect this gate exists for.
        if (!cars.TurningAtTheBox[car] || cars.ToTheBoxM[car] > config.Lamps.JunctionAheadM) return set;

        // Which side is still the line's own bend and never the wheel: on the approach the steering is
        // straight and the junction is thirty metres off. A reverse line is laid in the direction the rear
        // axle travels, which is the way the car is *not* pointing — the bend that takes the tail to the
        // line's left takes the nose to the body's right, and a lamp is bolted to the body.
        var turnRad = TurnAheadRad(cars.LineOf(car), cars.ProgressM[car], config.Lamps.TurnAheadM);
        if (cars.LineIsReverse[car]) turnRad = -turnRad;

        var indicateRad = config.Lamps.TurnDeg * MathF.PI / 180f;
        if (turnRad >= indicateRad) set |= CarLampSet.TurnRight;
        else if (turnRad <= -indicateRad) set |= CarLampSet.TurnLeft;

        return set;
    }

    /// <summary>
    /// How far the line bends over the next <paramref name="aheadM"/> metres of it, positive towards the
    /// car's own right. Summed along the arcs rather than differenced between two headings, because a way
    /// into a bay bends by half a turn and a difference of two angles cannot say which way round that was.
    /// </summary>
    public static float TurnAheadRad(ReadOnlySpan<ArcSeg> line, float progressM, float aheadM)
    {
        var turnRad = 0f;
        var startM = 0f;
        var lastM = progressM + aheadM;
        foreach (var arc in line)
        {
            if (startM >= lastM) break;

            var fromM = MathF.Max(startM, progressM);
            var toM = MathF.Min(startM + arc.LengthM, lastM);
            if (toM > fromM) turnRad += arc.Curvature * (toM - fromM);
            startM += arc.LengthM;
        }

        return turnRad;
    }

    /// <summary>
    /// This car's lenses at this instant: every one the variant draws, the colour it is carrying, and
    /// whether it is lit or dull. <b>A lens that is off is still a lens</b> — it is drawn dark, the way
    /// a signal head's unlit lamps are, so a lamp is a section of the car that changes rather than a
    /// glow that appears from nowhere.
    /// </summary>
    /// <remarks>
    /// <b>Each car flashes at its own rate</b>, its fuse jitter over the nominal one, which is what a
    /// flasher relay does and what keeps a queue of cars from blinking in unison. Nothing is stored: the
    /// phase is the town's clock, so a frame drawn twice draws the same lamps.
    /// </remarks>
    public static int Shown(
        ReadOnlySpan<CarLens> lenses, CarLampSet showing, SimConfig config, float elapsedS, float rateJitter,
        Span<ShownLamp> into)
    {
        var lamps = config.Lamps;
        var braking = (showing & CarLampSet.Brake) != 0;
        var rearOn = braking || (showing & CarLampSet.Reverse) != 0;
        var indicating = showing & (CarLampSet.TurnLeft | CarLampSet.TurnRight);
        var flashOn = indicating != CarLampSet.None
                      && Phase(elapsedS, lamps.FlashHz * rateJitter) < lamps.FlashOnShare;

        // The bar never goes dark while it is on: what flashes is which end is which — the colours of a
        // two-colour bar, and which end of a one-colour bar is burning — so the car is lit in every frame
        // it is in.
        var beaconOn = (showing & CarLampSet.Beacon) != 0;

        // And an amber bar is up for the job rather than for the priority (CAR-14.6), so it burns on legs
        // the town owes the truck nothing for. A hand at the wheel runs whichever bar the car has.
        var amberOn = beaconOn || (showing & CarLampSet.Works) != 0;
        var firstHalf = Phase(elapsedS, lamps.BeaconHz * rateJitter) < 0.5f;
        var swapped = beaconOn && firstHalf;

        var written = 0;
        foreach (var lens in lenses)
        {
            var onTheRight = lens.AtBodyM.Y > 0f;
            var (colour, lit) = lens.Fitting switch
            {
                // The pedal outranks the gear on the one cluster a body has: what the car behind has to
                // read first is that this one is stopping. Off, the cluster is its own red glass — a car
                // standing still is not showing a reversing lamp that happens to be dark.
                CarLampFitting.Rear => (
                    !braking && (showing & CarLampSet.Reverse) != 0 ? CarLamp.Reverse : CarLamp.Brake, rearOn),
                CarLampFitting.Indicator => (
                    CarLamp.Indicator,
                    flashOn && indicating == (onTheRight ? CarLampSet.TurnRight : CarLampSet.TurnLeft)),
                CarLampFitting.BeaconRed => (swapped ? CarLamp.BeaconBlue : CarLamp.BeaconRed, beaconOn),
                CarLampFitting.BeaconBlue => (swapped ? CarLamp.BeaconRed : CarLamp.BeaconBlue, beaconOn),

                // An amber bar has no second colour to swap to, so its ends take the burn in turns: each
                // one blinks between its own lit glass and its own dull glass, half a period apart.
                _ => (CarLamp.BeaconAmber, amberOn && firstHalf != onTheRight),
            };

            into[written++] = new ShownLamp(lens, colour, lit);
        }

        return written;
    }

    /// <summary>How far through its own period a flash of this rate is, in <c>[0, 1)</c>.</summary>
    static float Phase(float elapsedS, float rateHz)
    {
        var turns = elapsedS * rateHz;
        return turns - MathF.Floor(turns);
    }
}
