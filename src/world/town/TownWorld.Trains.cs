using TrafficSimulation.Agents.Train.Body;
using TrafficSimulation.Agents.Train.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Rail;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The railway's side of the town</b> (TRN-1…8, RAIL-1): the trains run on and their wagons signalled to the world each
/// tick, and the level crossings held against the traffic while a train closes them.
/// </summary>
/// <remarks>
/// <b>A wagon is signalled every tick its train has it on the map</b> (<see cref="ControlAction.Run"/>), and taken out of the
/// world the tick it leaves: nothing the step does to a wagon is kept, so a wagon not signalled would coast. <b>A held town
/// holds its trains</b> (<see cref="HoldAgents"/>): they stand where they are, signalled at rest.
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>The town's trains (TRN-1…8).</summary>
    readonly Trains _trains;

    /// <summary>What each crossing holds while a train closes it (RAIL-1).</summary>
    readonly CrossingHolds _crossingHolds;

    /// <summary>Whether each wagon was last signalled into the world, so it is taken out of it once and not every tick.</summary>
    readonly bool[] _wagonSent;

    /// <summary>The channels each wagon was last put on, every one laid on the ground's.</summary>
    readonly byte[] _wagonChannels;

    /// <summary>What each crossing shows: its barriers and its lamps, and when it closed and opened (RAIL-7).</summary>
    readonly CrossingGear _crossingGear;

    /// <summary>The crossings' gear, for whatever draws it.</summary>
    public CrossingGear CrossingGear => _crossingGear;

    /// <summary>The town's trains, for whatever draws or reports them.</summary>
    public Trains Trains => _trains;

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

    /// <summary>Every wagon the trains can field, as the world lays them: out of the world until their train runs (TRN-7).</summary>
    WagonLaid[] WagonsLaid
    {
        get
        {
            var wagons = new WagonLaid[_trains.WagonCount];
            for (var wagon = 0; wagon < wagons.Length; wagon++)
            {
                var (halfSizeM, cornerRadiusM) = _trains.ShapeOf(wagon);
                wagons[wagon] = new WagonLaid(halfSizeM, cornerRadiusM);
            }

            return wagons;
        }
    }

    /// <summary>
    /// <b>Every train run on a tick</b>, and every wagon on the map signalled where it now is — or at rest where it stands, in
    /// a held town — and every one that has left it taken out of the world.
    /// </summary>
    void RunTheTrains()
    {
        if (!HoldAgents) _trains.Advance(_config.TickSeconds);
        _crossingGear.Update(_trains.Runs, ElapsedS);

        for (var wagon = 0; wagon < _wagonSent.Length; wagon++)
        {
            var agent = Roster.Count + wagon;
            if (_trains.InWorld(wagon))
            {
                _world.Signals.Add(ControlSignal.Run(
                    agent, _trains.PositionM(wagon), _trains.HeadingRad(wagon), HoldAgents ? 0f : _trains.SpeedMps(wagon),
                    _trains.LookOf(wagon)));
                if (_trains.ChannelsOf(wagon) != _wagonChannels[wagon])
                {
                    _world.Signals.Add(ControlSignal.Channels(agent, _trains.ChannelsOf(wagon)));
                    _wagonChannels[wagon] = _trains.ChannelsOf(wagon);
                }

                _wagonSent[wagon] = true;
                continue;
            }

            if (!_wagonSent[wagon]) continue;

            _world.Signals.Add(ControlSignal.Contain(agent));
            _wagonSent[wagon] = false;
        }
    }

    /// <summary>The crossing holds whose turn it is, laid against where the trains are (RAIL-1).</summary>
    void LayTheCrossings(TickGroups turns, long tick) => _crossingHolds.Lay(_occupancy, _trains.Runs, turns, tick);

    static Trains TrainsOf(CityPlan plan, SimConfig config, ulong agentSeed) =>
        new(plan.Rails, TrackLine.Of(plan.Rails), plan.WorldSizeM, TrainCatalog.Shared, config, agentSeed);
}
