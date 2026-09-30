using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Foot;

/// <summary>
/// <b>The junction each end of a town's zebras is cut into the walk as, and the ways over the paint between
/// them</b> (WLK-15): a place standing between the two lanes of the pavement, square off the crossing it
/// belongs to, <b>which every lane at it is connected through</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>A junction hands over at a point per connected lane</b> (TER-5d), and six lanes meet at this one: the
/// walk arriving and the walk leaving on each of the pavement's two lanes, and the crossing's two lanes over
/// the road. <b>So each lane of pavement is cut twice</b> — into the walk up to the junction, the stretch of
/// it inside the junction, and the walk on past — and the crossing's two lanes hand over on the driven
/// ground's own boundary, where their paint stops (TER-7b). <b>Every arrival is connected to every
/// departure but the one that would turn a walk back down the lane beside it</b> (WLK-13).
/// </para>
/// <para>
/// <b>Its centre stands between the two lanes of the pavement</b>, half the walk's own width off the kerb on
/// the line the crossing runs along, and the connection points stand
/// <see cref="RoadFigures.FootConnectorAlongM"/> either side of it along each lane's own course. It is what
/// makes the place a place rather than a point on one line: a walker arriving on either lane may set off
/// over the road and one arriving off the road may take either lane, so <b>a pavement's two lanes are joined
/// across the carriageway</b> — out over the paint and back over it — rather than by a loop from one to the
/// other on the same side, which is a walker turning round.
/// </para>
/// <para>
/// <b>The walk keeps its own geometry and is parted rather than laid again.</b> A course is the whole town's
/// boundary moved off itself once (WLK-1, <see cref="PavementLanes"/>), and what a junction wants of it is
/// somewhere to hand over — so the line is cut at those places and every metre of it stays exactly where the
/// move put it, the stretch between two cuts being the junction's own. It is the same injection a junction
/// is cut into a street with (<c>CityGen.CutJunctions</c>, GEN-52): the road that was there is parted, keeps
/// its line, and what is new is the arms and the nodes between the pieces.
/// </para>
/// <para>
/// <b>The paint stops at the kerb</b>: a crossing's own stretch runs from the boundary on one side to the
/// boundary on the other, which is the ground the stripes cover, so what a walker's secondary claims hold of
/// the road (TER-5c.1) is the carriageway and nothing else. What runs between the boundary and the walk is one of the
/// junction's own connections, and it is pavement.
/// </para>
/// <para>
/// <b>Where a point falls on a line is that line's answer</b> (WLK-9): the boundary passes near the place the
/// paint ends rather than through it, and a course passes near the place the junction stands rather than
/// through it, so each point is moved onto the line it belongs to and no line is ever fitted to a point.
/// <b>A line answering from further off than <see cref="RoadFigures.CrossingMeetsTheWalkWithinM"/> is not
/// this crossing's</b> — it is the walk across the road or round the block — and nothing is run to it.
/// <b>What that costs is read at two different sizes.</b> A mouth is the paint's own end and stands on the
/// boundary, so a crossing whose boundary answers from too far off is not laid at all. A hand-over point is a
/// place on one lane's course, and a course rounded away from a tight corner leaves that lane out of this
/// junction and nothing else: the paint is laid, the place stands, and the lanes that did answer are
/// connected to it exactly as they would have been.
/// </para>
/// </remarks>
internal sealed class CrossingWays
{
    /// <summary>A town whose walk crosses nothing, which is what a town with no paint on it hands over.</summary>
    public static readonly CrossingWays None = new([], []);

    readonly Walked[] _ways;
    readonly float[][][] _partedM;

    CrossingWays(Walked[] ways, float[][][] partedM, int refused = 0)
    {
        _ways = ways;
        _partedM = partedM;
        Refused = refused;
    }

    /// <summary>
    /// <b>How many zebras found no kerb to stop at</b>
    /// (<see cref="RoadFigures.CrossingMeetsTheWalkWithinM"/>): the paint runs from the boundary to the
    /// boundary, so an end answering from further off than that is a crossing with nowhere to begin and is
    /// not laid, both its lanes and both its ends.
    /// </summary>
    public int Refused { get; }

    /// <summary>
    /// <b>And how many lanes of the walk a junction that stands was not reached from</b>
    /// (<see cref="RoadFigures.CrossingMeetsTheWalkWithinM"/>): the reading the figure is weighed by, since
    /// what it refuses is a connection laid across whatever stands between the place and another street's
    /// pavement. <b>It costs that lane its connections at this place and nothing else</b> — the place stands,
    /// the paint is laid, and the lanes that answered are connected as they were.
    /// </summary>
    public int Unreached { get; private set; }

    /// <summary>
    /// And where one of them stands, which is what a reader goes and looks at — the last junction a lane
    /// was missing at, or the origin where none was.
    /// </summary>
    public Vector2 UnreachedAtM { get; private set; }

    /// <summary>
    /// <b>And how far off that junction the lane's own course really stood</b>, which is what says whether
    /// the figure was a stride too short or the walk was round the block.
    /// </summary>
    public float UnreachedOffM { get; private set; }

    /// <summary>
    /// <b>And how many connections no curve would join</b> (WLK-14): a movement whose two poses ask a walker
    /// to set off behind where they arrived is one the place does not offer, so it is not laid — and what
    /// that costs a town is a reading rather than a rough line through a junction. <b>So is one no curve joins
    /// on the walk</b> (WLK-15): a connection out over the verge is not laid either.
    /// </summary>
    public int Unjoined { get; private set; }

    /// <summary>How many junctions the town's crossings stand at, after the merge (WLK-3).</summary>
    public int Junctions { get; private set; }

    /// <summary>
    /// And how many mouths that merge took: the ones standing at a junction another was already standing at,
    /// which is what says whether the figure is doing anything.
    /// </summary>
    public int Merged { get; private set; }

    /// <summary>
    /// <b>One stretch of one crossing</b>: the paint over the road, or one of the junction's own connections
    /// between the places it hands over at. <b>Its two ends are connection points</b>, so a reader that wants
    /// where a crossing hands over wants these.
    /// </summary>
    /// <remarks>
    /// <b>A line and not a pair of points, because a connection turns.</b> The paint is the straight over the
    /// carriageway, but a connection leaves the lane it sets off from along that lane's own heading and joins
    /// the one it arrives on along <em>its</em> heading (<see cref="Reading.OntoThePaint"/>, WLK-14) — as a
    /// junction's connectors do on the road side (TER-5d). <b>The walk straight through
    /// the junction on one lane is not among them</b>: that is the stretch of course between two cuts, and
    /// the cut is the whole of what lays it.
    /// </remarks>
    public readonly record struct Walked(ArcSeg[] Arcs, FootEdgeKind Kind, float BandM)
    {
        /// <summary>The place it sets off from, which is one of the junction's connection points.</summary>
        public Vector2 FromM => Arcs[0].StartM;

        /// <summary>And the place it arrives at, which is another.</summary>
        public Vector2 OntoM => Arcs[^1].EndM;
    }

    /// <summary>The stretches every crossing in the town is walked on, both lanes and both junctions of each.</summary>
    public ReadOnlySpan<Walked> Ways => _ways;

    /// <summary>
    /// <b>How far along one ring of one lane of the pavement a junction parts it</b> — in order, and never
    /// twice at one place. A ring nothing crosses answers with nothing and is laid exactly as it was.
    /// </summary>
    public ReadOnlySpan<float> PartedOn(int lane, int ring) =>
        lane >= _partedM.Length || ring >= _partedM[lane].Length ? [] : _partedM[lane][ring];

    /// <summary>
    /// <b>The junctions the paint a town's own kerb ends ask for wants</b> (WLK-10,
    /// <see cref="KerbEnds.CrossedM"/>) — where the walk crosses, which is not always where its traffic is
    /// held (WLK-10a).
    /// </summary>
    public static CrossingWays Of(CityPlan plan, PavementLanes pavement, SimConfig config) =>
        Of(plan, pavement, Crossings.Lay(plan, config, plan.Paving(config).RoadEnds(config).CrossedM), config);

    /// <summary>
    /// The same, off zebras somebody has already laid — <b>which is how a town keeps one set of them</b>:
    /// what the walk is cut at is what the road is crossed by, and a second laying is a second set.
    /// </summary>
    public static CrossingWays Of(
        CityPlan plan, PavementLanes pavement, Crossings crossings, SimConfig config)
    {
        var paving = plan.Paving(config);
        return Lay(
            pavement, KerbLines.Of(paving.Perimeter(config), config), paving.Rings(config).WalkSides(config),
            crossings, config);
    }

    /// <summary>
    /// Lays every crossing's junctions and ways and files the places they part the walk at. Build-time only —
    /// it allocates freely and indexes each course once.
    /// </summary>
    /// <param name="boundary">
    /// The driven ground's own outline (TER-7b), which is where the paint stops and the pavement begins.
    /// </param>
    /// <param name="walk">
    /// And the walk's outer face as the ground answers it (<see cref="GroundRings.WalkSides"/>), which is where the
    /// pavement ends and no connection goes past.
    /// </param>
    public static CrossingWays Lay(
        PavementLanes pavement, KerbLines boundary, RingSides walk, Crossings crossings, SimConfig config)
    {
        if (pavement.Count == 0 || crossings.Count == 0) return None;

        var courses = new KerbLines[pavement.Count];
        var parting = new List<float>[pavement.Count][];
        for (var lane = 0; lane < pavement.Count; lane++)
        {
            var rings = pavement.RingsOf(lane);
            courses[lane] = KerbLines.Of(rings, pavement.LooseOf(lane), config);
            parting[lane] = new List<float>[rings.Length];
            for (var ring = 0; ring < rings.Length; ring++) parting[lane][ring] = [];
        }

        var reading = new Reading(pavement, courses, boundary, walk, config);
        var refused = 0;

        // <b>The mouths first and the junctions after</b>: two to a crossing, each the place its paint stops
        // at one end, and it is where they stand that says how many junctions a town has (WLK-3).
        var mouths = new Mouth[crossings.Count * 2];
        var stands = new bool[mouths.Length];
        for (var at = 0; at < crossings.Count; at++)
        {
            if (crossings.DepthM[at] <= 0f || crossings.SpanM[at] <= 0f) continue;

            // Both ends before either of them: a crossing whose far mouth stands nowhere is a way over the
            // road to nowhere, so the near one is not laid either.
            if (!reading.Mouth(crossings, at, TheNearEnd, out mouths[At(at, TheNearEnd)])
                || !reading.Mouth(crossings, at, TheFarEnd, out mouths[At(at, TheFarEnd)]))
            {
                refused++;
                continue;
            }

            stands[At(at, TheNearEnd)] = true;
            stands[At(at, TheFarEnd)] = true;
        }

        var places = Places(mouths, stands, config.Road.FootNodeMergeM);
        var unreached = 0;
        var unreachedAtM = Vector2.Zero;
        var unreachedOffM = 0f;
        foreach (var place in places)
        {
            var missing = reading.HandsOver(place, mouths, out var offM);
            unreached += missing;
            if (missing == 0) continue;

            unreachedAtM = place.CentreM;
            unreachedOffM = offM;
        }

        // The paint, laid once the places are settled: what a lane of the walk could not be reached from is
        // that lane's connections and never the crossing.
        var ways = new List<Walked>();
        var laid = new bool[mouths.Length];
        for (var at = 0; at < crossings.Count; at++)
        {
            var near = At(at, TheNearEnd);
            var far = At(at, TheFarEnd);
            if (!stands[near] || !stands[far]) continue;

            // <b>The paint is what its connections join</b>, so it is drawn before them: the heading a
            // walker steps off the kerb on is this line's own and not the way the band was struck.
            var one = Straight(mouths[near].SetsOffM, mouths[far].ArrivesM);
            var other = Straight(mouths[far].SetsOffM, mouths[near].ArrivesM);
            if (one.LengthM <= 0f || other.LengthM <= 0f)
            {
                refused++;
                continue;
            }

            var bandM = crossings.DepthM[at] * 0.5f;
            ways.Add(new Walked([one], FootEdgeKind.Crossing, bandM));
            ways.Add(new Walked([other], FootEdgeKind.Crossing, bandM));
            mouths[near] = mouths[near] with { SetsOffRad = one.HeadingRad, ArrivesRad = other.HeadingRad };
            mouths[far] = mouths[far] with { SetsOffRad = other.HeadingRad, ArrivesRad = one.HeadingRad };
            laid[near] = true;
            laid[far] = true;
        }

        var unjoined = 0;
        var merged = 0;
        foreach (var place in places)
        {
            unjoined += Through(ways, place, mouths, laid, reading, config.WalkingLaneWidthM);
            File(parting, place);
            merged += place.Mouths.Count - 1;
        }

        return new CrossingWays([.. ways], Sorted(parting, config.Network.FootGraphNodeWeldM), refused)
        {
            Unjoined = unjoined,
            Unreached = unreached,
            UnreachedAtM = unreachedAtM,
            UnreachedOffM = unreachedOffM,
            Junctions = places.Count,
            Merged = merged,
        };
    }

    /// <summary>The two ends of a crossing, which are the two mouths its paint runs between.</summary>
    const int TheNearEnd = 0;

    /// <inheritdoc cref="TheNearEnd"/>
    const int TheFarEnd = 1;

    /// <summary>Where one end of one crossing stands in the town's own list of mouths.</summary>
    static int At(int crossing, int end) => (crossing * 2) + end;

    /// <summary>
    /// One place a junction hands over at: where it stands, <b>which way the walk runs there</b> — so a
    /// connection can leave along the lane it sets off from and arrive along the one it joins (WLK-14) — and
    /// where along which ring the cut that made it was taken.
    /// </summary>
    readonly record struct Meeting(int Ring, float AlongM, Vector2 AtM, float HeadingRad);

    /// <summary>
    /// <b>One end of one crossing</b>: the point on the boundary its paint sets off from, the point it
    /// arrives back at, which way each of those runs, where the mouth stands, and the junction it stands at.
    /// </summary>
    /// <remarks>
    /// <b>The place is not the mouth</b> (WLK-3): two mouths standing within a merge of one another are one
    /// junction, so a corner where two streets are crossed hands both crossings over at the one place.
    /// </remarks>
    readonly record struct Mouth(
        Vector2 CentreM, Vector2 SetsOffM, Vector2 ArrivesM, float SetsOffRad, float ArrivesRad, Place Place);

    /// <summary>
    /// <b>One pedestrian junction</b>: the mouths merged into it, where it stands, and the places the walk
    /// hands over at on either side of it on each lane of the pavement (WLK-15).
    /// </summary>
    /// <remarks>
    /// <b>Arriving and leaving and not near and far</b>: a pavement's two lanes are walked opposite ways
    /// (WLK-8), so which of a lane's two cuts a walker reaches first is that lane's own answer and is read
    /// off the way its course is wound.
    /// </remarks>
    sealed class Place
    {
        public readonly List<int> Mouths = [];

        public Vector2 CentreM { get; set; }

        public Meeting[] Arriving { get; set; } = [];

        public Meeting[] Leaving { get; set; } = [];

        /// <summary>
        /// Which lanes of the walk this place was reached from, which are the ones it hands over on
        /// (<see cref="RoadFigures.CrossingMeetsTheWalkWithinM"/>). A lane whose course answered from too far
        /// off is left out of this junction and of nothing else.
        /// </summary>
        public bool[] Reaches { get; set; } = [];
    }

    /// <summary>
    /// <b>The junctions a town's crossings stand at</b> (WLK-3): one per mouth, but for mouths standing
    /// within <paramref name="mergeM"/> of one another, which are one place — and transitively, so a run of
    /// near neighbours is one junction and nothing has to decide which of them it is.
    /// </summary>
    /// <remarks>
    /// <b>What a merge saves is the walk between them.</b> Two crossings at one corner left a stretch of
    /// pavement a stride long standing between two junctions, with a connection at each end of it and a
    /// walker arriving at one place only to hand over at the next; merged, the lanes of both crossings meet
    /// at the one place and connect to each other directly.
    /// </remarks>
    static List<Place> Places(Mouth[] mouths, bool[] stands, float mergeM)
    {
        var of = new int[mouths.Length];
        for (var at = 0; at < of.Length; at++) of[at] = at;

        for (var at = 0; at < mouths.Length; at++)
        {
            if (!stands[at]) continue;

            for (var other = 0; other < at; other++)
            {
                if (!stands[other]) continue;
                if (Vector2.Distance(mouths[at].CentreM, mouths[other].CentreM) > mergeM) continue;

                of[Root(of, at)] = Root(of, other);
            }
        }

        var places = new Dictionary<int, Place>();
        for (var at = 0; at < mouths.Length; at++)
        {
            if (!stands[at]) continue;

            var root = Root(of, at);
            if (!places.TryGetValue(root, out var place)) places[root] = place = new Place();

            place.Mouths.Add(at);
            mouths[at] = mouths[at] with { Place = place };
        }

        // Midway between the mouths merged into it (WLK-3), which is a mouth's own place where nothing
        // merged with it.
        foreach (var place in places.Values)
        {
            var centreM = Vector2.Zero;
            foreach (var mouth in place.Mouths) centreM += mouths[mouth].CentreM;

            place.CentreM = centreM / place.Mouths.Count;
        }

        return [.. places.Values];
    }

    static int Root(int[] of, int at)
    {
        while (of[at] != at)
        {
            of[at] = of[of[at]];
            at = of[at];
        }

        return at;
    }

    /// <summary>
    /// <b>Everything one junction is connected through</b>: the walk arriving on each lane onto every
    /// crossing at the place, every crossing onto the walk leaving on each lane, and — where more than one
    /// crossing was merged into it — each of them onto the others.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The walk straight through the junction on one lane is not laid here</b>: the cut has already left
    /// it standing as the stretch of course between that lane's two connection points, which is the ground
    /// the junction owns of that lane and is exactly where the move put it.
    /// </para>
    /// <para>
    /// <b>And neither is the way from one lane of a pavement to the other, nor the way back over a crossing
    /// just walked</b> (WLK-13). A pavement's two lanes are walked opposite ways (WLK-8), so a walker taking
    /// the first would arrive and at once retrace the lane beside the one they came in on, and the second is
    /// the same walker on the same paint — both are turning round rather than turns. <b>What joins a
    /// pavement's two lanes is the crossing</b>: out over the paint and back over it.
    /// </para>
    /// </remarks>
    static int Through(
        List<Walked> ways, Place place, Mouth[] mouths, bool[] laid, Reading reading, float walkM)
    {
        var owned = new Owned?[place.Arriving.Length];
        for (var lane = 0; lane < owned.Length; lane++)
        {
            if (!place.Reaches[lane]) continue;

            owned[lane] = reading.StretchOf(lane, place.Arriving[lane], place.Leaving[lane], place.CentreM);
        }

        var unjoined = 0;
        foreach (var at in place.Mouths)
        {
            if (!laid[at]) continue;

            var mouth = mouths[at];
            for (var lane = 0; lane < owned.Length; lane++)
            {
                if (!place.Reaches[lane]) continue;

                if (owned[lane] is not { } stretch)
                {
                    unjoined += 2;
                    continue;
                }

                unjoined += Laid(ways, reading.OntoThePaint(stretch.Walked, mouth.SetsOffM, mouth.SetsOffRad), walkM);

                // Off the paint is onto it walked backwards: down the lane's course from where it hands over
                // leaving, and turned round.
                var off = reading.OntoThePaint(
                    stretch.Back, mouth.ArrivesM, Spline.WrapRad(mouth.ArrivesRad + MathF.PI));
                unjoined += Laid(ways, off is null ? null : Reversed(off), walkM);
            }

            // And from this crossing onto every other at the same place, which is how a walker takes the
            // second of a corner's two zebras without a stretch of pavement between them.
            foreach (var other in place.Mouths)
            {
                if (other == at || !laid[other]) continue;

                var joined = reading.Joined(
                    mouth.ArrivesM, mouth.ArrivesRad, mouths[other].SetsOffM, mouths[other].SetsOffRad, owned);
                unjoined += Laid(ways, joined, walkM);
            }
        }

        return unjoined;
    }

    /// <summary>A connection kept, or counted as one no turn would join (WLK-14).</summary>
    static int Laid(List<Walked> ways, ArcSeg[]? line, float walkM)
    {
        if (line is null) return 1;

        ways.Add(new Walked(line, FootEdgeKind.Pavement, walkM));
        return 0;
    }

    /// <summary>
    /// <b>The stretch of one lane's course a junction owns</b>, both ways: in the order the lane is walked —
    /// from the point it hands over at arriving to the one it hands over at leaving — and back. <b>Every
    /// connection between that lane and the paint runs down it</b> as far as it leaves it (WLK-16).
    /// </summary>
    readonly record struct Owned(ArcSeg[] Walked, ArcSeg[] Back);

    /// <summary>
    /// How many times the metre a turn leaves a course from is closed in on once a step has bracketed it — a
    /// centimetre halved this often is under a micrometre.
    /// </summary>
    const int Halvings = 16;

    static ArcSeg[] Reversed(ArcSeg[] arcs)
    {
        var reversed = new ArcSeg[arcs.Length];
        Spline.ReverseInto(arcs, reversed);
        return reversed;
    }

    /// <summary>Room for the line a connection is drawn as: a straight, the turn and a straight (<see cref="Reading.Joined"/>).</summary>
    const int MostArcsInAConnection = 3;

    /// <summary>The plain line between two places, which is what a crossing's paint is.</summary>
    static ArcSeg Straight(Vector2 fromM, Vector2 ontoM)
    {
        var run = ontoM - fromM;
        return new ArcSeg(fromM, MathF.Atan2(run.Y, run.X), run.Length(), 0f);
    }

    static void File(List<float>[][] parting, Place place)
    {
        for (var lane = 0; lane < place.Arriving.Length; lane++)
        {
            // A lane this place does not hand over on is a lane it does not cut: the walk runs past it
            // exactly as the move laid it.
            if (!place.Reaches[lane]) continue;

            parting[lane][place.Arriving[lane].Ring].Add(place.Arriving[lane].AlongM);
            parting[lane][place.Leaving[lane].Ring].Add(place.Leaving[lane].AlongM);
        }
    }

    /// <summary>
    /// The lines one caller reads a junction off: the pavement it is cut into, the boundary the paint stops
    /// at, the walk's outer face its connections keep inside, and the figures that place it.
    /// </summary>
    sealed class Reading(
        PavementLanes pavement, KerbLines[] courses, KerbLines boundary, RingSides walk, SimConfig config)
    {
        /// <summary>
        /// <b>One end of one crossing as the mouth it is</b> (WLK-15), or false where the paint stops nowhere
        /// the town has a kerb: the two points on the boundary its lanes hand over at, and the place half a
        /// pavement out from the kerb that they stand at.
        /// </summary>
        /// <remarks>
        /// <b>Each reading is taken against the line the next one is struck from</b>: the place the paint
        /// stops is a point of the crossing's own and is dropped onto the boundary, and the mouth stands half
        /// the walk's width off <em>that</em>, square to the kerb.
        /// </remarks>
        public bool Mouth(Crossings crossings, int at, int end, out Mouth mouth)
        {
            mouth = default;

            var across = Heading.RightOf(crossings.Axis[at]);
            var outward = end == TheNearEnd ? -across : across;
            var paintM = crossings.CentreM[at] + (outward * (crossings.SpanM[at] * 0.5f));

            // The two lanes of the crossing, each in the middle of its own half of the paint and on the side
            // of the pair its walker keeps (WLK-8, TER-4a): at this end one of them sets off and the other
            // arrives, so the mouth has a point on the boundary for each.
            var asideM = Heading.RightOf(across) * (crossings.DepthM[at] * 0.25f * config.RoadSideSign);
            if (!OnTheKerb(paintM + asideM, out var one) || !OnTheKerb(paintM - asideM, out var other))
            {
                return false;
            }

            // Where it stands: half a pavement out from the kerb on the line the crossing runs along, which
            // is between the two lanes of the walk by construction (SimConfig.WalkingLaneAtM).
            if (!OnTheKerb(paintM, out var kerbM)) return false;

            var centreM = kerbM + (outward * (config.PavementWidthM * 0.5f));
            mouth = end == TheNearEnd
                ? new Mouth(centreM, one, other, 0f, 0f, null!)
                : new Mouth(centreM, other, one, 0f, 0f, null!);
            return true;
        }

        /// <summary>
        /// <b>Where one junction hands over on each lane of the walk</b>: a setback beyond the outermost of
        /// the mouths merged into it, at either end, along that lane's own course (WLK-15, WLK-3) — and how
        /// many lanes it was not reached from, which is what it answers. <b>A lane the walk is not there to
        /// be handed over to on is left out of this place and out of nothing else</b>: a course answering
        /// from further off than <see cref="RoadFigures.CrossingMeetsTheWalkWithinM"/> is another street's,
        /// and one the move could not close is walked by nobody (<see cref="PavementLanes.LooseOf"/>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Beyond the outermost mouth and not a setback from the middle</b>: a merged place stands between
        /// its mouths, so points struck off its centre would fall level with them, and a connection onto
        /// paint standing beside its own hand-over point is a corner no curve holds (WLK-14). Taken from each
        /// mouth and the furthest kept, every connection has the ground it would have had unmerged.
        /// </para>
        /// <para>
        /// <b>And a lane is reached only where every mouth of the place answered on it</b>, which is what
        /// keeps the setback outside all of them: kept from the mouths that did answer, the point could stand
        /// level with one that did not.
        /// </para>
        /// </remarks>
        public int HandsOver(Place place, Mouth[] mouths, out float offM)
        {
            var arriving = new Meeting[courses.Length];
            var leaving = new Meeting[courses.Length];
            var reaches = new bool[courses.Length];
            var unreached = 0;
            offM = 0f;
            for (var lane = 0; lane < courses.Length; lane++)
            {
                // Which of a lane's two cuts a walk reaches first is which way that lane is walked (WLK-8).
                var setbackM = config.Road.FootConnectorAlongM;
                var withTheRing = pavement.RunsWithTheRing(lane);
                var reachedM = -1f;
                var leftM = -1f;
                reaches[lane] = true;
                foreach (var at in place.Mouths)
                {
                    var standsAtM = mouths[at].CentreM;
                    var arrived = HandsOver(lane, standsAtM, withTheRing ? -setbackM : setbackM, out var arrives, out var arrivesOffM);
                    var leftOff = HandsOver(lane, standsAtM, withTheRing ? setbackM : -setbackM, out var left, out var leftOffM);
                    if (!arrived || !leftOff)
                    {
                        reaches[lane] = false;
                        unreached++;
                        offM = MathF.Max(offM, MathF.Min(arrivesOffM, leftOffM));
                        break;
                    }

                    Furthest(place.CentreM, arrives, ref arriving[lane], ref reachedM);
                    Furthest(place.CentreM, left, ref leaving[lane], ref leftM);
                }
            }

            place.Arriving = arriving;
            place.Leaving = leaving;
            place.Reaches = reaches;
            return unreached;
        }

        /// <summary>
        /// Keeps the hand-over standing furthest out from the middle of the place, which on a run of merged
        /// mouths is the one outside all of them.
        /// </summary>
        static void Furthest(Vector2 centreM, in Meeting met, ref Meeting kept, ref float offM)
        {
            var standsM = Vector2.Distance(met.AtM, centreM);
            if (standsM <= offM) return;

            (kept, offM) = (met, standsM);
        }

        /// <summary>
        /// <b>The stretch of one lane's course between the two points a junction hands over at on it</b>, walked
        /// the way the lane is — the stretch running nearer the junction, a ring having two — or null where the
        /// two stand on different lines of the course.
        /// </summary>
        public Owned? StretchOf(int lane, in Meeting arriving, in Meeting leaving, Vector2 nearM)
        {
            var arcs = courses[lane].Between(
                new KerbLines.Station(arriving.Ring, arriving.AlongM),
                new KerbLines.Station(leaving.Ring, leaving.AlongM),
                nearM);
            if (arcs.Length == 0) return null;

            // Cut the way the ring is wound, from whichever end that puts first — and a lane is walked either
            // way round its ring (WLK-8).
            var fromM = arcs[0].StartM;
            var againstTheRing = Vector2.DistanceSquared(fromM, arriving.AtM) > Vector2.DistanceSquared(fromM, leaving.AtM);
            return againstTheRing ? new Owned(Reversed(arcs), arcs) : new Owned(arcs, Reversed(arcs));
        }

        /// <summary>
        /// <b>A walk down a course and onto the paint</b> (WLK-15, WLK-16): the course as far as the first metre
        /// of it one arc turns from onto the paint's own line short of the mouth, that arc, and straight down the
        /// paint's line to the boundary. <b>Or nothing</b>, where no metre of the course turns onto it on a
        /// circle that keeps the walk off the road — a movement the place does not offer (WLK-14).
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The course and not the pose's own straight</b>: the course's band is the boundary moved off by the
        /// lane's own distance (WLK-1), so the walk down it covers the lane's ground and none besides, where a
        /// straight laid off the pose leaves that ground wherever the kerb turns a corner.
        /// </para>
        /// <para>
        /// <b>At half its band a turn pivots about its own inside edge</b>, and that is the circle asked for
        /// first. The first metre a turn that wide leaves from is the one whose pivot stands that far off the
        /// paint's line — on the lane beside the kerb, a point of the boundary itself — and the inside edge stays
        /// on it all the way round. Any wider circle swings that edge out past the pivot, onto the road; a
        /// tighter one, which is what a paint struck a little off square to the kerb is left with, folds it back
        /// onto the walk. A turn away from the kerb swings its kerb side further from it however wide it is.
        /// </para>
        /// <para>
        /// <b>Where no metre of the course turns onto the paint on so tight a circle</b> — a course rounded well
        /// back from a tight corner, the paint's line running along it rather than across — the walk is the
        /// curve straight from where the lane hands over (<see cref="Spline.CorneredInto"/>), and <b>only where
        /// its band stands off the road</b> the whole way (<see cref="BandOffTheRoad"/>).
        /// </para>
        /// </remarks>
        public ArcSeg[]? OntoThePaint(ArcSeg[] course, Vector2 mouthM, float intoTheRoadRad)
        {
            if (!PaintAt(mouthM, intoTheRoadRad, out var paint)) return null;
            if (Departed(course, paint) is { } departed && BandOffTheVerge(departed)) return departed;

            Span<ArcSeg> drawn = stackalloc ArcSeg[MostArcsInAConnection];
            var laid = Spline.CorneredInto(
                course[0].StartM, course[0].HeadingRad, mouthM, intoTheRoadRad, config.WalkerTightestTurnM, drawn);
            return laid > 0 && BandOffTheRoad(drawn[..laid], paint.RoadHand) && BandOffTheVerge(drawn[..laid])
                ? drawn[..laid].ToArray()
                : null;
        }

        /// <summary>
        /// <b>A walk from one crossing onto another at the same place</b> (WLK-3, WLK-14, WLK-16), from where the
        /// first arrives to where the second sets off — the first of these that keeps its band on the walk, or
        /// nothing:
        /// <list type="number">
        /// <item><b>Straight along each paint's own line and one arc between them</b>
        /// (<see cref="Spline.StraightArcStraightInto"/>): half the walk's band wide, and widened a half band at a
        /// time up to the widest the two fit.</item>
        /// <item><b>Up the first paint and one turn onto a lane's course, down it, and one turn off it onto the
        /// second</b> (<see cref="ByTheCourse"/>).</item>
        /// <item><b>The curve straight from one paint to the other</b> (<see cref="Spline.CorneredInto"/>), off the
        /// road, as a paint's own connection falls back on.</item>
        /// </list>
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>At half its band a turn pivots about its own inside edge</b>, so its ground is the ground of the two
        /// straights it joins and none besides — each of them standing on the kerb square across its own paint.
        /// </para>
        /// <para>
        /// <b>But the two straights meet where the paint lines do</b>, and at a corner whose two crossings stand
        /// back from it further than a walk is wide, that is out on the verge. A wider circle stands back off that
        /// corner by more, towards the kerb the two crossings share, so the widening spends on the turn only the
        /// ground it has to. <b>Where no circle fits that stays on</b> — the two stand back further still — the walk
        /// between them is the pavement's own, round the corner on a lane's course.
        /// </para>
        /// </remarks>
        public ArcSeg[]? Joined(Vector2 fromM, float fromRad, Vector2 ontoM, float ontoRad, ReadOnlySpan<Owned?> owned)
        {
            var tightestM = config.WalkerTightestTurnM;
            var widestM = Spline.WidestTurnM(fromM, fromRad, ontoM, ontoRad);
            var stepM = config.WalkingLaneWidthM * 0.5f;
            var radiusM = MathF.Min(stepM, widestM);

            Span<ArcSeg> drawn = stackalloc ArcSeg[MostArcsInAConnection];
            while (radiusM >= tightestM)
            {
                var laid = Spline.StraightArcStraightInto(fromM, fromRad, ontoM, ontoRad, radiusM, drawn);
                if (laid > 0 && BandOffTheVerge(drawn[..laid])) return drawn[..laid].ToArray();
                if (radiusM >= widestM) break;

                radiusM = MathF.Min(radiusM + stepM, widestM);
            }

            foreach (var stretch in owned)
            {
                if (stretch is { } walked && ByTheCourse(walked, fromM, fromRad, ontoM, ontoRad) is { } line) return line;
            }

            // The road is on the hand of the boundary the first paint runs down into it on.
            var curved = Spline.CorneredInto(fromM, fromRad, ontoM, ontoRad, tightestM, drawn);
            return curved > 0 && OnTheWalk(drawn[..curved], fromM, Spline.WrapRad(fromRad + MathF.PI))
                ? drawn[..curved].ToArray()
                : null;
        }

        /// <summary>
        /// <b>A walk from one paint onto another by the course between them</b>: the lane's connection off the
        /// first paint and its connection onto the second (<see cref="OntoThePaint"/>), met on the course — up the
        /// first paint, one turn onto the course, down it, and one turn off it down the second. <b>Or nothing</b>,
        /// where the lane comes to its turn onto the second before its turn off the first, or no turn joins either,
        /// or the band leaves the walk.
        /// </summary>
        ArcSeg[]? ByTheCourse(in Owned stretch, Vector2 fromM, float fromRad, Vector2 ontoM, float ontoRad)
        {
            // Off the paint is onto it walked backwards, and turned round (Through).
            if (!PaintAt(fromM, Spline.WrapRad(fromRad + MathF.PI), out var off)
                || !DepartsAt(stretch.Back, off, out var offM)
                || !PaintAt(ontoM, ontoRad, out var onto)
                || !DepartsAt(stretch.Walked, onto, out var leavesM))
            {
                return null;
            }

            var landsM = Spline.TotalLengthM(stretch.Walked) - offM;
            if (landsM > leavesM) return null;

            Span<ArcSeg> down = stackalloc ArcSeg[2];
            var offLaid = OntoItFrom(stretch.Back, offM, off, down);

            var line = new ArcSeg[offLaid + stretch.Walked.Length + 2];
            Spline.ReverseInto(down[..offLaid], line);
            var laid = offLaid + Spline.SubChainInto(stretch.Walked, landsM, leavesM, line.AsSpan(offLaid));
            laid += OntoItFrom(stretch.Walked, leavesM, onto, line.AsSpan(laid));

            return BandOffTheVerge(line.AsSpan(0, laid)) ? line[..laid] : null;
        }

        /// <summary>
        /// Where a paint's lane meets the boundary, which way is down it, and which hand of the boundary's own
        /// line the road is on there — the one reading every other side is told by.
        /// </summary>
        readonly record struct Paint(Vector2 MouthM, Vector2 Down, float DownRad, int RoadHand);

        /// <summary>The paint whose lane meets the boundary at a mouth, run down into the road on a heading — or false where no boundary answers.</summary>
        bool PaintAt(Vector2 mouthM, float intoTheRoadRad, out Paint paint)
        {
            paint = default;
            if (!boundary.NearestTo(mouthM, out var kerb)) return false;

            var down = Heading.Unit(intoTheRoadRad);
            paint = new Paint(mouthM, down, intoTheRoadRad, MathF.Sign(Vector2.Dot(down, kerb.Right)));
            return true;
        }

        /// <summary>The course as far as the first metre a turn leaves it from, the turn, and down the paint.</summary>
        ArcSeg[]? Departed(ArcSeg[] course, in Paint paint)
        {
            if (!DepartsAt(course, paint, out var leavesM)) return null;

            var line = new ArcSeg[course.Length + 2];
            var laid = Spline.SubChainInto(course, 0f, leavesM, line);
            laid += OntoItFrom(course, leavesM, paint, line.AsSpan(laid));
            return line[..laid];
        }

        /// <summary>The first metre of a course one turn leaves from onto the paint (<see cref="Leaves"/>), closed in on — or false where none does.</summary>
        bool DepartsAt(ArcSeg[] course, in Paint paint, out float leavesM)
        {
            leavesM = float.NaN;
            var beforeM = 0f;
            var lengthM = Spline.TotalLengthM(course);
            for (var atM = 0f; atM <= lengthM; atM += LineTolerance.JoinedM)
            {
                if (Leaves(course, atM, paint, out _))
                {
                    leavesM = atM;
                    break;
                }

                beforeM = atM;
            }

            if (float.IsNaN(leavesM)) return false;

            for (var halving = 0; leavesM > 0f && halving < Halvings; halving++)
            {
                var midM = (beforeM + leavesM) * 0.5f;
                if (Leaves(course, midM, paint, out _)) leavesM = midM;
                else beforeM = midM;
            }

            return true;
        }

        /// <summary>The turn off a course at a metre of it and the straight down the paint to the mouth, as many of the two as there are.</summary>
        int OntoItFrom(ArcSeg[] course, float leavesM, in Paint paint, Span<ArcSeg> into)
        {
            Leaves(course, leavesM, paint, out var round);
            into[0] = round;

            var downM = Vector2.Dot(paint.MouthM - round.EndM, paint.Down);
            if (downM <= LineTolerance.RoundingM) return 1;

            into[1] = new ArcSeg(round.EndM, paint.DownRad, downM, 0f);
            return 2;
        }

        /// <summary>
        /// <b>The one arc that leaves a course at a metre of it and arrives on the paint's own line heading down
        /// it</b> — false where that arc is tighter than the feet can hold, wider than half the walk's band, or
        /// arrives past the mouth.
        /// </summary>
        bool Leaves(ArcSeg[] course, float atM, in Paint paint, out ArcSeg round)
        {
            round = default;
            var at = Spline.SampleAt(course, atM);
            var turnRad = Spline.WrapRad(paint.DownRad - at.HeadingRad);

            // An arc's end is its start moved by its radius times the end of the same turn on a unit circle, so
            // the radius that lands it on the paint's line is one division.
            var unit = new ArcSeg(Vector2.Zero, at.HeadingRad, MathF.Abs(turnRad), MathF.Sign(turnRad));
            var across = Spline.Cross(paint.Down, unit.EndM);
            if (across == 0f) return false;

            var radiusM = -Spline.Cross(paint.Down, at.PositionM - paint.MouthM) / across;
            if (radiusM < config.WalkerTightestTurnM || radiusM > config.WalkingLaneWidthM * 0.5f) return false;

            round = new ArcSeg(at.PositionM, at.HeadingRad, MathF.Abs(turnRad) * radiusM, MathF.Sign(turnRad) / radiusM);
            return Vector2.Dot(paint.MouthM - round.EndM, paint.Down) >= -LineTolerance.RoundingM;
        }

        /// <summary>
        /// <b>Whether a line's band stands off the road</b>: both its edges, a touch apart along it, no further
        /// onto the road's side of the boundary than a touch (<see cref="SimConfig.RibbonTouchM"/>, TER-5c) —
        /// so no edge moves further than a touch between two places looked at.
        /// </summary>
        bool BandOffTheRoad(ReadOnlySpan<ArcSeg> line, int roadHand)
        {
            var halfM = config.WalkingLaneWidthM * 0.5f;
            var lengthM = Spline.TotalLengthM(line);
            for (var atM = 0f; atM <= lengthM; atM += config.RibbonTouchM)
            {
                var at = Spline.SampleAt(line, atM);
                if (OnTheRoad(at.PositionM + (at.Right * halfM), roadHand)
                    || OnTheRoad(at.PositionM - (at.Right * halfM), roadHand))
                {
                    return false;
                }
            }

            return true;
        }

        bool OnTheRoad(Vector2 pointM, int roadHand) =>
            boundary.NearestTo(pointM, out var kerb)
            && Vector2.Dot(pointM - kerb.PositionM, kerb.Right) * roadHand > config.RibbonTouchM;

        /// <summary>
        /// <b>Whether a line's band stands on the walk the whole way</b>: off the road on the hand the road is on at
        /// the paint it runs down into it (<see cref="BandOffTheRoad"/>), and off the verge.
        /// </summary>
        public bool OnTheWalk(ReadOnlySpan<ArcSeg> line, Vector2 mouthM, float intoTheRoadRad) =>
            boundary.NearestTo(mouthM, out var kerb)
            && BandOffTheRoad(line, MathF.Sign(Vector2.Dot(Heading.Unit(intoTheRoadRad), kerb.Right)))
            && BandOffTheVerge(line);

        /// <summary>
        /// <b>Whether a line's band stands off the verge</b>: both its edges, a touch apart along it, no further past
        /// the walk's outer face than a touch (<see cref="SimConfig.RibbonTouchM"/>, TER-5c) — the face the ground
        /// answers the walk off, notches filled and all (<see cref="GroundRings.WalkSides"/>). <b>A connection is the
        /// walk's own ground and none besides</b>, whichever of its shapes it takes (WLK-15).
        /// </summary>
        public bool BandOffTheVerge(ReadOnlySpan<ArcSeg> line)
        {
            var inM = (config.WalkingLaneWidthM * 0.5f) - config.RibbonTouchM;
            var lengthM = Spline.TotalLengthM(line);
            for (var atM = 0f; atM <= lengthM; atM += config.RibbonTouchM)
            {
                var at = Spline.SampleAt(line, atM);
                if (!walk.Encloses(at.PositionM + (at.Right * inM)) || !walk.Encloses(at.PositionM - (at.Right * inM)))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Where the paint stops, which is where the tarmac does.</summary>
        bool OnTheKerb(Vector2 paintM, out Vector2 kerbM)
        {
            kerbM = default;
            if (!boundary.NearestTo(paintM, out var on)) return false;
            if (Vector2.Distance(on.PositionM, paintM) > config.Road.CrossingMeetsTheWalkWithinM) return false;

            kerbM = on.PositionM;
            return true;
        }

        /// <summary>
        /// Where one lane of the walk hands over: a setback along its own course from the junction, and
        /// <b>which way the walk runs there</b> — the course's own heading where the lane is walked with its
        /// ring and the other way about where it is walked against it (WLK-8).
        /// </summary>
        bool HandsOver(int lane, Vector2 centreM, float stepM, out Meeting met, out float offM)
        {
            met = default;
            offM = float.PositiveInfinity;

            // <b>Whose pavement this is, is asked where the course passes the junction</b> and not at the
            // point a setback along it lands on: the setback says where to hand over, and a lane pushed back
            // from a tight corner by its own rounding still runs past the place (WLK-15).
            if (!courses[lane].NearestTo(centreM, out var beside)) return false;

            offM = Vector2.Distance(beside.PositionM, centreM);
            if (offM > config.Road.CrossingMeetsTheWalkWithinM) return false;
            if (!courses[lane].Along(centreM, stepM, out var on, out var station)) return false;
            if (station.Line >= pavement.RingsOf(lane).Length) return false;

            var headingRad = pavement.RunsWithTheRing(lane)
                ? on.HeadingRad
                : Spline.WrapRad(on.HeadingRad + MathF.PI);
            met = new Meeting(station.Line, station.AlongM, on.PositionM, headingRad);
            return true;
        }
    }

    /// <summary>
    /// The places each ring is parted, in the order it is walked and <b>with the ones a weld would join
    /// dropped</b>: two junctions at the ends of a short street part a ring within a stride of each other,
    /// and a corner two crossings meet at parts it twice at one place.
    /// </summary>
    static float[][][] Sorted(List<float>[][] parting, float weldM)
    {
        var partedM = new float[parting.Length][][];
        for (var lane = 0; lane < parting.Length; lane++)
        {
            partedM[lane] = new float[parting[lane].Length][];
            for (var ring = 0; ring < parting[lane].Length; ring++)
            {
                var places = parting[lane][ring];
                places.Sort();

                var kept = new List<float>(places.Count);
                foreach (var alongM in places)
                {
                    if (kept.Count > 0 && alongM - kept[^1] <= weldM) continue;

                    kept.Add(alongM);
                }

                partedM[lane][ring] = [.. kept];
            }
        }

        return partedM;
    }
}
