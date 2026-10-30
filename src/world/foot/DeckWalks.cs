using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Foot;

/// <summary>
/// <b>The walk across the decks over other roads</b> (WLK-1b): each deck's own pavement, laid off the level above's walked
/// ground as the ground's is off its own (<see cref="LaneShell"/>), and joined to the ground's at each end of the deck by a
/// joint on both levels' channels — so a walker walks up onto a bridge and down off it, rather than round its end under it.
/// </summary>
/// <remarks>
/// <b>The two levels meet at a seam across each end of a deck.</b> The ground's pavement runs round the end of the deck's
/// lanes under the deck (<see cref="LaneShell"/>'s stub of them), and the deck's runs round the same end on top of it, the
/// sides of each its own lane's distance off the same kerb — so past the seam the ground's is walked by nobody, short of
/// <see cref="RoadFigures.DeckWalkJointM"/> past it the deck's is walked by nobody, and down each side of the road a joint
/// steps from the end one level's lane arrives at to the end the other level's sets off from.
/// </remarks>
internal sealed class DeckWalks
{
    /// <summary>A town with no deck over another road, which has no walk above its ground.</summary>
    public static readonly DeckWalks None = new(PavementLanes.None, []);

    readonly Joint[] _joints;

    DeckWalks(PavementLanes lanes, Joint[] joints)
    {
        Lanes = lanes;
        _joints = joints;
    }

    /// <summary>The decks' own pavement, each end of each deck walked by nobody short of its joints.</summary>
    public PavementLanes Lanes { get; }

    /// <summary>The joints between the ground's pavement and the decks', each walked one way.</summary>
    public ReadOnlySpan<Joint> Joints => _joints;

    /// <summary>One joint, walked from one level's lane end to the other's: up onto a deck, or down off it.</summary>
    internal readonly record struct Joint(Vector2 FromM, Vector2 ToM, bool Up);

    /// <summary>
    /// <b>The decks' walk off a plan, and the ground's pavement walked by nobody under each deck's ends</b>, which is where
    /// the joints take a walker instead. Build-time only.
    /// </summary>
    public static (PavementLanes Ground, DeckWalks Decks) Of(CityPlan plan, PavementLanes ground, SimConfig config)
    {
        var decks = Deck.AllRaised(plan);
        if (decks.Length == 0) return (ground, None);

        // <b>The ground's walk ends short of the deck's end, which is where the deck is drawn from</b>: a walker under it is
        // drawn under it, so the joint — on both levels, so drawn over it — begins before it.
        var groundSeamM = -config.UnderTheDeckM * 0.5f;
        var deckSeamM = groundSeamM + config.Road.DeckWalkJointM;

        // How far into a deck the ground's pavement reaches round the end of its lanes: the stub, the outer lane's
        // distance off it and the courses' rounding.
        var underReachM = config.UnderTheDeckM + config.WalkingLaneAtM(SimConfig.LanesPerPavement - 1) + config.Road.WalkRoundedM;
        bool UnderAnEnd(Vector2 atM)
        {
            foreach (var deck in decks)
            {
                var at = deck.Place(atM, underReachM);
                if (at.Held && at.InM > groundSeamM && at.InM < underReachM) return true;
            }

            return false;
        }

        bool PastAnEnd(Vector2 atM)
        {
            foreach (var deck in decks)
            {
                var at = deck.Place(atM, underReachM);
                if (at.Held) return at.InM < deckSeamM;
            }

            return true;
        }

        var underTheEnds = ground.GappedWhere((_, atM) => UnderAnEnd(atM), config);
        var paving = plan.Paving(config);
        var above = PavementLanes.Of(LaneShell.Of(paving, config, CityPlan.RoadArrays.Over, ShellOf.WalkedCourses), config)
            .GappedWhere((_, atM) => PastAnEnd(atM), config);

        var reachM = config.WalkingLaneWidthM;
        var groundEnds = Ends(underTheEnds, decks, groundSeamM, reachM, underReachM);
        var deckEnds = Ends(above, decks, deckSeamM, reachM, underReachM);
        var joints = new List<Joint>();
        foreach (var onTheGround in groundEnds)
        {
            foreach (var onTheDeck in deckEnds)
            {
                if (onTheGround.Where != onTheDeck.Where) continue;

                if (onTheGround.Arrives && !onTheDeck.Arrives) joints.Add(new Joint(onTheGround.AtM, onTheDeck.AtM, Up: true));
                else if (onTheDeck.Arrives && !onTheGround.Arrives) joints.Add(new Joint(onTheDeck.AtM, onTheGround.AtM, Up: false));
            }
        }

        return (underTheEnds, new DeckWalks(above, [.. joints]));
    }

    /// <summary>Which end of which deck, and which side of its road, a lane's end stands at.</summary>
    readonly record struct Seam(int Deck, bool AtItsStart, bool OnItsRight);

    /// <summary>A lane's end at a seam, and whether the lane arrives there or sets off from it.</summary>
    readonly record struct End(Vector2 AtM, Seam Where, bool Arrives);

    /// <summary>
    /// <b>Every end of a gap in these lanes standing at a deck's seam</b> <paramref name="seamM"/> in from either end, within
    /// <paramref name="reachM"/> of it — a ring's own start being no end (<see cref="PavementLanes.GapsOf"/>).
    /// </summary>
    /// <param name="endsM">How far either side of a deck's end it is read as straight on from its line (<see cref="Deck.Place"/>).</param>
    static List<End> Ends(PavementLanes lanes, Deck[] decks, float seamM, float reachM, float endsM)
    {
        var ends = new List<End>();
        for (var lane = 0; lane < lanes.Count; lane++)
        {
            var rings = lanes.RingsOf(lane);
            for (var ring = 0; ring < rings.Length; ring++)
            {
                var line = rings[ring];
                var lengthM = Spline.TotalLengthM(line);
                foreach (var (fromM, toM) in lanes.GapsOf(lane, ring))
                {
                    // Walked with its ring a lane arrives at a gap's start and sets off from its end, and against it the other way.
                    if (fromM > 0f) Add(Spline.SampleAt(line, fromM).PositionM, lanes.RunsWithTheRing(lane));
                    if (toM < lengthM) Add(Spline.SampleAt(line, toM).PositionM, !lanes.RunsWithTheRing(lane));
                }
            }
        }

        return ends;

        void Add(Vector2 atM, bool arrives)
        {
            for (var deck = 0; deck < decks.Length; deck++)
            {
                var at = decks[deck].Place(atM, endsM);
                if (!at.Held || MathF.Abs(at.InM - seamM) > reachM) continue;

                ends.Add(new End(atM, new Seam(deck, at.AtItsStart, at.OnItsRight), arrives));
                return;
            }
        }
    }

    /// <summary>
    /// Where a point stands against a deck: whether within its width of its road's line, how far in from its nearer end —
    /// short of it below nought — which end that is, and which side of the line.
    /// </summary>
    readonly record struct DeckPlace(bool Held, float InM, bool AtItsStart, bool OnItsRight);

    /// <summary>One deck over other roads: its road's line, the stretch of it decked, and half its width.</summary>
    sealed class Deck(ArcSeg[] line, float fromM, float toM, float halfWidthM)
    {
        readonly float _lengthM = Spline.TotalLengthM(line);

        public static Deck[] AllRaised(CityPlan plan)
        {
            var decks = new List<Deck>();
            for (var bridge = 0; bridge < plan.Bridges.Count; bridge++)
            {
                var road = plan.Bridges.Road[bridge];
                if (plan.Roads.LevelOf(road) == CityPlan.RoadArrays.Ground) continue;

                decks.Add(new Deck(
                    plan.Roads.SegmentsOf(road).ToArray(), plan.Bridges.FromM[bridge], plan.Bridges.ToM[bridge],
                    plan.Bridges.DeckWidthM[bridge] * 0.5f));
            }

            return [.. decks];
        }

        /// <summary>
        /// <b>Where a point stands against the deck</b> (<see cref="DeckPlace"/>). Within <paramref name="endsM"/> of an end
        /// it is read off the line run straight on past that end — a projection onto the line stops at its end, and the
        /// ground's walk ends short of it.
        /// </summary>
        public DeckPlace Place(Vector2 atM, float endsM)
        {
            var alongM = Spline.ProjectM(line, atM, _lengthM * 0.5f, _lengthM, out var offSq);
            var atItsStart = alongM - fromM < toM - alongM;
            var inM = atItsStart ? alongM - fromM : toM - alongM;
            if (inM > endsM)
            {
                var on = Spline.SampleAt(line, alongM);
                return new DeckPlace(offSq <= halfWidthM * halfWidthM, inM, atItsStart, Vector2.Dot(atM - on.PositionM, on.Right) > 0f);
            }

            var end = Spline.SampleAt(line, atItsStart ? fromM : toM);
            var offM = atM - end.PositionM;
            var acrossM = Vector2.Dot(offM, end.Right);
            var intoM = Vector2.Dot(offM, end.Direction) * (atItsStart ? 1f : -1f);
            return new DeckPlace(MathF.Abs(acrossM) <= halfWidthM, intoM, atItsStart, acrossM > 0f);
        }
    }
}
