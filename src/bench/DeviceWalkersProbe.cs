using System.Diagnostics;
using System.Numerics;
using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;
using static TrafficSimulation.App.DeviceTick.DeviceWalkers;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The walkers' side of every claims rebuild on the device beside the host, on a running town</b>: once the town's
/// walkers are out, every rebuild of a stretch of it handed to the device a stage at a time as the town has it there —
/// stationed, bodies laid, walks planned and claimed, answers read — and the town's own share of each timed against the
/// device's, and held to one answer.
/// </summary>
/// <remarks>
/// <b>What the host's share leaves out</b>: the passes laid among the cars' bodies (<c>Sidestepping.Lay</c>), which
/// is a branch on the action for all but a few walkers. <b>What the device's leaves out</b>: sending the walkers across,
/// which a resident tick does not do, and laying the claims' lists, which are the claims' own and quoted apart.
/// </remarks>
internal static class DeviceWalkersProbe
{
    const int WarmupTicks = 1_200;
    const int MeasuredTicks = 600;

    /// <summary>How far a figure may land from the host's and still be one answer, as against one apart in its discrete outcome.</summary>
    const float WithinM = 1e-2f;

    /// <summary>
    /// One stage over the run: the host's time and the device's, how many walkers were asked, how they differ, and the first
    /// walker of each kind, figure by figure.
    /// </summary>
    sealed class Stage(string what)
    {
        public readonly List<double> HostMs = [];
        public readonly List<double> DeviceMs = [];
        public readonly Stopwatch Host = new();
        public int Asked;
        public int Differ;
        public int NotExact;
        public string? FirstDiffering;
        public string? FirstNotExact;

        public void Print()
        {
            Console.WriteLine(
                $"  {what,-24} host {Median(HostMs),7:F3} ms   device {Median(DeviceMs),7:F3} ms   {Differ} of {Asked} differ, {NotExact} not to the bit");
            if (FirstDiffering != null) Console.WriteLine($"    first differing: {FirstDiffering}");
            if (FirstNotExact != null) Console.WriteLine($"    first not to the bit: {FirstNotExact}");
        }
    }

    public static void Run(string map, SimConfig config)
    {
        Warmup.TheProcess(config);

        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        using var world = new TownWorld(plan, config);
        var loop = new SimLoop<TownWorld>(world, config);
        while (loop.Tick < WarmupTicks) loop.Advance();

        using var vk = Vk.Open("traffic-dotnet", validation: false);
        using var onDevice = new DeviceWalkers(vk, config, world);
        var station = new Stage("stationed");
        var bodies = new Stage("bodies laid");
        var walks = new Stage("walks planned, claimed");
        var reads = new Stage("answers read, aimed");
        var listsMs = new List<double>();
        var cover = new BodyPiece[WalkingGround.MostPiecesOfABody];
        world.WatchingTheClaims = (town, stage) =>
        {
            switch (stage)
            {
                case ClaimsStage.Begun:
                    onDevice.Lay(town, Piece.Station);
                    onDevice.Run(Piece.Station);
                    station.DeviceMs.Add(onDevice.RanMs);
                    station.Host.Restart();
                    break;

                case ClaimsStage.Stationed:
                    station.HostMs.Add(station.Host.Elapsed.TotalMilliseconds);
                    CompareTheStations(town, onDevice, station);
                    onDevice.Lay(town, Piece.Body);
                    onDevice.Run(Piece.Body);
                    bodies.DeviceMs.Add(onDevice.RanMs);
                    CompareTheBodies(town, onDevice, bodies, cover);
                    bodies.Host.Restart();
                    break;

                case ClaimsStage.WalkersLaid:
                    bodies.HostMs.Add(bodies.Host.Elapsed.TotalMilliseconds);
                    break;

                case ClaimsStage.WalksDue:
                    onDevice.Lay(town, Piece.Plan);
                    onDevice.LayTheClaims(town.Occupancy);
                    onDevice.Run(Piece.Plan);
                    walks.DeviceMs.Add(onDevice.RanMs);
                    listsMs.Add(onDevice.ListedMs);
                    walks.Host.Restart();
                    break;

                case ClaimsStage.PlansLaid:
                    walks.HostMs.Add(walks.Host.Elapsed.TotalMilliseconds);
                    CompareTheClaims(town, onDevice, walks);
                    break;

                case ClaimsStage.WalkersDue:
                    onDevice.Lay(town, Piece.Read);
                    onDevice.LayTheClaims(town.Occupancy);
                    onDevice.LayTheAnswers(town);
                    onDevice.Run(Piece.Read);
                    reads.DeviceMs.Add(onDevice.RanMs);
                    reads.Host.Restart();
                    break;

                case ClaimsStage.GrantsRead:
                    reads.HostMs.Add(reads.Host.Elapsed.TotalMilliseconds);
                    CompareTheReadings(town, onDevice, reads);
                    break;
            }
        };

        var until = loop.Tick + MeasuredTicks;
        while (loop.Tick < until) loop.Advance();
        world.WatchingTheClaims = null;

        var walking = 0;
        for (var person = 0; person < world.People.Count; person++) walking += world.People.Walking[person] ? 1 : 0;

        Stage[] stages = [station, bodies, walks, reads];
        Console.WriteLine($"device walkers probe — {map} on {vk.DeviceName}, {world.People.Count} walkers ({walking} walking) and {world.Cars.Count} cars after {WarmupTicks} ticks");
        Console.WriteLine($"{station.HostMs.Count} rebuilds; medians a rebuild, every walker one thread; a difference is a walker whose discrete outcome differs or a figure more than {WithinM} m off:");
        foreach (var stage in stages) stage.Print();
        Console.WriteLine($"  {"the walkers' share",-24} host {stages.Sum(stage => Median(stage.HostMs)),7:F3} ms   device {stages.Sum(stage => Median(stage.DeviceMs)),7:F3} ms");
        Console.WriteLine($"  the claims' body lists the plan and the read stand on, laid on the device: median {Median(listsMs):F3} ms (the claims' own, not counted above)");
    }

    static void CompareTheStations(TownWorld town, DeviceWalkers onDevice, Stage into)
    {
        var people = town.People;
        for (var person = 0; person < people.Count; person++)
        {
            into.Asked++;
            var row = onDevice.Walkers[person];
            var discrete = row.OnWay == people.OnWay[person] && row.RouteTaken == people.RouteTaken[person]
                           && row.Is(WalkerRow.Walking) == people.Walking[person] && row.Action == (uint)people.Action[person]
                           && row.PassAsideM == people.Pass[person].AsideM;
            var came = Tally(
                into, discrete, Apart(people.OnWayM[person], row.OnWayM), Apart(people.OffWayM[person], row.OffWayM),
                Apart(people.DestinationM[person], row.DestinationM), Apart(town.WalkingBack.BackToM[person], row.BackToM));
            if (!IsTheFirst(into, came)) continue;

            Describe(
                into, came,
                $"walker {person} {people.Action[person]} on way {people.OnWay[person]}: host at {people.OnWayM[person]:R} off " +
                $"{people.OffWayM[person]:R} aiming {people.DestinationM[person]:R} back to {town.WalkingBack.BackToM[person]:R}; device " +
                $"way {row.OnWay} at {row.OnWayM:R} off {row.OffWayM:R} aiming {row.DestinationM:R} back to {row.BackToM:R}");
        }
    }

    static void CompareTheBodies(TownWorld town, DeviceWalkers onDevice, Stage into, BodyPiece[] cover)
    {
        for (var person = 0; person < town.People.Count; person++)
        {
            if (town.People.Inside[person].Any) continue;

            into.Asked++;
            var laid = onDevice.BodyOf(person);
            var count = town.WalkingGround.BodyCover(person, cover);
            var occupant = town.GroundHeldAs(person, out _);
            var discrete = laid.Length == count && onDevice.Walkers[person].HeldAs == occupant;
            var apartM = 0f;
            for (var at = 0; discrete && at < count; at++)
            {
                var piece = cover[at];
                discrete = laid[at].Way == (uint)piece.Way && laid[at].FromM == piece.FromM && laid[at].ToM == piece.ToM
                           && laid[at].Onward == piece.Onward;
                apartM = MathF.Max(apartM, Apart(piece.AlongMps, laid[at].AlongMps));
            }

            Tally(into, discrete, apartM);
        }
    }

    static void CompareTheClaims(TownWorld town, DeviceWalkers onDevice, Stage into)
    {
        var claims = town.Occupancy;
        for (var person = 0; person < town.People.Count; person++)
        {
            var hold = town.WalkHold(person);
            if (hold == LaneOccupancy.NoHold && !onDevice.LaidAHold(person, out _)) continue;

            into.Asked++;
            if (hold == LaneOccupancy.NoHold || !onDevice.LaidAHold(person, out _))
            {
                Tally(into, false);
                continue;
            }

            var (first, end) = claims.PiecesOf(hold);
            var rows = onDevice.ProposedOf(person);
            var discrete = rows.Length == end - first;
            var apartM = 0f;
            for (var at = 0; discrete && at < rows.Length; at++)
            {
                ref readonly var slot = ref claims.Slots[first + at];
                discrete = rows[at].Way == (uint)claims.SlotWays[first + at] && rows[at].FromM == slot.FromM && rows[at].ToM == slot.ToM
                           && rows[at].LineFromM == slot.LineFromM && rows[at].AheadM == slot.AheadM;
                apartM = MathF.Max(apartM, Apart(slot.AlongMps, rows[at].AlongMps));
            }

            Tally(into, discrete, apartM);
        }
    }

    static void CompareTheReadings(TownWorld town, DeviceWalkers onDevice, Stage into)
    {
        var people = town.People;
        for (var person = 0; person < people.Count; person++)
        {
            into.Asked++;
            var row = onDevice.Walkers[person];
            var discrete = row.GrantM == people.GrantM[person] && row.OnCrossing == people.OnCrossing[person]
                           && row.Action == (uint)people.Action[person] && row.PassAsideM == people.Pass[person].AsideM
                           && row.Is(WalkerRow.PassBegun) == people.Pass[person].Begun;
            var came = Tally(into, discrete, Apart(people.DestinationM[person], row.DestinationM));
            if (!IsTheFirst(into, came)) continue;

            Describe(
                into, came,
                $"walker {person} {people.Action[person]}: host granted {people.GrantM[person]:R} aiming {people.DestinationM[person]:R}; " +
                $"device granted {row.GrantM:R} {(PersonAction)row.Action} aiming {row.DestinationM:R}");
        }
    }

    const int Alike = 0;
    const int NotExact = 1;
    const int Differs = 2;

    /// <returns>How the walker came out: <see cref="Alike"/>, <see cref="NotExact"/> or <see cref="Differs"/>.</returns>
    static int Tally(Stage into, bool discrete, params ReadOnlySpan<float> apartM)
    {
        var most = 0f;
        foreach (var apart in apartM) most = MathF.Max(most, apart);
        if (!discrete || most > WithinM)
        {
            into.Differ++;
            return Differs;
        }

        if (most == 0f) return Alike;

        into.NotExact++;
        return NotExact;
    }

    /// <summary>Whether a walker that came out so is the first of its kind, and so wants describing.</summary>
    static bool IsTheFirst(Stage of, int came) =>
        (came == Differs && of.FirstDiffering == null) || (came == NotExact && of.FirstNotExact == null);

    static void Describe(Stage of, int came, string walker)
    {
        if (came == Differs) of.FirstDiffering = walker;
        else of.FirstNotExact = walker;
    }

    static float Apart(float host, float onDevice) => host == onDevice ? 0f : MathF.Abs(host - onDevice);

    static float Apart(Vector2 host, Vector2 onDevice) => MathF.Max(Apart(host.X, onDevice.X), Apart(host.Y, onDevice.Y));

    static double Median(List<double> values)
    {
        var sorted = values.Order().ToArray();
        return sorted.Length == 0 ? 0d : sorted[sorted.Length / 2];
    }
}
