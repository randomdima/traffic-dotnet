using System.Numerics;
using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;
using static TrafficSimulation.App.DeviceTick.DeviceWalkers;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>The walkers' side of every claims rebuild on the device, asked the town's own question</b>: at each stage of every
/// rebuild of the walked town, the walkers handed to the device as the town has them there, that stage run on the
/// device, and what it came to held to what the town made of the same walkers (<see cref="WalkersOnTheDevice"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Exact where the arithmetic is the host's</b>, and within the device's own trigonometry where a sine, cosine or
/// arctangent is taken: where a walker is found on a bent way and how far off it, a point sampled on one, and how fast a
/// walker goes the way it faces (<see cref="WalkersOnTheDevice.Near(float, float)"/>).
/// </para>
/// <para>
/// <b>A discrete outcome is exact everywhere</b>, and a walker stationed within a millimetre of a threshold a last place
/// of trigonometry could carry it across — walking a way out, standing on its way's ground, arriving — is left out of the
/// station's comparison rather than the check loosened. How many were is in the message.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class DeviceWalkersTests(WalkersOnTheDevice run) : IClassFixture<WalkersOnTheDevice>
{
    /// <summary>
    /// <b>Every walker is stationed where the town stations it</b>: on the same way of its route, on its own way's ground
    /// or not, walking or arrived, walking its route or back onto it, with the same pass — and found as far along its way,
    /// as far off it, aiming at and walking back to the same places, to the device's own trigonometry.
    /// </summary>
    [Fact]
    public void EveryWalkerIsStationedAsTheTownStationsIt() => Assert.True(run.Stations.Kept, run.Stations.Said);

    /// <summary>
    /// <b>Every walker's body is laid where the town lays it</b>: held as the same ground, over the same ways and the same
    /// stretches of each in the same order — its own way's off its place on it — travelling the same ones, onward the
    /// same, at rest or not, going as fast down them to the device's own cosine.
    /// </summary>
    [Fact]
    public void EveryWalkersBodyIsLaidAsTheTownLaysIt() => Assert.True(run.Bodies.Kept, run.Bodies.Said);

    /// <summary>
    /// <b>Every walker's pass lays the ground the town lays for it</b> — the runs it has not walked a stride past, as
    /// passes held as its own ground — and a walker no longer on the sidestep it decided walks again, its pass let go.
    /// </summary>
    [Fact]
    public void EveryPassIsLaidAsTheTownLaysIt() => Assert.True(run.Passes.Kept, run.Passes.Said);

    /// <summary>
    /// <b>Every walk is planned down the ways the town plans it down</b> — the same ways, stretches and line metres in the
    /// same order, down its route, along the hop off its end, back onto it or to a post — and planned for the same
    /// walkers; and a straight committed to is the same straight swept over the same ground.
    /// </summary>
    [Fact]
    public void EveryWalkIsPlannedDownTheWaysTheTownPlans() => Assert.True(run.Walks.Kept, run.Walks.Said);

    /// <summary>
    /// <b>Every walker's claim is laid as the town lays it</b>: a hold for the same walkers, answered with the plans or
    /// held where it stands as the town's is, kept off what cuts it by as much — and its pieces, main claims and the
    /// secondary claims their marks place, the same rows in the same order: ways, stretches, line metres, arrivals, rungs
    /// and how far the walker's own body already reaches, going as fast down them to the device's own cosine.
    /// </summary>
    [Fact]
    public void EveryWalkersClaimIsLaidAsTheTownLaysIt() => Assert.True(run.Claims.Kept, run.Claims.Said);

    /// <summary>
    /// <b>Every walker reads its answer as the town reads it</b>: granted as far, on or held off the same zebra, its pass
    /// begun, withdrawn or over where the town's is, walking the same action — and aimed at the same place, to the device's
    /// own trigonometry.
    /// </summary>
    [Fact]
    public void EveryWalkerReadsItsAnswerAsTheTownReadsIt() => Assert.True(run.Readings.Kept, run.Readings.Said);
}

/// <summary>
/// <b>The walked town run once with the walkers' side of every rebuild on the device beside it</b>, every stage held to
/// the town where the town comes to it, and what differed kept a stage at a time for the class to read.
/// </summary>
/// <remarks>
/// <b>Somebody is stood in the way of a few walkers, a few are shoved off the pavement and a few sent to a post</b>, once
/// the town's walkers are out of their doors: a town left to itself sidesteps nobody, walks few back and posts no officer
/// in two minutes, and what is not walked is not asked.
/// </remarks>
public sealed class WalkersOnTheDevice : IDisposable
{
    /// <summary>What was asked of one stage over the run, and what differed.</summary>
    internal sealed class Asked(string what)
    {
        public readonly List<string> Differ = [];

        public int Count;

        public string Also = string.Empty;

        public bool Kept => Differ.Count == 0;

        public string Said => $"{Differ.Count} of {Count} {what} differently{Also}: {string.Join("; ", Differ.Take(6))}";
    }

    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>How long the town is left before anybody is stood in anybody's way: its walkers out of their doors.</summary>
    static int WalkersOutTicks => (int)(Config.Building.DwellMaxS * Config.Sim.TickRateHz) + 120;

    /// <summary>Forty seconds of the walked town once it is staged: walkers set off, cross, wait, pass, hop and arrive.</summary>
    const int WatchedTicks = 2_400;

    /// <summary>How many walkers are stood in another's way, and how many shoved off the pavement.</summary>
    const int Staged = 6;

    /// <summary>How far in front of a walker the one stood in its way stands, and the room its way runs on past them.</summary>
    const float StandsAheadM = 3f;

    const float RoomPastM = 4f;

    /// <summary>How near a threshold a trigonometric last place could carry a walker's station across, in metres.</summary>
    const float ThresholdM = 1e-3f;

    readonly Lavapipe _device = new();

    internal Asked Stations { get; } = new("stationed");

    internal Asked Bodies { get; } = new("bodies laid");

    internal Asked Passes { get; } = new("passes laid");

    internal Asked Walks { get; } = new("walks planned");

    internal Asked Claims { get; } = new("claims laid");

    internal Asked Readings { get; } = new("answers read");

    public WalkersOnTheDevice()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WalkersOutTicks);
        StandSomebodyInTheWay(world);
        ShoveOffThePavement(world);
        SendToAPost(world);

        using var onDevice = new DeviceWalkers(_device.Vk, Config, world);
        var body = new BodyPiece[WalkingGround.MostPiecesOfABody];
        var left = new SweptRun[Sidestepping.MostRunsOfAPass];
        var planned = new bool[world.People.Count];
        var aside = 0;
        var straights = 0;
        var hops = 0;
        var byAction = new int[Enum.GetValues<PersonAction>().Length];
        world.WatchingTheClaims = (town, stage) =>
        {
            switch (stage)
            {
                case ClaimsStage.Begun:
                    onDevice.Lay(town, Piece.Station);
                    onDevice.Run(Piece.Station);
                    break;
                case ClaimsStage.Stationed:
                    aside += CompareTheStations(town, onDevice);
                    onDevice.Lay(town, Piece.Body);
                    onDevice.Run(Piece.Body);
                    CompareTheBodies(town, onDevice, body);
                    CompareThePasses(town, onDevice, left);
                    break;
                case ClaimsStage.WalksDue:
                    Array.Clear(planned);
                    onDevice.Lay(town, Piece.Plan);
                    onDevice.LayTheClaims(town.Occupancy);
                    onDevice.Run(Piece.Plan);
                    break;
                case ClaimsStage.PlansLaid:
                    straights += CompareThePlans(town, onDevice, planned);
                    break;
                case ClaimsStage.WalkersDue:
                    onDevice.Lay(town, Piece.Read);
                    onDevice.LayTheClaims(town.Occupancy);
                    onDevice.LayTheAnswers(town);
                    onDevice.Run(Piece.Read);
                    break;
                case ClaimsStage.GrantsRead:
                    CompareTheReadings(town, onDevice);
                    break;
            }
        };
        world.WhenAWalkIsPlanned = (town, person, ways) =>
        {
            Walks.Count++;
            planned[person] = true;
            byAction[(int)town.People.Action[person]]++;
            hops += town.WalkingGround.IsHopping(person) ? 1 : 0;
            if (!onDevice.WaysOf(person).SequenceEqual(ways))
            {
                Walks.Differ.Add($"walker {person} {town.People.Action[person]}: town [{string.Join(", ", ways.ToArray())}]; device [{string.Join(", ", onDevice.WaysOf(person).ToArray())}]");
            }
        };

        loop.Advance(WatchedTicks);
        Stations.Also = $" ({aside} on a threshold)";
        Walks.Also = $" ({byAction[(int)PersonAction.Walk]} walking, {hops} of them hopping, {byAction[(int)PersonAction.Sidestep]} sidestepping, {byAction[(int)PersonAction.Rejoin]} walking back, {byAction[(int)PersonAction.Post]} to a post; {straights} straights)";
    }

    /// <returns>How many walkers were left out, stationed on a threshold.</returns>
    int CompareTheStations(TownWorld town, DeviceWalkers onDevice)
    {
        var aside = 0;
        var people = town.People;
        for (var person = 0; person < people.Count; person++)
        {
            if (OnAThreshold(town, person))
            {
                aside++;
                continue;
            }

            Stations.Count++;
            var row = onDevice.Walkers[person];
            var same = row.OnWay == people.OnWay[person] && row.RouteTaken == people.RouteTaken[person]
                       && row.Is(WalkerRow.Walking) == people.Walking[person] && row.Action == (uint)people.Action[person]
                       && SamePass(row, town, person) && Near(people.OnWayM[person], row.OnWayM)
                       && Near(people.OffWayM[person], row.OffWayM) && Near(people.DestinationM[person], row.DestinationM)
                       && Near(town.WalkingBack.BackToM[person], row.BackToM);
            if (!same) Stations.Differ.Add($"walker {person}: town way {people.OnWay[person]} slot {people.RouteTaken[person]} at {people.OnWayM[person]} off {people.OffWayM[person]} {people.Action[person]} aiming {people.DestinationM[person]}; device way {row.OnWay} slot {row.RouteTaken} at {row.OnWayM} off {row.OffWayM} {(PersonAction)row.Action} aiming {row.DestinationM}");
        }

        return aside;
    }

    void CompareTheBodies(TownWorld town, DeviceWalkers onDevice, BodyPiece[] into)
    {
        for (var person = 0; person < town.People.Count; person++)
        {
            Bodies.Count++;
            var laid = onDevice.BodyOf(person);
            if (town.People.Inside[person].Any)
            {
                if (laid.Length != 0) Bodies.Differ.Add($"walker {person} inside: device laid {laid.Length}");
                continue;
            }

            var occupant = town.GroundHeldAs(person, out var roster);
            var row = onDevice.Walkers[person];
            var count = town.WalkingGround.BodyCover(person, into);
            var same = row.HeldAs == occupant && row.Is(WalkerRow.HeldAsDriving) == (roster == LaneRoster.Driving) && laid.Length == count;
            for (var at = 0; same && at < count; at++)
            {
                ref readonly var piece = ref into[at];
                var expected = DeviceClaims.ClaimRow.Of(
                    new LaneClaim(piece.FromM, piece.ToM, piece.AlongMps, occupant, ClaimPriority.Hard, roster, piece.OnItsLine, Onward: piece.Onward, Still: piece.Still),
                    piece.Way, false, piece.FromM);
                same = Near(expected.AlongMps, laid[at].AlongMps) && laid[at] with { AlongMps = expected.AlongMps } == expected;
            }

            if (!same) Bodies.Differ.Add($"walker {person}: town {count} as {occupant}/{roster} [{string.Join(", ", into[..count])}]; device {laid.Length} as {row.HeldAs} [{string.Join(", ", laid.ToArray())}]");
        }
    }

    void CompareThePasses(TownWorld town, DeviceWalkers onDevice, SweptRun[] left)
    {
        for (var person = 0; person < town.People.Count; person++)
        {
            var count = town.Sidestepping.GroundStillToCover(person, left);
            if (count == 0) continue;

            Passes.Count++;
            var laid = onDevice.PassOf(person);
            var row = onDevice.Walkers[person];
            var occupant = town.GroundHeldAs(person, out var roster);
            var same = laid.Length == Math.Max(0, count)
                       && (count > 0 ? row.Action == (uint)town.People.Action[person] : row.Action == (uint)PersonAction.Walk && row.PassAsideM == 0f);
            for (var at = 0; same && at < count; at++)
            {
                var expected = DeviceClaims.ClaimRow.Of(
                    new LaneClaim(left[at].FromM, left[at].ToM, 0f, occupant, ClaimPriority.Hard, roster, Passing: true), left[at].Way, false, left[at].FromM);
                same = laid[at] == expected;
            }

            if (!same) Passes.Differ.Add($"walker {person}: town {count} [{string.Join(", ", left[..Math.Max(0, count)])}]; device {laid.Length} {(PersonAction)row.Action}");
        }
    }

    /// <returns>How many walkers had committed to a straight.</returns>
    int CompareThePlans(TownWorld town, DeviceWalkers onDevice, bool[] planned)
    {
        var straights = 0;
        var claims = town.Occupancy;
        var ground = town.WalkingGround;
        for (var person = 0; person < town.People.Count; person++)
        {
            if (onDevice.Planned(person) != planned[person]) Walks.Differ.Add($"walker {person}: town planned {planned[person]}, device {onDevice.Planned(person)}");

            var row = onDevice.Walkers[person];
            var straight = ground.StraightOf(person);
            if (!float.IsNaN(ground.StraightToM(person).X))
            {
                straights++;
                var sameStraight = row.Is(WalkerRow.OnAStraight) && row.StraightFromM == ground.StraightFromM(person)
                                   && row.StraightToM == ground.StraightToM(person) && onDevice.StraightOf(person).SequenceEqual(straight);
                if (!sameStraight) Walks.Differ.Add($"walker {person}: town straight {ground.StraightFromM(person)}→{ground.StraightToM(person)} [{string.Join(", ", straight.ToArray())}]; device {row.StraightFromM}→{row.StraightToM} [{string.Join(", ", onDevice.StraightOf(person).ToArray())}]");
            }

            var hold = town.WalkHold(person);
            var laidAHold = onDevice.LaidAHold(person, out var held);
            if (laidAHold != (hold != LaneOccupancy.NoHold))
            {
                Claims.Differ.Add($"walker {person}: town hold {hold}, device {laidAHold}");
                continue;
            }

            if (hold == LaneOccupancy.NoHold) continue;

            Claims.Count++;
            var endsAtM = claims.HoldEndsAtM(hold, out var marginM, out var cutBy);
            var answered = (held.Flags & 1u) != 0u;
            var sameCutter = held.LaidCutBy == DeviceClaims.ClaimAnswer.NoRow ? !cutBy.Found : claims.Slots[(int)held.LaidCutBy] == cutBy;
            var terms = claims.TermsOf(hold);
            var same = answered == claims.IsAnswered(hold) && held.StandingMarginM == claims.StandingMarginM(hold)
                       && held.LaidCutLineM == endsAtM && held.LaidMarginM == marginM && held.LaidCutOn == claims.HoldCutOn(hold)
                       && sameCutter && held.KeptOffWhatItMayPassM == terms.KeptOffWhatItMayPassM && held.PassesBelowMps == terms.PassesBelowMps;
            var (first, end) = claims.PiecesOf(hold);
            var rows = onDevice.ProposedOf(person);
            same = same && rows.Length == end - first;
            for (var at = 0; same && at < rows.Length; at++)
            {
                var slot = first + at;
                var expected = DeviceClaims.ClaimRow.Of(
                    claims.Slots[slot], claims.SlotWays[slot], false, claims.Slots[slot].Secondary ? claims.Slots[slot].FromM : claims.SlotStandsToM[slot]);
                same = Near(expected.AlongMps, rows[at].AlongMps) && rows[at] with { Hold = expected.Hold, AlongMps = expected.AlongMps } == expected;
            }

            if (!same)
            {
                var laidByTheTown = new List<string>();
                for (var slot = first; slot < end; slot++) laidByTheTown.Add(claims.Slots[slot].ToString());
                Claims.Differ.Add($"walker {person} {town.People.Action[person]}: town ends {endsAtM} kept {marginM} on {claims.HoldCutOn(hold)} answered {claims.IsAnswered(hold)} [{string.Join(", ", laidByTheTown)}]; device {held} [{string.Join(", ", rows.ToArray())}]");
            }
        }

        return straights;
    }

    void CompareTheReadings(TownWorld town, DeviceWalkers onDevice)
    {
        var people = town.People;
        for (var person = 0; person < people.Count; person++)
        {
            Readings.Count++;
            var row = onDevice.Walkers[person];
            var same = row.GrantM == people.GrantM[person] && row.OnCrossing == people.OnCrossing[person]
                       && row.Action == (uint)people.Action[person] && SamePass(row, town, person) && Near(people.DestinationM[person], row.DestinationM);
            if (!same) Readings.Differ.Add($"walker {person}: town granted {people.GrantM[person]} crossing {people.OnCrossing[person]} {people.Action[person]} {people.Pass[person]} aiming {people.DestinationM[person]}; device {row.GrantM} {row.OnCrossing} {(PersonAction)row.Action} begun {row.Is(WalkerRow.PassBegun)} aside {row.PassAsideM} aiming {row.DestinationM}");
        }
    }

    /// <summary>
    /// Somebody stood still on the way of each of a few walkers out on a stretch of pavement with a lane beside it and
    /// room ahead, <see cref="StandsAheadM"/> in front of it — going nowhere, which is what a walker gets past (PER-28).
    /// </summary>
    static void StandSomebodyInTheWay(TownWorld world)
    {
        var people = world.People;
        var stood = new bool[people.Count];
        var staged = 0;
        for (var walker = 0; walker < people.Count && staged < Staged; walker++)
        {
            var code = people.CurrentRouteWay(walker);
            if (stood[walker] || !people.Walking[walker] || people.Inside[walker].Any || people.OnWay[walker] < 0) continue;
            if (WalkingNetwork.IsACorner(code) || world.IsTheCrossing(people.OnWay[walker])) continue;

            var atM = people.OnWayM[walker] + StandsAheadM;
            var endM = people.OnTheLastWay(walker) ? people.RouteToM[walker] : world.Walking.WayLengthM(code);
            if (endM - atM < RoomPastM || float.IsNaN(world.Walking.AsideM(code, people.OnWayM[walker]))) continue;

            for (var standing = people.Count - 1; standing > walker; standing--)
            {
                if (stood[standing] || people.Inside[standing].Any || people.Wounded[standing]) continue;

                var at = Spline.SampleAt(world.Walking.WayArcs(code), atM);
                Put(world, standing, at.PositionM, at.HeadingRad);
                people.ClearRoute(standing);
                people.Walking[standing] = false;
                people.Manual[standing] = true;
                people.Stage[standing] = TripStage.UnderOrders;
                people.GoalM[standing] = at.PositionM;
                stood[walker] = stood[standing] = true;
                staged++;
                break;
            }
        }
    }

    /// <summary>
    /// A few walkers on their way shoved off the pavement altogether, onto ground only the traffic drives beside it —
    /// what a walker walks back from over a straight of its own (PER-25).
    /// </summary>
    static void ShoveOffThePavement(TownWorld world)
    {
        var people = world.People;
        Span<WayCover> under = stackalloc WayCover[RibbonAtlas.MostWaysUnderABody];
        var shoved = 0;
        for (var walker = 0; walker < people.Count && shoved < Staged; walker++)
        {
            var code = people.CurrentRouteWay(walker);
            if (!people.Walking[walker] || people.Manual[walker] || people.Inside[walker].Any || people.OnWay[walker] < 0) continue;

            var at = Spline.SampleAt(world.Walking.WayArcs(code), people.OnWayM[walker]);
            foreach (var asideM in (ReadOnlySpan<float>)[3f, -3f, 4.5f, -4.5f])
            {
                var toM = at.PositionM + (at.Right * asideM);
                var count = world.Atlas.UnderDisc(toM, people.RadiusM[walker], under);
                var driven = count > 0;
                for (var way = 0; way < count; way++) driven &= world.Ways.IsDriven(under[way].Way);
                if (!driven) continue;

                Put(world, walker, toM, people.HeadingRad[walker]);
                shoved++;
                break;
            }
        }
    }

    /// <summary>
    /// A few walkers sent straight to a place out on the carriageway beside them and kept there by an order, as a closure
    /// sends its officer to a post (SRV-11) — over every way the straight crosses.
    /// </summary>
    static void SendToAPost(TownWorld world)
    {
        var people = world.People;
        Span<WayCover> under = stackalloc WayCover[RibbonAtlas.MostWaysUnderABody];
        var sent = 0;
        for (var walker = people.Count - 1; walker >= 0 && sent < Staged; walker--)
        {
            var code = people.CurrentRouteWay(walker);
            if (!people.Walking[walker] || people.Manual[walker] || people.Inside[walker].Any || people.OnWay[walker] < 0) continue;

            var at = Spline.SampleAt(world.Walking.WayArcs(code), people.OnWayM[walker]);
            foreach (var asideM in (ReadOnlySpan<float>)[6f, -6f, 8f, -8f])
            {
                var postM = at.PositionM + (at.Right * asideM);
                var count = world.Atlas.UnderDisc(postM, people.RadiusM[walker], under);
                var driven = count > 0;
                for (var way = 0; way < count; way++) driven &= world.Ways.IsDriven(under[way].Way);
                if (!driven) continue;

                people.ClearRoute(walker);
                people.Manual[walker] = true;
                people.Stage[walker] = TripStage.UnderOrders;
                people.Action[walker] = PersonAction.Post;
                people.GoalM[walker] = postM;
                people.DestinationM[walker] = postM;
                sent++;
                break;
            }
        }
    }

    /// <summary>A walker's body put down where it is told and stood still there.</summary>
    static void Put(TownWorld world, int person, Vector2 atM, float headingRad)
    {
        var people = world.People;
        people.PositionM[person] = atM;
        people.DestinationM[person] = atM;
        people.VelocityMps[person] = Vector2.Zero;
        people.DeclaredMps[person] = Vector2.Zero;
        people.HeadingRad[person] = headingRad;
        world.PhysicsForInstruments.Release(people.Body[person], atM, headingRad);
    }

    /// <summary>Whether the device left a walker on the pass the town left it on, or on none as the town did.</summary>
    static bool SamePass(in WalkerRow row, TownWorld town, int person)
    {
        var pass = town.People.Pass[person];
        return row.PassSlot == pass.Slot && row.PassFromM == pass.FromM && row.PassClearM == pass.ClearM
               && row.PassAsideM == pass.AsideM && row.Is(WalkerRow.PassBegun) == pass.Begun;
    }

    /// <summary>
    /// Whether the town stationed a walker within <see cref="ThresholdM"/> of a threshold its own trigonometry decides:
    /// the end of the way it is on, the edge of that way's ground, or the hop's end.
    /// </summary>
    static bool OnAThreshold(TownWorld town, int person)
    {
        var people = town.People;
        var way = people.CurrentRouteWay(person);
        if (way == -1 || !people.Walking[person]) return false;

        var walkedOutM = MathF.Abs(people.OnWayM[person] - (town.WalkingGround.EndOfTheWayM(person) - Config.PersonStepM));
        var groundM = MathF.Abs(people.OffWayM[person] - (town.Walking.WayWidthM(way) * 0.5f));
        var arrivedM = MathF.Abs((people.DestinationM[person] - people.PositionM[person]).Length() - people.RadiusM[person]);
        return walkedOutM < ThresholdM || groundM < ThresholdM || arrivedM < ThresholdM;
    }

    /// <summary>
    /// Within a part in a thousand and a hundredth absolute: a point on a bent way, and a pace along a heading, is found
    /// by the device's own sine, which Vulkan allows far further off the host's than a float's last bit.
    /// </summary>
    internal static bool Near(float host, float onDevice) =>
        host == onDevice || MathF.Abs(host - onDevice) <= 1e-2f + (1e-3f * MathF.Abs(host));

    static bool Near(Vector2 host, Vector2 onDevice) => Near(host.X, onDevice.X) && Near(host.Y, onDevice.Y);

    public void Dispose() => _device.Dispose();
}
