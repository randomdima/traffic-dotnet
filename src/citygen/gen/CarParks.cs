using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>One car park for every few of the buildings the brief plans, laid off the kerb of a street that stays
/// whole</b> (GEN-53, <see cref="SimConfig.CarParksFor"/>). A car park is a rank of bays — on one side of the
/// street or both — and <b>every bay is a short road of its own joined to nothing</b>: the street is not parted,
/// no junction stands at it and no movement is drawn between the street and a bay. What gets a car in and out of one
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
/// <b>A bay's line is its own</b> (<see cref="CityPlan.RoadArrays.IsABay"/>): it is laid here, where the kerb
/// is, and the arms at its two nodes are read off it rather than drawn to them (GEN-52's reading).
/// </para>
/// <para>
/// <b>So the bays are counted before the place is chosen.</b> How much street a car park takes is what the sites
/// are found with (<see cref="SimConfig.CarParkFrontageM"/>, <see cref="RoadsideSites.Sites"/>): the rank and the
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
/// <b>It is the last stage of the layout</b> (<see cref="TownGen.Lay"/>): a bay's arms are read off its own
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
    /// <param name="District">
    /// The district each car park was laid for: a yard's own (GEN-56), and <see cref="AnyDistrict"/> for the town's.
    /// </param>
    internal readonly record struct Laid(
        int[] Street, Vector2[] AtM, int[] BayOffsets, int[] Road, bool[] Right, BuildingUse[] For, int[] District)
    {
        public static Laid None => new([], [], [0], [], [], [], []);

        public int Count => Street.Length;
    }

    /// <summary>The two sides of a road, as the driver's right of its own direction and its left.</summary>
    static readonly bool[] Sides = [true, false];

    /// <param name="districts">
    /// The districts the town was laid in: each is laid a yard for each service (GEN-56), on whichever side of its road
    /// the building stands inside the district (<see cref="YardSites"/>).
    /// </param>
    /// <param name="sizes">The service buildings' footprints, which say how far across its rank a yard's building reaches.</param>
    /// <param name="extentM">The town's size, which the car parks stand within.</param>
    /// <param name="parksHere">
    /// Whether the town's own car parks may stand at a place (GEN-53): not in a suburb, whose houses park on their own
    /// plots. A service's yard stands wherever its district is.
    /// </param>
    public static Laid Lay(
        TownLayout layout, int buildings, SimConfig config, DistrictMap districts, BuildingSizes sizes, Vector2 extentM,
        Func<Vector2, bool> parksHere, ref Rng draw)
    {
        var services = TheServicesWanted(buildings, districts, config.CityGen);
        var wanted = services.Count + config.CarParksFor(buildings);
        if (wanted <= 0) return Laid.None;

        var street = new List<int>();
        var atM = new List<Vector2>();
        var bayOffsets = new List<int> { 0 };
        var road = new List<int>();
        var right = new List<bool>();
        var forUse = new List<BuildingUse>();
        var forDistrict = new List<int>();
        var parks = new PointCells(config.Grid.Main, Vector2.Zero, extentM);
        var books = new Dictionary<int, SiteBook>();
        var yards = new Dictionary<BuildingUse, YardSites>();

        Span<int> perSide = stackalloc int[Sides.Length];
        var mostAYard = config.CityGen.BaysPerLotMost;
        foreach (var use in ServiceBuildings.Uses) mostAYard = Math.Max(mostAYard, YardBays(config, sizes, use));
        var onTheRight = new bool[mostAYard * Sides.Length];
        var handedOn = 0;
        var refused = new HashSet<(BuildingUse, int)>();

        for (var want = 0; want < wanted; want++)
        {
            // <b>The bays are counted before the place is chosen and never after</b> (GEN-53): how much street a
            // car park takes is how far its longest rank reaches along it, so the size of the car park is what
            // decides which places can carry one (GEN-10).
            var (use, district) = want < services.Count ? services[want] : (BuildingUse.Ordinary, AnyDistrict);
            SiteBook book;
            List<YardSite>? yardSites = null;
            if (use == BuildingUse.Ordinary)
            {
                BaysPerSide(config, perSide, ref draw);
                var mostBays = Math.Max(perSide[0], perSide[1]);
                if (!books.TryGetValue(mostBays, out book!))
                {
                    var sites = RoadsideSites.Sites(layout, config, config.CarParkFrontageM(mostBays), config.CarParkCurvatureMax(mostBays));
                    sites.RemoveAll(site => !parksHere(site.AtM));
                    books[mostBays] = book = new SiteBook(layout, parks, sites);
                }
            }
            else
            {
                AYard(config, sizes, use, perSide);
                if (!yards.TryGetValue(use, out var ofUse)) yards[use] = ofUse = new YardSites(layout, config, districts, sizes, use);
                yardSites = ofUse.In(district);
                book = new SiteBook(layout, parks, YardSites.SitesOf(yardSites));
            }

            int[]? bays = null;
            RoadsideSites.Site laidAt = default;
            if (parks.Count == 0)
            {
                // <b>The first one is drawn</b>, there being nothing yet to stand away from.
                var first = book.Count > 0 ? draw.NextInt(book.Count) : 0;
                for (var at = 0; at < book.Count && bays is null; at++)
                {
                    var entry = at == 0 ? first : at == first ? 0 : at;
                    bays = Offer(layout, config, book, entry, yardSites, perSide, onTheRight);
                    laidAt = book[entry];
                }
            }
            else
            {
                // <b>A refusal costs that site and never the car park</b>: what a site is refused for is the
                // ground beside one bay rather than anything about the road it was offered on, and the site
                // after it is somewhere else entirely. The site stays in the book for the next car park that
                // draws ranks it has not refused (SiteBook).
                for (var entry = book.Next(perSide); entry >= 0; entry = book.Next(perSide))
                {
                    bays = Offer(layout, config, book, entry, yardSites, perSide, onTheRight);
                    laidAt = book[entry];
                    if (bays is null) continue;

                    book.Take(entry);
                    break;
                }
            }

            // <b>A district with nowhere to lay a yard hands its catchment's service to its nearest neighbour</b> (GEN-56)
            // that stands none of that use, a few at most, and then stands it nowhere (GEN-8): it is that district's ground
            // that ran out and not the town's.
            if (bays is null && use != BuildingUse.Ordinary)
            {
                refused.Add((use, district));
                if (++handedOn <= YardHandedOnMost && NextDistrict(districts, use, district, services, refused) is { } next)
                {
                    services[want--] = (use, next);
                }
                else
                {
                    handedOn = 0;
                }

                continue;
            }

            handedOn = 0;

            if (bays is null) break;

            street.Add(laidAt.Road);
            atM.Add(laidAt.AtM);
            parks.Add(laidAt.AtM);
            forUse.Add(use);
            forDistrict.Add(district);
            for (var bay = 0; bay < bays.Length; bay++)
            {
                road.Add(bays[bay]);
                right.Add(onTheRight[bay]);
            }

            bayOffsets.Add(road.Count);
        }

        return new Laid([.. street], [.. atM], [.. bayOffsets], [.. road], [.. right], [.. forUse], [.. forDistrict]);
    }

    /// <summary>How many neighbours a catchment's service is handed on to before it is stood nowhere (GEN-56).</summary>
    const int YardHandedOnMost = 4;

    /// <summary>
    /// The built district nearest one that could not stand a use's yard, among those neither asked for that use nor refused
    /// it — or none where every one is.
    /// </summary>
    static int? NextDistrict(
        DistrictMap districts, BuildingUse use, int from, List<(BuildingUse Use, int District)> asked, HashSet<(BuildingUse, int)> refused)
    {
        int? nearest = null;
        var nearestM = float.PositiveInfinity;
        for (var district = 0; district < districts.Count; district++)
        {
            if (!districts.Built(district) || asked.Contains((use, district)) || refused.Contains((use, district))) continue;

            var awayM = Vector2.Distance(districts.CentreM[district], districts.CentreM[from]);
            if (awayM < nearestM) (nearest, nearestM) = (district, awayM);
        }

        return nearest;
    }

    /// <summary>The district a car park of the town's own is laid for: whichever it lands in.</summary>
    public const int AnyDistrict = -1;

    /// <summary>A site refused at its rank's middle, whatever was drawn either side of it (<see cref="At"/>).</summary>
    const int WholeSite = -1;

    /// <summary>
    /// One book entry's bays: a town car park's as drawn, a yard's whole rank on the side its site was filed for
    /// (<see cref="YardSites"/>). What refuses it is written into the book, which refuses it again unasked.
    /// </summary>
    static int[]? Offer(
        TownLayout layout, SimConfig config, SiteBook book, int entry, List<YardSite>? yardSites, ReadOnlySpan<int> perSide,
        bool[] onTheRight)
    {
        Span<int> sides = stackalloc int[Sides.Length];
        if (yardSites is null)
        {
            perSide.CopyTo(sides);
        }
        else
        {
            sides.Clear();
            sides[yardSites[entry].Right ? 0 : 1] = Math.Max(perSide[0], perSide[1]);
        }

        if (book.Refuses(entry, sides)) return null;

        var bays = At(layout, config, book[entry], sides, onTheRight, out var refusedSide);
        if (bays is null) book.Refuse(entry, refusedSide, sides);

        return bays;
    }

    /// <summary>A site a yard can be laid at, and the side of its road the yard's building stands inside its district on.</summary>
    readonly record struct YardSite(RoadsideSites.Site Site, bool Right);

    /// <summary>
    /// <b>Every site one service's yard could be laid at, filed by the district its building would stand in</b>
    /// (GEN-56): the ground across the rank on each side, near face and far face both, read once for the use.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The building and not the road</b>, because a district's edge is as often as not a district road: asked of the
    /// road alone, a yard laid on one stood its building in the district over the road.
    /// </para>
    /// <para>
    /// <b>Both sides are asked</b>, so a site may be filed under two districts, one a side. Asked only of the side
    /// facing a district's middle, a crescent's yard looked across the street from its own ground and was refused.
    /// </para>
    /// <para>
    /// <b>Filed once rather than scanned a yard at a time</b>: a town lays three yards a district, and each asking every
    /// site of the town was the yards times the sites.
    /// </para>
    /// </remarks>
    sealed class YardSites
    {
        readonly Dictionary<int, List<YardSite>> _in = [];

        public YardSites(TownLayout layout, SimConfig config, DistrictMap districts, BuildingSizes sizes, BuildingUse use)
        {
            var rank = YardBays(config, sizes, use);
            var depthM = sizes.Of(use).Y;
            foreach (var site in RoadsideSites.Sites(layout, config, config.CarParkFrontageM(rank), config.CarParkCurvatureMax(rank)))
            {
                var at = Spline.SampleAt(layout.LineOf(site.Road), site.AlongM);
                foreach (var right in Sides)
                {
                    // The kerb on that side, the rank, the walk wrapping it and the pitch a face is walked at (GEN-55).
                    var nearM = KerbM(config, layout.Edges[site.Road], right) + config.CarParkBayDepthM + config.WalkOuterM + config.CityGen.BuildingPitchM;
                    var outward = right ? at.Right : -at.Right;
                    var district = districts.At(at.PositionM + (outward * nearM));
                    if (district < 0 || districts.At(at.PositionM + (outward * (nearM + depthM))) != district) continue;

                    if (!_in.TryGetValue(district, out var list)) _in[district] = list = [];
                    list.Add(new YardSite(site, right));
                }
            }
        }

        public List<YardSite> In(int district) => _in.TryGetValue(district, out var list) ? list : [];

        public static List<RoadsideSites.Site> SitesOf(List<YardSite> yards)
        {
            var sites = new List<RoadsideSites.Site>(yards.Count);
            foreach (var yard in yards) sites.Add(yard.Site);
            return sites;
        }
    }

    /// <summary>
    /// <b>One car park's bays laid at one site</b>, as the roads they are, or nothing where the town cannot have
    /// every one of them there. Which side each stands on is written into <paramref name="onTheRight"/> in the
    /// same order.
    /// </summary>
    /// <param name="refusedSide">
    /// Where nothing is laid, what refused it: the side whose rank did, as its place in <paramref name="sides"/>, or
    /// <see cref="WholeSite"/> where the rank's middle did.
    /// </param>
    static int[]? At(
        TownLayout layout, SimConfig config, RoadsideSites.Site site, ReadOnlySpan<int> sides, bool[] onTheRight,
        out int refusedSide)
    {
        refusedSide = WholeSite;
        var line = layout.LineOf(site.Road);
        var street = layout.Edges[site.Road];
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
            refusedSide = right ? 0 : 1;
            var rank = sides[refusedSide];
            var outward = right ? middle.Right : -middle.Right;
            var kerbM = KerbM(config, street, right);
            for (var bay = 0; bay < rank; bay++)
            {
                var asideM = (bay - ((rank - 1) * 0.5f)) * config.LaneWidthM;
                var footM = middle.PositionM + (middle.Direction * asideM);
                var fromM = footM + (outward * (KerbDepthM(line, middle, footM, outward, kerbM, config) - config.CarParkKerbOverlapM));
                var lengthM = Vector2.Dot(footM + (outward * kerbM) - fromM, outward) + config.CarParkBayDepthM;
                var bayLine = new ArcSeg(fromM, Trig.Atan2(outward.Y, outward.X), lengthM, 0f);

                // <b>A node stands a lead off each end, as every node does</b> (GEN-46), and each owes the town a
                // locality like any other (GEN-16) — the one over the street as much as the one past the far end.
                var leadM = config.CityGen.ConnectionStandoffM;
                var nearM = fromM - (outward * leadM);
                var farM = bayLine.EndM + (outward * leadM);
                if (!layout.Dry(nearM) || !layout.Dry(farM) || !layout.StandsClear(farM) || !layout.StandsClear(nearM)) return null;

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
    /// <b>How far toward one side of a road its kerb stands</b>, from the line the road was laid down: half its
    /// carriageway, every lane it carries counted (GEN-67), and for a road driven one way moved by as far as its
    /// carriageway was moved onto the half its traffic drives (<see cref="RoadStage.DrivenHalfM"/>, TER-4d) — a whole
    /// carriageway out on the side it was moved toward and none on the side it was moved off.
    /// </summary>
    public static float KerbM(SimConfig config, in LayoutEdge edge, bool right)
    {
        var widthM = RoadStage.WidthM(config, edge.Flow, edge.Lanes);
        var movedM = RoadStage.DrivenHalfM(config, edge.Flow, widthM);
        return (widthM * 0.5f) + (right ? movedM : -movedM);
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
    /// Read afresh and sorted for every car park, the stage would be the car parks squared times the sites — a
    /// brief of Odesa ten times over each way would still be here after ten minutes.
    /// </para>
    /// <para>
    /// <b>A refusal is kept, and is the answer the site would give again.</b> Nothing a refusal is read off comes back
    /// while car parks are laid: the water is fixed, nodes and roads are only added, and a bay's ground is a
    /// question about its site, its side and that side's rank alone. So a site refused at its rank's middle leaves
    /// the book, and one refused on a side is never offered that side's rank again.
    /// </para>
    /// <para>
    /// <b>So the book keeps one ranking for each count of bays a side drawn</b>, and a site refused under one leaves
    /// that ranking and no other. A refused site is the furthest from every car park, so in one ranking for every
    /// draw it stayed at the head and every later car park walked past it again — 1.3 million offers for 11 000 car
    /// parks on a large town. Each ranking is the one ranking with what its draw is refused taken out, so the
    /// site each car park is laid at is the same.
    /// </para>
    /// </remarks>
    sealed class SiteBook
    {
        readonly TownLayout _layout;
        readonly PointCells _parks;

        readonly List<RoadsideSites.Site> _site = [];
        readonly List<float> _nearestSq = [];

        /// <summary>How many car parks each site's <see cref="_nearestSq"/> was measured against.</summary>
        readonly List<int> _parksCounted = [];

        /// <summary>
        /// Per entry and side (<see cref="Sides"/>' order), the ranks refused there, a bit each; every bit for a site
        /// refused whole. A rank past the bits is never kept and is asked each time.
        /// </summary>
        readonly ulong[] _refusedRanks;

        /// <summary>Whether a site has left every ranking: laid at, refused whole, or no longer clear of the nodes.</summary>
        readonly bool[] _gone;

        /// <summary>One ranking for each draw of bays a side, keyed by <see cref="DrawKey"/> and made as it is first drawn.</summary>
        readonly Dictionary<(int, int), PriorityQueue<int, SiteOrder>> _byDraw = [];

        /// <param name="sites">The sites, entered in order: an entry is its site's place in this list.</param>
        public SiteBook(TownLayout layout, PointCells parks, List<RoadsideSites.Site> sites)
        {
            _layout = layout;
            _parks = parks;
            _refusedRanks = new ulong[sites.Count * Sides.Length];
            _gone = new bool[sites.Count];

            foreach (var site in sites) Enter(site);
        }

        public RoadsideSites.Site this[int entry] => _site[entry];

        public int Count => _site.Count;

        /// <summary>Whether this site has already refused one of these ranks, and so refuses the car park.</summary>
        public bool Refuses(int entry, ReadOnlySpan<int> sides)
        {
            for (var side = 0; side < sides.Length; side++)
            {
                if ((_refusedRanks[(entry * Sides.Length) + side] & RankBit(sides[side])) != 0) return true;
            }

            return false;
        }

        /// <param name="side">The side whose rank refused, or <see cref="WholeSite"/>.</param>
        public void Refuse(int entry, int side, ReadOnlySpan<int> sides)
        {
            if (side != WholeSite)
            {
                _refusedRanks[(entry * Sides.Length) + side] |= RankBit(sides[side]);
                return;
            }

            for (var each = 0; each < Sides.Length; each++) _refusedRanks[(entry * Sides.Length) + each] = ulong.MaxValue;
            _gone[entry] = true;
        }

        /// <summary>A site a car park was laid at, out of every ranking.</summary>
        public void Take(int entry) => _gone[entry] = true;

        static ulong RankBit(int rank) => rank < 64 ? 1UL << rank : 0UL;

        static (int, int) DrawKey(ReadOnlySpan<int> perSide) => (perSide[0], perSide[1]);

        /// <summary>
        /// The furthest site that still stands and has not refused this draw, taken out of its ranking, or −1 where none
        /// is left. A site returned is the caller's to offer: refused, it stays out of this ranking.
        /// </summary>
        public int Next(ReadOnlySpan<int> perSide)
        {
            var queue = RankingFor(perSide);
            while (queue.TryDequeue(out var entry, out var order))
            {
                if (_gone[entry]) continue;

                var site = _site[entry];
                if (!_layout.StandsClear(site.AtM))
                {
                    _gone[entry] = true;
                    continue;
                }

                if (_parksCounted[entry] < _parks.Count)
                {
                    _nearestSq[entry] = _parks.NearestSq(site.AtM, _nearestSq[entry], _parksCounted[entry]);
                    _parksCounted[entry] = _parks.Count;
                }

                // Ranked at a distance that has fallen since, here or in another ranking: ranked again at this one.
                if (_nearestSq[entry] < order.NearestSq)
                {
                    queue.Enqueue(entry, OrderOf(entry));
                    continue;
                }

                return entry;
            }

            return -1;
        }

        PriorityQueue<int, SiteOrder> RankingFor(ReadOnlySpan<int> perSide)
        {
            ref var queue = ref CollectionsMarshal.GetValueRefOrAddDefault(_byDraw, DrawKey(perSide), out var had);
            if (had) return queue!;

            queue = new PriorityQueue<int, SiteOrder>(FurthestFirst.Instance);
            for (var entry = 0; entry < _site.Count; entry++)
            {
                if (!_gone[entry]) queue.Enqueue(entry, OrderOf(entry));
            }

            return queue;
        }

        SiteOrder OrderOf(int entry) => new(_nearestSq[entry], _site[entry].Road, _site[entry].AlongM);

        void Enter(RoadsideSites.Site site)
        {
            _site.Add(site);
            _nearestSq.Add(_parks.NearestSq(site.AtM));
            _parksCounted.Add(_parks.Count);
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
    /// building: a hospital, a police station and a depot in every district a street stands in, each to be laid inside it.
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
    static List<(BuildingUse Use, int District)> TheServicesWanted(int buildings, DistrictMap districts, CityGenFigures figures)
    {
        var wanted = new List<(BuildingUse, int)>();
        foreach (var use in ServiceBuildings.Uses)
        {
            foreach (var district in ServiceBuildings.Serving(use, buildings, districts, figures)) wanted.Add((use, district));
        }

        return wanted;
    }

    /// <summary>
    /// <b>A service's own car park is a yard: one rank, on one side, as wide as a car park gets or as its
    /// building's frontage, whichever is wider</b> (GEN-55, GEN-4k). <b>One side</b> because a special building
    /// stands past the far end of its own rank and there is only one of it; <b>the widest</b> because the apron
    /// it holds is the bays nearest its door, and a station with three bays stands three vehicles. <b>Which side
    /// is the site's to say</b> (<see cref="YardSites"/>), so the rank is written on the first and moved there.
    /// </summary>
    static void AYard(SimConfig config, BuildingSizes sizes, BuildingUse use, Span<int> perSide)
    {
        var bays = YardBays(config, sizes, use);
        for (var at = 0; at < perSide.Length; at++) perSide[at] = at == 0 ? bays : 0;
    }

    /// <summary>
    /// <b>How many bays a service's yard holds</b>: enough that its building's whole frontage stands on the flat of
    /// the rank with a lane to spare (<c>BuildingStage.SquareOnItsRank</c>), and never fewer than the widest lot.
    /// </summary>
    internal static int YardBays(SimConfig config, BuildingSizes sizes, BuildingUse use) =>
        Math.Max(config.CityGen.BaysPerLotMost, (int)MathF.Ceiling(sizes.Of(use).X / config.LaneWidthM) + 1);

    static int Bays(SimConfig config, ref Rng draw, bool orNone)
    {
        var fewest = config.CityGen.BaysPerLotFewest;
        var band = config.CityGen.BaysPerLotMost - fewest + 1;
        if (!orNone) return fewest + draw.NextInt(band);

        var drawn = draw.NextInt(band + 1);
        return drawn == 0 ? 0 : fewest + drawn - 1;
    }
}
