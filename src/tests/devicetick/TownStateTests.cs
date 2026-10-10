using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>The town's state reached on the device by its C# names</b> (<see cref="TownState"/>, <c>town_state_check.comp</c>):
/// each table at the id its header names, a module's fields read through their accessors as the C# holds them, a stream
/// drawn as the host draws it, and the map's queries answered as the host answers them over every lane of the suite's
/// built town.
/// </summary>
/// <remarks>
/// The fields read are staged first with a figure of their own a row, so a table read at another's stride or another's
/// id reads another figure.
/// </remarks>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class TownStateTests(Lavapipe device, TownStateTests.StagedTown staged) : IClassFixture<Lavapipe>, IClassFixture<TownStateTests.StagedTown>
{
    /// <summary>The town every case reads, stood and staged once (<see cref="StageTheTown"/>).</summary>
    public sealed class StagedTown : IDisposable
    {
        internal TownWorld Town { get; } = StageTheTown();

        public void Dispose() => Town.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct Push(uint Probe, uint Count, uint OutId);

    /// <summary>Words a thread writes (<c>ProbeWords</c>), and the id the check's own output is set at.</summary>
    const int Words = 8;
    const int OutId = TownRoot.Ids - 1;

    const uint Cars = 0, People = 1, Bays = 2, Errands = 3, Lanes = 4, CarActions = 5, WalkerActions = 6, Draws = 7;
    const uint ConnectorBetween = 8, ReachesBySwitching = 9, PieceAt = 10, NearestDoor = 11;

    TownWorld Town => staged.Town;

    [Fact]
    public void EveryTableIsLaidAtTheIdItsHeaderNames()
    {
        var named = new Dictionary<string, int>();
        foreach (var header in Directory.GetFiles(Path.Combine(ProjectPaths.Root, "src", "world", "town", "kernels"), "town_*.glsl"))
        {
            foreach (Match id in Regex.Matches(File.ReadAllText(header), @"const uint (\w+Id) = (\d+)u;"))
            {
                if (id.Groups[1].Value != "TownCountsId") named.Add(id.Groups[1].Value, int.Parse(id.Groups[2].Value));
            }
        }

        var laid = typeof(TownState).GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.Name.EndsWith("Id", StringComparison.Ordinal))
            .ToDictionary(field => field.Name, field => (int)field.GetRawConstantValue()!);

        Assert.Equal(named.OrderBy(id => id.Key), laid.OrderBy(id => id.Key));
    }

    [Fact]
    public void TheCarsFieldsAreReadThroughTheirAccessors()
    {
        var (cars, standing, wrecked) = (Town.Cars, Town.StandingKept, Town.Settled.Wrecked);
        AssertProbe(Cars, cars.Capacity, car =>
        [
            (uint)cars.Action[car], (uint)cars.RouteLanes[(car * CarFleet.RouteLanesPerCar) + CarFleet.RouteLanesPerCar - 1],
            (uint)cars.Owner[car], Bits(cars.DestinationM[car].Y), Flag(Town.AsksTheRoute(car)), standing.Entries[car],
            Bits(cars.BackRoomM[car]), Flag(wrecked.Has(car)),
        ]);
    }

    [Fact]
    public void ThePeoplesFieldsAreReadThroughTheirAccessors()
    {
        var (people, wounded) = (Town.People, Town.Settled.Wounded);
        AssertProbe(People, people.Capacity, person =>
        [
            Bits(people.TimerS[person]), (uint)people.Inside[person].Kind, (uint)people.Inside[person].Index, people.Variant[person],
            (uint)people.DestinationBuilding[person], Flag(people.Manual[person]), Flag(wounded.Has(person)), 0u,
        ]);
    }

    [Fact]
    public void TheBaysAreReadThroughTheirAccessors()
    {
        var (parking, streets) = (Town.Parking.Kept, Town.BayStreets.Kept);
        AssertProbe(Bays, Town.Parking.BayCount, bay =>
        [
            (uint)parking.CarInBay[bay], Bits(parking.CentreM[bay].X), (uint)parking.HeldFor[bay], (uint)streets.Lane[(bay * 2) + 1],
            Bits(Town.BayStreets.AtLaneM(bay, streets.Lane[bay * 2])), Bits(parking.SpaceWidthM), (uint)Town.Parking.BayCount, 0u,
        ]);
    }

    [Fact]
    public void TheErrandsAreReadThroughTheirAccessors()
    {
        var (beat, commands) = (Town.Beat, Town.Commands);
        AssertProbe(Errands, Town.Cars.Capacity, car =>
        [
            (uint)Town.Recovery.OnTheHookOf[car], (uint)beat.ClosedLanes[(car * beat.MostLanesClosed) + beat.MostLanesClosed - 1],
            Bits(Town.Duty.SinceS[car]), (uint)Town.CarOrders.Kind[car], (uint)Town.Containment.CrewOf(car, Containers.CrewSeats - 1),
            Bits(Town.Rounds.Round.StoodS[car]), (uint)Town.Aprons.Stride, Bits(commands.Hands.Steer),
        ]);
    }

    [Fact]
    public void TheMapsLanesAreReadThroughTheirAccessors()
    {
        var roads = Town.Roads;
        AssertProbe(Lanes, roads.LaneCount, lane =>
        [
            Bits(roads.LaneWidthM[lane]), (uint)(roads.ConnectorsFrom(lane).FirstId + roads.ConnectorsFrom(lane).Count),
            (uint)roads.Places.Arriving(lane), (uint)Town.Driving.LinkOfLane(lane), Bits(roads.ArcsOf(lane)[0].LengthM),
            (uint)roads.LaneBeside(lane, inward: false), Bits(Town.Furniture.StopBarAlongM(lane)), (uint)roads.LaneCount,
        ]);
    }

    [Fact]
    public void TheCarActionsAreReadThroughTheirAccessors()
    {
        var (overtaking, switching, bays) = (Town.Overtaking.Kept, Town.Switching.Kept.Swept.Kept, Town.Bays.Kept);
        AssertProbe(CarActions, Town.Cars.Capacity, car =>
        [
            Bits(overtaking.Shapes[(car * 2) + 1].AsideM), (uint)overtaking.PassedOn[(((car * 2) + 1) * 24) + 23],
            (uint)switching.Count[car], Bits(switching.Runs[(car * switching.MostEach) + switching.MostEach - 1].LastAtM),
            (uint)Town.Manoeuvres.Bay[car], (uint)bays.StandingOf[car].Covers, (uint)overtaking.AskedAs[car],
            Bits(Town.Manoeuvres.RoomOf(car)[BayManoeuvre.MostArcs - 1].LengthM),
        ]);
    }

    [Fact]
    public void TheWalkerActionsAreReadThroughTheirAccessors()
    {
        var sidestepping = Town.Sidestepping.Kept;
        var asked = sidestepping.AskedGround.Kept;
        AssertProbe(WalkerActions, Town.People.Capacity, person =>
        [
            Bits(sidestepping.Decided[person].AsideM), Bits(asked.Runs[(person * asked.MostEach) + asked.MostEach - 1].ToM),
            (uint)sidestepping.PassedOn[person], Bits(Town.WalkingGround.StraightGround.Kept.StationAtM[person]),
            Bits(sidestepping.WaitedS[person]), (uint)Town.Sidestepping.Made, 0u, 0u,
        ]);
    }

    [Fact]
    public void AStreamDrawsOnTheDeviceAsTheHostDraws()
    {
        AssertProbe(Draws, Town.Cars.Capacity, car =>
        {
            var draw = Town.Cars.Draw[car];
            return [Bits(draw.NextFloat()), Bits(draw.NextFloat(0.8f, 1.2f)), (uint)draw.NextInt(7), draw.NextUint(), (uint)draw.NextInt(0), Bits(draw.NextFloat()), 0u, 0u];
        });
    }

    [Fact]
    public void ConnectorBetweenIsTheHostsOnEveryLane()
    {
        var roads = Town.Roads;
        AssertProbe(ConnectorBetween, roads.LaneCount, lane =>
        [
            (uint)roads.ConnectorBetween(lane, FirstOnward(lane)), (uint)roads.ConnectorBetween(lane, roads.LaneReverse[lane]),
            (uint)roads.ConnectorBetween(lane, lane), 0u, 0u, 0u, 0u, 0u,
        ]);
    }

    [Fact]
    public void ReachesBySwitchingIsTheHostsOnEveryLane()
    {
        var roads = Town.Roads;
        AssertProbe(ReachesBySwitching, roads.LaneCount, lane =>
        {
            var beside = roads.LaneBeside(lane, inward: true);
            var onto = FirstOnward(beside == RoadGraph.NoLane ? lane : beside);
            var reaches = roads.ReachesBySwitching(lane, onto, out var besideAt);
            return [Flag(reaches), (uint)besideAt, (uint)onto, 0u, 0u, 0u, 0u, 0u];
        });
    }

    [Fact]
    public void WhereALaneStandsOnItsLinkIsTheHostsOnEveryLane()
    {
        var (roads, driving) = (Town.Roads, Town.Driving);
        AssertProbe(PieceAt, roads.LaneCount, lane =>
        {
            var placeM = driving.PlaceOfM(lane, 0.5f * roads.LaneLengthM[lane]);
            var piece = driving.Runs.PieceAt(driving.LinkOfLane(lane), placeM, out var alongPieceM);
            return [Bits(placeM), (uint)piece, Bits(alongPieceM), 0u, 0u, 0u, 0u, 0u];
        });
    }

    [Fact]
    public void TheNearestWayInIsTheHostsForEveryBuilding()
    {
        var (doors, buildings) = (Town.Doors.Grid, Town.Plan.Buildings);
        AssertProbe(NearestDoor, buildings.Count, building =>
        {
            var door = doors.Nearest(buildings.CentreM[building], out var distanceM);
            return [(uint)door, Bits(distanceM), 0u, 0u, 0u, 0u, 0u, 0u];
        });
    }

    /// <summary>A probe run over <paramref name="count"/> rows, each row's words held to the C#'s.</summary>
    void AssertProbe(uint probe, int count, Func<int, uint[]> expected)
    {
        var read = Probed(probe, count);
        var wanted = new uint[count * Words];
        for (var row = 0; row < count; row++) expected(row).CopyTo(wanted, row * Words);

        Assert.Equal(wanted, read);
    }

    uint[] Probed(uint probe, int count)
    {
        var vk = device.Vk;
        using var root = new TownRoot(vk);
        using var state = new TownState(vk, root, Town);
        using var into = vk.CreateBuffer(GpuBuffer.Bytes<uint>(count * Words), BufferUsageFlags.StorageBufferBit, HostAccess.Reads);
        into.Span<uint>().Clear();
        root.Set(OutId, into);

        using var kernel = new ComputeKernel(vk, "town_state_check.comp", 1, (uint)Marshal.SizeOf<Push>());
        var set = kernel.Bind([root.Buffer]);
        vk.OneShot(commands => kernel.Dispatch(commands, set, (uint)((count + 63) / 64), new Push(probe, (uint)count, OutId)));
        return into.Span<uint>()[..(count * Words)].ToArray();
    }

    int FirstOnward(int lane) => Town.Roads.LanesFrom(lane) is { Length: > 0 } onward ? onward[0] : RoadGraph.NoLane;

    static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);

    static uint Flag(bool value) => value ? 1u : 0u;

    /// <summary>
    /// <b>The suite's built town with a figure of its own staged in each field a probe reads</b> where the town stood it
    /// at a default — so a field read at a stride or an id not its own reads another figure.
    /// </summary>
    static TownWorld StageTheTown()
    {
        var town = new TownWorld(Towns.Built, SimConfig.Shipped());
        var cars = town.Cars;
        for (var car = 0; car < cars.Capacity; car++)
        {
            cars.Action[car] = (CarAction)(car % 10);
            cars.RouteLanes[(car * CarFleet.RouteLanesPerCar) + CarFleet.RouteLanesPerCar - 1] = 1000 + car;
            cars.Owner[car] = (3 * car) - 1;
            cars.DestinationM[car] = new Vector2(car, 0.25f * car);
            cars.BackRoomM[car] = 0.5f * car;
            town.StandingKept.Entries[car] = 7u * (uint)car;
            if (car % 3 == 0) town.Settled.Wrecked.Add(car);

            town.Recovery.OnTheHookOf[car] = car - 1;
            town.Beat.ClosedLanes[(car * town.Beat.MostLanesClosed) + town.Beat.MostLanesClosed - 1] = 500 + car;
            town.Duty.SinceS[car] = 0.75f * car;
            town.CarOrders.Kind[car] = (PlayerOrder)(car % 5);
            town.Rounds.Round.StoodS[car] = 1.5f * car;

            var overtaking = town.Overtaking.Kept;
            overtaking.Shapes[(car * 2) + 1] = new Overtake(car, 0f, 0f, 0f, 0f, 0.01f * car, 0f, 0f, 0f, Begun: false);
            overtaking.PassedOn[(((car * 2) + 1) * 24) + 23] = 900 + car;
            overtaking.AskedAs[car] = 11L * car;
            var swept = town.Switching.Kept.Swept.Kept;
            swept.Count[car] = car % swept.MostEach;
            swept.Runs[(car * swept.MostEach) + swept.MostEach - 1] = new SweptRun(car, 0f, 0f, 0f, 0.125f * car);
            town.Manoeuvres.Bay[car] = (2 * car) + 1;
            town.Bays.Kept.StandingOf[car] = new BayManoeuvring.Standing(0, 0, 0f, car % 48);
            town.Manoeuvres.RoomOf(car)[BayManoeuvre.MostArcs - 1] = new ArcSeg(Vector2.Zero, 0f, 0.375f * car, 0f);
        }

        var people = town.People;
        var sidestepping = town.Sidestepping.Kept;
        var asked = sidestepping.AskedGround.Kept;
        for (var person = 0; person < people.Capacity; person++)
        {
            people.TimerS[person] = 0.1f * person;
            people.Inside[person] = new Contained((ContainerKind)(person % 3), (2 * person) - 1);
            people.Variant[person] = (byte)(person % 5);
            people.DestinationBuilding[person] = person - 1;
            people.Manual[person] = person % 2 == 0;
            if (person % 4 == 0) town.Settled.Wounded.Add(person);

            sidestepping.Decided[person] = new Sidestep(0, 0f, 0f, 0.3f * person, Begun: false);
            asked.Runs[(person * asked.MostEach) + asked.MostEach - 1] = new SweptRun(0, 0f, 0.2f * person, 0f, 0f);
            sidestepping.PassedOn[person] = 700 + person;
            town.WalkingGround.StraightGround.Kept.StationAtM[person] = 0.6f * person;
            sidestepping.WaitedS[person] = 0.05f * person;
        }

        var parking = town.Parking.Kept;
        for (var bay = 0; bay < town.Parking.BayCount; bay++)
        {
            parking.CarInBay[bay] = (2 * bay) - 1;
            parking.HeldFor[bay] = (bay % 5) - 3;
        }

        return town;
    }
}
