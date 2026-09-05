using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>Where everything the driving exam stages stands</b>: the ground is the lattice's
/// (<see cref="ExamGround"/>) and what is on it is the cards' — the pose every staged car starts and
/// finishes in, and the body standing at the paint a card is about. <see cref="ExamPlan"/> writes the map
/// from it and the harness reads the same answers, so what a test drives at is what the map was laid to.
/// </summary>
/// <remarks>
/// <b>A car starts and finishes on its own side of the centreline</b> (TER-4a), a stand back from the box
/// on the arm it comes from and a run on past it on the arm it leaves by — so the movement a card names is
/// the only route between the two, and a driver ordered to the far point has been asked for that crossing
/// and no other.
/// </remarks>
internal sealed class ExamLattice
{
    /// <summary>How far past the box a driver is sent: clear of the junction and well short of the next one.</summary>
    public const float RunOnM = 40f;

    public const int NoWalker = -1;

    readonly ExamGround _ground;
    readonly int[] _walker = new int[ExamCards.Count];
    readonly int[] _firstCar = new int[ExamCards.Count];

    ExamLattice(SimConfig config)
    {
        Array.Fill(_walker, NoWalker);

        var cells = new ExamCell[ExamCards.Count];
        for (var card = 0; card < ExamCards.Count; card++)
        {
            var of = ExamCards.All[card];
            cells[card] = new ExamCell(of.Spur, of.Lit, of.Stage);
        }

        _ground = new ExamGround(config, ExamCards.Rows, ExamCards.Columns, cells);

        for (var card = 0; card < ExamCards.Count; card++)
        {
            _firstCar[card] = Cars;
            Cars += ExamCards.All[card].Drivers.Length;
        }

        for (var card = 0; card < ExamCards.Count; card++)
        {
            if (Watched(card, out _)) _walker[card] = Walkers++;
        }
    }

    public static ExamLattice Of(SimConfig config) => new(config);

    /// <summary>The ground the cards are staged on, for whoever is laying it or measuring against it.</summary>
    public ExamGround Ground => _ground;

    /// <summary>How many cars the map stands up, which is every driver of every card.</summary>
    public int Cars { get; private set; }

    /// <summary>How many bodies the map stands on foot, which is one at the kerb of every crossing a card is about.</summary>
    public int Walkers { get; private set; }

    /// <summary>The junction a card is staged at — its cell's own node, its spur's head, or the node struck in its south arm.</summary>
    public Vector2 StageM(int card) => _ground.StageM(card);

    /// <summary>How far a junction's ground reaches, which is what says a body is on the box.</summary>
    public float ReachM => _ground.ReachM;

    /// <summary>How far back from a junction its stop bar is painted, which is where a driver is told what the box is showing.</summary>
    public float BarM => _ground.BarM;

    /// <summary>How far off the middle of a crossing its own kerbs stand: half the carriageway and half a pavement.</summary>
    public float KerbOffsetM => _ground.KerbOffsetM;

    /// <summary>The first of a card's cars in the fleet. A card's drivers are laid together and in the card's own order.</summary>
    public int CarOf(int card, int driver) => _firstCar[card] + driver;

    /// <summary>Where one of a card's cars starts: a stand back from the box, on its own side of the arm it comes from.</summary>
    public Vector2 StandM(int card, int driver)
    {
        var drives = ExamCards.All[card].Drivers[driver];
        var outward = ExamGround.Bearing(drives.From);
        return StageM(card) + (outward * drives.StandBackM) + _ground.Beside(-outward);
    }

    /// <summary>And which way it faces there, which is at the junction it is staged for.</summary>
    public float StandHeadingRad(int card, int driver) =>
        ExamGround.Facing(-ExamGround.Bearing(ExamCards.All[card].Drivers[driver].From));

    /// <summary>
    /// And where it is sent: a run on past the box on the arm it leaves by, on that arm's own side of the
    /// centreline. <b>A driver sent back down the arm it came from is a card about turning round</b>
    /// (`P-19`), and the point lands on the other lane of the same road because the bearing it is read
    /// against is reversed.
    /// </summary>
    public Vector2 AimM(int card, int driver)
    {
        var drives = ExamCards.All[card].Drivers[driver];
        var outward = ExamGround.Bearing(drives.To);
        return StageM(card) + (outward * drives.RunOnM) + _ground.Beside(outward);
    }

    /// <summary>Which of the map's crossings a card is about — the one somebody is standing at.</summary>
    public bool Watched(int card, out ExamCrossing crossing)
    {
        var of = ExamCards.All[card];
        if (of.Stage == ExamStage.MidBlock)
        {
            crossing = _ground.MidBlock(card);
            return true;
        }

        if (of.Watched is not { } arm)
        {
            crossing = default;
            return false;
        }

        crossing = _ground.OnTheArm(_ground.Stage(card), arm);
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

        var across = Core.Geometry.Heading.RightOf(crossing.Axis);
        standM = crossing.CentreM + (across * KerbOffsetM);
        facingRad = ExamGround.Facing(-across);
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

        toM = crossing.CentreM - (Core.Geometry.Heading.RightOf(crossing.Axis) * KerbOffsetM);
        return true;
    }

    /// <summary>
    /// The body standing at the kerb of a card's own paint, or <see cref="NoWalker"/> where that card is
    /// about no paint. <b>The people are numbered after the cars and in card order</b>, which is the order
    /// <see cref="ExamPlan"/> puts them down in, so which body belongs to a card is read here rather than
    /// counted a second time at the call site.
    /// </summary>
    public int WalkerOf(int card) => _walker[card];
}
