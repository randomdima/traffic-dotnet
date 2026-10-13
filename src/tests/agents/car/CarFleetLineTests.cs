using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>The line a car carries, as the fleet holds it (<see cref="CarFleet.ShiftTheLine"/>).</summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P3)]
public class CarFleetLineTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>A line shifted on past its first lane is the line laid over the rest of its chain</b>: the same arcs, entry
    /// figures, lanes and joins, and the same metres to within what two sums of one length disagree by.
    /// </summary>
    [Fact]
    public void AShiftedLineIsTheLineLaidOverTheRestOfItsChain()
    {
        var graph = RoadGraph.Build(Towns.Of(Towns.Fixture), Config);
        var arcsPerCar = LineAssembler.ArcsFor(graph);
        var cars = new CarFleet(2, arcsPerCar, CarBuilds.OfTheFleet(Config, CarCatalog.Shared));
        var shifted = cars.Add(Vector2.Zero, 0f, 0, false, new Rng(1, 1));
        var laid = cars.Add(Vector2.Zero, 0f, 0, false, new Rng(1, 2));
        var random = new Random(3);
        Span<int> lanes = stackalloc int[LineAssembler.MostLanes];
        for (var trial = 0; trial < 500; trial++)
        {
            var count = Chain(graph, lanes, random);
            if (count < 2) continue;

            var stopM = random.Next(3) == 0 ? random.NextSingle() * graph.LaneLengthM[lanes[count - 1]] : float.PositiveInfinity;
            Lay(graph, cars, shifted, lanes[..count], stopM);
            Lay(graph, cars, laid, lanes[1..count], stopM);
            if (cars.Line[shifted].LaneCount != count) continue;

            cars.ShiftTheLine(shifted);

            Assert.Equal(cars.Line[laid].ArcCount, cars.Line[shifted].ArcCount);
            Assert.Equal(cars.Line[laid].LaneCount, cars.Line[shifted].LaneCount);
            Assert.Equal(cars.Line[laid].LengthM, cars.Line[shifted].LengthM, LineTolerance.RoundingM);
            Assert.True(cars.LineOf(laid).SequenceEqual(cars.LineOf(shifted)));
            Assert.True(cars.EntriesOf(laid).SequenceEqual(cars.EntriesOf(shifted)));
            for (var slot = 0; slot < cars.Line[laid].LaneCount; slot++)
            {
                Assert.Equal(cars.ChainOf(laid)[slot], cars.ChainOf(shifted)[slot]);
                Assert.Equal(cars.LaneFirstArcsOf(laid)[slot], cars.LaneFirstArcsOf(shifted)[slot]);
                Assert.Equal(cars.LaneStartsOf(laid)[slot], cars.LaneStartsOf(shifted)[slot], LineTolerance.RoundingM);
                Assert.Equal(cars.LaneEndsOf(laid)[slot], cars.LaneEndsOf(shifted)[slot], LineTolerance.RoundingM);
                Assert.Equal(cars.JoinsOf(laid)[slot], cars.JoinsOf(shifted)[slot]);
                if (slot + 1 < cars.Line[laid].LaneCount) Assert.Equal(cars.JoinBreaksOf(laid)[slot], cars.JoinBreaksOf(shifted)[slot]);
            }
        }
    }

    /// <summary>
    /// <b>A line grown at its far end is the line assembled whole over the same lanes</b>, to the bit — its arcs, entry
    /// figures and metres — whether or not it was shifted on before it grew: the lanes it kept are summed again, never
    /// carried (<see cref="LineAssembler.Assemble"/>'s <c>keptWhole</c>, <see cref="CornerLimits.Lay"/>'s <c>keptBefore</c>).
    /// </summary>
    [Fact]
    public void AGrownLineIsTheLineAssembledWhole()
    {
        var graph = RoadGraph.Build(Towns.Of(Towns.Fixture), Config);
        var cars = new CarFleet(2, LineAssembler.ArcsFor(graph), CarBuilds.OfTheFleet(Config, CarCatalog.Shared));
        var grown = cars.Add(Vector2.Zero, 0f, 0, false, new Rng(1, 1));
        var whole = cars.Add(Vector2.Zero, 0f, 0, false, new Rng(1, 2));
        var random = new Random(5);
        Span<int> lanes = stackalloc int[LineAssembler.MostLanes];
        var grew = 0;
        for (var trial = 0; trial < 500; trial++)
        {
            var count = Chain(graph, lanes, random);
            var shifts = random.Next(2);
            var keeps = 2 + shifts;
            if (count <= keeps) continue;

            var stopM = random.NextSingle() * graph.LaneLengthM[lanes[keeps - 1]];
            Lay(graph, cars, grown, lanes[..keeps], stopM);
            if (cars.Line[grown].LaneCount != keeps) continue;

            for (var shift = 0; shift < shifts; shift++) cars.ShiftTheLine(grown);

            var chain = lanes[shifts..count];
            var kept = cars.Line[grown].LaneCount - 1;
            var line = LineAssembler.Assemble(
                graph, chain, cars.LineArcsOf(grown), cars.LaneStartsOf(grown), cars.LaneEndsOf(grown),
                laneFirstArc: cars.LaneFirstArcsOf(grown), keptWhole: kept);
            var keptArcs = cars.LaneFirstArcsOf(grown)[kept];
            cars.SetLine(grown, line);
            CornerLimits.Lay(cars.LineOf(grown), cars.LineEntriesOf(grown), Config, keptArcs);
            Lay(graph, cars, whole, chain, float.PositiveInfinity);
            grew++;

            Assert.Equal(cars.Line[whole], cars.Line[grown]);
            Assert.True(cars.LineOf(whole).SequenceEqual(cars.LineOf(grown)));
            Assert.True(cars.EntriesOf(whole).SequenceEqual(cars.EntriesOf(grown)));
            var laneCount = cars.Line[whole].LaneCount;
            Assert.True(cars.LaneStartsOf(whole)[..laneCount].SequenceEqual(cars.LaneStartsOf(grown)[..laneCount]));
            Assert.True(cars.LaneEndsOf(whole)[..laneCount].SequenceEqual(cars.LaneEndsOf(grown)[..laneCount]));
            Assert.True(cars.LaneFirstArcsOf(whole)[..laneCount].SequenceEqual(cars.LaneFirstArcsOf(grown)[..laneCount]));
        }

        Assert.True(grew > 0, "no chain was long enough to grow");
    }

    static int Chain(RoadGraph graph, Span<int> lanes, Random random)
    {
        lanes[0] = random.Next(graph.LaneCount);
        var count = 1;
        var wanted = 1 + random.Next(6);
        while (count < wanted && graph.LanesFrom(lanes[count - 1]).Length > 0)
        {
            var onward = graph.LanesFrom(lanes[count - 1]);
            lanes[count++] = onward[random.Next(onward.Length)];
        }

        return count;
    }

    /// <summary>A line over these lanes laid into one car's room as the town lays one.</summary>
    static void Lay(RoadGraph graph, CarFleet cars, int car, ReadOnlySpan<int> lanes, float stopM)
    {
        var line = LineAssembler.Assemble(
            graph, lanes, cars.LineArcsOf(car), cars.LaneStartsOf(car), cars.LaneEndsOf(car), stopM, cars.LaneFirstArcsOf(car));
        cars.SetLine(car, line);
        lanes[..line.LaneCount].CopyTo(cars.ChainOf(car));
        CornerLimits.Lay(cars.LineOf(car), cars.LineEntriesOf(car), Config);
        cars.LayTheJoins(car, graph);
    }
}
