using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// Which arm of a junction something comes from or leaves by, on a map whose every road runs one of the
/// four ways. <b>The lattice's bearings and not the compass's</b>: <see cref="ExamArm.North"/> is the
/// way the rows count up, which is up the screen.
/// </summary>
internal enum ExamArm : byte
{
    North,
    East,
    South,
    West,
}

/// <summary>
/// Which side of an arm something stands on, read off the bearing that runs out of the junction along it.
/// <b>The lattice's own handedness and not the road's</b> — which side a car drives on is
/// <see cref="SimConfig.RoadSideSign"/>'s and moves with it, and a place on the pavement does not.
/// </summary>
internal enum ExamSide : byte
{
    Left,
    Right,
}

/// <summary>
/// Where a cell's card is staged relative to the cell that holds it. A cell's own node is the ordinary
/// answer; the other two are for the shapes a lattice of crossings cannot itself be.
/// </summary>
internal enum ExamStage : byte
{
    /// <summary>The cell's own node — a crossroads or a T, depending on how many arms it has.</summary>
    Cell,

    /// <summary>The head of the cell's spur, which is a dead end (TER-5a).</summary>
    Head,

    /// <summary>
    /// The cell's own node again, with a crossing <b>struck in the middle of its south arm</b> rather than
    /// on an arm of the junction — a crossing belonging to no junction (TER-6).
    /// </summary>
    MidBlock,
}

/// <summary>
/// One cell of a lattice map, as the shape its card asked for. <b>What is staged on it is the card's and
/// never this</b>: a lattice lays ground, and who stands on that ground is the map's own business.
/// </summary>
/// <param name="Spur">Whether the cell is given a short road out of the lattice, which is what makes an edge cell a crossroads.</param>
/// <param name="Lit">Whether that junction carries lights (TLT-3).</param>
/// <param name="Stage">Which junction of the cell the card is staged at.</param>
internal readonly record struct ExamCell(bool Spur, bool Lit, ExamStage Stage);

/// <summary>One road of the lattice: the two nodes it runs between, and the straight it runs along.</summary>
internal readonly record struct ExamRoad(int FromJunction, int ToJunction, Vector2 FromM, Vector2 ToM);

/// <summary>
/// One crossing: where the paint is, the way the road under it runs, the road it is painted across —
/// whose width it spans — and the junction it approaches, <see cref="CityPlan.NoRecord"/> where it was
/// struck in the middle of a block.
/// </summary>
internal readonly record struct ExamCrossing(Vector2 CentreM, Vector2 Axis, int Road, int Junction);

/// <summary>
/// <b>The ground a lattice map stands on</b>: a grid of cells, the spurs and mid-block crossings their
/// cards asked for, the roads between all of them, and every place on the result a card can name. It is
/// arithmetic over the cells and holds no bodies — <see cref="ExamMap"/> paints the ground from it and
/// a harness reads the same answers, so what a test stages is what the map was laid to.
/// </summary>
/// <remarks>
/// <para>
/// <b>The shape of a cell's junction is the lattice's answer and not the card's.</b> A cell in the middle
/// has four neighbours and is a crossroads; one on an edge has three and is a T; a card that wants the
/// fourth arm asks for a <em>spur</em> — a short road out of the lattice ending in a dead end — and gets
/// its crossroads that way. <b>A corner is given one whether it asked or not</b>: two arms meeting at a
/// right angle is a road that turns and not a junction (TER-5b), so the corners of the lattice would
/// otherwise be the commonest authoring mistake there is.
/// </para>
/// <para>
/// <b>Two maps are laid on it</b> — the driving exam (<see cref="ExamPlan"/>) and the walking one
/// (<see cref="FootwayPlan"/>) — and they differ in their cards and in who is stood up, never in the
/// ground. A junction that behaved differently under the two would be two engines being measured.
/// </para>
/// </remarks>
internal sealed class ExamGround
{
    /// <summary>A metre a cell, as the fixture map and the proving ground are, which is what the classifier's tolerances are written against.</summary>
    public const float CellSizeM = 1f;

    /// <summary>The spacing of the lattice: long enough that a car pulls away, reaches a road speed and stops again inside one block.</summary>
    public const float BlockM = 110f;

    /// <summary>A spur, which is a block's worth of road cut short — long enough to stage a card on and to turn round at the end of.</summary>
    public const float SpurM = 70f;

    /// <summary>The ground left round the lattice, which is a spur's length again plus room for the camera.</summary>
    public const float MarginM = 30f;

    public const int NoRoad = -1;

    readonly SimConfig _config;
    readonly ExamCell[] _cells;

    /// <summary>Which way each cell's spur points, or none.</summary>
    readonly ExamArm?[] _spur;

    /// <summary>The road on each arm of each cell, indexed <c>cell * 4 + arm</c>.</summary>
    readonly int[] _armRoad;

    readonly int[] _spurHead;
    readonly int[] _stage;
    readonly List<bool> _isHead = [];
    readonly List<Vector2> _junctionM = [];
    readonly List<float> _radiusM = [];
    readonly List<bool> _lit = [];
    readonly List<float> _phaseOffsetS = [];
    readonly List<ExamRoad> _roads = [];

    public ExamGround(SimConfig config, int rows, int columns, ReadOnlySpan<ExamCell> cells)
    {
        if (cells.Length != rows * columns)
        {
            throw new ArgumentException(
                $"A lattice of {rows} by {columns} is {rows * columns} cells and was given {cells.Length}.",
                nameof(cells));
        }

        _config = config;
        Rows = rows;
        Columns = columns;
        _cells = cells.ToArray();
        _spur = new ExamArm?[cells.Length];
        _armRoad = new int[cells.Length * 4];
        _spurHead = new int[cells.Length];
        _stage = new int[cells.Length];
        Array.Fill(_spurHead, -1);
        Array.Fill(_armRoad, NoRoad);

        LayTheCells();
        LayTheSpurs();
        LayTheRoads();
    }

    public int Rows { get; }

    public int Columns { get; }

    public int Cells => _cells.Length;

    public int JunctionCount => _junctionM.Count;

    public Vector2 JunctionM(int junction) => _junctionM[junction];

    public float RadiusM(int junction) => _radiusM[junction];

    public bool Lit(int junction) => _lit[junction];

    public float PhaseOffsetS(int junction) => _phaseOffsetS[junction];

    /// <summary>Whether a junction is the head of a spur, which is the one shape whose ground is a disc rather than a mouth (TER-5a).</summary>
    public bool IsHead(int junction) => _isHead[junction];

    public IReadOnlyList<ExamRoad> Roads => _roads;

    /// <summary>How big the ground has to be to hold the lattice with its margin and its spurs.</summary>
    public Vector2 WorldSizeM =>
        new(
            (2f * (MarginM + SpurM)) + ((Columns - 1) * BlockM),
            (2f * (MarginM + SpurM)) + ((Rows - 1) * BlockM));

    /// <summary>The junction a card is staged at — its cell's own node, or its spur's head.</summary>
    public int Stage(int cell) => _stage[cell];

    public Vector2 StageM(int cell) => _junctionM[_stage[cell]];

    /// <summary>Which way a cell's spur runs, or none. It is the first bearing with no neighbour on it, so it always points out of the lattice.</summary>
    public ExamArm? Spur(int cell) => _spur[cell];

    /// <summary>The road on one arm of a cell, or <see cref="NoRoad"/> where that cell has no arm there.</summary>
    public int ArmRoad(int cell, ExamArm arm) => _armRoad[(cell * 4) + (int)arm];

    /// <summary>The arm of a spur's head that runs back to the cell it belongs to, which is the only arm a head has.</summary>
    public static ExamArm BackFromTheHead(ExamArm spur) => Opposite(spur);

    /// <summary>How far back from a junction its stop bar is painted, which is where a driver is told what the box is showing.</summary>
    public float BarM =>
        ReachM + _config.Road.CrossingSetbackM + _config.Road.CrossingDepthM + _config.Road.StopBarSetbackM;

    /// <summary>
    /// How far out from a junction's own middle the paint on one of its arms is struck, which is the
    /// distance every place at that crossing is measured from.
    /// </summary>
    public float CrossingOutM => ReachM + _config.Road.CrossingSetbackM + (_config.Road.CrossingDepthM * 0.5f);

    /// <summary>
    /// <b>Every crossing the map paints</b>: one on every arm of every junction that has arms to conflict
    /// at, at a fixed setback from the ground the junction reaches (TER-6), and the ones a cell asks for in
    /// the middle of a block. A dead end's head carries none (TER-5a).
    /// </summary>
    public List<ExamCrossing> Crossings()
    {
        var painted = new List<ExamCrossing>();
        for (var cell = 0; cell < Cells; cell++)
        {
            for (var arm = 0; arm < 4; arm++)
            {
                if (ArmRoad(cell, (ExamArm)arm) == NoRoad) continue;

                painted.Add(OnTheArm(cell, (ExamArm)arm));
            }
        }

        for (var cell = 0; cell < Cells; cell++)
        {
            if (_cells[cell].Stage == ExamStage.MidBlock) painted.Add(MidBlock(cell));
        }

        return painted;
    }

    /// <summary>The paint on one arm of one junction, a setback out from the ground that junction reaches.</summary>
    public ExamCrossing OnTheArm(int junction, ExamArm arm)
    {
        var axis = Bearing(arm);
        return new ExamCrossing(
            JunctionM(junction) + (axis * CrossingOutM), axis, ArmRoad(junction, arm), junction);
    }

    /// <summary>
    /// And the paint struck in the middle of a cell's south arm, which <b>belongs to no junction</b>
    /// (TER-6): a crossing adds no node, and a place on a road where two arms would meet is a node this
    /// build can carry but cannot light (TER-5b, TLT-3).
    /// </summary>
    public ExamCrossing MidBlock(int cell) =>
        new(
            StageM(cell) + (Bearing(ExamArm.South) * (BlockM * 0.5f)),
            Bearing(ExamArm.North),
            ArmRoad(cell, ExamArm.South),
            CityPlan.NoRecord);

    /// <summary>
    /// <b>A place on the pavement beside one arm of a junction</b>: a distance out from that junction's own
    /// middle, on one side of the road running down it. Every place either map stages a body at is one of
    /// these, so the kerb a walker waits at and the kerb it is sent to are the same arithmetic read twice.
    /// </summary>
    public Vector2 BesideTheArmM(int junction, ExamArm arm, ExamSide side, float outM) =>
        JunctionM(junction) + (Bearing(arm) * outM) + (Across(arm, side) * KerbOffsetM);

    /// <summary>Which way is across an arm, on one side of it — the way a body standing there would step into the road.</summary>
    public static Vector2 Across(ExamArm arm, ExamSide side) =>
        Heading.RightOf(Bearing(arm)) * (side == ExamSide.Right ? 1f : -1f);

    /// <summary>How far off the middle of a road the pavement's own line runs: half the carriageway and half a pavement.</summary>
    public float KerbOffsetM => (_config.RoadWidthM * 0.5f) + (_config.PavementWidthM * 0.5f);

    /// <summary>And how far off it one lane's own line runs, which is where a body standing in a lane stands.</summary>
    public float LaneOffsetM => _config.LaneOffsetM;

    /// <summary>Half a carriageway's width to the driver's own side of a line driven along a bearing (TER-4a).</summary>
    public Vector2 Beside(Vector2 travel) =>
        Heading.RightOf(travel) * _config.LaneOffsetM * _config.RoadSideSign;

    /// <summary>The angle a direction is, which is the one thing the spawn arrays carry a pose as.</summary>
    public static float Facing(Vector2 direction) => MathF.Atan2(direction.Y, direction.X);

    /// <summary>
    /// How far a junction's ground reaches: where the fillet an arm is flared back on lets go of the kerb
    /// (TER-5). <b>The lattice is square</b>, so every corner it turns is a right angle and every arm of it
    /// reaches the same distance — which is the shortest reach any junction in any town has.
    /// </summary>
    public float ReachM => _config.JunctionArmReachM(MathF.PI * 0.5f);

    /// <summary>
    /// The head of a dead end holds a car working itself round on the spot, its own width clear of the kerb
    /// (TER-5a). <b>It holds the car's body and not the path of its middle</b>: the turning circle is what
    /// the middle sweeps, and the corner furthest from that middle stands half a length and half a width off
    /// it — so a head sized on the circle alone is a head the nose leaves on every shunt round it, and the
    /// only reason that ever looked like it worked is that a grid of metre squares handed it back the
    /// difference.
    /// </summary>
    public float HeadRadiusM =>
        _config.CarTurningRadiusM + _config.Car.WidthM
        + (new Vector2(_config.Car.LengthM, _config.Car.WidthM).Length() * 0.5f);

    /// <summary>The bearing an arm runs on, out of the junction it is an arm of.</summary>
    public static Vector2 Bearing(ExamArm arm) => arm switch
    {
        ExamArm.North => new Vector2(0f, -1f),
        ExamArm.East => new Vector2(1f, 0f),
        ExamArm.South => new Vector2(0f, 1f),
        _ => new Vector2(-1f, 0f),
    };

    public static ExamArm Opposite(ExamArm arm) => (ExamArm)(((int)arm + 2) % 4);

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

    /// <summary>
    /// Where a cell's node stands. <b>On the middle of a cell and never on the corner of one</b>: a
    /// carriageway is laid either side of its own centreline, so a lattice standing on whole metres puts
    /// every kerb exactly on a cell boundary — and a sample a hair short of one, which is all a straight
    /// laid at an angle read back through a sine is, then lands in the cell beyond it. Half a cell over,
    /// nothing the map is measured against sits on a boundary at all.
    /// </summary>
    Vector2 CellM(int cell) =>
        new(
            MarginM + SpurM + (CellSizeM * 0.5f) + (Column(cell) * BlockM),
            MarginM + SpurM + (CellSizeM * 0.5f) + ((Rows - 1 - Row(cell)) * BlockM));

    void LayTheCells()
    {
        // The lit junctions are staggered across the one cycle so that the cards about lights are as many
        // different moments of it rather than the same one over and over (TLT-3).
        var litCells = 0;
        foreach (var cell in _cells)
        {
            if (cell.Lit) litCells++;
        }

        var lit = 0;
        for (var cell = 0; cell < Cells; cell++)
        {
            var phaseS = _cells[cell].Lit ? lit++ * _config.Signals.CycleS / litCells : 0f;
            Add(CellM(cell), _config.LaneOffsetM, _cells[cell].Lit, phaseS, head: false);
            _stage[cell] = cell;
        }
    }

    void LayTheSpurs()
    {
        for (var cell = 0; cell < Cells; cell++)
        {
            var free = FreeBearing(cell);

            // A card asks for a spur to make its cell a crossroads; a corner gets one whether it asked or
            // not, because two arms at a right angle is a road that turns (TER-5b).
            if ((!_cells[cell].Spur && Arms(cell) != 2) || free is not { } arm) continue;

            _spur[cell] = arm;
            _spurHead[cell] = Add(
                CellM(cell) + (Bearing(arm) * SpurM), HeadRadiusM, lit: false, phaseS: 0f, head: true);
            if (_cells[cell].Stage == ExamStage.Head) _stage[cell] = _spurHead[cell];
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

                _roads.Add(new ExamRoad(cell, beyond, _junctionM[cell], _junctionM[beyond]));
                _armRoad[(cell * 4) + (int)arm] = _roads.Count - 1;
                _armRoad[(beyond * 4) + (int)Opposite(arm)] = _roads.Count - 1;
            }
        }

        for (var cell = 0; cell < Cells; cell++)
        {
            if (_spur[cell] is not { } arm) continue;

            _roads.Add(new ExamRoad(cell, _spurHead[cell], _junctionM[cell], _junctionM[_spurHead[cell]]));
            _armRoad[(cell * 4) + (int)arm] = _roads.Count - 1;
        }
    }

    /// <summary>How many of the lattice's own arms a cell has, before any spur is laid.</summary>
    int Arms(int cell)
    {
        var arms = 0;
        for (var arm = 0; arm < 4; arm++)
        {
            if (Neighbour(cell, (ExamArm)arm) >= 0) arms++;
        }

        return arms;
    }

    /// <summary>The first bearing a cell has no neighbour on, which is where its spur goes if it has one.</summary>
    ExamArm? FreeBearing(int cell)
    {
        for (var arm = 0; arm < 4; arm++)
        {
            if (Neighbour(cell, (ExamArm)arm) < 0) return (ExamArm)arm;
        }

        return null;
    }

    int Add(Vector2 atM, float radiusM, bool lit, float phaseS, bool head)
    {
        _junctionM.Add(atM);
        _radiusM.Add(radiusM);
        _lit.Add(lit);
        _phaseOffsetS.Add(phaseS);
        _isHead.Add(head);
        return _junctionM.Count - 1;
    }
}
