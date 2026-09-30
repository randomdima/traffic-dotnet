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

    /// <summary>The most lanes one bay is worked off: the two ways of its street.</summary>
    public const int MostLanes = 2;

    const int NoLane = -1;

    readonly int[] _lane;
    readonly float[] _atLaneM;
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

    /// <summary>The street lanes this bay is worked off — one on a street driven one way, two on one driven both.</summary>
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
    /// <b>The bay a lane turns round in</b> (GEN-4l): the one worked off it furthest along it, where that is within
    /// <paramref name="withinM"/> of the lane's end — or <see cref="NoBay"/>.
    /// </summary>
    public int TheBayToTurnIn(RoadGraph roads, int lane, float withinM)
    {
        var best = NoBay;
        var bestM = roads.LaneLengthM[lane] - withinM;
        foreach (var bay in BaysOffLane(lane))
        {
            var atM = AtLaneM(bay, lane);
            if (atM < bestM) continue;

            best = bay;
            bestM = atM;
        }

        return best;
    }

    /// <summary>
    /// <b>Each bay's street, read off the car parks the plan laid</b> (GEN-53): the lanes of the road its car
    /// park stands off, numbered as the plan lists the bays — the numbering the registry keeps them in.
    /// </summary>
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
                var slot = 0;
                foreach (var onStreet in street)
                {
                    if (slot == MostLanes) break;

                    var arcs = roads.ArcsOf(onStreet);
                    var lengthM = roads.LaneLengthM[onStreet];
                    laneOf[(bay * MostLanes) + slot] = onStreet;
                    atLaneM[(bay * MostLanes) + slot] = Spline.ProjectM(arcs, mouthM, lengthM * 0.5f, lengthM);
                    (perLane[onStreet] ??= []).Add(bay);
                    slot++;
                }
            }
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
