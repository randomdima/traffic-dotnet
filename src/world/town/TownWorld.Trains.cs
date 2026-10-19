using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Train.Body;
using TrafficSimulation.Agents.Train.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Rail;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The railway's side of the town</b> (TRN-1…9, RAIL-1, RAIL-9): the trains driven off where the world left their
/// vehicles and what they ask signalled to it, each train's claim on its rail lanes, and the level crossings held
/// against the traffic while a train closes them.
/// </summary>
/// <remarks>
/// <b>A rail vehicle is an agent past the roster</b> (<see cref="WorldLaying.AgentOfWagon"/>): a body of the world on its
/// own wheels, driven by its train's driver here rather than by a turn of its own — a train decides every tick, and
/// there are few of them.
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>The railway as a network (RAIL-9).</summary>
    readonly RailGraph _rails;

    /// <summary>The town's trains (TRN-1…9).</summary>
    readonly Trains _trains;

    /// <summary>What the trains asked of the world this tick, and where the world last left each of their vehicles.</summary>
    readonly TrainOrders _trainOrders = new();

    readonly RailVehicleSeen[] _railSeen;

    /// <summary>Each train's claim on its rail lanes (RAIL-9), or <see cref="LaneOccupancy.NoHold"/>.</summary>
    readonly int[] _trainHold;

    /// <summary>What each crossing holds while a train closes it (RAIL-1).</summary>
    readonly CrossingHolds _crossingHolds;

    /// <summary>What each crossing shows: its barriers and its lamps, and when it closed and opened (RAIL-7).</summary>
    readonly CrossingGear _crossingGear;

    /// <summary>The crossings' gear, for whatever draws it.</summary>
    public CrossingGear CrossingGear => _crossingGear;

    /// <summary>The town's trains, for whatever draws or reports them.</summary>
    public Trains Trains => _trains;

    /// <summary>The railway as a network, for whatever draws or reports it.</summary>
    public RailGraph Rails => _rails;

    /// <summary>How many stretches of the town's ways its crossings hold while closed, and how many are held now — for an instrument.</summary>
    public int CrossingStretches => _crossingHolds.Count;

    public int CrossingStretchesHeld => _crossingHolds.HeldCount;

    /// <summary>How many cars' plans and walkers' walks a crossing's hold cuts now (RAIL-1): an instrument's walk of every one.</summary>
    public (int Cars, int Walkers) HeldByACrossing
    {
        get
        {
            var (cars, walkers) = (0, 0);
            for (var car = 0; car < Cars.Count; car++) cars += ByACrossing(_occupancy.CutOf(_carHold[car])) ? 1 : 0;
            for (var person = 0; person < People.Count; person++) walkers += ByACrossing(_occupancy.CutOf(_walkingGround.WalkHold[person])) ? 1 : 0;
            return (cars, walkers);

            static bool ByACrossing(in Refusal cut) => cut.ByALight && cut.Rung == ClaimPriority.Hard;
        }
    }

    /// <summary>Every rail vehicle the trains can field, as the world lays them: out of the world until their train runs (TRN-7).</summary>
    WagonLaid[] WagonsLaid
    {
        get
        {
            var wagons = new WagonLaid[_trains.WagonCount];
            for (var wagon = 0; wagon < wagons.Length; wagon++)
            {
                ref readonly var vehicle = ref _trains.VehicleOf(wagon);
                wagons[wagon] = new WagonLaid(vehicle.FootprintM * 0.5f, vehicle.CornerRadiusM, RailBuild.Of(vehicle));
            }

            return wagons;
        }
    }

    /// <summary>
    /// <b>Every train driven on a tick</b> off where the world last left its vehicles, and what each asked signalled; the
    /// crossings' gear turned as the trains now stand.
    /// </summary>
    void RunTheTrains()
    {
        var views = _world.View.Wagons;
        for (var wagon = 0; wagon < _railSeen.Length; wagon++)
        {
            ref readonly var view = ref views[wagon];
            _railSeen[wagon] = new RailVehicleSeen(view.PositionM, Heading.Unit(view.HeadingRad), view.VelocityMps, view.IsInWorld);
        }

        _trainOrders.Clear();
        _trains.Advance(_railSeen, _config.TickSeconds, HoldAgents, _trainOrders);
        foreach (ref readonly var order in _trainOrders.Written) _world.Signals.Add(SignalOf(order));

        _crossingGear.Update(_trains.Runs, ElapsedS);
    }

    ControlSignal SignalOf(in TrainOrder order)
    {
        var agent = order.Kind == TrainOrderKind.Switch ? Roster.Count : Roster.Count + order.Wagon;
        return order.Kind switch
        {
            TrainOrderKind.Place => ControlSignal.Place(agent, order.AtM, order.HeadingRad, order.SpeedMps, order.Other),
            TrainOrderKind.Contain => ControlSignal.Contain(agent),
            TrainOrderKind.Couple => ControlSignal.Couple(agent, order.Other < 0 ? -1 : Roster.Count + order.Other),
            TrainOrderKind.Drive => ControlSignal.Drive(agent, order.Command, underAHand: false),
            TrainOrderKind.Switch => ControlSignal.Switch(agent, order.Wagon, order.Other),
            _ => ControlSignal.Channels(agent, (byte)order.Other),
        };
    }

    /// <summary>The crossing holds whose turn it is, laid against where the trains are (RAIL-1), and every train's own claim.</summary>
    void LayTheCrossings(TickGroups turns, long tick)
    {
        _crossingHolds.Lay(_occupancy, _trains.Runs, turns, tick);
        LayTheTrainsClaims();
    }

    /// <summary>
    /// <b>Each running train's claim on its rail lanes</b> (RAIL-9): from its tail to a warning's run ahead of its front, at
    /// the top of the ladder, as a call claims its road — laid on the lanes alone, since a crossing's own hold is what
    /// keeps the road clear of it, ended at the first body on the road's approach (RAIL-6). A train reads no refusal.
    /// </summary>
    void LayTheTrainsClaims()
    {
        var rail = _config.Rail;
        for (var train = 0; train < _trains.Count; train++)
        {
            _occupancy.Release(_trainHold[train]);
            _trainHold[train] = LaneOccupancy.NoHold;
            if (!_trains.Running(train)) continue;

            var hold = _trainHold[train] = _occupancy.BeginHold(train, LaneRoster.Rail);
            var tailM = _trains.FrontM(train) - _trains.LengthM(train);
            var reachM = _trains.FrontM(train) + (_trains.SpeedMps(train) * rail.WarningS);
            var leg = _trains.LegOf(train);
            for (var lane = 0; lane < leg.Length; lane++)
            {
                var startM = _trains.LegStartM(train, lane);
                var lengthM = _rails.LengthM(leg[lane]);
                var fromM = MathF.Max(tailM - startM, 0f);
                var toM = MathF.Min(reachM - startM, lengthM);
                if (toM > fromM) _occupancy.Place(hold, _ways.OfRail(leg[lane]), fromM, toM, ClaimPriority.Hard);
            }
        }
    }

    static Trains TrainsOf(CityPlan plan, RailGraph rails, SimConfig config, ulong agentSeed) =>
        new(rails, plan.Rails, plan.WorldSizeM, TrainCatalog.Shared, config, agentSeed);
}
