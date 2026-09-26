using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>What cut the grants the town's drivers were held by, and on what ground.</summary>
internal static class GrantProbe
{
    public static void Run(string map, SimConfig config)
    {
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        var seed = Environment.GetEnvironmentVariable("GRANT_SEED");
        using var world = new TownWorld(plan, config, agentSeed: seed is null ? null : ulong.Parse(seed));
        var loop = new SimLoop<TownWorld>(world, config);
        loop.Advance(600);

        var cars = world.Cars;
        var reasons = new Dictionary<string, int>(StringComparer.Ordinal);
        var samples = 0;
        var was = new string[cars.Count];
        var broken = new bool[cars.Count];
        for (var car = 0; car < cars.Count; car++) broken[car] = cars.Broken[car];
        var wrecks = 0;
        const int Kept = 300;
        var history = new (float Mps, float OffM, DrivingHold Hold, float HeadwayM, HeadwayKind Ahead, float GrantM, HeadwayKind Cut)[cars.Count, Kept];
        var story = new string[cars.Count, Kept];
        var boxStories = Environment.GetEnvironmentVariable("GRANT_STORY") == "1";
        for (var tick = 0; tick < 3600; tick++)
        {
            for (var car = 0; car < cars.Count; car++)
            {
                history[car, tick % Kept] = (cars.AlongMps[car], cars.OffLineM[car], cars.Hold[car], cars.Context[car].HeadwayM,
                    cars.Context[car].Ahead, cars.AuthorityM[car], cars.GrantCutBy[car]);
            }

            for (var car = 0; car < cars.Count; car++)
            {
                var hold = world.DriveHold(car);
                world.Occupancy.HoldEndsAtM(hold, out _, out var by);
                var way = world.Occupancy.HoldCutOn(hold);
                was[car] =
                    $"car {car} at ({cars.PositionM[car].X:F1},{cars.PositionM[car].Y:F1}) heading {cars.HeadingRad[car] * 180f / MathF.PI:F0} lane {cars.LaneOf(car)} off {cars.OffLineM[car]:F2} driven {cars.Driven[car]} {cars.AlongMps[car]:F1} m/s " +
                    $"hold {cars.Hold[car]} grant {cars.AuthorityM[car]:F1} cut by {cars.GrantCutBy[car]} " +
                    $"[{(by.Found ? (by.HasBody ? "body" : by.Linked ? "marked" : "plan") + $" {by.Of} {by.Occupant} {by.Priority} {by.FromM:F1}-{by.ToM:F1} ahead {by.AheadM:F1} committedTo {by.CommittedToM:F1} held {by.Held}" : "-")} on {(way < 0 ? "-" : world.Ways.KindOf(way) + " " + way)}] movement {cars.MovementWay[car]} " +
                    $"box in {cars.ToTheBoxM[car]:F1} inside {cars.InsideTheBox[car]} committed {cars.CommittedToTheBox[car]} " +
                    $"plan {cars.ClaimFromM[car]:F1}-{cars.ClaimToM[car]:F1} committedTo {cars.CommittedToM[car]:F1} light {cars.LightAheadM[car]:F1} headway {cars.Context[car].HeadwayM:F1} {cars.Context[car].Ahead}";
                story[car, tick % Kept] = was[car];
            }

            var people = world.People;
            var wasDown = new bool[people.Count];
            var wayBefore = new int[people.Count];
            var atBefore = new System.Numerics.Vector2[people.Count];
            for (var person = 0; person < people.Count; person++)
            {
                wayBefore[person] = people.OnWay[person];
                atBefore[person] = people.PositionM[person];
            }

            var walkerWas = new string[people.Count];
            var routeWas = new int[people.Count][];
            var routeAtWas = new int[people.Count];
            for (var person = 0; person < people.Count; person++)
            {
                routeWas[person] = people.RouteOf(person).ToArray();
                routeAtWas[person] = people.RouteAt(person);
                wasDown[person] = people.Wounded[person];
                walkerWas[person] =
                    $"walker {person} at ({people.PositionM[person].X:F1},{people.PositionM[person].Y:F1}) way {people.OnWay[person]} " +
                    $"at {people.OnWayM[person]:F1} crossing {people.OnCrossing[person]} crossing way {people.OnCrossingWay[person]} grant {people.GrantM[person]:F2} " +
                    $"walking {people.Walking[person]} speed {people.VelocityMps[person].Length():F2} {WhatHeld(world, world.WalkHold(person))}";
            }

            loop.Advance();
            for (var person = 0; person < people.Count; person++)
            {
                if (wasDown[person] || !people.Wounded[person]) continue;

                Console.WriteLine($"knocked down at tick {tick}: {walkerWas[person]}");
                var walkWay = wayBefore[person];
                if (walkWay >= 0)
                {
                    var arcs = world.LineOfWay(walkWay, out var widthM);
                    var len = TrafficSimulation.Core.Geometry.Spline.TotalLengthM(arcs);
                    var along = TrafficSimulation.Core.Geometry.Spline.ProjectM(arcs, atBefore[person], len * 0.5f, len);
                    var foot = TrafficSimulation.Core.Geometry.Spline.SampleAt(arcs, along).PositionM;
                    Console.WriteLine($"    walk way {world.Ways.KindOf(walkWay)} {walkWay} length {len:F1} width {widthM:F2}: nearest {along:F1} m, {(foot - atBefore[person]).Length():F2} m off; starts {TrafficSimulation.Core.Geometry.Spline.SampleAt(arcs, 0f).PositionM} ends {TrafficSimulation.Core.Geometry.Spline.SampleAt(arcs, len).PositionM}; marks {string.Join(",", world.Atlas.Marks.Of(walkWay).ToArray().Select(m => $"{world.Ways.KindOf(m.OnWay)} {m.OnWay} [{m.MineFromM:F1},{m.MineToM:F1}]"))}");
                }

                var route = routeWas[person];
                var at = routeAtWas[person];
                for (var slot = Math.Max(0, at - 2); slot < Math.Min(route.Length, at + 4); slot++)
                {
                    var code = route[slot];
                    var townWay = code == int.MinValue || code == -1 ? -1 : code < 0 ? world.Ways.OfMitre(~code) : world.Ways.OfFootway(code);
                    if (townWay < 0)
                    {
                        Console.WriteLine($"    route[{slot}]{(slot == at ? "*" : " ")} code {code}: off the network");
                        continue;
                    }

                    var arcs = world.LineOfWay(townWay, out _);
                    var len = TrafficSimulation.Core.Geometry.Spline.TotalLengthM(arcs);
                    Console.WriteLine(
                        $"    route[{slot}]{(slot == at ? "*" : " ")} {world.Ways.KindOf(townWay)} {townWay} length {len:F1}: " +
                        $"{(len > 0f ? TrafficSimulation.Core.Geometry.Spline.SampleAt(arcs, 0f).PositionM : default)} -> {(len > 0f ? TrafficSimulation.Core.Geometry.Spline.SampleAt(arcs, len).PositionM : default)}; " +
                        $"marks {string.Join(",", world.Atlas.Marks.Of(townWay).ToArray().Select(m => $"{world.Ways.KindOf(m.OnWay)} {m.OnWay} [{m.MineFromM:F1},{m.MineToM:F1}]"))}");
                }

                for (var car = 0; car < cars.Count; car++)
                {
                    if ((cars.PositionM[car] - people.PositionM[person]).Length() > 6f) continue;

                    Console.WriteLine($"    near: {was[car]}");
                }
            }

            for (var car = 0; car < cars.Count; car++)
            {
                if (broken[car] || !cars.Broken[car]) continue;

                broken[car] = true;
                if (++wrecks > 40) continue;

                Console.WriteLine($"wreck at tick {tick}: {was[car]}");
                if (boxStories && cars.OffLineM[car] < 1.2f)
                {
                    for (var back = 240; back >= 0; back -= 4)
                    {
                        Console.WriteLine($"      story -{back,2}: {story[car, (tick - back + Kept * 100) % Kept]}");
                    }
                }

                for (var back = 89; back >= 0; back -= 6)
                {
                    var h = history[car, (tick - back + Kept * 100) % Kept];
                    Console.WriteLine($"      -{back,2}: {h.Mps,5:F1} m/s off {h.OffM,4:F2} {h.Hold,-9} headway {h.HeadwayM,6:F1} {h.Ahead,-11} grant {h.GrantM,6:F1} {h.Cut}");
                }
                for (var other = 0; other < cars.Count; other++)
                {
                    if (other == car || (cars.PositionM[other] - cars.PositionM[car]).Length() > 8f) continue;

                    Console.WriteLine($"    near: {was[other]}");
                }
            }

            for (var car = 0; car < cars.Count; car++)
            {
                if (!cars.Driven[car] || cars.Hold[car] != DrivingHold.Claimed) continue;

                samples++;
                var hold = world.DriveHold(car);
                world.Occupancy.HoldEndsAtM(hold, out _, out var by);
                var way = world.Occupancy.HoldCutOn(hold);
                var kind = way < 0 ? "none" : world.Ways.KindOf(way).ToString();
                var what = !by.Found ? "place" : by.HasBody ? (by.OnItsLine ? "queue body" : "loose body") : by.Linked ? "marked" : "plan";
                var where = cars.InsideTheBox[car] ? "in box" : float.IsFinite(cars.ToTheBoxM[car]) && cars.ToTheBoxM[car] < 30f ? "near box" : "lane";
                var key = $"{where,-9} {what,-11} {by.Of,-8} {(by.Found ? by.Priority.ToString() : ""),-13} on {kind}";
                reasons[key] = reasons.GetValueOrDefault(key) + 1;
            }
        }

        Console.WriteLine($"grant probe — {map}: {samples} car-ticks held by the grant");

        static string WhatHeld(TownWorld world, int hold)
        {
            var endsAtM = world.Occupancy.HoldEndsAtM(hold, out _, out var by);
            if (float.IsPositiveInfinity(endsAtM)) return "(whole)";

            var way = world.Occupancy.HoldCutOn(hold);
            return by.Found
                ? $"(cut by {(by.HasBody ? "body" : by.Linked ? "marked" : "plan")} {by.Of} {by.Occupant} {by.Priority} {by.FromM:F1}-{by.ToM:F1} on {(way < 0 ? "-" : world.Ways.KindOf(way) + " " + way)})"
                : $"(a place on {way})";
        }
        foreach (var (key, count) in reasons.OrderByDescending(entry => entry.Value).Take(30))
        {
            Console.WriteLine($"  {100.0 * count / samples,5:F1} %  {key}");
        }
    }
}
