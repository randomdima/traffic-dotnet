using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>Every holder under way states where it goes</b> (<see cref="TownWorld.StateIntents"/>): a town with cars, walkers
/// and trains, asked once a train is running.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P8)]
public class WayIntentsTests
{
    static readonly SimConfig Config = new() { Train = new TrainFigures { FirstLeastS = 0.1f, FirstMostS = 0.5f } };

    /// <summary>
    /// Every car on a line, every walker walking and every train running states a stretch of way it means to travel, in
    /// its own name and on a way of its own kind: a car on the carriageway, a walker on the pavement, a train on a track.
    /// </summary>
    [Fact]
    public void EveryHolderUnderWayStatesWhereItGoes()
    {
        using var town = new TownWorld(Towns.LayFresh(Towns.Brief(seed: 3, buildings: 80, people: 60, railTracks: 2)), Config);
        new SimLoop<TownWorld>(town, Config).Advance(240);
        Assert.True(town.Trains.Sent > 0, "no train was sent, so the rail roster had nothing to state");

        var intents = new WayIntents();
        town.StateIntents(intents);

        for (var car = 0; car < town.Cars.Count; car++)
        {
            if (town.Cars.Line[car].LaneCount > 0) AssertStated(town, intents, car, LaneRoster.Driving, WayKind.Lane, WayKind.Connector);
        }

        for (var person = 0; person < town.People.Count; person++)
        {
            if (town.People.Walking[person] && town.People.RouteCount[person] > 0)
            {
                AssertStated(town, intents, person, LaneRoster.Walking, WayKind.Footway, WayKind.Mitre);
            }
        }

        for (var train = 0; train < town.Trains.Count; train++)
        {
            if (town.Trains.Running(train)) AssertStated(town, intents, train, LaneRoster.Rail, WayKind.Rail);
        }
    }

    static void AssertStated(TownWorld town, WayIntents intents, int occupant, LaneRoster roster, params ReadOnlySpan<WayKind> kinds)
    {
        var key = LaneOccupancy.KeyOf(occupant, roster);
        var stated = false;
        foreach (var intent in intents.Rows)
        {
            if (intent.Key != key) continue;

            Assert.True(kinds.Contains(town.Ways.KindOf(intent.Way)), $"{roster} {occupant} states a {town.Ways.KindOf(intent.Way)}");
            stated = true;
        }

        Assert.True(stated, $"{roster} {occupant} is under way and states nothing");
    }
}
