using System.Numerics;
using TrafficSimulation.Agents.Ambulance;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Evacuator;
using TrafficSimulation.Agents.Service;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Terrain;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>Where a leg begins and where it ends</b> (GEN-4f, SRV-5): a car leaves its bay knowing which way it is going,
/// turns into the bay it was sent to the first time it comes onto that bay's street, and a patrol stops on the
/// place it was sent to rather than driving through it — and drives into a bay over the road.
/// </summary>
/// <remarks>
/// <b>One run of <see cref="Towns.Built"/>, watched every tick</b> — the one town the suite lays with service
/// buildings, so the one with patrols — and read by each claim: every one holds at every tick of it.
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
    public void ACarLeavesABayOntoTheLaneItsRouteSetsOffDown() => Assert.Empty(Watched.LeftTheWrongWay);

    /// <summary>
    /// <b>A car that comes onto the street of the bay it was sent to turns into it before it leaves that street</b>
    /// (GEN-4f). A route ends at the first lane of that street it reaches, so a car driving on off it has passed a
    /// bay it was aimed at — and went round the block, to pass it again.
    /// </summary>
    [Fact]
    public void ACarTurnsIntoItsBayTheFirstTimeItComesOntoItsStreet() => Assert.Empty(Watched.DroveByTheBay);

    /// <summary>
    /// SRV-5: <b>a patrol that comes to the place on its beat stops on it</b>, and draws the next from there. A leg
    /// ends by the car standing where it got to; one that drove through its place was sent round the block to it
    /// again, every lap, until its clock ran out.
    /// </summary>
    [Fact]
    public void APatrolStopsOnThePlaceItWasSentTo() => Assert.Empty(Watched.DroveThroughThePlace);

    /// <summary>
    /// GEN-4f: <b>a car driving the first straight of its manoeuvre into a bay keeps both axles on drivable ground</b> —
    /// the straight is along the street, and what leaves it is the turn in. Shaped off a bend, the first straight ran
    /// on along the car's heading while the street curved away, across the pavement and into a shed.
    /// </summary>
    [Fact]
    public void ACarDrivesIntoABayOverTheRoad() =>
        Assert.True(Watched.ParkedOffTheRoad.Count == 0, string.Join(Environment.NewLine, Watched.ParkedOffTheRoad));

    static Watch Watched => BuiltTownWatched.Legs;

    /// <summary>
    /// Past the latest a patrol first sets out, and long enough after it for a car sent round a block to its bay to
    /// have come back onto its street — which, in this town, is what the shorter watch never saw.
    /// </summary>
    internal static readonly int WatchedTicks = (int)((Config.Service.FirstBeatAfterMaxS + WatchedAfterTheBeatS) / Config.TickSeconds);

    const float WatchedAfterTheBeatS = 360f;

    internal sealed class Watch
    {
        public readonly List<string> LeftTheWrongWay = [];
        public readonly List<string> DroveByTheBay = [];
        public readonly List<string> DroveThroughThePlace = [];
        public readonly List<string> ParkedOffTheRoad = [];

        readonly TownWorld _world;
        readonly GroundLocator _ground;
        readonly CarAction[] _wasDoing;
        readonly bool[] _wasOffTheRoad;
        readonly int[] _wasOnLane;
        readonly int[] _cameOntoTheStreetOf;
        readonly Vector2[] _placeM;
        readonly bool[] _reachedThePlace;
        readonly bool[] _stoodOnThePlace;

        internal Watch(TownWorld world)
        {
            _world = world;
            _ground = new GroundLocator(world.Plan, Config);
            var cars = world.Cars.Count;
            _wasDoing = new CarAction[cars];
            _wasOffTheRoad = new bool[cars];
            _wasOnLane = new int[cars];
            _cameOntoTheStreetOf = new int[cars];
            _placeM = new Vector2[cars];
            _reachedThePlace = new bool[cars];
            _stoodOnThePlace = new bool[cars];
            Array.Fill(_wasOnLane, CarFleet.NoLane);
            Array.Fill(_cameOntoTheStreetOf, CarFleet.NoBay);
            Array.Fill(_placeM, new Vector2(float.NaN));
        }

        /// <summary>Every car of the town as it stands after a tick.</summary>
        internal void SawEveryCar()
        {
            for (var car = 0; car < _world.Cars.Count; car++)
            {
                LeavingItsBay(car);
                OnItsBaysStreet(car);
                OnItsBeat(car);
                IntoItsBay(car);
                _wasDoing[car] = _world.Cars.Action[car];
                _wasOnLane[car] = _world.Cars.LaneOf(car);
            }
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

        /// <summary>Said once a stretch off the road, where it left it.</summary>
        void IntoItsBay(int car)
        {
            var cars = _world.Cars;
            var manoeuvres = _world.Manoeuvres;
            var parking = cars.Action[car] == CarAction.Park && manoeuvres.IsBegun(car) && manoeuvres.Piece[car] == 0
                          && manoeuvres.PieceOf(car, 0)[0] is { Curvature: 0f } straight && cars.ProgressM[car] < straight.LengthM;
            ref readonly var build = ref cars.BuildOf(car);
            var forward = cars.Forward[car];
            var rearM = CarFollower.RearAxleM(build, cars.PositionM[car], forward);
            var frontM = rearM + (forward * build.WheelbaseM);
            var off = parking && (!_ground.At(rearM).Drivable || !_ground.At(frontM).Drivable);
            if (off && !_wasOffTheRoad[car])
            {
                ParkedOffTheRoad.Add(
                    $"car {car} into bay {manoeuvres.Bay[car]} on piece {manoeuvres.Piece[car]} left the road at " +
                    $"({cars.PositionM[car].X:F1}, {cars.PositionM[car].Y:F1})");
            }

            _wasOffTheRoad[car] = off;
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
