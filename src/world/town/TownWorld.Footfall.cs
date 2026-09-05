using System.Numerics;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The walkers' half of the claims</b>: which stretch of the footway each walker has asked for and which
/// it was granted, laid once a tick from the bodies themselves — and the one question a walker asks of the
/// index, which is how much of the pavement in front of it is its own.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the same table and the same arithmetic over ground of another kind</b>
/// (<see cref="LaneOccupancy"/>, <see cref="TownWays"/>), and
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
/// <b>Everything in front cuts it and what a body is doing decides the reply</b> (PER-24, TER-4c.3). A body
/// going nowhere holds its ground like any other, so the walk stops at it either way; the same walk that
/// takes the grant picks it out (<see cref="PersonFleet.StepsRound"/>) and the feet re-aim past it with
/// whatever room the cut left, which is one question answered with one arithmetic and two replies.
/// </para>
/// <para>
/// <b>A body on no way of the pavement is granted on the ground instead of along a line</b>
/// (<see cref="GrantWhereItStands"/>). A walk that has not taken its first point, one shoved off its line and
/// one under a hand are all in that state, and a body with no stretch to be cut on still walks only into
/// ground nobody is standing on.
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
/// <b>Whatever is standing on the pavement is written into it</b> (TER-4c.2), a car that has mounted a kerb
/// included (<see cref="LieOnThePavement"/>): a body holds the ground it occupies whatever kind of body it
/// is, and a car that claims nothing here is one a walk goes straight through.
/// </para>
/// <para>
/// <b>What is never written into it is a car on the paint</b> (TER-5c.1). A zebra is a walk laid over a
/// carriageway, so the ground under it has two names and one owner: the car's stretch of it is a stretch of
/// the <em>lane</em>, and what stops a body walking into it is that stretch, looked up where the crossing
/// runs over the lane (<see cref="WhereTheWalkRunsOut"/>). Marked here as well, a car held one body
/// twice over one piece of ground, under two claims whose answers could differ.
/// </para>
/// <para>
/// <b>A body on foot standing there is written in like anywhere else</b> (TER-4c.2,
/// <see cref="StandInTheWay"/>). The look-up that answers for a car does not answer for it: what a walker
/// asks the road is what traffic is coming (<see cref="LaneOccupancy.AnyTrafficOver"/>), it asks it of the
/// band it is about to step into and of no other, and a person on the carriageway is not an answer to it. So
/// the ground a body occupies is claimed on every way it stands on, and a crossing is a way of the
/// walk like the rest.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>
    /// How many ways one walker's stretch of pavement may be cut into: the ways behind it — the margin it
    /// keeps reaches back over a mitre and onto the stretch before it — the one it stands on, and the ones
    /// its claim runs over. A bound on a stack span and not a figure behaviour reads.
    /// </summary>
    /// <remarks>
    /// <b>Reached, the ways at the far end go unwritten</b>, and a walker's ground in front of it left
    /// unclaimed is somebody else granted it. A corner is two short ways within a stride, and the margin
    /// now reaches back over one of them, so the count is what a body on a corner can cover rather than what
    /// a stretch of pavement suggests.
    /// </remarks>
    const int MostWaysAlongAWalk = 5;

    /// <summary>
    /// How many claims one walker may lay at once, which is one per way it covers: the ways
    /// along the walk it is asking for, or — standing about — every way of the pavement its own box is over
    /// (<see cref="StandInTheWay"/>). A body lays one of the two and never both.
    /// </summary>
    static int MostSlotsPerWalker(in PavementWays pavement) =>
        Math.Max(MostWaysAlongAWalk, pavement.MostWaysUnderAPlace);

    /// <summary>
    /// <b>The pavement's two blocks of the town's numbering, as the runs of metres they are</b> — a lane
    /// each way down every stretch, and the mitre at every corner. Handed to <see cref="TownWays"/>, which
    /// is what makes them ways of the same table the carriageway's are.
    /// </summary>
    /// <remarks>
    /// <b>What stands far enough aside of one of these lines to be walked past is half a body</b>
    /// (<see cref="SimConfig.WalkPassableAsideM"/>, <see cref="TownWays.ClearsAsideM"/>), which is the
    /// road's own bar in the walking side's figures. At nought a stretch stopped being in the way the moment
    /// it was a hair clear of the line — so a car parked across a footway was walked straight through, the
    /// walker's own width being the whole of what it had left over.
    /// </remarks>
    static float[] PavementLengthsM(WalkingNetwork walking, out float[] mitreLengthM)
    {
        var lanesM = new float[walking.Foot.EdgeCount];
        for (var edge = 0; edge < lanesM.Length; edge++) lanesM[edge] = walking.LaneLengthM(edge);

        mitreLengthM = new float[walking.TurnCount];
        for (var turn = 0; turn < mitreLengthM.Length; turn++) mitreLengthM[turn] = walking.JoinLengthM(turn);

        return lanesM;
    }

    /// <summary>
    /// <b>Where this walker stands on the pavement's own network</b>, or <see cref="PersonFleet.NoWay"/>
    /// where it is on none of it.
    /// </summary>
    /// <remarks>
    /// <b>Worked out before either network is laid, because both read it</b> (<see cref="RebuildLaneOccupancy"/>):
    /// the road needs the way a body on a crossing is walking to know which lane it stands in, and
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
    /// <b>The body needs no stretch of its own</b>, here or on the road: a claim already begins at its
    /// owner's back, so where the body ends is an edge of that stretch (<see cref="LaneClaim.StandsToM"/>)
    /// and never a second one laid over it.
    /// </remarks>
    void AskForThePavement(int person, Span<LineWay> ways)
    {
        People.ClaimAheadM[person] = 0f;
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
        People.ClaimAheadM[person] = MathF.Max(stoppingM, MathF.Min(WantsAheadM(person), HeldAtM(person)));

        // From the margin behind its back, exactly as a car's is (TER-5c.2): the ground a body keeps around
        // itself is that body's to hold, and whoever comes up behind is cut at it rather than keeping a gap
        // of its own.
        // <b>And where across the way it is walking</b> (PER-24): a body part way round somebody is asking
        // for the pavement beside them and is standing there, so it is written where it is — or whoever is
        // behind it is cut at a stretch of line this body has stepped off, and whoever is coming past it
        // reads the line as clear.
        var radiusM = People.RadiusM[person];
        var acrossM = People.StepsAcrossM[person];
        var count = WaysAlongTheWalk(person, radiusM + _config.PersonStandstillGapM, ClaimToM(person), ways);
        for (var index = 0; index < count; index++)
        {
            ref readonly var over = ref ways[index];
            _occupancy.ClaimUnderWay(
                over.Way, over.FromM, OnTheWayM(over, radiusM), over.ToM, alongMps, person,
                of: LaneRoster.Walking, acrossFromM: acrossM - radiusM, acrossToM: acrossM + radiusM);
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
    /// has to be before this body asks for it (<see cref="PlaceTheWalkerOnTheRoad"/>) — so the carriageway
    /// and the footway are asked for the same distance in front of one body and not two figures that drift
    /// apart.
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
    /// grant is read against (<see cref="PersonFleet.IsHeldByTheClaims"/>), which are the same distance said
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
    float ClaimToM(int person) => People.RadiusM[person] + People.ClaimAheadM[person];

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
    /// owner — and a body that asked the road for the next band and did not get it is a body that may
    /// walk up to that band and no further. It is the driving side's own arrangement over ground of another
    /// kind (TER-5c.1): where two ways lie over one another the ground is looked up on the one it belongs
    /// to, and nobody claims on a way they are not on.
    /// </para>
    /// <para>
    /// <b>And cut at a body going nowhere too</b> (TER-4c.3, PER-13). It holds the stretch it is standing on
    /// for as long as it stands there, so the walk stops at that stretch's near edge like anywhere else — a
    /// walker walks only into ground it holds, and a grant reaching past a parked car would be the town
    /// saying one thing in the claims and another in the permission taken off them.
    /// </para>
    /// <para>
    /// <b>What going nowhere decides is what the walker does about it</b> (PER-24,
    /// <see cref="IsComingThrough"/>) — waited for where it stands, or stepped round with whatever room the
    /// cut leaves in front. It is a fact about the body's own movement and never about how its claim was
    /// laid: a body under a hand, one being shoved and a car crossing the pavement are all laid from a pose
    /// while moving, and each is coming through ground the walk wanted.
    /// </para>
    /// </remarks>
    void GrantThePavement(int person, Span<LineWay> ways)
    {
        People.StepsRound[person] = PersonFleet.NoBody;
        People.StepsRoundOf[person] = LaneRoster.Walking;
        People.HeldBy[person] = PersonFleet.NoBody;
        People.HeldByOf[person] = LaneRoster.Walking;
        People.StepsAcrossM[person] = 0f;
        if (People.OnWay[person] == PersonFleet.NoWay)
        {
            GrantWhereItStands(person);
            return;
        }

        var wanted = ClaimToM(person);
        var granted = TakeTheWalk(person, ways, acrossM: 0f, wanted);
        Adopt(person, in granted);

        // <b>And the step round, which is a second ask and not a re-aim</b> (PER-24, TER-4c.2). The body in
        // the way holds its stretch of the line either way; what a step is, is this walker asking for the
        // pavement beside that stretch instead — so the permission it walks on is the ground it is actually
        // walking over, and the feet are aimed at the offset the ask was granted at.
        if (granted.StepsRound == PersonFleet.NoBody) return;

        StepPastTheBody(person, ways, in granted, wanted);
    }

    /// <summary>
    /// <b>Where a step round would put this body, asked as a grant of its own</b> — the least offset that
    /// gets it past the stretch in the way (PER-24), to the right first and to the left where the ground
    /// refuses the right (PER-7.2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The offset comes off the body being got past and not off a figure</b>: a claim now says where
    /// across the way its holder is (<see cref="LaneClaim.AcrossFromM"/>), so the least that gets by is the
    /// far edge of that span, the room a reader needs to pass it, and the shoulders. Guessed at instead, a
    /// step is either short of what the body actually takes or wider than the pavement.
    /// </para>
    /// <para>
    /// <b>A step is taken only where it buys ground.</b> Re-asked at the offset, the walk is cut by whatever
    /// is over the ground beside the body — a second walker abreast of it, the kerb, a wall — and a step
    /// that comes back no further than standing still is not a step. Where neither side buys anything the
    /// walker is walled in and stands, which is the answer it had before there was a step at all, and the
    /// clock that gives up a leg is what draws it a line round (PER-8).
    /// </para>
    /// </remarks>
    void StepPastTheBody(int person, Span<LineWay> ways, in WalkGrant standing, float wantedM)
    {
        if (!TheWayItIsOn(person, out var arcs)) return;

        var byM = _ways.ClearsAsideM(People.OnWay[person]) + _config.PersonShoulderRoomM;
        var fromTheCarriageway = _terrain.At(People.PositionM[person]).Drivable;
        var on = Spline.SampleAt(arcs, People.OnWayM[person]);

        if (Steps(person, ways, standing, wantedM, standing.AcrossToM + byM, on, fromTheCarriageway)) return;

        if (Steps(person, ways, standing, wantedM, standing.AcrossFromM - byM, on, fromTheCarriageway))
        {
            StepsRoundToTheLeft++;
        }
    }

    /// <summary>One side of a step tried: the ground under it, the grant at it, and whether it was worth taking.</summary>
    bool Steps(
        int person, Span<LineWay> ways, in WalkGrant standing, float wantedM, float acrossM,
        in SplineSample on, bool fromTheCarriageway)
    {
        if (!IsGroundToStepOnto(on.PositionM + (on.Right * acrossM), fromTheCarriageway)) return false;

        var stepped = TakeTheWalk(person, ways, acrossM, wantedM);
        if (stepped.GrantedToM <= standing.GrantedToM) return false;

        Adopt(person, in stepped);
        People.StepsAcrossM[person] = acrossM;

        // <b>Still the body it is getting past</b>, and the offset is how. Read off the stepped walk instead
        // it is nobody — the whole point of the offset is that the body is not in the way from there — so a
        // walker mid-step would report itself held by nothing and stepping round nothing, and the clock that
        // gives up a leg (<see cref="IsHeldByAStandstill"/>) would read it as a walk with nothing to answer.
        People.StepsRound[person] = standing.StepsRound;
        People.StepsRoundOf[person] = standing.StepsRoundOf;
        StepsRound++;
        return true;
    }

    /// <summary>What one walk of the ways came to, so that a walk taken twice cannot be half written down.</summary>
    /// <param name="AcrossFromM">Where across the way the body being stepped round begins, and where it ends — the span a step has to clear.</param>
    readonly record struct WalkGrant(
        float GrantedToM, int HeldBy, LaneRoster HeldByOf, int StepsRound, LaneRoster StepsRoundOf,
        float AcrossFromM, float AcrossToM);

    void Adopt(int person, in WalkGrant granted)
    {
        People.AuthorityM[person] = granted.GrantedToM - People.RadiusM[person];
        People.HeldBy[person] = granted.HeldBy;
        People.HeldByOf[person] = granted.HeldByOf;
        People.StepsRound[person] = granted.StepsRound;
        People.StepsRoundOf[person] = granted.StepsRoundOf;
    }

    /// <summary>
    /// <b>The walk of this body's own ways, asked from one place across them</b> — the whole of the grant,
    /// written nowhere so that it can be asked again from a step's offset and the better of the two kept.
    /// </summary>
    WalkGrant TakeTheWalk(int person, Span<LineWay> ways, float acrossM, float wantedM)
    {
        var grantedToM = wantedM;
        var heldBy = PersonFleet.NoBody;
        var heldByOf = LaneRoster.Walking;
        var stepsRound = PersonFleet.NoBody;
        var stepsRoundOf = LaneRoster.Walking;
        var acrossFromM = 0f;
        var acrossToM = 0f;

        // The terms this walker is cut on, which are the driver's terms in the walker's own figures
        // (<see cref="LaneCredit"/>). <b>It asks with the weakest rank</b>: no claim on the pavement is a
        // walker's to take, so every stretch in front of it binds.
        var asker = new LaneCredit(
            _config.PersonStandstillGapM, LaneRoster.Walking, RightOfWay.TurningAcross, acrossM);

        var count = WaysAlongTheWalk(person, People.RadiusM[person], wantedM, ways);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];
            var fromM = OnTheWayM(way, People.RadiusM[person]);

            // In front of the body and not of the ground it holds, which is the road's own reading
            // (<see cref="LaneOccupancy.NextHeld"/>): a stretch begins behind its owner's back.
            var cutM = OnTheLineM(
                way,
                _occupancy.GrantedOn(
                    way.Way, fromM, way.ToM, person, asker, out var cutBy, ClaimsAsked.Walkable));
            if (cutM < grantedToM)
            {
                grantedToM = cutM;
                heldBy = cutBy.Found ? cutBy.Occupant : PersonFleet.NoBody;
                heldByOf = cutBy.Found ? cutBy.Of : LaneRoster.Walking;
            }

            // And the bodies laid where they lie, which the cut above leaves out and which the walker
            // answers one of two ways. <b>Of whichever roster</b> (TER-4c.2): a car that has mounted a kerb
            // is a body on the pavement like any other, and its number means nothing without the fleet it is
            // a number in.
            var at = LaneOccupancy.FromTheStart;
            while (_occupancy.NextLying(
                       way.Way, fromM, way.ToM, person, ref at, out var lying, LaneRoster.Walking, acrossM))
            {
                // <b>Every one of them cuts the walk</b> (TER-4c.3): the ground under a body is that body's
                // for as long as it stands there, whether it got there by walking, by being shoved or by
                // being driven. What its movement decides is the reply below, never the cut.
                var heldToM = OnTheLineM(way, lying.FromM) + asker.AtAPlaceM;
                if (heldToM < grantedToM)
                {
                    grantedToM = heldToM;
                    heldBy = lying.Occupant;
                    heldByOf = lying.Of;
                }

                // <b>Going nowhere is the body's own movement and never how its stretch was measured</b>
                // (PER-24, <see cref="IsComingThrough"/>). One coming through ground the walk wants is
                // waited for where it stands; one going nowhere is stepped past instead.
                if (IsComingThrough(lying)) continue;

                // The one the walk runs into rather than the one it is granted up to: the nearest is what
                // the feet have to get past, and a second body behind it is next tick's question.
                if (stepsRound != PersonFleet.NoBody) continue;
                if (IsWhereTheWalkIsGoing(person, in lying)) continue;

                stepsRound = lying.Occupant;
                stepsRoundOf = lying.Of;
                acrossFromM = lying.AcrossFromM;
                acrossToM = lying.AcrossToM;
            }

            // A lane's edge is a place and has no margin of its own, so the asker's is taken off it here —
            // the walking side of the one cut that is not made at somebody else's stretch.
            //
            // <b>And the vehicle standing on that band where one is</b> (PER-15,
            // <see cref="PersonFleet.RefusedBy"/>): a road refuses nobody in particular and is given back by
            // being driven on, so a walker held at one is waiting; a body over the paint is on no walk of
            // this town's (TER-5c.1) and would otherwise hold a walker with nothing named as holding it,
            // which is the one shape the clock that gives up a leg cannot see.
            var runsOutM = WhereTheWalkRunsOut(person, way) + asker.AtAPlaceM;
            if (runsOutM < grantedToM)
            {
                grantedToM = runsOutM;
                heldBy = People.RefusedBy[person];
                heldByOf = heldBy == PersonFleet.NoBody ? LaneRoster.Walking : LaneRoster.Driving;
            }
        }

        return new WalkGrant(
            grantedToM, heldBy, heldByOf, stepsRound, stepsRoundOf, acrossFromM, acrossToM);
    }

    /// <summary>
    /// The arcs of the way this walker is standing on, which is the frame its step is measured in — the
    /// stretch's own lane, or the mitre it is turning on.
    /// </summary>
    bool TheWayItIsOn(int person, out ReadOnlySpan<ArcSeg> arcs)
    {
        var way = People.OnWay[person];
        arcs = _ways.KindOf(way) == WayKind.Footway
            ? _pavement.ArcsOf(_ways.FootwayOf(way))
            : _pavement.JoinArcs(_ways.MitreOf(way));

        return arcs.Length > 0;
    }

    /// <summary>
    /// <b>What a walker on no way at all may step into</b> (PER-13). A body freshly handed a line, one that
    /// has stepped off its own to get past something, one under a hand: it holds the ground it stands on
    /// (<see cref="StandInTheWay"/>) and has no stretch in front of it to be cut on, so the permission is
    /// asked of the ground instead — is the patch this body would step into anybody's?
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The same question a manoeuvre asks</b> (<see cref="LaneOccupancy.AnybodyStandingOver"/>): a
    /// template runs over ground no way owns and the ways under it are all there is to ask, and a body with
    /// no line is in exactly that position. <b>It is a permission and not a distance</b> (PER-13) — there is
    /// ground to step into or there is not — because a body off every line has no metre to measure one along.
    /// </para>
    /// <para>
    /// <b>Without it a walker off its line was granted the whole town</b>: every walk begins on a line it has
    /// not walked a point of yet, and the give-up clock (PER-8) hands a held walker a fresh one — so the one
    /// thing that reliably freed a walker cut at a parked car was the tick after it stopped being cut at
    /// anything.
    /// </para>
    /// <para>
    /// <b>Asked over the whole reach and not at the end of it</b>: the box runs from the body outward, or a
    /// body standing inside the reach would be stepped straight through on the way past it.
    /// </para>
    /// <para>
    /// <b>And the reach is the margin this body keeps, not the ground it wants</b> — the same figure the walk
    /// on a way is cut short of a stretch by (<see cref="LaneCredit.AtAPlaceM"/>), and never less than what
    /// this tick commits it to. Asked over everything it is asking for, a walker is stopped a whole ask short
    /// of what it is walking at, and a paramedic never gets near enough to a casualty to pick one up.
    /// </para>
    /// </remarks>
    void GrantWhereItStands(int person)
    {
        // PHY-7: inside a container there is no body in the world, and nothing outside is in its way.
        if (People.Inside[person].Any || !People.Walking[person]) return;

        var positionM = People.PositionM[person];
        var strideM = People.DestinationM[person] - positionM;
        var lengthM = strideM.Length();
        if (lengthM < _config.CrossesOntoAWayM) return;

        var radiusM = People.RadiusM[person];
        var reachM = MathF.Min(lengthM, MathF.Max(StopsInM(person), _config.PersonStandstillGapM));
        var forward = strideM / lengthM;

        Span<WayUnder> under = stackalloc WayUnder[_pavement.MostWaysUnderAPlace];
        var count = GroundUnder.At(
            _pavement, positionM + (forward * (reachM * 0.5f)),
            new BodyFootprint(radiusM + (reachM * 0.5f), radiusM, forward), _config.CrossesOntoAWayM, under);

        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref under[index];
            if (!_occupancy.AnybodyStandingOver(
                    way.Way, way.AlongM + way.BackM, way.AlongM + way.AheadM, person, out var standing,
                    LaneRoster.Walking))
            {
                continue;
            }

            // Nothing holds a body off the place it is walking to (PER-24): arriving is what stops that walk,
            // and a permission read off the ground cannot say "you may not reach where you are going" without
            // stranding a paramedic a body's width short of the casualty it was sent to.
            if (IsWhereTheWalkIsGoing(person, in standing)) continue;

            People.AuthorityM[person] = 0f;
            People.HeldBy[person] = standing.Occupant;
            People.HeldByOf[person] = standing.Of;

            // The same reply the walk on a way makes to the same body (PER-24): waited for where it stands,
            // or stepped round with whatever room the cut leaves — which here is none, so what the step is
            // for is the clock behind it (<see cref="IsHeldByAStandstill"/>).
            if (IsComingThrough(standing)) return;

            People.StepsRound[person] = standing.Occupant;
            People.StepsRoundOf[person] = standing.Of;
            return;
        }
    }

    /// <summary>
    /// <b>Whether what is holding this walker is going nowhere</b> rather than queueing in front of it. A body
    /// under way down the same pavement gives its ground back by walking on, so the clock that gives up on a
    /// leg would be counting a wait that ends itself; a body going nowhere never does, and a walker cut at one
    /// is stopped rather than waiting (PER-8, PER-24).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is read off the holder and not off the stretch that cut this walker</b>, so every way a walk can
    /// be stopped by a standstill gives one answer: a body lying where it lies, a body at the very place this
    /// walk was going (<see cref="IsWhereTheWalkIsGoing"/>, which is never stepped round and so leaves nothing
    /// behind on the walker), and a car standing on the pavement, which is on no walk at all.
    /// </para>
    /// <para>
    /// <b>A body on no way is one of them however busily it is walking</b> (<see cref="IsAfoot"/>). What is on
    /// a way is in a queue with an order to it and gives its ground back down the line; a body off every way
    /// is held on the ground itself (<see cref="GrantWhereItStands"/>) and two of those can hold each other,
    /// which is a standstill that only a clock breaks. Left out, the town ran thirteen rings of walkers each
    /// waiting on the next.
    /// </para>
    /// </remarks>
    bool IsHeldByAStandstill(int person)
    {
        var holding = People.HeldBy[person];
        if (holding == PersonFleet.NoBody) return false;
        if (People.HeldByOf[person] != LaneRoster.Walking) return true;

        return !People.Walking[holding] || People.OnWay[holding] == PersonFleet.NoWay;
    }

    /// <summary>
    /// <b>Whether this body is standing where the walk is going</b> rather than in the way of it — the one
    /// body a walker never steps round (PER-24). The ground within a clearance of it is exactly the ground
    /// a step would be stepping out of, so a walker aiming inside that circle has an aim no step can reach:
    /// it would come round the body and round it again for as long as the leg lasted. A paramedic walks
    /// <em>at</em> a casualty, and a walker walks at its own doorway.
    /// </summary>
    /// <remarks>
    /// <b>It decides the reply and never the cut</b> (TER-4c.3): the body holds its stretch either way and
    /// the walk stops at it either way, so what this changes is that the walker stands a clearance short of
    /// where it was going instead of circling it.
    /// <b>Asked before a step is looked for at all</b> (<see cref="StepPastTheBody"/>), so a body standing
    /// at the end of the walk is never named as one to get past and no offset is ever tried against it.
    /// </remarks>
    bool IsWhereTheWalkIsGoing(int person, in LaneClaim lying)
    {
        WhereTheBodyInTheWayIs(lying.Occupant, lying.Of, out var bodyM, out var bodyRadiusM);

        var clearanceM = People.RadiusM[person] + bodyRadiusM + _config.PersonShoulderRoomM;
        return (bodyM - People.DestinationM[person]).LengthSquared() < clearanceM * clearanceM;
    }

    /// <summary>
    /// <b>Where on this walk a lane this body was refused begins</b>, in the walk's own metres, or infinity
    /// where it was refused none. <b>The refusal is not made here</b>: the road answered it when the
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
    /// whole of what the claims are for</b>. One that is gets queued behind however long it stands; one that
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
    /// off the near point alone it would stand at a negative distance, which is a body claimed before
    /// the way it is on begins — and every grant taken against it is then a grant over ground nobody is on.
    /// </para>
    /// <para>
    /// <b>The first point of a line is walked at like any other.</b> There is no point behind it to measure
    /// across, so the off-the-line test simply has nothing to say — and a body standing before the way that
    /// point is on has no way behind it to fall back to and is on none. Refused outright instead, every walk
    /// in the town began with its walker on no way at all, which is the one state the pavement's own grant
    /// could not answer for (<see cref="GrantWhereItStands"/>).
    /// </para>
    /// </remarks>
    bool IsAfoot(int person, out int way, out float alongM)
    {
        way = PersonFleet.NoWay;
        alongM = 0f;

        if (!People.Walking[person] || !People.IsOnItsFeet(person)) return false;

        // A hand at the keys aims a walker wherever it likes and the line under it is whatever was last
        // laid, so what would be claimed is where that walker was going before the hand took it.
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
        // walker on a lane claims ground nobody is on and queues a pavement behind it.
        //
        // <b>With no point behind it there is no stretch to measure against</b>, and a line freshly laid
        // from where a body got to says nothing about where that body stands across it — so it is not
        // placed until it has walked a point, which is the same answer the negative-distance case gives.
        if (at > 0
            && OffTheWalkM(points[at - 1], points[at], positionM) > _config.WalkerOffLaneM * OffLineTolerance)
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
            if (at == 0) return false;

            var before = WayOf(codes[at - 1]);
            if (before == PersonFleet.NoWay) return false;

            on = before;
            alongM = alongsM[at - 1] + MathF.Max(0f, (points[at] - points[at - 1]).Length() - toPointM);
        }

        way = on;
        alongM = MathF.Min(alongM, _occupancy.WayLengthM(way));
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

    /// <summary>
    /// The town's way number for a point of a walked line, or <see cref="PersonFleet.NoWay"/> for the hop
    /// off the network. <b>The one place a walked line's own encoding is spent</b>
    /// (<see cref="WalkedLine"/>): a stretch's own edge, or the complement of a mitre's turn slot.
    /// </summary>
    int WayOf(int code) =>
        code == WalkedLine.NoWay ? PersonFleet.NoWay
        : code >= 0 ? _ways.OfFootway(code)
        : _ways.OfMitre(~code);

    /// <summary>
    /// Anything that is not walking a line: a body standing about, one knocked off its feet, one somebody
    /// is steering by hand, the last stride of a walk off the network onto a doorstep. <b>It is where it
    /// lies</b>, which is a question for the pavement and not for a line it is no longer on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The same walk a car standing on the footway is written onto</b> (TER-4c.2,
    /// <see cref="LieOnThePavement"/>, <see cref="GroundUnder"/>): every way the body's own box touches — the
    /// lane it is on, the lane running back the other way where it reaches into it, and the mitres and lanes
    /// of a corner it is standing over. <b>And its own box and not a radius at a projection</b>
    /// (<see cref="BodyFootprint.CoversOn"/>), which is the reading every other body in the town is laid by.
    /// </para>
    /// <para>
    /// <b>The paint of a crossing among them</b> (TER-4c.2). A zebra is a way of the walk like any other and a
    /// body standing on one is standing on it; left out, the only claims a walker reads had nothing on that
    /// ground, so a walk went straight through somebody standing on the paint and no step was ever taken round
    /// one (PER-24). <b>It is the car that is left out and not the crossing</b>
    /// (<see cref="WalkedAlone"/>): what holds a walker off a car there is that car's stretch of the lane,
    /// looked up where the crossing runs over it — and that look-up is about the traffic that is
    /// <em>coming</em> (<see cref="LaneOccupancy.AnyTrafficOver"/>), which somebody standing in the road is
    /// not.
    /// </para>
    /// <para>
    /// <b>A place on a lane is projected onto that lane and never scaled onto it.</b> A lane is the stretch's
    /// curve moved a quarter of the band aside, so it is longer outside a bend and shorter inside one and
    /// carries the corner off its own end where that corner is nobody's choice
    /// (<see cref="WalkingNetwork.TailLengthM"/>): a share of the stretch walked, read as the same share of
    /// the lane, is a body drawn a corner's length from where it stands. <see cref="PavementWays"/> seeds the
    /// projection with that share and then projects, which is the whole of the difference.
    /// </para>
    /// <para>
    /// <b>And each row says how far aside of that way's line the body stands</b>
    /// (<see cref="LaneClaim.AsideM"/>, <see cref="LaneOccupancy.StandsAside"/>), which is what lets a body be
    /// written onto every way it touches without shutting every one of them: on the pavement the bar is nought,
    /// so what is in somebody's way is a body over the line they are walking. Written without it, one person
    /// standing at a corner held both lanes of every stretch meeting there and the mitres between them — a
    /// corner nobody could walk through.
    /// </para>
    /// </remarks>
    void StandInTheWay(int person)
    {
        var radiusM = People.RadiusM[person];

        Span<WayUnder> under = stackalloc WayUnder[_pavement.MostWaysUnderAPlace];
        var count = GroundUnder.At(
            _pavement, People.PositionM[person], BodyFootprint.Round(radiusM), _config.CrossesOntoAWayM,
            under);

        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref under[index];
            _occupancy.ClaimWhereItStands(
                way.Way, way.AlongM + way.BackM, way.AlongM + way.AheadM, way.AlongM + way.AheadM,
                Vector2.Dot(People.VelocityMps[person], way.AlongUnit), person, of: LaneRoster.Walking,
                acrossFromM: way.AcrossFromM, acrossToM: way.AcrossToM);
        }
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
