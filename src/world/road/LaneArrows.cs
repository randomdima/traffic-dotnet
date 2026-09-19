using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The arrows a lane is painted with</b> (TER-6a): one glyph on every lane that carries a bar, standing a
/// setback clear behind it, saying which of the movements out of that lane the junction in front of it
/// offers — <b>one branch per turn, bent by that turn's own angle</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>A shaft and the branches that leave it, and no catalogue of glyphs.</b> Left, straight and right are
/// not three shapes with combinations drawn for each pair: they are one shaft down the lane and a branch for
/// every kind of turn the lane offers, so a lane that turns both ways and goes straight on is drawn by the
/// same arithmetic as one that only turns.
/// </para>
/// <para>
/// <b>A branch is one arc, and the bend is the movement's own</b> (<see cref="Spline.TurnedRad"/>): the
/// heading the line a car is driven over that movement spends, which is the angle the turn was classified by
/// (<c>LaneLines.ConnectorKind</c>). So a slip road drawn at thirty degrees and a square corner are drawn as
/// what they are, rather than both as the same right angle.
/// </para>
/// <para>
/// <b>What the bend's radius is solved out of is how far across the lane the arrow may reach</b>
/// (<see cref="RoadFigures.LaneArrowReachAcrossInLaneWidths"/>), and the sweep is kept whatever that comes
/// to: the sharper the turn, the tighter the curl, and a gentle one sweeps the whole run after the fork and
/// reaches less far across for it. A bend that would take more room than the arrow's own length is cut to
/// the length and keeps its angle.
/// </para>
/// <para>
/// <b>Every arrow is one length down its lane, whatever it says</b>: the far end of the furthest-reaching
/// branch stands at the arrow's own length from its tail, and <b>the shaft takes up whatever the branches did
/// not spend getting there</b> — a turn advances less than a straight for the room it takes, so a lane of
/// turns alone carries a longer shaft rather than a shorter arrow. So the paint on every approach in the town
/// is the same size and stands the same setback behind its bar, which is what makes a row of them readable.
/// </para>
/// <para>
/// <b>Read off the lane and its bar</b>: the lane says what may be driven off it and how wide the ground is,
/// and the bar says where the paint in front of the arrow stands (<see cref="StopBars.AlongM"/>), so an arm
/// with no crossing carries no bar and no arrow either.
/// </para>
/// </remarks>
internal sealed class LaneArrows
{
    readonly int[] _lane;
    readonly float[] _fromM;
    readonly float[] _forkM;
    readonly int[] _branchAt;
    readonly ArcSeg[] _branch;
    readonly LaneTurn[] _branchTurn;

    LaneArrows(int[] lane, float[] fromM, float[] forkM, int[] branchAt, ArcSeg[] branch, LaneTurn[] branchTurn)
    {
        _lane = lane;
        _fromM = fromM;
        _forkM = forkM;
        _branchAt = branchAt;
        _branch = branch;
        _branchTurn = branchTurn;
    }

    public int Count => _lane.Length;

    /// <summary>The lane it is painted on, whose own line the shaft is laid down.</summary>
    public ReadOnlySpan<int> Lane => _lane;

    /// <summary>Where the shaft begins in that lane's own metres, and where the branches leave it.</summary>
    public ReadOnlySpan<float> FromM => _fromM;

    /// <inheritdoc cref="FromM"/>
    public ReadOnlySpan<float> ForkM => _forkM;

    /// <summary>Count + 1 entries: the branches of arrow i are <c>BranchAt[i]..BranchAt[i + 1]</c>.</summary>
    public ReadOnlySpan<int> BranchAt => _branchAt;

    /// <summary>
    /// One branch apiece: the arc from the fork to the base of the head it ends in, carrying the lane's own
    /// curvature where the movement goes straight on and the bend's where it turns.
    /// </summary>
    public ReadOnlySpan<ArcSeg> Branch => _branch;

    /// <summary>And which of the three turns each branch is there to say.</summary>
    public ReadOnlySpan<LaneTurn> BranchTurn => _branchTurn;

    /// <summary>The branches of one arrow, in the order they are read across the lane.</summary>
    public ReadOnlySpan<ArcSeg> BranchesOf(int arrow) =>
        _branch.AsSpan(_branchAt[arrow], _branchAt[arrow + 1] - _branchAt[arrow]);

    /// <summary>
    /// Every arrow the town is painted with, laid off the lanes and the bars they hold at.
    /// </summary>
    /// <remarks>
    /// <b>A lane with no room for a whole arrow behind its bar carries none</b>, rather than a shortened one:
    /// the glyph is read at a glance and a stub of it reads as a different glyph.
    /// </remarks>
    public static LaneArrows Lay(LaneLines lanes, StopBars bars, SimConfig config)
    {
        var figures = config.Road;
        var lengthM = figures.LaneArrowLengthM;
        var headM = figures.LaneArrowHeadLengthM;
        var runM = lengthM - figures.LaneArrowShaftM - headM;
        if (runM <= 0f || figures.LaneArrowShaftWidthM <= 0f) return new LaneArrows([], [], [], [0], [], []);

        var lane = new List<int>();
        var fromM = new List<float>();
        var forkM = new List<float>();
        var branchAt = new List<int> { 0 };
        var branch = new List<ArcSeg>();
        var branchTurn = new List<LaneTurn>();

        var turnRad = new float[Turns];
        var offered = new bool[Turns];
        var said = new LaneTurn[Turns];
        var bent = new (float ArcM, float Curvature, float AdvanceM)[Turns];
        for (var bar = 0; bar < bars.Count; bar++)
        {
            var at = bars.Lane[bar];
            var tipM = bars.AlongM[bar] - (bars.ThicknessM[bar] * 0.5f) - figures.LaneArrowSetbackM;
            var tailM = tipM - lengthM;
            if (tailM <= 0f) continue;

            Array.Clear(offered);
            Array.Clear(turnRad);
            for (var connector = lanes.ConnectorAt[at]; connector < lanes.ConnectorAt[at + 1]; connector++)
            {
                // <b>The sharpest of a kind is the one drawn</b>: two ways round to the same hand is one
                // branch, and the tighter of them is what the lane is asking a driver to be ready for.
                var kind = (int)lanes.ConnectorKind[connector];
                var turned = Spline.TurnedRad(lanes.ArcsOfConnector(connector));
                if (!offered[kind] || MathF.Abs(turned) > MathF.Abs(turnRad[kind])) turnRad[kind] = turned;
                offered[kind] = true;
            }

            var says = Across(turnRad, offered, said);
            if (says == 0) continue;

            // <b>The branches are shaped before the shaft they leave is measured</b>: how far one of them
            // reaches down the lane is what the bend came to, and the shaft is the rest of the arrow's own
            // length — so the glyph ends where a straight one would however sharply it turns.
            var reachM = lanes.LaneWidthM[at] * figures.LaneArrowReachAcrossInLaneWidths;
            var advanceM = 0f;
            for (var showing = 0; showing < says; showing++)
            {
                bent[showing] = Bend(said[showing], turnRad[(int)said[showing]], runM, reachM, figures);
                advanceM = MathF.Max(advanceM, bent[showing].AdvanceM);
            }

            var standsM = tailM + lengthM - advanceM;
            var fork = Spline.SampleAt(lanes.ArcsOf(at), standsM);
            for (var showing = 0; showing < says; showing++)
            {
                var kind = said[showing];
                var curvature = kind == LaneTurn.Straight ? fork.Curvature : bent[showing].Curvature;
                branch.Add(new ArcSeg(fork.PositionM, fork.HeadingRad, bent[showing].ArcM, curvature));
                branchTurn.Add(kind);
            }

            lane.Add(at);
            fromM.Add(tailM);
            forkM.Add(standsM);
            branchAt.Add(branch.Count);
        }

        return new LaneArrows(
            [.. lane], [.. fromM], [.. forkM], [.. branchAt], [.. branch], [.. branchTurn]);
    }

    /// <summary>
    /// <b>One branch of a glyph: the arc from the fork to the base of its head</b>, and <b>how far down the
    /// lane the head at the end of it reaches</b> — which is what says where the fork has to stand for the
    /// arrow to be its own length. A movement going straight on carries the lane's own curvature on for the
    /// whole run, so the arrow bends with the street it is painted on; one that turns gets the bend its own
    /// angle asks for and a straight nowhere.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The radius is what the reach across the lane leaves once the head has taken its own share of it</b>:
    /// the head's base stands off the shaft by <c>r·(1 − cos θ)</c>, its tip reaches <c>head·sin θ</c> further
    /// over and its outer barb <c>½·width·cos θ</c> further again, so the radius is the one at which the whole
    /// glyph spends exactly the allowance — the far corner of it and not the middle.
    /// </para>
    /// <para>
    /// <b>The sweep is kept and the length gives</b>. Where the arc the radius asks for is longer than the
    /// run left after the shaft, it is cut to the run and the radius read back off it — the arrow then
    /// reaches less far across the lane than it was allowed to, which is a mark drawn smaller and not a
    /// different mark. <b>And no bend is tighter than the paint is wide</b>, which is the floor under a turn
    /// sharp enough that its head alone spends the whole reach.
    /// </para>
    /// <para>
    /// <b>What it advances is the chord of the bend and the furthest corner of the head</b>, both in the
    /// direction the shaft points: a straight spends the whole of it going forward and a square corner spends
    /// most of it going sideways, which is the difference the shaft behind them makes up.
    /// </para>
    /// </remarks>
    static (float ArcM, float Curvature, float AdvanceM) Bend(
        LaneTurn kind, float turnedRad, float runM, float reachM, RoadFigures figures)
    {
        var mostRad = figures.LaneArrowBendMostDeg * MathF.PI / 180f;
        var sweepRad = Math.Clamp(turnedRad, -mostRad, mostRad);

        // A turn drawn over a line that turns through nothing is a straight arrow: the two lane ends butt,
        // which no town lays (<c>LaneLines.SameEndM</c>), and a bend of nothing has no radius.
        if (kind == LaneTurn.Straight || sweepRad == 0f)
        {
            return (runM, 0f, runM + figures.LaneArrowHeadLengthM);
        }

        var acrossM = reachM
            - (figures.LaneArrowHeadLengthM * MathF.Sin(MathF.Abs(sweepRad)))
            - (figures.LaneArrowHeadWidthM * 0.5f * MathF.Cos(sweepRad));
        var radiusM = MathF.Max(
            acrossM / (1f - MathF.Cos(sweepRad)), figures.LaneArrowShaftWidthM * 0.5f);
        var arcM = MathF.Min(radiusM * MathF.Abs(sweepRad), runM);
        radiusM = arcM / MathF.Abs(sweepRad);

        var headM = MathF.Max(
            figures.LaneArrowHeadLengthM * MathF.Cos(sweepRad),
            figures.LaneArrowHeadWidthM * 0.5f * MathF.Sin(MathF.Abs(sweepRad)));
        return (arcM, sweepRad / arcM, (radiusM * MathF.Sin(MathF.Abs(sweepRad))) + headM);
    }

    /// <summary>
    /// The turns one arrow says, in the order they stand across the lane — the near-side turn to the kerb
    /// hand, the straight on the shaft's own line and the far-side turn across the road.
    /// </summary>
    /// <remarks>
    /// <b>Ordered by the angle itself and not by the classification</b>, so which hand a branch is drawn to
    /// is read off the same number that bends it. <b>The set is emptied as it is read</b>, one lane's worth
    /// of it being filled again before the next lane asks.
    /// </remarks>
    static int Across(float[] turnRad, bool[] offered, Span<LaneTurn> into)
    {
        var said = 0;
        for (var taken = 0; taken < Turns; taken++)
        {
            var leftmost = -1;
            for (var kind = 0; kind < Turns; kind++)
            {
                if (!offered[kind]) continue;
                if (leftmost >= 0 && turnRad[kind] >= turnRad[leftmost]) continue;

                leftmost = kind;
            }

            if (leftmost < 0) break;

            offered[leftmost] = false;
            into[said++] = (LaneTurn)leftmost;
        }

        return said;
    }

    /// <summary>How many turns there are to say: the whole of <see cref="LaneTurn"/>.</summary>
    const int Turns = 3;
}
