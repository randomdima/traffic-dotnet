using System.Numerics;
using System.Runtime.CompilerServices;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The walkers' half of the claims, which is two claims and no arithmetic</b> (PER-26): the ground a
/// body is standing on, held at <see cref="ClaimPriority.Hard"/>, and the ground it is walking at, stated
/// at <see cref="ClaimPriority.Soft"/>. Both are laid from the body every tick and neither is answered.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no grant on this side of the town.</b> A driver asks for road and is handed a distance
/// because a car has a speed profile to spend it on; a walker's pace is a cap and never a curve (PER-3),
/// so a distance in front of one buys nothing that the ground it is standing on does not already say. What
/// the two claims are for is that the rest of the town can see a walker — the traffic is held off the body
/// by the first and off the paint it is stepping onto by the second — and <b>what a walker does about
/// another walker is the solver's</b> (PHY-1), not a queue's.
/// </para>
/// <para>
/// <b>It is the same table and the same ladder as the road's</b> (<see cref="LaneOccupancy"/>,
/// <see cref="TownWays"/>, TER-5g), and deliberately not a second mechanism. A body holds the ground it
/// occupies whatever kind of body it is (TER-4c.2), and states the ground it means to use like any driver
/// (TER-5g) — so nothing reading the claims learns that a walker exists.
/// </para>
/// <para>
/// <b>A way is one side of one stretch</b>, or the mitre between two of them. The two directions of a
/// pavement are two lines half a band apart (<see cref="WalkingNetwork.LaneOffsetM"/>), so somebody coming
/// the other way is on other ground — which falls out of the ways rather than being tested for.
/// </para>
/// <para>
/// <b>Where a body stands is read off the line it is walking and never searched for.</b> Every point of a
/// walked line carries the way it was stationed on and how far along that way it stands
/// (<see cref="WalkedLine"/>), so a walker's place on the network costs a subtraction. Only a body that is
/// on no line at all — standing about, knocked over, under a hand — is looked up, and that answer is what
/// says it is off the network and walking straight back onto it (PER-25).
/// </para>
/// <para>
/// <b>What is never written onto the pavement is a car on the paint</b> (TER-5c.1). A zebra is a walk laid
/// over a carriageway, so the ground under it has two names and one owner: the car's stretch of it is a
/// stretch of the <em>lane</em>. A body on foot standing there is written onto both, because the look-up
/// that answers for a car is about the traffic that is coming and a person in a lane is not an answer to
/// it.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>
    /// How many ways one walker's statement may run over: the way it is on and the ones its walk crosses
    /// onto before it gets where it is aiming. A bound on a stack span and not a figure behaviour reads.
    /// </summary>
    /// <remarks>
    /// <b>Reached, the ways at the far end go unstated</b>, which costs a walker nothing it was holding — a
    /// statement is not ground anybody was given. A corner is two short ways within a stride, so the count
    /// is what a body on a corner can cover rather than what a stretch of pavement suggests.
    /// </remarks>
    const int MostWaysAlongAWalk = 5;

    /// <summary>
    /// How many claims one walker may lay at once: every way its own box is over (PER-26's first), and the
    /// ways along the walk in front of it (PER-26's second). <b>A body lays both</b>, which is the whole of
    /// what it tells the town.
    /// </summary>
    static int MostSlotsPerWalker(in PavementWays pavement) =>
        MostWaysAlongAWalk + pavement.MostWaysUnderAPlace;

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
    /// where it is on none of it — <b>which is also what decides how it walks</b> (PER-25): a body on a way
    /// follows the line the network laid it, and a body on none of them walks straight at the nearest of
    /// them.
    /// </summary>
    /// <remarks>
    /// <b>Worked out before either network is laid, because both read it</b> (<see cref="RebuildLaneOccupancy"/>):
    /// the road needs the way a body on a crossing is walking to know which lane it stands in, and the
    /// statement in front of the body begins from the same place. Asked twice it was the same walk of the
    /// same line, and the two answers were a tick apart.
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
    /// <b>PER-26's first claim: the ground under this body, at p0.</b> Every way of the pavement its own
    /// box is over — the lane it is on, the lane running back the other way where it reaches into it, the
    /// mitres of a corner it is standing across, and the paint of a crossing it is standing on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The same walk a car standing on the footway is written onto</b> (TER-4c.2,
    /// <see cref="LieOnThePavement"/>, <see cref="GroundUnder"/>), <b>and its own box rather than a radius at
    /// a projection</b> (<see cref="BodyFootprint.CoversOn"/>) — the reading every other body in this town is
    /// laid by.
    /// </para>
    /// <para>
    /// <b>It is laid for every walker and not only for the ones standing about.</b> A body is a body whether
    /// it is walking a line or lying where it was knocked down, and nothing takes the ground somebody is
    /// already on (TER-5g, p0) — so there is one row per way per walker and no state that says which kind of
    /// walker this is.
    /// </para>
    /// <para>
    /// <b>And each row says how far aside of that way's line the body stands</b>
    /// (<see cref="LaneClaim.AsideM"/>), which is what lets a body be written onto every way it touches
    /// without shutting every one of them. Written without it, one person standing at a corner held both
    /// lanes of every stretch meeting there and the mitres between them — a corner nobody could walk through.
    /// </para>
    /// </remarks>
    [SkipLocalsInit]
    void HoldThePavementUnderIt(int person)
    {
        // PHY-7: inside a container there is no body in the world and nothing in anybody's way.
        if (People.Inside[person].Any) return;

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
    /// <b>PER-26's second claim: the pavement this body is walking at, at p9.</b> From its own front to
    /// where it is aiming, on each way that stretch runs over — a statement of where it is going and never
    /// ground anybody handed it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A body on no way of the network states nothing</b>, because there is no way to state it on: it is
    /// walking straight at the network (PER-25) over ground the town does not number, and what it holds
    /// while it does is the box it is standing in.
    /// </para>
    /// <para>
    /// <b>It is the weakest hold there is and it is meant to be</b> (TER-5g). Everything stronger takes it
    /// and nothing on the pavement is refused by it, so two walkers walking at the same doorway both get
    /// there; what the statement is worth is that the town can see where somebody is going, which is what a
    /// debug layer draws and what a crossing reads (<see cref="StateTheBandAhead"/>).
    /// </para>
    /// </remarks>
    void StateThePavementAhead(int person, Span<LineWay> ways)
    {
        // PHY-7: inside a container there is no body in the world and nothing in anybody's way.
        if (People.Inside[person].Any) return;
        if (!People.Walking[person] || People.OnWay[person] == PersonFleet.NoWay) return;

        var alongMps = AlongItsWalkMps(person);
        var count = WaysAlongTheWalk(person, backM: 0f, StatesAheadM(person), ways);
        for (var index = 0; index < count; index++)
        {
            ref readonly var over = ref ways[index];
            _occupancy.ClaimAhead(
                over.Way, over.FromM, over.ToM, alongMps, person, ClaimPriority.Soft, LaneRoster.Walking);
        }
    }

    /// <summary>
    /// <b>How far in front of itself a body states ground</b>: what it needs to come to rest at the pace it
    /// walks, plus the gap it keeps.
    /// </summary>
    /// <remarks>
    /// <b>Sized by the pace it walks at and not by what it is doing</b>, exactly as a driver's statement is:
    /// a walker stopped behind something states the ground it would set off into, or nothing would ever say
    /// it meant to move. <b>It is also the reach of the statement on the road's side</b> — how near a lane of
    /// a crossing has to be before this body says it is stepping onto it
    /// (<see cref="StateTheBandAhead"/>) — so the carriageway and the footway are stated the same distance
    /// in front of one body and not two figures that drift apart.
    /// </remarks>
    float StatesAheadM(int person) =>
        StoppingM(_config.PersonWalkSpeedMps, FootGripMps2(person)) + _config.PersonStandstillGapM;

    /// <summary>
    /// Whether this walker is a body on a line of its own rather than a shape on the pavement — <b>the
    /// whole of what says which of PER-25's two walks it is taking</b>.
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
    /// the way it is on begins.
    /// </para>
    /// <para>
    /// <b>The first point of a line is walked at like any other, and it is the ground that says so.</b>
    /// There is no point behind it for the off-the-line test to measure across, so the same bar is put to
    /// the way itself instead (<see cref="OffTheWayM"/>) — a body standing where it would be stationed is
    /// on that way, and one standing across a field from it is on none and is walking at the network.
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
        // side of it is standing somewhere else, whatever its line still says — and a body standing
        // somewhere else is one walking back onto the network (PER-25).
        //
        // <b>With no point behind it there is no stretch of its own walk to measure against</b>, so the
        // same bar is held to the way instead, below.
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
        return at > 0 || OffTheWayM(way, positionM, alongM, toPointM) <= _config.WalkerOffLaneM * OffLineTolerance;
    }

    /// <summary>
    /// <b>Where on this way the body actually stands, and how far off it</b> — the same bar as
    /// <see cref="OffTheWalkM"/> put to the ground rather than to the walk, for the one body that has no
    /// stretch of its own walk behind it to be measured against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What it refuses is a body standing nowhere near the way its freshly laid line names. The distance
    /// alone used to say so — a first point near the start of its own way leaves a body short of it a
    /// negative distance along — but only while a stretch was short enough for that to be true of it. On a
    /// pavement laid in one piece from junction to junction the first point stands eighty metres along one,
    /// and a body that had walked none of its line claimed ground across a field.
    /// </para>
    /// <para>
    /// <b>The line is searched and not sampled at the one metre</b>, and that is what makes the bar honest.
    /// Stepping back from the first point by the straight to it reads the walk's own metres, and a body
    /// standing <em>beside</em> that point has its whole sideways distance taken off its place on the way as
    /// well. Read at that metre alone the body stands metres from a line it is on, so the subtraction only
    /// seeds a window and the way itself is searched in it. <b>The metre is left as it was</b>: a way's
    /// metres are its lane's, and this line is the stretch the lane is offset from, so a place found along
    /// it is not a place along the way.
    /// </para>
    /// </remarks>
    float OffTheWayM(int way, Vector2 atM, float aroundM, float windowM)
    {
        var arcs = _ways.KindOf(way) == WayKind.Footway
            ? _pavement.ArcsOf(_ways.FootwayOf(way))
            : _pavement.ConnectorArcs(_ways.MitreOf(way));
        if (arcs.Length == 0) return 0f;

        var alongM = Spline.ProjectM(arcs, atM, aroundM, windowM + _config.WalkerOffLaneM);

        return (Spline.SampleAt(arcs, alongM).PositionM - atM).Length() - _config.WalkerOffLaneM;
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
    /// The ways of the pavement under a stretch of one walk — from <paramref name="backM"/> behind the body
    /// to <paramref name="aheadM"/> in front of it — each with the metres of its own that the stretch
    /// covers and where its near edge falls back on the walk.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The walk is its own measure here</b>, and <see cref="LineWay.LineFromM"/> is a distance from the
    /// body rather than from a line's origin: a walked line is re-laid from wherever the body has got to,
    /// so it has no origin two walkers could be compared against.
    /// </para>
    /// <para>
    /// <b>A station's way is the ground walked to reach it</b>, so the stretch between two stations belongs
    /// to the later of the two — which is what puts the mitre before a corner on the corner rather than on
    /// the pavement leading up to it.
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

            // The statement may end part-way to a station. Metres along a way and metres walked are the
            // same metres to within the bow of the straight between two of them, so what is left of it is
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
