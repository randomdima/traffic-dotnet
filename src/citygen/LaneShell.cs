using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The outer shell of the ground the town is driven over, said in the lines themselves</b>: for every
/// lane, every movement through a box and every way into a bay, the stretches of it whose own band edge is
/// the edge of that ground (GEN-4b, TER-5).
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the boundary of one shape and not of any one band.</b> The ground is what
/// <see cref="Kerbs.OffTheDrivenM"/> answers for — a band its full width for every road, every movement and
/// every bay — and a metre of a line is <em>outside</em> exactly where one of its own two edges stands on the
/// edge of that. On a plain street that leaves the outer lanes and nothing else, one where the street is one
/// lane and both where it is two, because the seam between two lanes of one carriageway is a road's own
/// middle. And a junction needs no case of its own: two roads crossing lay two bands that overlap, and what
/// is left outside is the four corners where their kerbs cross.
/// </para>
/// <para>
/// <b>A stretch has a side, and the side is what says which way it is walked.</b> The outside is walked with
/// the ground on one hand throughout, so a stretch whose clear edge is its line's left is walked the way the
/// line runs and one whose clear edge is its right is walked against it. It is what tells the outside of a
/// one-lane street, which goes out along it and comes back down the same line, from a ring that stops dead.
/// </para>
/// <para>
/// <b>Two stretches are joined at their edges, where the arithmetic is exact.</b> Where two bands cross,
/// their edges cross at a point, and each stretch ends where the other band's edge reaches its own — so the
/// two stops are <em>the same place</em>, solved rather than walked to (<see cref="BandEdges"/>) wherever
/// there is a band whose boundary explains the stop at all. Said in the lines instead,
/// the two stops stand half a width apart on lines that never touch, and every rule for pairing them up
/// prefers the width of the carriageway to the corner of the junction.
/// </para>
/// <para>
/// <b>What is drawn is the lines, and never their edges.</b> A ring carries the town's own arcs, cut out of
/// the lanes, movements and ways it runs along, each taken to where its own line crosses the next
/// (<see cref="Carried"/>); what a join adds is the straight between two lines that never cross. Nothing is
/// bent: a chord across a bend cuts inside the very lane the shell was asked to stay outside of.
/// </para>
/// <para>
/// <b>Where the ground is cut, the straight is the join</b> — the end of a road, the back of a car park,
/// the step at a junction mouth. Such a pair is offered only across the driven ground and never over the
/// grass.
/// </para>
/// <para>
/// <b>Every side of the ground is the outside of it.</b> A street grid bounds what it lays on the outside
/// and round every block it encloses, and both are its edge: a plain two-lane street is <em>both</em> its
/// lanes, one carried by the ring round the town and the other by the ring round the block behind it. So
/// what comes back is every ring that shuts, and a ring that goes round nothing — a notch a hand's breadth
/// across, shut on itself — is not one.
/// </para>
/// </remarks>
internal sealed partial class LaneShell
{
    /// <summary>
    /// How finely a line is walked when asking whether it is the outside. Half a metre: the answer changes
    /// where two bands cross, and the crossings a reader is looking at are metres long.
    /// </summary>
    const float StationM = 0.5f;

    /// <summary>
    /// How many times the station a stretch ends at is halved to find where the answer really changed.
    /// <b>It says which crossing and never where it is</b> (<see cref="BandEdges"/>): the walk asks about a
    /// probe standing off the edge, so the rounds converge on a place a fraction of a metre from the
    /// crossing however many of them there are. Twelve is what it takes to name the crossing on either side
    /// of it.
    /// </summary>
    const int CrossingRounds = 12;

    /// <summary>
    /// How near two band edges have to stop to be the same place. <b>It is not a search radius but a
    /// rounding</b>: the two are one crossing point, and what opens it up is the ends the arithmetic could
    /// not solve for (<see cref="BandEdges.CutM"/>) — the ground round a junction corner is nobody's band,
    /// so the walk's own answer stands there and stands a fraction of a metre off. Read as a search radius
    /// instead — five metres, say — an end takes whatever is nearest and the town comes back wired through
    /// itself; the solved ends stand within a millimetre of one another and want none of it.
    /// </summary>
    const float MeetM = 1.5f;

    /// <summary>How finely a straight is walked when asking whether it stays on the driven ground. A metre: the narrowest band is wider than that.</summary>
    const float CrossingStationM = 1f;

    /// <summary>
    /// How far apart two left-over ends may stand before they are not offered to each other at all. <b>Not a
    /// limit on the answer</b> — the ground is that — but on the asking: the question costs a walk of the
    /// straight between them, and a city's ends asked of one another pairwise is most of a minute spent
    /// proving that two of them are half a mile apart. Thirty metres is a car park's back with room over.
    /// </summary>
    const float OfferedAtLeastM = 30f;

    /// <summary>
    /// <b>And as far as the two bands themselves are wide</b>, because the join a line makes at its own
    /// square end is as wide as the line. A flat figure is a bound on how wide a road may be before it
    /// cannot cap itself: a laboratory map laid as rows a hundred and fifty metres across had two edges
    /// running its whole length, nothing to hand either of them on to, and so no boundary and no ground
    /// drawn at all.
    /// </summary>
    static float OfferedWithinM(Walk walk, List<Stretch> outer, int at, int other) =>
        MathF.Max(
            OfferedAtLeastM,
            walk.HalfM(outer[at].Line) + walk.HalfM(outer[other].Line));

    /// <summary>Where a stretch carries on to when it carries on to nothing.</summary>
    const int Nowhere = -1;

    /// <summary>
    /// How many times a corner is cut back before it is left as it is. Each round takes whichever of its
    /// two ends runs backwards to the foot of the other, which is where that end stops running backwards,
    /// so one round settles a corner between two lines and the rest are what a corner between two bends
    /// takes to settle again after its partner moved.
    /// </summary>
    const int SquaringRounds = 6;

    /// <summary>
    /// How many times the stretches the carry left with nothing are dropped and the carry run again over
    /// what is left. Dropping one can carry away the stretch beside it in its turn, and a ring shortens by
    /// fewer of them every round.
    /// </summary>
    const int DroppingRounds = 4;


    readonly ArcSeg[][] _chains;
    readonly int[][] _lineOfArc;
    readonly float[][] _halfOfArc;
    readonly float[] _halfM;
    readonly bool _turned;

    LaneShell(ArcSeg[][] chains, int[][] lineOfArc, float[] halfM, ShellReading reading, bool turned = false)
    {
        _chains = chains;
        _lineOfArc = lineOfArc;
        _halfM = halfM;
        _turned = turned;
        _halfOfArc = new float[chains.Length][];
        for (var ring = 0; ring < chains.Length; ring++)
        {
            var half = new float[chains[ring].Length];
            for (var arc = 0; arc < half.Length; arc++)
            {
                var line = lineOfArc[ring][arc];

                // A bridge belongs to no line, and the ground it draws across is the ground either side of
                // it: the band it stands for is the one the stretch it leaves laid, carried over the join.
                half[arc] = line >= 0 ? halfM[line] : Carried(lineOfArc[ring], halfM, arc);
            }

            _halfOfArc[ring] = half;
        }

        Reading = reading;
    }

    /// <summary>The nearest band a bridge arc stands between, walked back round the ring to the piece that has one.</summary>
    static float Carried(int[] lineOfArc, float[] halfM, int arc)
    {
        for (var step = 1; step <= lineOfArc.Length; step++)
        {
            var before = lineOfArc[((arc - step) % lineOfArc.Length + lineOfArc.Length) % lineOfArc.Length];
            if (before >= 0) return halfM[before];
        }

        return 0f;
    }

    /// <summary>
    /// One stretch of one driven line that is the outside: the metres of that line it covers, and which of
    /// the line's two edges was the clear one — which is also which way round the stretch is walked.
    /// </summary>
    readonly record struct Stretch(int Line, float FromM, float ToM, bool OnTheLeft);

    /// <summary>
    /// One end of one walked stretch: the band edge it stops on, how far along its own line that stands, and
    /// the way the outside is heading there.
    /// </summary>
    readonly record struct End(Vector2 EdgeM, float AtM, float AlongRad);

    /// <summary>
    /// <b>The outside of the town's driven ground as closed rings</b>, each the stretches that follow one
    /// another strung end to end and shut on itself.
    /// </summary>
    /// <remarks>
    /// <b>A run that will not close is not handed over.</b> Every stretch is meant to carry on into exactly
    /// one other, and where the outside stops instead, what is left is a length of the town's edge nothing
    /// accounts for — a fault in the ground under the reading. It is neither papered over with a straight
    /// across the town nor handed on as a piece of a perimeter, because a perimeter in pieces is not one.
    /// What that costs is that a town with one break in its shell has no shell here at all, which is the
    /// honest reading of a town with one break in its shell.
    /// </remarks>
    public ReadOnlySpan<ArcSeg[]> Chains => _chains;

    /// <summary>
    /// <b>What the laying came to, including what it threw away</b> (<see cref="ShellReading"/>) — since
    /// <see cref="Chains"/> is the rings that shut and nothing else, a run that broke leaves no trace in it.
    /// </summary>
    public ShellReading Reading { get; }

    /// <summary>
    /// <b>Which driven line each arc of a ring was cut out of</b>, or <see cref="Nowhere"/> for the straight
    /// a hand-over drew between two of them — so a reader wanting only the lanes, or only the ways into a
    /// car park, can take the stretches it wants and leave the rest.
    /// </summary>
    public ReadOnlySpan<int> LinesOf(int ring) => _lineOfArc[ring];

    /// <summary>
    /// <b>How far the edge of the ground stands off each arc of a ring</b>: half the band the line that arc
    /// runs along lays. It is the offset the ground's own boundary is this ring moved by, and every distance
    /// the town measures out from that boundary is this plus the distance wanted.
    /// </summary>
    public ReadOnlySpan<float> HalvesOf(int ring) => _halfOfArc[ring];

    readonly Dictionary<(float OutM, float SmoothM), ArcSeg[][]> _extruded = [];

    /// <summary>
    /// <b>The rings that stand <paramref name="outM"/> metres clear of the driven ground</b>, each of
    /// <see cref="Chains"/> moved off the ground it walks with by half its own band plus that distance, and
    /// smoothed over <paramref name="smoothM"/> metres (<see cref="Extrusion"/>). A ring the offset leaves
    /// nothing of comes back empty, and the answer stands one for one with <see cref="Chains"/> so that a
    /// caller holding several distances can read them against one another ring by ring.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nought is the kerb and not the line.</b> A ring runs down the lines cars are driven on, and the
    /// edge of what they lay stands half a band out from that — so the distance a caller asks for is
    /// measured from the edge of the ground, which is the only place a reader could measure it from.
    /// <b>A negative distance is inside the tarmac</b> and is how a kerb line is struck.
    /// </para>
    /// <para>
    /// <b>Out is the walker's left throughout</b>, on every ring and not only the outermost: a ring walks
    /// with the driven ground on its right, so the same hand takes the line away from the tarmac round the
    /// town and into the block behind it.
    /// </para>
    /// <para>
    /// Laid on the ask and kept, because the ask is the same one every time it is asked — a layer redrawing
    /// on a pan wants the line it had rather than a second walk of the town's own edge.
    /// </para>
    /// </remarks>
    public ReadOnlySpan<ArcSeg[]> Extruded(float outM, float smoothM)
    {
        if (_extruded.TryGetValue((outM, smoothM), out var held)) return held;

        // Out is the walker's left, which is the negative side (<see cref="Spline.OffsetInto"/>), and what
        // each piece of the ring stands off the ground it lays is half its own band.
        return _extruded[(outM, smoothM)] = Extrusion.Of(_chains, _halfOfArc, outM, smoothM);
    }

    public static LaneShell Of(Paving paving, SimConfig config)
    {
        var count = paving.DrivenCount;
        var halfM = new float[count];
        var lengthM = new float[count];
        var mostHalfM = 0f;

        var building = new ChainIndex.Builder();
        for (var line = 0; line < count; line++)
        {
            halfM[line] = paving.DrivenWidthM(line) * 0.5f;
            lengthM[line] = paving.DrivenLengthM(line);
            mostHalfM = MathF.Max(mostHalfM, halfM[line]);
            building.Add(line, paving.ArcsOfDriven(line), lengthM[line]);
        }

        var stops = Stops(paving);
        var bands = building.Seal(config.NearestChainCellM);
        var walk = new Walk(
            paving, bands, halfM, lengthM, mostHalfM, config.JunctionArmReachMaxM, stops,
            new BandEdges(paving, bands, halfM, lengthM, mostHalfM));

        var reading = new ShellReading();
        var outer = new List<Stretch>();
        for (var line = 0; line < count; line++)
        {
            var arcs = paving.ArcsOfDriven(line);
            if (arcs.Length == 0 || lengthM[line] <= 0f) continue;

            Stretches(walk, line, arcs, lengthM[line], onTheLeft: true, outer, reading);
            Stretches(walk, line, arcs, lengthM[line], onTheLeft: false, outer, reading);
        }

        reading.Walked(count, outer.Count);

        var ends = Ends(paving, halfM, outer);
        var following = Following(walk, outer, ends, reading, out var acrossACut);
        var (chains, lineOfArc) = Strung(
            walk, paving, outer, ends, following, acrossACut, leastRoundM: mostHalfM * 2f, reading);
        return new LaneShell(chains, lineOfArc, halfM, reading);
    }

    /// <summary>
    /// <b>Where each driven line really stops</b>: the two ends of it that nothing is driven on from, so the
    /// band's square end is the edge of the ground rather than the seam with whatever carries on
    /// (<c>[line * 2]</c> its start and <c>[line * 2 + 1]</c> its end).
    /// </summary>
    /// <remarks>
    /// <b>It is what tells the two kinds of hand-over apart</b> (<see cref="Fits"/>). A road that really
    /// stops and the back of a car park are ground that is cut, and the outside crosses them on a straight;
    /// a lane at a junction mouth has stopped only as a <em>line</em> — the box carries the ground on — and
    /// the outside goes round the movements that cross it. Both look alike from the edges alone, and the cut
    /// is the nearer join of the two, so read without this the lane capped on its own other edge at every
    /// mouth in the town and the perimeter came back as a rectangle round each street.
    /// </remarks>
    static bool[] Stops(Paving paving)
    {
        var lanes = paving.Lanes;
        var stops = new bool[paving.DrivenCount * 2];

        // A lane stops at the end no movement leaves and the start none arrives at: the dead end of a
        // street, and the place a lane runs out at (GEN-4h). A movement stops at neither end, being drawn
        // between two lanes.
        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            stops[lane * 2] = true;
            stops[(lane * 2) + 1] = lanes.ConnectorAt[lane + 1] == lanes.ConnectorAt[lane];
        }

        foreach (var onto in lanes.ConnectorToLane) stops[onto * 2] = false;

        // A way into a bay is drawn from the lane to the far end of the space and a way out the other way
        // about, so the bay's own end is the one that stops and the lane's end is a seam with the lane.
        var bays = paving.Bays;
        var firstBay = lanes.LaneCount + lanes.ConnectorCount;
        for (var at = 0; at < bays.GroundWays.Length; at++)
        {
            var entry = bays.IsEntry[bays.GroundWays[at]];
            stops[(firstBay + at) * 2] = !entry;
            stops[((firstBay + at) * 2) + 1] = entry;
        }

        return stops;
    }

    /// <summary>
    /// The stretches of one edge of one line that are the outside, each cut to where the answer really
    /// changed rather than to the station that noticed (<see cref="Crossing"/>).
    /// </summary>
    static void Stretches(
        Walk walk, int line, ReadOnlySpan<ArcSeg> arcs, float lengthM, bool onTheLeft, List<Stretch> into,
        ShellReading reading)
    {
        var stations = Math.Max(1, (int)MathF.Ceiling(lengthM / StationM));
        var openedAtM = 0f;
        var was = walk.Outside(line, arcs, 0f, onTheLeft);
        var lastM = 0f;

        for (var station = 1; station <= stations; station++)
        {
            var atM = lengthM * station / stations;
            var stands = walk.Outside(line, arcs, atM, onTheLeft);
            if (stands && !was) openedAtM = Crossing(walk, line, arcs, onTheLeft, atM, lastM, reading);
            else if (!stands && was)
            {
                Keep(
                    into, line, openedAtM, Crossing(walk, line, arcs, onTheLeft, lastM, atM, reading),
                    onTheLeft);
            }

            was = stands;
            lastM = atM;
        }

        if (was) Keep(into, line, openedAtM, lengthM, onTheLeft);
    }

    /// <summary>
    /// One stretch, kept unless <b>its own two ends are the same place</b> (<see cref="Kerbs.OnePlaceM"/>) —
    /// which is a crossing the walk grazed rather than a length of the outside, and would be a node in a
    /// ring with no line under it.
    /// </summary>
    /// <remarks>
    /// <b>Read at the rounding the ends are bisected to, it is a hundred times too fine.</b> A walk half a
    /// metre at a step, bisected, leaves a centimetre of edge standing wherever it grazes a crossing — long
    /// enough to pass a millimetre and short enough that its two ends are one point, so nothing meets it on
    /// either side and it carries the outside on to nothing. A hair of a stretch like that broke the ring
    /// round a car park and with it every lane the ring ran along.
    /// </remarks>
    static void Keep(List<Stretch> into, int line, float fromM, float toM, bool onTheLeft)
    {
        if (toM - fromM > Kerbs.OnePlaceM) into.Add(new Stretch(line, fromM, toM, onTheLeft));
    }

    /// <summary>
    /// <b>Where along the line the edge stopped being the outside</b>: solved against the boundary of the
    /// band that covered it (<see cref="BandEdges"/>), and bisected between the last station that was the
    /// outside and the first that was not only where no band explains the change.
    /// </summary>
    /// <remarks>
    /// <b>Bisection converges on the probe and not on the crossing.</b> The walk asks about a point a step
    /// outside the edge, so however many rounds it is given it lands short of the true crossing by that step
    /// divided by however shallowly the two bands meet — a tenth of a metre where a way out of a bay leaves
    /// its lane, and the whole reason two ends that stop at one point have to be recognised at a hand's
    /// breadth rather than at a millimetre. What is left to it is the ground that is nobody's band: the
    /// apron rounding a junction corner is driven ground no line lays an edge on, and where the outside
    /// stops there, there is nothing to solve against.
    /// </remarks>
    static float Crossing(
        Walk walk, int line, ReadOnlySpan<ArcSeg> arcs, bool onTheLeft, float clearM, float coveredM,
        ShellReading reading)
    {
        var wasCoveredAtM = coveredM;
        for (var round = 0; round < CrossingRounds; round++)
        {
            var midM = (clearM + coveredM) * 0.5f;
            if (walk.Outside(line, arcs, midM, onTheLeft)) clearM = midM;
            else coveredM = midM;
        }

        var walkedM = (clearM + coveredM) * 0.5f;
        if (!walk.CutM(line, onTheLeft, walkedM, wasCoveredAtM, out var solvedM))
        {
            reading.Cut(solved: false);
            return walkedM;
        }

        reading.Cut(solved: true);
        return solvedM;
    }

    /// <summary>
    /// The two ends of every stretch in the order it is walked — <c>at * 2</c> the one the outside arrives
    /// at and <c>at * 2 + 1</c> the one it leaves by.
    /// </summary>
    static End[] Ends(Paving paving, float[] halfM, List<Stretch> outer)
    {
        var ends = new End[outer.Count * 2];
        for (var at = 0; at < outer.Count; at++)
        {
            var stretch = outer[at];
            var arcs = paving.ArcsOfDriven(stretch.Line);
            ends[at * 2] = EndAt(arcs, halfM[stretch.Line], stretch, stretch.OnTheLeft ? stretch.FromM : stretch.ToM);
            ends[(at * 2) + 1] =
                EndAt(arcs, halfM[stretch.Line], stretch, stretch.OnTheLeft ? stretch.ToM : stretch.FromM);
        }

        return ends;
    }

    static End EndAt(ReadOnlySpan<ArcSeg> arcs, float halfM, Stretch stretch, float atM)
    {
        var on = Spline.SampleAt(arcs, atM);
        return new End(
            on.PositionM + (on.Right * (stretch.OnTheLeft ? -halfM : halfM)),
            atM,
            stretch.OnTheLeft ? on.HeadingRad : on.HeadingRad + MathF.PI);
    }

    /// <summary>
    /// <b>Which stretch each one carries the outside on to</b>, so that a ring is walked rather than
    /// assembled: the one whose own edge starts where this one's edge stopped, and where the ground is cut
    /// and no such stretch exists, the nearest one the ground can carry a straight to.
    /// </summary>
    static int[] Following(
        Walk walk, List<Stretch> outer, End[] ends, ShellReading reading, out bool[] acrossACut)
    {
        var following = new int[outer.Count];
        var taken = new bool[outer.Count];
        Array.Fill(following, Nowhere);

        Meetings(ends, following, taken, reading);
        var met = Handed(following);

        acrossACut = new bool[outer.Count];
        for (var at = 0; at < following.Length; at++) acrossACut[at] = following[at] == Nowhere;

        Cut(walk, outer, ends, following, taken);
        for (var at = 0; at < following.Length; at++) acrossACut[at] &= following[at] != Nowhere;

        reading.Paired(met, Handed(following) - met);
        Loose(outer, ends, following, reading);
        return following;
    }

    /// <summary>How many stretches have somewhere to carry the outside on to.</summary>
    static int Handed(int[] following)
    {
        var handed = 0;
        foreach (var next in following)
        {
            if (next != Nowhere) handed++;
        }

        return handed;
    }

    /// <summary>
    /// Every stretch left carrying the outside on to nothing, and the nearest end that could have taken it
    /// — which is what says whether the pairing missed a partner or the ground never offered one.
    /// </summary>
    static void Loose(List<Stretch> outer, End[] ends, int[] following, ShellReading reading)
    {
        for (var at = 0; at < following.Length; at++)
        {
            if (following[at] != Nowhere) continue;

            var leaving = ends[(at * 2) + 1];
            var nearestM = float.PositiveInfinity;
            var nearest = Nowhere;
            for (var other = 0; other < following.Length; other++)
            {
                if (other == at) continue;

                var apartM = Vector2.Distance(ends[other * 2].EdgeM, leaving.EdgeM);
                if (apartM >= nearestM) continue;

                nearestM = apartM;
                nearest = other;
            }

            reading.Loose(new ShellLooseEnd(
                outer[at].Line, leaving.EdgeM, nearestM,
                nearest == Nowhere ? Nowhere : outer[nearest].Line,
                nearest != Nowhere && Array.IndexOf(following, nearest) >= 0));
        }
    }

    /// <summary>
    /// The stretch that starts where this one stops, <b>the hard way round where more than one does</b>:
    /// the outside is walked with the ground on one hand, so of several lines leaving one point the one it
    /// carries on to is the one that turns furthest towards the ground it is keeping.
    /// </summary>
    static void Meetings(End[] ends, int[] following, bool[] taken, ShellReading reading)
    {
        var offered = new List<(float ApartM, float TurnRad, int At, int Other)>();
        for (var at = 0; at < following.Length; at++)
        {
            var leaving = ends[(at * 2) + 1];
            for (var other = 0; other < following.Length; other++)
            {
                if (other == at) continue;

                var arriving = ends[other * 2];
                var apartM = Vector2.Distance(arriving.EdgeM, leaving.EdgeM);
                if (apartM > MeetM) continue;

                offered.Add((apartM, Spline.WrapRad(arriving.AlongRad - leaving.AlongRad), at, other));
            }
        }

        // <b>Nearest first over the whole town, and the turn only between equals.</b> Taken in the order the
        // stretches happen to be numbered, an end that had a partner a millimetre away lost it to one that
        // asked first from half a metre off, and four ends in five were left over. The distance is compared
        // to the rounding the crossings are found to, so the ends that really are one point are ranked by
        // the turn between them and by nothing else.
        offered.Sort((one, other) =>
        {
            var by = MathF.Round(one.ApartM / Kerbs.RoundingM).CompareTo(MathF.Round(other.ApartM / Kerbs.RoundingM));
            return by != 0 ? by : one.TurnRad.CompareTo(other.TurnRad);
        });

        foreach (var (apartM, _, at, other) in offered)
        {
            if (following[at] != Nowhere || taken[other]) continue;

            following[at] = other;
            taken[other] = true;
            reading.Met(apartM);
        }
    }

    /// <summary>
    /// <b>What the meetings leave over, paired off nearest first and only across the driven ground.</b> The
    /// back of a car park is what it is for: the bays between the outermost two lie inside their neighbours,
    /// nothing is driven along their ends, and the outside arrives down one stem and has to cross to the
    /// other.
    /// </summary>
    /// <remarks>
    /// <b>The ground is the whole of the constraint, and a distance would not do.</b> Paired on nearness
    /// alone, an end whose own partner had already been taken reached for whatever was left and a car park
    /// came back joined to a junction three streets away, straight over the grass.
    /// </remarks>
    static void Cut(Walk walk, List<Stretch> outer, End[] ends, int[] following, bool[] taken)
    {
        var offered = new List<(float ApartM, int At, int Other)>();
        for (var at = 0; at < following.Length; at++)
        {
            if (following[at] != Nowhere) continue;

            for (var other = 0; other < taken.Length; other++)
            {
                if (taken[other] || other == at) continue;

                // <b>Ranked on the edges and not on the lines.</b> The join is a length of the boundary and
                // the boundary runs along the edges; the straight between the lines is only how it is drawn,
                // and it cuts corners the boundary does not. Read off the lines, the two sides of one bay are
                // one point and every bay closes on itself.
                var apartM = Handing(ends, at, other);
                if (apartM > OfferedWithinM(walk, outer, at, other)
                    || !Fits(walk, outer, ends, at, other))
                {
                    continue;
                }

                offered.Add((apartM, at, other));
            }
        }

        offered.Sort((one, other) =>
        {
            var by = Capping(walk, outer, one.At, one.Other)
                .CompareTo(Capping(walk, outer, other.At, other.Other));
            return by != 0 ? by : one.ApartM.CompareTo(other.ApartM);
        });

        foreach (var (_, at, other) in offered)
        {
            if (following[at] != Nowhere || taken[other]) continue;

            following[at] = other;
            taken[other] = true;
        }

        Swap(walk, outer, ends, following, taken);
    }

    /// <summary>
    /// <b>What is still over, taken back off whoever holds it.</b> An end left with nothing is usually not
    /// short of a partner but short of <em>its own</em>: the one it needs was taken, first come, by an end
    /// that had somewhere else to go. So the holder is asked to go there instead.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the last of the arithmetic and it is worth the little it costs, because <b>one end left over
    /// is a shell that does not close</b>, and a shell that does not close is no shell at all. Nothing is
    /// joined here that would not have been joined by the pass before it — the swap is between two joins
    /// the ground already allows.
    /// </para>
    /// <para>
    /// <b>And of every swap the ground allows, the one that costs the fewest metres.</b> Taken in the order
    /// the stretches happen to be numbered, the first legal swap is as likely as not to give up a hand-over
    /// whose two edges met exactly and send its holder clean across a junction — a hole moved rather than
    /// shut. What it costs is what the two new joins are longer than the one they replace, and the cheapest
    /// of those is what a left-over end is worth going to.
    /// </para>
    /// </remarks>
    static void Swap(Walk walk, List<Stretch> outer, End[] ends, int[] following, bool[] taken)
    {
        // Who holds each end, so a holder is looked up rather than searched for, and the ends nobody holds,
        // which is the whole of what a displaced holder may be sent to — as many as there are left over.
        var heldBy = new int[following.Length];
        Array.Fill(heldBy, Nowhere);
        for (var at = 0; at < following.Length; at++)
        {
            if (following[at] != Nowhere) heldBy[following[at]] = at;
        }

        var spares = new List<int>();
        for (var again = true; again;)
        {
            again = false;
            spares.Clear();
            for (var spare = 0; spare < taken.Length; spare++)
            {
                if (!taken[spare]) spares.Add(spare);
            }

            for (var at = 0; at < following.Length && !again; at++)
            {
                if (following[at] != Nowhere) continue;

                if (!Cheapest(walk, outer, ends, following, spares, heldBy, at, out var other, out var spare))
                {
                    continue;
                }

                var holder = heldBy[other];
                following[holder] = spare;
                taken[spare] = true;
                heldBy[spare] = holder;
                following[at] = other;
                heldBy[other] = at;
                again = true;
            }
        }
    }

    /// <summary>
    /// The swap that shuts one left-over end for the fewest metres of hand-over: which end it takes, and
    /// where the holder of that end goes instead.
    /// </summary>
    /// <remarks>
    /// <b>The ground is asked last</b> (<see cref="Fits"/>), and only of a pair that would beat what has
    /// already been found: the question costs a walk of two straights, and every end in a city weighed
    /// against every other is a walk of the whole town squared.
    /// </remarks>
    static bool Cheapest(
        Walk walk, List<Stretch> outer, End[] ends, int[] following, List<int> spares, int[] heldBy, int at,
        out int takes, out int sends)
    {
        var bestM = float.PositiveInfinity;
        takes = Nowhere;
        sends = Nowhere;

        for (var other = 0; other < following.Length; other++)
        {
            if (other == at) continue;

            var takingM = Handing(ends, at, other);
            if (takingM > OfferedWithinM(walk, outer, at, other)) continue;

            var holder = heldBy[other];
            if (holder < 0) continue;

            var gaveUpM = Handing(ends, holder, other);
            foreach (var spare in spares)
            {
                if (spare == holder) continue;

                var costM = takingM + Handing(ends, holder, spare) - gaveUpM;
                if (costM >= bestM) continue;
                if (!Fits(walk, outer, ends, at, other) || !Fits(walk, outer, ends, holder, spare)) continue;

                bestM = costM;
                takes = other;
                sends = spare;
            }
        }

        return takes != Nowhere;
    }

    /// <summary>How far one stretch's leaving edge stands from another's arriving edge, which is the whole of
    /// what a hand-over between them has to cover.</summary>
    static float Handing(End[] ends, int at, int other) =>
        Vector2.Distance(ends[(at * 2) + 1].EdgeM, ends[other * 2].EdgeM);

    /// <summary>
    /// <b>Whether a join is a line capped on its own other edge</b>, which is what the outside does at the
    /// end of a road that really stops — and the last thing it should do anywhere else.
    /// </summary>
    /// <remarks>
    /// A cap is always the nearest join on offer, being a band's width and nothing more, so ranked on
    /// distance alone it wins wherever it is legal at all. At a junction mouth it is legal and wrong: the
    /// lane's edge stopped because the junction's ground covered it, and the outside carries on round the
    /// movements that cross the box — but the lane took its own other edge first and the corner past it was
    /// left to whatever happened to be there. Behind every other join, a cap is still taken wherever it is
    /// the only one, which is the road that really does stop.
    /// <b>And the other edge of a road is the other lane's</b> (<see cref="LaneLines.LaneReverse"/>) as much
    /// as it is the line's own. Read as one line against itself, a two-way street running into a car park
    /// capped clean across its own carriageway at the mouth — nearer, by a lane's width, than carrying on
    /// round the bays — and the outside stopped there with the lot left unwrapped.
    /// <b>But a cap is the join that crosses a carriageway and not every join within one</b>: one edge of a
    /// road, covered for a few metres by a way crossing it and clear again past that, hands over to
    /// <em>itself</em>, and that step runs along the boundary rather than across it. Ranked as a cap it went
    /// behind every join in the town, a bay way fourteen metres off took the end first, and the ring left the
    /// row of bays on a straight over the middle of the lot and came back.
    /// </remarks>
    static int Capping(Walk walk, List<Stretch> outer, int at, int other)
    {
        var leaving = outer[at];
        var arriving = outer[other];
        if (!walk.OneCarriageway(leaving.Line, arriving.Line)) return 0;

        // One side of a road is the left edge of one of its lanes and the right edge of the other, the two
        // running opposite ways; read on a single line it is the same hand at both ends. So the two ends are
        // on opposite sides — which is what a cap crosses — exactly where that agreement fails.
        return (leaving.OnTheLeft == arriving.OnTheLeft) == (leaving.Line != arriving.Line) ? 1 : 0;
    }

    /// <summary>
    /// <b>Whether one stretch can carry the outside on into another</b>, which is what both the pairing and
    /// the swapping ask. <b>Two kinds of join, and the second is only offered where the ground is cut</b>
    /// (<see cref="Walk.Cut"/>): the two lines cross where this corner is and nothing is drawn between them
    /// at all; or the ground is cut at both ends and carries the straight between them — between the two band
    /// edges, which is the boundary itself, or between the two <em>lines</em>, which is what a ring actually
    /// draws where that fails.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A straight is the answer at a cut and nowhere else.</b> The end of a road, the back of a car park:
    /// there the band's square end really is the edge of the ground and the outside has to cross it. A lane
    /// at a junction mouth has stopped only as a line — the box carries the ground on and the outside goes
    /// round the movements crossing it — but from the edges alone the two look alike, and the cap across the
    /// carriageway is the nearer join of the two. Offered wherever the tarmac would hold it up, every mouth
    /// in the town took it and the perimeter came back as a rectangle round each street.
    /// </para>
    /// <para>
    /// <b>The edges are where the boundary runs and the lines are what is drawn</b>, and a junction corner is
    /// where the two part company. Two kerbs stop a road apart round the corner of a block, so the straight
    /// between those two edges crosses the pavement the corner goes round and the ground refuses it; the
    /// straight between the two lines is half a carriageway inside the tarmac the whole way and the ground
    /// carries it. Asked of the edges alone, every such corner was refused and the lane, left with nowhere to
    /// go, capped on its own other edge at the mouth of the junction and the outside stopped dead there.
    /// </para>
    /// </remarks>
    static bool Fits(Walk walk, List<Stretch> outer, End[] ends, int at, int other)
    {
        var fromM = ends[(at * 2) + 1].EdgeM;
        var toM = ends[other * 2].EdgeM;
        if (Vector2.Distance(fromM, toM) > OfferedWithinM(walk, outer, at, other)) return false;

        // The crossing is asked first because it is the join that draws nothing: where the two lines really
        // do cross at the corner, the pair meets at a point and there is no straight for the ground to hold
        // up — and nothing about the ground being cut is being claimed.
        if (walk.Crossing(
                outer[at], outer[other], ends[(at * 2) + 1].AtM, ends[other * 2].AtM, out _, out _, out _))
        {
            return true;
        }

        // And a straight only where the ground is really cut (<see cref="Walk.Cut"/>). A line that carries on
        // past where the outside left it has something driven on from there, and that is what the outside
        // goes round.
        if (!walk.Cut(outer[at], leaving: true) || !walk.Cut(outer[other], leaving: false)) return false;

        // A cap crosses the carriageway the two lines are lanes of, so what it may run in is what the two of
        // them are thick together — which is half that carriageway, and is what a cap is.
        var halfM = walk.HalfM(outer[at].Line) + walk.HalfM(outer[other].Line);
        return Carries(walk, fromM, toM, halfM, halfM * 2f)
               || Carries(
                   walk, walk.CentreAt(outer[at], leaving: true),
                   walk.CentreAt(outer[other], leaving: false), halfM, halfM * 2f);
    }

    /// <summary>
    /// <b>Whether the ground can carry the outside straight from one place to the other</b>: every metre
    /// between them on the driven ground, and none of it further in from the edge of that ground than the
    /// narrower of the two bands it joins is thick.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two together are what leave the back of a car park as the only long join in the town. <b>On the
    /// ground</b> refuses a line struck out over the grass to whatever end happened to be left unpaired.
    /// <b>Near the edge of it</b> refuses the other one: inside a junction everything is driven ground, so on
    /// that test alone two ends on opposite sides of a box were joined by a chord straight through the middle
    /// of the road.
    /// </para>
    /// <para>
    /// <b>How deep is read off the ground and not off the bands</b> (<see cref="Kerbs.OffTheDrivenM"/>). A
    /// join skirts the boundary and is a chord of whatever corner it turns, so what says it has left the
    /// boundary is how far in it goes — and a band's own half width is that, being the thickness of the
    /// thing whose edge it is drawn from. Asked instead whether it lay inside <em>some other</em> band, every
    /// join a junction needs was refused: the wedge between two arms is paved back to a fillet
    /// (TER-5) and the movements crossing the box run under it, so the straight from a lane's kerb to the
    /// next lane's kerb — the corner itself, a couple of metres of it — crosses a band the whole way.
    /// </para>
    /// <para>
    /// <b>Neither of the join's own two ends is asked about</b> (<paramref name="skipM"/>). A join sets off
    /// from a line's own edge and runs beside it, and a stretch stops where its neighbour covers its edge
    /// rather than where its own line ends, so its line carries on past the join and the first metres of the
    /// straight run inside it.
    /// </para>
    /// </remarks>
    static bool Carries(Walk walk, Vector2 fromM, Vector2 toM, float deepestM, float skipM)
    {
        var runM = toM - fromM;
        var lengthM = runM.Length();
        if (lengthM <= Kerbs.OnePlaceM) return true;

        var along = runM / lengthM;

        // <b>However short it is, its middle is asked about.</b> A join shorter than what its two ends are
        // allowed has nothing left to walk once they are skipped, and skipping it whole is how a straight
        // came to be drawn across the open middle of a junction.
        if (lengthM <= skipM * 2f) return walk.Skirts(fromM + (along * lengthM * 0.5f), deepestM);

        for (var atM = skipM; atM <= lengthM - skipM; atM += CrossingStationM)
        {
            if (!walk.Skirts(fromM + (along * atM), deepestM)) return false;
        }

        return true;
    }

    /// <summary>
    /// <b>Every stretch carried along its own line to where that line crosses the one that follows it</b>,
    /// so that what a straight has to cover is only what no lane can.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A stretch stops where its <em>edge</em> goes under the next band, and that is short of where its own
    /// <em>line</em> meets the line taking over — by half a width at a square crossing and by more at a skew
    /// one. Drawn from stop to stop, the difference is a straight over open tarmac with no lane under a
    /// metre of it, which is the one thing this is not allowed to be. Carried to the crossing, the two lines
    /// end at one point and the hand-over is lane to lane with nothing between them at all.
    /// </para>
    /// <para>
    /// <b>The crossing is solved and never guessed</b> (<see cref="Spline.CrossingsM"/>). Read instead as
    /// the point of one line nearest the other's stop, a hand-over is right only where the two meet square:
    /// two lines an angle <c>θ</c> apart put that foot <c>cos θ</c> of a half width past the crossing they
    /// actually make, and every skew junction in the town came back with a spike out of its corner where one
    /// line ran past the meeting and a straight came back to it.
    /// </para>
    /// <para>
    /// <b>Where the two lines do not cross, the corner is the straight between them</b> and not a failure:
    /// two arms of a junction stop at their own mouths and the ground between them is nobody's line, so
    /// there is nothing to be carried to and the corner is what crosses the box.
    /// </para>
    /// <para>
    /// <b>And whatever the corner came out as, it is cut back until it turns forwards</b>
    /// (<see cref="Squared"/>). That is the whole of what a corner has to be, said as arithmetic and not as
    /// a case: neither of the two may run against its own line's travel, however the two lines happen to
    /// meet.
    /// </para>
    /// </remarks>
    static Stretch[] Carried(
        Walk walk, List<Stretch> outer, End[] ends, int[] following, bool[] acrossACut,
        out ShellCorner[] corners, out bool[] dropped, out float[] gaps, ShellReading reading)
    {
        corners = new ShellCorner[outer.Count];
        dropped = new bool[outer.Count];
        gaps = new float[outer.Count];
        Stretch[] carried;
        for (var round = 1; ; round++)
        {
            carried = Carry(walk, outer, ends, following, corners, gaps);
            if (round >= DroppingRounds) break;
            if (!Dropped(carried, following, acrossACut, dropped, reading)) break;
        }

        return carried;
    }

    /// <summary>
    /// <b>Every stretch the carry left with no metres of its own, dropped, and the two either side of it
    /// handed straight to one another.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// A stretch cut back from both ends until the two meet is one the outside never actually runs along:
    /// its corner with the stretch before it stands past its corner with the one after. Left in the ring it
    /// draws nothing, and the ring walks past it — so the straight drawn there spans a pair of lines
    /// <em>nobody solved a corner for</em>, and it lands near the corner rather than on it. That is the
    /// notch, and it is why a hand-over could read as having been carried onto a crossing and still leave a
    /// straight behind.
    /// </para>
    /// <para>
    /// <b>Dropped rather than kept as a point</b>, because a corner is between the two lines that meet
    /// there and this one is not one of them. Dropping can carry away the next stretch along in its turn,
    /// which is why the carry is run again over the shorter ring.
    /// </para>
    /// </remarks>
    static bool Dropped(
        Stretch[] carried, int[] following, bool[] acrossACut, bool[] dropped, ShellReading reading)
    {
        var any = false;
        for (var at = 0; at < carried.Length; at++)
        {
            if (dropped[at] || following[at] == Nowhere) continue;
            if (carried[at].ToM - carried[at].FromM > Kerbs.RoundingM) continue;

            var before = Array.IndexOf(following, at);
            if (before < 0) continue;

            acrossACut[before] |= acrossACut[at];
            following[before] = following[at];
            following[at] = Nowhere;
            dropped[at] = true;
            reading.CarriedAway++;
            any = true;
        }

        return any;
    }

    /// <summary>
    /// One pass of the carry over whatever ring the joins have left, with <paramref name="gaps"/> the
    /// distance each hand-over was left standing open — <b>which is the drawn straight where the ring is
    /// strung the way the carry left it</b>, and is what says whether a notch was made here or later.
    /// </summary>
    static Stretch[] Carry(
        Walk walk, List<Stretch> outer, End[] ends, int[] following, ShellCorner[] corners, float[] gaps)
    {
        var carried = outer.ToArray();
        for (var at = 0; at < outer.Count; at++)
        {
            var next = following[at];
            if (next == Nowhere) continue;

            var crossed = walk.Crossing(
                carried[at], carried[next], ends[(at * 2) + 1].AtM, ends[next * 2].AtM,
                out var atCrossM, out var nextCrossM, out corners[at]);
            if (crossed)
            {
                carried[at] = Onto(carried[at], atCrossM, leaving: true);
                carried[next] = Onto(carried[next], nextCrossM, leaving: false);
            }
            else Settled(walk, carried, at, next);

            Squared(walk, carried, at, next);
            gaps[at] = Vector2.Distance(
                walk.CentreAt(carried[at], leaving: true), walk.CentreAt(carried[next], leaving: false));
        }

        return carried;
    }

    /// <summary>
    /// <b>One end walked onto the other and then the other onto where the first now is</b> — what a corner
    /// between two lines that never cross gets.
    /// </summary>
    /// <remarks>
    /// Two lines that only ever touch — a movement running out of the lane it serves, a bay's way beside its
    /// neighbour — have one nearest pair and no crossing to be carried to, and this is one step of the walk
    /// onto that pair. <b>Taken at the same time instead, each runs to the foot of where the other
    /// <em>was</em></b>, and two ends that both moved are nearest to nothing.
    /// </remarks>
    static void Settled(Walk walk, Stretch[] carried, int at, int next)
    {
        carried[at] = Onto(
            carried[at],
            walk.FootM(carried[at], walk.CentreAt(carried[next], leaving: false), leaving: true),
            leaving: true);
        carried[next] = Onto(
            carried[next],
            walk.FootM(carried[next], walk.CentreAt(carried[at], leaving: true), leaving: false),
            leaving: false);
    }

    /// <summary>
    /// <b>The corner cut back until it turns forwards</b>: whichever of its two ends the straight between
    /// them runs backwards from is taken to the foot of the other, which is the nearest place it stops
    /// running backwards.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the whole statement of what a corner is</b>, and it holds of every hand-over there is
    /// rather than of the ones somebody thought of. A ring that leaves one line, crosses to the next and
    /// arrives on it pointing back down the way it came has drawn the same metres twice and left a spike
    /// standing out of the corner; the two dot products are that, said as arithmetic, and cutting to the
    /// foot is the least that answers them.
    /// </para>
    /// <para>
    /// <b>Both corrections shorten</b>, so nothing here can carry a stretch over ground its own edge never
    /// bounded, and the rounds terminate. <b>And backwards is by more than two ends that are one place may
    /// stand apart</b> (<see cref="Kerbs.OnePlaceM"/>): where the outside really does reverse — the cap on
    /// the end of a band, the back of a car park — it crosses its own straight square on, and a corner
    /// square to within a float would otherwise be nibbled at every round.
    /// </para>
    /// </remarks>
    static void Squared(Walk walk, Stretch[] carried, int at, int next)
    {
        for (var round = 0; round < SquaringRounds; round++)
        {
            var fromM = walk.CentreAt(carried[at], leaving: true);
            var toM = walk.CentreAt(carried[next], leaving: false);
            var runM = toM - fromM;

            if (Vector2.Dot(runM, walk.Travel(carried[at], leaving: true)) < -Kerbs.OnePlaceM)
            {
                carried[at] = Onto(carried[at], walk.FootM(carried[at], toM, leaving: true), leaving: true);
                continue;
            }

            if (Vector2.Dot(runM, walk.Travel(carried[next], leaving: false)) < -Kerbs.OnePlaceM)
            {
                carried[next] = Onto(
                    carried[next], walk.FootM(carried[next], fromM, leaving: false), leaving: false);
                continue;
            }

            return;
        }
    }

    /// <summary>
    /// One end of one stretch taken to an exact distance along its own line, kept the right side of the
    /// stretch's other end.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It cuts as readily as it carries.</b> Where two lines have crossed before either edge stopped
    /// being the outside, the corner stands behind the stop, and a stretch that keeps the metres past it
    /// draws them twice. Allowed only to carry, the town met at a point fewer times than the nearest point
    /// managed.
    /// </para>
    /// <para>
    /// <b>And it carries past the line's own ends</b>, since the corner between two arms of a junction
    /// stands on neither of them. What bounds it is the crossing that was solved (<see cref="Walk.Crossing"/>)
    /// and not a clamp here; the only thing kept out is the stretch's own other end, because a stretch
    /// carried through itself is a ring turned inside out.
    /// </para>
    /// </remarks>
    static Stretch Onto(Stretch stretch, float atM, bool leaving) =>
        stretch.OnTheLeft == leaving
            ? stretch with { ToM = MathF.Max(atM, stretch.FromM) }
            : stretch with { FromM = MathF.Min(atM, stretch.ToM) };

    /// <summary>
    /// <b>A line's own arcs with its first and last run on past their own ends</b>, so a stretch carried to
    /// a corner standing off the end of its line is cut out of it like any other.
    /// </summary>
    /// <remarks>
    /// <b>The end piece is run on and never a tangent struck off it.</b> An arc's own circle carries on
    /// turning, and a straight laid off the end of a bend instead cuts inside the very lane the shell was
    /// asked to be the outside of — which is the same reason a join is added rather than bent in
    /// (<see cref="Bridge"/>).
    /// </remarks>
    static ReadOnlySpan<ArcSeg> RunOn(ReadOnlySpan<ArcSeg> line, float behindM, float pastM, ArcSeg[] into)
    {
        if (behindM <= 0f && pastM <= 0f) return line;

        line.CopyTo(into);
        if (behindM > 0f)
        {
            var first = into[0];
            into[0] = new ArcSeg(
                first.PointAtM(-behindM), first.HeadingAtRad(-behindM), first.LengthM + behindM,
                first.Curvature);
        }

        if (pastM > 0f)
        {
            var last = into[line.Length - 1];
            into[line.Length - 1] = last with { LengthM = last.LengthM + pastM };
        }

        return into.AsSpan(0, line.Length);
    }

    /// <summary>
    /// <b>The stretches strung into rings.</b> Each is cut out of the line it is a stretch of and laid the
    /// way it is walked, and between one and the next goes the straight from where the first stops to where
    /// the second starts.
    /// </summary>
    static (ArcSeg[][] Chains, int[][] LineOfArc) Strung(
        Walk walk, Paving paving, List<Stretch> outer, End[] ends, int[] following, bool[] acrossACut,
        float leastRoundM, ShellReading reading)
    {
        var carried = Carried(
            walk, outer, ends, following, acrossACut, out var corners, out var dropped, out var gaps,
            reading);

        for (var at = 0; at < outer.Count; at++)
        {
            reading.Stands(new ShellStretch(
                at, outer[at].Line, outer[at].OnTheLeft, carried[at].FromM, carried[at].ToM,
                ends[at * 2].EdgeM, ends[(at * 2) + 1].EdgeM, following[at], corners[at], acrossACut[at],
                dropped[at], gaps[at]));
        }

        var pieces = new ArcSeg[outer.Count][];
        var room = new ArcSeg[64];
        var reversed = new ArcSeg[64];
        var runOn = new ArcSeg[64];
        for (var at = 0; at < outer.Count; at++)
        {
            // A stretch the carry left with nothing was handed over past, so it is no part of any ring.
            if (dropped[at])
            {
                pieces[at] = [];
                continue;
            }

            var line = paving.ArcsOfDriven(outer[at].Line);
            if (room.Length < line.Length + 2)
            {
                room = new ArcSeg[line.Length + 2];
                reversed = new ArcSeg[line.Length + 2];
                runOn = new ArcSeg[line.Length + 2];
            }

            var behindM = MathF.Max(0f, -carried[at].FromM);
            var cut = Spline.SubChainInto(
                RunOn(line, behindM, MathF.Max(0f, carried[at].ToM - walk.LengthM(outer[at].Line)), runOn),
                carried[at].FromM + behindM,
                carried[at].ToM + behindM,
                room);
            if (cut > 0 && !outer[at].OnTheLeft)
            {
                Spline.ReverseInto(room.AsSpan(0, cut), reversed);
                pieces[at] = reversed.AsSpan(0, cut).ToArray();
            }
            else pieces[at] = cut > 0 ? room.AsSpan(0, cut).ToArray() : [];
        }

        var lineOf = new int[outer.Count];
        for (var at = 0; at < outer.Count; at++) lineOf[at] = outer[at].Line;

        var chains = new List<(ArcSeg[] Arcs, int[] LineOfArc, int Run)>();
        var strung = new bool[outer.Count];
        var run = new List<ArcSeg>();
        var ranAlong = new List<int>();
        var walked = new List<int>();

        // <b>From what nothing leads to first.</b> A stretch the joins left with no stretch before it is the
        // start of a run, and entered anywhere else that run comes back cut in two.
        var ledTo = new bool[outer.Count];
        foreach (var next in following)
        {
            if (next != Nowhere) ledTo[next] = true;
        }

        for (var at = 0; at < outer.Count; at++)
        {
            if (!ledTo[at])
            {
                Ring(walk, pieces, following, acrossACut, corners, gaps, strung, lineOf, at, leastRoundM,
                    run, ranAlong, walked, chains, reading);
            }
        }

        for (var at = 0; at < outer.Count; at++)
        {
            Ring(walk, pieces, following, acrossACut, corners, gaps, strung, lineOf, at, leastRoundM, run,
                ranAlong, walked, chains, reading);
        }

        return Shut(chains, leastRoundM, reading);
    }

    /// <summary>
    /// One ring, walked, shut over any gap a road could be, and kept if it is longer round than the town's
    /// widest band. <b>One figure decides both</b>, and it is the town's own: nothing narrower than a road
    /// is a feature of the shape of one.
    /// </summary>
    static void Ring(
        Walk walk, ArcSeg[][] pieces, int[] following, bool[] acrossACut, ShellCorner[] corners,
        float[] gaps, bool[] strung, int[] lineOf, int entry, float leastRoundM, List<ArcSeg> into,
        List<int> ranAlong, List<int> walked, List<(ArcSeg[] Arcs, int[] LineOfArc, int Run)> chains,
        ShellReading reading)
    {
        String(
            pieces, following, acrossACut, corners, gaps, strung, lineOf, entry, into, ranAlong, walked,
            reading);
        if (into.Count == 0) return;

        // <b>Shut over a gap a road could be and no wider.</b> Where the outside really does stop — a fault
        // in the ground under it, not in the walking of it — the two ends are streets apart, and a straight
        // drawn between them is a kilometre of perimeter over nothing, which is a worse answer than saying
        // it stopped.
        // Read before the ring is shut, because shutting it makes the two one point.
        var headM = into[0].StartM;
        var tailM = into[^1].EndM;
        var gapM = Vector2.Distance(tailM, headM);
        var shutOverAGap = gapM > Kerbs.RoundingM && gapM <= leastRoundM;
        var bareM = shutOverAGap ? Bare(walk, tailM, headM) : 0f;
        if (gapM <= leastRoundM) Close(into, ranAlong);

        var arcs = into.ToArray();
        var run = reading.Ran(new ShellRun
        {
            Stretches = walked.Count,
            Lines = Named(lineOf, walked),
            LengthM = Spline.TotalLengthM(arcs),
            GapM = gapM,
            HeadM = headM,
            TailM = tailM,
            ShutOverAGap = shutOverAGap,
            BareGapM = bareM,
        });

        if (reading.Runs[run].LengthM < leastRoundM)
        {
            reading.Verdict(run, kept: false, "shorter round than the town's widest road is wide");
            return;
        }

        chains.Add((arcs, [.. ranAlong], run));
    }

    /// <summary>The driven lines a run was strung out of, each once and in order.</summary>
    static int[] Named(int[] lineOf, List<int> walked)
    {
        var lines = new SortedSet<int>();
        foreach (var at in walked) lines.Add(lineOf[at]);

        return [.. lines];
    }

    /// <summary>
    /// How many metres of the straight that shuts a ring run over ground nothing is driven on.
    /// </summary>
    /// <remarks>
    /// <b>A reading with no rule behind it.</b> Every other join in the shell is refused unless the ground
    /// carries it (<see cref="Carries"/>) and the one that shuts a ring is asked nothing at all, so what
    /// that costs is measured here rather than assumed either way.
    /// </remarks>
    static float Bare(Walk walk, Vector2 fromM, Vector2 toM)
    {
        var runM = toM - fromM;
        var lengthM = runM.Length();
        if (lengthM <= Kerbs.OnePlaceM) return 0f;

        var along = runM / lengthM;
        var bareM = 0f;
        for (var atM = 0f; atM <= lengthM; atM += CrossingStationM)
        {
            if (!walk.OverTarmac(fromM + (along * atM))) bareM += CrossingStationM;
        }

        return MathF.Min(bareM, lengthM);
    }

    /// <summary>One ring's arcs, walked from a stretch through everything that follows it.</summary>
    static void String(
        ArcSeg[][] pieces, int[] following, bool[] acrossACut, ShellCorner[] corners, float[] gaps,
        bool[] strung, int[] lineOf, int entry, List<ArcSeg> into, List<int> ranAlong, List<int> walked,
        ShellReading reading)
    {
        into.Clear();
        ranAlong.Clear();
        walked.Clear();

        var at = entry;
        var came = Nowhere;
        while (at != Nowhere && !strung[at])
        {
            strung[at] = true;
            walked.Add(at);
            if (pieces[at].Length > 0)
            {
                if (into.Count > 0)
                {
                    var last = into[^1];
                    var fromM = last.EndM;
                    var toM = pieces[at][0].StartM;
                    reading.Handed(
                        lineOf[came], lineOf[at], fromM, toM,
                        Turn(last.HeadingAtRad(last.LengthM), fromM, toM, pieces[at][0].HeadingRad),
                        acrossACut[came], corners[came], gaps[came]);
                    if (Apart(fromM, toM) && !Doubles(last, fromM, toM))
                    {
                        into.Add(Bridge(fromM, toM));
                        ranAlong.Add(Nowhere);
                    }
                }

                into.AddRange(pieces[at]);
                for (var arc = 0; arc < pieces[at].Length; arc++) ranAlong.Add(lineOf[at]);

                came = at;
            }

            at = following[at];
        }

        // <b>The hand-over that shuts a ring is one like any other</b>, and the walk stops before it: what
        // follows the last stretch has already been strung, and for a ring that closes it is the stretch the
        // walk set off from. Left unsaid, the one corner a ring turns onto itself answers to nothing.
        if (came != Nowhere && at == entry && into.Count > 0)
        {
            var last = into[^1];
            reading.Handed(
                lineOf[came], lineOf[entry], last.EndM, into[0].StartM,
                Turn(last.HeadingAtRad(last.LengthM), last.EndM, into[0].StartM, into[0].HeadingRad),
                acrossACut[came], corners[came], gaps[came]);
        }
    }

    /// <summary>
    /// The sharpest of the corners one hand-over turns: <b>the straight between the two is a corner at each
    /// of its ends</b>, and a stretch carried past the meeting with the straight coming back to it turns a
    /// half turn onto that straight and a half turn off it while arriving pointed much as it left.
    /// </summary>
    static float Turn(float leavingRad, Vector2 fromM, Vector2 toM, float arrivingRad)
    {
        // <b>A straight shorter than two ends that are one place has no direction to read.</b> A crossing
        // is solved to a few millimetres at a town's own coordinates, which is all a float carries there,
        // so a hand-over that met at its crossing still leaves a hair of a straight — and the way that hair
        // happens to point is noise, not a corner the ring turns.
        var runM = toM - fromM;
        if (runM.LengthSquared() <= Kerbs.OnePlaceM * Kerbs.OnePlaceM)
        {
            return Spline.WrapRad(arrivingRad - leavingRad);
        }

        var straightRad = MathF.Atan2(runM.Y, runM.X);
        var onto = Spline.WrapRad(straightRad - leavingRad);
        var off = Spline.WrapRad(arrivingRad - straightRad);
        return MathF.Abs(onto) > MathF.Abs(off) ? onto : off;
    }

    /// <summary>
    /// <b>The rings that shut</b>, and that go round something at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every side of the ground is the outside of it.</b> A street grid bounds what it lays on the
    /// outside and round every block it encloses, and both are the edge of the driven ground: a plain
    /// two-lane street is <em>both</em> its lanes, one carried by the ring round the town and the other by
    /// the ring round the block behind it. Keeping the outermost alone left every such street marked down
    /// one side, which is half an answer to the question the layer is opened for.
    /// </para>
    /// <para>
    /// <b>A ring goes round something.</b> A notch shut on itself is a sliver of no width, and it would pass
    /// on its length alone — as long round as a road is wide twice over, being one length of edge and the
    /// straight back along it.
    /// </para>
    /// </remarks>
    static (ArcSeg[][] Chains, int[][] LineOfArc) Shut(
        List<(ArcSeg[] Arcs, int[] LineOfArc, int Run)> chains, float leastRoundM, ShellReading reading)
    {
        var kept = new List<ArcSeg[]>();
        var ranAlong = new List<int[]>();
        foreach (var (arcs, lineOfArc, run) in chains)
        {
            if (arcs.Length == 0 || Apart(arcs[^1].EndM, arcs[0].StartM))
            {
                reading.Verdict(run, kept: false, "would not close");
                continue;
            }

            if (MathF.Abs(Over(arcs)) < leastRoundM * leastRoundM)
            {
                reading.Verdict(run, kept: false, "goes round nothing");
                continue;
            }

            reading.Verdict(run, kept: true, "kept");
            kept.Add(arcs);
            ranAlong.Add(lineOfArc);
        }

        return ([.. kept], [.. ranAlong]);
    }

    /// <summary>
    /// Twice the area a ring encloses, signed — the arcs read as the straights between their ends, which is
    /// all that telling a shell from a sliver needs.
    /// </summary>
    static float Over(ArcSeg[] chain)
    {
        var twiceM = 0f;
        foreach (var arc in chain)
        {
            twiceM += (arc.StartM.X * arc.EndM.Y) - (arc.EndM.X * arc.StartM.Y);
        }

        return twiceM;
    }

    /// <summary>
    /// <b>The straight from where one stretch stops to where the next starts</b>, so a ring's arcs run end
    /// to end with no point between them belonging to nothing.
    /// </summary>
    /// <remarks>
    /// <b>Added, never bent in.</b> Reaching the two ends onto one point by rebuilding their arcs through it
    /// rewrites geometry that was measured: a stretch a single arc long is <em>replaced</em> by the chord
    /// between two welds, and a chord across a bend cuts inside the very lane the shell was asked to stay
    /// outside of. Every arc a ring holds is the town's own, and what a join adds is the metre or so between
    /// them.
    /// </remarks>
    static ArcSeg Bridge(Vector2 fromM, Vector2 toM)
    {
        var runM = toM - fromM;
        return new ArcSeg(fromM, MathF.Atan2(runM.Y, runM.X), runM.Length(), 0f);
    }

    /// <summary>The ring brought back to where it set off.</summary>
    static void Close(List<ArcSeg> into, List<int> ranAlong)
    {
        if (into.Count == 0 || !Apart(into[^1].EndM, into[0].StartM)) return;

        // <b>And this one is laid however it runs</b>, unlike the hand-overs inside the ring
        // (<see cref="Doubles"/>): it is the piece that makes the ring shut, and a ring that does not shut is
        // thrown away whole. Skipping a backwards centimetre here cost six rings and three and a half
        // kilometres of boundary on a city — which is a seam traded for six blocks having no edge at all.
        into.Add(Bridge(into[^1].EndM, into[0].StartM));
        ranAlong.Add(Nowhere);
    }

    static bool Apart(Vector2 oneM, Vector2 otherM) =>
        Vector2.DistanceSquared(oneM, otherM) > Kerbs.RoundingM * Kerbs.RoundingM;

    /// <summary>
    /// <b>Whether the straight between two ends would double the ring back on itself</b>: the next stretch
    /// starts <em>behind</em> where the last one stopped, by less than the two ends are allowed to stand
    /// apart (<see cref="Kerbs.OnePlaceM"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A centimetre of backwards straight is a seam and not a corner, and the extrusion cannot tell.</b>
    /// A stretch carried past its meeting stops a centimetre beyond where the next one starts, so the
    /// straight between them points back the way the ring came and the walk turns a half circle onto it and
    /// a half circle off it (<see cref="Turn"/>, which has always said so). Moved outward, the two half
    /// turns throw their sides of the seam to <em>opposite</em> sides of the line — the offset is taken to
    /// the left of travel and the travel reversed — so a centimetre here is a needle
    /// <c>2 × (band + distance)</c> long across the road out there: three and a half metres on the kerb,
    /// seven on a line struck a lane's half beyond it. Nothing downstream can remove it, every station of it
    /// standing honestly clear of every line.
    /// </para>
    /// <para>
    /// <b>Bounded by one place and not by a taste.</b> Within that the two ends <em>are</em> one point
    /// (<see cref="Kerbs.OnePlaceM"/>) and there is nothing between them to draw; beyond it a straight that
    /// runs back is real line the ring has to cover, and dropping it would leave a hole rather than a seam.
    /// The ring is left with the two ends a centimetre apart and no arc between them, which is what the
    /// readers of it already do with an arc's two ends — a chain of arc starts (<c>RingField</c>), a station
    /// walk per arc (<c>Extrusion</c>) and a fill of sampled points (<c>GroundMesh</c>) all close it without
    /// being told.
    /// </para>
    /// </remarks>
    static bool Doubles(in ArcSeg arriving, Vector2 fromM, Vector2 toM)
    {
        var runM = toM - fromM;
        if (runM.LengthSquared() > Kerbs.OnePlaceM * Kerbs.OnePlaceM) return false;

        return Vector2.Dot(runM, Heading.Unit(arriving.HeadingAtRad(arriving.LengthM))) < 0f;
    }

    /// <summary>The question asked of one station, with everything it needs to ask it held once.</summary>
    readonly struct Walk(
        Paving paving, ChainIndex bands, float[] halfM, float[] lengthM, float mostHalfM,
        float cornerReachMaxM, bool[] stops, BandEdges edges)
    {
        /// <summary>
        /// How many bands may have an edge at one point before the answer stops being the whole one. Six
        /// ways off one bay converge on a pose, and a box carries a movement per pair of arms; this is that
        /// with room, and past it the point is left to whoever else claims it.
        /// </summary>
        const int MostBandsNear = 64;

        /// <summary>
        /// How many of the places two lines cross are weighed before the nearest that reaches is taken as
        /// the corner. Eight: a lane and the movement leaving it cross where the movement pulls out and
        /// again where it comes back, and nothing in a town crosses one line more often than a handful.
        /// </summary>
        const int MostCrossings = 8;


        /// <summary>
        /// <b>Whether a place is on the driven ground at all.</b> It is asked of a straight somebody wants to
        /// carry the outside along, and what it has to tell apart is the back of a car park, where the bays'
        /// own ends are the ground, from open grass.
        /// </summary>
        public bool OverTarmac(Vector2 pointM) => paving.Kerbs.OffTheDrivenM(pointM) <= Kerbs.JoinedM;

        /// <summary>
        /// <b>Where one edge of one line really stops being the outside</b>, solved against the bands
        /// standing there (<see cref="BandEdges.CutM"/>) — false where none of them explains it.
        /// </summary>
        public bool CutM(int line, bool onTheLeft, float clearM, float coveredM, out float atM) =>
            edges.CutM(line, onTheLeft, clearM, coveredM, out atM);

        /// <summary>Half the width of the band one line lays.</summary>
        public float HalfM(int line) => halfM[line];

        /// <summary>The metres one line runs, end to end.</summary>
        public float LengthM(int line) => lengthM[line];

        /// <summary>
        /// <b>Where a line stands at a distance along it, its end pieces run on past their own ends</b> —
        /// which is where a stretch carried to a corner standing off the end of its line has got to.
        /// </summary>
        public SplineSample At(int line, float atM)
        {
            var arcs = paving.ArcsOfDriven(line);
            if (atM < 0f)
            {
                return new SplineSample(arcs[0].PointAtM(atM), arcs[0].HeadingAtRad(atM), arcs[0].Curvature);
            }

            var overM = atM - lengthM[line];
            if (overM <= 0f) return Spline.SampleAt(arcs, atM);

            var last = arcs[^1];
            var pastM = last.LengthM + overM;
            return new SplineSample(last.PointAtM(pastM), last.HeadingAtRad(pastM), last.Curvature);
        }

        /// <summary>
        /// <b>Whether the ground is really cut at one end of one stretch</b>, which is what says a straight
        /// is the join there.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Two ways a stretch stops, and only one of them is a cut.</b> One that stops at its own line's
        /// end is cut where nothing is driven on from there (<see cref="LaneShell.Stops"/>) — a dead end, the
        /// back of a bay — and where something is, the ground carries on and the outside goes round it.
        /// </para>
        /// <para>
        /// <b>One that stops in the middle of its line stopped because a neighbour covered its edge</b>, and
        /// the step across to that neighbour is a length of boundary whatever either line does further on.
        /// </para>
        /// </remarks>
        public bool Cut(Stretch stretch, bool leaving)
        {
            var atM = stretch.OnTheLeft == leaving ? stretch.ToM : stretch.FromM;
            if (atM <= Kerbs.OnePlaceM) return stops[stretch.Line * 2];

            return atM < lengthM[stretch.Line] - Kerbs.OnePlaceM || stops[(stretch.Line * 2) + 1];
        }

        /// <summary>
        /// <b>Whether two lines are one carriageway</b>: the same line, or a lane and the one running the
        /// other way down the same stretch of road.
        /// </summary>
        public bool OneCarriageway(int one, int other)
        {
            if (one == other) return true;

            var lanes = paving.Lanes;
            return one < lanes.LaneCount && other < lanes.LaneCount && lanes.LaneReverse[one] == other;
        }

        /// <summary>The line's own point at one end of a stretch, which is where a hand-over off it stands.</summary>
        public Vector2 CentreAt(Stretch stretch, bool leaving) =>
            At(stretch.Line, stretch.OnTheLeft == leaving ? stretch.ToM : stretch.FromM).PositionM;

        /// <summary>
        /// <b>The way the outside is travelling at one end of a stretch</b>: the line's own direction where
        /// the stretch is walked with it, and against it where the clear edge is the line's right.
        /// </summary>
        public Vector2 Travel(Stretch stretch, bool leaving)
        {
            var on = At(stretch.Line, stretch.OnTheLeft == leaving ? stretch.ToM : stretch.FromM);
            return stretch.OnTheLeft ? on.Direction : -on.Direction;
        }

        /// <summary>
        /// The point of one stretch's line nearest a place, looked for <b>within the stretch's own
        /// metres</b> — a line that comes back past itself stands nearest a corner twice, and only one of
        /// the two is the length of edge being walked.
        /// </summary>
        public float FootM(Stretch stretch, Vector2 pointM, bool leaving) =>
            Spline.ProjectM(
                paving.ArcsOfDriven(stretch.Line),
                pointM,
                stretch.OnTheLeft == leaving ? stretch.ToM : stretch.FromM,
                stretch.ToM - stretch.FromM);

        /// <summary>
        /// <b>Where two lines that hand the outside over to one another cross</b>, as a distance along each
        /// (<see cref="Spline.CrossingsM"/>), or false where the two never cross at a place that could be
        /// this corner.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Which crossing is this corner is settled by the question the stretches were cut with</b>
        /// (<see cref="Reaches"/>) and never by how far off it stands. There is no distance that could say
        /// it: two arms a right angle apart cross half a width past where their edges do, and two that are
        /// all but parallel cross that width divided by however shallowly they meet, which is a street away.
        /// </para>
        /// <para>
        /// <b>The two lines are run on past their own ends to find it</b> (<see cref="Spline.CrossingsM"/>),
        /// because the arms of a junction stop at their own mouths and the corner between them stands on
        /// neither. <b>How far they may be run on is how far out a corner ever stands</b>
        /// (<see cref="SimConfig.JunctionArmReachMaxM"/>) — the town's own reach at the sharpest corner it
        /// allows, which is the same figure that says where an arm's own ground begins. Run on by the
        /// straight between the two stops instead, a right-angled corner whose arms both stop at their
        /// mouths has its crossing a whole box away and comes back as no corner at all; run on without a
        /// bound, an arc is carried round its own circle and answers for a place it never reaches.
        /// </para>
        /// </remarks>
        public bool Crossing(
            Stretch from, Stretch to, float fromStopM, float toStopM, out float atM, out float otherAtM,
            out ShellCorner why)
        {
            atM = 0f;
            otherAtM = 0f;
            why = ShellCorner.Apart;
            if (from.Line == to.Line) return false;

            var one = paving.ArcsOfDriven(from.Line);
            var other = paving.ArcsOfDriven(to.Line);
            var beyondM = cornerReachMaxM;

            Span<SplineCrossing> found = stackalloc SplineCrossing[MostCrossings];
            var count = Spline.CrossingsM(one, other, fromStopM, toStopM, found, beyondM);

            // The nearest candidate's own reason and not the last one's: what the corner was refused for is
            // what refused the crossing it would have turned on.
            var refused = ShellCorner.Apart;
            for (var at = 0; at < count; at++)
            {
                var onOne = At(from.Line, found[at].OneM);
                var onOther = At(to.Line, found[at].OtherM);

                // <b>A crossing is one point, and two distances that name two places are not one.</b> A
                // town's coordinates are thousands of metres and a float carries seven figures, so a
                // solved crossing is right to a few millimetres and no better; anything past that is a
                // piece answering for a point it does not actually stand on, and the corner drawn on it
                // lands near the junction rather than in it.
                if (Vector2.DistanceSquared(onOne.PositionM, onOther.PositionM)
                    > Kerbs.OnePlaceM * Kerbs.OnePlaceM)
                {
                    Threw(ref refused, ShellCorner.Scattered);
                    continue;
                }

                var apartRad = Apart(onOne.HeadingRad, onOther.HeadingRad);
                var fromWithinM = Wedge(apartRad, halfM[from.Line], halfM[to.Line]);
                var toWithinM = Wedge(apartRad, halfM[to.Line], halfM[from.Line]);

                if (MathF.Abs(found[at].OneM - fromStopM) > fromWithinM
                    || MathF.Abs(found[at].OtherM - toStopM) > toWithinM)
                {
                    Threw(ref refused, ShellCorner.Wedged);
                    continue;
                }

                if (!Reaches(from, one, fromStopM, found[at].OneM, leaving: true)
                    || !Reaches(to, other, toStopM, found[at].OtherM, leaving: false))
                {
                    Threw(ref refused, ShellCorner.Covered);
                    continue;
                }

                // <b>And one of the two has to reach it without being run on at all.</b> Past a line's own
                // end there is no band and nothing to ask, so a crossing out there answers to none of the
                // rules above — and where <em>both</em> lines have stopped, what lies between them is the
                // fillet the junction rounds its corner with and no line runs along it. Carried to the
                // crossing of the two extensions anyway, the ring left both lanes at their mouths, ran out
                // into the middle of the box and came back: a spur of perimeter over ground neither lane
                // covers a metre of.
                if (Ran(from.Line, found[at].OneM) && Ran(to.Line, found[at].OtherM))
                {
                    Threw(ref refused, ShellCorner.Beyond);
                    continue;
                }

                atM = found[at].OneM;
                otherAtM = found[at].OtherM;
                why = ShellCorner.Crossed;
                return true;
            }

            why = refused;
            return false;
        }

        /// <summary>The first reason a candidate was thrown out, kept, since the candidates come nearest first.</summary>
        static void Threw(ref ShellCorner refused, ShellCorner why)
        {
            if (refused == ShellCorner.Apart) refused = why;
        }

        /// <summary>Whether a distance along a line stands off the end of it, either end.</summary>
        bool Ran(int line, float atM) => atM < 0f || atM > lengthM[line];

        /// <summary>
        /// <b>How far from a crossing the edge that crosses there stops being the outside</b> — the wedge
        /// between two bands read off the angle the two lines actually cross at
        /// (<see cref="SimConfig.JunctionCornerAlongM"/>), which is the same arithmetic that says where two
        /// kerbs cross.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>It is the whole of the bound on a crossing, and it is measured rather than assumed.</b> Two
        /// arms a right angle apart put the stop half a width past their crossing; two an eighth of a turn
        /// apart put it four widths past; two a degree apart put it a street away and have no corner here at
        /// all. Read at a fixed angle — the sharpest a junction may turn, say — every corner skewer than that
        /// falls outside its own window; read with no bound at all, two lines that run all but together are
        /// carried to wherever they finally touch, which came back as a hundred metres of straight.
        /// </para>
        /// <para>
        /// <b>The turn is folded onto the sharper of its two supplements</b>, since which of them is the
        /// corner's own depends on which way each of the two stretches is walked, and the sharper is the one
        /// that lets a real corner through. <b>And the two half widths are over</b>, which is what a bend
        /// between here and there moves the stop by.
        /// </para>
        /// </remarks>
        static float Wedge(float apartRad, float halfM, float neighbourHalfM)
        {
            var foldedRad = MathF.Min(apartRad, MathF.PI - apartRad);
            return foldedRad <= 0f
                ? float.PositiveInfinity
                : SimConfig.JunctionCornerAlongM(foldedRad, halfM, neighbourHalfM) + halfM + neighbourHalfM;
        }

        /// <summary>How far round two lines stand from one another, as an angle between nothing and a half turn.</summary>
        static float Apart(float oneRad, float otherRad) => MathF.Abs(Spline.WrapRad(otherRad - oneRad));

        /// <summary>
        /// <b>Whether one stretch's own line can be carried to a crossing</b>: past where the edge stopped
        /// being the outside, only while it stays under whatever covered it, and behind that stop, only as
        /// far back as the stretch's own metres go.
        /// </summary>
        /// <remarks>
        /// A stretch stops where its edge goes under somebody else's band, so between that stop and the
        /// corner the edge is covered the whole way — and a crossing with a length of outside standing in
        /// front of it belongs to some other corner, however near it happens to be. It is the same question
        /// the stretches were cut with (<see cref="Outside"/>), asked again of the ground between, so the
        /// answer cannot disagree with the cut. <b>Past the line's own end there is nothing to ask</b>: the
        /// band has stopped, and what the corner out there stands on is ground the two lines share.
        /// </remarks>
        bool Reaches(Stretch stretch, ReadOnlySpan<ArcSeg> arcs, float stopM, float crossM, bool leaving)
        {
            // Which way along the line the covered side lies, which is the way the outside was walking.
            var covered = stretch.OnTheLeft == leaving ? crossM > stopM : crossM < stopM;
            if (!covered)
            {
                return crossM >= stretch.FromM - Kerbs.RoundingM && crossM <= stretch.ToM + Kerbs.RoundingM;
            }

            var toM = MathF.Min(MathF.Max(stopM, crossM), lengthM[stretch.Line]);
            for (var atM = MathF.Max(MathF.Min(stopM, crossM), 0f) + StationM; atM < toM; atM += StationM)
            {
                if (Outside(stretch.Line, arcs, atM, stretch.OnTheLeft)) return false;
            }

            return true;
        }

        /// <summary>
        /// <b>Whether a place skirts the edge of the driven ground</b> rather than crossing it: on that
        /// ground at all, and no further in from its edge than <paramref name="deepestM"/>.
        /// </summary>
        /// <remarks>
        /// It is what tells a join the ground really needs from one that cuts across, and it is one question
        /// about the town's own shape rather than two about the bands that make it up. A straight over the
        /// back of a car park runs along the bays' own ends, which <em>is</em> the edge; the chord of a
        /// junction corner dips inside it by the sag of a fillet; a straight across the middle of a box is a
        /// length of perimeter half a carriageway in.
        /// </remarks>
        public bool Skirts(Vector2 pointM, float deepestM)
        {
            var offM = paving.Kerbs.OffTheDrivenM(pointM);
            return offM <= Kerbs.JoinedM && offM >= -deepestM;
        }

        /// <summary>
        /// <b>Whether nothing stands between a step off a band's edge and the edge of the ground</b>: walking
        /// on outward from it, the ground gives out before another band takes over.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A junction is paved wider than its own movements</b> (TER-5): the wedge between each pair of
        /// neighbouring arms is filled back to an arc tangent to both carriageways, and that fillet is ground
        /// no line lays a band on. So the movement that sweeps a corner has its outer edge covered — not by
        /// another lane but by the corner itself — and read as covered it is the outside nowhere, which
        /// leaves the corner to no line at all and the ring jumping across it on a straight.
        /// </para>
        /// <para>
        /// <b>What tells that from the middle of a box is which way out the ground ends.</b> Off the corner
        /// the fillet is the last of the tarmac and the kerb is a step away; off a movement inside the box
        /// the wedge between it and its neighbour is followed by the neighbour's own band, and beyond that
        /// more box. So the walk stops at whichever comes first, and only the first of them is the outside.
        /// </para>
        /// </remarks>
        bool Clear(Vector2 fromM, Vector2 outward, int line)
        {
            // <b>As far out as the town's widest band is wide</b>, which is the deepest a fillet between two
            // arms of it can be: past that, whatever is under foot is another road rather than a corner.
            for (var stepM = 0f; stepM <= mostHalfM * 2f; stepM += StationM)
            {
                var pointM = fromM + (outward * stepM);
                if (paving.Kerbs.OffTheDrivenM(pointM) > -Kerbs.RoundingM) return true;
                if (Banded(pointM, line)) return false;
            }

            return true;
        }

        /// <summary>
        /// Whether some band other than this line's own covers a place — <b>its middle and not its edge</b>,
        /// which is what makes a seam between two bands belong to one of them rather than to both.
        /// </summary>
        bool Banded(Vector2 pointM, int line)
        {
            Span<int> near = stackalloc int[MostBandsNear];
            Span<float> alongM = stackalloc float[MostBandsNear];
            var found = bands.Near(pointM, mostHalfM + Kerbs.JoinedM, near, alongM);
            if (found > near.Length) return true;

            for (var at = 0; at < found; at++)
            {
                if (near[at] == line) continue;

                // <b>To the edge and not short of it</b>: two bands laid against one another leave a seam
                // that is the half width of both, and read strictly inside either one the walk slips between
                // them and comes out the far side calling a bay in the middle of a row the outside.
                var over = Spline.SampleAt(paving.ArcsOfDriven(near[at]), alongM[at]);
                if (MathF.Abs(Vector2.Dot(pointM - over.PositionM, over.Right))
                    <= halfM[near[at]] + Kerbs.JoinedM)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// <b>Whether one edge of one station of one line is the outside</b>: whether the ground stops there
        /// rather than carrying on under more of itself.
        /// </summary>
        /// <remarks>
        /// <b>Asked of the town's own shape and not of the lanes' union</b>
        /// (<see cref="Kerbs.OffTheDrivenM"/>). The lanes alone do not tile a junction: they stop at its
        /// mouths and the movements that cross it fan apart, leaving a wedge between each pair. Read as
        /// their union every one of those came back with both edges on the boundary and every box in the
        /// town filled with lines down the middle of the road. What a road lays is one band its whole width,
        /// and two of them crossing leave nothing between.
        /// </remarks>
        public bool Outside(int line, ReadOnlySpan<ArcSeg> arcs, float atM, bool onTheLeft)
        {
            var on = Spline.SampleAt(arcs, atM);
            var outward = onTheLeft ? -on.Right : on.Right;

            // <b>A step past the edge and not the edge itself</b>, and <b>a step wider than the grace the
            // step is read with</b>. A band's edge stands at nothing from the ground wherever it is the
            // outside of it and at nothing from its neighbour wherever it is the seam between the two, so
            // the step is what tells those apart — and read at the same figure it is graced by, which side
            // of the two a bay in the middle of a row falls on is the last bit of a float. Every one of them
            // came back as the outside, along an edge its neighbour lies against.
            //
            // <b>And as short a step as a float will carry</b>, because the step is also the error. Where an
            // edge goes under another the walk stops when its <em>probe</em> crosses, which is short of the
            // crossing by the step divided by however shallowly the two meet — and a movement leaves the
            // lane it serves at a few degrees. At a centimetre that put the two stops metres apart on lines
            // that are all but one line, which is a hand-over no rule about how near two ends are can find.
            if (!Clear(on.PositionM + (outward * (halfM[line] + (Kerbs.RoundingM * 2f))), outward, line))
            {
                return false;
            }

            return Owns(line, on.PositionM + (outward * halfM[line]), outward);
        }

        /// <summary>
        /// <b>Whether this line is the one that owns the edge it stands on</b>, where more than one lies
        /// along it: <b>the widest band, and the lowest-numbered of those the same width</b> — which puts a
        /// road's own lane ahead of every movement that leaves it, because the lanes are numbered first.
        /// </summary>
        /// <remarks>
        /// A movement out of a lane that fills its road runs along that road's own kerb for its first
        /// metres, and a way into a bay runs up its approach beside five others a few centimetres apart.
        /// Every one of them has an edge on the outside of the town there and the ground cannot tell them
        /// apart, so without a tie-break the same length of the boundary comes back two and six times over
        /// and the ring has no order to walk in. It is a tie-break and needs only to be the same answer at
        /// every station.
        /// </remarks>
        bool Owns(int line, Vector2 edgeM, Vector2 outward)
        {
            Span<int> near = stackalloc int[MostBandsNear];
            Span<float> alongM = stackalloc float[MostBandsNear];
            var found = bands.Near(edgeM, mostHalfM + Kerbs.JoinedM, near, alongM);
            if (found > near.Length) return false;

            for (var at = 0; at < found; at++)
            {
                if (near[at] == line) continue;

                var over = Spline.SampleAt(paving.ArcsOfDriven(near[at]), alongM[at]);
                var offM = edgeM - over.PositionM;

                // A band has square ends (TER-3c.6), and one that has stopped lays no edge here.
                if (MathF.Abs(Vector2.Dot(offM, over.Direction)) > Kerbs.JoinedM) continue;

                var asideM = Vector2.Dot(offM, over.Right);
                if (MathF.Abs(MathF.Abs(asideM) - halfM[near[at]]) > Kerbs.JoinedM) continue;

                // <b>And only to a band facing the same way out.</b> One whose edge runs through this point
                // but whose own outside faces elsewhere is a different length of boundary that happens to
                // touch this one, and handed the edge on width alone it would never claim it — its own step
                // off its own edge lands in the tarmac — leaving the edge to nobody.
                if (Vector2.Dot(asideM < 0f ? -over.Right : over.Right, outward) <= 0f) continue;

                if (halfM[near[at]] > halfM[line] + Kerbs.JoinedM) return false;
                if (halfM[near[at]] > halfM[line] - Kerbs.JoinedM && near[at] < line) return false;
            }

            return true;
        }
    }
}
