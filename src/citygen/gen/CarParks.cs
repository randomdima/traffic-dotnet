using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>One car park for every few of the buildings the brief plans, laid off the kerb of a street that stays
/// whole</b> (GEN-53, <see cref="SimConfig.CarParksFor"/>). A car park is a rank of bays — on one side of the
/// street or both — and <b>every bay is a short road of its own joined to nothing</b>: the street is not parted,
/// no junction is cut and no movement is drawn between the street and a bay. What gets a car in and out of one
/// is the car's own manoeuvre (GEN-4f), laid when it parks and not with the town.
/// </summary>
/// <remarks>
/// <para>
/// <b>A bay is a lane's width of ground square to the kerb</b>, a bay long, <b>driven both ways over one
/// line</b> (<see cref="CityPlan.RoadArrays.DrivenOverOneLine"/>) so a body standing in it is on it whichever
/// way round it stands. The bays of one side are parallel and a lane apart, and each one's near end runs a hair
/// back over the street's own ground (<see cref="SimConfig.CarParkKerbOverlapM"/>), so the rank and the street
/// are one piece of tarmac (TER-3c.8) and not ground the two share (TER-5c).
/// </para>
/// <para>
/// <b>A bay's line is its own</b> (<see cref="CityPlan.RoadArrays.WasCut"/>): it is laid here, where the kerb
/// is, and the arms at its two nodes are read off it rather than drawn to them (GEN-52's reading).
/// </para>
/// <para>
/// <b>So the bays are counted before the place is chosen.</b> How much street a car park takes is what the sites
/// are found with (<see cref="SimConfig.CarParkFrontageM"/>, <see cref="CutJunctions.Sites"/>): the rank and the
/// street a car manoeuvres over past either end of it, on a stretch straight enough to square a rank to
/// (<see cref="SimConfig.CarParkCurvatureMax"/>) — rather than a place being taken and the bays that did not fit
/// on it taken back off again (GEN-10).
/// </para>
/// <para>
/// <b>They are spread and not scattered.</b> Each is laid at the site furthest from every car park already
/// laid, which is the whole of "evenly over the town" and needs no spacing figure of its own. What keeps two of
/// them off each other's ground is the locality every node owes (GEN-16), which the sites and the bays' own
/// nodes are held to.
/// </para>
/// <para>
/// <b>What is left of a count the ground cannot carry is what fitted</b> (GEN-8). A town with no road
/// straight enough, long enough or clear enough for another car park lays fewer, and the census reports the
/// shortfall rather than the generator trying again.
/// </para>
/// <para>
/// <b>The first few are laid for the services and not for the town</b> (GEN-55): every district's hospital,
/// police station and depot (GEN-56) gets a yard of its own inside it — one rank, on one side, as wide as a car
/// park gets — and the building is stood past the far end of it afterwards (<see cref="BuildingStage"/>).
/// They are laid first because the sites are ranked by distance from the car parks already laid, so taking
/// them first is what puts the services as far apart as the town's roads allow.
/// </para>
/// <para>
/// <b>It is the last stage of the layout</b> (<see cref="TownGenerator"/>): a bay's arms are read off its own
/// line, so nothing may be offered to the layout after one.
/// </para>
/// </remarks>
internal static class CarParks
{
    /// <summary>The car parks a town came out with.</summary>
    /// <param name="Street">The road each car park's rank stands off, one per car park.</param>
    /// <param name="AtM">
    /// Where along that road's line the car park is centred — the middle of its rank, on the line the street
    /// was laid down before it was moved onto its driven half (TER-4d).
    /// </param>
    /// <param name="For">
    /// Which use each car park was laid for (GEN-55): <see cref="BuildingUse.Ordinary"/> for the town's own,
    /// and a service use for one laid to stand a special building's vehicles. <b>It is the stage's own answer
    /// and not the plan's</b> — what holds a bay for a station is the apron, which finds the bays nearest its
    /// door (GEN-4k) — so it travels only as far as the stage that stands the building on it.
    /// </param>
    internal readonly record struct Laid(
        int[] Street, Vector2[] AtM, int[] BayOffsets, int[] Road, bool[] Right, BuildingUse[] For)
    {
        public static Laid None => new([], [], [0], [], [], []);

        public int Count => Street.Length;
    }

    /// <summary>The two sides of a road, as the driver's right of its own direction and its left.</summary>
    static readonly bool[] Sides = [true, false];

    /// <param name="districts">
    /// The wheel the town was laid on: its hub is the middle a yard's rank faces (<see cref="FaceTheTown"/>), and
    /// each district is laid a yard for each service (GEN-56).
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

        var street = new List<int>();
        var atM = new List<Vector2>();
        var bayOffsets = new List<int> { 0 };
        var road = new List<int>();
        var right = new List<bool>();
        var forUse = new List<BuildingUse>();
        var parks = new PointCells(config.Grid.Main);
        var books = new Dictionary<int, SiteBook>();
        var refused = new List<int>();

        Span<int> perSide = stackalloc int[Sides.Length];
        var onTheRight = new bool[config.CityGen.BaysPerLotMost * Sides.Length];

        for (var want = 0; want < wanted; want++)
        {
            // <b>The bays are counted before the place is chosen and never after</b> (GEN-53): how much street a
            // car park takes is how far its longest rank reaches along it, so the size of the car park is what
            // decides which places can carry one (GEN-10).
            var (use, district) = want < services.Count ? services[want] : (BuildingUse.Ordinary, AnyDistrict);
            if (use == BuildingUse.Ordinary) BaysPerSide(config, perSide, ref draw);
            else AYard(config, perSide);

            Vector2? facingM = use == BuildingUse.Ordinary ? null : townM;

            var mostBays = Math.Max(perSide[0], perSide[1]);
            var frontageM = config.CarParkFrontageM(mostBays);
            var curvatureMax = config.CarParkCurvatureMax(mostBays);
            var reach = use == BuildingUse.Ordinary ? default : YardReach.Of(config, sizes, use);

            int[]? bays = null;
            CutJunctions.Site laidAt = default;
            if (parks.Count == 0)
            {
                // <b>The first one is drawn</b>, there being nothing yet to stand away from.
                var sites = CutJunctions.Sites(layout, config, frontageM, curvatureMax);
                if (sites.Count == 0) break;

                var first = draw.NextInt(sites.Count);
                (sites[0], sites[first]) = (sites[first], sites[0]);
                foreach (var site in sites)
                {
                    if (!IsIn(layout, site, districts, district, reach)) continue;

                    bays = At(layout, config, site, perSide, facingM, onTheRight);
                    laidAt = site;
                    if (bays is not null) break;
                }
            }
            else
            {
                if (!books.TryGetValue(mostBays, out var book))
                {
                    books[mostBays] = book = new SiteBook(layout, config, frontageM, curvatureMax, parks);
                }

                // <b>A refusal costs that site and never the car park</b>: what a site is refused for is the
                // ground beside one bay rather than anything about the road it was offered on, and the site
                // after it is somewhere else entirely. The site stays in the book for the next car park.
                refused.Clear();
                for (var entry = book.Next(); entry >= 0; entry = book.Next())
                {
                    if (IsIn(layout, book[entry], districts, district, reach))
                    {
                        bays = At(layout, config, book[entry], perSide, facingM, onTheRight);
                        laidAt = book[entry];
                        if (bays is not null) break;
                    }

                    refused.Add(entry);
                }

                foreach (var entry in refused) book.Queue(entry);
            }

            // <b>A district with nowhere to lay a yard stands that service nowhere</b> (GEN-8), and the next
            // is still asked for: it is that district's ground that ran out and not the town's.
            if (bays is null && use != BuildingUse.Ordinary) continue;

            if (bays is null) break;

            street.Add(laidAt.Road);
            atM.Add(laidAt.AtM);
            parks.Add(laidAt.AtM);
            forUse.Add(use);
            for (var bay = 0; bay < bays.Length; bay++)
            {
                road.Add(bays[bay]);
                right.Add(onTheRight[bay]);
            }

            bayOffsets.Add(road.Count);
        }

        return new Laid([.. street], [.. atM], [.. bayOffsets], [.. road], [.. right], [.. forUse]);
    }

    const int AnyDistrict = -1;

    /// <summary>
    /// <b>Whether a site is inside the district a yard is wanted in</b> (GEN-56): the ground its building will
    /// stand on, across the rank on the side it faces (<see cref="FaceTheTown"/>), near face and far face both.
    /// <b>The building and not the road</b>, because a district's edge is as often as not a spoke or the orbital:
    /// asked of the road alone, a yard laid on one stood its building in the district over the road, and a yard
    /// on one facing into the district is that district's.
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
        public static YardReach Of(SimConfig config, BuildingSizes sizes, BuildingUse use)
        {
            var nearM = config.LaneWidthM + config.CarParkBayDepthM + config.WalkOuterM + config.CityGen.BuildingPitchM;
            return new YardReach(nearM, nearM + sizes.Of(use).Y);
        }
    }

    /// <summary>
    /// <b>One car park's bays laid at one site</b>, as the roads they are, or nothing where the town cannot have
    /// every one of them there. Which side each stands on is written into <paramref name="onTheRight"/> in the
    /// same order.
    /// </summary>
    /// <param name="facingM">Where a yard's rank faces (<see cref="FaceTheTown"/>), or nothing for the town's own car park.</param>
    static int[]? At(
        TownLayout layout, SimConfig config, CutJunctions.Site site, ReadOnlySpan<int> perSide, Vector2? facingM,
        bool[] onTheRight)
    {
        Span<int> sides = stackalloc int[Sides.Length];
        perSide.CopyTo(sides);
        if (facingM is { } townM) FaceTheTown(layout, site, townM, sides);

        var line = layout.LineOf(site.Road);
        var flow = layout.Edges[site.Road].Flow;
        var middle = Spline.SampleAt(line, site.AlongM);
        if (!layout.Dry(middle.PositionM) || !layout.StandsClear(middle.PositionM)) return null;

        var bays = sides[0] + sides[1];
        var nodesM = new Vector2[bays * 2];
        var edges = new LayoutEdge[bays];
        var lines = new ArcSeg[bays][];
        var firstNode = layout.NodeM.Count;
        ReadOnlySpan<int> instead = [site.Road];

        var taken = 0;
        foreach (var right in Sides)
        {
            var rank = sides[right ? 0 : 1];
            var outward = right ? middle.Right : -middle.Right;
            var kerbM = LaneTowardM(config, flow, right) + (config.LaneWidthM * 0.5f);
            for (var bay = 0; bay < rank; bay++)
            {
                var asideM = (bay - ((rank - 1) * 0.5f)) * config.LaneWidthM;
                var footM = middle.PositionM + (middle.Direction * asideM);
                var fromM = footM + (outward * (KerbDepthM(line, middle, footM, outward, kerbM, config) - config.CarParkKerbOverlapM));
                var lengthM = Vector2.Dot(footM + (outward * kerbM) - fromM, outward) + config.CarParkBayDepthM;
                var bayLine = new ArcSeg(fromM, MathF.Atan2(outward.Y, outward.X), lengthM, 0f);

                // <b>A node stands a lead off each end, as every node does</b> (GEN-46), and each owes the town a
                // locality like any other (GEN-16) — the one over the street as much as the one past the far end.
                var leadM = config.CityGen.ConnectionStandoffM;
                var nearM = fromM - (outward * leadM);
                var farM = bayLine.EndM + (outward * leadM);
                if (!layout.Dry(farM) || !layout.StandsClear(farM) || !layout.StandsClear(nearM)) return null;

                var node = firstNode + (taken * 2);
                var edge = new LayoutEdge(node, node + 1, RoadClass.CarPark, 0f, RoadFlow.BothWays, []);

                // <b>Asked of the bay and of the lead past it</b>: the node at the far end stands a lead past the
                // line, so the ground a bay takes reaches further than the road it lays. The street it stands off
                // is the one road it may share ground with (GEN-49).
                var reach = new ArcSeg(fromM, bayLine.HeadingRad, lengthM + leadM, 0f);
                if (!layout.Clear([reach], edge, instead)) return null;

                nodesM[taken * 2] = nearM;
                nodesM[(taken * 2) + 1] = farM;
                edges[taken] = edge;
                lines[taken] = [bayLine];
                onTheRight[taken] = right;
                taken++;
            }
        }

        var firstRoad = layout.Edges.Count;
        layout.Stand(nodesM, edges, lines);

        var laid = new int[bays];
        for (var bay = 0; bay < bays; bay++) laid[bay] = firstRoad + bay;

        return laid;
    }

    /// <summary>
    /// <b>How deep along <paramref name="outward"/> from the rank's middle the street's own edge lies under one
    /// bay's mouth</b> — the shallowest of it across the bay's width, so the whole mouth runs back over the
    /// street's ground however the kerb bends under it.
    /// </summary>
    static float KerbDepthM(
        ReadOnlySpan<ArcSeg> line, in SplineSample middle, Vector2 footM, Vector2 outward, float kerbM, SimConfig config)
    {
        var lengthM = Spline.TotalLengthM(line);
        var halfM = config.LaneWidthM * 0.5f;
        var depthM = float.PositiveInfinity;
        for (var corner = -1; corner <= 1; corner++)
        {
            var atM = footM + (middle.Direction * (corner * halfM));
            var on = Spline.SampleAt(line, Spline.ProjectM(line, atM, lengthM * 0.5f, lengthM));
            var sideways = Vector2.Dot(on.Right, outward) >= 0f ? on.Right : -on.Right;
            depthM = MathF.Min(depthM, Vector2.Dot(on.PositionM + (sideways * kerbM) - middle.PositionM, outward));
        }

        return depthM;
    }

    /// <summary>
    /// <b>A yard's one rank goes on the side of its road facing the town's middle</b> (GEN-55): its building
    /// stands past the rank's far end, and the edge of a town is where the ground to stand one on runs out.
    /// Drawn instead, a yard laid on a road along the edge of the map faced off it half the time and its
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
    /// <b>How far toward one side of a road the lane on that side runs</b>, from the line the road was laid
    /// down. A road driven both ways carries a lane each side, so it is half a lane toward either; a road driven
    /// one way carries its one lane on the half it was moved onto (<see cref="RoadStage.DrivenHalfM"/>, TER-4d),
    /// so the side it was moved off reaches the same lane the other way. Half a lane further is that side's kerb.
    /// </summary>
    public static float LaneTowardM(SimConfig config, RoadFlow flow, bool right)
    {
        if (flow == RoadFlow.BothWays) return config.LaneOffsetM;

        var halfM = RoadStage.DrivenHalfM(config, flow, RoadStage.WidthM(config, flow));
        return right ? halfM : -halfM;
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
    /// <b>Every place one size of car park could be laid, kept from one car park to the next</b> and handed out
    /// furthest from every car park already laid first — which spreads them over the town without a spacing of
    /// their own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The sites are read once</b>: the sites on a road are a question about that road's line alone, and
    /// laying a car park changes no road's line. Whether a site still stands a locality clear of every node
    /// (GEN-16) is asked as it comes up, since the nodes a car park adds are the only thing that can take one
    /// away.
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
        readonly PointCells _parks;

        readonly List<CutJunctions.Site> _site = [];
        readonly List<float> _nearestSq = [];

        /// <summary>How many car parks each site's <see cref="_nearestSq"/> was measured against.</summary>
        readonly List<int> _parksCounted = [];

        readonly PriorityQueue<int, SiteOrder> _queue = new(FurthestFirst.Instance);

        public SiteBook(TownLayout layout, SimConfig config, float frontageM, float curvatureMax, PointCells parks)
        {
            _layout = layout;
            _parks = parks;

            foreach (var site in CutJunctions.Sites(layout, config, frontageM, curvatureMax)) Enter(site);
        }

        public CutJunctions.Site this[int entry] => _site[entry];

        /// <summary>The furthest site that still stands, taken out of the book, or −1 where none is left.</summary>
        public int Next()
        {
            while (_queue.TryDequeue(out var entry, out _))
            {
                var site = _site[entry];
                if (!_layout.StandsClear(site.AtM)) continue;

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
            _nearestSq.Add(_parks.NearestSq(site.AtM));
            _parksCounted.Add(_parks.Count);
            Queue(entry);
        }
    }

    /// <summary>
    /// <b>How many bays each side of the road carries</b>: none, or a handful between the two a lot may be
    /// (GEN-4b). <b>Not both none</b> — a car park with no bay on either side is no car park.
    /// </summary>
    static void BaysPerSide(SimConfig config, Span<int> perSide, ref Rng draw)
    {
        for (var side = 0; side < perSide.Length; side++) perSide[side] = Bays(config, ref draw, orNone: true);

        var any = false;
        foreach (var bays in perSide) any |= bays > 0;

        if (!any) perSide[draw.NextInt(perSide.Length)] = Bays(config, ref draw, orNone: false);
    }

    /// <summary>
    /// <b>The yards a town lays before it lays a car park of its own</b> (GEN-55, GEN-56), one entry a service
    /// building: a hospital, a police station and a depot in every district, each to be laid inside it.
    /// </summary>
    /// <remarks>
    /// <b>They are laid first because the sites are ranked by distance from the car parks already laid</b>
    /// (<see cref="SiteBook"/>): taken first, and one use across every district before the next, the services
    /// land as far apart as each district's roads allow, which needs no spacing of its own.
    /// <para>
    /// <b>And they are laid on top of the town's own count</b> (GEN-53): the parking a town's buildings ask for is
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
