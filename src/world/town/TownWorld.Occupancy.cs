using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;
using static TrafficSimulation.World.Road.LineWays;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The lane index</b> (<see cref="LaneOccupancy"/>): every body where its collider stands, every agent's
/// plan taken strongest first, and the two questions a driver asks of it — what is in front of me on the road I am
/// driving, and how much of that road is mine.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the whole of what an ordinary agent reads of another</b> (TER-4c.5). A driver or a walker is
/// held off another body by that body's reservation on its own way, and off another agent's intentions by
/// the planned layer — never by reading the other agent itself.
/// </para>
/// <para>
/// <b>Following is a grant and not a reading.</b> Every agent plans from its nose to where it means to be
/// able to stop, and is granted what survives of that once every body and every other plan has been laid —
/// so nothing has to measure a gap to hold one, and the agent behind simply has less road to stop in.
/// </para>
/// <para>
/// <b>The steps are in this file, in order</b>: every walker's place on its walk, every body, every pass and
/// manoeuvre, every light's hold, every plan, every backing, all of them answered, and last what each came to.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    const int MostWaysAlongALine = DrivingGround.MostWaysAlongALine;

    const int MostWaysUnderABody = RibbonAtlas.MostWaysUnderABody;

    /// <summary>What every car's action reads of the town (<see cref="DrivingGround"/>).</summary>
    readonly DrivingGround _ground;

    /// <summary>
    /// <b>The index rebuilt from the bodies</b>, in phase 2, before anybody has decided anything. Every reader
    /// this tick therefore sees the same reservations, whatever tick its own decision clock came round on.
    /// </summary>
    /// <remarks>
    /// <b>Every body first and every plan after all of them</b> (TER-4c.2): a plan is cut at the first body in
    /// front of it, so what it is laid against is the whole of the town's bodies rather than whichever were
    /// written first. Plans are asked, then answered in one pass strongest rung first
    /// (<see cref="LaneOccupancy.Answer"/>), and what each came to is read after that.
    /// </remarks>
    void RebuildLaneOccupancy()
    {
        var at = System.Diagnostics.Stopwatch.GetTimestamp();
        _layings++;
        _occupancy.Begin();

        // Where every walker stands on its walk, before anything is laid: its body and its plan both begin
        // from it.
        for (var person = 0; person < People.Count; person++) StationTheWalker(person);

        at = Spent(LayingStage.WalkersStationed, at);
        Span<WayCover> body = stackalloc WayCover[WalkingGround.MostPiecesOfABody];
        for (var person = 0; person < People.Count; person++) LayTheWalkersBody(person, body);

        // Every car's body, and from here on only the cars that were asked for theirs: a standing car's pass,
        // manoeuvre, plan, backing and grant are the nothing they were at the last laying.
        at = Spent(LayingStage.WalkersBodies, at);
        LayTheCarsBodies();
        at = Spent(LayingStage.CarsBodies, at);
        LayThePasses();

        // A light's hold is placed where it stands, as far as the first body on its way (TLT-1).
        at = Spent(LayingStage.Passes, at);
        LightTheWays();

        at = Spent(LayingStage.Lights, at);
        Span<LineWay> ways = stackalloc LineWay[MostWaysAlongALine];
        foreach (var car in CarsAsked) PlanTheDrive(car, ways);

        at = Spent(LayingStage.Drives, at);
        Span<LineWay> walk = stackalloc LineWay[WalkingGround.MostWaysAlongAWalk];
        for (var person = 0; person < People.Count; person++) PlanTheWalk(person, walk);

        // The ground a car backs up over is weaker than every plan (TER-4c.7), so it takes nothing any of them keeps.
        at = Spent(LayingStage.Walks, at);
        foreach (var car in CarsAsked) _backingUp.Lay(car, IsUnderWay(car));

        at = Spent(LayingStage.Backing, at);
        _occupancy.Answer();
        _signalHolds.NoteWhatHolds(_occupancy, _layings);

        at = Spent(LayingStage.Answer, at);
        foreach (var car in CarsAsked) _backingUp.Read(car);
        foreach (var car in CarsAsked) _ground.ReadTheGrant(car);
        ReadTheWalkersGrants();
        _layings++;
        Spent(LayingStage.Grants, at);
    }

    /// <summary>The stages of a laying, as <see cref="LayingTicks"/> times them.</summary>
    internal enum LayingStage
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

    /// <summary>The <see cref="System.Diagnostics.Stopwatch"/> time every laying has spent in each stage, for an instrument.</summary>
    public long[] LayingTicks { get; } = new long[Enum.GetValues<LayingStage>().Length];

    long Spent(LayingStage stage, long from)
    {
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        LayingTicks[(int)stage] += now - from;
        return now;
    }

    /// <summary>
    /// <b>How many times the layer has been begun or finished</b> — what a figure read off it and kept is checked against,
    /// so nothing read off one laying is read again off the next, nor off a laying half done.
    /// </summary>
    long _layings;

    /// <summary>
    /// <b>The ground a pass, a step across or a manoeuvre at a bay will cover, asked as a plan of its own</b> (TER-4c.6,
    /// GEN-4f) — before the plans, which carry on from those begun; one not yet begun is answered after every plan of
    /// its rung all the same (<see cref="LaneOccupancy.BeginHold"/>).
    /// </summary>
    void LayThePasses()
    {
        foreach (var car in CarsAsked) _overtaking.Lay(car, IsUnderWay(car));
        foreach (var car in CarsAsked) _switching.Lay(car, IsUnderWay(car));
        foreach (var car in CarsAsked) _bays.Lay(car);
        for (var person = 0; person < People.Count; person++) _sidestepping.Lay(person);
    }

    /// <summary>Every walker's grant read off what its walk came to, its pass's word taken, and its aim set on it.</summary>
    void ReadTheWalkersGrants()
    {
        for (var person = 0; person < People.Count; person++)
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

    /// <summary>The town's ways under a stretch of one car's line (<see cref="DrivingGround.WaysAlong"/>).</summary>
    public int WaysAlong(int car, float fromLineM, float toLineM, Span<LineWay> into) =>
        _ground.WaysAlong(car, fromLineM, toLineM, into);
}
