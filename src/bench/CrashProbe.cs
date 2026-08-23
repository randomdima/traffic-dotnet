using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Physics;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.Bench;

/// <summary>One staged collision: what it was aimed at, and what the town did about it.</summary>
internal readonly record struct CrashRow(string Name, float EnergyKj, string Expected, string Read)
{
    public bool Passed => Expected == Read;
}

/// <summary>
/// The crash sandbox: nudges are harmless, fast hits kill and break, wrecks persist and
/// get pushed — each of them staged at a closing speed <b>computed backwards from the energy band it
/// is meant to land in</b>, so a case that passes for the wrong reason has nowhere to hide.
/// </summary>
/// <remarks>
/// <para>
/// These are the wiring, and not the arithmetic. The exhaustive part — every ordered pair of kinds
/// against every band — is unit-tested against <see cref="DamageResolver"/> with no solver in the room,
/// which is where VER-6 says the cheap exhaustive place is. What a staged case adds is everything
/// between the two: the filter that let the pair touch at all, the normal the closing speed is measured
/// along, the snapshot taken before the step, and the terminal state that comes out of it.
/// </para>
/// <para>
/// <b>Every row is its own world.</b> A case that left a wreck in the road for the next one would be a
/// case whose result depended on the order the table is printed in.
/// </para>
/// </remarks>
internal static class CrashProbe
{
    /// <summary>Long enough for the staged approach plus the tick the outcome lands on, at every speed used here.</summary>
    const int Ticks = 120;

    public static void Run(SimConfig config)
    {
        Console.WriteLine(
            $"crash probe — person shakes at {config.Damage.PersonShakeKj:F0} kJ and dies at {config.Damage.PersonFatalKj:F0} kJ; " +
            $"a car breaks at {config.Damage.CarWreckKj:F0} kJ");
        Console.WriteLine($"{"case",-28}{"kJ",8}  {"expected",-34}{"read",-34}{"",6}");

        var failed = 0;
        foreach (var row in Cases(config))
        {
            if (!row.Passed) failed++;
            Console.WriteLine($"{row.Name,-28}{row.EnergyKj,8:F1}  {row.Expected,-34}{row.Read,-34}{(row.Passed ? "ok" : "FAILED"),6}");
        }

        Console.WriteLine(failed == 0
            ? "Every band gives the outcome the energy and the tolerances give, and nothing else does."
            : $"{failed} staged case(s) did not read as the arithmetic says they must.");
    }

    /// <summary>Every staged case, run. The unit suite asserts on the same rows this prints.</summary>
    public static CrashRow[] Cases(SimConfig config) =>
    [
        CarIntoPerson(config, "car nudges person", config.Damage.PersonShakeKj * 0.5f),
        CarIntoPerson(config, "car shoves person", (config.Damage.PersonShakeKj + config.Damage.PersonFatalKj) * 0.5f),
        CarIntoPerson(config, "car kills person", config.Damage.PersonFatalKj * 1.5f),
        PersonIntoParkedCar(config),
        CarIntoCar(config, "cars nudge", config.Damage.CarWreckKj * 0.5f),
        CarIntoCar(config, "cars crash", config.Damage.CarWreckKj * 1.5f),
        CarIntoWall(config),
        PersonIntoPerson(config),
        PersonIntoWall(config),
        CarOverCorpse(config),
        CarIntoWreck(config),
        QueueRests(config),
        WreckIsPushed(config),
    ];

    /// <summary>The closing speed that lands a pair of these two masses in a given band.</summary>
    static float ClosingMps(float energyKj, float firstKg, float secondKg) =>
        MathF.Sqrt(2f * energyKj * 1000f / DamageResolver.ReducedMassKg(firstKg, secondKg));

    /// <summary>How much clear air to leave between two bodies: four tenths of a second of approach, whatever the speed.</summary>
    static float GapM(float closingMps) => MathF.Max(0.5f, closingMps * 0.4f);

    static void Advance(CrashSandbox rig, SimConfig config) => new SimLoop<CrashSandbox>(rig, config).Advance(Ticks);

    /// <summary>What the contact did, not what the body looks like afterwards — a stumble has worn off long before a case ends.</summary>
    static string PersonReads(CrashSandbox rig, int person) => rig.PersonOutcome[person] switch
    {
        DamageOutcome.Dead => "dead",
        DamageOutcome.Shaken => "shaken",
        _ => "intact",
    };

    static string CarReads(CrashSandbox rig, int car) => rig.Cars.Broken[car] ? "broken" : "intact";

    static CrashRow CarIntoPerson(SimConfig config, string name, float energyKj)
    {
        var closingMps = ClosingMps(energyKj, config.Car.MassKg, config.Person.MassKg);
        using var rig = new CrashSandbox(config);
        var car = rig.AddCar(Vector2.Zero, 0f);
        var person = rig.AddPerson(
            new Vector2(config.Car.LengthM * 0.5f + GapM(closingMps) + config.PersonDiameterM * 0.5f, 0f));

        rig.Launch(new BodyTag(BodyKind.Car, car), new Vector2(closingMps, 0f));
        Advance(rig, config);

        var expected = energyKj >= config.Damage.PersonFatalKj ? "dead"
            : energyKj >= config.Damage.PersonShakeKj ? "shaken" : "intact";
        return new CrashRow(name, energyKj, $"person {expected}, car intact", $"person {PersonReads(rig, person)}, car {CarReads(rig, car)}");
    }

    /// <summary>
    /// PER-12's second sentence, which is a rule about the arithmetic and not about blame: <b>only the
    /// closing speed and the two masses count, never who was moving.</b> The car stands still here and
    /// the person arrives at it, and the outcome is the one the same energy gives the other way round.
    /// </summary>
    static CrashRow PersonIntoParkedCar(SimConfig config)
    {
        var energyKj = config.Damage.PersonFatalKj * 1.5f;
        var closingMps = ClosingMps(energyKj, config.Car.MassKg, config.Person.MassKg);
        using var rig = new CrashSandbox(config);
        var car = rig.AddCar(Vector2.Zero, 0f);
        var person = rig.AddPerson(
            new Vector2(-(config.Car.LengthM * 0.5f + GapM(closingMps) + config.PersonDiameterM * 0.5f), 0f));

        rig.Launch(new BodyTag(BodyKind.Person, person), new Vector2(closingMps, 0f));
        Advance(rig, config);

        return new CrashRow(
            "person shoved into car", energyKj, "person dead, car intact",
            $"person {PersonReads(rig, person)}, car {CarReads(rig, car)}");
    }

    static CrashRow CarIntoCar(SimConfig config, string name, float energyKj)
    {
        var closingMps = ClosingMps(energyKj, config.Car.MassKg, config.Car.MassKg);
        using var rig = new CrashSandbox(config);
        var first = rig.AddCar(Vector2.Zero, 0f);
        var second = rig.AddCar(new Vector2(config.Car.LengthM + GapM(closingMps), 0f), MathF.PI);

        // Head on, half the closing speed each, so neither one is "the one that was moving".
        rig.Launch(new BodyTag(BodyKind.Car, first), new Vector2(closingMps * 0.5f, 0f));
        rig.Launch(new BodyTag(BodyKind.Car, second), new Vector2(-closingMps * 0.5f, 0f));
        Advance(rig, config);

        var expected = energyKj >= config.Damage.CarWreckKj ? "broken" : "intact";
        return new CrashRow(name, energyKj, $"{expected}, {expected}", $"{CarReads(rig, first)}, {CarReads(rig, second)}");
    }

    static CrashRow CarIntoWall(SimConfig config)
    {
        var energyKj = config.Damage.CarWreckKj * 1.5f;
        var closingMps = ClosingMps(energyKj, config.Car.MassKg, float.PositiveInfinity);
        using var rig = new CrashSandbox(config);
        var car = rig.AddCar(Vector2.Zero, 0f);
        var wallAtM = new Vector2(config.Car.LengthM * 0.5f + GapM(closingMps) + 1f, 0f);
        rig.AddWall(wallAtM, new Vector2(2f, 20f));

        rig.Launch(new BodyTag(BodyKind.Car, car), new Vector2(closingMps, 0f));
        Advance(rig, config);

        // PHY-2: the wall is not merely undamaged, it has not moved. Nothing in the world can move it.
        var moved = rig.Cars.PositionM[car].X > wallAtM.X - 1f - config.Car.LengthM * 0.5f + 0.25f;
        return new CrashRow(
            "car into wall", energyKj, "car broken, wall unmoved",
            $"car {CarReads(rig, car)}, wall {(moved ? "gave way" : "unmoved")}");
    }

    /// <summary>PHY-4a's first exemption: person against person is harmless <em>at any energy</em>, and this one is enormous.</summary>
    static CrashRow PersonIntoPerson(SimConfig config)
    {
        var energyKj = config.Damage.PersonFatalKj * 10f;
        var closingMps = ClosingMps(energyKj, config.Person.MassKg, config.Person.MassKg);
        using var rig = new CrashSandbox(config);
        var first = rig.AddPerson(Vector2.Zero);
        var second = rig.AddPerson(new Vector2(config.PersonDiameterM + GapM(closingMps), 0f));

        rig.Launch(new BodyTag(BodyKind.Person, first), new Vector2(closingMps * 0.5f, 0f));
        rig.Launch(new BodyTag(BodyKind.Person, second), new Vector2(-closingMps * 0.5f, 0f));
        Advance(rig, config);

        return new CrashRow(
            "people collide", energyKj, "intact, intact", $"{PersonReads(rig, first)}, {PersonReads(rig, second)}");
    }

    /// <summary>PHY-4a's second exemption: a person against static geometry is harmless at any energy.</summary>
    static CrashRow PersonIntoWall(SimConfig config)
    {
        var energyKj = config.Damage.PersonFatalKj * 10f;
        var closingMps = ClosingMps(energyKj, config.Person.MassKg, float.PositiveInfinity);
        using var rig = new CrashSandbox(config);
        var person = rig.AddPerson(Vector2.Zero);
        rig.AddWall(new Vector2(config.PersonDiameterM * 0.5f + GapM(closingMps) + 1f, 0f), new Vector2(2f, 20f));

        rig.Launch(new BodyTag(BodyKind.Person, person), new Vector2(closingMps, 0f));
        Advance(rig, config);

        return new CrashRow("person into wall", energyKj, "intact", PersonReads(rig, person));
    }

    /// <summary>PHY-5a: a car may drive over a dead person without breaking, at a speed that would break it on a live one.</summary>
    static CrashRow CarOverCorpse(SimConfig config)
    {
        var energyKj = config.Damage.CarWreckKj * 1.5f;
        var closingMps = ClosingMps(energyKj, config.Car.MassKg, config.Person.MassKg);
        using var rig = new CrashSandbox(config);
        var car = rig.AddCar(Vector2.Zero, 0f);
        var person = rig.AddPerson(
            new Vector2(config.Car.LengthM * 0.5f + GapM(closingMps) + config.PersonDiameterM * 0.5f, 0f));

        // Already dead when the car arrives, by the same route a contact would have taken it there.
        rig.Apply(new BodyTag(BodyKind.Person, person), DamageOutcome.Dead);
        rig.Launch(new BodyTag(BodyKind.Car, car), new Vector2(closingMps, 0f));
        Advance(rig, config);

        return new CrashRow(
            "car over corpse", energyKj, "corpse dead, car intact",
            $"corpse {PersonReads(rig, person)}, car {CarReads(rig, car)}");
    }

    /// <summary>PHY-5a again: a wreck cannot enter another terminal state, and contributes nothing to what hits it.</summary>
    static CrashRow CarIntoWreck(SimConfig config)
    {
        var energyKj = config.Damage.CarWreckKj * 1.5f;
        var closingMps = ClosingMps(energyKj, config.Car.MassKg, config.Car.MassKg);
        using var rig = new CrashSandbox(config);
        var car = rig.AddCar(Vector2.Zero, 0f);
        var wreck = rig.AddCar(new Vector2(config.Car.LengthM + GapM(closingMps), 0f), 0f);

        rig.Apply(new BodyTag(BodyKind.Car, wreck), DamageOutcome.Broken);
        rig.Launch(new BodyTag(BodyKind.Car, car), new Vector2(closingMps, 0f));
        Advance(rig, config);

        return new CrashRow(
            "car into wreck", energyKj, "wreck broken, car intact", $"wreck {CarReads(rig, wreck)}, car {CarReads(rig, car)}");
    }

    /// <summary>
    /// The rule that separates a queue from a massacre: damage is judged <b>once per touch</b>, on the
    /// tick the pair begin touching, and a pair resting against each other is never judged again.
    /// </summary>
    static CrashRow QueueRests(SimConfig config)
    {
        using var rig = new CrashSandbox(config);
        var first = rig.AddCar(Vector2.Zero, 0f);
        rig.AddCar(new Vector2(config.Car.LengthM + 0.5f, 0f), 0f);

        // Fast enough to still be rolling when it arrives: the ground takes 1.2 m/s² off a coasting car,
        // which is more than a half-metre nudge has in it. The case is about how often a resting pair is
        // judged, so what matters is that the two touch and then stay touching.
        rig.Launch(new BodyTag(BodyKind.Car, first), new Vector2(3f, 0f));
        new SimLoop<CrashSandbox>(rig, config).Advance(Ticks * 5);

        return new CrashRow("queue rests", 0f, "judged 1×", $"judged {rig.Judgements}×");
    }

    /// <summary>PHY-5/PHY-9: a wreck stays dynamic and is still pushed — with its wheels locked, so it skids.</summary>
    static CrashRow WreckIsPushed(SimConfig config)
    {
        const float PushMps = 6f;

        using var rig = new CrashSandbox(config);
        var car = rig.AddCar(Vector2.Zero, 0f);
        var wreck = rig.AddCar(new Vector2(config.Car.LengthM + GapM(PushMps), 0f), 0f);
        var stoodAtM = rig.Cars.PositionM[wreck];

        rig.Apply(new BodyTag(BodyKind.Car, wreck), DamageOutcome.Broken);
        rig.Launch(new BodyTag(BodyKind.Car, car), new Vector2(PushMps, 0f));
        Advance(rig, config);

        var shovedM = rig.Cars.PositionM[wreck].X - stoodAtM.X;
        return new CrashRow(
            "wreck is pushed", DamageResolver.EnergyKj(DamageResolver.ReducedMassKg(config.Car.MassKg, config.Car.MassKg), PushMps),
            "shoved along", shovedM > 0.1f ? "shoved along" : $"stood fast ({shovedM:F2} m)");
    }
}
