using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Parking;

/// <summary>
/// <b>The street each bay is worked off</b> (GEN-4f): the lanes of the road its car park stands off, and how
/// far along each of them the bay's mouth is. <b>Nothing else about getting in or out of a bay is laid with the
/// town</b> — the manoeuvre is the car's own, laid from where it stands when it parks or leaves (CAR-15).
/// </summary>
/// <remarks>
/// <b>A bay is a lane of the road</b> (GEN-53) — its own, joined to nothing — so a body standing in one is laid
/// on it like on any other (TER-4c.2), and what a car turning into one sweeps of it is read off the same ground.
/// What is kept here is only which street a car has to be on to reach it, which the road cannot say because
/// the bay joins none.
/// </remarks>
internal sealed class BayStreets
{
    public const int NoBay = -1;

    /// <summary>
    /// The most lanes one bay is worked off: the kerb lane beside it, and the lane across where its street's two ways are
    /// one lane each (<see cref="RoadGraph.LaneCrossesToPass"/>).
    /// </summary>
    public const int MostLanes = 2;

    const int NoLane = -1;

    int[] _lane;
    float[] _atLaneM;
    readonly int[] _firstBayOfLane;
    readonly int[] _baysOffLane;
    readonly int[] _bayOnLane;

    BayStreets(int[] lane, float[] atLaneM, int[] firstBayOfLane, int[] baysOffLane, int[] bayOnLane)
    {
        _lane = lane;
        _atLaneM = atLaneM;
        _firstBayOfLane = firstBayOfLane;
        _baysOffLane = baysOffLane;
        _bayOnLane = bayOnLane;
    }

    public int BayCount => _lane.Length / MostLanes;

    /// <summary>
    /// <b>Room for this many kerb places after the painted bays</b> (GEN-4n), each worked off no street until it is
    /// opened (<see cref="OpenKerb"/>) — so a closed one is a place no car can reach.
    /// </summary>
    public void MakeRoomForKerbs(int kerbs)
    {
        if (_kerbRoadside.Length > 0) throw new InvalidOperationException("the room for kerb places is made once, after the painted bays");

        var painted = _lane.Length;
        _paintedBays = painted / MostLanes;
        Array.Resize(ref _lane, painted + (kerbs * MostLanes));
        Array.Resize(ref _atLaneM, painted + (kerbs * MostLanes));
        _lane.AsSpan(painted).Fill(NoLane);
        _kerbRoadside = new int[kerbs];
        _kerbRoadsideAtM = new float[kerbs];
        Array.Fill(_kerbRoadside, NoLane);
    }

    int _paintedBays;
    int[] _kerbRoadside = [];
    float[] _kerbRoadsideAtM = [];

    /// <summary>
    /// <b>A kerb place worked off the one lane beside its roadside</b> (GEN-4n), at the metre its middle stands abeam of:
    /// a car parks alongside the kerb the way the traffic beside it runs, and leaves the same way.
    /// </summary>
    /// <param name="roadsideAtM">How far along its roadside the place's middle stands.</param>
    public void OpenKerb(int bay, int lane, float atLaneM, int roadside, float roadsideAtM)
    {
        _lane[bay * MostLanes] = lane;
        _atLaneM[bay * MostLanes] = atLaneM;
        _lane[(bay * MostLanes) + 1] = NoLane;
        _kerbRoadside[bay - _paintedBays] = roadside;
        _kerbRoadsideAtM[bay - _paintedBays] = roadsideAtM;
    }

    /// <summary>And worked off nothing again: closed.</summary>
    public void CloseKerb(int bay)
    {
        _lane[bay * MostLanes] = NoLane;
        _kerbRoadside[bay - _paintedBays] = NoLane;
    }

    /// <summary>
    /// <b>The roadside a kerb place stands on</b>, or −1 for a painted bay — which a manoeuvre into or out of it sweeps
    /// as well as the street it is worked off (GEN-4n): a body already standing at the kerb refuses it there.
    /// </summary>
    public int KerbRoadsideOf(int bay) => bay >= _paintedBays && bay - _paintedBays < _kerbRoadside.Length ? _kerbRoadside[bay - _paintedBays] : NoLane;

    /// <summary>How far along its roadside a kerb place's middle stands.</summary>
    public float KerbRoadsideAtM(int bay) => _kerbRoadsideAtM[bay - _paintedBays];

    /// <summary>The lanes of the road, which <see cref="BaysOffLane"/> is asked by.</summary>
    public int LaneCount => _firstBayOfLane.Length - 1;

    /// <summary>
    /// The street lanes this bay is worked off, the kerb lane beside it first — and the lane across only on a street of one
    /// lane each way, the one street whose middle line a car may cross (<see cref="RoadGraph.LaneCrossesToPass"/>).
    /// </summary>
    public ReadOnlySpan<int> LanesOf(int bay)
    {
        var lanes = _lane.AsSpan(bay * MostLanes, MostLanes);
        var count = 0;
        while (count < MostLanes && lanes[count] != NoLane) count++;

        return lanes[..count];
    }

    /// <summary>
    /// <b>How far along a lane the bay's mouth is</b> — the middle of its kerb end, projected onto the lane — or
    /// <see cref="float.NaN"/> where the bay is not worked off that lane.
    /// </summary>
    public float AtLaneM(int bay, int lane)
    {
        for (var slot = bay * MostLanes; slot < (bay + 1) * MostLanes; slot++)
        {
            if (_lane[slot] == lane) return _atLaneM[slot];
        }

        return float.NaN;
    }

    /// <summary>Whether any street lane runs past this bay at all — the one thing a bay needs to be one a car gets into.</summary>
    public bool CanBeReached(int bay) => _lane[bay * MostLanes] != NoLane;

    /// <summary>
    /// <b>The bays worked off one lane</b>, each named once — laid with the town because what a leg asks at a car
    /// park's frontage is a question about that stretch (GEN-4l), and walking every bay to answer it is a scan
    /// per leg.
    /// </summary>
    public ReadOnlySpan<int> BaysOffLane(int lane) =>
        _baysOffLane.AsSpan(_firstBayOfLane[lane], _firstBayOfLane[lane + 1] - _firstBayOfLane[lane]);

    /// <summary>
    /// <b>The bay a lane of the road is</b> (GEN-53), or <see cref="NoBay"/> for a lane of the carriageway.
    /// </summary>
    public int BayOnArm(int lane) => lane >= 0 && lane < _bayOnLane.Length ? _bayOnLane[lane] : NoBay;

    /// <summary>
    /// <b>The lanes a leg may come back the other way from</b>, one flag per lane of the town — a lane with a bay
    /// off it near its end to park in, and a lane running back to leave by (GEN-4l). <b>The data the driving
    /// network is priced off</b>, handed over as flags rather than as this type because the road is below the car
    /// parks that hang off it and a slice may not reach up.
    /// </summary>
    /// <param name="withinM">
    /// How near the lane's end the bay has to be (<see cref="Core.Config.SimConfig.TurnAtALotWithinM"/>): the router
    /// turns a leg at a lane's end, and a turn made further back is one the route was not planned for.
    /// </param>
    public static bool[] WhereALegMayTurn(RoadGraph roads, BayStreets bays, float withinM)
    {
        var turns = new bool[roads.LaneCount];
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            var back = roads.LaneReverse[lane];

            // <b>And a stretch with no way out of it</b>, which is a dead end: a search may turn a leg there
            // with no bay, and nothing turns the car round when it arrives — it stands at the end until its
            // leg's clock gives the leg up (CAR-15a).
            turns[lane] = back >= 0 && !roads.IsABayArm(lane)
                          && (bays.TheBayToTurnIn(roads, lane, withinM) != NoBay || roads.LanesFrom(lane).Length == 0);
        }

        return turns;
    }

    /// <summary>
    /// <b>The bay a lane turns round in</b> (GEN-4l): the one worked off it and off the lane running back furthest along
    /// it, where that is within <paramref name="withinM"/> of the lane's end — or <see cref="NoBay"/>.
    /// </summary>
    public int TheBayToTurnIn(RoadGraph roads, int lane, float withinM)
    {
        var back = roads.LaneReverse[lane];
        if (back == NoLane) return NoBay;

        var best = NoBay;
        var bestM = roads.LaneLengthM[lane] - withinM;
        foreach (var bay in BaysOffLane(lane))
        {
            var atM = AtLaneM(bay, lane);
            if (atM < bestM || float.IsNaN(AtLaneM(bay, back))) continue;

            best = bay;
            bestM = atM;
        }

        return best;
    }

    /// <summary>
    /// <b>Each bay's street, read off the car parks the plan laid</b> (GEN-53): the lanes of the road its car
    /// park stands off, numbered as the plan lists the bays — the numbering the registry keeps them in.
    /// </summary>
    /// <remarks>
    /// <b>The kerb lane is the street lane nearest the bay's mouth</b>, which is the outermost lane of the way keeping
    /// to that kerb, or the lane a one-way street runs beside it — read off where the lanes lie, never off the order a
    /// road numbers them in, which says nothing of the side a bay stands on.
    /// </remarks>
    public static BayStreets Build(CityPlan plan, RoadGraph roads)
    {
        var parks = plan.CarParks;
        var bayCount = parks.Road.Length;

        var lanesOfRoad = new Dictionary<int, List<int>>();
        var bayOnLane = new int[roads.LaneCount];
        Array.Fill(bayOnLane, NoBay);
        var bayOfRoad = new Dictionary<int, int>(bayCount);
        for (var bay = 0; bay < bayCount; bay++) bayOfRoad[parks.Road[bay]] = bay;

        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            var road = roads.LaneRoad[lane];
            if (bayOfRoad.TryGetValue(road, out var own))
            {
                bayOnLane[lane] = own;
                continue;
            }

            if (!roads.IsAStreetLane(lane)) continue;

            if (!lanesOfRoad.TryGetValue(road, out var lanes)) lanesOfRoad[road] = lanes = [];
            lanes.Add(lane);
        }

        var laneOf = new int[bayCount * MostLanes];
        var atLaneM = new float[bayCount * MostLanes];
        Array.Fill(laneOf, NoLane);
        var perLane = new List<int>?[roads.LaneCount];

        for (var park = 0; park < parks.Count; park++)
        {
            if (!lanesOfRoad.TryGetValue(parks.Street[park], out var street)) continue;

            for (var bay = parks.BayOffsets[park]; bay < parks.BayOffsets[park + 1]; bay++)
            {
                var line = plan.Roads.SegmentsOf(parks.Road[bay]);
                if (line.Length == 0) continue;

                var mouthM = line[0].StartM;
                var kerb = NoLane;
                var kerbAtM = 0f;
                var nearestSq = float.PositiveInfinity;
                foreach (var onStreet in street)
                {
                    var lengthM = roads.LaneLengthM[onStreet];
                    var atM = Spline.ProjectM(roads.ArcsOf(onStreet), mouthM, lengthM * 0.5f, lengthM, out var offSq);
                    if (offSq < nearestSq) (kerb, kerbAtM, nearestSq) = (onStreet, atM, offSq);
                }

                if (kerb == NoLane) continue;

                WorkedOff(bay, 0, kerb, kerbAtM);
                var across = roads.LaneReverse[kerb];
                if (across == NoLane || !roads.LaneCrossesToPass[kerb]) continue;

                var acrossM = roads.LaneLengthM[across];
                WorkedOff(bay, 1, across, Spline.ProjectM(roads.ArcsOf(across), mouthM, acrossM * 0.5f, acrossM));
            }
        }

        void WorkedOff(int bay, int slot, int lane, float atM)
        {
            laneOf[(bay * MostLanes) + slot] = lane;
            atLaneM[(bay * MostLanes) + slot] = atM;
            (perLane[lane] ??= []).Add(bay);
        }

        var firstBayOfLane = new int[roads.LaneCount + 1];
        var baysOffLane = new List<int>();
        for (var at = 0; at < roads.LaneCount; at++)
        {
            if (perLane[at] is { } bays) baysOffLane.AddRange(bays);
            firstBayOfLane[at + 1] = baysOffLane.Count;
        }

        return new BayStreets(laneOf, atLaneM, firstBayOfLane, [.. baysOffLane], bayOnLane);
    }
}
