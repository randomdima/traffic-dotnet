using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.App.Screen;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// What the two proving-ground watches share: <b>the lap's own instrument, ticked once and read by
/// whichever of them is watching</b>, so a panel drawing the shape table and the claims about it are one
/// reading of one lap.
/// </summary>
internal abstract class LapWatch : ScenarioWatch
{
    protected LapWatch(
        SimConfig config, TownWorld world, string name, string subject, string[] claims, string[] readings)
        : base(name, subject, claims, readings) => Metrics = new TrackMetrics(config, world);

    /// <summary>The figures themselves, for the probe's table and for the panel that draws it.</summary>
    public TrackMetrics Metrics { get; }

    public override void Saw(TownWorld world) => Metrics.Saw(world);
}

/// <summary>
/// <b>What the proving ground was laid to show, as claims rather than as a table</b>: every shape driven
/// often enough to quote, each corner taken at what its radius affords, the tighter corner held slower,
/// the straight worth accelerating down, and the car on the road through all of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>It reads <see cref="TrackMetrics"/> and measures nothing itself</b>, which is what lets the panel,
/// <c>--bench track</c> and the town tier answer the same question off the same laps. What each figure is
/// and how it was taken is the instrument's own business.
/// </para>
/// <para>
/// <b>Relations and not figures.</b> Every number on that table comes out of the tyres, the profile and
/// the solver together, so a claim that pinned one of them would fail for a change to any of the three
/// without saying which. What is claimed here is what has to hold whatever those three do.
/// </para>
/// <para>
/// <b>The two laps differ in one claim.</b> Nobody may be knocked down on the pacing lap — a body there
/// steps into ground nobody has taken, and a knock is a car that could not stop for what was in front of
/// it. A body on <see cref="TrackLap.Drunk"/> does not ask before it lurches, so what happens to it is
/// quoted and never gated (<see cref="Scenario"/>).
/// </para>
/// </remarks>
internal sealed class TrackWatch : LapWatch
{
    const int EveryShapeDriven = 0;
    const int StayedOnTheLine = 1;
    const int CornersAffordTheirSpeed = 2;
    const int TighterIsHeldSlower = 3;
    const int TheStraightIsDrivenAsOne = 4;
    const int SlowingIsThePlannedBraking = 5;
    const int NobodyIsKnockedDown = 6;

    const int WhatItCostToGetRound = 0;
    const int LapsDriven = 1;
    const int WhoWasKnockedDown = 2;

    /// <summary>
    /// How few passes a shape may be quoted on. Under this the mean under it is one car's day rather than
    /// the road's, so the claims about it are not answered yet rather than answered thinly.
    /// </summary>
    const int WorthQuoting = 4;

    /// <summary>
    /// How near the corner formula a corner speed has to come. The shortfall is the reaction lead and the
    /// lookahead the corner is taken with, and it is largest where the corner is tightest.
    /// </summary>
    const float OfWhatTheRadiusAffords = 0.7f;

    /// <summary>
    /// And how far over it the fastest moment on a corner may be. Two things put it there: a corner is
    /// entered while the car is still on its way down to the speed it will be held at, and the lane the car
    /// drives is offset from the centreline the radius is stated at — so the outside of a 30 m corner is a
    /// 31.5 m one.
    /// </summary>
    const float OverWhatTheRadiusAffords = 1.1f;

    /// <summary>How near the planned rate a slowing has to come. What is left over is the rolling resistance, which the tyres spend outside their own budget.</summary>
    const float OfWhatItPlanned = 0.25f;

    /// <summary>What a straight is worth: the car reaches this much of the fastest corner's speed down one.</summary>
    /// <remarks>
    /// <b>A fifth over, and not double.</b> Doubling is what a car capped at 270 km/h did on a lap whose
    /// fastest corner it took at 111, and both of those figures were the same defect — a top speed nothing
    /// derived and a pedal authored past every tyre in the fleet (CAR-45). A road car governed at 144 km/h
    /// that holds its fastest corner at 96 cannot double it and never could; a claim that asked it to would
    /// be unsatisfiable rather than strict, since twice the corner is already past the cap.
    /// </remarks>
    const float OverTheFastestCorner = 1.2f;

    /// <summary>
    /// And how much of the gear's own cap it reaches on it — under this the straight is measuring the road
    /// rather than the car. <b>Four fifths</b>, which is what 500 m of straight is worth to a car that has to
    /// stop at the end of it; the arithmetic says a nominal car needs 214 m of the 500 to reach the whole cap
    /// and be stopped again, so what holds it to four fifths is the profile rather than the road.
    /// </summary>
    const float OfTheGearsOwnCap = 0.8f;

    /// <summary>Everything either lap can claim, by the constants above. Which of them a lap makes is <see cref="AsksOf"/>.</summary>
    static readonly string[] TheClaims =
    [
        "every shape is driven often enough to quote",
        "every car stays on the line its shape offered it",
        "a corner is taken at what its radius affords",
        "a tighter corner is held slower than a wider one",
        "the straight is accelerated down and braked for",
        "a slowing is the planned braking and nothing else",
        "nobody is knocked down while it is measured",
    ];

    static readonly string[] TheReadings =
    [
        "what getting round the lap cost",
        "laps driven",
        "who was put on the ground",
    ];

    /// <summary>Which claim each row of this lap's table is, and which reading — the pacing lap and the drunks' differ in both.</summary>
    readonly int[] _asks;

    readonly int[] _quotes;

    /// <summary>What the tyres afford sideways, which is the whole of what a corner's own speed is.</summary>
    readonly float _lateralMps2;

    /// <summary>And what the profile plans to slow at, which is the figure every reservation on the road is sized by.</summary>
    readonly float _plannedMps2;

    /// <summary>Past this the town stops calling a car crabbing across its line a car on it: twice a lane's own half-width.</summary>
    readonly float _offLineAllowedM;

    readonly float _capMps;

    public TrackWatch(SimConfig config, TownWorld world, TrackLap lap)
        : base(config, world, "the proving ground", Subjects(lap), Rows(TheClaims, AsksOf(lap)), Rows(TheReadings, QuotesOf(lap)))
    {
        _asks = AsksOf(lap);
        _quotes = QuotesOf(lap);

        // The lap's cars are the nominal one (CAR-11a), so the figures the claims are read against are its.
        var nominal = CarBuild.Nominal(config, config.Car.DrivenFrontShare);
        _lateralMps2 = config.TyreGripMps2 * config.Driving.GripMargin;
        _plannedMps2 = CarFollower.BrakingMps2(config, nominal, groundCoefficient: 1f);
        _offLineAllowedM = config.CarOffPathM * 2f;
        _capMps = config.Car.MaxSpeedMps;
    }

    static string Subjects(TrackLap lap) => lap == TrackLap.Drunk
        ? "one lap of five shapes with fifteen people reeling down it"
        : "one lap of five shapes with fifteen people pacing across it";

    /// <summary>
    /// <b>Which claims a lap makes.</b> The pacing lap makes all of them: it is the one the shapes are
    /// measured on, and what stops a car there is somebody who steps out at a car that can still stop.
    /// <para>
    /// <b>The drunks' lap claims one thing and quotes the rest</b>, which is what the map is for. A body
    /// reeling down the carriageway stops the field where it stands, so no shape gets the clean passes a
    /// mean is worth taking over — and the swerves, the back-offs and the laps given up that this leaves
    /// instead are the reading the lap was laid to produce. Claiming the shape figures here would be
    /// claiming the pacing lap's answer of a road that is not being driven.
    /// </para>
    /// </summary>
    static int[] AsksOf(TrackLap lap) => lap == TrackLap.Drunk
        ? [StayedOnTheLine]
        :
        [
            EveryShapeDriven, StayedOnTheLine, CornersAffordTheirSpeed, TighterIsHeldSlower,
            TheStraightIsDrivenAsOne, SlowingIsThePlannedBraking, NobodyIsKnockedDown,
        ];

    /// <summary>And which readings: the knock is a claim on the pacing lap and a reading on the drunks'.</summary>
    static int[] QuotesOf(TrackLap lap) => lap == TrackLap.Drunk
        ? [WhatItCostToGetRound, LapsDriven, WhoWasKnockedDown]
        : [WhatItCostToGetRound, LapsDriven];

    static string[] Rows(string[] all, int[] wanted) => Array.ConvertAll(wanted, row => all[row]);

    public override ClaimVerdict Verdict(int claim) => Answer(_asks[claim]);

    ClaimVerdict Answer(int claim) => claim switch
    {
        EveryShapeDriven => Quotable() ? ClaimVerdict.Kept : ClaimVerdict.Waiting,

        StayedOnTheLine when !AnyPass() => ClaimVerdict.Waiting,
        StayedOnTheLine => WorstOffLine(out _) < _offLineAllowedM ? ClaimVerdict.Kept : ClaimVerdict.Broken,

        CornersAffordTheirSpeed when !Quotable() => ClaimVerdict.Waiting,
        CornersAffordTheirSpeed => Afforded(out _, out _) ? ClaimVerdict.Kept : ClaimVerdict.Broken,

        TighterIsHeldSlower when !Quotable() => ClaimVerdict.Waiting,
        TighterIsHeldSlower => InOrder(out _, out _) ? ClaimVerdict.Kept : ClaimVerdict.Broken,

        TheStraightIsDrivenAsOne when !Quotable() => ClaimVerdict.Waiting,
        TheStraightIsDrivenAsOne => DrivenAsOne() ? ClaimVerdict.Kept : ClaimVerdict.Broken,

        SlowingIsThePlannedBraking when !Quotable() => ClaimVerdict.Waiting,
        SlowingIsThePlannedBraking => SlowedAsPlanned(out _, out _) ? ClaimVerdict.Kept : ClaimVerdict.Broken,

        NobodyIsKnockedDown when Metrics.Knocks > 0 => ClaimVerdict.Broken,
        NobodyIsKnockedDown => Metrics.WatchedS > 0f ? ClaimVerdict.Kept : ClaimVerdict.Waiting,

        _ => ClaimVerdict.Waiting,
    };

    public override void Says(int claim, ref TextBuffer into)
    {
        switch (_asks[claim])
        {
            case EveryShapeDriven:
                into.Add(Quoted());
                into.Add(" of ");
                into.Add(Metrics.Shapes);
                into.Add(" shapes have the ");
                into.Add(WorthQuoting);
                into.Add(" passes a mean is worth taking over");
                break;

            case StayedOnTheLine:
                var wanderedM = WorstOffLine(out var wandered);
                if (wandered < 0)
                {
                    into.Add("nothing has been round a shape yet");
                    break;
                }

                into.Add(wanderedM, "F2");
                into.Add(" m off the line at worst, on the ");
                into.Add(Named(wandered));
                into.Add(", of ");
                into.Add(_offLineAllowedM, "F2");
                into.Add(" m allowed");
                break;

            case CornersAffordTheirSpeed:
                Afforded(out var corner, out var share);
                if (corner < 0)
                {
                    into.Add("no corner has the passes to be quoted on yet");
                    break;
                }

                into.Add(share, "F2");
                into.Add(" of what the radius affords at worst, on the ");
                into.Add(Named(corner));
                into.Add(", of ");
                into.Add(OfWhatTheRadiusAffords, "F2");
                into.Add('-');
                into.Add(OverWhatTheRadiusAffords, "F2");
                break;

            case TighterIsHeldSlower:
                InOrder(out var tighter, out var wider);
                if (tighter < 0)
                {
                    into.Add("every corner is held under what a wider one is");
                    break;
                }

                into.Add("the ");
                into.Add(Named(tighter));
                into.Add(" is held at ");
                into.Add(Metrics.Figures(tighter).TopMps, "F1");
                into.Add(" m/s and the wider ");
                into.Add(Named(wider));
                into.Add(" at ");
                into.Add(Metrics.Figures(wider).TopMps, "F1");
                break;

            case TheStraightIsDrivenAsOne:
                var straight = Metrics.Figures(ShapeOn(TrackPlan.Straight));
                into.Add(straight.TopMps, "F1");
                into.Add(" m/s down it off a ");
                into.Add(FastestCornerMps(), "F1");
                into.Add(" m/s corner, ");
                into.Add(straight.Stops);
                into.Add(" stops at the end of it, cap ");
                into.Add(_capMps, "F0");
                break;

            case SlowingIsThePlannedBraking:
                SlowedAsPlanned(out var shape, out var slowedMps2);
                if (shape < 0)
                {
                    into.Add("nothing has braked for a shape often enough to be quoted on");
                    break;
                }

                into.Add(slowedMps2, "F2");
                into.Add(" m/s2 at worst, on the ");
                into.Add(Named(shape));
                into.Add(", against a planned ");
                into.Add(_plannedMps2, "F2");
                break;

            case NobodyIsKnockedDown:
                Knocks(ref into);
                break;
        }
    }

    public override void Reads(int reading, ref TextBuffer into)
    {
        switch (_quotes[reading])
        {
            case WhatItCostToGetRound:
                into.Add(Metrics.Swerves);
                into.Add(" swerves (E-4), ");
                into.Add(Metrics.BackOffs);
                into.Add(" back-offs (E-3), ");
                into.Add(Metrics.GivenUp);
                into.Add(" laps given up, ");
                into.Add(Metrics.Wrecked);
                into.Add(" wrecked");
                break;

            case LapsDriven:
                Laps(out var fewest, out var most, out var mean);
                into.Add(mean, "F1");
                into.Add(" a car on average, ");
                into.Add(fewest, "F1");
                into.Add(" the fewest and ");
                into.Add(most, "F1");
                into.Add(" the most, over ");
                into.Add(Metrics.WatchedS, "F0");
                into.Add(" s");
                break;

            case WhoWasKnockedDown:
                Knocks(ref into);
                break;
        }
    }

    void Knocks(ref TextBuffer into)
    {
        into.Add(Metrics.Knocks);
        into.Add(" of the ");
        into.Add(TrackPlan.Pacers);
        into.Add(" put on the ground");
        if (Metrics.Knocks == 0) return;

        var last = Metrics.LastKnock;
        into.Add(", last walker ");
        into.Add(last.Person);
        into.Add(" at ");
        into.Add(last.AtS, "F0");
        into.Add(" s");
    }

    /// <summary>Whether every shape has been round often enough for the means under it to be the road's answer.</summary>
    bool Quotable() => Quoted() == Metrics.Shapes;

    int Quoted()
    {
        var quotable = 0;
        for (var shape = 0; shape < Metrics.Shapes; shape++)
        {
            if (Metrics.Figures(shape).Passes >= WorthQuoting) quotable++;
        }

        return quotable;
    }

    bool AnyPass()
    {
        for (var shape = 0; shape < Metrics.Shapes; shape++)
        {
            if (Metrics.Figures(shape).Any) return true;
        }

        return false;
    }

    /// <summary>The furthest any car ran off the line it was offered, and which shape it did it on — or −1 while no shape has a pass.</summary>
    float WorstOffLine(out int shape)
    {
        var worstM = -1f;
        shape = -1;
        for (var at = 0; at < Metrics.Shapes; at++)
        {
            var figures = Metrics.Figures(at);
            if (!figures.Any || figures.OffLineM <= worstM) continue;

            worstM = figures.OffLineM;
            shape = at;
        }

        return MathF.Max(0f, worstM);
    }

    /// <summary>
    /// Whether every corner was taken at the speed its own radius affords — sqrt(lateral grip × radius),
    /// arrived at from under. The share furthest from that band comes back with it, which is what a broken
    /// claim has to name.
    /// </summary>
    bool Afforded(out int corner, out float share)
    {
        corner = -1;
        share = 0f;
        var kept = true;
        var worst = float.NegativeInfinity;
        for (var shape = 0; shape < Metrics.Shapes; shape++)
        {
            var section = Metrics.SectionOf(shape);
            var figures = Metrics.Figures(shape);
            if (section.RadiusM <= 0f || figures.Passes < WorthQuoting) continue;

            var of = figures.TopMps / MathF.Sqrt(_lateralMps2 * section.RadiusM);
            kept &= of >= OfWhatTheRadiusAffords && of <= OverWhatTheRadiusAffords;

            // Whichever end of the band it is furthest from — negative while it is inside — so the figure
            // quoted is the one at fault and, on a lap keeping the claim, the nearest one to breaking it.
            var off = MathF.Max(OfWhatTheRadiusAffords - of, of - OverWhatTheRadiusAffords);
            if (off <= worst) continue;

            worst = off;
            corner = shape;
            share = of;
        }

        return kept;
    }

    /// <summary>
    /// Whether the tighter of every pair of corners is held slower than the wider one, and every corner
    /// under the straight. <b>The whole of what the shapes are laid to show</b>, stated over the sections
    /// rather than one section at a time. A broken claim names the pair.
    /// </summary>
    bool InOrder(out int tighter, out int wider)
    {
        tighter = -1;
        wider = -1;
        for (var one = 0; one < Metrics.Shapes; one++)
        {
            for (var other = 0; other < Metrics.Shapes; other++)
            {
                var thisOne = Metrics.SectionOf(one);
                var thatOne = Metrics.SectionOf(other);

                // A straight is a corner of no radius at all, which is what puts it at the top of the order.
                var thisRadiusM = thisOne.RadiusM > 0f ? thisOne.RadiusM : float.MaxValue;
                var thatRadiusM = thatOne.RadiusM > 0f ? thatOne.RadiusM : float.MaxValue;
                if (thisRadiusM >= thatRadiusM) continue;
                if (Metrics.Figures(one).TopMps < Metrics.Figures(other).TopMps) continue;

                tighter = one;
                wider = other;
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// <b>The straight is worth accelerating down and braking for</b>, which is the difference between a
    /// car driving a road and one being carried round it: it is taken at more than twice the fastest
    /// corner, reaches the gear's own cap on it, and somebody was brought to rest at the end of it.
    /// </summary>
    bool DrivenAsOne()
    {
        var straight = Metrics.Figures(ShapeOn(TrackPlan.Straight));
        return straight.TopMps > FastestCornerMps() * OverTheFastestCorner
               && straight.TopMps >= _capMps * OfTheGearsOwnCap
               && straight.TopMps <= _capMps
               && straight.Stops > 0;
    }

    float FastestCornerMps()
    {
        var fastestMps = 0f;
        for (var shape = 0; shape < Metrics.Shapes; shape++)
        {
            if (Metrics.SectionOf(shape).RadiusM > 0f) fastestMps = MathF.Max(fastestMps, Metrics.Figures(shape).TopMps);
        }

        return fastestMps;
    }

    /// <summary>
    /// Whether every shape was braked for at the rate the profile plans. A car that slowed far harder held
    /// less street than it used, and one that slowed far softer held a street shut for ground it never
    /// needed.
    /// </summary>
    bool SlowedAsPlanned(out int shape, out float slowedMps2)
    {
        shape = -1;
        slowedMps2 = 0f;
        var kept = true;
        var worst = float.NegativeInfinity;
        for (var at = 0; at < Metrics.Shapes; at++)
        {
            var figures = Metrics.Figures(at);
            if (figures.Slowings < WorthQuoting) continue;

            kept &= figures.SlowedAtMps2 >= _plannedMps2 * (1f - OfWhatItPlanned)
                    && figures.SlowedAtMps2 <= _plannedMps2 * (1f + OfWhatItPlanned);

            var off = MathF.Abs(figures.SlowedAtMps2 - _plannedMps2);
            if (off <= worst) continue;

            worst = off;
            shape = at;
            slowedMps2 = figures.SlowedAtMps2;
        }

        return kept;
    }

    void Laps(out float fewest, out float most, out float mean)
    {
        fewest = float.MaxValue;
        most = 0f;
        var total = 0f;
        for (var car = 0; car < Metrics.Cars; car++)
        {
            var laps = Metrics.Laps(car);
            fewest = MathF.Min(fewest, laps);
            most = MathF.Max(most, laps);
            total += laps;
        }

        if (Metrics.Cars == 0) fewest = 0f;
        mean = Metrics.Cars == 0 ? 0f : total / Metrics.Cars;
    }

    /// <summary>Which watched shape a road of the lap is, so a claim about the straight can name it.</summary>
    int ShapeOn(int road)
    {
        for (var shape = 0; shape < Metrics.Shapes; shape++)
        {
            if (Metrics.SectionOf(shape).Road == road) return shape;
        }

        return 0;
    }

    string Named(int shape) => Metrics.SectionOf(shape).Name;
}
