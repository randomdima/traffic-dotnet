using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

internal static class WreckProbe
{
    public static void Run(string map, SimConfig config)
    {
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        using var world = new TownWorld(plan, config);
        var loop = new SimLoop<TownWorld>(world, config);
        loop.Advance(600);
        var cars = world.Cars;
        var broken = new bool[cars.Count];
        for (var car = 0; car < cars.Count; car++) broken[car] = cars.Broken[car];
        const int Kept = 90;
        var history = new (float Mps, float OffM, DrivingHold Hold, float HeadwayM, float GrantM)[cars.Count, Kept];
        long fast = 0, samples = 0, sliding = 0;
        for (var tick = 0; tick < 3600; tick++)
        {
            for (var car = 0; car < cars.Count; car++)
            {
                history[car, tick % Kept] = (cars.AlongMps[car], cars.OffLineM[car], cars.Hold[car], cars.Context[car].HeadwayM, cars.AuthorityM[car]);
                if (!cars.Driven[car]) continue;
                samples++;
                if (cars.AlongMps[car] > 25f) fast++;
                if (cars.OffLineM[car] > 1.5f) sliding++;
            }
            loop.Advance();
            for (var car = 0; car < cars.Count; car++)
            {
                if (broken[car] || !cars.Broken[car]) continue;
                broken[car] = true;
                Console.WriteLine($"wreck at tick {tick}: car {car} lane {cars.LaneOf(car)}");
                for (var back = Kept - 1; back >= 0; back -= 15)
                {
                    var h = history[car, (tick - back + Kept * 100) % Kept];
                    Console.WriteLine($"      -{back,2}: {h.Mps,5:F1} m/s off {h.OffM,4:F2} {h.Hold,-9} headway {h.HeadwayM,6:F1} grant {h.GrantM,6:F1}");
                }
            }
        }
        Console.WriteLine($"car-ticks above 25 m/s {100.0 * fast / samples:F2} %, more than 1.5 m off line {100.0 * sliding / samples:F3} %");
    }
}
