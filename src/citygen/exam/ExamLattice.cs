using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Exam;

/// <summary>
/// <b>Where every card of the exam is, and where everything it stages stands</b>: which cell each card was
/// given and how far its frame was turned to fit it, the ground those cells make (<see cref="ExamGround"/>),
/// and the pose every staged car starts and finishes in. <see cref="ExamPlan"/> writes the map from it and the
/// harness reads the same answers, so what a test drives at is what the map was laid to.
/// </summary>
/// <remarks>
/// <para>
/// <b>A card is written in its own frame and turned onto the cell it is given.</b> Right-hand traffic is the
/// same traffic a quarter turn round, so a T is written once with its stem to the south and stood on
/// whichever edge of the lattice has room; the crossroads and the rings go in the middle, and the dead ends at
/// the corners, where a spur out of the lattice can be laid without making anybody else's junction a
/// different shape.
/// </para>
/// <para>
/// <b>A car starts and finishes on its own side of the centreline</b> (TER-4a), a stand back from the box on
/// the arm it comes from and a run on past it on the arm it leaves by — so the movement a card names is the
/// shortest route between the two, and a driver ordered to the far point has been asked for that movement.
/// </para>
/// </remarks>
internal sealed class ExamLattice
{
    public const int Rows = 11;

    public const int Columns = 11;

    public const int NoWalker = -1;

    readonly SimConfig _config;
    readonly ExamCard[] _cards;
    readonly int[] _cell;
    readonly int[] _quarters;
    readonly int[] _firstCar;
    readonly int[] _firstWalker;
    readonly int[] _cardAt;

    ExamLattice(SimConfig config, ExamCard[] cards)
    {
        _config = config;
        _cards = cards;
        _cell = new int[cards.Length];
        _quarters = new int[cards.Length];
        _firstCar = new int[cards.Length];
        _firstWalker = new int[cards.Length];
        _cardAt = new int[Rows * Columns];
        Array.Fill(_cardAt, -1);

        var cells = Place(cards);
        Ground = new ExamGround(config, ExamPlan.Seed, Rows, Columns, cells);

        for (var card = 0; card < cards.Length; card++)
        {
            _firstCar[card] = Cars;
            Cars += cards[card].Drivers.Length;
        }

        for (var card = 0; card < cards.Length; card++)
        {
            _firstWalker[card] = Walkers;
            Walkers += cards[card].Walkers.Length;
        }

        for (var card = 0; card < cards.Length; card++) Check(card);
    }

    /// <summary>The exam as it ships: every card of <see cref="ExamCards"/>.</summary>
    public static ExamLattice Of(SimConfig config) => new(config, ExamCards.All.ToArray());

    /// <summary>Any set of cards laid the same way, for a test that wants the placement without the whole table.</summary>
    public static ExamLattice Of(SimConfig config, ExamCard[] cards) => new(config, cards);

    public ExamGround Ground { get; }

    public int Cards => _cards.Length;

    public ExamCard Card(int card) => _cards[card];

    /// <summary>How many cars the map stands up, which is every driver of every card.</summary>
    public int Cars { get; }

    /// <summary>How many bodies the map stands on foot, which is every walker of every card.</summary>
    public int Walkers { get; }

    /// <summary>The cell a card was given.</summary>
    public int CellOf(int card) => _cell[card];

    /// <summary>The card on a cell, or −1 where the cell carries none.</summary>
    public int CardAt(int cell) => _cardAt[cell];

    /// <summary>A bearing of the card's own frame, as the lattice's.</summary>
    public ExamArm Arm(int card, ExamArm inTheCard) => ExamGround.Turned(inTheCard, _quarters[card]);

    /// <summary>The car of the fleet one of a card's drivers is. A card's drivers are laid together and in the card's own order.</summary>
    public int CarOf(int card, int driver) => _firstCar[card] + driver;

    /// <summary>The body on foot one of a card's walkers is, among the walkers alone — the people are laid after the cars.</summary>
    public int WalkerOf(int card, int walker) => _firstWalker[card] + walker;

    /// <summary>The junction a card is staged at, or the middle of its ring.</summary>
    public Vector2 StageM(int card) => Ground.StageM(_cell[card]);

    /// <summary>How far round the card's box reaches: a body with its nose inside this is on the box.</summary>
    public float BoxRadiusM(int card) => _cards[card].Shape == ExamShape.DeadEnd
        ? _config.JunctionRadiusM
        : Ground.BoxRadiusM(_cell[card]);

    /// <summary>
    /// The junction one arm of a card's junction runs out of — the node, the ring's own node on that bearing,
    /// or the head of a dead end — which is what every distance along that arm is measured from.
    /// </summary>
    public int JunctionOf(int card, ExamArm inTheCard) => _cards[card].Shape == ExamShape.DeadEnd
        ? Ground.Head(_cell[card])
        : Ground.ArmJunction(_cell[card], Arm(card, inTheCard));

    /// <summary>The road on one arm of a card's junction — for a dead end, the spur it is the head of.</summary>
    public int RoadOf(int card, ExamArm inTheCard) => _cards[card].Shape == ExamShape.DeadEnd
        ? Ground.ArmRoad(_cell[card], Ground.Cell(_cell[card]).Spur ?? ExamArm.North)
        : Ground.ArmRoad(_cell[card], Arm(card, inTheCard));

    /// <summary>
    /// The lattice's bearing out of the card's junction along one of its arms. <b>The node's and not the
    /// road's</b>: an arm leaves a little off it (<see cref="ExamGround"/>), which is nothing a question about
    /// which arm a body is on can tell.
    /// </summary>
    public Vector2 Outward(int card, ExamArm inTheCard) => ExamGround.Bearing(Arm(card, inTheCard));

    /// <summary>Where one of a card's cars starts: a stand back from the box, in the lane arriving on the arm it comes from.</summary>
    public Vector2 StandM(int card, int driver) => Stand(card, driver).AtM;

    /// <summary>And which way it faces there, which is at the junction it is staged for.</summary>
    public float StandHeadingRad(int card, int driver) => ExamGround.Facing(Stand(card, driver).Travel);

    (Vector2 AtM, Vector2 Travel) Stand(int card, int driver)
    {
        var drives = _cards[card].Drivers[driver];
        return Ground.OnTheLane(
            JunctionOf(card, drives.From), RoadOf(card, drives.From), drives.StandBackM, arriving: !drives.Outbound);
    }

    /// <summary>
    /// And where it is sent: a run on past the box in the lane leaving on the arm it goes out by. One standing
    /// outbound is sent on down the lane it stands in.
    /// </summary>
    public Vector2 AimM(int card, int driver)
    {
        var drives = _cards[card].Drivers[driver];
        var arm = drives.Outbound ? drives.From : drives.To;
        return Ground.OnTheLane(JunctionOf(card, arm), RoadOf(card, arm), drives.RunOnM, arriving: false).AtM;
    }

    /// <summary>
    /// <b>Every card given a cell</b>: the crossroads and the rings to the middle of the lattice, the T
    /// junctions round its edges turned to face in, and the dead ends at the corners on a spur of their own. A
    /// table asking for more of one shape than the lattice has room for fails here.
    /// </summary>
    /// <remarks>
    /// <b>A road is shared by the two cells at its ends</b>, so a card that makes one of its arms one-way makes
    /// its neighbour's arm one-way too. The cards that ask for one-way arms are placed first, and every card
    /// takes the first free cell of its shape whose neighbours' one-way roads it drives the right way — and
    /// whose own it does not ask a neighbour to drive the wrong way. Otherwise a card is in table order.
    /// </remarks>
    ExamCell[] Place(ExamCard[] cards)
    {
        var cells = new ExamCell[Rows * Columns];
        var middle = new List<int>();
        var edges = new List<int>();
        var corners = new List<int>();
        for (var cell = 0; cell < cells.Length; cell++)
        {
            var arms = Arms(cell);
            (arms == 4 ? middle : arms == 3 ? edges : corners).Add(cell);
        }

        var order = new List<int>();
        for (var card = 0; card < cards.Length; card++) if (OneWay(cards[card])) order.Add(card);
        for (var card = 0; card < cards.Length; card++) if (!OneWay(cards[card])) order.Add(card);

        foreach (var card in order)
        {
            var of = cards[card];

            // Where each shape goes first, and where it goes once that is full: a crossroads onto an edge with a
            // spur for its fourth arm, a T onto a corner with a spur for its third, and a dead end at the head
            // of a spur from an edge. A ring stands only where four roads already meet.
            var (first, then) = of.Shape switch
            {
                ExamShape.Crossroads => (middle, edges),
                ExamShape.Roundabout => (middle, null),
                ExamShape.Tee => (edges, corners),
                _ => (corners, edges),
            };

            var cell = Free(cards, card, first);
            var list = first;
            if (cell < 0 && then is not null)
            {
                cell = Free(cards, card, then);
                list = then;
            }

            if (cell < 0)
            {
                throw new InvalidOperationException(
                    $"The exam has no cell left for \"{of.Name}\", a {of.Shape}, on a {Rows} by {Columns} lattice.");
            }

            list.Remove(cell);
            _quarters[card] = Quarters(of, cell);
            _cell[card] = cell;
            _cardAt[cell] = card;
            cells[cell] = new ExamCell(
                Spur: SpurOf(of, cell),
                AtTheHead: of.Shape == ExamShape.DeadEnd,
                Lit: of.Lit,
                Roundabout: of.Shape == ExamShape.Roundabout,
                In: Mask(card, of.OneWayIn),
                Out: Mask(card, of.OneWayOut));
        }

        return cells;
    }

    /// <summary>The first cell of a list the card fits on, or −1.</summary>
    int Free(ExamCard[] cards, int card, List<int> free)
    {
        foreach (var cell in free)
        {
            if (Fits(cards, card, cell)) return cell;
        }

        return -1;
    }

    static bool OneWay(ExamCard card) => card.OneWayIn.Length > 0 || card.OneWayOut.Length > 0;

    /// <summary>
    /// The spur a card's cell is given, or none: a dead end is the head of one, a crossroads on an edge takes
    /// one for its fourth arm, and a T on a corner one for its third — along the first bearing clockwise from
    /// north with nothing on it, which leaves a corner's second free bearing the T's open side.
    /// </summary>
    static ExamArm? SpurOf(ExamCard card, int cell) => card.Shape switch
    {
        ExamShape.DeadEnd => FreeBearing(cell),
        ExamShape.Crossroads when Arms(cell) < 4 => FreeBearing(cell),
        ExamShape.Tee when Arms(cell) < 3 => FreeBearing(cell),
        _ => null,
    };

    /// <summary>
    /// How far a card's frame is turned to fit a cell: until its missing arm — a T's north, a dead end's spur —
    /// is the cell's. A T on a corner is open on the corner's second free bearing, its spur taking the first.
    /// </summary>
    static int Quarters(ExamCard card, int cell) => card.Shape switch
    {
        ExamShape.Tee when Arms(cell) < 3 => (int)SecondFreeBearing(cell),
        ExamShape.Tee or ExamShape.DeadEnd => (int)FreeBearing(cell),
        _ => 0,
    };

    /// <summary>The second bearing, clockwise from north, with no neighbour on it — a corner's other open side.</summary>
    static ExamArm SecondFreeBearing(int cell)
    {
        var found = 0;
        for (var arm = 0; arm < 4; arm++)
        {
            if (NeighbourOf(cell, (ExamArm)arm) >= 0) continue;
            if (found++ == 1) return (ExamArm)arm;
        }

        return FreeBearing(cell);
    }

    /// <summary>
    /// Whether a card can stand on a cell beside the cards already placed: along every road it shares with
    /// one of them, the two ask the same of which way it runs, and each drives it only that way — and <b>a
    /// road either of them walks is driven both ways</b>, because a one-way road is one lane wide (TER-4d)
    /// and the far side of its zebra is the lane its traffic drives.
    /// </summary>
    bool Fits(ExamCard[] cards, int card, int cell)
    {
        var quarters = Quarters(cards[card], cell);
        for (var arm = 0; arm < 4; arm++)
        {
            var beyond = NeighbourOf(cell, (ExamArm)arm);
            if (beyond < 0 || _cardAt[beyond] < 0) continue;

            var other = _cardAt[beyond];
            var back = ExamGround.Opposite((ExamArm)arm);
            var here = Use(cards[card], quarters, (ExamArm)arm);
            var there = Use(cards[other], _quarters[other], back);

            // Into this cell is out of that one, and the other way round.
            var intoHere = here.In || there.Out;
            var outOfHere = here.Out || there.In;
            if (intoHere && outOfHere) return false;
            if ((intoHere || outOfHere) && (here.Walked || there.Walked)) return false;
            if (intoHere && (here.Leaves || there.Arrives)) return false;
            if (outOfHere && (here.Arrives || there.Leaves)) return false;
        }

        return true;
    }

    /// <summary>What a card placed at some turn asks of the road on one lattice bearing, and how its cars and walkers use it.</summary>
    static (bool In, bool Out, bool Arrives, bool Leaves, bool Walked) Use(ExamCard card, int quarters, ExamArm arm)
    {
        var @in = false;
        var @out = false;
        foreach (var one in card.OneWayIn) @in |= ExamGround.Turned(one, quarters) == arm;
        foreach (var one in card.OneWayOut) @out |= ExamGround.Turned(one, quarters) == arm;

        var arrives = false;
        var leaves = false;
        foreach (var drives in card.Drivers)
        {
            var from = ExamGround.Turned(drives.From, quarters) == arm;
            var to = ExamGround.Turned(drives.To, quarters) == arm;
            if (drives.Outbound) leaves |= from;
            else arrives |= from;
            if (!drives.Parked && !drives.Outbound) leaves |= to;
        }

        var walked = false;
        foreach (var walks in card.Walkers)
        {
            walked |= ExamGround.Turned(walks.Crossing, quarters) == arm
                      || (walks.RoundTo is { } to && ExamGround.Turned(to, quarters) == arm);
        }

        return (@in, @out, arrives, leaves, walked);
    }

    byte Mask(int card, ExamArm[] arms)
    {
        byte mask = 0;
        foreach (var arm in arms) mask |= ExamCell.Mask(Arm(card, arm));

        return mask;
    }

    /// <summary>How many neighbours a cell has, which is the shape it is before anything is laid.</summary>
    static int Arms(int cell)
    {
        var arms = 0;
        for (var arm = 0; arm < 4; arm++)
        {
            if (NeighbourOf(cell, (ExamArm)arm) >= 0) arms++;
        }

        return arms;
    }

    /// <summary>
    /// The first bearing, clockwise from north, with no neighbour on it — which for an edge cell is the one
    /// its junction is missing and for a corner is the one its spur is laid along.
    /// </summary>
    static ExamArm FreeBearing(int cell)
    {
        for (var arm = 0; arm < 4; arm++)
        {
            if (NeighbourOf(cell, (ExamArm)arm) < 0) return (ExamArm)arm;
        }

        return ExamArm.North;
    }

    static int NeighbourOf(int cell, ExamArm arm)
    {
        var row = (cell / Columns) + (arm == ExamArm.North ? 1 : arm == ExamArm.South ? -1 : 0);
        var column = (cell % Columns) + (arm == ExamArm.East ? 1 : arm == ExamArm.West ? -1 : 0);
        return row < 0 || column < 0 || row >= Rows || column >= Columns ? -1 : (row * Columns) + column;
    }

    /// <summary>
    /// <b>A card that could not be staged as written fails here</b>, when the map is laid, rather than on the
    /// road: a car standing on an arm its junction does not have, standing against a one-way street or sent
    /// down one, or a claim about a driver or a walker the card does not stage.
    /// </summary>
    /// <remarks>
    /// <b>The flow is read off the road as it was laid</b>, so a one-way street a neighbouring card asked for
    /// refuses this card's car exactly as one of its own would.
    /// </remarks>
    void Check(int card)
    {
        var of = _cards[card];
        var cell = _cell[card];
        for (var driver = 0; driver < of.Drivers.Length; driver++)
        {
            var drives = of.Drivers[driver];
            if (!HasArm(card, drives.From) || (!drives.Parked && !HasArm(card, drives.To)))
            {
                throw new InvalidOperationException(
                    $"\"{of.Name}\": driver {driver} uses an arm the {of.Shape} does not have.");
            }

            if (of.Shape == ExamShape.DeadEnd) continue;

            var against = drives.Outbound
                ? RunsIn(cell, Arm(card, drives.From))
                : RunsOut(cell, Arm(card, drives.From)) || (!drives.Parked && RunsIn(cell, Arm(card, drives.To)));
            if (against)
            {
                throw new InvalidOperationException(
                    $"\"{of.Name}\": driver {driver} is staged against the one-way flow of the arm it uses.");
            }
        }

        for (var walker = 0; walker < of.Walkers.Length; walker++)
        {
            var walks = of.Walkers[walker];
            if (HasArm(card, walks.Crossing) && (walks.RoundTo is not { } to || HasArm(card, to))) continue;

            throw new InvalidOperationException($"\"{of.Name}\": walker {walker} walks an arm the {of.Shape} does not have.");
        }

        foreach (var claim in of.Claims)
        {
            var others = claim.Rule == ExamRule.ForWalker ? of.Walkers.Length : of.Drivers.Length;
            var needsOther = claim.Rule is ExamRule.YieldsTo or ExamRule.Before or ExamRule.ForWalker;
            if (claim.Subject >= 0 && claim.Subject < of.Drivers.Length
                && (!needsOther || (claim.Other >= 0 && claim.Other < others)))
            {
                continue;
            }

            throw new InvalidOperationException($"\"{of.Name}\": a {claim.Rule} claim names somebody the card does not stage.");
        }
    }

    bool HasArm(int card, ExamArm inTheCard)
    {
        var arm = Arm(card, inTheCard);
        return _cards[card].Shape == ExamShape.DeadEnd
            ? arm == ExamGround.Opposite(Ground.Cell(_cell[card]).Spur ?? ExamArm.North)
            : Ground.HasArm(_cell[card], arm);
    }

    /// <summary>
    /// Whether the road on one bearing of a cell runs one way out of it — asked of the road as it was laid,
    /// so a neighbour's one-way street is read the same as the cell's own.
    /// </summary>
    bool RunsOut(int cell, ExamArm arm) => Runs(cell, arm, into: false);

    bool RunsIn(int cell, ExamArm arm) => Runs(cell, arm, into: true);

    bool Runs(int cell, ExamArm arm, bool into)
    {
        var road = Ground.ArmRoad(cell, arm);
        if (road == ExamGround.NoRoad) return false;

        var laid = Ground.Roads[road];
        if (laid.Flow == RoadFlow.BothWays) return false;

        var arrivesAtTo = laid.Flow == RoadFlow.WithTheRoad;
        var here = Ground.ArmJunction(cell, arm);
        var arrivesHere = arrivesAtTo == (laid.ToJunction == here);
        return into == arrivesHere;
    }

    /// <summary>
    /// <b>The zebra on one arm of a card's junction</b>: the two kerb points the walk crosses between there
    /// (<see cref="KerbEnds.CrossedM"/>), the first on the left of the arm's bearing out of the junction and
    /// the second on its right. False where the town painted none — a dead end, a ring, or an arm the card's
    /// junction does not have.
    /// </summary>
    public bool Zebra(int card, ExamArm inTheCard, KerbEnds ends, out Vector2 leftM, out Vector2 rightM)
    {
        (leftM, rightM) = (Vector2.Zero, Vector2.Zero);
        var cell = _cell[card];
        var arm = Arm(card, inTheCard);
        var road = Ground.ArmRoad(cell, arm);
        if (road == ExamGround.NoRoad || _cards[card].Shape == ExamShape.DeadEnd) return false;

        var atTo = Ground.Roads[road].ToJunction == Ground.ArmJunction(cell, arm);
        if (!ExamPlan.Station(ends.CrossedM, road, atTo, out var station) || !station.Painted) return false;

        var right = Heading.RightOf(ExamGround.Bearing(arm));
        var nearIsRight = Vector2.Dot(station.NearM - station.FarM, right) > 0f;
        (leftM, rightM) = nearIsRight ? (station.FarM, station.NearM) : (station.NearM, station.FarM);
        return true;
    }

    /// <summary>
    /// <b>Where one of a card's walkers waits and where it is sent</b>: on the paint at one end of its zebra,
    /// half its own width inside the kerb, and on the paint at the other, its whole width inside — or, for one
    /// walking round a corner, the middle of the pavement at either end of the walk.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Paint to paint and never kerb to kerb</b>, because a walk that begins or ends on the pavement is not
    /// reliably routed over a zebra in this build (the known gaps, "no walk chooses a zebra"): sent from the
    /// kerb, a walker at some zebras goes round the block, and one sent on to the pavement from the far edge
    /// walks back over the paint to get there. Asked from one edge of the paint to the other, the walk is the
    /// zebra and nothing else.
    /// </para>
    /// <para>
    /// <b>So a walker stands in the edge of a lane at both ends.</b> A body is a metre across and a lane's
    /// ground runs to within half a metre of the kerb, so the lane beside it holds that body — before it sets
    /// off, and for good once it is over. A card stands it on the side of the lane the car it is about arrives
    /// in, and sends it to a side nobody in the card drives.
    /// </para>
    /// </remarks>
    public bool Kerbs(int card, int walker, KerbEnds ends, out Vector2 fromM, out Vector2 toM) =>
        Kerbs(card, walker, ends, out fromM, out _, out toM);

    /// <summary>
    /// And the place it stands on the way where it pauses: <b>the middle of the lane beside the kerb it set off
    /// from</b>, on the paint — which is the lane the car the card is about arrives in.
    /// </summary>
    public bool Kerbs(int card, int walker, KerbEnds ends, out Vector2 fromM, out Vector2 inTheLaneM, out Vector2 toM)
    {
        var walks = _cards[card].Walkers[walker];
        (fromM, inTheLaneM, toM) = (Vector2.Zero, Vector2.Zero, Vector2.Zero);
        if (walks.RoundTo is { } roundTo)
        {
            fromM = inTheLaneM = PavementM(card, walks.Crossing, roundTo, walks.OutM);
            toM = PavementM(card, roundTo, walks.Crossing, walks.OutM);
            return true;
        }

        if (!Zebra(card, walks.Crossing, ends, out var leftM, out var rightM)) return false;

        var across = Vector2.Normalize(rightM - leftM);
        var intoM = _config.PersonDiameterM * 0.5f;
        var shortM = _config.PersonDiameterM;
        var laneM = _config.LaneWidthM * 0.5f;
        (fromM, inTheLaneM, toM) = walks.From == ExamSide.Left
            ? (leftM + (across * intoM), leftM + (across * laneM), rightM - (across * shortM))
            : (rightM - (across * intoM), rightM - (across * laneM), leftM + (across * shortM));
        return true;
    }

    /// <summary>Half a pavement's width, which is how far a body standing on one stands off the kerb.</summary>
    public float HalfAPavementM => _config.PavementWidthM * 0.5f;

    /// <summary>
    /// <b>The middle of the pavement a distance out along one arm, on the side of it that faces another</b> —
    /// which is the corner the two share. The arm is driven both ways (<see cref="Fits"/>), so its kerb is half
    /// its carriageway off its line.
    /// </summary>
    Vector2 PavementM(int card, ExamArm on, ExamArm facing, float outM)
    {
        var road = RoadOf(card, on);
        var (laneM, travel) = Ground.OnTheLane(JunctionOf(card, on), road, outM, arriving: true);
        var side = Heading.RightOf(travel);
        if (Vector2.Dot(side, Outward(card, facing)) < 0f) side = -side;

        var kerbM = Ground.WidthM(Ground.Roads[road]) * 0.5f;
        return laneM - Ground.Beside(travel) + (side * (kerbM + HalfAPavementM));
    }
}
