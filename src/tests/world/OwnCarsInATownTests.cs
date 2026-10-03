using System.Numerics;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// PER-29: <b>everybody who lives here owns a car of their own, and nobody walks further than a few blocks</b> —
/// a door further off is driven to, in their own car, from a bay within a walk of them to a bay within a walk of
/// the door.
/// </summary>
/// <remarks>
/// <b>One run of <see cref="Towns.Built"/>, watched every tick</b> — the one town the suite stands people in — and
/// read by each claim at the moment a leg of a trip begins.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P2)]
public class OwnCarsInATownTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// Each car somebody owns is theirs and nobody else's, standing in a bay within a walk of their door — and every
    /// car of a town people live in, but for its service vehicles, is somebody's.
    /// </summary>
    [Fact]
    public void EverybodyOwnsACarOfTheirOwnParkedWithinAWalkOfTheirDoor()
    {
        using var world = new TownWorld(Towns.Built, Config);

        var owned = 0;
        for (var person = 0; person < world.People.Count; person++)
        {
            var car = world.People.Car[person];
            if (car == PersonFleet.NoCar) continue;

            owned++;
            Assert.Equal(person, world.Cars.Owner[car]);
            Assert.True(world.Parking.BayOf(car) >= 0, $"person {person}'s car {car} stands in no bay");

            var apartM = Vector2.Distance(world.Cars.PositionM[car], world.People.PositionM[person]);
            Assert.True(
                apartM <= Config.PersonWalkReachM,
                $"person {person}'s car {car} stands {apartM:F0} m from their door, past a walk of {Config.PersonWalkReachM:F0} m");
        }

        Assert.Equal(world.Cars.Count - world.Ambulances - world.PoliceCars - world.Evacuators, owned);
    }

    /// <summary>A trip walked is to a door within a walk of where it set off.</summary>
    [Fact]
    public void AWalkedTripIsToADoorWithinAWalk() => Assert.Empty(Watched.Value.WalkedTooFar);

    /// <summary>The walk to a car is to a car within a walk.</summary>
    [Fact]
    public void AWalkToTheCarIsToACarWithinAWalk() => Assert.Empty(Watched.Value.CarTooFar);

    /// <summary>Whoever drives a trip drives it in their own car, at the wheel.</summary>
    [Fact]
    public void ADrivenTripIsDrivenInTheirOwnCar()
    {
        Assert.True(Watched.Value.Driven > 0, "nobody of the town drove anywhere, so nothing was asked");
        Assert.Empty(Watched.Value.NotTheirCar);
    }

    /// <summary>And the bay they get out at is within a walk of the door they drove for.</summary>
    [Fact]
    public void ADrivenTripEndsInABayWithinAWalkOfTheDoor()
    {
        Assert.True(Watched.Value.GotOut > 0, "nobody of the town got out of a car they had parked, so nothing was asked");
        Assert.Empty(Watched.Value.ParkedTooFar);
    }

    static readonly Lazy<Watch> Watched = new(() => Watch.Run(WatchedTicks));

    /// <summary>
    /// Ten minutes: past the longest first dwell, and long enough after it for a walk to a car, a drive and the walk
    /// from the bay.
    /// </summary>
    static readonly int WatchedTicks = (int)(600f / Config.TickSeconds);

    sealed class Watch
    {
        public readonly List<string> WalkedTooFar = [];
        public readonly List<string> CarTooFar = [];
        public readonly List<string> NotTheirCar = [];
        public readonly List<string> ParkedTooFar = [];
        public int Driven;
        public int GotOut;

        readonly TownWorld _world;
        readonly TripStage[] _was;

        Watch(TownWorld world)
        {
            _world = world;
            _was = new TripStage[world.People.Count];
            for (var person = 0; person < world.People.Count; person++) _was[person] = world.People.Stage[person];
        }

        public static Watch Run(int ticks)
        {
            using var world = new TownWorld(Towns.Built, Config);
            var loop = new SimLoop<TownWorld>(world, Config);
            var watch = new Watch(world);
            for (var tick = 0; tick < ticks; tick++)
            {
                loop.Advance(1);
                for (var person = 0; person < world.People.Count; person++) watch.Saw(person);
            }

            return watch;
        }

        void Saw(int person)
        {
            var people = _world.People;
            var stage = people.Stage[person];
            var was = _was[person];
            _was[person] = stage;
            if (stage == was) return;

            switch (stage)
            {
                case TripStage.WalkingFromTheCar:
                    GotOut++;
                    if (ToTheDoorM(person) > Config.PersonWalkReachM) ParkedTooFar.Add(Said(person, "got out", ToTheDoorM(person)));
                    return;

                case TripStage.WalkingToTheDoor:
                    if (ToTheDoorM(person) > Config.PersonWalkReachM) WalkedTooFar.Add(Said(person, "set off walking", ToTheDoorM(person)));
                    return;

                case TripStage.WalkingToTheCar:
                    var carM = Vector2.Distance(_world.Cars.PositionM[people.Car[person]], people.PositionM[person]);
                    if (carM > Config.PersonWalkReachM) CarTooFar.Add($"person {person} set off for a car {carM:F0} m away");
                    return;

                case TripStage.Driving:
                    Driven++;
                    var car = people.Car[person];
                    if (car == PersonFleet.NoCar || people.Inside[person] != new Contained(ContainerKind.Car, car))
                    {
                        NotTheirCar.Add($"person {person} is driving and is in {people.Inside[person]}, owning car {car}");
                    }

                    return;
            }
        }

        /// <summary>From where the body stands to the nearest way in of the building its trip is for.</summary>
        float ToTheDoorM(int person)
        {
            var buildings = Towns.Built.Buildings;
            var building = _world.People.DestinationBuilding[person];
            var nearestM = float.PositiveInfinity;
            for (var entry = buildings.EntryOffsets[building]; entry < buildings.EntryOffsets[building + 1]; entry++)
            {
                nearestM = MathF.Min(nearestM, Vector2.Distance(buildings.EntryPointM[entry], _world.People.PositionM[person]));
            }

            return nearestM;
        }

        string Said(int person, string what, float farM) =>
            $"person {person} {what} {farM:F0} m from the door of building {_world.People.DestinationBuilding[person]}";
    }
}
