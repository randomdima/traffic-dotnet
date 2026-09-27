using System.Numerics;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Exam;

/// <summary>
/// One cell of the lattice, as the shape the card placed on it asked for — in the lattice's own bearings.
/// <b>What is staged on it is the card's and never this</b>: a lattice lays ground, and who stands on that
/// ground is the map's own business.
/// </summary>
/// <param name="Spur">
/// The bearing of a short road out of the lattice from this cell, or none. It is what turns an edge cell into
/// a crossroads, a corner into a T, and what a dead end is the head of.
/// </param>
/// <param name="AtTheHead">Whether the card is staged at the head of the spur rather than at the cell's own node.</param>
/// <param name="Lit">Whether the cell's junction carries lights (TLT-3).</param>
/// <param name="Roundabout">Whether the cell's node is opened out into a ring (GEN-19).</param>
/// <param name="In">The bearings, as a mask, whose road runs one way into the cell's junction.</param>
/// <param name="Out">And the ones whose road runs one way out of it.</param>
internal readonly record struct ExamCell(
    ExamArm? Spur, bool AtTheHead, bool Lit, bool Roundabout, byte In, byte Out)
{
    public static byte Mask(ExamArm arm) => (byte)(1 << (int)arm);

    public bool RunsIn(ExamArm arm) => (In & Mask(arm)) != 0;

    public bool RunsOut(ExamArm arm) => (Out & Mask(arm)) != 0;
}

/// <summary>
/// One road of the lattice: the two nodes it runs between, the line it is laid as, and which way it is
/// driven.
/// </summary>
/// <param name="Line">The road's own line, as the plan carries it — moved onto its driven half where it runs one way.</param>
/// <param name="Corridor">
/// And the line before that move, down the middle of the corridor: a lane either way of it is where a two-way
/// road's lanes run, and where a one-way road's one lane runs on the side its traffic keeps.
/// </param>
internal readonly record struct ExamRoad(
    int FromJunction, int ToJunction, ArcSeg[] Line, ArcSeg[] Corridor, RoadFlow Flow, bool Ring);

/// <summary>
/// <b>The ground the exam stands on</b>: a grid of cells, the spurs and rings their cards asked for, the roads
/// between all of them, and every place on the result a card can name. It is arithmetic over the cells and
/// holds no bodies — <see cref="ExamPlan"/> writes the map from it and the harness reads the same answers, so
/// what a test stages is what the map was laid to.
/// </summary>
/// <remarks>
/// <para>
/// <b>The shape of a cell's junction is the lattice's answer and not the card's.</b> A cell in the middle
/// has four neighbours and is a crossroads; one on an edge has three and is a T; a corner has two, which is a
/// road that turns and not a junction (TER-5b), so a corner stands a card only once a spur has made it a T.
/// </para>
/// <para>
/// <b>A roundabout is a ring of ordinary junctions</b>, laid the way the generator lays one (GEN-19,
/// <c>TownLayout.RingOut</c>): each arm cut back to its own point on the circle, square to it, and the points
/// joined by one-way arcs driven with the island on the side the traffic does not keep. Nothing about it is
/// a rule of its own — the circulating traffic keeps its right of way because going round is straight on
/// where coming in off an arm is a turn (TER-5e).
/// </para>
/// <para>
/// <b>Every road is laid the way the generator lays one</b> (TER-5d, <c>RoadLines</c>): each end is given the
/// arm the town's own draw gives it (<see cref="ConnectionPoints.ArmOf(ulong, SimConfig, int, Vector2, Vector2, bool, float)"/>),
/// and the road runs from the stand point of one to the stand point of the other, arriving on both bearings.
/// The lanes are read off the plan by that same draw, so a road laid straight from centre to centre would be
/// a road whose lanes end somewhere its line does not go — and a junction whose movements are never laid.
/// So the lattice is square in its nodes and not in its streets: each arm leaves a little off the chord.
/// </para>
/// <para>
/// <b>A one-way arm is laid as the generator lays one</b> (TER-4d, <see cref="RoadStage"/>): one lane's width,
/// moved onto the half of the carriageway its traffic drives, so its lane is where a two-way road's lane in
/// that direction would be and nothing staged beside it moves.
/// </para>
/// </remarks>
internal sealed class ExamGround
{
    /// <summary>
    /// The spacing of the lattice: room on each road for one card's approach at one end, the other card's
    /// run-on at the other, and a gap between them so no two cards share a car's stopping distance.
    /// </summary>
    public const float BlockM = 130f;

    /// <summary>A spur, which is half a block — long enough to stage a dead end on and to turn round at the end of.</summary>
    public const float SpurM = 70f;

    /// <summary>The ground left round the lattice past the spurs, which is room for the pavement and the camera.</summary>
    public const float MarginM = 30f;

    public const int NoRoad = -1;

    readonly SimConfig _config;
    readonly ulong _seed;
    readonly ExamCell[] _cells;

    /// <summary>The road on each arm of each cell, indexed <c>cell * 4 + arm</c>.</summary>
    readonly int[] _armRoad;

    /// <summary>And the junction that road meets the cell at: the cell's own node, or its ring's node on that bearing.</summary>
    readonly int[] _armJunction;

    readonly int[] _node;
    readonly int[] _head;
    readonly List<Vector2> _junctionM = [];
    readonly List<bool> _lit = [];
    readonly List<float> _phaseOffsetS = [];
    readonly List<ExamRoad> _roads = [];
    readonly List<int> _ringRoads = [];
    readonly List<int> _ringOffsets = [0];

    /// <param name="seed">The plan's own seed, which is what every arm's bearing is drawn from.</param>
    public ExamGround(SimConfig config, ulong seed, int rows, int columns, ReadOnlySpan<ExamCell> cells)
    {
        if (cells.Length != rows * columns)
        {
            throw new ArgumentException(
                $"A lattice of {rows} by {columns} is {rows * columns} cells and was given {cells.Length}.",
                nameof(cells));
        }

        _config = config;
        _seed = seed;
        Rows = rows;
        Columns = columns;
        _cells = cells.ToArray();
        _armRoad = new int[cells.Length * 4];
        _armJunction = new int[cells.Length * 4];
        _node = new int[cells.Length];
        _head = new int[cells.Length];
        Array.Fill(_armRoad, NoRoad);
        Array.Fill(_armJunction, NoRoad);
        Array.Fill(_head, NoRoad);

        LayTheNodes();
        LayTheRoads();
    }

    public int Rows { get; }

    public int Columns { get; }

    public int Cells => _cells.Length;

    public ExamCell Cell(int cell) => _cells[cell];

    public int JunctionCount => _junctionM.Count;

    public Vector2 JunctionM(int junction) => _junctionM[junction];

    public bool Lit(int junction) => _lit[junction];

    public float PhaseOffsetS(int junction) => _phaseOffsetS[junction];

    public IReadOnlyList<ExamRoad> Roads => _roads;

    /// <summary>Which roads each ring is made of, as the plan carries them (GEN-19).</summary>
    public ReadOnlySpan<int> RingRoads => System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_ringRoads);

    /// <inheritdoc cref="RingRoads"/>
    public ReadOnlySpan<int> RingOffsets => System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_ringOffsets);

    /// <summary>How big the ground has to be to hold the lattice with its spurs and its margin.</summary>
    public Vector2 WorldSizeM =>
        new(
            (2f * (MarginM + SpurM)) + ((Columns - 1) * BlockM),
            (2f * (MarginM + SpurM)) + ((Rows - 1) * BlockM));

    /// <summary>The middle of a cell, which is its junction or the middle of its ring.</summary>
    /// <remarks>
    /// <b>On the middle of a metre and never on the corner of one</b>: a carriageway is laid either side of
    /// its own line, so a lattice standing on whole metres puts every kerb exactly on a whole metre, and a
    /// sample a hair short of one lands on the other side of it. Half a metre over, nothing the map is
    /// measured against sits on one.
    /// </remarks>
    public Vector2 CentreM(int cell) =>
        new(
            MarginM + SpurM + 0.5f + (Column(cell) * BlockM),
            MarginM + SpurM + 0.5f + ((Rows - 1 - Row(cell)) * BlockM));

    /// <summary>The junction a card is staged at — its cell's own node, the node of its ring, or its spur's head.</summary>
    public Vector2 StageM(int cell) => _cells[cell].AtTheHead ? _junctionM[_head[cell]] : CentreM(cell);

    /// <summary>The spur's head, where the card is staged at one; otherwise <see cref="NoRoad"/>.</summary>
    public int Head(int cell) => _head[cell];

    /// <summary>The cell's own node, or <see cref="NoRoad"/> where it was opened out into a ring.</summary>
    public int Node(int cell) => _node[cell];

    /// <summary>The road on one arm of a cell, or <see cref="NoRoad"/> where that cell has no arm there.</summary>
    public int ArmRoad(int cell, ExamArm arm) => _armRoad[(cell * 4) + (int)arm];

    /// <summary>The junction the road on that arm meets the cell at, or <see cref="NoRoad"/>.</summary>
    public int ArmJunction(int cell, ExamArm arm) => _armJunction[(cell * 4) + (int)arm];

    /// <summary>
    /// How far out of the middle of a cell the arm on a bearing begins: nothing at a node, and the ring's own
    /// radius where the node was opened out, which is where every place along that arm is measured from.
    /// </summary>
    public float ArmStartsM(int cell) => _cells[cell].Roundabout ? RingRadiusM : 0f;

    /// <summary>
    /// <b>The circle a ring is laid on</b>, as the generator sizes one at four arms square to each other
    /// (GEN-19): two arms a quarter turn apart owe each other a road's ground and a pavement's width, or the
    /// floor the roundabout's own design speed affords, whichever is wider.
    /// </summary>
    public float RingRadiusM =>
        MathF.Max(
            _config.RoundaboutRadiusFloorM,
            (_config.RoadFootprintM + _config.PavementWidthM) * 0.5f / MathF.Sin(MathF.PI * 0.25f));

    /// <summary>
    /// <b>How far round a cell's box reaches</b>: the standoff its lanes end at (TER-5), and the whole ring
    /// inside that where the cell is a roundabout. A body with its nose inside this is on the box.
    /// </summary>
    public float BoxRadiusM(int cell) => ArmStartsM(cell) + _config.JunctionRadiusM;

    /// <summary>The bearing an arm runs on, out of the junction it is an arm of.</summary>
    public static Vector2 Bearing(ExamArm arm) => arm switch
    {
        ExamArm.North => new Vector2(0f, -1f),
        ExamArm.East => new Vector2(1f, 0f),
        ExamArm.South => new Vector2(0f, 1f),
        _ => new Vector2(-1f, 0f),
    };

    public static ExamArm Opposite(ExamArm arm) => (ExamArm)(((int)arm + 2) % 4);

    /// <summary>An arm turned clockwise by some quarter turns, which is how a card's frame is laid onto a cell.</summary>
    public static ExamArm Turned(ExamArm arm, int quarters) => (ExamArm)((((int)arm + quarters) % 4 + 4) % 4);

    /// <summary>The angle a direction is, which is the one thing the spawn arrays carry a pose as.</summary>
    public static float Facing(Vector2 direction) => MathF.Atan2(direction.Y, direction.X);

    /// <summary>Half a carriageway's width to the driver's own side of a line driven along a bearing (TER-4a).</summary>
    public Vector2 Beside(Vector2 travel) =>
        Heading.RightOf(travel) * _config.LaneOffsetM * _config.RoadSideSign;

    public int Row(int cell) => cell / Columns;

    public int Column(int cell) => cell % Columns;

    /// <summary>The cell one step along a bearing, or −1 where the lattice ends there.</summary>
    public int Neighbour(int cell, ExamArm arm)
    {
        var row = Row(cell) + (arm == ExamArm.North ? 1 : arm == ExamArm.South ? -1 : 0);
        var column = Column(cell) + (arm == ExamArm.East ? 1 : arm == ExamArm.West ? -1 : 0);
        if (row < 0 || column < 0 || row >= Rows || column >= Columns) return -1;

        return (row * Columns) + column;
    }

    /// <summary>How many of the lattice's own arms a cell has, before any spur is laid.</summary>
    public int LatticeArms(int cell)
    {
        var arms = 0;
        for (var arm = 0; arm < 4; arm++)
        {
            if (Neighbour(cell, (ExamArm)arm) >= 0) arms++;
        }

        return arms;
    }

    /// <summary>Whether a cell has an arm on a bearing, the spur included.</summary>
    public bool HasArm(int cell, ExamArm arm) => Neighbour(cell, arm) >= 0 || _cells[cell].Spur == arm;

    void LayTheNodes()
    {
        // The lit junctions are staggered across the one cycle, so that no two of them are the same moment
        // of it (TLT-3). A card about a light does not lean on this: it sends its car on the colour it wants.
        var litCells = 0;
        foreach (var cell in _cells)
        {
            if (cell.Lit) litCells++;
        }

        var lit = 0;
        for (var cell = 0; cell < Cells; cell++)
        {
            var of = _cells[cell];
            if (of.Roundabout)
            {
                _node[cell] = NoRoad;
                for (var arm = 0; arm < 4; arm++)
                {
                    if (!HasArm(cell, (ExamArm)arm)) continue;

                    _armJunction[(cell * 4) + arm] =
                        Add(CentreM(cell) + (Bearing((ExamArm)arm) * RingRadiusM), lit: false, phaseS: 0f);
                }

                continue;
            }

            var phaseS = of.Lit ? lit++ * _config.Signals.CycleS / litCells : 0f;
            _node[cell] = Add(CentreM(cell), of.Lit, phaseS);
            for (var arm = 0; arm < 4; arm++)
            {
                if (HasArm(cell, (ExamArm)arm)) _armJunction[(cell * 4) + arm] = _node[cell];
            }
        }

        for (var cell = 0; cell < Cells; cell++)
        {
            if (_cells[cell].Spur is not { } spur) continue;

            _head[cell] = Add(CentreM(cell) + (Bearing(spur) * SpurM), lit: false, phaseS: 0f);
        }
    }

    void LayTheRoads()
    {
        for (var cell = 0; cell < Cells; cell++)
        {
            foreach (var arm in (ReadOnlySpan<ExamArm>)[ExamArm.North, ExamArm.East])
            {
                var beyond = Neighbour(cell, arm);
                if (beyond < 0) continue;

                var from = ArmJunction(cell, arm);
                var to = ArmJunction(beyond, Opposite(arm));
                var road = Lay(from, to, FlowBetween(cell, arm, beyond));
                _armRoad[(cell * 4) + (int)arm] = road;
                _armRoad[(beyond * 4) + (int)Opposite(arm)] = road;
            }
        }

        for (var cell = 0; cell < Cells; cell++)
        {
            if (_cells[cell].Spur is not { } spur) continue;

            _armRoad[(cell * 4) + (int)spur] = Lay(ArmJunction(cell, spur), _head[cell], RoadFlow.BothWays);
        }

        for (var cell = 0; cell < Cells; cell++)
        {
            if (_cells[cell].Roundabout) LayTheRing(cell);
        }
    }

    /// <summary>
    /// <b>Which way the road between two cells is driven</b>, off whichever of the two asked for it — and a
    /// road the two ask for opposite things of is a table that cannot be laid, which fails here.
    /// </summary>
    /// <remarks>The road is laid from <paramref name="cell"/> to <paramref name="beyond"/>, so "with the road" is into the cell beyond.</remarks>
    RoadFlow FlowBetween(int cell, ExamArm arm, int beyond)
    {
        var back = Opposite(arm);
        var intoBeyond = _cells[cell].RunsOut(arm) || _cells[beyond].RunsIn(back);
        var intoCell = _cells[cell].RunsIn(arm) || _cells[beyond].RunsOut(back);
        if (intoBeyond && intoCell)
        {
            throw new InvalidOperationException(
                $"Cells {cell} and {beyond} ask for the road between them to run both one way and the other.");
        }

        return intoBeyond ? RoadFlow.WithTheRoad : intoCell ? RoadFlow.AgainstTheRoad : RoadFlow.BothWays;
    }

    /// <summary>
    /// <b>One road between two junctions, laid as the generator lays one</b> (TER-5d, <c>RoadLines</c>): from
    /// the stand point of the arm the town's draw gives it at one end to the stand point of the other, on the
    /// biarc that leaves and arrives on those two bearings — and, where it runs one way, one lane wide and
    /// moved onto the half its traffic drives (TER-4d).
    /// </summary>
    int Lay(int from, int to, RoadFlow flow)
    {
        var leaves = ConnectionPoints.ArmOf(
            _seed, _config, from, _junctionM[from], _junctionM[to], settled: false, curvature: 0f);
        var arrives = ConnectionPoints.ArmOf(
            _seed, _config, to, _junctionM[to], _junctionM[from], settled: false, curvature: 0f);

        Span<ArcSeg> drawn = stackalloc ArcSeg[2];
        var laid = Spline.BiarcInto(
            leaves.StandM, Facing(leaves.StandUnit), arrives.StandM, Facing(-arrives.StandUnit), drawn);
        if (laid == 0) throw new InvalidOperationException($"No road can be laid between junctions {from} and {to}.");

        // Two straights in a line are one straight, which is what a road between two arms that agree is.
        ArcSeg[] corridor = laid == 2 && drawn[0].Curvature == 0f && drawn[1].Curvature == 0f
            ? [new ArcSeg(drawn[0].StartM, drawn[0].HeadingRad, drawn[0].LengthM + drawn[1].LengthM, 0f)]
            : drawn[..laid].ToArray();

        var line = corridor;
        if (flow != RoadFlow.BothWays)
        {
            line = new ArcSeg[corridor.Length];
            Spline.OffsetInto(corridor, RoadStage.DrivenHalfM(_config, flow, RoadStage.WidthM(_config, flow)), line);
        }

        _roads.Add(new ExamRoad(from, to, line, corridor, flow, Ring: false));
        return _roads.Count - 1;
    }

    /// <summary>
    /// <b>The ring</b>: one piece of the circle between each pair of ring nodes that stand next to each other,
    /// driven the way the generator drives one (GEN-19) — the island on the side the traffic does not keep, so
    /// a car goes round it turning away from the kerb it drives against. <b>Each piece is laid between its two
    /// arms' stand points</b>, whose leads are themselves pieces of the circle, so the ring is one circle.
    /// </summary>
    void LayTheRing(int cell)
    {
        var curvature = -_config.RoadSideSign / RingRadiusM;

        // Round the circle the way the traffic goes: the next node is a quarter turn round on the side the
        // car is turning to.
        var step = _config.RoadSideSign > 0f ? 3 : 1;
        for (var arm = 0; arm < 4; arm++)
        {
            var from = ArmJunction(cell, (ExamArm)arm);
            var to = ArmJunction(cell, (ExamArm)((arm + step) % 4));
            var leaves = ConnectionPoints.ArmOf(_seed, _config, from, _junctionM[from], _junctionM[to], true, curvature);
            var arrives = ConnectionPoints.ArmOf(_seed, _config, to, _junctionM[to], _junctionM[from], true, -curvature);

            var chord = arrives.StandM - leaves.StandM;
            var chordM = chord.Length();
            var half = MathF.Asin(MathF.Min(1f, chordM * 0.5f * MathF.Abs(curvature)));
            var sweep = 2f * half * MathF.Sign(curvature);
            ArcSeg[] piece =
            [
                new ArcSeg(leaves.StandM, Facing(chord / chordM) - (sweep * 0.5f), MathF.Abs(sweep) / MathF.Abs(curvature), curvature),
            ];

            _roads.Add(new ExamRoad(from, to, piece, piece, RoadFlow.WithTheRoad, Ring: true));
            _ringRoads.Add(_roads.Count - 1);
        }

        _ringOffsets.Add(_ringRoads.Count);
    }

    /// <summary>
    /// <b>A place in a lane of a road, a distance out from one of its junctions</b>, and the way a car in that
    /// lane is travelling there: towards the junction, or away from it. The distance is measured from the
    /// node's own centre, which is where every card measures its stand backs and its run ons from.
    /// </summary>
    public (Vector2 AtM, Vector2 Travel) OnTheLane(int junction, int road, float outM, bool arriving)
    {
        var laid = _roads[road];
        var corridor = laid.Corridor;
        var lengthM = Spline.TotalLengthM(corridor);
        var fromThisEnd = laid.FromJunction == junction;
        var intoM = outM - _config.CityGen.ConnectionStandoffM;
        var at = Spline.SampleAt(corridor, Math.Clamp(fromThisEnd ? intoM : lengthM - intoM, 0f, lengthM));

        var outward = fromThisEnd ? at.Direction : -at.Direction;
        var travel = arriving ? -outward : outward;
        return (at.PositionM + Beside(travel), travel);
    }

    /// <summary>How far along a road's corridor a point stands, measured out from one of its junctions' centres.</summary>
    public float OutAlongM(int junction, int road, Vector2 pointM)
    {
        var laid = _roads[road];
        var lengthM = Spline.TotalLengthM(laid.Corridor);
        var alongM = Spline.ProjectM(laid.Corridor, pointM, lengthM * 0.5f, lengthM);
        var intoM = laid.FromJunction == junction ? alongM : lengthM - alongM;
        return intoM + _config.CityGen.ConnectionStandoffM;
    }

    /// <summary>How wide a road is laid (GEN-15, TER-4d): one lane's width for every way it is driven.</summary>
    public float WidthM(in ExamRoad road) => RoadStage.WidthM(_config, road.Flow);

    int Add(Vector2 atM, bool lit, float phaseS)
    {
        _junctionM.Add(atM);
        _lit.Add(lit);
        _phaseOffsetS.Add(phaseS);
        return _junctionM.Count - 1;
    }
}
