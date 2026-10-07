using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>A town's claims laid on the device and mirrored back, held to the same town laying its own</b>
/// (<see cref="DeviceTownClaims"/>): two of one map warmed alike, then run in step, one with the device laying its claims
/// and both ticking every agent and body themselves — every rebuild held slot by slot, hold by hold, car by car and walker
/// by walker, and every tick every car and walker where the other town has it.
/// </summary>
/// <remarks>
/// <b>Two towns that part are a mirror that is not the town's</b>: the town is the same code over the same state, so the
/// first figure that differs is the mirror's, and every one after it may be its consequence — what is quoted is where each
/// kind first differed.
/// </remarks>
internal static class DeviceMirrorProbe
{
    const int WarmupTicks = 600;
    const int MeasuredTicks = 300;

    public static void Run(string map, SimConfig config)
    {
        Warmup.TheProcess(config);
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        using var own = new TownWorld(plan, config);
        using var mirrored = new TownWorld(plan, config);
        var ownLoop = new SimLoop<TownWorld>(own, config);
        var mirroredLoop = new SimLoop<TownWorld>(mirrored, config);
        while (ownLoop.Tick < WarmupTicks) ownLoop.Advance();
        while (mirroredLoop.Tick < WarmupTicks) mirroredLoop.Advance();

        using var vk = Vk.Open("traffic-dotnet", validation: false);
        var cars = mirrored.Cars;
        var occupancy = mirrored.Occupancy;
        var builds = new CarBuild[CarCatalog.Shared.SheetCount];
        for (var variant = 0; variant < builds.Length; variant++) builds[variant] = cars.BuildOfVariant(variant);

        using var tyres = new DeviceTyres(vk, config, mirrored.WheelGrounds, builds, cars.Capacity);
        using var claims = new DeviceClaims(
            vk, occupancy.WayCount, occupancy.Capacity, occupancy.HoldCapacity, config.Road.MostSettlingPasses, cars.Capacity);
        using var carTick = new DeviceCarTick(
            vk, config, tyres, mirrored.Roads, cars.LineArcsOf(0).Length, mirrored.Ways.OfRoadConnector(0), claims.Cut);
        using var onDevice = new DeviceTownClaims(vk, config, mirrored, claims, carTick);
        mirrored.ClaimsDevice = onDevice;

        var (rebuilds, slots, holds, carsAsked, walkersAsked) = (0L, 0L, 0L, 0L, 0L);
        string? slotApart = null, holdApart = null, carApart = null, walkerApart = null, poseApart = null;
        for (var tick = 0; tick < MeasuredTicks; tick++)
        {
            ownLoop.Advance();
            mirroredLoop.Advance();
            poseApart ??= PosesApart(own, mirrored, tick);
            if (!own.ClaimsLaidThisTick) continue;

            rebuilds++;
            slots += own.Occupancy.SlotCount;
            holds += own.Occupancy.HoldCount;
            carsAsked += own.Cars.Count;
            walkersAsked += own.People.Count;
            slotApart ??= SlotsApart(own.Occupancy, mirrored.Occupancy, tick);
            holdApart ??= HoldsApart(own.Occupancy, mirrored.Occupancy, tick);
            carApart ??= CarsApart(own, mirrored, tick);
            walkerApart ??= WalkersApart(own, mirrored, tick);
        }

        mirrored.ClaimsDevice = null;
        var (here, byTheTown, onlyTheTown, tooMany) = onDevice.Rebuilds;
        var rebuild = onDevice.Rebuild;
        Console.WriteLine($"device mirror probe — {map} on {vk.DeviceName}, {WarmupTicks} ticks warmed, {MeasuredTicks} held in step");
        Console.WriteLine(
            $"  {rebuilds} rebuilds of {own.Cars.Count} cars and {own.People.Count} walkers: {here} laid on the device and {byTheTown} by the town " +
            $"({onlyTheTown} for what only the town answers, {tooMany} for too many cars on a pass)");
        Console.WriteLine(
            $"  what may not be mirrored exactly: {rebuild.OnOtherChannelsAtABay} cars at a bay put on other channels, " +
            $"{rebuild.StraightsTheTownWouldNotCommit} straights the device committed and the town would not");
        Console.WriteLine($"  {slots} slots: {slotApart ?? "none apart"}");
        Console.WriteLine($"  {holds} holds: {holdApart ?? "none apart"}");
        Console.WriteLine($"  {carsAsked} cars' claims: {carApart ?? "none apart"}");
        Console.WriteLine($"  {walkersAsked} walkers' walks: {walkerApart ?? "none apart"}");
        Console.WriteLine($"  {MeasuredTicks} ticks' poses: {poseApart ?? "none apart"}");
    }

    static string? SlotsApart(LaneOccupancy own, LaneOccupancy mirrored, int tick)
    {
        if (own.SlotCount != mirrored.SlotCount) return $"tick {tick}: {own.SlotCount} slots against {mirrored.SlotCount}";

        for (var slot = 0; slot < own.SlotCount; slot++)
        {
            var (a, b) = (DeviceClaims.ClaimRow.OfSlot(own, slot), DeviceClaims.ClaimRow.OfSlot(mirrored, slot));
            if (a != b) return $"tick {tick} slot {slot}: own {a}, mirrored {b}";
        }

        Span<int> ownList = stackalloc int[256];
        Span<int> mirroredList = stackalloc int[256];
        foreach (var way in own.OccupiedWays)
        {
            foreach (var bodies in (ReadOnlySpan<bool>)[true, false])
            {
                if (!ownList[..own.ListOf(way, bodies, ownList)].SequenceEqual(mirroredList[..mirrored.ListOf(way, bodies, mirroredList)]))
                {
                    return $"tick {tick} way {way}'s {(bodies ? "bodies" : "plans")} listed apart";
                }
            }
        }

        return null;
    }

    static string? HoldsApart(LaneOccupancy own, LaneOccupancy mirrored, int tick)
    {
        if (own.HoldCount != mirrored.HoldCount) return $"tick {tick}: {own.HoldCount} holds against {mirrored.HoldCount}";

        for (var hold = 0; hold < own.HoldCount; hold++)
        {
            var (a, b) = (HoldOf(own, hold), HoldOf(mirrored, hold));
            if (!a.Equals(b)) return $"tick {tick} hold {hold}: own {a}, mirrored {b}";
        }

        return null;

        static ((int, int), float, bool, HolderTerms, float, float, LaneClaim, int) HoldOf(LaneOccupancy claims, int hold)
        {
            var endsAtM = claims.HoldEndsAtM(hold, out var marginM, out var cutBy);
            return (claims.PiecesOf(hold), claims.StandingMarginM(hold), claims.IsAnswered(hold), claims.TermsOf(hold), endsAtM, marginM, cutBy,
                    claims.HoldCutOn(hold));
        }
    }

    static string? CarsApart(TownWorld own, TownWorld mirrored, int tick)
    {
        for (var car = 0; car < own.Cars.Count; car++)
        {
            var (a, b) = (ClaimOf(own, car), ClaimOf(mirrored, car));
            if (!a.Equals(b)) return $"tick {tick} car {car}: own {a}, mirrored {b}";
        }

        return null;

        static (int, float, float, float, float, float, float, float, float, float, byte) ClaimOf(TownWorld town, int car)
        {
            var cars = town.Cars;
            return (town.PlanHoldOf(car), cars.AuthorityM[car], cars.HorizonM[car], cars.ClaimFromM[car], cars.ClaimToM[car], cars.CommittedToM[car],
                    cars.GrantMarginM[car], (float)cars.GrantCutBy[car], cars.CoveredSinceClaimM[car], cars.BackRoomM[car], cars.Channels[car]);
        }
    }

    static string? WalkersApart(TownWorld own, TownWorld mirrored, int tick)
    {
        for (var person = 0; person < own.People.Count; person++)
        {
            var (a, b) = (WalkOf(own, person), WalkOf(mirrored, person));
            if (!a.Equals(b)) return $"tick {tick} walker {person}: own {a}, mirrored {b}";
        }

        return null;

        static (int, float, int, int, float, float, float, float, float, float) WalkOf(TownWorld town, int person)
        {
            var people = town.People;
            var ground = town.WalkingGround;
            return (town.WalkHold(person), people.GrantM[person], people.OnCrossing[person], (int)people.Action[person],
                    people.DestinationM[person].X, people.DestinationM[person].Y, ground.StraightFromM(person).X, ground.StraightFromM(person).Y,
                    ground.StraightToM(person).X, ground.StraightToM(person).Y);
        }
    }

    static string? PosesApart(TownWorld own, TownWorld mirrored, int tick)
    {
        for (var car = 0; car < own.Cars.Count; car++)
        {
            if (own.Cars.PositionM[car] != mirrored.Cars.PositionM[car] || own.Cars.VelocityMps[car] != mirrored.Cars.VelocityMps[car])
            {
                return $"tick {tick} car {car}: own at {own.Cars.PositionM[car]}, mirrored at {mirrored.Cars.PositionM[car]}";
            }
        }

        for (var person = 0; person < own.People.Count; person++)
        {
            if (own.People.PositionM[person] != mirrored.People.PositionM[person])
            {
                return $"tick {tick} walker {person}: own at {own.People.PositionM[person]}, mirrored at {mirrored.People.PositionM[person]}";
            }
        }

        return null;
    }
}
