using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>Lane zero</b>: a road's roadside laid as a lane of its own (<see cref="LaneLines.IsRoadside"/>, GEN-57) — the
/// strip between a kerb and the lanes, at its own width and on its own middle, pointing the way traffic keeping to
/// that kerb would, and joined to nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>It runs on into the box</b> (<see cref="RunInto"/>). A lane stops at the junction's disc and the box is its
/// movements' ground, which stand a roadside in from the kerb, so a roadside that stopped there too stepped the kerb
/// in round the box by its whole width: a notch across the straight side of a tee, and a knob of pavement at every
/// corner. Carried on, it reaches the kerb of the next arm round on its side, and the corner the two kerbs make is
/// the box's — rounded as the rest of the ground is (TER-3c.10).
/// </para>
/// <para>
/// <b>Except where the road loses it along the way</b>: there it stops with its road, and the kerb eases in across the
/// box over a band of its own (<see cref="Tapers"/>). The lanes' outline follows a lane's square end exactly, and a
/// rounding that took the corner off cut into the lane; the taper is ground added past the end, so the kerb leaves
/// the roadside's edge on the heading it ran on and meets the next road's kerb on that one's.
/// </para>
/// <para>
/// <b>Where the kerb or the lane beside it runs straight on, it runs beside the movement that lane makes</b>, and not straight: a
/// straight piece laid beside a movement that bends by a degree runs a hair off that movement's edge for metres,
/// two edges a merge at a town's own scale cannot tell into one or two (<see cref="BandShell"/>). Laid off the
/// movement, its edge is the movement's own, as along its road it is the lane's.
/// </para>
/// </remarks>
internal sealed class RoadsideLanes
{
    /// <summary>One road's end at a box: where its line meets the box, the way out of the box along it, and how far its kerbs stand off that line.</summary>
    readonly record struct ArmEnd(int Road, bool AtTo, Vector2 AtM, Vector2 Out, float KerbM, byte Level);

    /// <summary>
    /// <b>The lanes a town's movements join, as far as a roadside is laid beside them</b>: where each road's lanes
    /// begin, the boxes each lane runs between and how wide it is, and the lines out of each — the movements driven,
    /// and the ones only paved where a lane is lost or gained (<see cref="LaneLines.Tapers"/>) — laid before any
    /// roadside is (<see cref="LaneLines.FirstRoadside"/>).
    /// </summary>
    public readonly record struct Joined(
        int[] FirstLaneOf, List<int> LaneFromJunction, List<int> LaneToJunction, List<float> LaneWidthM, Runs Driven,
        Runs Eased)
    {
        /// <summary>The line from one lane onto another, driven or eased, and whether there is one.</summary>
        public bool Between(int from, int to, out ReadOnlySpan<ArcSeg> arcs) =>
            Driven.Between(from, to, out arcs) || Eased.Between(from, to, out arcs);
    }

    /// <summary>Lines between lanes, flat: those out of lane i are At[i]..At[i + 1], each its run of arcs.</summary>
    public readonly record struct Runs(int[] At, int[] ToLane, int[] ArcOffsets, ArcSeg[] Arcs)
    {
        public ReadOnlySpan<ArcSeg> ArcsOf(int line) => Arcs.AsSpan(ArcOffsets[line], ArcOffsets[line + 1] - ArcOffsets[line]);

        /// <summary>The line from one lane onto another, and whether there is one.</summary>
        public bool Between(int from, int to, out ReadOnlySpan<ArcSeg> arcs)
        {
            for (var line = At[from]; line < At[from + 1]; line++)
            {
                if (ToLane[line] != to) continue;

                arcs = ArcsOf(line);
                return true;
            }

            arcs = default;
            return false;
        }
    }

    /// <summary>
    /// <b>The ground a kerb is eased in over</b> where a roadside is lost along the way (<see cref="Tapers"/>): a line and
    /// the width of the band it is, and the level it lies on.
    /// </summary>
    public readonly record struct Taper(ArcSeg[] Line, float WidthM, byte Level);

    /// <summary>The most pieces a movement is drawn in (<see cref="LaneLines"/>).</summary>
    const int MovementPieces = Spline.MostMovementArcs;

    readonly CityPlan.RoadArrays _roads;
    readonly CityPlan.JunctionArrays _junctions;
    readonly Joined _joined;
    readonly float _roadSideSign;
    readonly float _straightRad;

    /// <inheritdoc cref="RoadFigures.LineRoundedM"/>
    readonly float _roundedM;

    /// <summary>Count + 1 entries over <see cref="_arms"/>: junction <c>j</c>'s road ends are <c>_arms[_armAt[j].._armAt[j + 1]]</c>.</summary>
    readonly int[] _armAt;

    readonly ArmEnd[] _arms;
    readonly ArcSeg[] _reversed;
    readonly ArcSeg[] _half = new ArcSeg[MovementPieces];
    readonly ArcSeg[] _beside = new ArcSeg[MovementPieces];
    readonly List<Taper> _tapers = [];

    RoadsideLanes(
        CityPlan.RoadArrays roads, CityPlan.JunctionArrays junctions, Joined joined, SimConfig config, int[] armAt,
        ArmEnd[] arms, int mostPieces)
    {
        _roads = roads;
        _junctions = junctions;
        _joined = joined;
        _roadSideSign = config.RoadSideSign;
        _straightRad = config.Road.TurnStraightToleranceDeg * MathF.PI / 180f;
        _roundedM = config.Road.LineRoundedM;
        _armAt = armAt;
        _arms = arms;
        _reversed = new ArcSeg[mostPieces];
        MostPieces = mostPieces + (2 * MovementPieces);
    }

    /// <summary>The most pieces one roadside's line takes: its road's longest line and a run into the box at each end.</summary>
    public int MostPieces { get; }

    /// <summary>
    /// <b>Every taper laid so far</b>: one at each end of a roadside lost along the way (<see cref="RunInto"/>), laid as
    /// that roadside's line is (<see cref="LineInto"/>). Ground and no lane — nothing is driven over it and nothing joins
    /// it — so it is the shell's alone (<see cref="LaneShell"/>).
    /// </summary>
    public IReadOnlyList<Taper> Tapers => _tapers;

    /// <summary>Every road end at every box of the plan, which is what a roadside's run into one is reckoned against.</summary>
    public static RoadsideLanes Of(in GroundPieces ground, Joined joined, SimConfig config)
    {
        var roads = ground.Roads;
        var junctions = ground.Junctions;
        var armAt = new int[junctions.Count + 1];
        var mostPieces = 1;
        for (var road = 0; road < roads.Count; road++)
        {
            var pieces = roads.SegmentsOf(road).Length;
            if (pieces == 0) continue;

            mostPieces = Math.Max(mostPieces, pieces);
            armAt[roads.FromJunction[road] + 1]++;
            armAt[roads.ToJunction[road] + 1]++;
        }

        for (var junction = 0; junction < junctions.Count; junction++) armAt[junction + 1] += armAt[junction];

        var arms = new ArmEnd[armAt[^1]];
        var cursor = (int[])armAt.Clone();
        for (var road = 0; road < roads.Count; road++)
        {
            var line = roads.SegmentsOf(road);
            if (line.Length == 0) continue;

            var kerbM = roads.WidthM[road] * 0.5f;
            var level = roads.LevelOf(road);
            var last = line[^1];
            arms[cursor[roads.FromJunction[road]]++] = new ArmEnd(road, false, line[0].StartM, line[0].StartUnit, kerbM, level);
            arms[cursor[roads.ToJunction[road]]++] =
                new ArmEnd(road, true, last.EndM, -Heading.Unit(last.HeadingAtRad(last.LengthM)), kerbM, level);
        }

        return new RoadsideLanes(roads, junctions, joined, config, armAt, arms, mostPieces);
    }

    /// <summary>
    /// <b>The line one roadside of a road is laid on</b>: the road's own line moved out to the middle of the strip, run
    /// the way the traffic keeping to that kerb runs, and carried on into the box at either end (<see cref="RunInto"/>).
    /// </summary>
    /// <returns>How many pieces of <paramref name="into"/> it took, which wants <see cref="MostPieces"/>.</returns>
    public int LineInto(int road, bool withTheRoad, Span<ArcSeg> into, out float inTheBoxAtStartM, out float inTheBoxAtEndM)
    {
        var line = _roads.SegmentsOf(road);
        var stripM = _roads.RoadsideM(road, withTheRoad);
        var offsetM = (_roads.RoadsideLineM(road, withTheRoad) + (stripM * 0.5f)) * _roadSideSign;
        var middle = into[(2 * MovementPieces)..];
        if (withTheRoad)
        {
            Spline.OffsetInto(line, offsetM, middle);
        }
        else
        {
            Spline.ReverseInto(line, _reversed);
            Spline.OffsetInto(_reversed.AsSpan(0, line.Length), offsetM, middle);
        }

        // The lane sets off out of the box it starts at and runs into the one it ends at, so the strip on its one side
        // stands on opposite sides of the way out of the two.
        var side = offsetM < 0f ? -1f : 1f;
        var first = middle[0];
        var last = middle[line.Length - 1];
        Span<ArcSeg> before = stackalloc ArcSeg[MovementPieces];
        var ahead = RunInto(road, withTheRoad, !withTheRoad, side, arriving: false, first.StartM, first.HeadingRad, stripM, before);
        Span<ArcSeg> after = stackalloc ArcSeg[MovementPieces];
        var behind = RunInto(
            road, withTheRoad, withTheRoad, -side, arriving: true, last.EndM, last.HeadingAtRad(last.LengthM), stripM, after);

        inTheBoxAtStartM = Spline.TotalLengthM(before[..ahead]);
        inTheBoxAtEndM = Spline.TotalLengthM(after[..behind]);
        before[..ahead].CopyTo(into);
        middle[..line.Length].CopyTo(into[ahead..]);
        after[..behind].CopyTo(into[(ahead + line.Length)..]);
        return ahead + line.Length + behind;
    }

    /// <summary>
    /// <b>How one roadside runs on into the box at one end of its road</b>, as the pieces it takes there, run the lane's
    /// own way: on beside the movement the lane beside it makes where the kerb runs straight on to the next arm round
    /// (<see cref="AlongTheMovement"/>), and otherwise on to where its kerb meets that arm's (<see cref="KerbsMeetM"/>):
    /// beside the movement the lane beside it makes straight across the box where it makes one
    /// (<see cref="BesideTheWayAcross"/>), and straight on, the way the road points where it meets the box, where not —
    /// none where that corner stands out along the road already, the two carriageways overlapping that far.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>None where the roadside is lost along the way</b>: the next arm round carries none on the kerb facing it, and
    /// the box is one the kerb runs straight on through or one of two arms. The roadside stops where its road does,
    /// which is where the line beside it is painted to, and the kerb is eased in across the box instead
    /// (<see cref="Tapered"/>). A traced roadside was carried on along its street with the plan, so one lost along the way
    /// is lost where its street meets another, or where it would have laid its walk over another road
    /// (<see cref="CityGenFigures.TracedRoadsideShortestM"/>).
    /// </para>
    /// <para>
    /// <b>Only an arm on its own level is the next one round</b> (PHY-1a): a bridge leaving a box over the road has no
    /// kerb on the ground, and the roadside stops where its road does.
    /// </para>
    /// </remarks>
    /// <param name="side">Which side of the way out of the box the strip stands: positive on its right.</param>
    /// <param name="arriving">Whether the lane ends at this box rather than setting off from it.</param>
    /// <param name="atM">Where the lane meets the box: its end where it arrives, its start where it sets off.</param>
    int RunInto(
        int road, bool withTheRoad, bool atTo, float side, bool arriving, Vector2 atM, float headingRad, float stripM,
        Span<ArcSeg> into)
    {
        var junction = atTo ? _roads.ToJunction[road] : _roads.FromJunction[road];
        if (_junctions.RunsOff(junction)) return 0;

        var arms = _arms.AsSpan(_armAt[junction], _armAt[junction + 1] - _armAt[junction]);
        var own = 0;
        while (arms[own].Road != road || arms[own].AtTo != atTo) own++;

        var arm = arms[own];
        var middleM = _junctions.CentreM[junction];
        var endM = Vector2.Dot(arm.AtM - middleM, arm.Out);
        if (endM <= 0f) return 0;

        // The next arm round toward the strip's side: the least turn from this one's way out to its, turned that way.
        var next = -1;
        var nextRad = float.PositiveInfinity;
        var others = 0;
        for (var other = 0; other < arms.Length; other++)
        {
            if (other == own || arms[other].Level != arm.Level) continue;

            others++;
            var turnRad = side * MathF.Atan2(Spline.Cross(arm.Out, arms[other].Out), Vector2.Dot(arm.Out, arms[other].Out));
            if (turnRad <= 0f) turnRad += 2f * MathF.PI;
            if (turnRad >= nextRad) continue;

            next = other;
            nextRad = turnRad;
        }

        if (next < 0) return 0;

        var straightOn = MathF.Abs(nextRad - MathF.PI) <= _straightRad;
        var facesWith = FacesWith(arms[next], side);
        if ((straightOn || others == 1) && _roads.RoadsideM(arms[next].Road, facesWith) <= 0f)
        {
            Tapered(road, withTheRoad, junction, arms[next], facesWith, arriving, atM, headingRad, stripM);
            return 0;
        }

        if (straightOn)
        {
            var along = AlongTheMovement(road, withTheRoad, junction, arms[next], facesWith, side, arriving, atM, stripM, into);
            if (along >= 0) return along;
        }

        var reachM = endM - KerbsMeetM(arm, arms[next], nextRad, side, middleM);
        if (reachM <= 0f) return 0;

        var across = BesideTheWayAcross(road, withTheRoad, junction, arms, own, arriving, atM, reachM, stripM, into);
        if (across > 0) return across;

        var way = Heading.Unit(headingRad);
        into[0] = new ArcSeg(arriving ? atM : atM - (way * reachM), headingRad, reachM, 0f);
        return 1;
    }

    /// <summary>
    /// <b>The roadside run on beside the movement the lane beside it makes</b> between its road and the next arm round —
    /// the half of that movement nearer its road, moved out to the strip's middle and run the lane's own way — or −1
    /// where those two lanes are not joined, and the roadside is run straight instead.
    /// </summary>
    /// <remarks>
    /// <b>Half, so the roadside of the arm across meets it</b>: that one runs beside the same movement from the other
    /// end, and the two halves meet in the one place on it.
    /// </remarks>
    int AlongTheMovement(
        int road, bool withTheRoad, int junction, in ArmEnd nextArm, bool facesWith, float side, bool arriving,
        Vector2 atM, float stripM, Span<ArcSeg> into)
    {
        if (!Joining(road, withTheRoad, junction, nextArm, facesWith, out var mine, out var leavesMine, out var movement)) return -1;
        if (movement.IsEmpty) return 0;

        var lengthM = Spline.TotalLengthM(movement);
        var halfM = Halfway(movement, lengthM);
        var pieces = leavesMine
            ? Spline.SubChainInto(movement, 0f, halfM, _half)
            : Spline.SubChainInto(movement, halfM, lengthM, _half);

        return OffTheMovement(movement, leavesMine, mine, arriving, atM, stripM, pieces, into);
    }

    /// <summary>
    /// <b>The roadside run as far into the box as its kerb reaches, beside the movement the lane beside it makes onto the
    /// arm straight across</b> — or −1 where that lane is joined to no arm straight across, and the reach is run straight.
    /// </summary>
    /// <remarks>
    /// <b>The kerb turns to the next arm round, but the lane beside it carries straight on.</b> Laid along the road's own
    /// line, the reach runs beside a movement that bends by a degree to meet an arm a degree off straight, and the slit
    /// between the two widens over the box to a hand's breadth: wider than two bands touching and narrower than the weld
    /// a merge strings its rings at (<see cref="BandShell"/>), so a turn crossing it left the town's outline open.
    /// </remarks>
    int BesideTheWayAcross(
        int road, bool withTheRoad, int junction, ReadOnlySpan<ArmEnd> arms, int own, bool arriving, Vector2 atM,
        float reachM, float stripM, Span<ArcSeg> into)
    {
        var mine = Beside(road, withTheRoad);
        if (mine < 0) return -1;

        var leavesMine = _joined.LaneToJunction[mine] == junction;
        var straightAcross = -MathF.Cos(_straightRad);
        for (var other = 0; other < arms.Length; other++)
        {
            if (other == own || arms[other].Level != arms[own].Level) continue;
            if (Vector2.Dot(arms[own].Out, arms[other].Out) > straightAcross) continue;

            var first = _joined.FirstLaneOf[arms[other].Road];
            var lanes = _roads.LanesWithTheRoad(arms[other].Road) + _roads.LanesAgainstTheRoad(arms[other].Road);
            for (var lane = first; first >= 0 && lane < first + lanes; lane++)
            {
                var (from, to) = leavesMine ? (mine, lane) : (lane, mine);
                if (_joined.LaneToJunction[from] != junction || _joined.LaneFromJunction[to] != junction) continue;
                if (!_joined.Between(from, to, out var movement) || movement.IsEmpty) continue;

                var lengthM = Spline.TotalLengthM(movement);
                var runM = ShortOfAJointM(movement, MathF.Min(reachM, lengthM), leavesMine);
                var pieces = leavesMine
                    ? Spline.SubChainInto(movement, 0f, runM, _half)
                    : Spline.SubChainInto(movement, lengthM - runM, lengthM, _half);

                return OffTheMovement(movement, leavesMine, mine, arriving, atM, stripM, pieces, into);
            }
        }

        return -1;
    }

    /// <summary>
    /// <b>A stretch of a movement moved out to the strip's middle</b> (<see cref="_half"/>, <paramref name="pieces"/> of
    /// it) and run the lane's own way, on whichever hand of the movement the strip stands where it meets this road — or
    /// −1 where it bends tighter than a kerb is laid.
    /// </summary>
    /// <param name="leavesMine">Whether the movement leaves the lane beside the roadside rather than arriving on it.</param>
    int OffTheMovement(
        ReadOnlySpan<ArcSeg> movement, bool leavesMine, int mine, bool arriving, Vector2 atM, float stripM, int pieces,
        Span<ArcSeg> into)
    {
        var meets = Spline.SampleAt(movement, leavesMine ? 0f : Spline.TotalLengthM(movement));
        var outM = (_joined.LaneWidthM[mine] + stripM) * 0.5f;
        Spline.OffsetInto(_half.AsSpan(0, pieces), Vector2.Dot(atM - meets.PositionM, meets.Right) < 0f ? -outM : outM, _beside);

        // A movement that bends tighter than that is no kerb running straight on: moved out to the strip it bends
        // tighter still, and a band whose inner edge turns on a circle tighter than the ground is rounded at is
        // detail the ground does not keep.
        var tightestM = (stripM * 0.5f) + _roundedM;
        for (var piece = 0; piece < pieces; piece++)
        {
            if (_beside[piece].LengthM <= 0f || MathF.Abs(_beside[piece].Curvature) * tightestM > 1f) return -1;
        }

        // Into the box where the lane ends, out of it where it sets off: the movement's own way where it leaves this
        // road that the lane arrives on, or arrives on this road that the lane sets off along.
        if (arriving == leavesMine) _beside.AsSpan(0, pieces).CopyTo(into);
        else Spline.ReverseInto(_beside.AsSpan(0, pieces), into);

        return pieces;
    }

    /// <summary>
    /// <b>How far along a movement a stretch of it from one end is taken</b>: as far as asked, or back to the joint
    /// behind where that lands within a weld past it (<see cref="ArcRings.WeldM"/>) — the same piece a few centimetres
    /// long that a stretch cut near a joint is left with (<see cref="Halfway"/>).
    /// </summary>
    /// <param name="fromStart">Whether the stretch is taken from the movement's start rather than back from its end.</param>
    static float ShortOfAJointM(ReadOnlySpan<ArcSeg> movement, float runM, bool fromStart)
    {
        var jointM = 0f;
        for (var piece = 0; piece + 1 < movement.Length; piece++)
        {
            jointM += movement[fromStart ? piece : movement.Length - 1 - piece].LengthM;
            if (jointM >= runM) break;
            if (runM - jointM < ArcRings.WeldM) return jointM;
        }

        return runM;
    }

    /// <summary>
    /// <b>The kerb eased in across the box where a roadside is lost along the way</b> (<see cref="Tapers"/>): a band of
    /// the strip's width from the roadside's own end, on the heading it ends on, to the far end of the movement the lane
    /// beside it makes onto the next arm — on that lane's own line, where the band is wholly inside the lane and its
    /// outer edge has crossed into the lane's kerb. None where the two lanes are not joined, where the strip is as wide as
    /// the lane it would end inside, or where the box is too short to ease in over without a turn tighter than the
    /// ground is rounded at (<see cref="Spline.CorneredInto"/>): the kerb steps in there, round the roadside's square end.
    /// </summary>
    void Tapered(
        int road, bool withTheRoad, int junction, in ArmEnd nextArm, bool facesWith, bool arriving, Vector2 atM,
        float headingRad, float stripM)
    {
        if (!Joining(road, withTheRoad, junction, nextArm, facesWith, out _, out var leavesMine, out var movement)) return;
        if (movement.IsEmpty || stripM >= _joined.LaneWidthM[Beside(nextArm.Road, facesWith)]) return;

        var far = Spline.SampleAt(movement, leavesMine ? Spline.TotalLengthM(movement) : 0f);
        var farRad = leavesMine ? far.HeadingRad : far.HeadingRad + MathF.PI;
        var intoRad = arriving ? headingRad : headingRad + MathF.PI;
        Span<ArcSeg> line = stackalloc ArcSeg[2];
        var pieces = Spline.CorneredInto(atM, intoRad, far.PositionM, farRad, (stripM * 0.5f) + _roundedM, line);
        if (pieces > 0) _tapers.Add(new Taper(line[..pieces].ToArray(), stripM, _roads.LevelOf(road)));
    }

    /// <summary>
    /// <b>The movement between the lane beside a roadside and the lane beside the next arm's kerb facing it</b>, laid
    /// whichever way the traffic runs through the box — or false where those two lanes are not joined there.
    /// </summary>
    /// <param name="mine">The lane beside the roadside.</param>
    /// <param name="leavesMine">Whether that lane runs into the box, the movement leaving it rather than arriving on it.</param>
    bool Joining(
        int road, bool withTheRoad, int junction, in ArmEnd nextArm, bool facesWith, out int mine, out bool leavesMine,
        out ReadOnlySpan<ArcSeg> movement)
    {
        mine = Beside(road, withTheRoad);
        var theirs = Beside(nextArm.Road, facesWith);
        leavesMine = mine >= 0 && _joined.LaneToJunction[mine] == junction;
        movement = default;
        if (mine < 0 || theirs < 0) return false;

        var (from, to) = leavesMine ? (mine, theirs) : (theirs, mine);
        return _joined.LaneToJunction[from] == junction && _joined.LaneFromJunction[to] == junction
            && _joined.Between(from, to, out movement);
    }

    /// <summary>
    /// Whether the next arm's kerb facing a roadside is the one its road's traffic keeps to running with it: the side of
    /// it that traffic keeps to whichever way that road runs.
    /// </summary>
    /// <param name="side">Which side of this arm's way out the roadside stands: positive on its right.</param>
    bool FacesWith(in ArmEnd nextArm, float side)
    {
        var facing = Heading.RightOf(nextArm.Out) * -side;
        var nextWay = nextArm.AtTo ? -nextArm.Out : nextArm.Out;
        return Vector2.Dot(facing, Heading.RightOf(nextWay) * _roadSideSign) > 0f;
    }

    /// <summary>
    /// <b>Where a movement is halved</b>: at the joint between its own pieces nearest its middle, or in the middle of
    /// a movement of one piece. Cut anywhere else, a piece of it near the cut is left as short as the cut is near a
    /// joint, and a band laid along a piece a few centimetres long is no band a merge can close.
    /// </summary>
    static float Halfway(ReadOnlySpan<ArcSeg> movement, float lengthM)
    {
        var halfM = lengthM * 0.5f;
        var nearestM = movement.Length > 1 ? float.NaN : halfM;
        var jointM = 0f;
        for (var piece = 0; piece + 1 < movement.Length; piece++)
        {
            jointM += movement[piece].LengthM;
            if (float.IsNaN(nearestM) || MathF.Abs(jointM - halfM) < MathF.Abs(nearestM - halfM)) nearestM = jointM;
        }

        return nearestM;
    }

    /// <summary>
    /// <b>The lane beside one roadside of a road</b>: the kerb lane of the traffic keeping to that kerb, or — where none
    /// keeps to it, the road being driven the other way alone — the innermost lane of that way; −1 for a road of no lane.
    /// </summary>
    int Beside(int road, bool withTheRoad)
    {
        var first = _joined.FirstLaneOf[road];
        var with = _roads.LanesWithTheRoad(road);
        var against = _roads.LanesAgainstTheRoad(road);
        if (first < 0 || with + against == 0) return -1;

        return withTheRoad
            ? with > 0 ? first : first + against - 1
            : against > 0 ? first + with : first + with - 1;
    }

    /// <summary>
    /// <b>Where one arm's kerb meets the next arm's round the box</b>, out along the first from the middle of the box —
    /// or the middle itself, where the two make no corner.
    /// </summary>
    /// <remarks>
    /// <b>A corner is out along both arms, or back past the middle along both</b>: the inside of a turn, or the outside
    /// of a bend, whose kerb mitres past the middle no further than it stands off its own line. Two kerbs straight on
    /// from each other are parallel and meet nowhere, and two of different widths all but straight on meet far out
    /// along one arm and behind the other, which is a kerb stepping and no corner — so a kerb stepping from a wider
    /// road to a narrower one straight on steps in the middle.
    /// </remarks>
    /// <param name="turnRad">How far round toward the strip's side the next arm leaves, from the first's way out.</param>
    static float KerbsMeetM(in ArmEnd arm, in ArmEnd next, float turnRad, float side, Vector2 middleM)
    {
        if (MathF.Abs(turnRad - MathF.PI) <= LineTolerance.StraightOnRad) return 0f;

        var kerbM = arm.AtM + (Heading.RightOf(arm.Out) * (side * arm.KerbM));
        var facing = Heading.RightOf(next.Out) * -side;
        var meetM = kerbM + (arm.Out * ((next.KerbM - Vector2.Dot(kerbM - next.AtM, facing)) / Vector2.Dot(arm.Out, facing)));
        var alongM = Vector2.Dot(meetM - middleM, arm.Out);
        var alongNextM = Vector2.Dot(meetM - middleM, next.Out);

        var corner = turnRad < MathF.PI ? alongM >= 0f && alongNextM >= 0f : alongM <= 0f && alongNextM <= 0f;
        return corner && float.IsFinite(alongM) ? MathF.Max(alongM, -arm.KerbM) : 0f;
    }
}
