using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The claims</b> (<see cref="LaneOccupancy"/>): every agent's body where its collider stands and the ground it plans
/// to use, held by the agent itself, and the two questions a driver asks of them — what is in front of me on the road I
/// am driving, and how much of that road is mine.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the whole of what an ordinary agent reads of another</b> (TER-4c.5). A driver or a walker is
/// held off another body by that body's reservation on its own way, and off another agent's intentions by
/// the planned layer — never by reading the other agent itself.
/// </para>
/// <para>
/// <b>Following is a grant and not a reading.</b> Every agent plans from its nose to where it means to be
/// able to stop, and is granted what survives of that against every body and every other plan held — so nothing has
/// to measure a gap to hold one, and the agent behind simply has less road to stop in.
/// </para>
/// <para>
/// <b>There is no laying</b> (root decision log, 2026-10-14): each agent claims on its own turn — its decision's, every
/// <see cref="SimFigures.AgentDecisionIntervalS"/> — and holds what it claimed until then. Nothing else touches it but
/// what takes it or comes to stand on it, which it reads at its next turn. <b>The steps of a turn are in this file, in
/// order</b>: each walker's place on its walk, each body, each pass and manoeuvre, each light's hold, each plan, each
/// backing, all of them answered, and last what each came to.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    const int MostWaysAlongALine = DrivingGround.MostWaysAlongALine;

    const int MostWaysUnderABody = RibbonAtlas.MostWaysUnderABody;

    /// <summary>What every car's action reads of the town (<see cref="DrivingGround"/>).</summary>
    readonly DrivingGround _ground;

    /// <summary>The walkers whose turn it is, in roster order.</summary>
    readonly int[] _walkersInTurn;

    int _walkersInTurnCount;

    ReadOnlySpan<int> WalkersInTurn => _walkersInTurn.AsSpan(0, _walkersInTurnCount);

    /// <summary>The cars whose turn it is, in roster order.</summary>
    readonly int[] _carsInTurn;

    int _carsInTurnCount;

    ReadOnlySpan<int> CarsInTurn => _carsInTurn.AsSpan(0, _carsInTurnCount);

    /// <summary>
    /// <b>The agents whose turn it is, claiming</b>, once they have decided (<see cref="DecideTheAgents"/>) — so each
    /// claims down the line it has just decided on, and reads what it came to before its next tick. Every body first and
    /// every plan after them (TER-4c.2), the plans asked and answered together, strongest rung first
    /// (<see cref="LaneOccupancy.Answer"/>), against everything held; what each came to read last.
    /// </summary>
    /// <param name="everyone">
    /// Every agent and every light at once — the claims laid off their clock (<see cref="LayTheClaims"/>).
    /// </param>
    /// <param name="tick">The tick, which says whose turn it is.</param>
    void TakeTheTurns(bool everyone, long tick)
    {
        var at = System.Diagnostics.Stopwatch.GetTimestamp();
        _turnEdges++;
        var turns = everyone ? new TickGroups(0f, 1) : _decisions;
        TheAgentsInTurn(turns, tick);

        // Where each walker stands on its walk, before anything is laid: its body and its plan both begin from it.
        foreach (var person in WalkersInTurn) StationTheWalker(person);

        at = Spent(TurnStage.WalkersStationed, at);
        Span<WayCover> body = stackalloc WayCover[WalkingGround.MostPiecesOfABody];
        foreach (var person in WalkersInTurn) LayTheWalkersBody(person, body);

        // A standing car's pass, manoeuvre, plan, backing and grant are the nothing they were at its last turn.
        at = Spent(TurnStage.WalkersBodies, at);
        LayTheCarsBodies();
        at = Spent(TurnStage.CarsBodies, at);
        LayThePasses();

        // A light's hold is placed where it stands, as far as the first body on its way (TLT-1).
        at = Spent(TurnStage.Passes, at);
        _signalHolds.Lay(_occupancy, _signals, _elapsedS, turns, tick);
        LayTheCrossings(turns, tick);

        at = Spent(TurnStage.Lights, at);
        Span<LineWay> ways = stackalloc LineWay[MostWaysAlongALine];
        var asked = CarsAsked;
        for (var slot = 0; slot < asked.Length; slot++)
        {
            if (slot + PrefetchAhead < asked.Length)
            {
                _occupancy.Prefetch(_carHold[asked[slot + PrefetchAhead]]);
                Cars.PrefetchTheLine(asked[slot + PrefetchAhead]);
            }

            PlanTheDrive(asked[slot], ways);
        }

        at = Spent(TurnStage.Drives, at);
        Span<LineWay> walk = stackalloc LineWay[WalkingGround.MostWaysAlongAWalk];
        foreach (var person in WalkersInTurn) PlanTheWalk(person, walk);

        // The ground a car backs up over is weaker than every plan (TER-4c.7), so it takes nothing any of them keeps.
        at = Spent(TurnStage.Walks, at);
        foreach (var car in CarsAsked) _backingUp.Lay(car, IsUnderWay(car));

        at = Spent(TurnStage.Backing, at);
        _occupancy.Answer();
        _signalHolds.NoteWhatHolds(_occupancy, _turnEdges, turns, tick);

        at = Spent(TurnStage.Answer, at);
        foreach (var car in CarsAsked) _backingUp.Read(car);
        foreach (var car in CarsAsked) _ground.ReadTheGrant(car);
        ReadTheWalkersGrants();
        _turnEdges++;
        Spent(TurnStage.Grants, at);
    }

    /// <summary>
    /// How many cars ahead of the one being ticked, laid or planned a walk of the fleet asks memory for what that car's will
    /// read (<see cref="CarFleet.PrefetchTheLine"/>, <see cref="LaneOccupancy.Prefetch"/>) — eight, measured against four
    /// and sixteen on O30.
    /// </summary>
    const int PrefetchAhead = 8;

    /// <summary>The walkers and the cars of the roster whose turn <paramref name="tick"/> is.</summary>
    void TheAgentsInTurn(TickGroups turns, long tick)
    {
        _walkersInTurnCount = 0;
        _carsInTurnCount = 0;
        foreach (var agent in turns.InTurn(tick, Roster.Count))
        {
            if (Roster.IsCar(agent)) _carsInTurn[_carsInTurnCount++] = Roster.CarIndex(agent);
            else _walkersInTurn[_walkersInTurnCount++] = agent;
        }
    }

    /// <summary>The stages of a turn, as <see cref="TurnStageTicks"/> times them.</summary>
    internal enum TurnStage
    {
        WalkersStationed,
        WalkersBodies,
        CarsBodies,
        Passes,
        Lights,
        Drives,
        Walks,
        Backing,
        Answer,
        Grants,
    }

    /// <summary>The <see cref="System.Diagnostics.Stopwatch"/> time every tick's turns have spent in each stage, for an instrument.</summary>
    public long[] TurnStageTicks { get; } = new long[Enum.GetValues<TurnStage>().Length];

    /// <summary>How many car plans have been kept as they were, asked further, or asked afresh at a turn, for an instrument.</summary>
    public ReadOnlySpan<long> PlansKept => _ground.PlansKept;

    /// <summary>How many walks have been kept as they were, asked further, or asked afresh at a turn, for an instrument.</summary>
    public ReadOnlySpan<long> WalksKept => _walkingGround.WalksKept;

    long Spent(TurnStage stage, long from)
    {
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        TurnStageTicks[(int)stage] += now - from;
        return now;
    }

    /// <summary>
    /// <b>How many times the turns have been begun or finished</b> — what a figure read off the claims and kept is checked
    /// against, so nothing read off one tick's is read again off the next, nor off turns half taken.
    /// </summary>
    long _turnEdges;

    /// <summary>
    /// <b>The ground a pass, a step across or a manoeuvre at a bay will cover, asked as a plan of its own</b> (TER-4c.6,
    /// GEN-4f) — before the plans, which carry on from those begun; one not yet begun is answered after every plan of
    /// its rung asked with it (<see cref="LaneOccupancy.BeginHold"/>).
    /// </summary>
    void LayThePasses()
    {
        foreach (var car in CarsAsked) _overtaking.Lay(car, IsUnderWay(car));
        foreach (var car in CarsAsked) _switching.Lay(car, IsUnderWay(car));
        foreach (var car in CarsAsked) _bays.Lay(car);
        foreach (var person in WalkersInTurn) _sidestepping.Lay(person);
    }

    /// <summary>Each walker's grant read off what its walk came to, its pass's word taken, and its aim set on it.</summary>
    void ReadTheWalkersGrants()
    {
        foreach (var person in WalkersInTurn)
        {
            _walkingGround.ReadTheGrant(person);
            _sidestepping.Consider(person);
            AimTheWalker(person);
        }
    }

    /// <summary>
    /// Whether this car is a driver on a route rather than a shape on the road — what decides whether it
    /// plans down its line, and whether the ways of its own line are ways it is travelling.
    /// </summary>
    /// <remarks>
    /// <b>Its own action and nobody else's</b> (<see cref="DrivesTheRoute"/>), and on its line: how far off it the
    /// car is was measured by the sensing half of last tick.
    /// </remarks>
    bool IsUnderWay(int car) =>
        DrivesTheRoute(car) && Cars.Line[car].LaneCount > 0 && Cars.OffLineM[car] <= OffTheLineAllowanceM(car);

    /// <summary>The line any one of the town's ways is travelled on, and how wide it is (<see cref="WayLines"/>).</summary>
    public ReadOnlySpan<ArcSeg> LineOfWay(int way, out float widthM) => _lines.LineOf(way, out widthM);

    /// <summary>The zebra one of the town's ways is the paint of, or <see cref="RibbonMarks.NoZebra"/>.</summary>
    int ZebraOf(int way) => _lines.ZebraOf(way);
}
