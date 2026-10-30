using System.Numerics;
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
/// <b>The railway's side of the town</b> (TRN-3…12, RAIL-1, RAIL-9): the trains driven off where the world left their
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

    /// <summary>The town's trains (TRN-3…12).</summary>
    readonly Trains _trains;

    /// <summary>What the trains asked of the world this tick, and where the world last left each of their vehicles.</summary>
    readonly TrainOrders _trainOrders;

    readonly RailVehicleSeen[] _railSeen;

    /// <summary>
    /// Whether each train is the player's (CTL-4, CTL-5b): taken by the hand and, once it lets go, held braked where it was
    /// left rather than given back to its own driver, until the reset or until it is out of the world (TRN-7).
    /// </summary>
    readonly bool[] _trainManual;

    /// <summary>Each train's claim on its rail lanes (RAIL-9), or <see cref="LaneOccupancy.NoHold"/>.</summary>
    readonly int[] _trainHold;

    /// <summary>What each crossing holds while a train closes it (RAIL-1).</summary>
    readonly CrossingHolds _crossingHolds;

    /// <summary>What each crossing shows: its barriers and its lamps, and when it closed and opened (RAIL-7).</summary>
    readonly CrossingGear _crossingGear;

    /// <summary>Whether each arm's body was last signalled into the world.</summary>
    readonly bool[] _barrierBlocks;

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
                wagons[wagon] = new WagonLaid(vehicle.BodyM * 0.5f, vehicle.BufferM, vehicle.CornerRadiusM, RailBuild.Of(vehicle));
            }

            return wagons;
        }
    }

    /// <summary>Every level crossing's arm, as the world lays it: lying down across its lanes, and out of the world while up (RAIL-10).</summary>
    BarrierLaid[] BarriersLaid
    {
        get
        {
            var barriers = new BarrierLaid[_crossingGear.Count];
            for (var gear = 0; gear < barriers.Length; gear++)
            {
                barriers[gear] = new BarrierLaid(
                    _crossingGear.ArmMiddleM(gear), _crossingGear.ArmHeadingRad[gear],
                    new Vector2(_crossingGear.ArmM[gear], _config.Rail.BarrierWidthM) * 0.5f);
            }

            return barriers;
        }
    }

    /// <summary>
    /// <b>Every train driven on a tick</b> off where the world last left its vehicles, and what each asked signalled; the
    /// crossings' gear turned as the trains now stand, and every arm that started down or came all the way up signalled.
    /// </summary>
    void RunTheTrains()
    {
        var views = _world.View.Wagons;
        for (var wagon = 0; wagon < _railSeen.Length; wagon++)
        {
            ref readonly var view = ref views[wagon];
            _railSeen[wagon] = new RailVehicleSeen(view.PositionM, Heading.Unit(view.HeadingRad), view.VelocityMps, view.IsInWorld);
        }

        for (var train = 0; train < _trains.Count; train++)
        {
            if (!_trains.Running(train)) _trainManual[train] = false;

            if (PlayersHandOnTrain(train))
            {
                _trainManual[train] = true;
                _trains.Hand(train, ControlsOf(train, _hands));
            }
            else if (_players.SeatOfTrain(train) is var seat and >= 0) _trains.Hand(train, ControlsOf(train, _players.HandOf(seat)));
            else if (_trainManual[train]) _trains.Hand(train, StandingBraked);
            else if (_trains.Handed(train)) _trains.LetGo(train);
        }

        _trainOrders.Clear();
        _trains.Advance(_railSeen, _config.TickSeconds, HoldAgents, _trainOrders);
        foreach (ref readonly var order in _trainOrders.Written)
        {
            if (order.Kind is TrainOrderKind.Contain or TrainOrderKind.Place) LetGoOfTheWagon(order.Wagon);
            _world.Signals.Add(SignalOf(order));
        }

        // A train gone off the map is out of the world, and a unit nobody can see is not one to be talking about (TRN-7):
        // its locomotive and any of its wagons drop out of the selection.
        for (var at = _selected.Count - 1; at >= 0; at--)
        {
            var unit = _selected.Units[at];
            if (unit.Kind is SelectionKind.Locomotive or SelectionKind.Wagon && !Standing(unit).Any && _selected.Remove(unit)) GaveUpTheWheel();
        }

        _crossingGear.Update(_trains.Runs, ElapsedS);
        MoveTheBarriers();
    }

    /// <summary>
    /// <b>Each arm's body put down across its lanes as the arm starts down, and taken out of the world once it is all the
    /// way up</b> (RAIL-10): on the tick it turns, and on no other.
    /// </summary>
    void MoveTheBarriers()
    {
        var firstAgent = Roster.Count + _trains.WagonCount;
        for (var gear = 0; gear < _barrierBlocks.Length; gear++)
        {
            var blocks = _crossingGear.Blocks(gear, ElapsedS);
            if (blocks == _barrierBlocks[gear]) continue;

            _barrierBlocks[gear] = blocks;
            _world.Signals.Add(blocks
                ? ControlSignal.Place(firstAgent + gear, _crossingGear.ArmMiddleM(gear), _crossingGear.ArmHeadingRad[gear])
                : ControlSignal.Contain(firstAgent + gear));
        }
    }

    bool PlayersHandOnTrain(int train) => _hands.Held && _selected.Holds(SelectionKind.Locomotive, train) && _trains.Running(train);

    /// <summary>What a train the player has let go of is held by: no pull and the brake on, wherever it was left.</summary>
    static readonly TrainControls StandingBraked = new(0f, Reverse: false, Brake: true);

    /// <summary>The reset of one train (CTL-4): its own driver drives it again from wherever the hand left it.</summary>
    void ReleaseTrain(int train)
    {
        if (train < 0 || train >= _trains.Count) return;

        _trainManual[train] = false;
    }

    /// <summary>
    /// <b>The keys as a train's controls</b> (CTL-5, TRN-10), as a car's are its pedals: the drive key the way the train is
    /// going pulls, the other brakes it and, from a stop, pulls it back; Space brakes; the steer keys step the switch ahead.
    /// </summary>
    TrainControls ControlsOf(int train, in HandInput hand)
    {
        var alongMps = _trains.AlongMps(train);
        var stopMps = _config.Driving.StopSpeedMps;
        var reverse = hand.Throttle < 0f;
        var against = reverse ? alongMps > stopMps : alongMps < -stopMps;
        var brake = hand.Handbrake || (hand.Throttle != 0f && against);

        // A boost (CTL-5e) pulls past the locomotive's own tractive effort.
        var boost = hand.Boost ? _config.Control.HandBoostPullShare : 1f;
        return new TrainControls(brake ? 0f : MathF.Abs(hand.Throttle) * boost, reverse, brake, MathF.Sign(hand.Steer));
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
        _crossingHolds.Lay(_occupancy, _trains.Runs, turns, tick, ElapsedS);
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
            var aheadM = _trains.Reach(train, rail.WarningS, out var behindM);
            var tailM = _trains.FrontM(train) - _trains.LengthM(train) - behindM;
            var reachM = _trains.FrontM(train) + aheadM;
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

    /// <summary>The town's trains: sent, or on the showroom stood, every look once (<see cref="ShowroomPlan.ShowsEachLookOnce"/>).</summary>
    static Trains TrainsOf(CityPlan plan, RailGraph rails, SimConfig config, ulong agentSeed) =>
        new(rails, plan.Rails, plan.WorldSizeM, TrainCatalog.Shared, config, agentSeed, ShowroomPlan.ShowsEachLookOnce(plan.Name));
}
