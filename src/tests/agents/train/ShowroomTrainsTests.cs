using System.Numerics;
using TrafficSimulation.Agents.Train.Body;
using TrafficSimulation.Agents.Train.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Rail;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Train;

/// <summary>
/// <b>The showroom's trains</b> (TRN-14): every look of the rolling stock stood once in a train of one kind, on a siding of
/// its own, never sent, and held where a hand leaves it.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P9)]
public class ShowroomTrainsTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static TrainCatalog Catalog => TrainCatalog.Shared;

    /// <summary>The kind of wagon each locomotive kind hauls (TRN-4).</summary>
    static bool Hauls(VehicleKind lead, VehicleKind wagon) =>
        lead == VehicleKind.Electric ? wagon == VehicleKind.Coach : wagon is VehicleKind.Tank or VehicleKind.Coal;

    sealed class Yard
    {
        public readonly Trains Trains;
        public readonly RailVehicleSeen[] Seen;
        public readonly TrainOrders Orders;

        public Yard()
        {
            var plan = Towns.Of(ShowroomPlan.Name);
            var graph = RailGraph.Of(plan.Rails, plan.WorldSizeM);
            Trains = new Trains(graph, plan.Rails, plan.WorldSizeM, Catalog, Config, agentSeed: 1, standEachLookOnce: true);
            Orders = new TrainOrders(Trains.MostOrdersATick);
            Seen = new RailVehicleSeen[Trains.WagonCount];
        }

        /// <summary>One tick, every vehicle put down taken to stand where it was put.</summary>
        public TrainOrder[] Tick(bool held = false)
        {
            Orders.Clear();
            Trains.Advance(Seen, Config.TickSeconds, held, Orders);
            var given = Orders.Written.ToArray();
            foreach (var order in given)
            {
                if (order.Kind != TrainOrderKind.Place) continue;

                var forward = new Vector2(MathF.Cos(order.HeadingRad), MathF.Sin(order.HeadingRad));
                Seen[order.Wagon] = new RailVehicleSeen(order.AtM, forward, forward * order.SpeedMps);
            }

            return given;
        }
    }

    /// <summary>
    /// <b>Every look stands once, in a train of one kind</b> (TRN-4): a locomotive look at the head of each, and behind it
    /// wagons of the one kind it hauls.
    /// </summary>
    [Fact]
    public void EveryLookStandsOnceInATrainOfOneKind()
    {
        var stood = Trains.StandEachLookOnce(Catalog, ShowroomPlan.Sidings);

        var worn = new int[Catalog.Vehicles.Length];
        foreach (var train in stood)
        {
            foreach (var look in train) worn[look]++;
            if (train.Length == 0) continue;

            var lead = Catalog.Vehicles[train[0]].Kind;
            Assert.True(lead is VehicleKind.Electric or VehicleKind.Diesel, $"{Catalog.Vehicles[train[0]].Id} heads a train");
            Assert.True(train.Length > 1, $"{Catalog.Vehicles[train[0]].Id} stands alone");
            var wagon = Catalog.Vehicles[train[1]].Kind;
            Assert.True(Hauls(lead, wagon), $"{Catalog.Vehicles[train[0]].Id} hauls {wagon}");
            foreach (var look in train[1..]) Assert.Equal(wagon, Catalog.Vehicles[look].Kind);
        }

        for (var look = 0; look < worn.Length; look++) Assert.True(worn[look] == 1, $"{Catalog.Vehicles[look].Id} stands {worn[look]} times");
    }

    /// <summary>
    /// <b>Each train is put down standing on a siding of its own at the first tick</b>, however the town is held, and none is
    /// ever sent.
    /// </summary>
    [Fact]
    public void EachTrainIsPutDownStandingOnASidingOfItsOwnAndNeverSent()
    {
        var yard = new Yard();
        var first = yard.Tick(held: true);

        var placed = first.Where(order => order.Kind == TrainOrderKind.Place).ToArray();
        Assert.Equal(Catalog.Vehicles.Length, placed.Length);
        Assert.All(placed, order => Assert.Equal(0f, order.SpeedMps));
        Assert.All(first.Where(order => order.Kind == TrainOrderKind.Drive), order => Assert.True(order.Command.Handbrake));

        var sidings = new HashSet<float>();
        for (var train = 0; train < yard.Trains.Count; train++)
        {
            var vehicles = yard.Trains.VehiclesOf(train).ToArray();
            if (vehicles.Length == 0) continue;

            var rows = placed.Where(order => vehicles.Contains(order.Wagon)).Select(order => order.AtM.Y).Distinct().ToArray();
            Assert.Single(rows);
            Assert.True(sidings.Add(rows[0]), $"train {train} stands on another's siding");
        }

        for (var tick = 0; tick < 600; tick++) Assert.DoesNotContain(yard.Tick(), order => order.Kind == TrainOrderKind.Place);
        Assert.Equal(0, yard.Trains.Sent);
    }

    /// <summary>
    /// <b>Let go, a train brakes where it was left</b>: it has no driver of its own to take it on, so the last hand's pull
    /// is not what it keeps.
    /// </summary>
    [Fact]
    public void ATrainLetGoBrakesWhereItWasLeft()
    {
        var yard = new Yard();
        yard.Tick();
        yard.Trains.Hand(0, new TrainControls(1f, Reverse: false, Brake: false));
        Assert.Contains(yard.Tick(), order => order.Kind == TrainOrderKind.Drive && !order.Command.Handbrake);

        yard.Trains.LetGo(0);
        var vehicles = yard.Trains.VehiclesOf(0).ToArray();
        var braked = yard.Tick().Where(order => order.Kind == TrainOrderKind.Drive && vehicles.Contains(order.Wagon)).ToArray();
        Assert.Equal(vehicles.Length, braked.Length);
        Assert.All(braked, order => Assert.True(order.Command.Handbrake && order.Command.ThrottleMps2 == 0f));
    }
}
