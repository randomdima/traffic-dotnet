using TrafficSimulation.App.Screen;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>Every car anybody may be handed can drive the road</b>, claimed of the fleet lap
/// (<see cref="TrackLap.Fleet"/>): one of every look the fleet ships, on the circuit the shape table is
/// measured on, with nobody on foot anywhere on it.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the question the measured lap cannot answer.</b> That one stands six of the nominal car so a
/// difference between its rows is a difference about drive layout (CAR-11a); this one varies everything a
/// variant states — 1050 kg to 4200, a 3.4 m hatchback to a 4.0 m pickup — and asks only whether each of
/// them drove the lap, stayed on it, and got itself away from what stopped it.
/// </para>
/// <para>
/// <b>What each look is worth is quoted and never gated.</b> A lap whose cars differ in every figure is a
/// table to read rather than a bound to hold: the armoured car pulls at a fifth of what the sports car
/// does, which is what its own file asks for.
/// </para>
/// </remarks>
internal sealed class FleetWatch : LapWatch
{
    const int EveryLookDrivesIt = 0;
    const int EveryLookStaysOnIt = 1;
    const int EveryLookGetsItselfMoving = 2;
    const int NothingIsWreckedOrGivenUp = 3;
    const int TheSpreadIsTheFleetsOwn = 4;

    const int WhatEachLookIsWorth = 0;
    const int LapsDriven = 1;

    /// <summary>
    /// How much harder the hardest-pulling look has to get away than the softest. Half again is well inside
    /// the spread the fleet's own files state — a factor of five between the supercar's pedal and the
    /// armoured car's — and well outside anything two runs of the same body would differ by.
    /// </summary>
    const float TimesTheSoftest = 1.5f;

    /// <summary>How few passes a look may be quoted on before its row is one car's moment rather than its lap.</summary>
    const int WorthQuoting = 3;

    /// <summary>
    /// The floor under <em>driving</em>: below this a car is being carried round the lap rather than
    /// getting itself out of the corners. It is a fraction of the nominal car's own pedal, low enough that
    /// the 4.2 t armoured car with a third of that pedal still clears it and high enough that a car whose
    /// throttle never arrived does not.
    /// </summary>
    const float OfTheNominalPedal = 0.15f;

    static readonly string[] TheClaims =
    [
        "every look gets its passes over the lap",
        "every look stays on the line the lap offered it",
        "every look gets itself moving rather than crawling",
        "nothing is wrecked and no lap is given up on",
        "a look that pulls harder is one built to pull harder",
    ];

    static readonly string[] TheReadings =
    [
        "what each look is worth",
        "laps driven",
    ];

    readonly float _crawlingMps2;
    readonly float _offLineAllowedM;

    public FleetWatch(SimConfig config, TownWorld world)
        : base(
            config, world, "the fleet lap", "one of every look the fleet ships, on the one lap, nobody on foot",
            TheClaims, TheReadings)
    {
        _crawlingMps2 = config.CarAccelerationMps2 * OfTheNominalPedal;
        _offLineAllowedM = config.CarOffPathM * 2f;
    }

    public override ClaimVerdict Verdict(int claim) => claim switch
    {
        EveryLookDrivesIt => Quotable() ? ClaimVerdict.Kept : ClaimVerdict.Waiting,

        EveryLookStaysOnIt when !Quotable() => ClaimVerdict.Waiting,
        EveryLookStaysOnIt => WorstOffLine(out _) < _offLineAllowedM ? ClaimVerdict.Kept : ClaimVerdict.Broken,

        EveryLookGetsItselfMoving when !Quotable() => ClaimVerdict.Waiting,
        EveryLookGetsItselfMoving => SoftestPull(out _) > _crawlingMps2 ? ClaimVerdict.Kept : ClaimVerdict.Broken,

        NothingIsWreckedOrGivenUp when Metrics.Wrecked > 0 || Metrics.GivenUp > 0 => ClaimVerdict.Broken,
        NothingIsWreckedOrGivenUp => Metrics.WatchedS > 0f ? ClaimVerdict.Kept : ClaimVerdict.Waiting,

        // A lap on which the figure a variant states never reaches the road is a fleet of one car wearing
        // sixteen pictures (CAR-11), which every other claim here would keep.
        TheSpreadIsTheFleetsOwn when !Quotable() => ClaimVerdict.Waiting,
        TheSpreadIsTheFleetsOwn => Spread(out _, out _) ? ClaimVerdict.Kept : ClaimVerdict.Broken,

        _ => ClaimVerdict.Waiting,
    };

    public override void Says(int claim, ref TextBuffer into)
    {
        switch (claim)
        {
            case EveryLookDrivesIt:
                into.Add(Quoted());
                into.Add(" of ");
                into.Add(Metrics.Cars);
                into.Add(" looks have the ");
                into.Add(WorthQuoting);
                into.Add(" passes a row is worth quoting on");
                break;

            case EveryLookStaysOnIt:
                var wanderedM = WorstOffLine(out var wandered);
                if (wandered < 0)
                {
                    into.Add("no look has been round the lap yet");
                    break;
                }

                into.Add(wanderedM, "F2");
                into.Add(" m off the line at worst, by the ");
                into.Add(Metrics.LookOf(wandered));
                into.Add(", of ");
                into.Add(_offLineAllowedM, "F2");
                into.Add(" m allowed");
                break;

            case EveryLookGetsItselfMoving:
                var softestMps2 = SoftestPull(out var softest);
                if (softest < 0)
                {
                    into.Add("nothing has pulled away yet");
                    break;
                }

                into.Add(softestMps2, "F2");
                into.Add(" m/s2 at softest, by the ");
                into.Add(Metrics.LookOf(softest));
                into.Add(", over the ");
                into.Add(_crawlingMps2, "F2");
                into.Add(" this lap calls crawling");
                break;

            case NothingIsWreckedOrGivenUp:
                into.Add(Metrics.Wrecked);
                into.Add(" of ");
                into.Add(Metrics.Cars);
                into.Add(" wrecked, ");
                into.Add(Metrics.GivenUp);
                into.Add(" laps given up");
                break;

            case TheSpreadIsTheFleetsOwn:
                Spread(out var hardestMps2, out var softestPullMps2);
                into.Add(hardestMps2, "F2");
                into.Add(" m/s2 against ");
                into.Add(softestPullMps2, "F2");
                into.Add(", of the ");
                into.Add(TimesTheSoftest, "F1");
                into.Add(" times the fleet's own files ask for");
                break;
        }
    }

    public override void Reads(int reading, ref TextBuffer into)
    {
        switch (reading)
        {
            case WhatEachLookIsWorth:
                var hardest = HardestPull(out var hardestPullMps2);
                var softestMps2 = SoftestPull(out var softest);
                if (hardest < 0 || softest < 0)
                {
                    into.Add("nothing has pulled away yet");
                    break;
                }

                into.Add("hardest ");
                into.Add(Metrics.LookOf(hardest));
                into.Add(" at ");
                into.Add(hardestPullMps2, "F2");
                into.Add(" m/s2, softest ");
                into.Add(Metrics.LookOf(softest));
                into.Add(" at ");
                into.Add(softestMps2, "F2");
                break;

            case LapsDriven:
                into.Add(Metrics.Cars);
                into.Add(" cars over ");
                into.Add(Metrics.WatchedS, "F0");
                into.Add(" s, ");
                into.Add(Laps(out var fewest), "F1");
                into.Add(" laps at most and ");
                into.Add(fewest, "F1");
                into.Add(" at least");
                break;
        }
    }

    bool Quotable() => Metrics.Cars > 0 && Quoted() == Metrics.Cars;

    int Quoted()
    {
        var quoted = 0;
        for (var car = 0; car < Metrics.Cars; car++)
        {
            if (Metrics.FiguresOfCar(car).Passes >= WorthQuoting) quoted++;
        }

        return quoted;
    }

    /// <summary>
    /// Whether the hardest-pulling look gets away enough harder than the softest to be a fleet at all: the
    /// two ends of the spread, and whether they are as far apart as the fleet's own files state.
    /// </summary>
    bool Spread(out float hardestMps2, out float softestMps2)
    {
        HardestPull(out hardestMps2);
        softestMps2 = SoftestPull(out var softest);
        return softest >= 0 && hardestMps2 > softestMps2 * TimesTheSoftest;
    }

    /// <summary>The furthest any look ran off its line, and which look that was — or −1 with no lap watched yet.</summary>
    float WorstOffLine(out int car)
    {
        var worstM = -1f;
        car = -1;
        for (var at = 0; at < Metrics.Cars; at++)
        {
            var offLineM = Metrics.FiguresOfCar(at).OffLineM;
            if (offLineM <= worstM) continue;

            worstM = offLineM;
            car = at;
        }

        return MathF.Max(0f, worstM);
    }

    /// <summary>The hardest any of them ever got itself moving, which is the top of the spread the lap is read for.</summary>
    int HardestPull(out float mps2)
    {
        mps2 = -1f;
        var hardest = -1;
        for (var car = 0; car < Metrics.Cars; car++)
        {
            var pulled = Metrics.FiguresOfCar(car).PulledBestMps2;
            if (pulled <= mps2) continue;

            mps2 = pulled;
            hardest = car;
        }

        mps2 = MathF.Max(0f, mps2);
        return hardest;
    }

    /// <summary>And the softest, which is the one the claim about crawling is answered by.</summary>
    float SoftestPull(out int car)
    {
        var softestMps2 = float.MaxValue;
        car = -1;
        for (var at = 0; at < Metrics.Cars; at++)
        {
            var pulled = Metrics.FiguresOfCar(at).PulledBestMps2;
            if (pulled >= softestMps2) continue;

            softestMps2 = pulled;
            car = at;
        }

        return car < 0 ? 0f : softestMps2;
    }

    float Laps(out float fewest)
    {
        fewest = float.MaxValue;
        var most = 0f;
        for (var car = 0; car < Metrics.Cars; car++)
        {
            var laps = Metrics.Laps(car);
            fewest = MathF.Min(fewest, laps);
            most = MathF.Max(most, laps);
        }

        if (Metrics.Cars == 0) fewest = 0f;
        return most;
    }
}
