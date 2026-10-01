using System.Collections.Concurrent;
using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>What stands along the streets</b> (GEN-54): a building against the pavement's outer face, its front
/// wall on the walk's own kerb and its way in on the concrete behind it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The face is walked, because frontage is a line and not an area</b> (<see cref="GroundRings.WalkEdge"/>,
/// TER-7b). It is the whole town's outer kerb in one set of closed rings — round every block, round the
/// outside of the town, round the mouth of every rank — so nothing here knows what a road, a junction, a
/// roundabout or a car park is, and a building fronts all of them on the same terms. A sweep of the ground
/// asking each square whether it happened to be near a street would have to know all of it.
/// </para>
/// <para>
/// <b>It runs after every stage that lays driven ground and before the props</b> (GEN-10, GEN-6b): the
/// boundary is settled when this starts, so a building is cleared against the ground the finished map
/// answers with rather than against one a later stage would move; and the scatter takes what is left, which
/// is why a verge crowded with buildings carries fewer props.
/// </para>
/// <para>
/// <b>A building stands square to the face and touches its kerb</b> (GEN-2a, TER-3c.2): the wall is at
/// <see cref="SimConfig.BuildingLineM"/> off the driven ground's boundary, which is the outer face of the
/// walk's own kerbstone, and the way in is at <see cref="SimConfig.BuildingWayInM"/> — the line of the
/// walking lane that runs past the door, so the entrance is on ground the walk is actually held on (GEN-5).
/// </para>
/// <para>
/// <b>Where that face wraps a rank of bays it is the car park's, and only the flat of it is built on</b>
/// (<see cref="SquareOnItsRank"/>, GEN-55): the whole of a building's frontage stands on the line the bays
/// end on, never on the rounding round the corner of the rank and never down its side.
/// </para>
/// <para>
/// <b>The services are stood first and each in the middle of its own car park</b> (GEN-55). A hospital, a
/// police station and a depot are buildings the generator cut a yard for (<see cref="CarParks"/>), and each
/// stands square across the far end of that yard's rank. What puts them across the town from each other is
/// where their car parks were cut, so there is no second spread here.
/// </para>
/// <para>
/// <b>Every station in the town is cut before any of them is filled, and they are filled in a drawn
/// order.</b> Filled face by face instead, a town whose brief asks for fewer buildings than its frontage
/// affords is built solid along whichever rings came first and empty everywhere else — the count runs out
/// before the walk reaches the rest of the map.
/// </para>
/// <para>
/// <b>Nothing is placed and taken back</b> (GEN-10, GEN-8). A candidate that does not stand is not a
/// building, the station is not tried again from another angle, and a brief asking for more than the ground
/// affords gets what fitted.
/// </para>
/// </remarks>
internal static class BuildingStage
{
    /// <summary>One place along the pavement's outer face a building may be offered: the point, the way off the town, and the face's own bearing there.</summary>
    readonly record struct Station(Vector2 AtM, Vector2 Outward, float HeadingRad);

    /// <summary>
    /// <b>One car park's rank of bays, as the frame its own stretch of the face stands in</b> (GEN-53): the
    /// middle of the line its bays end on, the way they point, how far that line reaches either side of the
    /// middle, and how far back towards the street the rank runs.
    /// </summary>
    readonly record struct Rank(int Park, Vector2 TipM, Vector2 Outward, float HalfAcrossM, float DeepM);

    /// <summary>The two sides of the road a car park was cut into, as its bays are recorded (GEN-53).</summary>
    static readonly bool[] Sides = [true, false];

    public static CityPlan.BuildingArrays Lay(
        TownBrief brief, CarParks.Laid carParks, CityPlan.RoadArrays roads,
        Paving paving, GroundShapes ground, GenClaims claims, BuildingSizes sizes, SimConfig config,
        ref Rng draw)
    {
        if (brief.Buildings <= 0 || sizes.OrdinaryM.Length == 0) return CityPlan.BuildingArrays.None;

        var stations = AlongTheFace(paving.Rings(config), config);
        if (stations.Count == 0) return CityPlan.BuildingArrays.None;

        var ranks = new Ranks(TheRanks(carParks, roads), config);
        var built = new Built();
        var asking = new Asking(ground);
        TheServices(carParks, ranks, stations, sizes, config, ground, claims, built, asking);

        for (var at = stations.Count - 1; at > 0; at--)
        {
            var other = draw.NextInt(at + 1);
            (stations[at], stations[other]) = (stations[other], stations[at]);
        }

        TheOrdinary(stations, brief.Buildings, sizes, ranks, config, ground, claims, built, asking, ref draw);
        return built.Arrays(config.CityGen.BuildingCapacity);
    }

    /// <summary>
    /// How many stations the ground is asked about at once (<see cref="TheOrdinary"/>): enough to keep every
    /// thread busy for longer than a parallel pass takes to start, and few enough that the stations solved past
    /// the one that fills the brief — never read — are a small share of the town's.
    /// </summary>
    const int Batch = 1024;

    /// <summary>
    /// <b>The ordinary buildings, stood station by station in the drawn order</b>, with what the ground says
    /// about each station asked of a batch of them at once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only the claims depend on the order, so only the claims are walked in it.</b> Whether a footprint
    /// stands on grass, clear of the paving and square on its rank is a fact about the station and the
    /// footprint drawn for it, which no building stood before it changes; whether its ground is still unclaimed
    /// is the one question the stations before it answer. So the ground is asked on every thread a batch at a
    /// time and the claims are then walked in order — the same town, building for building, as asking all of it
    /// one station at a time.
    /// </para>
    /// <para>
    /// <b>The claims are asked in the parallel pass as well, and again in order</b>: a claim is never given
    /// back (GEN-10), so a station the batch's opening claims refuse is refused at its turn and its ground need
    /// not be solved. It is what holds the parallel pass to roughly the stations a serial walk would have
    /// solved — on a shipped city, the claims refuse seven stations in ten. <b>Nothing claims while the pass
    /// runs</b>: every claim is made in the walk after it, which is what lets every thread read them.
    /// </para>
    /// <para>
    /// <b>Every station of a batch has its footprint drawn, the ones past the brief's last building
    /// included</b>: station <em>n</em>'s footprint is still the stream's <em>n</em>th draw, which is all the
    /// town reads of it.
    /// </para>
    /// </remarks>
    static void TheOrdinary(
        List<Station> stations, int wanted, BuildingSizes sizes, Ranks ranks, SimConfig config,
        GroundShapes ground, GenClaims claims, Built built, Asking asking, ref Rng draw)
    {
        var footprintM = new Vector2[Math.Min(Batch, stations.Count)];
        var plots = new Plot[footprintM.Length];
        var open = new bool[footprintM.Length];

        // One working set a thread across every batch: a scan is the size of the index it reads, and the
        // boundary's is a hundred thousand pieces.
        var idle = new ConcurrentBag<Asking> { asking };
        for (var first = 0; first < stations.Count && built.Count < wanted; first += Batch)
        {
            var count = Math.Min(Batch, stations.Count - first);

            // Drawn before the ground is asked about anything, so that what the stream has spent by station
            // n is the face's own length and never what the ground answered at the stations before it.
            for (var at = 0; at < count; at++) footprintM[at] = sizes.OrdinaryM[draw.NextInt(sizes.OrdinaryM.Length)];

            var from = first;
            InChunks.Over(
                count,
                () => idle.TryTake(out var own) ? own : new Asking(ground),
                (own, at) =>
                {
                    var station = stations[from + at];
                    var plot = plots[at] = Plot.Of(station, footprintM[at], config);
                    open[at] = claims.IsFree(plot.CentreM, plot.Axis, plot.PaddedM)
                               && OnOpenGround(own, station, footprintM[at], plot, ranks, config, ground);
                },
                idle.Add);

            for (var at = 0; at < count && built.Count < wanted; at++)
            {
                ref readonly var plot = ref plots[at];
                if (!open[at] || !claims.IsFree(plot.CentreM, plot.Axis, plot.PaddedM)) continue;

                Raise(stations[from + at], footprintM[at], BuildingUse.Ordinary, plot, config, claims, built);
            }
        }
    }

    /// <summary>
    /// <b>Every place the town could put a building</b>, walked off the pavement's outer face at the one
    /// pitch (<see cref="CityGenFigures.BuildingPitchM"/>).
    /// </summary>
    /// <remarks>
    /// <b>A ring is walked once and the outward hand is the ring's own</b>: a shell is walked with the
    /// ground it covers on its right (<see cref="BandShell.Chains"/>), so the block is to its left, whether
    /// the ring is the one round the outside of the town or one round a block inside it.
    /// </remarks>
    static List<Station> AlongTheFace(GroundRings rings, SimConfig config)
    {
        var pitchM = config.CityGen.BuildingPitchM;
        var stations = new List<Station>();
        foreach (var face in rings.WalkEdge)
        {
            var lengthM = Spline.TotalLengthM(face);
            var cursor = default(SplineCursor);
            for (var alongM = 0f; alongM < lengthM; alongM += pitchM)
            {
                var on = Spline.SampleFrom(face, alongM, ref cursor);
                stations.Add(new Station(on.PositionM, -on.Right, on.HeadingRad));
            }
        }

        return stations;
    }

    /// <summary>
    /// <b>Every rank in the town, read off its car park's bays</b> (GEN-53). The bays of a rank are parallel
    /// and their far ends lie on one line parallel to the carriageway, so the ends averaged are the middle of
    /// that line, the bearing is every bay's, and the furthest end either way plus half a bay is how far the
    /// rank reaches along the street. <b>A car park with a rank each side is two of these</b>.
    /// </summary>
    static List<Rank> TheRanks(CarParks.Laid carParks, CityPlan.RoadArrays roads)
    {
        var ranks = new List<Rank>();
        for (var park = 0; park + 1 < carParks.BayOffsets.Length; park++)
        {
            foreach (var right in Sides)
            {
                if (RankOf(carParks, roads, park, right) is { } rank) ranks.Add(rank);
            }
        }

        return ranks;
    }

    /// <summary>One side's rank, or nothing where that side of the road carries no bay.</summary>
    static Rank? RankOf(CarParks.Laid carParks, CityPlan.RoadArrays roads, int park, bool right)
    {
        var from = carParks.BayOffsets[park];
        var to = carParks.BayOffsets[park + 1];

        var tipM = Vector2.Zero;
        var outward = Vector2.Zero;
        var halfABayM = 0f;
        var bays = 0;
        for (var bay = from; bay < to; bay++)
        {
            if (carParks.Right[bay] != right || roads.SegmentsOf(carParks.Road[bay]).Length == 0) continue;

            tipM += EndOfABayM(roads, carParks.Road[bay]);
            outward += Spline.SampleAt(roads.SegmentsOf(carParks.Road[bay]), 0f).Direction;
            halfABayM = roads.WidthM[carParks.Road[bay]] * 0.5f;
            bays++;
        }

        if (bays == 0 || outward.LengthSquared() <= 0f) return null;

        tipM /= bays;
        outward = Vector2.Normalize(outward);

        // How far the rank reaches along the street, off the bays themselves rather than off a count: the
        // furthest bay end either way, plus the half of that bay's own ground lying beyond its line.
        var along = Heading.RightOf(outward);
        var halfAcrossM = 0f;
        for (var bay = from; bay < to; bay++)
        {
            if (carParks.Right[bay] != right || roads.SegmentsOf(carParks.Road[bay]).Length == 0) continue;

            var endM = EndOfABayM(roads, carParks.Road[bay]);
            halfAcrossM = MathF.Max(halfAcrossM, MathF.Abs(Vector2.Dot(endM - tipM, along)));
        }

        // And how deep it runs: back to the street's own line, which is where the rank's tarmac gives on to the
        // street's.
        return new Rank(
            park, tipM, outward, halfAcrossM + halfABayM, MathF.Abs(Vector2.Dot(tipM - carParks.AtM[park], outward)));
    }

    static Vector2 EndOfABayM(CityPlan.RoadArrays roads, int bay)
    {
        var chain = roads.SegmentsOf(bay);
        return Spline.SampleAt(chain, Spline.TotalLengthM(chain)).PositionM;
    }

    /// <summary>
    /// <b>One building of each service, each square across the middle of the yard that was cut for it</b>
    /// (GEN-55). A use whose car park was never cut, or whose yard the face does not reach past, stands no
    /// building — which is a shortfall the census reports (GEN-8) and never a second search for somewhere
    /// else to put a hospital.
    /// </summary>
    static void TheServices(
        CarParks.Laid carParks, Ranks ranks, List<Station> stations, BuildingSizes sizes,
        SimConfig config, GroundShapes ground, GenClaims claims, Built built, Asking asking)
    {
        foreach (var rank in ranks.All)
        {
            var use = carParks.For[rank.Park];
            if (use == BuildingUse.Ordinary) continue;
            if (OffTheRank(stations, rank, config.CityGen.LocalityM) is not { } middle) continue;

            Stand(middle, sizes.Of(use), use, ranks, config, ground, claims, built, asking);
        }
    }

    /// <summary>
    /// <b>The place on the face the rank's own normal reaches</b>: the nearest station beyond the rank's far
    /// end that faces the way it points, <b>slid along the face to the middle of the rank</b>. The two tests
    /// are what keeps a yard's building off the kerb behind it — a face a stride away across the block runs
    /// the other way, and one beside the rank is behind its tip — and the slide is what stands the building
    /// square in the middle of its own yard rather than wherever the walk happened to be stepped to
    /// (GEN-55).
    /// </summary>
    /// <remarks>
    /// <b>Sliding along the face is exact here and nowhere else.</b> The far ends of a rank lie on one line
    /// parallel to the carriageway (GEN-53), so the stretch of face over them is straight and a step along
    /// the station's own bearing stays on it. Off that straight it would not, which is why nothing else in
    /// this stage moves a station.
    /// </remarks>
    /// <param name="withinM">
    /// How far past the rank the face may stand and still be this rank's (GEN-16). It is a walk in a town
    /// with nothing wrong with it; the bound is there so a yard whose own face was swallowed stands no
    /// building rather than one across the block.
    /// </param>
    static Station? OffTheRank(List<Station> stations, Rank rank, float withinM)
    {
        var best = -1;
        var bestM = withinM * withinM;
        for (var at = 0; at < stations.Count; at++)
        {
            var awayM = stations[at].AtM - rank.TipM;
            if (Vector2.Dot(stations[at].Outward, rank.Outward) <= 0f
                || Vector2.Dot(awayM, rank.Outward) <= 0f)
            {
                continue;
            }

            var offM = awayM.LengthSquared();
            if (offM >= bestM) continue;

            best = at;
            bestM = offM;
        }

        if (best < 0) return null;

        var station = stations[best];
        var along = Heading.Unit(station.HeadingRad);
        return station with { AtM = station.AtM - (along * Vector2.Dot(station.AtM - rank.TipM, along)) };
    }

    /// <summary>
    /// <b>Whether a place on the face is one a building may stand off, given the rank it belongs to</b>
    /// (GEN-55). A stretch of face that wraps a rank of bays is the car park's own: the flat of it is the
    /// line the bays end on, and either side of that the walk turns round the corner of the rank and runs
    /// back down its side. <b>A building stands on the flat and the whole of its frontage stands there</b> —
    /// one perched on a rounding fronts the mouth of the car park at an angle, and one down the side fronts
    /// the row of bays edge-on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A place belongs to a rank when it stands inside that rank's own ground grown by a walk</b>, which
    /// is where the face round it was struck (TER-3c.3). Past that the face is the street's and a building
    /// on it owes the rank nothing. The slack is the figure two computations of one distance disagree by
    /// (<see cref="LineTolerance.RoundingM"/>), because the face over the flat stands at exactly a walk.
    /// </para>
    /// <para>
    /// <b>So a narrow car park carries no building behind it at all</b>, its flat being shorter than the
    /// frontage offered. That is the rule holding rather than failing: what is behind three bays is a strip
    /// of verge, and the building the draw wanted there goes somewhere it fits (GEN-8).
    /// </para>
    /// </remarks>
    /// <param name="near">This caller's own list, which the ranks near the place are read into.</param>
    static bool SquareOnItsRank(Ranks ranks, List<int> near, Vector2 atM, float halfFrontageM, SimConfig config)
    {
        var walkM = config.WalkOuterM;
        var slackM = LineTolerance.RoundingM;
        ranks.Near(atM, near);
        foreach (var index in near)
        {
            var rank = ranks.All[index];
            var along = Heading.RightOf(rank.Outward);
            var alongM = Vector2.Dot(atM - rank.TipM, along);
            var outM = Vector2.Dot(atM - rank.TipM, rank.Outward);

            var itsOwn = MathF.Abs(alongM) <= rank.HalfAcrossM + walkM + slackM
                         && outM <= walkM + slackM
                         && outM >= -(rank.DeepM + walkM) - slackM;
            if (!itsOwn) continue;

            return outM > 0f && MathF.Abs(alongM) + halfFrontageM <= rank.HalfAcrossM;
        }

        return true;
    }

    /// <summary>
    /// One building on one station, or nothing at all. <b>It is sized by the roof it will wear</b>
    /// (<see cref="BuildingSizes"/>), so the picture drawn on it is the size the plan authored rather than
    /// the nearest thing to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The claims are asked first because they are a grid and the ground is a solver</b>: most of what a
    /// station is refused for is the building already standing next to it, and the cheapest question that
    /// can answer it is the one that gets asked.
    /// </para>
    /// <para>
    /// <b>The footprint has to be grass and the padding does not</b> (GEN-3, GEN-6a). What is claimed is the
    /// building and the walkable padding around it, so no two neighbours can touch; what has to be grass is
    /// the building, because the padding in front of it is the pavement it opens onto.
    /// </para>
    /// </remarks>
    static bool Stand(
        Station station, Vector2 footprintM, BuildingUse use, Ranks ranks, SimConfig config,
        GroundShapes ground, GenClaims claims, Built built, Asking asking)
    {
        var plot = Plot.Of(station, footprintM, config);
        if (!claims.IsFree(plot.CentreM, plot.Axis, plot.PaddedM)) return false;
        if (!OnOpenGround(asking, station, footprintM, plot, ranks, config, ground)) return false;

        Raise(station, footprintM, use, plot, config, claims, built);
        return true;
    }

    /// <summary>
    /// <b>Whether the ground lets a building stand on a plot</b> — everything <see cref="Stand"/> asks but the
    /// claims, and so a fact about the station and its footprint alone, whatever was stood before it.
    /// </summary>
    static bool OnOpenGround(
        Asking asking, Station station, Vector2 footprintM, in Plot plot, Ranks ranks, SimConfig config,
        GroundShapes ground)
    {
        if (footprintM.X <= 0f || footprintM.Y <= 0f) return false;
        if (!SquareOnItsRank(ranks, asking.Ranks, station.AtM, footprintM.X * 0.5f, config)) return false;

        var stepM = config.Terrain.GroundStepM;
        return ground.IsAll(asking.Ground, plot.CentreM, plot.Axis, plot.HalfM, stepM, Ground.Grass)
               && !ReachesThePaving(ground, asking.Ground, plot.CentreM, plot.Axis, plot.HalfM, stepM);
    }

    /// <summary>The building stood on its plot: its ground and padding claimed, and the building kept.</summary>
    static void Raise(
        Station station, Vector2 footprintM, BuildingUse use, in Plot plot, SimConfig config, GenClaims claims,
        Built built)
    {
        claims.Claim(plot.CentreM, plot.Axis, plot.PaddedM);
        built.Add(
            plot.CentreM, footprintM, station.HeadingRad, use,
            station.AtM - (station.Outward * (config.WalkOuterM - config.BuildingWayInM)));
    }

    /// <summary>
    /// Where a building of one footprint stands off one station: square to the face with its front wall on
    /// the building line, and the padding it claims beyond that.
    /// </summary>
    readonly record struct Plot(Vector2 CentreM, Vector2 Axis, Vector2 HalfM, Vector2 PaddedM)
    {
        public static Plot Of(Station station, Vector2 footprintM, SimConfig config)
        {
            var halfM = footprintM * 0.5f;
            var wallM = station.AtM + (station.Outward * (config.BuildingLineM - config.WalkOuterM));
            return new Plot(
                wallM + (station.Outward * halfM.Y), Heading.Unit(station.HeadingRad), halfM,
                halfM * (1f + config.CityGen.BuildingPaddingShare));
        }
    }

    /// <summary>
    /// <b>One thread's working set for the ground a plot is asked about</b>: its own scan of the ground
    /// (<see cref="GroundShapes.NewScan"/>) and its own list of the ranks near a place.
    /// </summary>
    sealed class Asking(GroundShapes ground)
    {
        public GroundShapes.Scan Ground { get; } = ground.NewScan();

        public List<int> Ranks { get; } = [];
    }

    /// <summary>
    /// <b>The town's ranks, filed by the place their bays end</b> (SIM-8, the main level), so a place on the
    /// face asks the ranks that could own it rather than every rank in the town.
    /// </summary>
    /// <remarks>
    /// <b>The same ranks in the same order as the list</b>: a rank's own ground grown by a walk lies inside
    /// the reach of its tip read here, so a rank left out is one that could not have owned the place, and
    /// the first that does is the first a scan of the list would have met. Asked of every rank, the stage
    /// was the town's stations times its car parks.
    /// </remarks>
    sealed class Ranks
    {
        readonly PointCells _tips;
        readonly float _reachM;

        public Ranks(List<Rank> all, SimConfig config)
        {
            All = all;
            _tips = new PointCells(config.Grid.Main);
            var walkM = config.WalkOuterM + LineTolerance.RoundingM;
            foreach (var rank in all)
            {
                _tips.Add(rank.TipM);
                var acrossM = rank.HalfAcrossM + walkM;
                var deepM = MathF.Max(walkM, rank.DeepM + walkM);
                _reachM = MathF.Max(_reachM, MathF.Sqrt((acrossM * acrossM) + (deepM * deepM)));
            }

            // A centimetre over, so no rounding between the ownership test's two readings and this one's
            // distance leaves an owner out.
            _reachM += LineTolerance.JoinedM;
        }

        public List<Rank> All { get; }

        /// <summary>
        /// The ranks whose tip stands within reach of a place, as their places in <see cref="All"/> and in its
        /// order, read into the caller's own list so that two threads may ask at once.
        /// </summary>
        public void Near(Vector2 atM, List<int> into) => _tips.Within(atM, _reachM, into);
    }

    /// <summary>
    /// <b>Whether any of the building would stand on the town's paving</b> — its own street's or another's.
    /// </summary>
    /// <remarks>
    /// <b>Read off the boundary itself and not off the ground's answer</b>
    /// (<see cref="GroundRings.PavedWithin(Vector2, float)"/>): what holds a building off a walk is the distance that walk was
    /// struck at, so the front wall clears it by half a kerbstone by construction, and this is what keeps the
    /// back of a deep building out of the street behind it.
    /// </remarks>
    static bool ReachesThePaving(
        GroundShapes ground, GroundShapes.Scan scan, Vector2 centreM, Vector2 axis, Vector2 halfM, float stepM)
    {
        var side = Heading.RightOf(axis);
        for (var alongM = -halfM.X; ; alongM += stepM)
        {
            var atAlongM = MathF.Min(alongM, halfM.X);
            for (var acrossM = -halfM.Y; ; acrossM += stepM)
            {
                var atAcrossM = MathF.Min(acrossM, halfM.Y);
                if (ground.PavingWithin(scan, centreM + (axis * atAlongM) + (side * atAcrossM), 0f)) return true;

                if (atAcrossM >= halfM.Y) break;
            }

            if (atAlongM >= halfM.X) break;
        }

        return false;
    }

    /// <summary>The buildings as they are stood, one list a field, gathered into the plan's arrays at the end.</summary>
    sealed class Built
    {
        readonly List<Vector2> _centreM = [];
        readonly List<Vector2> _sizeM = [];
        readonly List<float> _headingRad = [];
        readonly List<BuildingUse> _use = [];
        readonly List<Vector2> _entryM = [];

        public int Count => _centreM.Count;

        public void Add(Vector2 centreM, Vector2 sizeM, float headingRad, BuildingUse use, Vector2 entryM)
        {
            _centreM.Add(centreM);
            _sizeM.Add(sizeM);
            _headingRad.Add(headingRad);
            _use.Add(use);
            _entryM.Add(entryM);
        }

        public CityPlan.BuildingArrays Arrays(int capacity) => new()
        {
            CentreM = [.. _centreM],
            SizeM = [.. _sizeM],
            HeadingRad = [.. _headingRad],
            Capacity = Each(_centreM.Count, capacity),
            Use = [.. _use],
            EntryOffsets = OneEach(_entryM.Count),
            EntryPointM = [.. _entryM],
        };

        static int[] Each(int count, int value)
        {
            var filled = new int[count];
            Array.Fill(filled, value);
            return filled;
        }

        /// <summary>Every building has one way in, so its offsets are the numbers up to its count.</summary>
        static int[] OneEach(int count)
        {
            var offsets = new int[count + 1];
            for (var at = 0; at <= count; at++) offsets[at] = at;
            return offsets;
        }
    }
}
