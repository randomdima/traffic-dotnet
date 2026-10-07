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
/// plan settled against every other, and the two questions a driver asks of it — what is in front of me on
/// the road I am driving, and how much of that road is mine.
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
/// <b>The passes are in this file, in order</b>: every walker's place on its walk, every body, every light's
/// hold, every plan, every plan settled against what the others came to, and last what each plan came to.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    const int MostWaysAlongALine = DrivingGround.MostWaysAlongALine;

    const int MostWaysUnderABody = RibbonAtlas.MostWaysUnderABody;

    /// <summary>What every car's action reads of the town (<see cref="DrivingGround"/>).</summary>
    readonly DrivingGround _ground;

    /// <summary>
    /// <b>How many reservations one plan may lay</b>: a main claim on every way of its line, and a secondary
    /// claim for every mark those lie over (TER-5c.1).
    /// </summary>
    static int MostPlannedPer(int waysAlong, WayCrossings marks) => waysAlong * (1 + marks.MostCrossedByOne);

    /// <summary>
    /// <b>The index rebuilt from the bodies</b>, in phase 2, before anybody has decided anything. Every reader
    /// this tick therefore sees the same reservations, whatever tick its own decision clock came round on.
    /// </summary>
    /// <remarks>
    /// <b>Every body first and every plan after all of them</b> (TER-4c.2): a plan is cut at the first body in
    /// front of it, so what it is laid against is the whole of the town's bodies rather than whichever were
    /// written first. Plans are laid whole and answered against each other all at once, by a comparison that does
    /// not depend on the order (<see cref="LaneOccupancy.Beats"/>, <see cref="AnswerThePlans"/>), and what each came
    /// to is read after that.
    /// </remarks>
    void RebuildLaneOccupancy()
    {
        Watch(ClaimsStage.Begun);
        _occupancy.Begin();

        // Where every walker stands on its walk, before anything is laid: its body and its plan both begin
        // from it.
        for (var person = 0; person < People.Count; person++) StationTheWalker(person);

        Watch(ClaimsStage.Stationed);
        Span<BodyPiece> body = stackalloc BodyPiece[WalkingGround.MostPiecesOfABody];
        for (var person = 0; person < People.Count; person++) LayTheWalkersBody(person, body);

        Watch(ClaimsStage.WalkersLaid);

        // Every car's body, and from here on only the cars that were asked for theirs: a standing car's pass,
        // manoeuvre, plan, backing and grant are the nothing they were at the last laying.
        LayTheCarsBodies();
        Watch(ClaimsStage.CarsLaid);
        LayThePasses();
        Watch(ClaimsStage.PassesLaid);

        // The lights before any plan: a light's hold is placed rather than asked for, so it has to be down before
        // the plans it refuses are answered (TLT-1).
        LightTheWays();

        // What a hand or a truck cannot stop short of is laid where it stands before any plan (<see cref="AnswerThePlans"/>),
        // and every plan after them laid whole.
        Watch(ClaimsStage.PlansDue);
        Span<LineWay> ways = stackalloc LineWay[MostWaysAlongALine];
        foreach (var car in CarsAsked)
        {
            if (!PlansDownItsLine(car)) PlanTheDrive(car, ways);
        }

        Watch(ClaimsStage.DrivesDue);
        foreach (var car in CarsAsked)
        {
            if (PlansDownItsLine(car)) PlanTheDrive(car, ways);
        }

        Watch(ClaimsStage.WalksDue);
        Span<LineWay> walk = stackalloc LineWay[WalkingGround.MostWaysAlongAWalk];
        for (var person = 0; person < People.Count; person++) PlanTheWalk(person, walk);

        AnswerThePlans();

        // The ground a car backs up over is weaker than every plan (TER-4c.7), so it is asked once all of them are
        // settled and takes nothing any of them keeps — but what the car queued behind it could still stop short of.
        foreach (var car in CarsAsked) _backingUp.Lay(car, IsUnderWay(car));

        foreach (var car in CarsAsked) _ground.ReadTheGrant(car);
        Watch(ClaimsStage.WalkersDue);
        ReadTheWalkersGrants();
        Watch(ClaimsStage.GrantsRead);
    }

    /// <summary>
    /// <b>The ground a pass will cover is a body's</b> (TER-4c.6), and so is a manoeuvre's at a bay (GEN-4f): both are
    /// down before anything is asked for.
    /// </summary>
    void LayThePasses()
    {
        foreach (var car in CarsAsked) _overtaking.Lay(car, IsUnderWay(car));
        foreach (var car in CarsAsked) _switching.Lay(car, IsUnderWay(car));
        _bays.ForgetTheSweeps();
        foreach (var car in CarsAsked) _bays.Lay(car);
        for (var person = 0; person < People.Count; person++) _sidestepping.Lay(person);
    }

    /// <summary>Every walker's grant read off what its walk came to, its pass's word taken, and its aim set on it.</summary>
    /// <param name="granted">Whether a device read every walker's grant already (<see cref="ITownClaims.ReadTheGrants"/>).</param>
    /// <param name="keepLater">Whether a pass's word waits for its layer to land (<see cref="Sidestepping.Consider"/>).</param>
    void ReadTheWalkersGrants(bool granted = false, bool keepLater = false)
    {
        for (var person = 0; person < People.Count; person++)
        {
            if (granted)
            {
                var (laid, endsAtM, marginM, cutOn) = _walkGrantedOnTheDevice[person];
                _walkingGround.Grant(person, laid, endsAtM, marginM, cutOn);
            }
            else
            {
                _walkingGround.ReadTheGrant(person);
            }

            _sidestepping.Consider(person, keepLater);
            AimTheWalker(person);
        }
    }

    /// <summary>
    /// <b>How many rebuilds ran out of passes with a plan still moving</b> since the town was laid
    /// (<see cref="AnswerThePlans"/>) — a layer left disjoint but not where its answers say.
    /// </summary>
    public long Unsettled { get; private set; }

    /// <summary>How many times a plan's answer has moved from one pass to the next, past the first, since the town was laid.</summary>
    public long PlansLaidAgain { get; private set; }

    /// <summary>
    /// <b>Every plan answered at once, against what every other came to the pass before, until none moves</b>
    /// (<see cref="LaneOccupancy.ShowTheAnswers"/>) — so a plan ends where something that beats it still stands, and
    /// which of two plans keeps a piece of ground never turns on which was laid first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A plan refused anywhere is held to the rules of one refused</b>: a car refused anywhere between a zebra and
    /// the room clear of it waits short of the paint, whoever else wanted it (TER-5c.3).
    /// </para>
    /// <para>
    /// <b>The passes are bounded</b> (<see cref="RoadFigures.MostSettlingPasses"/>), and a ring is why. The
    /// comparison is total at one metre but not transitive along a line: one hold cut back frees ground a second
    /// takes, which cuts a third, which frees the ground the first was cut back from. Such a ring never settles, so
    /// the answers stop on a pass read against a longer layer than their own, which is disjoint, and the rebuild is
    /// counted. <b>It is an instrument's to report and not a gate's</b>: whether a town has rings is a fact about its
    /// junctions.
    /// </para>
    /// </remarks>
    void AnswerThePlans()
    {
        Watch(ClaimsStage.PlansLaid);
        PlansLaidAgain += _occupancy.AnswerThePlans(_config.Road.MostSettlingPasses, out var settled);
        if (!settled) Unsettled++;

        Watch(ClaimsStage.PlansAnswered);
    }

    /// <summary>
    /// Whether this car's claim is a plan down its own line, answered with every other plan — and not what a hand, a
    /// truck or a manoeuvre holds where the car stands.
    /// </summary>
    bool PlansDownItsLine(int car) =>
        Cars.Action[car] is not (CarAction.Hand or CarAction.Towed) && !IsManoeuvring(car) && IsUnderWay(car);

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
