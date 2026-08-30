using System.Numerics;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The pavement's own index</b>: who is on each way of the footway and which stretch of it each walker
/// has been granted, laid once a tick from the bodies themselves — and the one question a walker asks of
/// it, which is how much of the pavement in front of it is its own.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the lane index's arithmetic over the other network</b> (<see cref="LaneOccupancy"/>), and
/// deliberately not a second mechanism: every driver asks for the road from its own tail to where it plans
/// to stop and is granted what is left of it in front of the nearest body already on it, and a walker asks
/// for the pavement on exactly those terms. <b>Nobody is granted ground somebody else will still be
/// standing on once they have stopped</b>, which is the whole of what holds one body off the next.
/// </para>
/// <para>
/// <b>What a walker does with the grant is where the two part company.</b> A driver hands the distance to a
/// speed profile; a walker has none (PER-3) — its pace is a cap and never a curve — so the grant is read as
/// a permission: it walks while there is ground granted to walk into and stands while there is not. It is a
/// difference in what reads the answer rather than in how the answer is arrived at.
/// </para>
/// <para>
/// <b>And what it is asked about is one use narrower</b> (PER-24, <see cref="LaneOccupancy.UnderWay"/>). A
/// body going nowhere cuts no walk: the same walk that takes the grant picks it out
/// (<see cref="PersonFleet.StepsRound"/>) and the feet go round it, so the book answers the same question
/// with the same arithmetic and one of the two answers it gives is a step rather than a stop.
/// </para>
/// <para>
/// <b>A way is one side of one stretch</b>, or the mitre between two of them. The two directions of a
/// pavement are two lines half a band apart (<see cref="WalkingNetwork.LaneOffsetM"/>), so somebody coming
/// the other way is on other ground and is not something to queue behind — which falls out of the ways
/// rather than being tested for.
/// </para>
/// <para>
/// <b>Where a body stands is read off the line it is walking and never searched for.</b> Every point of a
/// walked line carries the way it was stationed on and how far along that way it stands
/// (<see cref="WalkedLine"/>), so a walker's place on the network costs a subtraction. Only a body that is
/// on no line at all — standing about, knocked over, under a hand — is looked up, and it goes in as the
/// obstruction it is.
/// </para>
/// <para>
/// <b>Nothing but a walker is ever written into it</b> (TER-5c.1). A zebra is a walk laid over a
/// carriageway, so the ground under it has two names and one owner: the car's stretch of it is a stretch of
/// the <em>lane</em>, and what stops a body walking into it is that stretch, looked up where the crossing
/// runs over the lane (<see cref="WhereTheWalkRunsOut"/>). Marked here as well, a car held one body
/// twice over one piece of ground, in two books whose answers could differ.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>
    /// How many ways one walker's stretch of pavement may be cut into: the ways behind it — the margin it
    /// keeps reaches back over a mitre and onto the stretch before it — the one it stands on, and the ones
    /// its reservation runs over. A bound on a stack span and not a figure behaviour reads.
    /// </summary>
    /// <remarks>
    /// <b>Reached, the ways at the far end go unwritten</b>, and a walker's ground in front of it missing
    /// from the book is somebody else granted it. A corner is two short ways within a stride, and the margin
    /// now reaches back over one of them, so the count is what a body on a corner can cover rather than what
    /// a stretch of pavement suggests.
    /// </remarks>
    const int MostWaysAlongAWalk = 5;

    /// <summary>How many stretches one walker may put in the book at once, which is one per way it covers.</summary>
    const int MostSlotsPerWalker = MostWaysAlongAWalk;

    /// <summary>
    /// The book laid over the walking network's own ways — a lane each way down every stretch, then the
    /// mitres — in the numbering <see cref="LaneOccupancy.WayOfLane"/> and
    /// <see cref="LaneOccupancy.WayOfTurn"/> hand out.
    /// </summary>
    static LaneOccupancy BookOfPavement(WalkingNetwork walking, int mostSlots)
    {
        var lanesM = new float[walking.Foot.EdgeCount];
        for (var edge = 0; edge < lanesM.Length; edge++) lanesM[edge] = walking.LaneLengthM(edge);

        var mitresM = new float[walking.TurnCount];
        for (var turn = 0; turn < mitresM.Length; turn++) mitresM[turn] = walking.JoinLengthM(turn);

        return new LaneOccupancy(lanesM, mitresM, mostSlots);
    }

    /// <summary>
    /// <b>The book rebuilt from the bodies</b>, in phase 2, before any walker has decided anything — asked
    /// in one walk and granted in the next, so that what a walker is granted is a fact about where the
    /// bodies are and never about which of them was served first.
    /// </summary>
    void RebuildFootOccupancy()
    {
        _footfall.Begin();

        Span<LineWay> ways = stackalloc LineWay[MostWaysAlongAWalk];
        for (var person = 0; person < People.Count; person++) AskForThePavement(person, ways);

        for (var person = 0; person < People.Count; person++) GrantThePavement(person, ways);
    }

    /// <summary>
    /// <b>Where this walker stands on the pavement's own network</b>, or <see cref="PersonFleet.NoWay"/>
    /// where it is on none of it.
    /// </summary>
    /// <remarks>
    /// <b>Worked out before either book is laid, because both read it</b> (<see cref="RebuildLaneOccupancy"/>):
    /// the road's book needs the way a body on a crossing is walking to know which lane it stands in, and
    /// the pavement's own ask begins from the same place. Asked twice it was the same walk of the same
    /// line, and the two answers were a tick apart.
    /// </remarks>
    void StationTheWalker(int person)
    {
        People.OnWay[person] = PersonFleet.NoWay;

        // PHY-7: inside a container there is no body in the world and nothing in anybody's way.
        if (People.Inside[person].Any) return;
        if (!IsAfoot(person, out var way, out var alongM)) return;

        People.OnWay[person] = way;
        People.OnWayM[person] = alongM;
    }

    /// <summary>
    /// <b>The pavement this walker asks for</b>: from its own back to where its front may stop at the pace
    /// it is walking, plus the gap it keeps. A body that is not walking a line of its own asks for nothing
    /// and goes in where it lies.
    /// </summary>
    /// <remarks>
    /// <b>The body needs no stretch of its own</b>, here or on the road: a reservation already begins at its
    /// owner's back, so where the body ends is an edge of that stretch (<see cref="LaneSlot.StandsToM"/>)
    /// and never a second one laid over it.
    /// </remarks>
    void AskForThePavement(int person, Span<LineWay> ways)
    {
        People.ReserveAheadM[person] = 0f;
        People.AuthorityM[person] = float.PositiveInfinity;

        // PHY-7: inside a container there is no body in the world and nothing in anybody's way.
        if (People.Inside[person].Any) return;

        if (People.OnWay[person] == PersonFleet.NoWay)
        {
            StandInTheWay(person);
            return;
        }

        // Never past the place it is already held at, the gap it keeps included (TER-4c.1): the kerb a red
        // is holding it at (PER-15), or the edge of a lane the road refused it. Asking for ground on the far
        // side of either would queue the pavement behind it a crossing further up than anybody is going to
        // get, and would put this body's stretch over ground a driver has.
        var alongMps = AlongItsWalkMps(person);
        var stoppingM = StoppingM(alongMps, FootGripMps2(person));
        People.ReserveAheadM[person] = MathF.Max(stoppingM, MathF.Min(WantsAheadM(person), HeldAtM(person)));

        // From the margin behind its back, exactly as a car's is (TER-5c.2): the ground a body keeps around
        // itself is that body's to hold, and whoever comes up behind is cut at it rather than keeping a gap
        // of its own.
        var radiusM = People.RadiusM[person];
        var count = WaysAlongTheWalk(person, radiusM + _config.PersonStandstillGapM, ReserveToM(person), ways);
        for (var index = 0; index < count; index++)
        {
            ref readonly var over = ref ways[index];
            _footfall.AddUnderWay(
                over.Way, over.FromM, OnTheWayM(over, radiusM), over.ToM, alongMps, person,
                of: LaneRoster.Walking);
        }
    }

    /// <summary>
    /// <b>How far in front of itself this body is asking for ground, with nothing holding it there</b>: what
    /// it needs to come to rest at the pace it walks, plus the gap it keeps, and never less than what it
    /// needs to stop from the speed it is actually doing.
    /// </summary>
    /// <remarks>
    /// <b>Sized by the pace it walks at and not by what it is doing</b>, exactly as a driver's is: a walker
    /// stopped at the back of a queue asks for the ground it needs to set off into, or the queue could never
    /// let it go. <b>It is also the reach of the ask on the road's side</b> — how near a lane of a crossing
    /// has to be before this body asks for it (<see cref="PlaceTheWalkerOnTheRoad"/>) — so the two networks
    /// are asked for the same distance in front of one body and not two figures that drift apart.
    /// </remarks>
    float WantsAheadM(int person)
    {
        var gripMps2 = FootGripMps2(person);
        var paceM = StoppingM(_config.PersonWalkSpeedMps * People.GroundCoefficient[person], gripMps2);

        return MathF.Max(StopsInM(person), paceM + _config.PersonStandstillGapM);
    }

    /// <summary>
    /// <b>What this body needs to come to rest in from the speed it is actually doing</b> — nothing at rest,
    /// and the pace's own stopping distance at the pace. It is the floor under the ask above and the bar the
    /// grant is read against (<see cref="PersonFleet.IsHeldByTheBook"/>), which are the same distance said
    /// once.
    /// </summary>
    public float StopsInM(int person)
    {
        var alongMps = AlongItsWalkMps(person);

        // The stride this tick as well as the stop after it: nothing asks again until the next tick, so the
        // ground this body is committing to is what it covers before the question is put again.
        return StoppingM(alongMps, FootGripMps2(person)) + MathF.Max(0f, alongMps * _config.TickSeconds);
    }

    /// <summary>
    /// How much ground in front of it a rule leaves this body, measured from where the body stands: nothing
    /// at a kerb a signal is holding it at, the edge of the lane it was refused where it was refused one,
    /// and everything otherwise.
    /// </summary>
    float HeldAtM(int person)
    {
        if (People.HeldAtTheKerb[person]) return 0f;
        if (People.RefusedWay[person] != People.OnWay[person]) return float.PositiveInfinity;

        // Short of the lane's edge by the margin this body keeps, which is where its grant will hold it
        // anyway (<see cref="GrantThePavement"/>): asked for past that, the stretch it lays is over ground
        // it may not walk into.
        return MathF.Max(
            0f, People.RefusedAtM[person] - People.OnWayM[person] - _config.PersonStandstillGapM);
    }

    /// <summary>How far past its own middle the far end of a walker's ask stands.</summary>
    float ReserveToM(int person) => People.RadiusM[person] + People.ReserveAheadM[person];

    /// <summary>
    /// <b>What the walker actually got</b>: its own stretch, cut at the nearest place anything in front of
    /// it will come to rest, as a distance from its front to the far end of the ground it may walk into.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cut is at the resting place of what is in front and not at its back — a walker's own stopping
    /// distance past the margin it keeps, which at a pace lost inside a fifth of a body is centimetres, and
    /// for a body going nowhere the margin this one keeps off it instead. Two walkers keeping the same pace
    /// therefore hold a standing gap rather than opening out.
    /// </para>
    /// <para>
    /// <b>And cut at the edge of a lane this body was refused</b> (<see cref="WhereTheWalkRunsOut"/>).
    /// A zebra is carriageway a walk runs over, so the ground under a crossing way has two names and one
    /// owner — and a body that asked the road's book for the next band and did not get it is a body that may
    /// walk up to that band and no further. It is the driving side's own arrangement over the other network
    /// (TER-5c.1): the ground where two ways cross is looked up in the book it belongs to, and nobody writes
    /// into a book of a network they are not on.
    /// </para>
    /// <para>
    /// <b>What it is not cut at is a body going nowhere</b> (PER-24, <see cref="LaneOccupancy.UnderWay"/>).
    /// That is the whole of where a walker parts from a driver on the far side of the same book: a driver
    /// waits behind a wreck and is eventually walked round it by the ladder, and a walker has feet — it is
    /// handed the nearest one on the ground it asked for and aims past it in the same tick.
    /// </para>
    /// </remarks>
    void GrantThePavement(int person, Span<LineWay> ways)
    {
        People.StepsRound[person] = PersonFleet.NoBody;
        People.HeldBy[person] = PersonFleet.NoBody;
        if (People.OnWay[person] == PersonFleet.NoWay) return;

        var grantedToM = ReserveToM(person);

        // The terms this walker is cut on, which are the driver's terms in the walker's own figures
        // (<see cref="LaneCredit"/>). <b>It asks with the weakest rank</b>: no claim on the pavement is a
        // walker's to take, so every stretch in front of it binds.
        var asker = new LaneCredit(
            _config.PersonStandstillGapM, LaneRoster.Walking, RightOfWay.TurningAcross);

        var count = WaysAlongTheWalk(person, People.RadiusM[person], grantedToM, ways);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];
            var fromM = OnTheWayM(way, People.RadiusM[person]);

            // In front of the body and not of the ground it holds, which is the road's own reading
            // (<see cref="LaneOccupancy.NextSpokenFor"/>): a stretch begins behind its owner's back.
            var cutM = OnTheLineM(
                way,
                _footfall.GrantedOn(
                    way.Way, fromM, way.ToM, person, asker, out var heldBy, LaneOccupancy.UnderWay));
            if (cutM < grantedToM)
            {
                grantedToM = cutM;
                People.HeldBy[person] = heldBy.Found ? heldBy.Occupant : PersonFleet.NoBody;
            }

            // The one the walk runs into rather than the one it is granted up to: the nearest is what the
            // feet have to get past, and a second body behind it is next tick's question.
            if (People.StepsRound[person] == PersonFleet.NoBody
                && _footfall.AheadObstruction(
                    way.Way, fromM, way.ToM, person, out var inTheWay, LaneRoster.Walking))
            {
                People.StepsRound[person] = inTheWay.Occupant;
            }

            // A lane's edge is a place and has no margin of its own, so the asker's is taken off it here —
            // the walking side of the one cut that is not made at somebody else's stretch.
            var runsOutM = WhereTheWalkRunsOut(person, way) + asker.AtAPlaceM;
            if (runsOutM < grantedToM) grantedToM = runsOutM;
        }

        People.AuthorityM[person] = grantedToM - People.RadiusM[person];
    }

    /// <summary>
    /// <b>Where on this walk a lane this body was refused begins</b>, in the walk's own metres, or infinity
    /// where it was refused none. <b>The refusal is not made here</b>: the road's book answered it when the
    /// band was asked for (<see cref="MayStepOnto"/>) and said where on the crossing way it lands, and this
    /// is that one answer spent — so the body stops at the kerb line of the lane rather than in it.
    /// </summary>
    /// <remarks>
    /// A lane's band lands at different metres on each way a zebra is made of, so the metre is the one taken
    /// on the way the body is actually walking and is spent only there.
    /// </remarks>
    float WhereTheWalkRunsOut(int person, in LineWay way) =>
        People.RefusedWay[person] == way.Way
            ? way.LineFromM + (People.RefusedAtM[person] - way.FromM)
            : float.PositiveInfinity;

    /// <summary>
    /// Whether this walker is a body on a line of its own rather than a shape on the pavement — <b>the
    /// whole of what the book is for</b>. One that is gets queued behind however long it stands; one that
    /// is not gets given up on and walked round.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Its place is the point it is walking at, walked back to where the body actually is.</b> The step
    /// back is the straight to that point and the ground is the way's own curve, so on a corner it reads
    /// the body a centimetre or two further along than it stands — which is the short reading, and a short
    /// reading of your own place is a longer gap to whoever is in front.
    /// </para>
    /// <para>
    /// <b>A body that has not reached the way its point is on is still on the way behind it</b>, and this
    /// is the common case rather than the corner one: a station is laid up to four metres up the walk, so
    /// a walker rounding a corner spends whole seconds aiming at a point on ground it is not on yet. Read
    /// off the near point alone it would stand at a negative distance, which is a body in the book before
    /// the way it is on begins — and every grant taken against it is then a grant over ground nobody is on.
    /// </para>
    /// </remarks>
    bool IsAfoot(int person, out int way, out float alongM)
    {
        way = PersonFleet.NoWay;
        alongM = 0f;

        if (!People.Walking[person] || !People.IsOnItsFeet(person)) return false;

        // A hand at the keys aims a walker wherever it likes and the line under it is whatever was last
        // laid, so what the book would be reading is where that walker was going before the hand took it.
        if (_hands.Held && _selected.Holds(SelectionKind.Person, person)) return false;

        var at = People.WalkedAt(person);
        if (at < 0 || at >= People.WalkedCount[person]) return false;

        var points = People.WalkedLineOf(person);
        var alongsM = People.WalkedAlongOf(person);
        var codes = People.WalkedWayOf(person);
        var positionM = People.PositionM[person];

        // The same bar the driving side holds a car to before it calls its line lost, in the walking side's
        // own figures: a body further off the stretch of walk it is on than that stretch has ground either
        // side of it is standing somewhere else, whatever its line still says. Reading one of those as a
        // walker on a lane puts a reservation on ground nobody is on and queues a pavement behind it.
        //
        // <b>With no point behind it there is no stretch to measure against</b>, and a line freshly laid
        // from where a body got to says nothing about where that body stands across it — so it is not
        // placed until it has walked a point, which is the same answer the negative-distance case gives.
        if (at == 0
            || OffTheWalkM(points[at - 1], points[at], positionM) > _config.WalkerOffLaneM * OffLineTolerance)
        {
            return false;
        }

        var on = WayOf(codes[at]);
        if (on == PersonFleet.NoWay) return false;

        var toPointM = (points[at] - positionM).Length();
        alongM = alongsM[at] - toPointM;
        if (alongM < 0f)
        {
            // Standing before the way its own point is on. The ground under it is the way the point behind
            // it was stationed on, and how much of the stretch between the two is still on that way is the
            // near point's own distance short of it.
            var before = WayOf(codes[at - 1]);
            if (before == PersonFleet.NoWay) return false;

            on = before;
            alongM = alongsM[at - 1] + MathF.Max(0f, (points[at] - points[at - 1]).Length() - toPointM);
        }

        way = on;
        alongM = MathF.Min(alongM, _footfall.WayLengthM(way));
        return true;
    }

    /// <summary>How far a body stands off the stretch of its walk it is on, which is the walking side's own off-line.</summary>
    static float OffTheWalkM(Vector2 fromM, Vector2 toM, Vector2 atM)
    {
        var run = toM - fromM;
        var lengthSq = run.LengthSquared();
        if (lengthSq < 1e-8f) return (atM - fromM).Length();

        var at = Math.Clamp(Vector2.Dot(atM - fromM, run) / lengthSq, 0f, 1f);
        return (atM - (fromM + (run * at))).Length();
    }

    /// <summary>The book's own way number for a point of a walked line, or <see cref="PersonFleet.NoWay"/> for the hop off the network.</summary>
    int WayOf(int code) =>
        code == WalkedLine.NoWay ? PersonFleet.NoWay
        : code >= 0 ? _footfall.WayOfLane(code)
        : _footfall.WayOfTurn(~code);

    /// <summary>
    /// Anything that is not walking a line: a body standing about, one knocked off its feet, one somebody
    /// is steering by hand, the last stride of a walk off the network onto a doorstep. <b>It is where it
    /// lies</b>, which is a question for the pavement and not for a line it is no longer on.
    /// </summary>
    void StandInTheWay(int person)
    {
        var positionM = People.PositionM[person];
        var edge = _foot.NearestEdge(positionM, out var alongEdgeM);
        if (edge < 0) return;

        var on = Spline.SampleAt(_foot.ArcsOf(edge), alongEdgeM);
        var offsetM = _walking.LaneOffsetM(edge);

        // Which of the stretch's two lanes it is standing on. They are laid half a band apart, so a body on
        // one of them stands on neither the other nor between them, and the answer is which side of the
        // stretch's own line the body is.
        var acrossM = Vector2.Dot(positionM - on.PositionM, on.Right);
        var lane = acrossM >= 0f ? edge : _foot.Reverse(edge);
        if (MathF.Abs(MathF.Abs(acrossM) - offsetM) > _config.WalkerOffLaneM) return;

        var edgeLengthM = MathF.Max(1e-4f, _foot.LengthM(edge));
        var alongLaneM = lane == edge ? alongEdgeM : edgeLengthM - alongEdgeM;
        alongLaneM = alongLaneM / edgeLengthM * (_walking.LaneLengthM(lane) - _walking.TailLengthM(lane));

        var radiusM = People.RadiusM[person];
        var alongMps = Vector2.Dot(People.VelocityMps[person], lane == edge ? on.Direction : -on.Direction);
        _footfall.Add(
            _footfall.WayOfLane(lane), alongLaneM - radiusM, alongLaneM + radiusM, alongMps, person,
            LaneUse.Obstruction, LaneRoster.Walking);
    }

    /// <summary>
    /// The ways of the pavement under a stretch of one walk — from <paramref name="backM"/> behind the body
    /// to <paramref name="aheadM"/> in front of it — each with the metres of its own that the stretch
    /// covers and where its near edge falls back on the walk.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The walk is its own measure here</b>, and <see cref="LineWay.LineFromM"/> is a distance from the
    /// body rather than from a line's origin: a walked line is re-laid from wherever the body has got to,
    /// so it has no origin two walkers could be compared against. What the grant is carried home through is
    /// therefore the same subtraction, taken from a different zero.
    /// </para>
    /// <para>
    /// <b>A station's way is the ground walked to reach it</b>, so the stretch between two stations belongs
    /// to the later of the two — which is what puts the mitre before a corner on the corner rather than on
    /// the pavement leading up to it.
    /// </para>
    /// <para>
    /// <b>Behind the body is one hand-over and no more.</b> A walker's back is a quarter-metre from its
    /// middle and the stations are metres apart, so the ground behind it lies on the way it is on or on the
    /// one before, and never on a third.
    /// </para>
    /// </remarks>
    int WaysAlongTheWalk(int person, float backM, float aheadM, Span<LineWay> into)
    {
        var points = People.WalkedLineOf(person);
        var codes = People.WalkedWayOf(person);
        var alongsM = People.WalkedAlongOf(person);
        var count = People.WalkedCount[person];
        var at = People.WalkedAt(person);

        var way = People.OnWay[person];
        var alongM = People.OnWayM[person];
        var written = 0;

        // Behind: the body's own back, and what is left of it on the way before where the way it is on has
        // not that much of itself behind the body.
        var behindM = MathF.Min(backM, alongM);
        if (backM > behindM && at > 0)
        {
            var overM = backM - behindM;
            var before = WayOf(codes[at - 1]);
            if (before != PersonFleet.NoWay && before != way)
            {
                var endM = alongsM[at - 1];
                into[written++] = new LineWay(before, MathF.Max(0f, endM - overM), endM, -backM);
            }
        }

        var fromM = alongM - behindM;
        var sM = -behindM;
        var toM = alongM;
        var walkedM = 0f;
        var previousM = People.PositionM[person];

        for (var index = at; index < count && walkedM < aheadM && written < into.Length; index++)
        {
            var stepM = (points[index] - previousM).Length();
            previousM = points[index];

            var onto = WayOf(codes[index]);
            if (onto == PersonFleet.NoWay) break;

            if (onto != way)
            {
                into[written++] = new LineWay(way, fromM, toM, sM);
                if (written == into.Length) return written;

                // <b>A way is entered the near point's own distance short of it</b>, which is what makes
                // the hand-over a place on the walk rather than a place in the arrays: as much of the
                // stretch between the two points as that way has of itself behind the point is on it, and
                // the rest was on the way before.
                var entryM = MathF.Min(alongsM[index], stepM);
                way = onto;
                fromM = alongsM[index] - entryM;
                toM = fromM;
                sM = walkedM + stepM - entryM;
            }

            // The ask may end part-way to a station. Metres along a way and metres walked are the same
            // metres to within the bow of the straight between two of them, so what is left of the ask is
            // spent as ground on the way — never past the station it is walking at.
            toM = MathF.Min(alongsM[index], toM + MathF.Min(stepM, aheadM - walkedM));
            walkedM += stepM;
        }

        if (written < into.Length) into[written++] = new LineWay(way, fromM, toM, sM);

        return written;
    }

    /// <summary>How fast this walker is going the way it is facing, which is the only direction it walks in.</summary>
    float AlongItsWalkMps(int person) =>
        Vector2.Dot(People.VelocityMps[person], Heading.Unit(People.HeadingRad[person]));

    /// <summary>What the feet can put down on the ground this walker is standing on (TER-2, PER-3).</summary>
    float FootGripMps2(int person) => _config.PersonFootGripMps2 * People.GroundCoefficient[person];
}
