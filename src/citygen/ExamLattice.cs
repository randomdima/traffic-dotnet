using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>One road of the lattice: the two nodes it runs between, and the straight it runs along.</summary>
internal readonly record struct ExamRoad(int FromJunction, int ToJunction, Vector2 FromM, Vector2 ToM);

/// <summary>
/// One crossing: where the paint is, the way the road under it runs, the road it is painted across —
/// whose width it spans — and the junction it approaches, <see cref="CityPlan.NoRecord"/> where it was
/// struck in the middle of a block.
/// </summary>
internal readonly record struct ExamCrossing(Vector2 CentreM, Vector2 Axis, int Road, int Junction);

/// <summary>
/// <b>Where everything on the exam map stands</b>: the thirty-six cells, the spurs and mid-block nodes
/// their cards asked for, the roads between all of them, and the pose every staged car starts and finishes
/// in. It is arithmetic over <see cref="ExamCards"/> and holds no cells — <see cref="ExamPlan"/> paints
/// the ground from it and the harness reads the same answers, so what a test drives at is what the map was
/// laid to.
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
/// <b>A car starts and finishes on its own side of the centreline</b> (TER-4a), a stand back from the box
/// on the arm it comes from and a run on past it on the arm it leaves by — so the movement a card names is
/// the only route between the two, and a driver ordered to the far point has been asked for that crossing
/// and no other.
/// </para>
/// </remarks>
internal sealed class ExamLattice
{
    /// <summary>A metre a cell, as the fixture map and the proving ground are, which is what the classifier's tolerances are written against.</summary>
    public const float CellSizeM = 1f;

    /// <summary>The spacing of the lattice: long enough that a car pulls away, reaches a road speed and stops again inside one block.</summary>
    public const float BlockM = 110f;

    /// <summary>A spur, which is a block's worth of road cut short — long enough to stage a card on and to turn round at the end of.</summary>
    public const float SpurM = 70f;

    /// <summary>The ground left round the lattice, which is a spur's length again plus room for the camera.</summary>
    public const float MarginM = 30f;

    /// <summary>How far past the box a driver is sent: clear of the junction and well short of the next one.</summary>
    public const float RunOnM = 40f;

    public const int NoRoad = -1;

    public const int NoWalker = -1;

    readonly SimConfig _config;

    /// <summary>Which way each cell's spur points, or none.</summary>
    readonly ExamArm?[] _spur = new ExamArm?[ExamCards.Count];

    /// <summary>The road on each arm of each cell, indexed <c>cell * 4 + arm</c>.</summary>
    readonly int[] _armRoad = new int[ExamCards.Count * 4];

    readonly int[] _walker = new int[ExamCards.Count];
    readonly int[] _spurHead = new int[ExamCards.Count];
    readonly int[] _stage = new int[ExamCards.Count];
    readonly int[] _firstCar = new int[ExamCards.Count];
    readonly List<bool> _isHead = [];
    readonly List<Vector2> _junctionM = [];
    readonly List<float> _radiusM = [];
    readonly List<bool> _lit = [];
    readonly List<float> _phaseOffsetS = [];
    readonly List<ExamRoad> _roads = [];

    ExamLattice(SimConfig config)
    {
        _config = config;
        Array.Fill(_spurHead, -1);
        Array.Fill(_armRoad, NoRoad);
        Array.Fill(_walker, NoWalker);

        LayTheCells();
        LayTheSpursAndMidBlocks();
        LayTheRoads();
        LayTheCars();
        LayTheWalkers();
    }

    public static ExamLattice Of(SimConfig config) => new(config);

    public int JunctionCount => _junctionM.Count;

    public Vector2 JunctionM(int junction) => _junctionM[junction];

    public float RadiusM(int junction) => _radiusM[junction];

    public bool Lit(int junction) => _lit[junction];

    public float PhaseOffsetS(int junction) => _phaseOffsetS[junction];

    /// <summary>Whether a junction is the head of a spur, which is the one shape whose ground is a disc rather than a mouth (TER-5a).</summary>
    public bool IsHead(int junction) => _isHead[junction];

    public IReadOnlyList<ExamRoad> Roads => _roads;

    /// <summary>How many cars the map stands up, which is every driver of every card.</summary>
    public int Cars { get; private set; }

    /// <summary>How big the ground has to be to hold the lattice with its margin and its spurs.</summary>
    public Vector2 WorldSizeM
    {
        get
        {
            var sideM = (2f * (MarginM + SpurM)) + ((ExamCards.Columns - 1) * BlockM);
            return new Vector2(sideM, (2f * (MarginM + SpurM)) + ((ExamCards.Rows - 1) * BlockM));
        }
    }

    /// <summary>The junction a card is staged at — its cell's own node, its spur's head, or the node struck in its south arm.</summary>
    public int Stage(int card) => _stage[card];

    public Vector2 StageM(int card) => _junctionM[_stage[card]];

    /// <summary>Which way a cell's spur runs, or none. It is the first bearing with no neighbour on it, so it always points out of the lattice.</summary>
    public ExamArm? Spur(int card) => _spur[card];

    /// <summary>The road on one arm of a cell, or <see cref="NoRoad"/> where that cell has no arm there.</summary>
    public int ArmRoad(int cell, ExamArm arm) => _armRoad[(cell * 4) + (int)arm];

    /// <summary>The first of a card's cars in the fleet. A card's drivers are laid together and in the card's own order.</summary>
    public int CarOf(int card, int driver) => _firstCar[card] + driver;

    /// <summary>Where one of a card's cars starts: a stand back from the box, on its own side of the arm it comes from.</summary>
    public Vector2 StandM(int card, int driver)
    {
        var drives = ExamCards.All[card].Drivers[driver];
        var outward = Bearing(drives.From);
        return StageM(card) + (outward * drives.StandBackM) + Beside(-outward);
    }

    /// <summary>And which way it faces there, which is at the junction it is staged for.</summary>
    public float StandHeadingRad(int card, int driver) =>
        Facing(-Bearing(ExamCards.All[card].Drivers[driver].From));

    /// <summary>
    /// And where it is sent: a run on past the box on the arm it leaves by, on that arm's own side of the
    /// centreline. <b>A driver sent back down the arm it came from is a card about turning round</b>
    /// (`P-19`), and the point lands on the other lane of the same road because the bearing it is read
    /// against is reversed.
    /// </summary>
    public Vector2 AimM(int card, int driver)
    {
        var drives = ExamCards.All[card].Drivers[driver];
        var outward = Bearing(drives.To);
        return StageM(card) + (outward * drives.RunOnM) + Beside(outward);
    }

    /// <summary>How far back from a junction its stop bar is painted, which is where a driver is told what the box is showing.</summary>
    public float BarM =>
        ReachM + _config.Road.CrossingSetbackM + _config.Road.CrossingDepthM + _config.Road.StopBarSetbackM;

    /// <summary>
    /// <b>Every crossing the map paints</b>: one on every arm of every junction that has arms to conflict
    /// at, at a fixed setback from the ground the junction reaches (TER-6), and the ones a card asks for in
    /// the middle of a block. A dead end's head carries none (TER-5a).
    /// </summary>
    public List<ExamCrossing> Crossings()
    {
        var painted = new List<ExamCrossing>();
        for (var cell = 0; cell < ExamCards.Count; cell++)
        {
            for (var arm = 0; arm < 4; arm++)
            {
                if (ArmRoad(cell, (ExamArm)arm) == NoRoad) continue;

                painted.Add(OnTheArm(cell, (ExamArm)arm));
            }
        }

        for (var card = 0; card < ExamCards.Count; card++)
        {
            if (ExamCards.All[card].Stage == ExamStage.MidBlock) painted.Add(MidBlock(card));
        }

        return painted;
    }

    /// <summary>The paint on one arm of one junction, a setback out from the ground that junction reaches.</summary>
    public ExamCrossing OnTheArm(int junction, ExamArm arm)
    {
        var axis = Bearing(arm);
        return new ExamCrossing(
            JunctionM(junction)
            + (axis * (ReachM + _config.Road.CrossingSetbackM + (_config.Road.CrossingDepthM * 0.5f))),
            axis, ArmRoad(junction, arm), junction);
    }

    /// <summary>
    /// And the paint struck in the middle of a card's south arm, which <b>belongs to no junction</b>
    /// (TER-6): a crossing adds no node, and a place on a road where two arms would meet is a node this
    /// build can carry but cannot light (TER-5b, TLT-3).
    /// </summary>
    public ExamCrossing MidBlock(int card) =>
        new(
            StageM(card) + (Bearing(ExamArm.South) * (BlockM * 0.5f)),
            Bearing(ExamArm.North),
            ArmRoad(card, ExamArm.South),
            CityPlan.NoRecord);

    /// <summary>Which of the map's crossings a card is about — the one somebody is standing at.</summary>
    public bool Watched(int card, out ExamCrossing crossing)
    {
        var of = ExamCards.All[card];
        if (of.Stage == ExamStage.MidBlock)
        {
            crossing = MidBlock(card);
            return true;
        }

        if (of.Watched is not { } arm)
        {
            crossing = default;
            return false;
        }

        crossing = OnTheArm(Stage(card), arm);
        return true;
    }

    /// <summary>Where the walker beside that paint stands, and which way it faces — across the road it is about to step into (PER-15).</summary>
    public bool Waiting(int card, out Vector2 standM, out float facingRad)
    {
        if (!Watched(card, out var crossing))
        {
            (standM, facingRad) = (Vector2.Zero, 0f);
            return false;
        }

        var across = Heading.RightOf(crossing.Axis);
        standM = crossing.CentreM + (across * KerbOffsetM);
        facingRad = Facing(-across);
        return true;
    }

    /// <summary>
    /// And the kerb on the far side of that paint, which is where a body sent over it is sent. The two
    /// kerbs stand off the crossing's own centre by the same amount and on opposite sides, so a walker
    /// ordered from one to the other has been asked for that crossing and no other.
    /// </summary>
    public bool Across(int card, out Vector2 toM)
    {
        if (!Watched(card, out var crossing))
        {
            toM = Vector2.Zero;
            return false;
        }

        toM = crossing.CentreM - (Heading.RightOf(crossing.Axis) * KerbOffsetM);
        return true;
    }

    /// <summary>How far off the middle of a crossing its own kerbs stand: half the carriageway and half a pavement.</summary>
    public float KerbOffsetM => (_config.RoadWidthM * 0.5f) + (_config.PavementWidthM * 0.5f);

    /// <summary>How many bodies the map stands on foot, which is one at the kerb of every crossing a card is about.</summary>
    public int Walkers { get; private set; }

    /// <summary>
    /// The body standing at the kerb of a card's own paint, or <see cref="NoWalker"/> where that card is
    /// about no paint. <b>The people are numbered after the cars and in card order</b>, which is the order
    /// <see cref="ExamPlan"/> puts them down in, so which body belongs to a card is read here rather than
    /// counted a second time at the call site.
    /// </summary>
    public int WalkerOf(int card) => _walker[card];

    /// <summary>The angle a direction is, which is the one thing the spawn arrays carry a pose as.</summary>
    public static float Facing(Vector2 direction) => MathF.Atan2(direction.Y, direction.X);

    /// <summary>
    /// How far a junction's ground reaches: where the fillet an arm is flared back on lets go of the kerb
    /// (TER-5). <b>The lattice is square</b>, so every corner it turns is a right angle and every arm of it
    /// reaches the same distance — which is the shortest reach any junction in any town has.
    /// </summary>
    public float ReachM => _config.JunctionArmReachM(MathF.PI * 0.5f);

    /// <summary>The head of a dead end holds a car working itself round on the spot, its own width clear of the kerb (TER-5a).</summary>
    public float HeadRadiusM => _config.CarTurningRadiusM + _config.Car.WidthM;

    /// <summary>The bearing an arm runs on, out of the junction it is an arm of.</summary>
    public static Vector2 Bearing(ExamArm arm) => arm switch
    {
        ExamArm.North => new Vector2(0f, -1f),
        ExamArm.East => new Vector2(1f, 0f),
        ExamArm.South => new Vector2(0f, 1f),
        _ => new Vector2(-1f, 0f),
    };

    public static ExamArm Opposite(ExamArm arm) => (ExamArm)(((int)arm + 2) % 4);

    public static int Row(int cell) => cell / ExamCards.Columns;

    public static int Column(int cell) => cell % ExamCards.Columns;

    /// <summary>The cell one step along a bearing, or −1 where the lattice ends there.</summary>
    public static int Neighbour(int cell, ExamArm arm)
    {
        var row = Row(cell) + (arm == ExamArm.North ? 1 : arm == ExamArm.South ? -1 : 0);
        var column = Column(cell) + (arm == ExamArm.East ? 1 : arm == ExamArm.West ? -1 : 0);
        if (row < 0 || column < 0 || row >= ExamCards.Rows || column >= ExamCards.Columns) return -1;

        return (row * ExamCards.Columns) + column;
    }

    /// <summary>Half a carriageway's width to the driver's own side of a line driven along a bearing (TER-4a).</summary>
    Vector2 Beside(Vector2 travel) =>
        Heading.RightOf(travel) * _config.LaneOffsetM * _config.RoadSideSign;

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
            MarginM + SpurM + (CellSizeM * 0.5f) + ((ExamCards.Rows - 1 - Row(cell)) * BlockM));

    void LayTheCells()
    {
        var lit = 0;
        for (var cell = 0; cell < ExamCards.Count; cell++)
        {
            var card = ExamCards.All[cell];

            // The lit junctions are staggered across the one cycle so that four cards about lights are four
            // different moments of it rather than the same one four times over (TLT-3).
            var phaseS = card.Lit ? lit++ * _config.Signals.CycleS / 4f : 0f;
            Add(CellM(cell), _config.LaneOffsetM, card.Lit, phaseS, head: false);
            _stage[cell] = cell;
        }
    }

    void LayTheSpursAndMidBlocks()
    {
        for (var cell = 0; cell < ExamCards.Count; cell++)
        {
            var card = ExamCards.All[cell];
            var free = FreeBearing(cell);

            // A card asks for a spur to make its cell a crossroads; a corner gets one whether it asked or
            // not, because two arms at a right angle is a road that turns (TER-5b).
            if ((card.Spur || Arms(cell) == 2) && free is { } arm)
            {
                _spur[cell] = arm;
                _spurHead[cell] = Add(
                    CellM(cell) + (Bearing(arm) * SpurM), HeadRadiusM, lit: false, phaseS: 0f, head: true);
                if (card.Stage == ExamStage.Head) _stage[cell] = _spurHead[cell];
            }

        }
    }

    void LayTheRoads()
    {
        for (var cell = 0; cell < ExamCards.Count; cell++)
        {
            foreach (var arm in (ReadOnlySpan<ExamArm>)[ExamArm.North, ExamArm.East])
            {
                var beyond = Neighbour(cell, arm);
                if (beyond < 0) continue;

                Join(cell, beyond);
                _armRoad[(cell * 4) + (int)arm] = _roads.Count - 1;
                _armRoad[(beyond * 4) + (int)Opposite(arm)] = _roads.Count - 1;
            }
        }

        for (var cell = 0; cell < ExamCards.Count; cell++)
        {
            if (_spur[cell] is not { } arm) continue;

            _roads.Add(new ExamRoad(cell, _spurHead[cell], _junctionM[cell], _junctionM[_spurHead[cell]]));
            _armRoad[(cell * 4) + (int)arm] = _roads.Count - 1;
        }
    }

    void Join(int from, int to) => _roads.Add(new ExamRoad(from, to, _junctionM[from], _junctionM[to]));

    void LayTheCars()
    {
        for (var card = 0; card < ExamCards.Count; card++)
        {
            _firstCar[card] = Cars;
            Cars += ExamCards.All[card].Drivers.Length;
        }
    }

    void LayTheWalkers()
    {
        for (var card = 0; card < ExamCards.Count; card++)
        {
            if (Watched(card, out _)) _walker[card] = Walkers++;
        }
    }

    /// <summary>How many of the lattice's own arms a cell has, before any spur is laid.</summary>
    static int Arms(int cell)
    {
        var arms = 0;
        for (var arm = 0; arm < 4; arm++)
        {
            if (Neighbour(cell, (ExamArm)arm) >= 0) arms++;
        }

        return arms;
    }

    /// <summary>The first bearing a cell has no neighbour on, which is where its spur goes if it has one.</summary>
    static ExamArm? FreeBearing(int cell)
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
