using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>Where everybody the walking exam stages stands</b>: the ground is the lattice's
/// (<see cref="ExamGround"/>) and who is on it is the cards' — where every body is put down and where it is
/// sent. <see cref="FootwayPlan"/> writes the map from it and the harness reads the same answers, so what
/// a test walks at is what the map was laid to.
/// </summary>
/// <remarks>
/// <b>One arithmetic, read twice.</b> A card names a place as an arm, a side and a distance out
/// (<see cref="WalkStand"/>); this turns that into a point, and it is the only thing that does — a rig
/// working out its own kerbs would be measuring the rig.
/// </remarks>
internal sealed class FootwayLattice
{
    readonly ExamGround _ground;
    readonly int[] _firstWalker = new int[FootwayCards.Count];

    FootwayLattice(SimConfig config)
    {
        var cells = new ExamCell[FootwayCards.Count];
        for (var card = 0; card < FootwayCards.Count; card++)
        {
            var of = FootwayCards.All[card];
            cells[card] = new ExamCell(of.Spur, of.Lit, of.Stage);
        }

        _ground = new ExamGround(config, FootwayCards.Rows, FootwayCards.Columns, cells);

        for (var card = 0; card < FootwayCards.Count; card++)
        {
            _firstWalker[card] = Walkers;
            Walkers += FootwayCards.All[card].Walkers.Length;
        }
    }

    public static FootwayLattice Of(SimConfig config) => new(config);

    /// <summary>The ground the cards are staged on, for whoever is laying it or measuring against it.</summary>
    public ExamGround Ground => _ground;

    /// <summary>How many bodies the map stands up, which is every walker of every card.</summary>
    public int Walkers { get; private set; }

    /// <summary>
    /// The first of a card's bodies in the fleet. <b>A card's walkers are laid together and in the card's
    /// own order</b>, so which body belongs to a card is read here rather than counted a second time at the
    /// call site.
    /// </summary>
    public int WalkerOf(int card, int walker) => _firstWalker[card] + walker;

    /// <summary>Where one of a card's bodies is put down.</summary>
    public Vector2 StandM(int card, int walker) => PlaceM(card, FootwayCards.All[card].Walkers[walker].From);

    /// <summary>And where it is sent, which for a body that is furniture is where it already stands.</summary>
    public Vector2 AimM(int card, int walker) => PlaceM(card, FootwayCards.All[card].Walkers[walker].To);

    /// <summary>
    /// Which way it faces when it is put down: where it is going, or — for a body that is going nowhere —
    /// across the road it is standing beside, which is the pose somebody at a kerb is in (PER-15).
    /// </summary>
    public float FacingRad(int card, int walker)
    {
        var of = FootwayCards.All[card].Walkers[walker];
        var runM = AimM(card, walker) - StandM(card, walker);
        return runM.LengthSquared() > 0f
            ? ExamGround.Facing(runM)
            : ExamGround.Facing(-ExamGround.Across(of.From.Arm, of.From.Side));
    }

    /// <summary>
    /// A card's own place, as a point: along the arm it names, and off the middle of that arm by the
    /// pavement's line or by the lane's, depending on which ground it stands on.
    /// </summary>
    public Vector2 PlaceM(int card, WalkStand stand)
    {
        var alongM = stand.OutM == FootwayCards.AtThePaint ? _ground.CrossingOutM : stand.OutM;
        var acrossM = stand.Ground == WalkGround.Pavement ? _ground.KerbOffsetM : _ground.LaneOffsetM;
        return _ground.StageM(card)
               + (ExamGround.Bearing(stand.Arm) * alongM)
               + (ExamGround.Across(stand.Arm, stand.Side) * acrossM);
    }
}
