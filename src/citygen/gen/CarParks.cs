using System.Numerics;
using TrafficSimulation.Core.Config;
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
/// <b>It is the last stage of the layout</b> (<see cref="TownGenerator"/>, <see cref="CutJunctions"/>): the
/// streets that run one way are settled before it, so a cut into one parts a street that is already one way
/// rather than leaving a scatter to choose a street in pieces (GEN-18), and nothing is offered to the layout
/// after it.
/// </para>
/// </remarks>
internal static class CarParks
{
    /// <summary>The car parks a town came out with, and every road their cuts made a cut road (GEN-52).</summary>
    internal readonly record struct Laid(
        int[] Junction, int[] BayOffsets, int[] Road, bool[] Right, int[] CutRoads)
    {
        public static Laid None => new([], [0], [], [], []);
    }

    /// <summary>The two sides of a road, as the driver's right of its own direction and its left.</summary>
    static readonly bool[] Sides = [true, false];

    public static Laid Lay(TownLayout layout, TownBrief brief, SimConfig config, ref Rng draw)
    {
        var wanted = config.CarParksFor(brief.Buildings);
        if (wanted <= 0) return Laid.None;

        var junction = new List<int>();
        var bayOffsets = new List<int> { 0 };
        var road = new List<int>();
        var right = new List<bool>();
        var cutRoads = new List<int>();
        var atM = new List<Vector2>();

        Span<int> perSide = stackalloc int[Sides.Length];
        var arms = new CutArm[config.CityGen.BaysPerLotMost * Sides.Length];
        var onTheRight = new bool[arms.Length];

        for (var want = 0; want < wanted; want++)
        {
            // <b>The bays are counted before the place is chosen and never after</b> (GEN-53): how far back
            // the street has to stand off is how far the longest rank reaches along it, so the size of the
            // car park is what decides which places can carry one — rather than a place being taken and the
            // bays that did not fit on it being taken back off again (GEN-10).
            BaysPerSide(config, perSide, ref draw);
            var mostBays = Math.Max(perSide[0], perSide[1]);
            var standoffM = config.CarParkStandoffM(mostBays);

            var sites = CutJunctions.Sites(layout, config, standoffM, config.CarParkCurvatureMax(mostBays));
            if (sites.Count == 0) break;

            Spread(sites, atM, ref draw);

            var cut = Take(
                layout, config, sites, standoffM, mostBays, arms, perSide, onTheRight, out var taken);
            if (cut is not { } made) break;

            junction.Add(made.Junction);
            atM.Add(layout.NodeM[made.Junction]);
            cutRoads.AddRange(made.Roads);
            for (var bay = 0; bay < taken; bay++)
            {
                road.Add(made.Arms[bay]);
                right.Add(onTheRight[bay]);
            }

            bayOffsets.Add(road.Count);
        }

        return new Laid([.. junction], [.. bayOffsets], [.. road], [.. right], [.. cutRoads]);
    }

    /// <summary>
    /// The first site the town can take, tried in order until one of them is a car park. <b>A refusal costs
    /// that site and never the car park</b>: what a cut is refused for is the ground beside one bay rather
    /// than anything about the road it was offered on, and the site after it is somewhere else entirely.
    /// </summary>
    static Cut? Take(
        TownLayout layout, SimConfig config, List<CutJunctions.Site> sites, float standoffM, int mostBays,
        CutArm[] arms, ReadOnlySpan<int> perSide, bool[] onTheRight, out int taken)
    {
        taken = 0;
        foreach (var site in sites)
        {
            // <b>The ranks are laid off the lane each turns off and not off the node</b> (GEN-53): a street
            // driven one way carries its one lane half a lane to the side the traffic was moved onto
            // (TER-4d), so the two sides ask for different leads on it and for the same lead on a street of
            // two ways. Read here rather than once for the car park, the site being what says which road.
            var flow = layout.Edges[site.Road].Flow;

            taken = 0;
            foreach (var right in Sides)
            {
                var laneTowardM = LaneTowardM(config, flow, right);
                taken += Rank(
                    config, perSide[right ? 0 : 1], right, config.CarParkBayLeadM(mostBays, laneTowardM),
                    config.CarParkArmStandM(mostBays, laneTowardM), arms, onTheRight, taken);
            }

            var cut = CutJunctions.Into(
                layout, config, site.Road, site.AlongM, standoffM, arms.AsSpan(0, taken));
            if (cut is not null) return cut;
        }

        return null;
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
    /// <b>The sites in the order the town would rather have them</b>: furthest from every car park already
    /// cut first, which spreads them over the town without a spacing of their own. <b>The first one is
    /// drawn</b>, there being nothing yet to stand away from.
    /// </summary>
    static void Spread(List<CutJunctions.Site> sites, List<Vector2> takenM, ref Rng draw)
    {
        if (takenM.Count == 0)
        {
            var first = draw.NextInt(sites.Count);
            (sites[0], sites[first]) = (sites[first], sites[0]);
            return;
        }

        sites.Sort((one, other) => NearestM(other.AtM, takenM).CompareTo(NearestM(one.AtM, takenM)));
    }

    static float NearestM(Vector2 atM, List<Vector2> takenM)
    {
        var nearestM = float.PositiveInfinity;
        foreach (var otherM in takenM) nearestM = MathF.Min(nearestM, Vector2.DistanceSquared(atM, otherM));

        return nearestM;
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

    static int Bays(SimConfig config, ref Rng draw, bool orNone)
    {
        var fewest = config.CityGen.BaysPerLotFewest;
        var band = config.CityGen.BaysPerLotMost - fewest + 1;
        if (!orNone) return fewest + draw.NextInt(band);

        var drawn = draw.NextInt(band + 1);
        return drawn == 0 ? 0 : fewest + drawn - 1;
    }
}
