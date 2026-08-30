using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Screen;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The circle a car is asked to turn against the one it turns</b>, claimed of the skidpad
/// (<see cref="SkidpadPlan"/>): every look the fleet ships, on full left lock, under six pedals — and for
/// each of them the arc its own axles ask for beside the arc its own tyres describe.
/// </summary>
/// <remarks>
/// <para>
/// <b>The asking is <see cref="TurnCircle"/>'s and nothing here works it out again.</b> That is the same
/// arithmetic the layer draws on the ground (OBS-2j), so the ring in the picture and the figure in this
/// table are one claim about one car rather than two that have to be kept in step.
/// </para>
/// <para>
/// <b>The comparison itself is quoted and never gated.</b> A turn circle is a geometric fact only while the
/// tyres are keeping up with the wheel, and on the two heavier pedals every car on the pad is being asked
/// for more than its rubber holds — so how far it runs wide of its own axles is a reading about this
/// fleet's tyres rather than a bound anything should hold. Gating it would be tuning the pad until the
/// instrument could no longer report the thing it was laid to find. <b>What is gated is what a pad of cars
/// on one lock owes whatever the tyres do</b>: that each of them turns, that it turns the way its wheel is
/// pointed, and that it stays in its own square.
/// </para>
/// <para>
/// <b>A car is only sampled once its rack has arrived.</b> The wheel travels at the car's own rate
/// (CAR-3a), so the first second of every run is a car turning about a centre that is still moving, and a
/// mean taken over it would be a mean of the wind-on.
/// </para>
/// </remarks>
internal sealed class SkidpadWatch : ScenarioWatch
{
    const int EveryCarIsTurning = 0;
    const int TheWayRoundIsTheWheels = 1;
    const int NothingLeavesItsOwnSquare = 2;

    const int WhatThePedalCostsTheCircle = 0;
    const int WhatTheFleetTurns = 1;
    const int WhatTheGeometryIsWorth = 2;
    const int WhatTheGripAllowsInstead = 3;
    const int WhatWasASpinAndNotACircle = 4;
    const int WhatALighterPedalWillNotMove = 5;

    static readonly string[] TheClaims =
    [
        "every look turns on a whole pedal, in both gears",
        "every car goes round the way its wheel is turned",
        "nothing on the pad leaves the square it was given",
    ];

    static readonly string[] TheReadings =
    [
        "what the pedal costs the circle",
        "what the fleet turns",
        "what the geometry is worth before anything slides",
        "what the tyres could have held at that speed",
        "how much of it was a spin and not a circle",
        "what a lighter pedal will not move",
    ];

    /// <summary>
    /// <b>The gate is the whole pedal and the lighter rows are read rather than gated.</b> A car on full
    /// lock is four patches scrubbing, and whether a third of a given look's throttle is enough to push
    /// them round is a fact about that look's engine against its own tyres — the pad is here to report it,
    /// not to be tuned until every row moves. What every vehicle owes is that it gets round at all when
    /// it is asked for everything it has.
    /// </summary>
    static bool AWholePedal(int run) => MathF.Abs(SkidpadPlan.PedalOf(run)) >= 1f;

    /// <summary>
    /// <b>How far inside its own geometry a car has to be before it is not on a circle at all.</b> Four
    /// rolling wheels cannot describe an arc tighter than the one their axles cross at, so a body doing it
    /// is pivoting rather than turning and its radius is not a turn circle to be read as one. The margin is
    /// slack enough that a tyre's working creep does not read as a pirouette.
    /// </summary>
    const float InsideItsOwnGeometry = 0.9f;

    /// <summary>Below this a car is not going anywhere and its yaw is noise rather than a turn.</summary>
    const float RollingMps = 0.3f;

    const float TurningRadPerS = 0.05f;

    /// <summary>How much of its own lock the rack has to have wound on before the car is on a circle at all.</summary>
    const float OnItsStop = 0.98f;

    /// <summary>
    /// <b>How long a car is left to settle onto its circle before its arcs are counted.</b> Every one of
    /// them starts from rest, so the first seconds are a spiral: the wheel is still winding on, the speed
    /// is still building, and the radius a tyre will hold grows with the square of it. A mean taken from
    /// the standing start is a mean of the launch, and the circle the pad is read for is the one the car
    /// ends up on.
    /// </summary>
    const float SettlesInS = 15f;

    /// <summary>
    /// <b>The lateral acceleration under which the geometry has to be right.</b> Ackermann's construction
    /// is exact in the limit of no sliding and approximate everywhere else, so a run that only ever samples
    /// a car at its grip limit can never say whether the construction itself is sound. Every car crosses
    /// this window once — on its way up from rest to the circle its tyres will hold — and what it turns
    /// down there is the one figure that is a fact about the axles rather than about the rubber.
    /// </summary>
    const float AtACrawlMps2 = 0.5f;

    /// <summary>What one car has come to: the arcs it has described against the arcs it was asked for.</summary>
    struct Turned
    {
        public double RatioSum;

        /// <summary>
        /// The arcs themselves, to be read as their mean. <b>Never the tightest or the widest of them</b>:
        /// a car pulling away is a tenth of a second of enormous yaw at walking pace and another of none at
        /// all, and either end of that is a transient rather than the circle the car settles onto.
        /// </summary>
        public double TurnedSumM;

        public double AskedSumM;
        public double GripSumM;
        public double HeldSumM;
        public double SpeedSumMps;
        public double LateralSumMps2;

        /// <summary>Where the centre it is really turning about stood, ahead of the axle it is drawn on.</summary>
        public double CentreAheadSumM;

        public double FrontSlipSumRad;
        public int Samples;

        /// <summary>Of which this many were tighter than four rolling wheels could have described.</summary>
        public int InsideSamples;

        /// <summary>And the same ratio taken only while the car was too slow to be sliding.</summary>
        public double CrawlRatioSum;

        public int CrawlSamples;
        public int WrongWay;
        public float FurthestM;
    }

    readonly Turned[] _turned;
    readonly Vector2[] _stoodAtM;

    /// <summary>
    /// What each car's own tyres hold across the roll. <b>Not the nominal figure the throttle ceiling is
    /// worked out against</b> (<c>TownWorld.DriveCeilingMps2</c>), and the gap between the two is a thing
    /// this pad can price.
    /// </summary>
    readonly float[] _gripMps2;
    readonly float _tickS;
    readonly float _squareM;

    /// <summary>What a lateral acceleration is quoted in, since a circle is read in g and not in m/s².</summary>
    readonly float _gravityMps2;
    float _watchedS;

    public SkidpadWatch(SimConfig config, TownWorld world)
        : base(
            "the skidpad", "every look on full left lock, six pedals, the arc asked for against the arc turned",
            TheClaims, TheReadings)
    {
        _tickS = config.TickSeconds;
        _gravityMps2 = config.Tyre.StandardGravityMps2;
        _squareM = SkidpadPlan.PitchM * 0.5f;
        _turned = new Turned[world.Cars.Count];
        _stoodAtM = new Vector2[world.Cars.Count];
        _gripMps2 = new float[world.Cars.Count];
        for (var car = 0; car < world.Cars.Count; car++)
        {
            _stoodAtM[car] = world.Cars.PositionM[car];
            _gripMps2[car] = world.Cars.BuildOf(car).GripMps2;
        }
    }

    public override void Saw(TownWorld world)
    {
        _watchedS += _tickS;
        var settled = _watchedS >= SettlesInS;

        var cars = world.Cars;
        for (var car = 0; car < cars.Count && car < _turned.Length; car++)
        {
            ref var turned = ref _turned[car];
            var atM = cars.PositionM[car];
            turned.FurthestM = MathF.Max(turned.FurthestM, (atM - _stoodAtM[car]).Length());

            ref readonly var build = ref cars.BuildOf(car);
            var steerRad = cars.Command[car].SteerRad;
            if (MathF.Abs(steerRad) < build.MaxSteerRad * OnItsStop) continue;

            var headingRad = cars.HeadingRad[car];
            Heading.Frame(headingRad, out var forward, out var right);
            var velocityMps = cars.VelocityMps[car];
            var alongMps = Vector2.Dot(velocityMps, forward);
            var acrossMps = Vector2.Dot(velocityMps, right);
            var yawRadPerS = cars.YawRateRadPerS[car];
            if (MathF.Abs(alongMps) < RollingMps || MathF.Abs(yawRadPerS) < TurningRadPerS) continue;

            // Which way round a car goes is the gear times the lock, and nothing else: the centre stands on
            // the same side of a reversing car as of one going forward, and it is the body that runs the
            // other way about it.
            if (MathF.Sign(yawRadPerS) != MathF.Sign(alongMps) * MathF.Sign(steerRad))
            {
                // Counted only once the car is on its circle, so that a launch transient is not a verdict.
                if (settled) turned.WrongWay++;
                continue;
            }

            if (!TurnCircle.Of(build, atM, headingRad, steerRad, out var asked)) continue;

            // The rear axle's own arc, which is what a yaw rate answers: the body turns about a centre at
            // this radius, whichever way it is travelling round it.
            var turnedM = MathF.Abs(alongMps) / MathF.Abs(yawRadPerS);
            var lateralMps2 = MathF.Abs(alongMps * yawRadPerS);

            // The crawl is sampled from the first tick, because the whole of it happens before the settle.
            if (lateralMps2 <= AtACrawlMps2)
            {
                turned.CrawlRatioSum += turnedM / asked.RearAxleRadiusM;
                turned.CrawlSamples++;
            }

            // Where it has got to is watched from the first tick — the square is about where the body
            // stands — and the circle it settles onto is not counted until it has settled onto one.
            if (!settled) continue;

            // The centre of the turn, off the body's own motion: for a rigid body the point that is not
            // moving stands at (−across, along) / yaw in the car's own frame. Ackermann puts it square
            // abeam the rear axle, so what is quoted is how far forward of that it has actually gone.
            var centreAheadM = build.CentreAheadOfAxleM - (acrossMps / yawRadPerS);

            // And what the front patches are crossing, against where they are pointed.
            var frontAcrossMps = acrossMps + (yawRadPerS * (build.WheelbaseM - build.CentreAheadOfAxleM));
            var slipRad = Folded(MathF.Atan2(frontAcrossMps, alongMps) - steerRad);

            turned.RatioSum += turnedM / asked.RearAxleRadiusM;
            turned.TurnedSumM += turnedM;
            turned.AskedSumM += asked.RearAxleRadiusM;

            var gripM = alongMps * alongMps / build.GripMps2;
            turned.GripSumM += gripM;
            turned.HeldSumM += MathF.Max(asked.RearAxleRadiusM, gripM);
            turned.SpeedSumMps += MathF.Abs(alongMps);
            turned.LateralSumMps2 += lateralMps2;
            turned.CentreAheadSumM += centreAheadM;
            turned.FrontSlipSumRad += MathF.Abs(slipRad);
            if (turnedM < asked.RearAxleRadiusM * InsideItsOwnGeometry) turned.InsideSamples++;
            turned.Samples++;
        }
    }

    /// <summary>
    /// An angle between a wheel and the ground it crosses, folded into the quarter turn either side of the
    /// wheel's own plane. <b>A tyre plane is a line and not a direction</b>, so a wheel rolling backwards is
    /// at no slip rather than at half a turn of it.
    /// </summary>
    static float Folded(float rad)
    {
        while (rad > MathF.PI * 0.5f) rad -= MathF.PI;
        while (rad < MathF.PI * -0.5f) rad += MathF.PI;
        return rad;
    }

    public override ClaimVerdict Verdict(int claim) => claim switch
    {
        EveryCarIsTurning => OnAWholePedal(out var turning, out var of) && turning == of
            ? ClaimVerdict.Kept
            : ClaimVerdict.Waiting,

        TheWayRoundIsTheWheels when WrongWay() > 0 => ClaimVerdict.Broken,
        TheWayRoundIsTheWheels => TurningCars() > 0 ? ClaimVerdict.Kept : ClaimVerdict.Waiting,

        NothingLeavesItsOwnSquare when Furthest(out _) >= _squareM => ClaimVerdict.Broken,
        NothingLeavesItsOwnSquare => _watchedS > 0f ? ClaimVerdict.Kept : ClaimVerdict.Waiting,

        _ => ClaimVerdict.Waiting,
    };

    public override void Says(int claim, ref TextBuffer into)
    {
        switch (claim)
        {
            case EveryCarIsTurning:
                OnAWholePedal(out var turning, out var ofThem);
                into.Add(turning);
                into.Add(" of ");
                into.Add(ofThem);
                into.Add(" cars have described an arc on their own lock");

                // The square a missing one stands in is the whole of what such a figure is worth: one car
                // short is a look that will not turn on everything it has.
                var still = FirstStill(onAWholePedal: true);
                if (still < 0) break;

                into.Add(", none yet from the ");
                into.Add(LookOf(still));
                into.Add(" ");
                into.Add(SkidpadPlan.RunName(SkidpadPlan.RunOf(still)));
                break;

            case TheWayRoundIsTheWheels:
                into.Add(WrongWay());
                into.Add(" car(s) have gone round against their own wheel");
                break;

            case NothingLeavesItsOwnSquare:
                var furthestM = Furthest(out var wanderer);
                if (wanderer < 0)
                {
                    into.Add("nothing has moved off the spot it was put down on");
                    break;
                }

                into.Add(furthestM, "F1");
                into.Add(" m from where it was put down at furthest, by the ");
                into.Add(LookOf(wanderer));
                into.Add(", of ");
                into.Add(_squareM, "F0");
                into.Add(" m it has");
                break;
        }
    }

    public override void Reads(int reading, ref TextBuffer into)
    {
        switch (reading)
        {
            // <b>The pedal and not the row's own name</b>: four names is a line nothing can print, and the
            // share of the pedal is what the row actually is — signed, so the gear is in the figure too.
            case WhatThePedalCostsTheCircle:
                var any = false;
                for (var run = 0; run < SkidpadPlan.Runs.Length; run++)
                {
                    if (!MeanOfRun(run, out var mean)) continue;

                    if (any) into.Add(", ");
                    any = true;
                    into.Add(SkidpadPlan.PedalOf(run), "F1");
                    into.Add(" pedal ");
                    into.Add(mean, "F2");
                    into.Add("x");
                }

                if (!any) into.Add("nothing has turned a circle yet");
                break;

            case WhatTheFleetTurns:
                if (!TightestAndWidest(out var tightest, out var widest))
                {
                    into.Add("nothing has turned a circle yet");
                    break;
                }

                into.Add("tightest ");
                into.Add(TurnedM(tightest), "F1");
                into.Add(" m (");
                into.Add(LookOf(tightest));
                into.Add(", ");
                into.Add(SkidpadPlan.PedalOf(SkidpadPlan.RunOf(tightest)), "F1");
                into.Add(" pedal), widest ");
                into.Add(TurnedM(widest), "F1");
                into.Add(" m (");
                into.Add(LookOf(widest));
                into.Add(", ");
                into.Add(SkidpadPlan.PedalOf(SkidpadPlan.RunOf(widest)), "F1");
                into.Add(" pedal)");
                break;

            case WhatTheGeometryIsWorth:
                var crawled = 0;
                var crawlSum = 0d;
                for (var car = 0; car < _turned.Length; car++)
                {
                    if (_turned[car].CrawlSamples == 0) continue;

                    crawlSum += _turned[car].CrawlRatioSum / _turned[car].CrawlSamples;
                    crawled++;
                }

                if (crawled == 0)
                {
                    into.Add("nothing was ever slow enough for the geometry to be exact");
                    break;
                }

                into.Add((float)(crawlSum / crawled), "F2");
                into.Add("x the axles below ");
                into.Add(AtACrawlMps2 / _gravityMps2, "F2");
                into.Add(" g, over ");
                into.Add(crawled);
                into.Add(" of ");
                into.Add(_turned.Length);
                into.Add(" cars");
                break;

            case WhatTheGripAllowsInstead:
                if (!Figures(AnyRun, out var fleet))
                {
                    into.Add("nothing has turned a circle yet");
                    break;
                }

                into.Add(fleet.TimesAsked, "F2");
                into.Add("x the axles, but ");
                into.Add(fleet.TimesHeld, "F2");
                into.Add("x the tightest circle the grip holds at ");
                into.Add(fleet.SpeedMps, "F1");
                into.Add(" m/s");
                break;

            case WhatWasASpinAndNotACircle:
                var spun = 0;
                var inside = 0L;
                var arcs = 0L;
                for (var car = 0; car < _turned.Length; car++)
                {
                    if (_turned[car].InsideSamples > 0) spun++;
                    inside += _turned[car].InsideSamples;
                    arcs += _turned[car].Samples;
                }

                if (arcs == 0)
                {
                    into.Add("nothing has turned a circle yet");
                    break;
                }

                into.Add(spun);
                into.Add(" of ");
                into.Add(_turned.Length);
                into.Add(" cars pivoted inside their own axles, over ");
                into.Add((float)(100d * inside / arcs), "F0");
                into.Add("% of the arcs measured");
                break;

            // A look whose engine cannot push its own tyres round on a part pedal, which is a fact about
            // that look and never a claim: the pad reports it and the gate above stays on the whole pedal.
            case WhatALighterPedalWillNotMove:
                var stalled = 0;
                for (var car = 0; car < _turned.Length; car++)
                {
                    if (_turned[car].Samples == 0 && !AWholePedal(SkidpadPlan.RunOf(car))) stalled++;
                }

                if (stalled == 0)
                {
                    into.Add("every look gets round on every pedal the pad stands");
                    break;
                }

                var first = FirstStill(onAWholePedal: false);
                into.Add(stalled);
                into.Add(" of the part-pedal squares never turned, the first the ");
                into.Add(LookOf(first));
                into.Add(" ");
                into.Add(SkidpadPlan.RunName(SkidpadPlan.RunOf(first)));
                break;
        }
    }

    /// <summary>Every car on the pad, rather than one row of it.</summary>
    public const int AnyRun = -1;

    /// <summary>
    /// What one row of the pad came to, as the mean over its cars of each car's own mean.
    /// <b>Averaged that way and not over the samples</b>: a car that turned twice as many arcs as another
    /// is not twice the row, and the row is a fact about sixteen looks.
    /// </summary>
    public bool Figures(int run, out SkidpadFigures figures)
    {
        var cars = 0;
        double speed = 0, lateral = 0, asked = 0, grip = 0, held = 0, turnedM = 0, ahead = 0, slip = 0, crawl = 0;
        var crawled = 0;

        for (var car = 0; car < _turned.Length; car++)
        {
            if (run != AnyRun && SkidpadPlan.RunOf(car) != run) continue;

            ref readonly var one = ref _turned[car];
            if (one.CrawlSamples > 0)
            {
                crawl += one.CrawlRatioSum / one.CrawlSamples;
                crawled++;
            }

            if (one.Samples == 0) continue;

            var over = (double)one.Samples;
            speed += one.SpeedSumMps / over;
            lateral += one.LateralSumMps2 / over;
            asked += one.AskedSumM / over;
            grip += one.GripSumM / over;
            held += one.HeldSumM / over;
            turnedM += one.TurnedSumM / over;
            ahead += one.CentreAheadSumM / over;
            slip += one.FrontSlipSumRad / over;
            cars++;
        }

        if (cars == 0)
        {
            figures = default;
            return false;
        }

        var over2 = (double)cars;
        figures = new SkidpadFigures(
            cars,
            (float)(speed / over2),
            (float)(lateral / over2 / _gravityMps2),
            (float)(asked / over2),
            (float)(grip / over2),
            (float)(held / over2),
            (float)(turnedM / over2),
            (float)(ahead / over2),
            (float)(slip / over2 * (180f / MathF.PI)),
            crawled == 0 ? 0f : (float)(crawl / crawled));
        return true;
    }

    /// <summary>One car of the grid, in the same columns as a row of it.</summary>
    public bool FiguresOfCar(int car, out SkidpadFigures figures)
    {
        ref readonly var one = ref _turned[car];
        if (one.Samples == 0)
        {
            figures = default;
            return false;
        }

        var over = (double)one.Samples;
        figures = new SkidpadFigures(
            1,
            (float)(one.SpeedSumMps / over),
            (float)(one.LateralSumMps2 / over / _gravityMps2),
            (float)(one.AskedSumM / over),
            (float)(one.GripSumM / over),
            (float)(one.HeldSumM / over),
            (float)(one.TurnedSumM / over),
            (float)(one.CentreAheadSumM / over),
            (float)(one.FrontSlipSumRad / over * (180f / MathF.PI)),
            one.CrawlSamples == 0 ? 0f : (float)(one.CrawlRatioSum / one.CrawlSamples));
        return true;
    }

    /// <summary>Which look wears this square of the grid, for a caller printing the pad out.</summary>
    public string LookNameOf(int car) => LookOf(car);

    /// <summary>And what that look's own tyres hold across the roll.</summary>
    public float GripMps2Of(int car) => _gripMps2[car];

    /// <summary>Which look this car wears, which is what a row of the pad is named by (CAR-11).</summary>
    string LookOf(int car) => CarCatalog.Shared.Variants[SkidpadPlan.LookOf(car)].Id;

    int TurningCars()
    {
        var turning = 0;
        foreach (var turned in _turned)
        {
            if (turned.Samples > 0) turning++;
        }

        return turning;
    }

    /// <summary>How many of the cars on a whole pedal have described an arc, and how many of them there are.</summary>
    bool OnAWholePedal(out int turning, out int of)
    {
        turning = 0;
        of = 0;
        for (var car = 0; car < _turned.Length; car++)
        {
            if (!AWholePedal(SkidpadPlan.RunOf(car))) continue;

            of++;
            if (_turned[car].Samples > 0) turning++;
        }

        return of > 0;
    }

    /// <summary>The first car on a pedal of this weight that has not described an arc, or −1.</summary>
    int FirstStill(bool onAWholePedal)
    {
        for (var car = 0; car < _turned.Length; car++)
        {
            if (_turned[car].Samples == 0 && AWholePedal(SkidpadPlan.RunOf(car)) == onAWholePedal) return car;
        }

        return -1;
    }

    int WrongWay()
    {
        var against = 0;
        foreach (var turned in _turned)
        {
            if (turned.WrongWay > 0) against++;
        }

        return against;
    }

    /// <summary>
    /// The mean of one row: how much of the circle its axles asked for each of its cars actually turned,
    /// averaged over the row. One figure a pedal, which is the reading the pad exists to print.
    /// </summary>
    bool MeanOfRun(int run, out float mean)
    {
        var sum = 0d;
        var cars = 0;
        for (var car = 0; car < _turned.Length; car++)
        {
            if (SkidpadPlan.RunOf(car) != run || _turned[car].Samples == 0) continue;

            sum += _turned[car].RatioSum / _turned[car].Samples;
            cars++;
        }

        mean = cars == 0 ? 0f : (float)(sum / cars);
        return cars > 0;
    }

    /// <summary>The circle one car settled onto: the mean of every arc it was measured describing.</summary>
    float TurnedM(int car) => _turned[car].Samples == 0 ? 0f : (float)(_turned[car].TurnedSumM / _turned[car].Samples);

    /// <summary>
    /// The two ends of what the fleet turns, as places in the grid: the car that came round tightest and
    /// the one that ran widest, whichever pedal and whichever look each of them is.
    /// </summary>
    bool TightestAndWidest(out int tightest, out int widest)
    {
        tightest = -1;
        widest = -1;
        var tightestM = float.MaxValue;
        var widestM = 0f;
        for (var car = 0; car < _turned.Length; car++)
        {
            if (_turned[car].Samples == 0) continue;

            var turnedM = TurnedM(car);
            if (turnedM < tightestM)
            {
                tightestM = turnedM;
                tightest = car;
            }

            if (turnedM > widestM)
            {
                widestM = turnedM;
                widest = car;
            }
        }

        return tightest >= 0 && widest >= 0;
    }

    float Furthest(out int car)
    {
        car = -1;
        var furthestM = 0f;
        for (var at = 0; at < _turned.Length; at++)
        {
            if (_turned[at].FurthestM <= furthestM) continue;

            furthestM = _turned[at].FurthestM;
            car = at;
        }

        return furthestM;
    }
}
