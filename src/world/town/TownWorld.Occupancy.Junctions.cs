using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>What a car crossing a junction holds of its own join</b>: the runs of that join the other ways
/// through the box are driven over it at, laid ahead of the road the car's own claim has reached.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>The ground a car crossing a junction has committed to on its own join</b>, claimed from the car's
    /// own field — the runs of that join the other ways through the box are driven over
    /// it at (<see cref="WayCrossings.OwnRuns"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Its own join and nothing else</b> (TER-5c). Writing a stretch onto every join it is driven over
    /// would put it in front of the traffic on those joins in their own metres, and it would also be a car
    /// claiming several ways at once, none of which it will ever be on, and a box washed over by whoever
    /// merely aimed at it. What the runs are for is the same car coming the other way — its own stretch is
    /// settled against them as it is laid (<see cref="LaneOccupancy.AcrossTheWays"/>) and it reads them where
    /// they lie (<see cref="WhereTheGroundIsCrossed"/>), which is the same fact asked from the other end.
    /// </para>
    /// <para>
    /// <b>They are laid where the car's own committed claim has not reached yet</b>, and that is the whole of
    /// why the claim ahead exists: a car's road ahead is a braking distance and no more, which does not reach
    /// the place two lines meet until it is nearly on top of the junction. Under the body the same ground is
    /// the car's own claim (<see cref="AskForTheGround"/>), which carries its length and its swing.
    /// </para>
    /// <para>
    /// Re-laid from the car every tick for the same reason every claim is: nothing has to be released, a
    /// crossing cannot outlive the car making it, and a car wrecked or taken over by a hand is gone
    /// on the next rebuild without anything having had to notice.
    /// </para>
    /// </remarks>
    void PlaceTheCrossing(int car)
    {
        var movementWay = Cars.MovementWay[car];
        if (movementWay == CarFleet.NoWay) return;

        if (!Cars.Driven[car] || Cars.Broken[car])
        {
            Cars.MovementWay[car] = CarFleet.NoWay;
            return;
        }

        LayTheMovement(car, movementWay);
    }

    /// <summary>
    /// The runs themselves, which the rebuild lays and a car taking a crossing up mid-walk lays again:
    /// <b>the stretches of its own join the other ways through the box are driven over it at</b>, which is
    /// what refuses them before this car's own road has reached that far.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only the crossings the car has still to reach</b>, and the near edge walks up with the tail. A
    /// run is where this car's line is driven over by somebody else's, read from this car's own end, so the
    /// metres of it behind the body are the crossing spent — and the box empties behind the car as it works
    /// through rather than at the far side.
    /// </para>
    /// <para>
    /// <b>One stretch, from the first crossing it has still to reach to the last</b> (TER-5c.2), and never
    /// a claim apiece. The metres between two crossings are driven over by nothing, so holding them refuses
    /// nobody anything that was ever going to be asked — and laid as a run apiece the same car was several
    /// holders of one way with the ground nobody's in between, which is the one shape a body's hold may not
    /// have.
    /// </para>
    /// <para>
    /// <b>What is laid is the run and the road is what cuts it</b>, which is what keeps one body to one metre
    /// of one way. The car's own claim on this join is a stretch of the same way carrying its length, its
    /// swing and where it comes to rest; the stretch laid here is cut back at it where the two meet
    /// (<see cref="LaneOccupancy"/> makes the ground nobody else's before it goes in), so the seam falls on an
    /// exact metre rather than on the same figure worked out twice. The two are read as one set
    /// (<see cref="ClaimsAsked.Held"/>), so what another movement is refused by is their union and does not
    /// turn on where that seam is.
    /// </para>
    /// <para>
    /// <b>And the road between the two of them is what the car states</b>
    /// (<see cref="StateTheRoadItMeansToUse"/>). A driver's committed road is a braking distance and no more,
    /// which does not reach the box until the car is nearly on top of it, so a car granted a movement holds
    /// the crossings before its own road gets there; the statement is what carries the span across the metres
    /// in between. <b>Behind the body there is nothing to claim</b>, because the committed claim already
    /// begins a margin behind the tail (<see cref="SimConfig.CarTailMarginM"/>) — the width a
    /// one-dimensional reading of a swinging body threw away, carried on every way the car is on rather than
    /// added back on this one. Released at the bare tail instead, Odesa's soak wrecks cars.
    /// </para>
    /// <para>
    /// <b>And it is claimed with the movement's own right of way</b> (TER-5e). A claim is ground this car
    /// has not reached and is not committed to, so a movement with the greater right of way takes it back by
    /// asking for it — which is what a car turning across the oncoming stream gives up to the traffic going
    /// straight. <b>Once the car is past the point it could stop short of the box, the same claim is laid
    /// as ground nothing takes</b> (<see cref="CarFleet.CommittedToTheBox"/>): it is going in, and a right
    /// of way that took ground off a body already committed to it would be a rule about who is driven into.
    /// </para>
    /// <para>
    /// <b>A car whose line no longer takes this join claims the runs whole</b>, since there is no metre of
    /// its own to measure them against — which is the conservative way round for a body still holding a
    /// movement it has come off, and the whole of what such a body holds on that join
    /// (<see cref="LayTheMovement"/>).
    /// </para>
    /// <para>
    /// <b>A body that is not driving its movement claims it all the same</b>, and that is not the same claim
    /// as a driver's: one shoved off its line or under a hand is a body whose ground nothing else can work
    /// out — its own line says one thing and its pose another — so what it holds is the movement it is on,
    /// whole, until something puts it back on a line or takes it off the road. Dropped instead, on the
    /// grounds that a body off its line holds the ground it lies on, Odesa's soak wrecks two cars a minute:
    /// the ground it lies on is a projection, and a body far enough off its line falls outside the band of
    /// every join it is actually in.
    /// </para>
    /// </remarks>
    void LayTheMovement(int car, int movementWay)
    {
        if (!TheMovementHold(car, movementWay, out var fromM, out var toM)) return;

        HoldTheMovement(car, movementWay, fromM, toM);
    }

    /// <summary>
    /// <b>The ground this car holds on the way it is crossing on</b>, in that way's own metres: from the near
    /// edge of the first crossing it has still to reach to the far edge of the last one, <b>as one
    /// stretch</b>. False where it holds none of it — a movement nothing is driven over, or one whose every
    /// crossing is behind the body.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Never behind the body.</b> A crossing is a place and is given back where it is passed, so the near
    /// edge walks up with the tail through a run the car is half way along — and the metres behind it are the
    /// car's own claim's, which carries the margin. Laid from the run's own near edge instead, a car in the
    /// middle of a box was cut back to the half of the run it had already crossed and held the box behind
    /// itself and none of the box in front.
    /// </para>
    /// <para>
    /// <b>Where the car's own road has got to is not asked here</b>, because it is not this walk's to know:
    /// the stretch is laid over that road and cut back at it, so the seam is the metre the road actually
    /// reached rather than the same figure carried across by a second piece of arithmetic (SIM-7).
    /// </para>
    /// </remarks>
    bool TheMovementHold(int car, int movementWay, out float fromM, out float toM)
    {
        fromM = float.PositiveInfinity;
        toM = float.NegativeInfinity;

        var pastM = PastOnTheMovementM(car, movementWay);
        foreach (ref readonly var run in _crossings.OwnRuns(movementWay))
        {
            if (run.ToM <= pastM) continue;

            if (!float.IsFinite(fromM)) fromM = MathF.Max(run.FromM, pastM);
            toM = run.ToM;
        }

        return toM > fromM;
    }

    /// <summary>
    /// <b>The road between this car and the box it holds, stated</b> (TER-5g, TER-5c.2) — the metres its own
    /// committed road has not reached and its movement's claim does not cover, which are the metres that
    /// would otherwise belong to nobody with the same car on both sides of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A statement when it is laid, whatever it ends up worth</b>: the car has not been granted this
    /// ground and is not committed to it, and at the rebuild there is no answer yet to say otherwise. What
    /// the answer makes of it afterwards is <see cref="LevelTheRungToTheBox"/> — the box's own rung where the
    /// grant reached the mouth, and a statement where it did not. Laid firm here
    /// instead, a car at a give-way line would be holding the box shut against the traffic it is itself
    /// waiting for, on the strength of a question nobody had answered.
    /// </para>
    /// <para>
    /// <b>It is laid whether the car is moving or not</b>, which is the one exception to <em>a body that is
    /// not moving states nothing</em> (TER-5g) and the reason it is written here rather than inside the ask.
    /// A driver's committed road is a braking distance and no more — at a standstill its own length and a
    /// metre — so the gap between it and a box the gate has already given the car is the ordinary case and
    /// not an edge of one.
    /// </para>
    /// <para>
    /// <b>Laid to the far end of what the car holds and cut back where the two meet</b>, so the seam is an
    /// exact metre; laid to the near edge instead, it is the same metre reached by two sums and a hair of
    /// road between them.
    /// </para>
    /// <para>
    /// <b>And laid again the moment a movement is taken</b> (<see cref="TakeTheMovement"/>), because a
    /// movement is taken mid-walk as well as at the rebuild — exactly as the claim itself is
    /// (<see cref="LayTheMovement"/>). Written only by the rebuild, a car that was given its box during a
    /// tick's decisions held it with nothing between, until the next one.
    /// </para>
    /// </remarks>
    void StateTheRoadToTheMovement(int car, Span<LineWay> ways)
    {
        if (!IsUnderWay(car)) return;

        var holdsToM = TheMovementHoldEndsAtM(car);
        if (!float.IsFinite(holdsToM)) return;

        var fromM = Cars.ClaimToM[car];
        var toM = MathF.Min(holdsToM, Cars.Line[car].LengthM);
        if (toM <= fromM) return;

        // What the car says it is using now reaches the box, which is what the answer is taken over
        // (<see cref="GrantTheGround"/>) and what the cut is held off (<see cref="CutTheGroundToTheGrant"/>).
        Cars.StatedToM[car] = MathF.Max(Cars.StatedToM[car], toM);

        var count = WaysAlong(car, fromM, toM, ways);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];
            var whole = _occupancy.ClaimAhead(
                way.Way, way.FromM, way.ToM, Cars.AlongMps[car], car, SoftOf(car, way.Way));

            // The span stops where it stops: past a way that would not take the whole of it there is
            // nothing to carry (TER-5c.2).
            if (!whole) break;
        }
    }

    /// <summary>
    /// <b>The road between this car and the box it holds, held at that box's own rung — or the box let down
    /// to what the road to it is worth</b> (TER-5g.1). A hold is one run of ground from the body outward, and
    /// the ladder is read along it: nothing a car holds further on is stronger than what it holds this side
    /// of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The answer is what says which of the two it is</b> (TER-4c.1). Granted as far as the mouth, the
    /// metres in between are this car's and nobody else is going to be given them, so they are worth exactly
    /// what the box is worth. Left stated instead, they are the one rung an equal right of way is entitled to
    /// take (TER-5g) — and a car that loses them is holding the far side of a junction and not the road it
    /// has to cross to reach it, which is a hold with a hole in it by any reading but the geometry's
    /// (TER-5c.2).
    /// </para>
    /// <para>
    /// <b>Stopped short of the mouth by the answer, it is the other way about.</b> The car is not getting
    /// there this tick, and a box held firm against the traffic that could be crossing it is a box shut for
    /// nothing. The box is let down to a statement (<see cref="ClaimPriority.Soft"/>).
    /// </para>
    /// <para>
    /// <b>A body past the point it could stop is raised whatever the answer said</b> (TER-5e), which is the
    /// one place the answer is not what decides. Its box is p0 because nothing takes what such a car holds,
    /// and the metres between it and that box are road it can no longer give back, which is the same p0 by
    /// the same fact: let down to meet a grant it is going to drive past anyway, they would be ground the
    /// town offered to somebody else with a car already committed to crossing it.
    /// </para>
    /// <para>
    /// <b>It is a pass of its own and not a step of the ask</b> (<see cref="RebuildLaneOccupancy"/>): the
    /// rung it carries back is the answer's, and there is no answer yet while the asks are being laid.
    /// </para>
    /// </remarks>
    void LevelTheRungToTheBox(int car, Span<LineWay> ways)
    {
        var movementWay = Cars.MovementWay[car];
        if (movementWay == CarFleet.NoWay) return;

        var count = TheRoadToTheBox(car, ways, out var boxFromM);
        if (!float.IsFinite(boxFromM)) return;

        var noseM = Cars.ProgressM[car] + LeadingEdgeAheadOfTheAxleM(car);
        var rung = FirmOnTheMovement(car, movementWay);

        // Infinite where nothing cut this car at all, which is the common way to be granted the mouth.
        if (rung != ClaimPriority.Hard && noseM + Cars.AuthorityM[car] < boxFromM)
        {
            if (!TheMovementHold(car, movementWay, out _, out var holdToM)) return;

            // <b>From the way's own beginning and not from the hold's</b>: what the car holds of a join comes
            // in as many stretches as the seam between its road and its box has moved
            // (<see cref="ClaimWhatTheAnswerTook"/>), and every one of them is the same box.
            _occupancy.LowerTo(movementWay, car, 0f, holdToM, ClaimPriority.Soft);
            return;
        }

        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];
            _occupancy.RaiseTo(way.Way, car, way.FromM, way.ToM, rung);
        }

        // <b>And the join's own metres behind the box this car holds</b> (TER-5g.1). A car already inside a
        // box has no approach left — its claim's near edge is past the mouth, so the walk above names no way
        // at all — and where the traffic in there has cut its road short, what spans the gap between its body
        // and the crossing it still holds is a statement (<see cref="StateWhatTheAnswerTook"/>). That is a
        // hold whose rung grows along it: the far piece is one nothing takes and the middle is a statement,
        // so a movement crossing those metres is granted ground this car is about to drive over.
        if (TheMovementHold(car, movementWay, out var raiseFromM, out _))
        {
            _occupancy.RaiseTo(movementWay, car, 0f, raiseFromM, rung);
        }
    }

    /// <summary>
    /// <b>And back to a statement with the box it was the road to</b> (TER-5g.1). A movement given back is a
    /// car with nothing at the end of that road, and metres held at the rung of ground nobody holds any more
    /// are a hold on the approach to an empty junction.
    /// </summary>
    void UnlevelTheRoadToTheBox(int car, Span<LineWay> ways)
    {
        var count = TheRoadToTheBox(car, ways, out _);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];
            _occupancy.LowerTo(way.Way, car, way.FromM, way.ToM, ClaimPriority.Soft);
        }
    }

    /// <summary>
    /// <b>The ways the road between this car and the box it holds runs over</b>, from its own tail up to the
    /// near edge of its movement's ground — and <paramref name="boxFromM"/>, that near edge on the line the
    /// car is driving. None of it where the car holds no movement, holds none of one, or is standing at the
    /// mouth already.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>From the tail and not from the far edge of the road</b>, because the metres a rung has to be
    /// carried over are not only the ones past the ask: an answer that came back short leaves the end of the
    /// car's own ask stated rather than committed (<see cref="StateWhatTheAnswerTook"/>), and those are the
    /// metres directly behind a box. What stands in the way of nothing is left alone — a stretch with a body
    /// in it has no rung to move.
    /// </para>
    /// <para>
    /// <b>One reading of the mouth for everything that has an opinion about it.</b> The metre is the movement
    /// hold's own near edge carried onto the line (<see cref="TheMovementHold"/>), which is the same metre
    /// the claim on the box was laid from rather than a second sum that lands beside it.
    /// </para>
    /// </remarks>
    int TheRoadToTheBox(int car, Span<LineWay> ways, out float boxFromM)
    {
        boxFromM = float.PositiveInfinity;

        var movementWay = Cars.MovementWay[car];
        if (movementWay == CarFleet.NoWay) return 0;
        if (!TheMovementHold(car, movementWay, out var holdFromM, out _)) return 0;

        boxFromM = WhereTheMovementBeginsM(car, movementWay) + holdFromM;

        var fromM = MathF.Max(0f, Cars.ClaimFromM[car]);
        return boxFromM <= fromM ? 0 : WaysAlong(car, fromM, boxFromM, ways);
    }

    /// <summary>The stretch itself, at the rung this car crosses with.</summary>
    void HoldTheMovement(int car, int movementWay, float fromM, float toM) =>
        _occupancy.ClaimAhead(movementWay, fromM, toM, 0f, car, FirmOnTheMovement(car, movementWay));

    /// <summary>
    /// <b>Where the ground this car holds on its own movement ends, on the line it is driving</b> — the far
    /// edge of the last crossing it has still to reach, or infinity where it holds no movement, none of one,
    /// or one its line does not take.
    /// </summary>
    /// <remarks>
    /// <b>It is what the statement reaches to</b> (<see cref="StateTheRoadItMeansToUse"/>): a hold with a
    /// hole in it is a hold that says nothing about the metres in the hole, and the far edge rather than the
    /// near one because a statement laid short by a hair is a hair of road belonging to nobody.
    /// </remarks>
    public float TheMovementHoldEndsAtM(int car)
    {
        var movementWay = Cars.MovementWay[car];
        if (movementWay == CarFleet.NoWay || !TheMovementHold(car, movementWay, out _, out var toM))
        {
            return float.PositiveInfinity;
        }

        return WhereTheMovementBeginsM(car, movementWay) + toM;
    }

    /// <summary>
    /// <b>The metres the answer took off the road, handed over to the claim that was carrying the rest of the
    /// same ground</b> (<see cref="CutTheGroundToTheGrant"/>) — so that what a car holds of its own join is
    /// the ground it is committed to whichever side of the seam each metre falls.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The seam moves and the union does not.</b> A committed claim and the one beyond it are laid as one
    /// piece of ground with the join between them wherever the road happened to reach
    /// (<see cref="LayTheMovement"/>), and every reader takes them as one set (<see cref="ClaimsAsked.Held"/>). Cut without
    /// this, the metres between the answer and the ask fell out of both, and a car sitting in a box was
    /// sitting on ground a crossing movement was free to be granted.
    /// </para>
    /// <para>
    /// <b>And they come back as a claim rather than as road, which is the honest name for them</b> (TER-5e):
    /// they are metres this car has not reached. A car already committed to the box claims them with the rank
    /// that says so and nothing takes them; a car still short of it can still stop, and a stronger movement
    /// asking for the same ground is entitled to have them.
    /// </para>
    /// </remarks>
    /// <param name="roadIsToM">
    /// Where the road this car holds on its movement was cut to, in that way's own metres — <b>the metre the
    /// cut was actually made at</b> and not the answer carried across a second time, so the stretch laid here
    /// meets it rather than nearly meeting it.
    /// </param>
    void ClaimWhatTheAnswerTook(int car, int movementWay, float roadIsToM)
    {
        if (!TheMovementHold(car, movementWay, out var fromM, out var toM) || roadIsToM >= toM) return;

        // <b>Laid from the metre the road was actually cut to</b> and over the whole of the stretch it is
        // rejoining, which cuts it back to the metres that fell out. Worked out from the ask instead, the
        // near edge is the same metre arrived at by a second route and the seam is a hair of road belonging
        // to nobody; stopped short of the stretch's own near edge instead, a road that had reached past the
        // last crossing and was then cut back to the first handed nothing over at all.
        HoldTheMovement(car, movementWay, MathF.Max(fromM, roadIsToM), toM);
    }

    /// <summary>
    /// The rung a car holds its own join's ground at, on either side of the seam between the road and the
    /// claim ahead: <b>the movement's own, until the car is past the point it could stop short of the box</b>
    /// (<see cref="CarFleet.CommittedToTheBox"/>), and then the rung nothing takes
    /// (<see cref="ClaimPriority.Hard"/>) — a movement that took ground off a body already committed to it
    /// would be a rule about who is driven into.
    /// </summary>
    /// <remarks>
    /// <b>Being committed outranks carrying a light</b>, and that order is the whole of it: what a body
    /// already committed to a box holds, nothing takes (TER-5e), and a rescue's claim is above everything a
    /// road carries of itself but below a body that can no longer stop.
    /// </remarks>
    ClaimPriority FirmOnTheMovement(int car, int movementWay) =>
        Cars.CommittedToTheBox[car] ? ClaimPriority.Hard : FirmOf(car, movementWay);

    /// <summary>
    /// <b>The rung a car both holds and asks a way's ground at</b> — on the way through the box it is
    /// committed to, the rung that says so (<see cref="FirmOnTheMovement"/>); everywhere else, the
    /// movement's own. <b>The committed claim, the one beyond it and the ask are one rung</b>, which is what
    /// keeps the seam between them from being a place a car outranks itself.
    /// </summary>
    /// <remarks>
    /// <b>Asked at the weaker rung a car refuses itself into a corner</b> (TER-5e). A body past the point
    /// it could stop holds its crossing against everything; asking below that it would be held off ground
    /// somebody else had merely stated, and a car stopped in the middle of a box on the strength of
    /// another car's intentions is the one shape the ladder exists to prevent.
    /// </remarks>
    ClaimPriority AskingRungOn(int car, int way) =>
        way == Cars.MovementWay[car] ? FirmOnTheMovement(car, way) : FirmOf(car, way);

    /// <summary>
    /// <b>How far behind this car a crossing point has to fall before it is behind it</b>: where its own
    /// ground begins on the way it is making its movement on, in that way's own metres, or negative
    /// infinity where its line does not take that movement at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the near edge of the committed claim and nothing worked out a second time</b>
    /// (<see cref="AskForTheGround"/>) — a body's length and the margin it keeps behind its tail. The tail
    /// and not the nose, because a section is a place the <em>body</em> goes over: a car whose bumper has
    /// cleared a crossing point is still lying across it. The margin is what a one-dimensional
    /// reading of a body owes the width it threw away; released at the bare tail, Odesa's soak wrecks cars
    /// (<see cref="SimConfig.CarTailMarginM"/>).
    /// </para>
    /// <para>
    /// A car that is not under way has asked for no ground, so its stretch stands at its line's own origin
    /// and every crossing point on the way is in front of it — which is what makes such a body claim the
    /// runs whole (<see cref="LayTheMovement"/>).
    /// </para>
    /// </remarks>
    float PastOnTheMovementM(int car, int movementWay) =>
        Cars.ClaimFromM[car] - WhereTheMovementBeginsM(car, movementWay);

    /// <summary>
    /// <b>Where the way this car's movement is made on begins under the line's own metres</b>, and infinity
    /// where the line does not take that movement at all — so a car holding one it has come off measures
    /// everything from beyond the end of it and claims the runs whole.
    /// </summary>
    /// <remarks>
    /// <b>The one place the two shapes of movement are told apart</b>, so the metre a crossing is measured
    /// from cannot be worked out two ways. A join is threaded between two lanes of a chain and begins where
    /// the arriving lane's own stretch of the line ends; a bay's way out <em>is</em> the line, and begins
    /// where the line does.
    /// </remarks>
    float WhereTheMovementBeginsM(int car, int movementWay)
    {
        if (movementWay == CarFleet.NoWay) return float.PositiveInfinity;
        if (Cars.LineWayOf(car) == movementWay) return 0f;

        var ahead = LaneAheadSlot(car, Cars.ProgressM[car]);
        if (ahead + 1 >= Cars.Line[car].LaneCount) return float.PositiveInfinity;

        var chain = Cars.ChainOf(car);
        var slot = _roads.ConnectorBetween(chain[ahead], chain[ahead + 1]);
        return slot != RoadGraph.NoConnector && _ways.OfRoadConnector(slot) == movementWay
            ? Cars.LaneEndsOf(car)[ahead]
            : float.PositiveInfinity;
    }
}
