using System.Numerics;
using TrafficSimulation.Agents.Ambulance;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Evacuator;
using TrafficSimulation.Agents.Service;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>Where a leg begins and where it ends</b> (GEN-4f, SRV-5): a car leaves its bay knowing which way it is going,
/// turns into the bay it was sent to the first time it comes onto that bay's street, and a patrol stops on the
/// place it was sent to rather than driving through it.
/// </summary>
/// <remarks>
/// <b>One run of <see cref="Towns.Built"/>, watched every tick</b> — the one town the suite lays with service
/// buildings, so the one with patrols — and read by each claim: all three hold at every tick of it.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P2)]
public class LegEndsInATownTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// GEN-4f: <b>a car pulling out of a bay already holds its route, and pulls out onto the lane that route sets
    /// off down</b>. Chosen by where the destination lay as the crow flies, a car set off the wrong way and began
    /// its leg by turning round.
    /// </summary>
    [Fact]
    public void ACarLeavesABayOntoTheLaneItsRouteSetsOffDown() => Assert.Empty(Watched.Value.LeftTheWrongWay);

    /// <summary>
    /// <b>A car that comes onto the street of the bay it was sent to turns into it before it leaves that street</b>
    /// (GEN-4f). A route ends at the first lane of that street it reaches, so a car driving on off it has passed a
    /// bay it was aimed at — and went round the block, to pass it again.
    /// </summary>
    [Fact]
    public void ACarTurnsIntoItsBayTheFirstTimeItComesOntoItsStreet() => Assert.Empty(Watched.Value.DroveByTheBay);

    /// <summary>
    /// SRV-5: <b>a patrol that comes to the place on its beat stops on it</b>, and draws the next from there. A leg
    /// ends by the car standing where it got to; one that drove through its place was sent round the block to it
    /// again, every lap, until its clock ran out.
    /// </summary>
    [Fact]
    public void APatrolStopsOnThePlaceItWasSentTo() => Assert.Empty(Watched.Value.DroveThroughThePlace);

    static readonly Lazy<Watch> Watched = new(() => Watch.Run(WatchedTicks));

    /// <summary>
    /// Past the latest a patrol first sets out, and long enough after it for a car sent round a block to its bay to
    /// have come back onto its street — which, in this town, is what the shorter watch never saw.
    /// </summary>
    static readonly int WatchedTicks = (int)((Config.Service.FirstBeatAfterMaxS + WatchedAfterTheBeatS) / Config.TickSeconds);

    const float WatchedAfterTheBeatS = 360f;

    sealed class Watch
    {
        public readonly List<string> LeftTheWrongWay = [];
        public readonly List<string> DroveByTheBay = [];
        public readonly List<string> DroveThroughThePlace = [];

        readonly TownWorld _world;
        readonly CarAction[] _wasDoing;
        readonly int[] _wasOnLane;
        readonly int[] _cameOntoTheStreetOf;
        readonly Vector2[] _placeM;
        readonly bool[] _reachedThePlace;
        readonly bool[] _stoodOnThePlace;

        Watch(TownWorld world)
        {
            _world = world;
            var cars = world.Cars.Count;
            _wasDoing = new CarAction[cars];
            _wasOnLane = new int[cars];
            _cameOntoTheStreetOf = new int[cars];
            _placeM = new Vector2[cars];
            _reachedThePlace = new bool[cars];
            _stoodOnThePlace = new bool[cars];
            Array.Fill(_wasOnLane, CarFleet.NoLane);
            Array.Fill(_cameOntoTheStreetOf, CarFleet.NoBay);
            Array.Fill(_placeM, new Vector2(float.NaN));
        }

        public static Watch Run(int ticks)
        {
            using var world = new TownWorld(Towns.Built, Config);
            var loop = new SimLoop<TownWorld>(world, Config);
            var watch = new Watch(world);
            for (var tick = 0; tick < ticks; tick++)
            {
                loop.Advance(1);
                for (var car = 0; car < world.Cars.Count; car++)
                {
                    watch.LeavingItsBay(car);
                    watch.OnItsBaysStreet(car);
                    watch.OnItsBeat(car);
                    watch._wasDoing[car] = world.Cars.Action[car];
                    watch._wasOnLane[car] = world.Cars.LaneOf(car);
                }
            }

            return watch;
        }

        void LeavingItsBay(int car)
        {
            var cars = _world.Cars;
            if (cars.Action[car] != CarAction.Unpark || _wasDoing[car] == CarAction.Unpark) return;
            if (!cars.HasDestination[car] || _world.Manoeuvres.Kind[car] != ManoeuvreKind.Leave) return;

            var lane = _world.Manoeuvres.Lane[car];
            var holdsARoute = cars.RouteCount[car] > 0 || cars.RouteEndsOn[car] >= 0 || cars.TurnsBackOn[car] >= 0;
            var setsOffDownIt = cars.RouteCount[car] > 0
                ? _world.Roads.ConnectorBetween(lane, cars.RouteOf(car)[0]) != RoadGraph.NoConnector
                : cars.RouteEndsOn[car] == lane || cars.TurnsBackOn[car] == _world.Roads.LaneReverse[lane];

            if (!holdsARoute || !setsOffDownIt)
            {
                LeftTheWrongWay.Add(
                    $"car {car} pulled out of bay {_world.Manoeuvres.Bay[car]} onto lane {lane} holding " +
                    (holdsARoute ? $"a route that does not set off down it" : "no route"));
            }
        }

        void OnItsBaysStreet(int car)
        {
            var cars = _world.Cars;
            var bay = _world.Parking.ClaimedBayOf(car);
            var lane = cars.LaneOf(car);
            if (bay < 0 || !cars.Driven[car] || cars.Action[car] == CarAction.Park || bay != _cameOntoTheStreetOf[car])
            {
                _cameOntoTheStreetOf[car] = CarFleet.NoBay;
            }

            if (bay < 0 || !cars.Driven[car] || lane == _wasOnLane[car]) return;

            // Driven off the lane it came onto the street by, still aimed at that bay and not parking in it.
            if (_cameOntoTheStreetOf[car] == bay)
            {
                DroveByTheBay.Add($"car {car} drove off lane {_wasOnLane[car]}, which bay {bay} is worked off, onto lane {lane}");
                _cameOntoTheStreetOf[car] = CarFleet.NoBay;
                return;
            }

            // Onto the street from another lane of the road, and not out of a bay onto it — a car that pulled out
            // beside a bay it is sent to may be past its turn-in before it has moved.
            if (_wasOnLane[car] != CarFleet.NoLane && lane != CarFleet.NoLane && !float.IsNaN(_world.BayStreets.AtLaneM(bay, lane)))
            {
                _cameOntoTheStreetOf[car] = bay;
            }
        }

        void OnItsBeat(int car)
        {
            var cars = _world.Cars;
            if (!IsOnTheBeat(car) || cars.DestinationM[car] != _placeM[car])
            {
                _placeM[car] = IsOnTheBeat(car) ? cars.DestinationM[car] : new Vector2(float.NaN);
                _reachedThePlace[car] = false;
                _stoodOnThePlace[car] = false;
                if (!IsOnTheBeat(car)) return;
            }

            // Measured along and across the lane the place was drawn on, and only for a car driving that lane's way: a
            // car arriving on the other side of its street stands a lane off, and one passing it there is going round.
            var lane = _world.Roads.NearestStreetLane(_placeM[car], out var alongM);
            if (lane < 0) return;

            var at = Spline.SampleAt(_world.Roads.ArcsOf(lane), alongM);
            var offM = cars.PositionM[car] - _placeM[car];
            var acrossM = MathF.Abs((offM.X * at.Direction.Y) - (offM.Y * at.Direction.X));
            var pastM = Vector2.Dot(offM, at.Direction);
            var alongTheLane = Vector2.Dot(Heading.Unit(cars.HeadingRad[car]), at.Direction) > 0f;
            if (acrossM > Config.LaneWidthM * 0.5f || !alongTheLane) return;

            var lengthM = cars.BuildOf(car).LengthM;
            if (MathF.Abs(pastM) <= lengthM)
            {
                _reachedThePlace[car] = true;
                _stoodOnThePlace[car] |= cars.VelocityMps[car].Length() <= Config.Driving.StopSpeedMps;
            }
            else if (pastM > lengthM && _reachedThePlace[car] && !_stoodOnThePlace[car])
            {
                DroveThroughThePlace.Add($"patrol {car} drove through its place at ({_placeM[car].X:F0}, {_placeM[car].Y:F0}) on lane {lane}");
                _reachedThePlace[car] = false;
            }
        }

        bool IsOnTheBeat(int car) =>
            _world.ServiceBeat.Patrols[car]
            && (_world.Beat.Station[car] != PatrolDuty.NoBuilding ? _world.Beat.Stage[car] == PatrolStage.Patrolling
                : _world.Cars.Ambulance[car] ? _world.Duty.Stage[car] == RescueStage.Patrolling
                : _world.Recovery.Stage[car] == RecoveryStage.Patrolling);
    }
}
