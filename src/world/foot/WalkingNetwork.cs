using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Routing;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.World.Foot;

/// <summary>
/// The walking side of the global tier: <b>the fine foot graph contracted so that a node is a place the
/// footway splits</b>, and a link is the whole run of pavement or the one crossing between two of them
/// It is deliberately the same shape as the driving side, because <em>which way to go</em>
/// is one question and it is asked of one search.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both ends of a walk are plural</b>, which is what a two-way pavement means: a body on a stretch may
/// set off either way, and its destination may be reached by either of the two links covering the stretch
/// it stands on. All of them go to the search, which is the only place they can be compared — choosing
/// between them outside it sent a walker four times round a dead end's head rather than over the zebra a
/// stride away.
/// </para>
/// <para>
/// <b>Every stretch has a lane each way, and the offset is one figure for the whole stretch.</b> The
/// ground cuts it back, never the plan: whether a body stands at that offset on both hands is asked at
/// every station along the edge and the largest offset that holds <em>everywhere on it</em> is kept. The
/// version that gave way to the ground per station moved the line's distance from the kerb every few
/// strides and made a walk down a street weave.
/// </para>
/// </remarks>
internal sealed class WalkingNetwork
{
    /// <summary>Two stretches with no turn between them, which is every pair that does not meet at a node.</summary>
    public const int NoTurn = -1;

    /// <summary>How finely the ladder of offsets is stepped when the ground is asked what a stretch has room for.</summary>
    const int OffsetRungs = 20;

    readonly FootGraph _foot;
    readonly int[] _linkOfEdge;
    readonly int[] _slotOfEdge;
    readonly float[] _laneOffsetM;
    readonly Lanes _lanes;
    readonly Joins _joins;

    WalkingNetwork(
        FootGraph foot, RunNetwork runs, int[] linkOfEdge, int[] slotOfEdge, float[] laneOffsetM, Lanes lanes,
        Joins joins)
    {
        _foot = foot;
        Runs = runs;
        _linkOfEdge = linkOfEdge;
        _slotOfEdge = slotOfEdge;
        _laneOffsetM = laneOffsetM;
        _lanes = lanes;
        _joins = joins;
    }

    public FootGraph Foot => _foot;

    public RunNetwork Runs { get; }

    public TravelGraph Graph => Runs.Graph;

    /// <summary>The run a stretch is part of. Every stretch belongs to exactly one, which is what a contraction may not lose.</summary>
    public int LinkOfEdge(int edge) => _linkOfEdge[edge];

    /// <summary>Where in that run the stretch stands, as an index into its pieces.</summary>
    public int SlotOfEdge(int edge) => _slotOfEdge[edge];

    /// <summary>How many mitres the town holds, which is what numbers the corners apart from the lanes.</summary>
    public int TurnCount => _joins.ToEdge.Length;

    /// <summary>
    /// <b>The length of the line a walker going this way is actually held on</b>, which is not the
    /// stretch's own: a lane is offset a quarter of the band, so it is longer than the kerb outside a bend
    /// and shorter inside one. Everything measured along a lane — a place on it, a stretch of it somebody
    /// has taken — is in these metres.
    /// </summary>
    public float LaneLengthM(int edge) => _lanes.LengthM[edge];

    /// <summary>
    /// How much of that is the corner on the end of it, and nought where the lane carries none — which is
    /// what a reader converting a place on the <em>stretch</em> into a place on the lane leaves out, since
    /// the stretch's own ground stops where the corner starts.
    /// </summary>
    public float TailLengthM(int edge) => _lanes.TailM[edge];

    /// <summary>
    /// The turn this lane <b>carries rather than hands over at</b>, or <see cref="NoTurn"/> where it carries
    /// none. It is the corner of a stretch that arrives at a node offering exactly one way on: nothing is
    /// chosen there, so the corner is a bend in the lane like any other and not a piece in its own right.
    /// </summary>
    public int TailOf(int edge) => _joins.TailSlot[edge];

    /// <summary>
    /// How far to the walker's own right this stretch's line is laid, whichever way along it the walker is
    /// going — so the two directions are two lines half a band apart and nobody shares a line with somebody
    /// coming the other way. Zero where the ground has no room for a lane at all, which is honest.
    /// </summary>
    public float LaneOffsetM(int edge) => _laneOffsetM[edge >> 1];

    /// <summary>
    /// <b>The line a walker going this way down this stretch is actually held on</b>: the stretch's own
    /// curve moved to the lane its own side asks for, <b>with the corner off the end of it where that
    /// corner is not a choice</b> (<see cref="TailOf"/>).
    /// </summary>
    /// <remarks>
    /// The graph carries the pavement's centreline and the offset is this network's, so a reader that took
    /// the graph's chain would be drawing a lane nobody walks. Laid once with the town rather than offset
    /// into a caller's span: every caller wanted the same line, and the span each brought had a bound on it
    /// past which a long stretch came back empty.
    /// </remarks>
    public ReadOnlySpan<ArcSeg> LaneOf(int edge) => _lanes.Of(edge);

    /// <summary>The stretches a walker on this one may leave for, at the node this one arrives at.</summary>
    public ReadOnlySpan<int> TurnsFrom(int edge) =>
        _joins.ToEdge.AsSpan(_joins.TurnOffsets[edge], _joins.TurnOffsets[edge + 1] - _joins.TurnOffsets[edge]);

    /// <summary>Where the <paramref name="turn"/>th way out of a stretch stands in the town's own turn table.</summary>
    public int TurnSlotAt(int edge, int turn) => _joins.TurnOffsets[edge] + turn;

    /// <summary>
    /// The stretch a mitre leads onto. <b>A mitre is the arriving lane's ground</b>, so its width and its
    /// offset are that lane's, exactly as a junction's join takes the width of the lane it arrives on.
    /// </summary>
    public int TurnToEdge(int slot) => _joins.ToEdge[slot];

    /// <summary>
    /// Where a pair of stretches stands in it, or <see cref="NoTurn"/> where they do not meet. <b>A corner
    /// a lane carries is still one of these</b>: where it lands on the next lane is the same question
    /// whoever walked it, and only whether it has to be laid down separately differs
    /// (<see cref="TailOf"/>).
    /// </summary>
    public int TurnSlot(int fromEdge, int toEdge)
    {
        for (var slot = _joins.TurnOffsets[fromEdge]; slot < _joins.TurnOffsets[fromEdge + 1]; slot++)
        {
            if (_joins.ToEdge[slot] == toEdge) return slot;
        }

        return NoTurn;
    }

    /// <summary>
    /// <b>The mitre where two lanes meet at a node</b>, laid once with the town: the corner a walker
    /// turns between the lane it arrives on and the lane it leaves on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The two lanes do not meet on their own and never did.</b> Both stretches end at the node they
    /// share, but each is laid a quarter of its own band to its own right, so where they turn at that
    /// node one lane's end stands a lane offset off the next one's start — <b>0.84 m to 1.02 m on the
    /// shipped maps, and 1.34 m to 1.39 m wherever a crossing meets a pavement</b>, which is two right
    /// angles' worth of offset. Left un-mitred it is a walk that steps sideways at every node and a
    /// picture of a zebra whose lane stops in the middle of the pavement.
    /// </para>
    /// <para>
    /// <b>A corner is rounded over the ground the junction takes at either end of it</b> (<see cref="Margins"/>),
    /// because a walker turns before it steps and a right angle laid as a point is a standstill.
    /// </para>
    /// <para>
    /// <b>The mitre belongs to the stretch it leads onto</b> (<see cref="WalkedLine"/>), a crossing
    /// included: the question a walker asks the road is then asked from the near side of the corner rather
    /// than from inside it, which at a kerb is the pavement rather than the paint.
    /// </para>
    /// <para>
    /// <b>A stretch whose lane arrives at the very point the next one sets off from gets no join at
    /// all.</b> A plan splits a pavement wherever a zebra or a lot meets it, and where both sides of such
    /// a split are one line at one offset a join would be 4.6 m of that line laid again, at the price of
    /// the points it costs a walk to hold. <b>Running straight on is not enough to skip it</b>: two
    /// stretches the ground let keep different offsets stand a few centimetres apart on a dead straight
    /// pavement, and a walk handed that step end to end kinks across it twice.
    /// </para>
    /// </remarks>
    public ReadOnlySpan<ArcSeg> JoinArcs(int slot) =>
        _joins.Arcs.AsSpan(_joins.ArcOffsets[slot], _joins.ArcOffsets[slot + 1] - _joins.ArcOffsets[slot]);

    /// <summary>How far short of the arriving lane's end the mitre leaves it.</summary>
    public float JoinFromM(int slot) => _joins.FromM[slot];

    /// <summary>And how far into the lane it leaves for the mitre arrives.</summary>
    public float JoinToM(int slot) => _joins.ToM[slot];

    public float JoinLengthM(int slot) => _joins.LengthM[slot];

    /// <summary>
    /// <b>The span of this stretch's lane any walk covers</b>: from where the first mitre onto it rejoins,
    /// to where the last mitre off it hands over. Outside that span the ground is under a mitre or under
    /// nothing at all.
    /// </summary>
    /// <remarks>
    /// The corners at a stretch's two ends take their ground before the lane is stationed
    /// (<see cref="WalkedLine"/>), so the last stretch of a lane into a node and the first of the lane out
    /// of it are under a mitre and under no lane at all (<see cref="Margins"/>). A reader that draws
    /// the whole lane and the mitres beside it therefore draws that ground twice and leaves a spur running
    /// past every corner into the node — <b>which is a picture of a walk nobody walks</b>, and is why the
    /// span is answered here rather than worked out by whoever is drawing.
    /// </remarks>
    public float WalkedFromM(int edge) => _joins.LaneFromM[edge];

    public float WalkedToM(int edge) => _joins.LaneToM[edge];

    /// <summary>How far into its own run a place on a stretch stands.</summary>
    public float PlaceOfM(int edge, float alongEdgeM) =>
        Runs.PlaceOfM(_linkOfEdge[edge], _slotOfEdge[edge], alongEdgeM);

    /// <summary>
    /// Where a walker under way joins the network: <b>the link it is already on</b>, priced at what is
    /// left of it. The stretch already covered is spent, and charging for it again lets a route that turns
    /// round at the next node look cheaper than carrying on.
    /// </summary>
    public RouteEntry EntryOnEdge(int edge, float alongEdgeM)
    {
        var link = _linkOfEdge[edge];
        var alongM = PlaceOfM(edge, alongEdgeM);
        return new RouteEntry(link, alongM, Runs.LengthM(link) - alongM);
    }

    /// <summary>
    /// Where a walker standing still joins it: <b>both ways along the stretch it stands on</b>. A walker
    /// has no lane of its own to be pointing along, so unlike a driver it is offered both and the search
    /// settles it.
    /// </summary>
    public int EntriesNear(Vector2 pointM, Span<RouteEntry> into)
    {
        var edge = _foot.NearestEdge(pointM, out var alongM);
        if (edge < 0) return 0;

        var count = 0;
        into[count++] = EntryOnEdge(edge, alongM);

        var back = _foot.Reverse(edge);
        if (back >= 0 && into.Length > 1)
        {
            into[count++] = EntryOnEdge(back, MathF.Max(0f, _foot.LengthM(back) - alongM));
        }

        return count;
    }

    /// <summary>A destination as the search takes it: a place on a link, offered on both links that cover the stretch it stands on.</summary>
    public int GoalsAt(Vector2 pointM, Span<RouteGoal> into)
    {
        var edge = _foot.NearestEdge(pointM, out var alongM);
        if (edge < 0) return 0;

        var count = 0;
        into[count++] = new RouteGoal(_linkOfEdge[edge], PlaceOfM(edge, alongM));

        var back = _foot.Reverse(edge);
        if (back >= 0 && into.Length > 1)
        {
            var backM = MathF.Max(0f, _foot.LengthM(back) - alongM);
            into[count++] = new RouteGoal(_linkOfEdge[back], PlaceOfM(back, backM));
        }

        return count;
    }

    public static WalkingNetwork Build(FootGraph foot, TerrainGrid terrain, SimConfig config)
    {
        var runs = RunNetwork.Contract(foot, default(Pricer));

        var linkOfEdge = new int[foot.EdgeCount];
        var slotOfEdge = new int[foot.EdgeCount];
        Array.Fill(linkOfEdge, TravelGraph.NoLink);
        for (var link = 0; link < runs.LinkCount; link++)
        {
            var edges = runs.PiecesOf(link);
            for (var slot = 0; slot < edges.Length; slot++)
            {
                linkOfEdge[edges[slot]] = link;
                slotOfEdge[edges[slot]] = slot;
            }
        }

        var laneOffsetM = LaneOffsets(foot, terrain, config);
        var offset = LayLanes(foot, laneOffsetM);
        var joins = LayJoins(foot, offset, config);
        return new WalkingNetwork(foot, runs, linkOfEdge, slotOfEdge, laneOffsetM, Carrying(foot, offset, joins), joins);
    }

    /// <summary>
    /// Every lane in the town, laid once: the arcs, how long each line is and how much of that length is a
    /// corner the lane carries rather than ground of its own stretch.
    /// </summary>
    readonly record struct Lanes(int[] ArcOffsets, ArcSeg[] Arcs, float[] LengthM, float[] TailM)
    {
        public ReadOnlySpan<ArcSeg> Of(int edge) =>
            Arcs.AsSpan(ArcOffsets[edge], ArcOffsets[edge + 1] - ArcOffsets[edge]);
    }

    /// <summary>
    /// Every mitre in the town, drawn once: the arcs, how far into the two lanes each was taken, what that
    /// leaves of every lane (<see cref="LaneFromM"/>, <see cref="LaneToM"/>, per stretch) and which of a
    /// stretch's turns its own lane carries (<see cref="TailSlot"/>, per stretch; the rest per turn).
    /// </summary>
    readonly record struct Joins(
        int[] TurnOffsets, int[] ToEdge, int[] ArcOffsets, ArcSeg[] Arcs, float[] FromM, float[] ToM, float[] LengthM,
        float[] LaneFromM, float[] LaneToM, int[] TailSlot)
    {
        public ReadOnlySpan<ArcSeg> ArcsOf(int slot) =>
            Arcs.AsSpan(ArcOffsets[slot], ArcOffsets[slot + 1] - ArcOffsets[slot]);
    }

    /// <summary>The two lanes of every stretch, at the offset the ground allowed it, before any corner is folded into one.</summary>
    static Lanes LayLanes(FootGraph foot, float[] laneOffsetM)
    {
        var arcOffsets = new int[foot.EdgeCount + 1];
        for (var edge = 0; edge < foot.EdgeCount; edge++)
        {
            arcOffsets[edge + 1] = arcOffsets[edge] + foot.ArcsOf(edge).Length;
        }

        var arcs = new ArcSeg[arcOffsets[foot.EdgeCount]];
        var lengthM = new float[foot.EdgeCount];
        for (var edge = 0; edge < foot.EdgeCount; edge++)
        {
            var into = arcs.AsSpan(arcOffsets[edge], arcOffsets[edge + 1] - arcOffsets[edge]);
            Spline.OffsetInto(foot.ArcsOf(edge), laneOffsetM[edge >> 1], into);
            lengthM[edge] = Spline.TotalLengthM(into);
        }

        return new Lanes(arcOffsets, arcs, lengthM, new float[foot.EdgeCount]);
    }

    /// <summary>
    /// The same lanes with each one's own corner folded into it: the stretch to where the corner leaves it,
    /// and then the corner. <b>A stretch that arrives at a node offering one way on is one line round that
    /// corner and not two pieces meeting at it</b> — nothing is decided there, the ground is the stretch's
    /// own bend, and a piece in its own right is a piece every walk down it has to be handed.
    /// </summary>
    static Lanes Carrying(FootGraph foot, Lanes offset, Joins joins)
    {
        var most = 0;
        for (var edge = 0; edge < foot.EdgeCount; edge++) most = Math.Max(most, offset.Of(edge).Length);

        var cut = new ArcSeg[most];
        var arcOffsets = new int[foot.EdgeCount + 1];
        var arcs = new List<ArcSeg>(offset.Arcs.Length);
        var lengthM = new float[foot.EdgeCount];
        var tailM = new float[foot.EdgeCount];

        for (var edge = 0; edge < foot.EdgeCount; edge++)
        {
            var lane = offset.Of(edge);
            var tail = joins.TailSlot[edge];
            if (tail == NoTurn)
            {
                foreach (var arc in lane) arcs.Add(arc);
                lengthM[edge] = offset.LengthM[edge];
            }
            else
            {
                var written = Spline.SubChainInto(lane, 0f, offset.LengthM[edge] - joins.FromM[tail], cut);
                for (var arc = 0; arc < written; arc++) arcs.Add(cut[arc]);
                foreach (var arc in joins.ArcsOf(tail)) arcs.Add(arc);

                tailM[edge] = joins.LengthM[tail];
                lengthM[edge] = joins.LaneToM[edge];
            }

            arcOffsets[edge + 1] = arcs.Count;
        }

        return new Lanes(arcOffsets, [.. arcs], lengthM, tailM);
    }

    /// <summary>
    /// Below this a turn is no turn: the stretch runs straight on through a node a zebra or a lot put in
    /// it, and there is no corner to round. A hundredth of a radian is half a degree.
    /// </summary>
    const float StraightOnRad = 0.01f;

    /// <summary>
    /// And below this the two lanes are already the same point, so there is nothing at all to lay. It is
    /// a centimetre rather than the graph's quarter-metre weld: a node welded onto another leaves a step
    /// of a few centimetres between two lanes that are otherwise one line, and a step is still a step.
    /// </summary>
    const float SamePlaceM = 0.01f;

    static Joins LayJoins(FootGraph foot, Lanes offset, SimConfig config)
    {
        var drawn = new ArcSeg[2];

        var turnOffsets = new int[foot.EdgeCount + 1];
        for (var edge = 0; edge < foot.EdgeCount; edge++)
        {
            turnOffsets[edge + 1] = turnOffsets[edge] + foot.EdgesOut(foot.ToNode(edge)).Length;
        }

        var toEdge = new int[turnOffsets[foot.EdgeCount]];
        var arcOffsets = new int[toEdge.Length + 1];
        var arcs = new List<ArcSeg>();
        var fromM = new float[toEdge.Length];
        var intoM = new float[toEdge.Length];
        var lengthM = new float[toEdge.Length];

        // What every corner at one end of a stretch gives up of it — one figure per end and not one per
        // turn, so a stretch hands over at a place and not at a place per way off it.
        var takenAtTheStartM = Margins(foot, offset, config, leaving: false);
        var takenAtTheEndM = Margins(foot, offset, config, leaving: true);

        var laneFromM = new float[foot.EdgeCount];
        var laneToM = new float[foot.EdgeCount];
        var tailSlot = new int[foot.EdgeCount];
        Array.Fill(tailSlot, NoTurn);

        for (var edge = 0; edge < foot.EdgeCount; edge++)
        {
            var lane = offset.Of(edge);
            var laneM = offset.LengthM[edge];
            var slot = turnOffsets[edge];

            // The one way on, where there is only one: the corner to it is nobody's choice, so the lane
            // carries it rather than handing over at it.
            var carries = NoTurn;
            var ways = 0;

            foreach (var onto in foot.EdgesOut(foot.ToNode(edge)))
            {
                toEdge[slot] = onto;
                var onward = offset.Of(onto);

                var backM = takenAtTheEndM[edge];
                var forwardM = takenAtTheStartM[onto];
                var laid = 0;
                if (lane.Length > 0 && onward.Length > 0 && !SamePlaceAt(lane, laneM - backM, onward, forwardM))
                {
                    laid = Corner(lane, laneM - backM, onward, forwardM, config.WalkerTightestTurnM, drawn);

                    // The two poses defeated the construction. The straight between the two hand-over
                    // points still closes the corner and still stands on the band, so it is what is laid —
                    // <b>between the same two points</b>, because what a stretch gives up at an end is the
                    // end's and not this turn's.
                    if (laid == 0) laid = StraightBetween(lane, laneM - backM, onward, forwardM, drawn);
                }

                for (var arc = 0; arc < laid; arc++)
                {
                    arcs.Add(drawn[arc]);
                    lengthM[slot] += drawn[arc].LengthM;
                }

                fromM[slot] = laid == 0 ? 0f : backM;
                intoM[slot] = laid == 0 ? 0f : forwardM;
                arcOffsets[slot + 1] = arcs.Count;

                if (onto != foot.Reverse(edge))
                {
                    ways++;
                    if (laid > 0 && OffThePaint(foot, edge, onto)) carries = slot;
                }

                slot++;
            }

            laneFromM[edge] = takenAtTheStartM[edge];
            tailSlot[edge] = ways == 1 ? carries : NoTurn;
            laneToM[edge] = tailSlot[edge] == NoTurn
                ? laneM - takenAtTheEndM[edge]
                : laneM - fromM[tailSlot[edge]] + lengthM[tailSlot[edge]];
        }

        return new Joins(
            turnOffsets, toEdge, arcOffsets, [.. arcs], fromM, intoM, lengthM, laneFromM, laneToM, tailSlot);
    }

    /// <summary>
    /// What every corner at one end of each stretch gives up of its lane: <b>one figure per end</b>, and
    /// it is <b>the size of the junction that end stands in</b> — half the band of the widest stretch
    /// running across it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A stretch ends where the ground stops being its own</b>, exactly as a lane is cut back from a
    /// junction disc on the road side. Where a zebra meets a pavement the two bands overlap over a whole
    /// pavement's width: the crossing's own edge is laid from one pavement's line to the other's, so half
    /// a band at each end of it is pavement, and drawn whole it is a zebra lying over the junction it
    /// arrives at. Cut by the box, the crossing's lane is the paint and the pavement's stops at the
    /// zebra's mouth.
    /// </para>
    /// <para>
    /// <b>It is a fact about an end and not about a turn</b> — the same rule the road side settled on for
    /// a lane end (`TER-5d`). Per turn, a stretch handed over at as many places as it had ways off it, and
    /// what the picture showed at a kerb was four points where the walk has one.
    /// </para>
    /// <para>
    /// <b>A way that runs straight on adds no box</b>, because two stretches of one line do not cross:
    /// the overlap of their bands is the whole of both, and cutting either back for it would take ground
    /// nothing is coming through. What such a way is given is the room to round the step between them.
    /// </para>
    /// <para>
    /// Bounded by half of each of the two lanes a corner stands between, so two corners a stride apart
    /// share the ground rather than overrunning one another, and a stretch with no way off an end keeps
    /// the whole of it. Turning round on the spot is not a way off: it decides nothing about how far a
    /// lane is walked, and counted it pulled a lane's start back behind the corner landing bodies on it.
    /// </para>
    /// </remarks>
    static float[] Margins(FootGraph foot, Lanes offset, SimConfig config, bool leaving)
    {
        var takenM = new float[foot.EdgeCount];
        for (var edge = 0; edge < foot.EdgeCount; edge++)
        {
            var atM = float.PositiveInfinity;
            var boxM = 0f;
            var anythingToLay = false;
            var walked = leaving ? edge : foot.Reverse(edge);
            foreach (var onto in foot.EdgesOut(foot.ToNode(walked)))
            {
                if (onto == foot.Reverse(walked)) continue;

                var other = leaving ? onto : foot.Reverse(onto);
                atM = MathF.Min(atM, MathF.Min(offset.LengthM[edge], offset.LengthM[other]) * 0.5f);

                // Where every way through this end is already one line with the lane, the end gives up
                // nothing: a corner there would be the same line laid again, at the price of the points it
                // costs a walk to hold. One way through that is not makes the end a place, and then even
                // the ways that were one line hand over at it.
                var from = leaving ? edge : other;
                var to = leaving ? other : edge;
                if (SamePlace(offset.Of(from), offset.LengthM[from], offset.Of(to))) continue;

                anythingToLay = true;
                if (!RunsStraightOn(offset.Of(from), offset.LengthM[from], offset.Of(to)))
                {
                    boxM = MathF.Max(boxM, foot.BandM(other) * 0.5f);
                }
            }

            // Never less than the turn itself needs, or a step between two lanes of one line has no room
            // to be rounded over.
            var marginM = MathF.Max(2f * config.WalkerTightestTurnM, boxM);
            takenM[edge] = float.IsFinite(atM) && anythingToLay ? MathF.Min(marginM, atM) : 0f;
        }

        return takenM;
    }

    /// <summary>
    /// Whether a lane may carry this corner: <b>neither end of it is a crossing</b>. A corner onto the
    /// paint belongs to the crossing it leads onto and not to the pavement it leaves — that is what has a
    /// walker ask the road from the near side of the kerb rather than from inside the turn
    /// (<see cref="WalkedLine"/>) — so a lane carrying one would be a body already on the zebra when it
    /// asked whether it could step onto it.
    /// </summary>
    static bool OffThePaint(FootGraph foot, int edge, int onto) =>
        foot.KindOf(edge) != FootEdgeKind.Crossing && foot.KindOf(onto) != FootEdgeKind.Crossing;

    /// <summary>
    /// The arcs from one lane's pose to the next one's, <b>or nothing where the two defeat the
    /// construction</b> and the caller has to fall back on the straight.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An equal-tangent biarc gives two arcs of its own choosing, and two of the answers it gives are no
    /// use to a walker. <b>It runs away</b> where the two poses are nearly parallel and offset sideways:
    /// a six-centimetre step between two stretches of one straight pavement drew a 26 m loop. And
    /// <b>it hairpins</b> where they are not: a third of the corners in a town came out tighter than the
    /// tightest circle the feet can hold, some of them a centimetre across, and a walker handed one of
    /// those orbits it rather than reaching the point on the far side — measured, on the day the mitre
    /// was wired in, as Odesa's given-up walks going from 37 a minute to 211.
    /// </para>
    /// <para>
    /// So a corner has to be both: <b>no longer than twice the span it bridges</b>, since a corner is at
    /// most half a turn, and <b>no tighter than the feet can hold</b>.
    /// </para>
    /// </remarks>
    static int Corner(
        ReadOnlySpan<ArcSeg> lane, float leavesAtM, ReadOnlySpan<ArcSeg> onward, float joinsAtM, float tightestM,
        Span<ArcSeg> into)
    {
        var leaves = Spline.SampleAt(lane, leavesAtM);
        var joins = Spline.SampleAt(onward, joinsAtM);
        var laid = Spline.BiarcInto(leaves.PositionM, leaves.HeadingRad, joins.PositionM, joins.HeadingRad, into);
        if (laid == 0) return 0;

        var lengthM = 0f;
        var bend = 0f;
        for (var arc = 0; arc < laid; arc++)
        {
            lengthM += into[arc].LengthM;
            bend = MathF.Max(bend, MathF.Abs(into[arc].Curvature));
        }

        if (bend > 1e-6f && 1f / bend < tightestM) return 0;

        return lengthM <= 2f * (joins.PositionM - leaves.PositionM).Length() + SamePlaceM ? laid : 0;
    }

    /// <summary>The plain line between the two hand-over points — a corner cut rather than turned, and the last resort.</summary>
    static int StraightBetween(
        ReadOnlySpan<ArcSeg> lane, float leavesAtM, ReadOnlySpan<ArcSeg> onward, float joinsAtM, Span<ArcSeg> into)
    {
        var fromM = Spline.SampleAt(lane, leavesAtM).PositionM;
        var run = Spline.SampleAt(onward, joinsAtM).PositionM - fromM;
        var lengthM = run.Length();
        if (lengthM < SamePlaceM) return 0;

        into[0] = new ArcSeg(fromM, MathF.Atan2(run.Y, run.X), lengthM, 0f);
        return 1;
    }

    /// <summary>Whether the lane heads on the way the next one sets off, which is what a split in a straight run looks like.</summary>
    static bool RunsStraightOn(ReadOnlySpan<ArcSeg> lane, float laneLengthM, ReadOnlySpan<ArcSeg> onward) =>
        MathF.Abs(Spline.WrapRad(Spline.SampleAt(onward, 0f).HeadingRad - Spline.SampleAt(lane, laneLengthM).HeadingRad))
        < StraightOnRad;

    /// <summary>And whether it arrives at the very point the next one sets off from, in which case there is nothing to lay.</summary>
    static bool SamePlace(ReadOnlySpan<ArcSeg> lane, float laneLengthM, ReadOnlySpan<ArcSeg> onward) =>
        SamePlaceAt(lane, laneLengthM, onward, 0f);

    /// <summary>The same question asked of the two points the corner would actually run between.</summary>
    static bool SamePlaceAt(ReadOnlySpan<ArcSeg> lane, float leavesAtM, ReadOnlySpan<ArcSeg> onward, float joinsAtM) =>
        MathF.Abs(
            Spline.WrapRad(
                Spline.SampleAt(onward, joinsAtM).HeadingRad - Spline.SampleAt(lane, leavesAtM).HeadingRad))
        < StraightOnRad
        && (Spline.SampleAt(onward, joinsAtM).PositionM - Spline.SampleAt(lane, leavesAtM).PositionM).Length()
        < SamePlaceM;

    /// <summary>
    /// What each stretch has room for: the largest offset at which <b>a body stands clear on both hands at
    /// every station along it</b>, in both directions, since the two lanes of one stretch are one figure
    /// mirrored. Asked of the ground once when the town is laid, and never again.
    /// </summary>
    static float[] LaneOffsets(FootGraph foot, TerrainGrid terrain, SimConfig config)
    {
        var fullM = config.WalkingLaneOffsetM;
        var bodyM = config.PersonDiameterM * 0.5f;

        // The stations are the walked line's own sampling rule and not the cells': a cell is a metre and
        // the four curves this asks about clip its corners, so a station a cell apart walks past the
        // corner of a carriageway that a body's shoulder is already over.
        var stationM = config.Network.SplineToleranceWalkedM;
        var keptM = new float[foot.EdgeCount / 2];

        for (var edge = 0; edge < foot.EdgeCount; edge += 2)
        {
            var arcs = foot.ArcsOf(edge);
            var lengthM = foot.LengthM(edge);
            var stations = Math.Max(1, (int)MathF.Ceiling(lengthM / stationM));
            var edgeM = fullM;

            for (var station = 0; station <= stations && edgeM > 0f; station++)
            {
                var at = Spline.SampleAt(arcs, lengthM * station / stations);
                while (edgeM > 0f && !Clear(terrain, at, edgeM, bodyM)) edgeM -= fullM / OffsetRungs;
            }

            keptM[edge >> 1] = MathF.Max(0f, edgeM);
        }

        return keptM;
    }

    /// <summary>Whether a body walking either lane of this stretch stands on walkable ground on both hands.</summary>
    static bool Clear(TerrainGrid terrain, SplineSample at, float offsetM, float bodyM)
    {
        foreach (var acrossM in (ReadOnlySpan<float>)
                 [offsetM - bodyM, offsetM + bodyM, -offsetM - bodyM, -offsetM + bodyM])
        {
            if (!terrain.At(at.PositionM + at.Right * acrossM).Walkable) return false;
        }

        return true;
    }

    /// <summary>
    /// <b>A walker's turn costs nothing the network can price.</b> All three turn prices are a driver's:
    /// a gap in the oncoming stream, a junction occupancy, a manoeuvre with a reverse in it. A walker
    /// turns where it stands, so what a corner costs it is the ground the corner's own margin takes —
    /// which is in the line the local tier lays and not in a preference between routes.
    /// </summary>
    readonly struct Pricer : IEdgeTurnPricer
    {
        public float PriceM(int fromEdge, int toEdge) => 0f;
    }
}
