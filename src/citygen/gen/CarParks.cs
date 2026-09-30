using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>One car park for every few of the buildings the brief plans, cut into a road as a junction of its
/// own</b> (GEN-53, <see cref="SimConfig.CarParksFor"/>). A car park
/// is not a rectangle beside a street: it is <b>an ordinary junction let into the carriageway</b>
/// (<see cref="CutJunctions"/>, GEN-52) with <b>one arm a bay</b>, and a car reaches a bay over a movement
/// through a box like every other turn it makes.
/// </summary>
/// <remarks>
/// <para>
/// <b>A bay is an arm and a bay's way is its lane</b> (GEN-4f). Each bay stands on its own node a reach past
/// the road's edge, the bays of one side stand a lane apart in a rank along that road <b>square to it and so
/// parallel to one another</b>, and the way to each is <b>a lane's width of ground driven both ways over one
/// line</b> (<see cref="CityPlan.RoadArrays.DrivenOverOneLine"/>) — a car drives in over it and comes back
/// out over it, which is what a bay's way has always been.
/// </para>
/// <para>
/// <b>A bay joins the street and nothing else</b> (GEN-53, <see cref="LaneLines"/>): <b>no movement joins
/// two bays</b>, the ground between two ranks being a car park's own rather than a road with a right of way
/// on it.
/// </para>
/// <para>
/// <b>And it joins the street both ways, which is what the car park's own standoff is for</b>
/// (<see cref="SimConfig.CarParkStandoffM"/>). A junction's arms ordinarily leave from its node, so one
/// standoff stands them all clear; a rank hangs its bays along the street instead, and a bay past the lane
/// end a car arrives on is one that car can only loop back into — a turn tighter than the junction corners
/// and so no movement at all. So the street is parted <b>the rank's own reach further back</b>, which puts
/// every bay ahead of both arrivals and gives each of them two ways in and two ways out.
/// </para>
/// <para>
/// <b>So the bays are counted before the place is chosen.</b> The standoff is what the sites are found with
/// (<see cref="CutJunctions.Sites"/>), so a wide car park asks for a longer, straighter, emptier stretch of
/// road and is laid where one is — rather than a place being taken and the bays that did not fit on it
/// taken back off again (GEN-10).
/// </para>
/// <para>
/// <b>The road it is cut into does not move</b> (GEN-52). That is why a car park is a cut and not a junction
/// the layout laid: the carriageway, its lanes and the movement straight through the new node are the ground
/// that street was already on, so adding one somewhere changes nothing anywhere else.
/// </para>
/// <para>
/// <b>The rank is an apron and not a fan of carriageways</b>, so GEN-13 is not asked of the bays
/// (<see cref="CutArm.Square"/>). It measures the angle between two arms, and every arm of a rank leaves on
/// the <em>same</em> bearing — what stands two of them apart is the lane between their feet, which that rule
/// cannot see. The ground they share is one piece of tarmac rather than two roads lying against one another,
/// and nothing is filleted, crossed or barred between them.
/// </para>
/// <para>
/// <b>They are spread and not scattered.</b> Each is cut at the site furthest from every car park already
/// cut, which is the whole of "evenly over the town" and needs no spacing figure of its own — where GEN-18's
/// one-way streets and GEN-19's roundabouts each carry one because what they are spread against is a
/// neighbourhood rather than each other. What keeps two of them off each other's ground is the locality
/// every pair of junctions owes (GEN-16), which the sites are already held to.
/// </para>
/// <para>
/// <b>What is left of a count the ground cannot carry is what fitted</b> (GEN-8). A town with no road
/// straight enough, long enough or clear enough for another car park lays fewer, and the census reports the
/// shortfall rather than the generator trying again.
/// </para>
/// <para>
/// <b>The first few are cut for the services and not for the town</b> (GEN-55): every district's hospital,
/// police station and depot (GEN-56) gets a yard of its own inside it — one rank, on one side, as wide as a car
/// park gets — and the building is stood past the far end of it afterwards (<see cref="BuildingStage"/>).
/// They are cut first because the sites are ranked by distance from the car parks already cut, so taking
/// them first is what puts the services as far apart as the town's roads allow.
/// </para>
/// <para>
/// <b>It is the last stage of the layout</b> (<see cref="TownGenerator"/>, <see cref="CutJunctions"/>): the
/// streets that run one way are settled before it, so a cut into one parts a street that is already one way
/// rather than leaving a scatter to choose a street in pieces (GEN-18), and nothing is offered to the layout
/// after it.
/// </para>
/// </remarks>
internal static class CarParks
{
    /// <summary>The car parks a town came out with, and every road their cuts made a cut road (GEN-52).</summary>
    /// <param name="For">
    /// Which use each car park was cut for (GEN-55): <see cref="BuildingUse.Ordinary"/> for the town's own,
    /// and a service use for one cut to stand a special building's vehicles. <b>It is the stage's own answer
    /// and not the plan's</b> — what holds a bay for a station is the apron, which finds the bays nearest
    /// its door (GEN-4k) — so it travels only as far as the stage that stands the building on it.
    /// </param>
    internal readonly record struct Laid(
        int[] Junction, int[] BayOffsets, int[] Road, bool[] Right, int[] CutRoads, BuildingUse[] For)
    {
        public static Laid None => new([], [0], [], [], [], []);
    }

    /// <summary>The two sides of a road, as the driver's right of its own direction and its left.</summary>
    static readonly bool[] Sides = [true, false];

    /// <param name="districts">
    /// The wheel the town was laid on: its hub is the middle a yard's rank faces (<see cref="FaceTheTown"/>), and
    /// each district is cut a yard for each service (GEN-56).
    /// </param>
    /// <param name="sizes">The service buildings' footprints, which say how far across its rank a yard's building reaches.</param>
    public static Laid Lay(
        TownLayout layout, TownBrief brief, SimConfig config, DistrictWheel districts, BuildingSizes sizes,
        ref Rng draw)
    {
        var services = TheServicesWanted(brief, districts);
        var wanted = services.Count + config.CarParksFor(brief.Buildings);
        if (wanted <= 0) return Laid.None;

        var townM = districts.HubM;

        var junction = new List<int>();
        var bayOffsets = new List<int> { 0 };
        var road = new List<int>();
        var right = new List<bool>();
        var cutRoads = new List<int>();
        var forUse = new List<BuildingUse>();
        var parks = new PointCells(config.Grid.Main);
        var changes = new RoadChanges(layout.Edges.Count);
        var books = new Dictionary<int, SiteBook>();
        var refused = new List<int>();

        Span<int> perSide = stackalloc int[Sides.Length];
        var arms = new CutArm[config.CityGen.BaysPerLotMost * Sides.Length];
        var onTheRight = new bool[arms.Length];

        for (var want = 0; want < wanted; want++)
        {
            // <b>The bays are counted before the place is chosen and never after</b> (GEN-53): how far back
            // the street has to stand off is how far the longest rank reaches along it, so the size of the
            // car park is what decides which places can carry one — rather than a place being taken and the
            // bays that did not fit on it being taken back off again (GEN-10).
            var (use, district) = want < services.Count ? services[want] : (BuildingUse.Ordinary, AnyDistrict);
            if (use == BuildingUse.Ordinary) BaysPerSide(config, perSide, ref draw);
            else AYard(config, perSide);

            Vector2? facingM = use == BuildingUse.Ordinary ? null : townM;

            var mostBays = Math.Max(perSide[0], perSide[1]);
            var standoffM = config.CarParkStandoffM(mostBays);
            var curvatureMax = config.CarParkCurvatureMax(mostBays);
            var reach = use == BuildingUse.Ordinary ? default : YardReach.Of(config, sizes, use, mostBays);

            Cut? cut = null;
            var taken = 0;
            if (parks.Count == 0)
            {
                // <b>The first one is drawn</b>, there being nothing yet to stand away from.
                var sites = CutJunctions.Sites(layout, config, standoffM, curvatureMax);
                if (sites.Count == 0) break;

                var first = draw.NextInt(sites.Count);
                (sites[0], sites[first]) = (sites[first], sites[0]);
                foreach (var site in sites)
                {
                    if (!IsIn(layout, site, districts, district, reach)) continue;

                    cut = At(layout, config, site, standoffM, mostBays, arms, perSide, facingM, onTheRight, out taken);
                    if (cut is not null) break;
                }
            }
            else
            {
                if (!books.TryGetValue(mostBays, out var book))
                {
                    books[mostBays] = book = new SiteBook(layout, config, standoffM, curvatureMax, parks, changes);
                }

                // <b>A refusal costs that site and never the car park</b>: what a cut is refused for is the
                // ground beside one bay rather than anything about the road it was offered on, and the site
                // after it is somewhere else entirely. The site stays in the book for the next car park.
                book.CatchUp();
                refused.Clear();
                for (var entry = book.Next(); entry >= 0; entry = book.Next())
                {
                    if (IsIn(layout, book[entry], districts, district, reach))
                    {
                        cut = At(
                            layout, config, book[entry], standoffM, mostBays, arms, perSide, facingM, onTheRight,
                            out taken);
                        if (cut is not null) break;
                    }

                    refused.Add(entry);
                }

                foreach (var entry in refused) book.Queue(entry);
            }

            // <b>A district with nowhere to cut a yard stands that service nowhere</b> (GEN-8), and the next
            // is still asked for: it is that district's ground that ran out and not the town's.
            if (cut is null && use != BuildingUse.Ordinary) continue;

            if (cut is not { } made) break;

            junction.Add(made.Junction);
            parks.Add(layout.NodeM[made.Junction]);
            changes.Parted(made);
            cutRoads.AddRange(made.Roads);
            forUse.Add(use);
            for (var bay = 0; bay < taken; bay++)
            {
                road.Add(made.Arms[bay]);
                right.Add(onTheRight[bay]);
            }

            bayOffsets.Add(road.Count);
        }

        return new Laid(
            [.. junction], [.. bayOffsets], [.. road], [.. right], [.. cutRoads], [.. forUse]);
    }

    const int AnyDistrict = -1;

    /// <summary>
    /// <b>Whether a site is inside the district a yard is wanted in</b> (GEN-56): the ground its building will
    /// stand on, across the rank on the side it faces (<see cref="FaceTheTown"/>), near face and far face both.
    /// <b>The building and not the road</b>, because a district's edge is as often as not a spoke or the orbital:
    /// asked of the road alone, a yard cut into one stood its building in the district over the road, and a yard
    /// cut into one facing into the district is that district's.
    /// </summary>
    static bool IsIn(TownLayout layout, CutJunctions.Site site, DistrictWheel districts, int district, YardReach reach)
    {
        if (district == AnyDistrict) return true;

        var at = Spline.SampleAt(layout.LineOf(site.Road), site.AlongM);
        var townward = Vector2.Dot(at.Right, districts.HubM - at.PositionM) >= 0f ? at.Right : -at.Right;
        return districts.At(at.PositionM + (townward * reach.NearM)) == district
               && districts.At(at.PositionM + (townward * reach.FarM)) == district;
    }

    /// <summary>How far from its road's line a yard's building stands: its face on the walk, and its back.</summary>
    readonly record struct YardReach(float NearM, float FarM)
    {
        /// <summary>
        /// The road's own half, the rank, the walk wrapping it and the pitch a face is walked at (GEN-55), and the
        /// building's depth behind that.
        /// </summary>
        public static YardReach Of(SimConfig config, BuildingSizes sizes, BuildingUse use, int mostBays)
        {
            var nearM = config.LaneWidthM + config.CarParkArmStandM(mostBays, config.LaneOffsetM) + config.WalkOuterM
                        + config.CityGen.BuildingPitchM;
            return new YardReach(nearM, nearM + sizes.Of(use).Y);
        }
    }

    /// <summary>One car park cut at one site, or nothing where the town cannot have it there.</summary>
    /// <param name="facingM">Where a yard's rank faces (<see cref="FaceTheTown"/>), or nothing for the town's own car park.</param>
    static Cut? At(
        TownLayout layout, SimConfig config, CutJunctions.Site site, float standoffM, int mostBays,
        CutArm[] arms, ReadOnlySpan<int> perSide, Vector2? facingM, bool[] onTheRight, out int taken)
    {
        // <b>The ranks are laid off the lane each turns off and not off the node</b> (GEN-53): a street
        // driven one way carries its one lane half a lane to the side the traffic was moved onto
        // (TER-4d), so the two sides ask for different leads on it and for the same lead on a street of
        // two ways. Read here rather than once for the car park, the site being what says which road.
        var flow = layout.Edges[site.Road].Flow;

        Span<int> sides = stackalloc int[Sides.Length];
        perSide.CopyTo(sides);
        if (facingM is { } townM) FaceTheTown(layout, site, townM, sides);

        taken = 0;
        foreach (var right in Sides)
        {
            var laneTowardM = LaneTowardM(config, flow, right);
            taken += Rank(
                config, sides[right ? 0 : 1], right, config.CarParkBayLeadM(mostBays, laneTowardM),
                config.CarParkArmStandM(mostBays, laneTowardM), arms, onTheRight, taken);
        }

        return CutJunctions.Into(layout, config, site.Road, site.AlongM, standoffM, arms.AsSpan(0, taken));
    }

    /// <summary>
    /// <b>A yard's one rank goes on the side of its road facing the town's middle</b> (GEN-55): its building
    /// stands past the rank's far end, and the edge of a town is where the ground to stand one on runs out.
    /// Drawn instead, a yard cut into a road along the edge of the map faced off it half the time and its
    /// building stood nowhere.
    /// </summary>
    static void FaceTheTown(TownLayout layout, CutJunctions.Site site, Vector2 townM, Span<int> perSide)
    {
        var at = Spline.SampleAt(layout.LineOf(site.Road), site.AlongM);
        var rank = Math.Max(perSide[0], perSide[1]);
        var right = Vector2.Dot(at.Right, townM - at.PositionM) >= 0f;
        perSide[0] = right ? rank : 0;
        perSide[1] = right ? 0 : rank;
    }

    /// <summary>
    /// <b>How far toward one side of a road the lane a bay on that side is turned off runs</b> (GEN-53,
    /// <see cref="SimConfig.CarParkBayLeadM"/>), from the line the road's junctions stand on. A road driven
    /// both ways carries a lane each side, so it is half a lane toward either; a road driven one way carries
    /// its one lane on the half it was moved onto (<see cref="RoadStage.DrivenHalfM"/>, TER-4d), so the side
    /// it was moved off reaches the same lane the other way.
    /// </summary>
    public static float LaneTowardM(SimConfig config, RoadFlow flow, bool right)
    {
        if (flow == RoadFlow.BothWays) return config.LaneOffsetM;

        var halfM = RoadStage.DrivenHalfM(config, flow, RoadStage.WidthM(config, flow));
        return right ? halfM : -halfM;
    }

    /// <summary>
    /// <b>One side's rank of bays</b>: <paramref name="bays"/> of them a lane apart along the road, centred
    /// on the node, each standing a reach out past that road's own edge. <b>Every arm of a rank runs square
    /// to the street</b> — a bay is a car's length of ground it backs straight out of, so the bays of a side
    /// are parallel to each other and their ends lie on one line parallel to the carriageway. What tells them
    /// apart is where each one's foot stands along the street (<see cref="CutArm.AsideM"/>) and not which way
    /// it points.
    /// </summary>
    static int Rank(
        SimConfig config, int bays, bool right, float leadM, float standM, CutArm[] arms, bool[] onTheRight,
        int taken)
    {
        var turnRad = right ? MathF.PI * 0.5f : -MathF.PI * 0.5f;
        for (var bay = 0; bay < bays; bay++)
        {
            onTheRight[taken + bay] = right;
            arms[taken + bay] = new CutArm(
                turnRad, leadM, standM, RoadClass.CarPark, Square: false,
                AsideM: (bay - ((bays - 1) * 0.5f)) * config.LaneWidthM);
        }

        return bays;
    }

    /// <summary>
    /// <b>Which of the layout's roads a cut has parted, and how often</b> — what tells a
    /// <see cref="SiteBook"/> which of its sites stand on a road that is no longer there, and which roads it has
    /// yet to read.
    /// </summary>
    sealed class RoadChanges
    {
        /// <summary>How many times each road has been parted; a site read off an earlier one is gone.</summary>
        public readonly List<int> Version;

        /// <summary>Every road a cut left a new line on — the piece before its node and the piece after — in the order cut.</summary>
        public readonly List<int> Relaid = [];

        public RoadChanges(int roads) => Version = [.. new int[roads]];

        public void Parted(Cut made)
        {
            Version[made.Roads[0]]++;
            for (var added = 1; added < made.Roads.Length; added++) Version.Add(0);

            // The arms are car parks' own and carry no site (CutJunctions.SitesOn).
            Relaid.Add(made.Roads[0]);
            Relaid.Add(made.Roads[1]);
        }
    }

    /// <summary>Where a site stands in the order it is offered: furthest from every car park first, then by road, then along it.</summary>
    readonly record struct SiteOrder(float NearestSq, int Road, float AlongM);

    sealed class FurthestFirst : IComparer<SiteOrder>
    {
        public static readonly FurthestFirst Instance = new();

        public int Compare(SiteOrder one, SiteOrder other)
        {
            var byDistance = other.NearestSq.CompareTo(one.NearestSq);
            if (byDistance != 0) return byDistance;

            var byRoad = one.Road.CompareTo(other.Road);
            return byRoad != 0 ? byRoad : one.AlongM.CompareTo(other.AlongM);
        }
    }

    /// <summary>
    /// <b>Every place one size of car park could be cut, kept from one car park to the next</b> and handed out
    /// furthest from every car park already cut first — which spreads them over the town without a spacing of
    /// their own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The sites are read once and then only where a cut changed the road</b>
    /// (<see cref="RoadChanges"/>): the sites on a road are a question about that road's line alone, and a cut
    /// changes the line of the road it parts and of no other. Whether a site still stands a locality clear of
    /// every node (GEN-16) is asked as it comes up, since the nodes a cut adds are the only other thing that
    /// can take one away.
    /// </para>
    /// <para>
    /// <b>The ranking is kept rather than sorted again.</b> A site's distance from the nearest car park only
    /// ever falls, so each is kept with the distance it had and how many car parks that counted; the one at the
    /// head is brought up to date as it comes up, and it is the furthest once its distance has not fallen.
    /// Read afresh and sorted for every car park, the stage was the car parks squared times the sites — a
    /// brief of Odesa ten times over each way was still here after ten minutes.
    /// </para>
    /// </remarks>
    sealed class SiteBook
    {
        readonly TownLayout _layout;
        readonly SimConfig _config;
        readonly float _standoffM;
        readonly float _curvatureMax;
        readonly PointCells _parks;
        readonly RoadChanges _changes;

        readonly List<CutJunctions.Site> _site = [];

        /// <summary>The version of its road each site was read off (<see cref="RoadChanges.Version"/>).</summary>
        readonly List<int> _version = [];

        readonly List<float> _nearestSq = [];

        /// <summary>How many car parks each site's <see cref="_nearestSq"/> was measured against.</summary>
        readonly List<int> _parksCounted = [];

        readonly PriorityQueue<int, SiteOrder> _queue = new(FurthestFirst.Instance);
        readonly List<CutJunctions.Site> _room = [];
        readonly HashSet<int> _read = [];
        int _relaidRead;

        public SiteBook(
            TownLayout layout, SimConfig config, float standoffM, float curvatureMax, PointCells parks,
            RoadChanges changes)
        {
            _layout = layout;
            _config = config;
            _standoffM = standoffM;
            _curvatureMax = curvatureMax;
            _parks = parks;
            _changes = changes;

            foreach (var site in CutJunctions.Sites(layout, config, standoffM, curvatureMax)) Enter(site);
            _relaidRead = changes.Relaid.Count;
        }

        public CutJunctions.Site this[int entry] => _site[entry];

        /// <summary>The sites on every road a cut has laid again since this book last read, each road once.</summary>
        public void CatchUp()
        {
            _read.Clear();
            for (; _relaidRead < _changes.Relaid.Count; _relaidRead++)
            {
                var road = _changes.Relaid[_relaidRead];
                if (!_read.Add(road)) continue;

                _room.Clear();
                CutJunctions.SitesOn(_layout, _config, road, _standoffM, _curvatureMax, _room);
                foreach (var site in _room)
                {
                    if (_layout.StandsClear(site.AtM)) Enter(site);
                }
            }
        }

        /// <summary>The furthest site that still stands, taken out of the book, or −1 where none is left.</summary>
        public int Next()
        {
            while (_queue.TryDequeue(out var entry, out _))
            {
                var site = _site[entry];
                if (_version[entry] != _changes.Version[site.Road] || !_layout.StandsClear(site.AtM)) continue;

                if (_parksCounted[entry] < _parks.Count)
                {
                    var nearestSq = _parks.NearestSq(site.AtM, _nearestSq[entry]);
                    _parksCounted[entry] = _parks.Count;
                    if (nearestSq < _nearestSq[entry])
                    {
                        _nearestSq[entry] = nearestSq;
                        Queue(entry);
                        continue;
                    }
                }

                return entry;
            }

            return -1;
        }

        /// <summary>A site put back into the book, at the distance it was last measured at.</summary>
        public void Queue(int entry) =>
            _queue.Enqueue(entry, new SiteOrder(_nearestSq[entry], _site[entry].Road, _site[entry].AlongM));

        void Enter(CutJunctions.Site site)
        {
            var entry = _site.Count;
            _site.Add(site);
            _version.Add(_changes.Version[site.Road]);
            _nearestSq.Add(_parks.NearestSq(site.AtM));
            _parksCounted.Add(_parks.Count);
            Queue(entry);
        }
    }

    /// <summary>
    /// <b>How many bays each side of the road carries</b>: none, or a handful between the two a lot may be
    /// (GEN-4b). <b>Not both none</b> — a car park with no bay on either side is a junction cut into a road
    /// for nothing.
    /// </summary>
    static void BaysPerSide(SimConfig config, Span<int> perSide, ref Rng draw)
    {
        for (var side = 0; side < perSide.Length; side++) perSide[side] = Bays(config, ref draw, orNone: true);

        var any = false;
        foreach (var bays in perSide) any |= bays > 0;

        if (!any) perSide[draw.NextInt(perSide.Length)] = Bays(config, ref draw, orNone: false);
    }

    /// <summary>
    /// <b>The yards a town cuts before it cuts a car park of its own</b> (GEN-55, GEN-56), one entry a service
    /// building: a hospital, a police station and a depot in every district, each to be cut inside it.
    /// </summary>
    /// <remarks>
    /// <b>They are cut first because the sites are ranked by distance from the car parks already cut</b>
    /// (<see cref="SiteBook"/>): taken first, and one use across every district before the next, the services
    /// land as far apart as each district's roads allow, which needs no spacing of its own.
    /// <para>
    /// <b>And they are cut on top of the town's own count</b> (GEN-53): the parking a town's buildings ask for is
    /// its people's, and a district's services are not a share of it.
    /// </para>
    /// </remarks>
    static List<(BuildingUse Use, int District)> TheServicesWanted(TownBrief brief, DistrictWheel districts)
    {
        var wanted = new List<(BuildingUse, int)>();
        var each = ServiceBuildings.OfEachUse(brief.Buildings, districts);
        foreach (var use in ServiceBuildings.Uses)
        {
            for (var district = 0; district < each; district++) wanted.Add((use, district));
        }

        return wanted;
    }

    /// <summary>
    /// <b>A service's own car park is a yard: one rank, on one side, as wide as a car park gets</b>
    /// (GEN-55, GEN-4k). <b>One side</b> because a special building stands past the far end of its own rank
    /// and there is only one of it; <b>the widest</b> because the apron it holds is the bays nearest its
    /// door, and a station with three bays stands three vehicles. <b>Which side is the site's to say</b>
    /// (<see cref="FaceTheTown"/>), so the rank is written on the first and moved there.
    /// </summary>
    static void AYard(SimConfig config, Span<int> perSide)
    {
        for (var at = 0; at < perSide.Length; at++) perSide[at] = at == 0 ? config.CityGen.BaysPerLotMost : 0;
    }

    static int Bays(SimConfig config, ref Rng draw, bool orNone)
    {
        var fewest = config.CityGen.BaysPerLotFewest;
        var band = config.CityGen.BaysPerLotMost - fewest + 1;
        if (!orNone) return fewest + draw.NextInt(band);

        var drawn = draw.NextInt(band + 1);
        return drawn == 0 ? 0 : fewest + drawn - 1;
    }
}
