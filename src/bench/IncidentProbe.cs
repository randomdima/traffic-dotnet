using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>Wrecks, knockdowns, touches and distance over several agent seeds of one map.</summary>
internal static class IncidentProbe
{
    public static void Run(string map, SimConfig config)
    {
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        long wrecked = 0, down = 0, touches = 0, gaveUp = 0;
        double km = 0;
        for (var seed = 1UL; seed <= 6; seed++)
        {
            using var world = new TownWorld(plan, config, agentSeed: seed);
            var loop = new SimLoop<TownWorld>(world, config);
            loop.Advance(600);
            var cars = world.Cars;
            var people = world.People;
            var brokenBefore = 0;
            for (var car = 0; car < cars.Count; car++) if (cars.Broken[car]) brokenBefore++;
            var wasDown = new bool[people.Count];
            var touchesBefore = world.Touches;
            var gaveUpBefore = world.WalksGivenUp;
            var at = new System.Numerics.Vector2[cars.Count];
            for (var car = 0; car < cars.Count; car++) at[car] = cars.PositionM[car];
            double metres = 0;
            var downs = 0;
            var wasBroken = new bool[cars.Count];
            var was = new string[cars.Count];
            const int Kept = 60;
            var holds = new string[cars.Count, Kept];
            var sliding = new bool[cars.Count];
            for (var car = 0; car < cars.Count; car++) wasBroken[car] = cars.Broken[car];
            for (var tick = 0; tick < 3600; tick++)
            {
                for (var car = 0; car < cars.Count; car++)
                {
                    holds[car, tick % Kept] = $"{(cars.AlongMps[car] > 15f ? "fast" : cars.AlongMps[car] > 3f ? "moving" : "slow"),-7} {cars.Hold[car],-9} {(cars.InsideTheBox[car] ? "in box" : "road")}";
                    var off = cars.Driven[car] && !cars.Broken[car] && cars.OffLineM[car] > 0.5f;
                    if (off && !sliding[car])
                    {
                        var key = holds[car, (tick + 1) % Kept] ?? holds[car, tick % Kept];
                        Onsets[key] = Onsets.GetValueOrDefault(key) + 1;
                    }

                    sliding[car] = off;
                }

                for (var car = 0; car < cars.Count; car++)
                {
                    was[car] = $"{(cars.OffLineM[car] > 1.2f ? "sliding" : "on line"),-8} {(cars.InsideTheBox[car] ? "in box" : float.IsFinite(cars.ToTheBoxM[car]) && cars.ToTheBoxM[car] < 10f ? "at box" : "road"),-7} " +
                               $"{(cars.AlongMps[car] > 15f ? "fast" : cars.AlongMps[car] > 3f ? "moving" : "slow"),-7} {cars.Hold[car]}";
                }

                loop.Advance();
                for (var car = 0; car < cars.Count; car++)
                {
                    if (wasBroken[car] || !cars.Broken[car]) continue;

                    wasBroken[car] = true;
                    Classes[was[car]] = Classes.GetValueOrDefault(was[car]) + 1;
                }

                for (var car = 0; car < cars.Count; car++)
                {
                    metres += (cars.PositionM[car] - at[car]).Length();
                    at[car] = cars.PositionM[car];
                }

                for (var person = 0; person < people.Count; person++)
                {
                    if (people.Wounded[person] && !wasDown[person]) downs++;
                    wasDown[person] = people.Wounded[person];
                }
            }

            var brokenAfter = 0;
            for (var car = 0; car < cars.Count; car++) if (cars.Broken[car]) brokenAfter++;
            Console.WriteLine($"  seed {seed}: wrecked {brokenAfter - brokenBefore}, down {downs}, touches {world.Touches - touchesBefore}, walks given up {world.WalksGivenUp - gaveUpBefore}, {metres / 1000:F1} km");
            wrecked += brokenAfter - brokenBefore;
            down += downs;
            touches += world.Touches - touchesBefore;
            gaveUp += world.WalksGivenUp - gaveUpBefore;
            km += metres / 1000;
        }

        Console.WriteLine($"{map}: wrecked {wrecked}, down {down}, touches {touches}, walks given up {gaveUp}, {km:F1} km over 6 seeds");
        foreach (var (key, count) in Classes.OrderByDescending(entry => entry.Value)) Console.WriteLine($"  {count,4}  {key}");
        Console.WriteLine($"slides past 0.5 m: {Onsets.Values.Sum()}, by what held the car a second before");
        foreach (var (key, count) in Onsets.OrderByDescending(entry => entry.Value).Take(15)) Console.WriteLine($"  {count,4}  {key}");
    }

    static readonly Dictionary<string, int> Classes = new(StringComparer.Ordinal);
    static readonly Dictionary<string, int> Onsets = new(StringComparer.Ordinal);
}
