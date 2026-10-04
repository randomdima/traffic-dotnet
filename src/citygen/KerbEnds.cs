using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>Where the town's outline stops following a road and starts following something else</b> — the places
/// a kerb really ends, as against the places a road's line ends.
/// </summary>
/// <remarks>
/// <para>
/// <b>A road ends further back than its line does.</b> The boundary of the driven ground
/// (<see cref="LaneShell"/>) is the outside of a union of bands, so approaching a junction it leaves the
/// road's own kerb wherever a movement's band, or the road it turns into, reaches further out — and from
/// there to the middle of the box the outline is the mouth widening rather than the street. The place a
/// walk beside that street has to stop is where the two part company, which is nearer than the lane's end
/// and is not a point any line in the plan carries.
/// </para>
/// <para>
/// <b>The boundary usually puts a point there, and where it does not the place is found along it.</b> A
/// ring turns only where the shape does (<see cref="BandShell"/>), so most handovers are a joint: the piece
/// before and the piece after are the two answers. But a mouth's edge that runs on into the kerb beyond it
/// along one line is one piece and not two, the rounding having joined consecutive stretches of one circle
/// into the one stretch they are — so the road takes over somewhere along that piece and the ring has no
/// point there at all. Nothing is intersected or offset here either way: the reading is taken at places on
/// the pieces the town already holds, and a piece is read from end to end at a pitch
/// (<see cref="Along"/>), every step that changes hands cut down to the place it changed at
/// (<see cref="Splits"/>).
/// </para>
/// <para>
/// <b>A piece answers as many things as it runs along, and every one of them is a handover.</b> A straight
/// kerb is one piece for every road it is straight through, so one stretch of a town's outline reads a
/// street, then the mouth it runs into, then the next street — and a reading that stopped at the first of
/// those would file that place under the road at the far end of the piece, which is a street whose own kerb
/// begins tens of metres further on, and would lose every handover after it. What the walk is cut at is the
/// place a road really gives the boundary up, so all of them are found and each is named by what stood
/// either side of it.
/// </para>
/// <para>
/// <b>The rounded boundary and not the merge's own</b> (<see cref="GroundRings.Carriageway"/>): the merge
/// hands back the corners a union of bands has, and the town does not have them all — the same move run again
/// at the kerb radius (TER-3c.10) fills the ones it turns into and keeps the ones it turns away at, and that
/// rounded line is what the tarmac stops at, what the kerbstone is bent to and what the pavement is struck
/// off. A point taken off the merge stands at a corner nothing was laid at, and it stands further into the
/// mouth than the kerb really runs, because the arc that rounds the corner leaves the road before the
/// corner does.
/// </para>
/// <para>
/// <b>What a piece runs along is asked of the piece and never of the merge's bookkeeping.</b> Every piece
/// of every ring is a piece of some band's own edge or of its square end, so a piece is a road's kerb when
/// it stands about half a width off that road's line, square to it and pointing the way it points — three
/// readings taken at a place on it, against whatever lines the town's index has near it
/// (<see cref="Paving.DrivenLines"/>). A movement's edge is half a movement's width off a line that is not
/// a road's; a square end is square to one rather than along it; and both of them answer "no road".
/// </para>
/// <para>
/// <b>About half a width, and the nearest line rather than the first</b> (<see cref="OffTheLineM"/>). The
/// ring is a built line and not a measured one — offset, merged, rounded and joined again, each step
/// allowed its own rounding — so it wanders off the exact half-width by more than the weld that joined it,
/// and by more the further it is joined. Asked for the offset to within a weld it reads one unbroken kerb
/// as its street for as long as the drift stays inside that figure and as nothing at all afterwards, which
/// puts a road's end at the arbitrary place the drift crossed the tolerance. Read at the figure two places
/// are one place at instead, and where more than one line answers, the one the piece stands nearest to the
/// edge of.
/// </para>
/// <para>
/// <b>The road and not the lane</b> (<c>LaneLines.LaneRoad</c>): a street is two lanes and the outline
/// crossing from one of them to the other has not left the street, while the outline crossing from one
/// street to the next has left the first — so the reading that marks a road's end is which road, with the
/// lane being the thing that answers it rather than the thing compared.
/// </para>
/// <para>
/// <b>A bay is not a street</b> (<see cref="CityPlan.RoadArrays.IsABay"/>, GEN-53): it is a road in the plan
/// like any other, but a walk beside it is a walk into a car park rather than down a pavement, and the place
/// it meets the street is the street's own kerb running past its mouth. So a piece of boundary standing off
/// a bay answers no road, and a car park laid off a street leaves the street's kerb one line with no ends in
/// it.
/// </para>
/// <para>
/// <b>And a box that does not fork is not an end either</b> (GEN-5a, <see cref="ForkedArms"/>). A bend
/// carries its traffic straight through and a dead end carries it back out the way it came, so nothing
/// turns across the walk beside the road at either — the kerb still turns a corner there and the pavement
/// turns with it, which is a corner and not a place a walk stops and is crossed. Ends are kept only where
/// three or more roads meet.
/// </para>
/// <para>
/// <b>Every change is one road's end, and a road ends twice at every box it runs into</b> — once on each
/// of its two kerbs. Those two are the pair (<see cref="Ranked"/>): the further of them out along the road
/// is <see cref="Further"/> and the other <see cref="Nearer"/>, so a street carries one of each at each
/// of its ends. <b>The pair is the road's and not the corner's</b>: the round at a corner also has two
/// ends, but they belong to two different streets, and both stop at the same box at the same radius — a
/// comparison with no answer, and one that puts the same street at both ends of its own two rounds.
/// </para>
/// <para>
/// <b>Nor is a roundabout's ring one</b> (GEN-19, WLK-2): it is a carriageway with no frontage on either
/// hand, so no walk runs beside it and none crosses it. The boundary leaving the circle is a corner of the
/// circle, and the arms that meet it keep the ends they have at the same boxes.
/// </para>
/// <para>
/// <b>And a road nobody would walk the length of is crossed once</b>
/// (<see cref="Core.Config.RoadFigures.CrossedOnceBelowM"/>, WLK-10a, <see cref="Cut"/>). The ends are the
/// road's and the stations are read off them one by one, so a street between two boxes a few strides apart
/// is cut twice by the same rule every other street is cut by — and what stands there is two zebras a
/// walker can stand between, neither of them where the walk wants to cross. Below the figure the walk
/// crosses once, midway between the pair (<see cref="CrossedM"/>). <b>The traffic is still held at both
/// ends</b> (<see cref="HeldM"/>), which is a fact about the box in front of it and not about the paint —
/// and held at the kerb end itself there, a station being where it is because a zebra stands at it.
/// </para>
/// <para>
/// <b>Out of the box along the road</b> (<see cref="OutM"/>): the run from the middle of the box to the
/// place, taken along the way the road points where it meets that box. What separates a street's two kerb
/// ends is how far into the mouth each of them runs, which is a fact about the mouth rather than about the
/// road's own line — a lane stops at the box's edge and both its kerbs stop within a hair of that, so
/// measured back along the lane instead the two answer the same nought.
/// </para>
/// </remarks>
internal sealed class KerbEnds
{
    /// <summary>What a piece of boundary that follows no road at all answers.</summary>
    const int None = -1;

    /// <summary>
    /// <b>What a piece of boundary standing off a car park's way answers</b> — a third answer, and neither
    /// of the other two: a park's ground is not a street, and it is not the nothing between two streets
    /// either, because what runs into it and what leaves it are the same place rather than two ends of a
    /// round (<see cref="CityPlan.RoadArrays.IsABay"/>, GEN-53).
    /// </summary>
    public const int Park = -2;

    readonly Mark[] _further;
    readonly Mark[] _nearer;
    readonly KerbNodes[] _heldM;
    readonly KerbNodes[] _crossedM;

    KerbEnds(Mark[] further, Mark[] nearer, KerbNodes[] heldM, KerbNodes[] crossedM)
    {
        _further = further;
        _nearer = nearer;
        _heldM = heldM;
        _crossedM = crossedM;
    }

    /// <summary>
    /// <b>The further of each road's two ends at one box</b> — the kerb of it that runs furthest out along
    /// the road before the boundary leaves it.
    /// </summary>
    /// <remarks>
    /// <b>Each end carries the reading that placed it</b> (<see cref="Mark"/>): the road and the box it is an
    /// end of, and how far out of that box it stands. That is the figure this pair was ranked on
    /// (<see cref="Ranked"/>) and the one an instrument weighs an end by, so an end handed over as a bare
    /// point would have to be matched back to a road by looking.
    /// </remarks>
    public ReadOnlySpan<Mark> Further => _further;

    /// <summary>
    /// <b>And the other kerb of the same road at the same box</b>, which stops nearer the middle of it.
    /// </summary>
    public ReadOnlySpan<Mark> Nearer => _nearer;

    /// <summary>
    /// <b>What the traffic arriving at each end of each street is held behind</b> (<c>World.Road.StopBars</c>,
    /// TER-6): the station the walk crosses at, where one stands there, and the end of the road's own kerb
    /// where the crossing went elsewhere (<see cref="KerbNodes.Painted"/>, WLK-10a). Two points to an end,
    /// one either side of the carriageway and both at the one place along the road
    /// (<see cref="Nodes"/>), so what is laid across them is square to the street rather than to a kerb.
    /// </summary>
    /// <remarks>
    /// <b>Named by the road end they stand at</b>, so a reader that lays anything across them — the bar a
    /// driver holds at (TER-6) — can put it on the arm it belongs to without matching points back to roads.
    /// </remarks>
    public ReadOnlySpan<KerbNodes> HeldM => _heldM;

    /// <summary>
    /// <b>And where the walk crosses</b>, which is a station at each end of each street but for a road too
    /// short to be crossed twice: that one's pair is welded into the station midway between them
    /// (<see cref="Core.Config.RoadFigures.CrossedOnceBelowM"/>, WLK-10a, <see cref="Cut"/>).
    /// </summary>
    /// <remarks>
    /// <b>Where the walk crosses and where the traffic is held are two answers</b> because a crossing that
    /// moves does not take the hold with it. The zebra goes where a walker would cross, which on a short
    /// street is the middle of it; the bar goes where the traffic is held for the box in front of it, which
    /// is that street's own end.
    /// </remarks>
    public ReadOnlySpan<KerbNodes> CrossedM => _crossedM;

    /// <summary>How many lines a reader asking about one piece may have near it, which is a corner's worth.</summary>
    const int NearOnePlace = 64;

    /// <summary>
    /// How many times a step is halved to find the place the road under it changes: enough to put the
    /// answer inside a millimetre of a step (<see cref="StepM"/>).
    /// </summary>
    const int Halvings = 10;

    /// <summary>
    /// <b>How finely a piece of boundary is read along its length</b> (<see cref="Along"/>) — a metre, which
    /// is the cost of the whole reading: one question of the index per metre of the town's outline, asked
    /// once when the town is laid. That is a tenth of a second on the largest city this build ships, against
    /// the eight the town takes to lay.
    /// </summary>
    /// <remarks>
    /// <b>A run of kerb shorter than this may be passed over</b>, and passing one over leaves the reading
    /// consistent rather than wrong: both of its handovers go together, so what is lost is a mark and never
    /// a mark in the wrong place. What is being looked for is where a street's frontage stops, and a
    /// frontage a stride long is not one.
    /// </remarks>
    const float StepM = 1f;

    /// <summary>
    /// <b>How far off the exact offset a piece of boundary may stand and still be read as that road's
    /// kerb</b> (<see cref="LineTolerance.OnePlaceM"/>, <see cref="LaneAlong"/>) — the figure two places are
    /// one place at, rather than the weld the ring's own pieces were joined at.
    /// </summary>
    /// <remarks>
    /// <b>The ring is a built line</b>: offset off the lines, merged, rounded at the kerb radius and joined
    /// again, each step allowed its own rounding. The line that comes out stands a weld off the exact
    /// half-width at the joints and further than that between them, and further again the longer the run
    /// joined into one piece — so a tolerance of a weld is a question the shape cannot answer, and answers it
    /// differently at the two ends of one unbroken kerb.
    /// </remarks>
    const float OffTheLineM = LineTolerance.OnePlaceM;

    /// <summary>
    /// How many roads have to meet at a box before it forks (GEN-5a). Two are a bend and one is a dead
    /// end: the traffic carries straight on through both, so there is nothing turning across a walk beside
    /// the road and nothing for that walk to stop short of.
    /// </summary>
    const int ForkedArms = 3;

    /// <summary>
    /// <b>What one piece of boundary reads as running along</b> — the lane, or <see cref="None"/> — for a
    /// reader asking about the stretch under their pointer (OBS-2t) rather than about the town. The same
    /// reading <see cref="Of"/> takes, so a stretch that answers nothing here is a stretch that carries no
    /// end there.
    /// </summary>
    public static int LaneUnder(Paving paving, SimConfig config, in ArcSeg piece, bool fromStart)
    {
        Span<int> near = stackalloc int[NearOnePlace];
        Span<float> alongM = stackalloc float[NearOnePlace];
        var index = paving.DrivenLines(config);
        return LaneAlong(
            paving.Lanes, index, index.NewScan(), near, alongM,
            ReachM(paving.Lanes) + OffTheLineM, piece, fromStart ? FirstM(piece) : LastM(piece));
    }

    /// <summary>
    /// The road a lane is one way of, for a caller holding what <see cref="LaneUnder"/> answered —
    /// <see cref="None"/> for no lane and <see cref="Park"/> for a car park's way.
    /// </summary>
    public static int RoadOf(Paving paving, int lane) => RoadOf(paving.Lanes, paving.Of.Roads, lane);

    static int RoadOf(LaneLines lanes, CityPlan.RoadArrays roads, int lane)
    {
        if (lane == None) return None;

        var road = lanes.LaneRoad[lane];
        return roads.IsABay(road) ? Park : road;
    }

    /// <summary>
    /// <b>The ends read off the town's own rounded boundary.</b> Build-time only — it allocates, and it asks
    /// the index once per piece of the outline.
    /// </summary>
    public static KerbEnds Of(Paving paving, SimConfig config)
    {
        var lanes = paving.Lanes;
        var index = paving.DrivenLines(config);
        var reachM = ReachM(lanes);

        var reading = new Reading(
            lanes, paving.Of.Roads, paving.Of.Junctions, paving.Of.ArmsPerJunction(),
            Circulating(paving.Of), index, reachM + OffTheLineM);
        var changes = new List<(Vector2 AtM, int Left, int Taken)>();
        var marks = new List<Mark>();

        foreach (var ring in paving.Rings(config).Carriageway.Rings)
        {
            if (ring.Length < 2) continue;

            Changes(reading, ring, changes);
            Marks(reading, changes, marks);
        }

        var (further, nearer, heldM, crossedM) = Ranked(reading, marks, config.Road);
        return new KerbEnds(further, nearer, heldM, crossedM);
    }

    /// <summary>
    /// One end of one road's kerb: where it stands, the road and the box it is an end of, and how far out
    /// of that box it stands along that road (<see cref="OutM"/>).
    /// </summary>
    public readonly record struct Mark(Vector2 AtM, int Lane, int Road, int Junction, float OutM);

    /// <summary>
    /// <b>The ends of one road at one box put in order</b>: a street has a kerb either side of it, so it
    /// ends twice at every box it runs into, and the further of those two out along the road is the one
    /// handed back as <see cref="Further"/>.
    /// </summary>
    /// <remarks>
    /// <b>The road's two ends and not the corner's two ends.</b> The round at a corner also has two ends,
    /// but they belong to two <em>different</em> roads, and ranking them ranks one street against the next
    /// rather than one kerb of a street against its other — which is a comparison with no answer, since
    /// both roads stop at the same box at the same radius. Grouped by the road instead, the two ends really
    /// are the same road's, the figure that separates them is how far into the mouth each kerb runs, and a
    /// street gets one of each at every box.
    /// </remarks>
    static (Mark[] Further, Mark[] Nearer, KerbNodes[] HeldM, KerbNodes[] CrossedM) Ranked(
        Reading reading, List<Mark> marks, RoadFigures figures)
    {
        var furthest = new Dictionary<(int Road, int Junction), int>();
        for (var at = 0; at < marks.Count; at++)
        {
            var key = (marks[at].Road, marks[at].Junction);
            if (!furthest.TryGetValue(key, out var standing) || marks[at].OutM > marks[standing].OutM)
            {
                furthest[key] = at;
            }
        }

        var further = new List<Mark>();
        var nearer = new List<Mark>();
        var winners = new HashSet<int>(furthest.Values);
        for (var at = 0; at < marks.Count; at++)
        {
            (winners.Contains(at) ? further : nearer).Add(marks[at]);
        }

        var stands = new List<Stand>();
        foreach (var at in furthest.Values) stands.Add(Stands(reading, marks[at], figures.FootNodeClearM));

        var (heldM, crossedM) = Cut(reading, stands, figures.CrossedOnceBelowM);
        return (further.ToArray(), nearer.ToArray(), heldM, crossedM);
    }

    /// <summary>
    /// <b>Where along one lane a walk beside one road is cut, before it is known whether that road is
    /// crossed there at all</b>: the station itself, the kerb end it was struck off, the side of the lane's
    /// own line that kerb stands on, and which of the road's ends it belongs to.
    /// </summary>
    /// <remarks>
    /// <b>The metres and not the points</b>, because neither place is settled yet: a pair of stations on one
    /// road may be welded into the one between them and the traffic then held at the kerb ends instead
    /// (<see cref="Cut"/>), and a place averaged out of two pairs of points is a place beside the road rather
    /// than on it wherever the road bends.
    /// </remarks>
    readonly record struct Stand(int Road, int Lane, float AlongM, float KerbAlongM, float Side, KerbCut Cut);

    /// <summary>
    /// <b>The places a town's walks cross, and the places its traffic is held</b> — the same list, but for a
    /// road whose two stations stand closer than <paramref name="onceBelowM"/>: that one is crossed once
    /// midway between them and held at each of its two kerb ends
    /// (<see cref="RoadFigures.CrossedOnceBelowM"/>, WLK-10a).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two crossings a few strides apart are one crossing in the wrong two places.</b> A street short
    /// enough for that is cut at both ends by the same rule every other street is, and what comes of it is a
    /// walker choosing between two zebras they can stand between — so the pair is welded into the one
    /// station midway between them, which the traffic meets from both hands.
    /// </para>
    /// <para>
    /// <b>And what a bar stands behind then is the kerb end itself.</b> A station is a station because a
    /// zebra stands at it: the traffic is held a setback clear of that paint, and clear of the paint is how
    /// far out along the road the bar ends up. With the paint gone to the middle of the street the bar is
    /// left standing at nothing, a carriageway's width out from the box it is supposed to hold for — so it
    /// falls back to the last thing there really is on the way in, which is the end of the road's own kerb
    /// (<see cref="Further"/>). The place is handed down carrying no paint, and the road tier holds a
    /// setback clear of the place itself rather than of a band's edge (<c>World.Road.StopBars</c>, TER-6).
    /// </para>
    /// </remarks>
    static (KerbNodes[] HeldM, KerbNodes[] CrossedM) Cut(
        Reading reading, List<Stand> stands, float onceBelowM)
    {
        var twin = Twins(stands);
        var heldM = new List<KerbNodes>();
        var crossedM = new List<KerbNodes>();
        for (var at = 0; at < stands.Count; at++)
        {
            var other = twin[at];
            var welded = other != None
                         && MathF.Abs(SharedM(reading, stands[at], stands[other]) - stands[at].AlongM)
                         < onceBelowM;

            var nodes = Nodes(reading, stands[at], painted: !welded);
            heldM.Add(nodes);
            if (!welded)
            {
                crossedM.Add(nodes);
                continue;
            }

            // Crossed once, at the first of the two the walk reaches.
            if (other < at) continue;

            crossedM.Add(Nodes(reading, Midway(reading, stands[at], stands[other])));
        }

        return (heldM.ToArray(), crossedM.ToArray());
    }

    /// <summary>
    /// The other station on the same road for each of them, or <see cref="None"/> for a road standing one:
    /// a road runs between two boxes, so it is cut at most twice and the two are each other's.
    /// </summary>
    static int[] Twins(List<Stand> stands)
    {
        var twin = new int[stands.Count];
        Array.Fill(twin, None);

        var standing = new Dictionary<int, int>();
        for (var at = 0; at < stands.Count; at++)
        {
            if (standing.TryAdd(stands[at].Road, at)) continue;

            var first = standing[stands[at].Road];
            twin[first] = at;
            twin[at] = first;
        }

        return twin;
    }

    /// <summary>
    /// <b>One station read in the metres of the other's lane</b>, which is the one frame the two can be
    /// weighed in: a road's two stations are read off its two kerbs, so they stand on lanes pointing
    /// opposite ways and their own metres run against each other.
    /// </summary>
    static float SharedM(Reading reading, in Stand on, in Stand of)
    {
        var lanes = reading.Lanes;
        var arcs = lanes.ArcsOf(on.Lane);
        var lengthM = lanes.LaneLengthM[on.Lane];
        return Spline.ProjectM(arcs, At(lanes, of), lengthM * 0.5f, lengthM);
    }

    /// <summary>The place one station stands, on its own lane's line.</summary>
    static Vector2 At(LaneLines lanes, in Stand stand) =>
        Spline.SampleAt(lanes.ArcsOf(stand.Lane), stand.AlongM).PositionM;

    /// <summary>The one station between two that stand too close to be two, on the first one's lane.</summary>
    static Stand Midway(Reading reading, in Stand one, in Stand other) =>
        one with
        {
            AlongM = (one.AlongM + SharedM(reading, one, other)) * 0.5f,
            Cut = KerbCut.Midway,
        };

    /// <summary>
    /// <b>Where a walk beside one road is cut at one box</b>: the further of the road's two kerb ends taken
    /// <paramref name="clearM"/> further out along the road, as the place on that kerb's own lane it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One station across the road and not one per kerb.</b> The two ends of a road stop at different
    /// depths into the mouth, so cutting each walk at its own kerb's end would put the two nodes of a
    /// street out of line with each other by as much as the mouth is lopsided — and what a walk is cut for
    /// is to be joined across, which wants a square line rather than two places that happen to be ends.
    /// <b>The further end is the one that sets it</b>, because the nearer one has already given up the
    /// boundary by then and a station taken off it would stand on ground the junction is driven over.
    /// </para>
    /// <para>
    /// <b>And never in the box</b>, which a kerb running along a roadside reaches (<see cref="LaneLines.RoadFromM"/>):
    /// what a walk crosses and a driver is held behind is the road, and the box beyond its end is the movements'.
    /// </para>
    /// </remarks>
    static Stand Stands(Reading reading, in Mark mark, float clearM)
    {
        var lanes = reading.Lanes;
        var arcs = lanes.ArcsOf(mark.Lane);
        var lengthM = lanes.LaneLengthM[mark.Lane];
        var atM = Spline.ProjectM(arcs, mark.AtM, lengthM * 0.5f, lengthM);

        // Away from the box, which is whichever end of the lane this end of it stands at.
        var outward = atM * 2f <= lengthM ? clearM : -clearM;

        // Which side of its own line the kerb this end stands on is, read off the end rather than assumed.
        var end = Spline.SampleAt(arcs, atM);
        var side = Vector2.Dot(mark.AtM - end.PositionM, end.Right) < 0f ? -1f : 1f;

        var (fromM, toM) = (lanes.RoadFromM(mark.Lane), lanes.RoadToM(mark.Lane));
        return new Stand(
            mark.Road, mark.Lane, Math.Clamp(atM + outward, fromM, toM), Math.Clamp(atM, fromM, toM), side,
            reading.AtTheFarEnd(mark.Road, mark.Junction) ? KerbCut.AtTheFarEnd : KerbCut.AtTheNearEnd);
    }

    /// <summary>
    /// The two nodes of one station: the road's own edge either side of the place it stands at — or either
    /// side of the kerb end it was struck off, where nothing is painted at it and what stands there is the
    /// traffic's hold rather than the walk's crossing.
    /// </summary>
    /// <remarks>
    /// <b>The far edge is read off the carriageway's own width and not off the shape.</b> A street's carriageway
    /// is its lanes side by side, a roadside the outermost of them where it has one (TER-4d, GEN-57), and the lane a
    /// kerb runs along is the outermost, so the near edge is that lane's half and the far edge the rest of the
    /// carriageway across — three half-lanes on a street of one lane each way, and the lane's own half on a road of
    /// one lane.
    /// </remarks>
    static KerbNodes Nodes(Reading reading, in Stand stand, bool painted = true)
    {
        var lanes = reading.Lanes;
        var nearM = lanes.LaneWidthM[stand.Lane] * 0.5f;
        var farM = reading.Roads.WidthM[lanes.LaneRoad[stand.Lane]] - nearM;

        var on = Spline.SampleAt(lanes.ArcsOf(stand.Lane), painted ? stand.AlongM : stand.KerbAlongM);
        return new KerbNodes(
            stand.Road, stand.Cut,
            on.PositionM + (on.Right * (nearM * stand.Side)),
            on.PositionM - (on.Right * (farM * stand.Side)),
            painted);
    }

    /// <summary>
    /// <b>Every place round one ring where it stops following one road and starts following another</b>, in
    /// the order the ring is walked: where it stands, what ran into it and what took it up.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A handover is not always a joint, and that is a fact about the shape rather than a shortcoming
    /// here.</b> A ring turns only where the shape does (<see cref="BandShell"/>), so a mouth's edge that
    /// runs on into the kerb beyond it along one line is one piece and not two — the rounding joins
    /// consecutive stretches of one circle into the one stretch they are
    /// (<see cref="Spline.JoinedInto"/>) — and the place the road takes over is somewhere along it. So every
    /// piece is read from end to end (<see cref="Along"/>) and not only at its two ends, and every stretch
    /// this hands back follows one road or none.
    /// </para>
    /// <para>
    /// <b>The joints are read from the two pieces that meet at them</b> and never from a piece as a whole,
    /// for the same reason: a straight kerb is one piece for every road it is straight through, a hundred
    /// metres of it in a town laid on a grid, and its middle is a place neither of its joints is about.
    /// </para>
    /// </remarks>
    static void Changes(Reading reading, ArcSeg[] ring, List<(Vector2 AtM, int Left, int Taken)> changes)
    {
        changes.Clear();

        var left = reading.At(ring[^1], LastM(ring[^1]));
        foreach (ref readonly var piece in ring.AsSpan())
        {
            var taken = reading.At(piece, FirstM(piece));
            if (reading.Road(left) != reading.Road(taken)) changes.Add((piece.StartM, left, taken));

            left = Along(reading, piece, taken, changes);
        }
    }

    /// <summary>
    /// <b>Every place along one piece where what it runs along changes hands</b>, in the order it is walked:
    /// the reading taken every <see cref="StepM"/> from the piece's first end to its last, and each step
    /// that answers differently from the one before it cut down to the place it changed at
    /// (<see cref="Splits"/>). Hands back what its far end reads, which is what the next piece is weighed
    /// against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every change and not the first.</b> One piece of a town's outline runs along a street, then past
    /// the mouth it opens into, then along the next street, and each of those is a road giving the boundary
    /// up or taking it — so a piece read only at its two ends hands back one place for however many it has,
    /// and hands it back named after the road at the far end of the piece rather than the one that took over
    /// there.
    /// </para>
    /// <para>
    /// <b>Each is named by what stood either side of it</b>, which is the whole of what a change is. The two
    /// readings a step apart bracket it, so the road giving up and the road taking over are both in hand
    /// before the place between them is looked for — and <see cref="Marks"/> can file the place under the
    /// road whose end it is.
    /// </para>
    /// </remarks>
    static int Along(
        Reading reading, in ArcSeg piece, int taken, List<(Vector2 AtM, int Left, int Taken)> changes)
    {
        var lastM = LastM(piece);
        var left = taken;
        var leftM = FirstM(piece);
        while (leftM < lastM)
        {
            var atM = MathF.Min(leftM + StepM, lastM);
            var now = reading.At(piece, atM);
            if (reading.Road(now) != reading.Road(left))
            {
                changes.Add((piece.PointAtM(Splits(reading, piece, left, leftM, atM)), left, now));
            }

            left = now;
            leftM = atM;
        }

        return left;
    }

    /// <summary>
    /// <b>Where a piece is read from, at each of its two ends</b>: a little way inside it
    /// (<see cref="LineTolerance.OnePlaceM"/>, or a quarter of a short piece). A joint is two ends standing
    /// within a weld of each other, so a reading taken at the end itself is a reading of whichever of them
    /// the arithmetic rounded towards.
    /// </summary>
    static float FirstM(in ArcSeg piece) =>
        MathF.Min(LineTolerance.OnePlaceM, MathF.Abs(piece.LengthM) * 0.25f);

    /// <inheritdoc cref="FirstM"/>
    static float LastM(in ArcSeg piece) => piece.LengthM - FirstM(piece);

    /// <summary>
    /// <b>Where along one step of a piece the road under it changes hands</b>: the place found by halving,
    /// which is the only way to ask a question whose answer is a reading rather than a curve.
    /// </summary>
    /// <remarks>
    /// <b>Inside a step and never over the whole piece</b> (<see cref="Along"/>). Halving bounds a place
    /// only where the answer changes once between the two ends it is given, and a piece of a town's outline
    /// answers as many things as it runs along — so what is halved is the one step that was seen to change
    /// hands, and the first place it changes at is the place it changes at. <b>Ten halvings</b>, which puts
    /// the answer inside a millimetre of a step.
    /// </remarks>
    static float Splits(Reading reading, in ArcSeg piece, int left, float fromM, float toM)
    {
        var road = reading.Road(left);
        for (var step = 0; step < Halvings; step++)
        {
            var midM = (fromM + toM) * 0.5f;
            if (reading.Road(reading.At(piece, midM)) == road) fromM = midM;
            else toM = midM;
        }

        return toM;
    }

    /// <summary>
    /// The changes of one ring filed as the ends they are — one mark each, named by the road whose kerb
    /// ends there and the box it ends at.
    /// </summary>
    /// <remarks>
    /// <b>A change is one road's end and not two.</b> Where a round follows, the road running into it is
    /// the one that ends; where a round is what ran in, the road taking the boundary up is; and where one
    /// kerb takes up another with no round between them, the change is filed under the road giving it up,
    /// the other having its own end at its own other kerb.
    /// </remarks>
    static void Marks(Reading reading, List<(Vector2 AtM, int Left, int Taken)> changes, List<Mark> into)
    {
        for (var at = 0; at < changes.Count; at++)
        {
            var change = changes[at];
            var left = reading.Road(change.Left);
            var taken = reading.Road(change.Taken);

            // <b>A road that takes the boundary straight back from itself has not ended</b>
            // (<see cref="StepsRound"/>): what the outline did there was step round something standing inside
            // the road, and a kerb is still a kerb the far side of it.
            if (StepsRound(reading, changes, at)) continue;

            // <b>A park is no end of its street</b>: what a walk does where a car park stands off a kerb is carry
            // on round it, so neither the boundary leaving the kerb for the rank nor the round it turns into the
            // rank on is a place the walk stops (GEN-53).
            if (left == Park || taken == Park) continue;
            if (RoundsIntoAPark(reading, changes, at)) continue;

            var lane = change.Left != None ? change.Left : change.Taken;

            // <b>A roundabout's ring is not a street</b> (GEN-19): nothing fronts onto it and nothing
            // crosses it, so the boundary leaving it is a corner of the circle rather than the end of a
            // walk. <b>Its arms are ordinary</b> and keep the ends they have at the same box (WLK-2).
            if (reading.Circulates(change.Left) || reading.Circulates(change.Taken)) continue;

            // <b>A box that does not fork is not a road's end to a walker</b> (GEN-5a): a bend and a dead
            // end carry the traffic straight through, so the kerb turning there is a corner the pavement
            // turns with rather than a place the walk stops and is crossed.
            if (reading.WhereNothingForks(lane, change.AtM)) continue;

            into.Add(
                new Mark(
                    change.AtM, lane, reading.Road(lane), reading.JunctionOf(lane, change.AtM),
                    reading.OutM(lane, change.AtM)));
        }
    }

    /// <summary>
    /// <b>Whether one change is the boundary stepping round something standing inside a road</b> rather than
    /// that road's kerb ending: the road gives the outline up and the next change hands it straight back,
    /// with nothing but no road between the two.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A road ends at a box and the box is on the far side of the change.</b> Where a street really stops,
    /// what takes the outline up is the mouth it opens into and what picks it up after that is the next
    /// street — so the pair around a mouth names two roads. A pair that names one road twice is the outline
    /// going round and coming back: the rounding filling a notch along a road's own kerb (TER-3c.10) is
    /// the shape that does it, and what stands there is a kerb with a curve in it rather than a place a walk
    /// stops and is crossed. Left as ends, the two of them stand as far out along their road as the bend is
    /// from the box — tens of metres — and being the furthest they carry the paint (<see cref="Ranked"/>).
    /// </para>
    /// <para>
    /// <b>Both of the pair go, and it is the same answer read from either of them</b>, so the walk is cut
    /// nowhere along that kerb rather than at one of the two places it did not end at. <b>It says nothing
    /// about a dead end or a cul-de-sac head</b>, where the outline turns round the end of the road it came
    /// in on: those are a box that does not fork and are refused as one (GEN-5a).
    /// </para>
    /// </remarks>
    static bool StepsRound(Reading reading, List<(Vector2 AtM, int Left, int Taken)> changes, int at)
    {
        if (changes.Count < 2) return false;

        var change = changes[at];
        if (reading.Road(change.Taken) == None)
        {
            var next = changes[(at + 1) % changes.Count];
            return reading.Road(next.Taken) == reading.Road(change.Left);
        }

        if (reading.Road(change.Left) == None)
        {
            var last = changes[((at - 1) % changes.Count + changes.Count) % changes.Count];
            return reading.Road(last.Left) == reading.Road(change.Taken);
        }

        return false;
    }

    /// <summary>
    /// <b>Whether one change is a kerb turning off into a car park's rank</b>, or back out of one: the round it
    /// hands the boundary to, or took it from, runs on into a bay rather than into the next street (GEN-53).
    /// </summary>
    static bool RoundsIntoAPark(Reading reading, List<(Vector2 AtM, int Left, int Taken)> changes, int at)
    {
        var change = changes[at];
        if (reading.Road(change.Taken) == None)
        {
            return reading.Road(changes[(at + 1) % changes.Count].Taken) == Park;
        }

        if (reading.Road(change.Left) == None)
        {
            return reading.Road(changes[((at - 1) % changes.Count + changes.Count) % changes.Count].Left) == Park;
        }

        return false;
    }

    /// <summary>
    /// One caller's working set for reading what a place on the boundary follows: the lines it is weighed
    /// against, and the room the index needs to answer.
    /// </summary>
    sealed class Reading(
        LaneLines lanes, CityPlan.RoadArrays roads, CityPlan.JunctionArrays junctions, int[] arms,
        bool[] circulating, ChainIndex index, float reachM)
    {
        readonly int[] _near = new int[index.ChainCount];
        readonly float[] _alongM = new float[index.ChainCount];

        /// <summary>
        /// <b>This reading's own working set over the index</b> (<see cref="ChainIndex.NewScan"/>). The
        /// index is the paving's and is shared — a town lays it once and everything that asks about a place
        /// asks the same one — while the scan belongs to one query at a time, so a reading that took the
        /// index's own would answer whatever another thread's query had left in it.
        /// </summary>
        readonly ChainIndex.Scan _scan = index.NewScan();

        public LaneLines Lanes => lanes;

        public CityPlan.RoadArrays Roads => roads;

        public int At(in ArcSeg piece, float atM) =>
            LaneAlong(lanes, index, _scan, _near, _alongM, reachM, piece, atM);

        /// <summary>What a lane this reading answered is, as the three answers a handover is weighed in.</summary>
        public int Road(int lane) => RoadOf(lanes, roads, lane);

        /// <inheritdoc cref="OutM(LaneLines, CityPlan.JunctionArrays, int, Vector2)"/>
        public float OutM(int lane, Vector2 pointM) => KerbEnds.OutM(lanes, junctions, lane, pointM);

        /// <inheritdoc cref="KerbEnds.JunctionOf"/>
        public int JunctionOf(int lane, Vector2 pointM) => KerbEnds.JunctionOf(lanes, lane, pointM);

        /// <summary>Whether a box is the one a road runs <em>to</em>, which is which of its two ends this is.</summary>
        public bool AtTheFarEnd(int road, int junction) => roads.ToJunction[road] == junction;

        /// <summary>
        /// <b>Whether a lane is one way round a roundabout's own ring</b> (GEN-19): a carriageway with no
        /// frontage on either hand, which stands no pedestrian node and carries no paint (WLK-2).
        /// </summary>
        public bool Circulates(int lane) =>
            lane != None && circulating[lanes.LaneRoad[lane]];

        /// <summary>
        /// <b>Whether one end of a kerb stands at a box that does not fork</b> — a bend or a dead end
        /// rather than a junction (GEN-5a), where the traffic has nowhere to turn across a walk and a walk
        /// has nothing to stop for.
        /// </summary>
        public bool WhereNothingForks(int lane, Vector2 pointM)
        {
            var junction = JunctionOf(lane, pointM);
            return junction != None && arms[junction] < ForkedArms;
        }
    }

    /// <summary>
    /// <b>How far out along its road one place stands from the middle of the box</b>, for a reader asking
    /// about the stretch under their pointer (OBS-2t): the figure that decides which end of a round is the
    /// further of the two, so that a reader asking why one end was called further can read both and see.
    /// </summary>
    public static float OutM(Paving paving, int lane, Vector2 pointM) =>
        OutM(paving.Lanes, paving.Of.Junctions, lane, pointM);

    /// <summary>
    /// <b>How far out of the junction one place stands, measured along the road it is the kerb of</b>: the
    /// run from the middle of the box to the place, taken along the way the road points where it meets that
    /// box.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>From the box's middle and not from the end of the road's own line</b>, which is the one measure
    /// that cannot separate the two ends of a round. A lane runs between two boxes and stops at the mouth
    /// of each, so a kerb gives up the boundary within a hair of where its own line stops: asked how far
    /// back along the lane each end stands, both answer nought, the comparison ties, and which end is
    /// called the further one falls to whichever way the ring happened to be walked.
    /// </para>
    /// <para>
    /// <b>Along the road and not away from the box</b> (the run projected onto the road's own direction):
    /// the two roads at a corner are generally different widths, so a straight distance from the middle
    /// answers the wider road's half-width as much as anything about where its kerb stops.
    /// </para>
    /// </remarks>
    static float OutM(LaneLines lanes, CityPlan.JunctionArrays junctions, int lane, Vector2 pointM)
    {
        if (lane == None) return 0f;

        var nearer = NearerEnd(lanes, lane, pointM);
        var junction = nearer ? lanes.LaneFromJunction[lane] : lanes.LaneToJunction[lane];
        var along = Spline.SampleAt(lanes.ArcsOf(lane), nearer ? 0f : lanes.LaneLengthM[lane]);
        return MathF.Abs(Vector2.Dot(pointM - junctions.CentreM[junction], along.Direction));
    }

    /// <summary>
    /// The box one end of a road's kerb belongs to: the end of its lane the place stands nearer, a lane
    /// running from one box to the next so that either end of it is one.
    /// </summary>
    static int JunctionOf(LaneLines lanes, int lane, Vector2 pointM) =>
        lane == None ? None
            : NearerEnd(lanes, lane, pointM) ? lanes.LaneFromJunction[lane] : lanes.LaneToJunction[lane];

    /// <summary>Whether a place stands nearer the start of a lane than its end.</summary>
    static bool NearerEnd(LaneLines lanes, int lane, Vector2 pointM)
    {
        var lengthM = lanes.LaneLengthM[lane];
        return Spline.ProjectM(lanes.ArcsOf(lane), pointM, lengthM * 0.5f, lengthM) * 2f <= lengthM;
    }

    /// <summary>
    /// <b>Which roads are a roundabout's own circulating carriageway</b> (GEN-19) — the ones no kerb end is
    /// placed on, their arms' ends at the same boxes standing as they always did (WLK-2).
    /// </summary>
    static bool[] Circulating(in GroundPieces ground)
    {
        var circulating = new bool[ground.Roads.Count];
        foreach (var ring in ground.Roundabouts.Road) circulating[ring] = true;

        return circulating;
    }

    /// <summary>
    /// <b>As far off a line as a lane's own edge can stand</b>, which bounds what could be a kerb at all and
    /// is what the index is asked over.
    /// </summary>
    static float ReachM(LaneLines lanes)
    {
        var reachM = 0f;
        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            reachM = MathF.Max(reachM, lanes.LaneWidthM[lane] * 0.5f);
        }

        return reachM;
    }

    /// <summary>
    /// The lane one place on one piece of boundary runs along the edge of, or <see cref="None"/> where it
    /// runs along none of them: the line near that place it stands nearest half a width off
    /// (<see cref="OffTheLineM"/>), square to and parallel with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One place and not the piece</b>, because a piece is not one road's: the rounding joins
    /// consecutive stretches of one circle, so a straight kerb is one piece for every road it is straight
    /// through, and a reading is only ever about where it was taken.
    /// </para>
    /// <para>
    /// <b>The nearest of what answers and not the first of them</b>. The order the index hands its lines
    /// back in is the grid's (<see cref="ChainIndex.Near"/>), so a place two lines could both be the edge of
    /// — a bay's mouth laid over the street's kerb, two arms leaving one box at a hair of an angle —
    /// would otherwise answer whichever cell was walked first, and a kerb would change hands where nothing
    /// about the town changed. Settled on the line the place stands nearest the edge of, and on the lower
    /// line where two are the same distance, for the reason <see cref="ChainIndex.Measure"/> settles its own
    /// tie: the answer is the shape's and not the lattice's.
    /// </para>
    /// </remarks>
    static int LaneAlong(
        LaneLines lanes, ChainIndex index, ChainIndex.Scan scan, Span<int> near, Span<float> alongM,
        float reachM, in ArcSeg piece, float atM)
    {
        var pointM = piece.PointAtM(atM);
        var headingRad = piece.HeadingAtRad(atM);

        // Truncated to the room the caller gave, which is what the index hands back a count past
        // (<see cref="ChainIndex.Near"/>): read past it and the answer is whatever the stack held.
        var found = Math.Min(index.Near(scan, pointM, reachM, near, alongM), near.Length);

        var best = None;
        var bestOffM = float.MaxValue;
        for (var slot = 0; slot < found; slot++)
        {
            // The boundary is the ground's (<see cref="Paving.Perimeter"/>), so a lane above it is the edge of none
            // of it — the ground runs on under a bridge's first metres along that lane's own edge.
            var line = near[slot];
            if (line >= lanes.LaneCount || lanes.LaneLevel[line] != CityPlan.RoadArrays.Ground) continue;

            var on = Spline.SampleAt(lanes.ArcsOf(line), alongM[slot]);
            var offM = pointM - on.PositionM;
            var offTheLineM = MathF.Abs(offM.Length() - (lanes.LaneWidthM[line] * 0.5f));
            if (offTheLineM > OffTheLineM) continue;
            if (best != None && (offTheLineM > bestOffM || (offTheLineM == bestOffM && line > best))) continue;

            // Square to the line, which is what the projection landing inside it rather than on an end means
            // — a band's square end stands off a line's end and would otherwise read as its edge.
            if (MathF.Abs(Vector2.Dot(offM, on.Direction)) > LineTolerance.JoinedM) continue;

            var turnRad = MathF.Abs(Spline.WrapRad(headingRad - on.HeadingRad));
            if (turnRad > LineTolerance.StraightOnRad && MathF.PI - turnRad > LineTolerance.StraightOnRad)
            {
                continue;
            }

            best = line;
            bestOffM = offTheLineM;
        }

        return best;
    }
}

/// <summary>
/// <b>One place a walk meets one road</b>: the two points, one on each side of the carriageway and both at
/// the one station along the road, which cut of which road they are, and whether the walk crosses there.
/// </summary>
/// <remarks>
/// <para>
/// <b>The road and the cut travel with the points</b> because what is laid between them belongs to an arm —
/// the paint a walk crosses on is filed by road end (<c>World.Road.Crossings</c>, TER-6), and a reader
/// handed points alone would have to find the arm again by looking.
/// </para>
/// <para>
/// <b><see cref="Painted"/> is what tells a station from a kerb end</b> (WLK-10a): a place the walk crosses
/// carries a band of paint, and the traffic is held a setback clear of that band's near edge; a place it
/// does not is the end of the road's kerb, standing no paint at all, and the traffic is held a setback clear
/// of the place itself.
/// </para>
/// </remarks>
internal readonly record struct KerbNodes(
    int Road, KerbCut Cut, Vector2 NearM, Vector2 FarM, bool Painted);

/// <summary>
/// <b>Which cut of a road one station is</b>: the one at the end the road was drawn from, the one at the end
/// it runs to, or the single cut midway that a road too short to be crossed twice carries in place of both
/// (<see cref="Core.Config.RoadFigures.CrossedOnceBelowM"/>, WLK-10a).
/// </summary>
internal enum KerbCut
{
    AtTheNearEnd,
    AtTheFarEnd,

    /// <summary>Between the two ends and belonging to neither — the traffic meets it whichever way it drives.</summary>
    Midway,
}
