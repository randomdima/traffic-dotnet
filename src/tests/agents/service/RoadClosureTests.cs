using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Service;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Service;

/// <summary>
/// Which lanes a closure holds, read off the suite's built city (SRV-9), and a closed lane refused by the tour
/// (SRV-10) — the road's own questions, asked with no car on it.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P5)]
[Collection(nameof(TownGeometryCollection))]
public class RoadClosureTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>No car coming down the road is left a lane whose one way on is closed</b> (SRV-9, SRV-10): every lane of
    /// the carriageway leading into a closure is either closed with it or leads somewhere else as well — so its
    /// entrance is never a bend, nor a car park's cut, whose arms are no way on (GEN-4h).
    /// </summary>
    [Fact]
    public void EveryLaneIntoAClosureIsClosedOrLeadsSomewhereElse()
    {
        var roads = RoadGraph.Build(Towns.Built, Config);
        Span<int> stretch = stackalloc int[Config.Service.ClosureMostLanes];

        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            if (roads.IsABayArm(lane)) continue;

            var laid = RoadClosure.Stretch(roads, lane, stretch);
            var closed = stretch[..laid];
            Assert.Contains(lane, closed.ToArray());

            // A closure walked back as far as its room reaches ends where the room does, which is a stretch of a
            // street and never a district (SRV-9).
            if (laid == stretch.Length) continue;

            foreach (var into in closed)
            {
                foreach (var arriving in roads.Places.LanesArriving(roads.Places.Starting(into)))
                {
                    if (roads.IsABayArm(arriving) || closed.Contains(arriving)) continue;
                    if (roads.ConnectorBetween(arriving, into) == RoadGraph.NoConnector) continue;

                    Assert.True(
                        Streets(roads, arriving, closed) > 0,
                        $"lane {arriving} leads only into the closure of lane {lane}, and is not closed with it");
                }
            }
        }
    }

    /// <summary>
    /// <b>The officer stands at the head of a single file down to the scene</b> (SRV-9): the entrance leads into the
    /// next lane of the closure and so on, and the scene's lane is reached from it.
    /// </summary>
    [Fact]
    public void AClosureIsEnteredAtItsHeadAndLeadsDownToTheScene()
    {
        var roads = RoadGraph.Build(Towns.Built, Config);
        Span<int> stretch = stackalloc int[Config.Service.ClosureMostLanes];

        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            if (roads.IsABayArm(lane)) continue;

            var laid = RoadClosure.Stretch(roads, lane, stretch);
            var at = 0;
            while (stretch[at] != lane)
            {
                Assert.True(at + 1 < laid, $"the closure of lane {lane} never reaches it from its entrance {stretch[0]}");
                Assert.NotEqual(RoadGraph.NoConnector, roads.ConnectorBetween(stretch[at], stretch[at + 1]));
                at++;
            }
        }
    }

    /// <summary>
    /// <b>A tour never turns into a closed lane</b> (SRV-10): with every way on but one closed, that one is drawn
    /// every time.
    /// </summary>
    [Fact]
    public void ATourNeverDrawsAClosedLane()
    {
        var roads = RoadGraph.Build(Towns.Built, Config);
        var closed = new bool[roads.LaneCount];
        var from = -1;
        for (var lane = 0; lane < roads.LaneCount && from < 0; lane++)
        {
            if (Streets(roads, lane) >= 2) from = lane;
        }

        Assert.True(from >= 0, "the built city has no lane with two streets off it");

        var open = -1;
        foreach (var onto in roads.LanesFrom(from))
        {
            if (roads.IsABayArm(onto)) continue;
            if (open < 0 && roads.LanesFrom(onto).Length > 0) open = onto;
            else closed[onto] = true;
        }

        var draw = new Rng(1, 2);
        for (var tour = 0; tour < 64; tour++)
        {
            Assert.Equal(open, LaneTour.NextLane(roads, Config, from, closed, ref draw));
        }
    }

    /// <summary>How many lanes of the carriageway a car on this one may leave for, the closed ones not counted.</summary>
    static int Streets(RoadGraph roads, int lane, ReadOnlySpan<int> closed = default)
    {
        var streets = 0;
        foreach (var onto in roads.LanesFrom(lane)) streets += roads.IsABayArm(onto) || closed.Contains(onto) ? 0 : 1;
        return streets;
    }
}
