using System.Collections.Concurrent;
using System.Numerics;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>Where on the pavement every place a walk begins or ends stands, worked out while the map is built</b> (the root
/// decision log, 2026-10-13): a building's ways in, a bay's way in either way round, the kerb beside every lane — and
/// each walker's own last place on the pavement, kept as it walks. A walk is laid from one of these to another, and
/// nothing is looked up while the town runs but what a player's order names.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>Each building's way in, on the pavement — <see cref="FootPlace.Nowhere"/> where the hop off the network would not reach it.</summary>
    FootPlace[] _entryPlace = [];

    /// <summary>Each bay's way in, nose in and backed in, on the pavement.</summary>
    FootPlace[] _wayInPlace = [];

    /// <summary>The pavement beside every lane, a lane's width of it apart, from where each lane's run of them begins.</summary>
    FootPlace[] _kerbPlace = [];

    int[] _kerbOfLane = [];

    /// <summary>Where each walker last stood on the pavement, which a walk is laid from — a body off it goes straight back there.</summary>
    FootPlace[] _placeOf = [];

    /// <summary>Where each walker's walk is going, on the pavement.</summary>
    FootPlace[] _goalPlace = [];

    /// <summary>The street beside each building, where a leg to its door with no bay to aim at ends.</summary>
    RoadPlace[] _roadPlaceOfBuilding = [];

    /// <summary>The street beside each stretch of pavement, at its middle — where somebody lying on it is reached from.</summary>
    RoadPlace[] _streetBesideFootway = [];

    /// <summary>Where each car's leg aimed at a place in the road is going — <see cref="RoadPlace.Nowhere"/> for one aimed at a bay, or a player's order.</summary>
    RoadPlace[] _destinationPlace = [];

    /// <summary>
    /// <b>The places laid with the map</b>: every look-up a walk needs, made once — each a hop off the network no
    /// further than a walker may take (<see cref="Core.Config.SimConfig.PersonOffNetworkHopM"/>) where it has to be.
    /// </summary>
    void LayThePlaces(int walkers, int drivers)
    {
        _placeOf = new FootPlace[walkers];
        _goalPlace = new FootPlace[walkers];
        Array.Fill(_placeOf, FootPlace.Nowhere);
        Array.Fill(_goalPlace, FootPlace.Nowhere);
        _destinationPlace = new RoadPlace[drivers];
        Array.Fill(_destinationPlace, RoadPlace.Nowhere);

        // <b>Every item writes its own slot</b>, so each pass runs side by side; a thread's scans go back to be taken up by
        // the next pass rather than laid again, the pavement's being the size of every lane of it.
        var spare = new ConcurrentBag<PlaceScans>();
        Func<PlaceScans> taken = () => spare.TryTake(out var own) ? own : new PlaceScans(_roads.NewStreetScan(), _walking.NewScan());
        Action<PlaceScans> given = spare.Add;

        var buildings = _plan.Buildings;
        _roadPlaceOfBuilding = new RoadPlace[buildings.Count];
        InChunks.Over(
            buildings.Count, taken,
            (scans, building) => _roadPlaceOfBuilding[building] =
                StreetNearest(scans.Street, DoorOf(building, buildings.CentreM[building])),
            given);

        _streetBesideFootway = new RoadPlace[_foot.EdgeCount];
        InChunks.Over(
            _foot.EdgeCount, taken,
            (scans, edge) => _streetBesideFootway[edge] =
                StreetNearest(scans.Street, Spline.SampleAt(_foot.ArcsOf(edge), _foot.LengthM(edge) * 0.5f).PositionM),
            given);

        var entries = _plan.Buildings.EntryPointM;
        _entryPlace = new FootPlace[entries.Length];
        InChunks.Over(
            entries.Length, taken,
            (scans, entry) =>
            {
                var place = _walking.PlaceNearest(scans.Foot, entries[entry]);
                _entryPlace[entry] = place.IsSomewhere && (PointOf(place) - entries[entry]).Length() <= _config.PersonOffNetworkHopM
                    ? place
                    : FootPlace.Nowhere;
            },
            given);

        // Every place's way in has its slot, a kerb place's filled when it is opened (GEN-4n).
        _wayInPlace = new FootPlace[_parking.BayCount * 2];
        InChunks.Over(
            _parking.PaintedCount, taken,
            (scans, bay) =>
            {
                _wayInPlace[2 * bay] = _walking.PlaceNearest(scans.Foot, _parking.WayInM(bay, noseIn: false));
                _wayInPlace[(2 * bay) + 1] = _walking.PlaceNearest(scans.Foot, _parking.WayInM(bay, noseIn: true));
            },
            given);

        var stepM = _config.LaneWidthM;
        _kerbOfLane = new int[_roads.LaneCount + 1];
        var count = 0;
        for (var lane = 0; lane < _roads.LaneCount; lane++)
        {
            _kerbOfLane[lane] = count;
            count += KerbStepsOf(lane, stepM);
        }

        _kerbOfLane[_roads.LaneCount] = count;
        _kerbPlace = new FootPlace[count];
        InChunks.Over(_roads.LaneCount, taken, (scans, lane) => LayTheKerbOf(lane, stepM, scans.Foot), given);
    }

    /// <summary>One thread's working sets over the carriageway and the pavement while the places are laid.</summary>
    sealed record PlaceScans(ChainIndex.Scan Street, ChainIndex.Scan Foot);

    void LayTheKerbOf(int lane, float stepM, ChainIndex.Scan foot)
    {
        var arcs = _roads.ArcsOf(lane);
        for (var step = 0; step < KerbStepsOf(lane, stepM); step++)
        {
            var at = Spline.SampleAt(arcs, MathF.Min(step * stepM, _roads.LaneLengthM[lane]));
            var kerbM = at.PositionM + (at.Right * _config.RoadSideSign * _roads.LaneWidthM[lane]);
            _kerbPlace[_kerbOfLane[lane] + step] = _walking.PlaceNearest(foot, kerbM);
        }
    }

    int KerbStepsOf(int lane, float stepM) => (int)MathF.Ceiling(_roads.LaneLengthM[lane] / stepM) + 1;

    /// <summary>
    /// <b>For every lane, the lanes with bays off them within a walk of it</b> (<see cref="BaysNearReach"/>): where a
    /// free bay near a place on that lane is looked for, so a bay is found off the lanes and never off the map while the
    /// town runs (the root decision log, 2026-10-13). <c>[of[lane], of[lane + 1])</c> of <see cref="_bayLanesNear"/>.
    /// </summary>
    int[] _bayLanesNearOf = [0];

    int[] _bayLanesNear = [];

    /// <summary>
    /// The box round the centres of the bays off each lane, which a search for a free bay within a distance passes a lane
    /// by on (<see cref="FreeBaysNear"/>): nothing inside a box is nearer than the box.
    /// </summary>
    Vector2[] _bayLaneLeastM = [], _bayLaneMostM = [];

    /// <summary>The buildings whose street is each lane (<see cref="_roadPlaceOfBuilding"/>), <c>[of[lane], of[lane + 1])</c> of <see cref="_buildingsOnLane"/>.</summary>
    int[] _buildingsOnLaneOf = [0];

    int[] _buildingsOnLane = [];

    /// <summary>
    /// How far a lane looks for bays: a walk, and the walk a bay is worth past it — the furthest any place on or off the
    /// lane asks a bay to be from it.
    /// </summary>
    float BaysNearReach => _config.PersonWalkReachM + _config.PersonWalkWorthM;

    /// <summary>
    /// <b>The bays and the doors laid onto the lanes</b>, once, off the town's own index of lanes while the map is
    /// built: each lane's bay lanes within <see cref="BaysNearReach"/>, and each lane's buildings.
    /// </summary>
    void LayTheBaysAndDoorsOntoTheLanes()
    {
        var lanes = _roads.LaneCount;
        var lanesNearBayLane = new int[]?[lanes];
        _bayLaneLeastM = new Vector2[lanes];
        _bayLaneMostM = new Vector2[lanes];
        InChunks.Over(
            lanes, () => new BayReach(_roads.NewLaneScan(), lanes),
            (reach, bayLane) => lanesNearBayLane[bayLane] = LanesNearTheBaysOff(bayLane, reach));

        // Filed a bay lane at a time and in order, so each lane's bay lanes stand ascending and once each.
        var near = new List<int>[lanes];
        for (var bayLane = 0; bayLane < lanes; bayLane++)
        {
            foreach (var lane in lanesNearBayLane[bayLane] ?? []) (near[lane] ??= []).Add(bayLane);
        }

        (_bayLanesNearOf, _bayLanesNear) = Flattened(near);

        var onLane = new List<int>[lanes];
        for (var building = 0; building < _roadPlaceOfBuilding.Length; building++)
        {
            var lane = _roadPlaceOfBuilding[building].Lane;
            if (lane < 0) continue;

            (onLane[lane] ??= []).Add(building);
        }

        (_buildingsOnLaneOf, _buildingsOnLane) = Flattened(onLane);

        static (int[] Of, int[] All) Flattened(List<int>?[] lists)
        {
            var of = new int[lists.Length + 1];
            for (var at = 0; at < lists.Length; at++) of[at + 1] = of[at] + (lists[at]?.Count ?? 0);

            var all = new int[of[^1]];
            for (var at = 0; at < lists.Length; at++) lists[at]?.CopyTo(all, of[at]);

            return (of, all);
        }
    }

    /// <summary>
    /// <b>The lanes within <see cref="BaysNearReach"/> of any bay off one lane</b>, each once — null for a lane with no
    /// bays — and that lane's box round its bays (<see cref="_bayLaneLeastM"/>), which is its own slot.
    /// </summary>
    int[]? LanesNearTheBaysOff(int bayLane, BayReach reach)
    {
        _bayLaneLeastM[bayLane] = new Vector2(float.PositiveInfinity);
        _bayLaneMostM[bayLane] = new Vector2(float.NegativeInfinity);
        var bays = _bayStreets.BaysOffLane(bayLane);
        if (bays.IsEmpty) return null;

        reach.Found.Clear();
        foreach (var bay in bays)
        {
            _bayLaneLeastM[bayLane] = Vector2.Min(_bayLaneLeastM[bayLane], _parking.CentreM(bay));
            _bayLaneMostM[bayLane] = Vector2.Max(_bayLaneMostM[bayLane], _parking.CentreM(bay));
            var found = _roads.LanesAround(reach.Scan, _parking.CentreM(bay), BaysNearReach, reach.Around);
            if (found > reach.Around.Length)
            {
                reach.Around = new int[found];
                found = _roads.LanesAround(reach.Scan, _parking.CentreM(bay), BaysNearReach, reach.Around);
            }

            foreach (var lane in reach.Around.AsSpan(0, found))
            {
                if (reach.MetFor[lane] == bayLane + 1) continue;

                reach.MetFor[lane] = bayLane + 1;
                reach.Found.Add(lane);
            }
        }

        return [.. reach.Found];
    }

    /// <summary>One thread's working set for <see cref="LanesNearTheBaysOff"/>.</summary>
    sealed class BayReach(ChainIndex.Scan scan, int lanes)
    {
        public ChainIndex.Scan Scan => scan;

        public int[] Around { get; set; } = new int[256];

        /// <summary>The bay lane, one up so that nought is none, each lane was last found for.</summary>
        public int[] MetFor { get; } = new int[lanes];

        public List<int> Found { get; } = [];
    }

    /// <summary>
    /// <b>The free bays within <paramref name="withinM"/> of a place a car can be got into</b>, nearest first, off the bay
    /// lanes near the lane the place is on (<see cref="_bayLanesNear"/>) — <b>and the nearest gap at a kerb</b> beside the
    /// place's street, opened for <paramref name="car"/> (GEN-4n), among them by how far it is.
    /// </summary>
    int FreeBaysNear(in RoadPlace near, Vector2 ofM, float withinM, int car, Span<int> into)
    {
        var count = FreePaintedBaysNear(near, ofM, withinM, into);
        var kerb = KerbPlaceNear(near, ofM, withinM, car);
        if (kerb < 0) return count;

        var kerbM = Vector2.Distance(_parking.CentreM(kerb), ofM);
        var at = count;
        while (at > 0 && Vector2.Distance(_parking.CentreM(into[at - 1]), ofM) > kerbM) at--;
        if (at == into.Length) return count;

        var kept = Math.Min(count + 1, into.Length);
        for (var slot = kept - 1; slot > at; slot--) into[slot] = into[slot - 1];
        into[at] = kerb;
        return kept;
    }

    /// <summary>The painted bays of <see cref="FreeBaysNear"/>, nearest first.</summary>
    int FreePaintedBaysNear(in RoadPlace near, Vector2 ofM, float withinM, Span<int> into)
    {
        if (!near.IsSomewhere || into.IsEmpty) return 0;

        Span<float> farM = stackalloc float[into.Length];
        var count = 0;
        foreach (var bayLane in _bayLanesNear.AsSpan(_bayLanesNearOf[near.Lane], _bayLanesNearOf[near.Lane + 1] - _bayLanesNearOf[near.Lane]))
        {
            if (!_parking.AnyFreeOff(bayLane)) continue;

            // Measured as each bay is, so a box no nearer than the distance holds no bay that is.
            var outsideM = Vector2.Max(Vector2.Max(_bayLaneLeastM[bayLane] - ofM, ofM - _bayLaneMostM[bayLane]), Vector2.Zero);
            if (outsideM.Length() > withinM) continue;

            foreach (var bay in _bayStreets.BaysOffLane(bayLane))
            {
                var distanceM = Vector2.Distance(_parking.CentreM(bay), ofM);
                if (distanceM > withinM || !_parking.IsFree(bay) || !_parking.CanBeReached(bay)) continue;
                if (count == into.Length && distanceM >= farM[count - 1]) continue;
                if (into[..count].Contains(bay)) continue;

                var at = count < into.Length ? count++ : count - 1;
                while (at > 0 && farM[at - 1] > distanceM)
                {
                    farM[at] = farM[at - 1];
                    into[at] = into[at - 1];
                    at--;
                }

                farM[at] = distanceM;
                into[at] = bay;
            }
        }

        return count;
    }

    /// <summary>The nearest free bay within <paramref name="withinM"/> of a place on the road for a car (<see cref="FreeBaysNear"/>), or −1.</summary>
    int FreeBayOff(in RoadPlace near, Vector2 ofM, float withinM, int car)
    {
        Span<int> one = stackalloc int[1];
        return FreeBaysNear(near, ofM, withinM, car, one) > 0 ? one[0] : -1;
    }

    /// <summary>
    /// And the nearest painted bay alone — for an ambulance back at its hospital, whose bays are a car park's and never a
    /// kerb's (AMB-2).
    /// </summary>
    int FreePaintedBayOff(in RoadPlace near, Vector2 ofM, float withinM)
    {
        Span<int> one = stackalloc int[1];
        return FreePaintedBaysNear(near, ofM, withinM, one) > 0 ? one[0] : -1;
    }

    /// <summary>
    /// <b>A place drawn along the road within <paramref name="reachM"/> of one</b>: down the lanes from it, a lane at a
    /// time and a turn drawn at each end, for a drawn distance — so no further off than the road it was drawn down, and
    /// nothing looked up (the root decision log, 2026-10-13).
    /// </summary>
    RoadPlace DrawnDownTheRoad(in RoadPlace from, float reachM, ref Rng draw)
    {
        if (!from.IsSomewhere) return from;

        var leftM = reachM * MathF.Sqrt(draw.NextFloat());
        var lane = from.Lane;
        var alongM = from.AlongM;
        while (alongM + leftM > _roads.LaneLengthM[lane])
        {
            var onward = _roads.LanesFrom(lane);
            if (onward.IsEmpty) return new RoadPlace(lane, _roads.LaneLengthM[lane]);

            leftM -= _roads.LaneLengthM[lane] - alongM;
            lane = onward[draw.NextInt(onward.Length)];
            alongM = 0f;
        }

        return new RoadPlace(lane, alongM + leftM);
    }

    /// <summary>The buildings whose street is a lane (<see cref="_buildingsOnLane"/>).</summary>
    ReadOnlySpan<int> BuildingsOnLane(int lane) =>
        _buildingsOnLane.AsSpan(_buildingsOnLaneOf[lane], _buildingsOnLaneOf[lane + 1] - _buildingsOnLaneOf[lane]);

    /// <summary>
    /// Which of the spots along the pavement off each way in, each bay's way in and each kerb place are clear of the
    /// town's furniture (<see cref="ExitSpotsOf"/>) — a bit a spot, worked out once the furniture is stood.
    /// </summary>
    uint[] _entryFree = [];

    /// <inheritdoc cref="_entryFree"/>
    uint[] _wayInFree = [];

    /// <inheritdoc cref="_entryFree"/>
    uint[] _kerbFree = [];

    /// <summary>
    /// <b>The spots a body may be put down at off a place on the pavement</b> (PHY-7a), nearest first: the place itself,
    /// then a body's width further along its way and back, and so on out to the exit's reach — as many as a mask has
    /// bits for.
    /// </summary>
    int ExitSpotsOf() => Math.Min(MostExitSpots, 1 + (2 * (int)MathF.Floor(_config.PersonExitSearchRadiusM / _config.PersonDiameterM)));

    /// <summary>How many spots one place's mask holds: a bound on the table and not a figure behaviour reads.</summary>
    const int MostExitSpots = 31;

    /// <summary>How far along its way from the place the <paramref name="spot"/>th spot stands: 0, +1, −1, +2, … bodies.</summary>
    float SpotAlongM(int spot) => ((spot + 1) / 2) * (spot % 2 == 1 ? 1f : -1f) * _config.PersonDiameterM;

    /// <summary>
    /// <b>The exits laid with the map, once its furniture is stood</b>: for every place a body comes out at, which of its
    /// spots are on the map and clear of the props and buildings — the one shape test exits ask, asked here and never
    /// while the town runs (the root decision log, 2026-10-13).
    /// </summary>
    void LayTheExits()
    {
        _entryFree = FreeSpots(_entryPlace);
        _wayInFree = FreeSpots(_wayInPlace);
        _kerbFree = FreeSpots(_kerbPlace);
    }

    uint[] FreeSpots(ReadOnlySpan<FootPlace> places)
    {
        var free = new uint[places.Length];
        var half = new Vector2(_config.PersonDiameterM * 0.5f);
        var spots = ExitSpotsOf();
        for (var at = 0; at < places.Length; at++)
        {
            ref readonly var place = ref places[at];
            if (!place.IsSomewhere) continue;

            for (var spot = 0; spot < spots; spot++)
            {
                var alongM = place.AlongM + SpotAlongM(spot);
                if (alongM < 0f || alongM > _foot.LengthM(place.Edge)) continue;

                var atM = Spline.SampleAt(_foot.ArcsOf(place.Edge), alongM).PositionM;
                if (atM.X < 0f || atM.Y < 0f || atM.X >= _plan.WorldSizeM.X || atM.Y >= _plan.WorldSizeM.Y) continue;
                if (Statics.InBox(atM - half, atM + half)) continue;

                free[at] |= 1u << spot;
            }
        }

        return free;
    }

    /// <summary>
    /// <b>A body put down off a place on the pavement</b> (PHY-7a): at the nearest of its spots clear of the furniture
    /// (<paramref name="free"/>) that nobody holds the ground of — on the pavement way the place is on, its own radius
    /// either side — and, beside a car, that the car's own panels do not reach. False where every one is taken.
    /// </summary>
    /// <param name="beside">
    /// The car the body gets out of, or −1: <b>its own ground is no objection and its panels are one</b> — the walk to and
    /// from it is held as the car's (<see cref="GroundHeldAs"/>), and asked of the car's own claim, every place beside a
    /// car in a bay would be taken by the car standing in it, so its owner could never get out.
    /// </param>
    bool TryPutDownOff(in FootPlace place, uint free, int beside, out Vector2 spotM, out FootPlace spotPlace)
    {
        spotM = default;
        spotPlace = place;
        if (!place.IsSomewhere) return false;

        var way = _ways.OfFootway(place.Edge);
        var radiusM = _config.PersonDiameterM * 0.5f;
        var spots = ExitSpotsOf();
        for (var spot = 0; spot < spots; spot++)
        {
            if ((free & (1u << spot)) == 0) continue;

            var alongM = place.AlongM + SpotAlongM(spot);
            var atM = Spline.SampleAt(_foot.ArcsOf(place.Edge), alongM).PositionM;
            if (beside >= 0 && Reaches(beside, atM, radiusM)) continue;

            var wayM = _walking.LaneMOf(place.Edge, alongM);
            if (_occupancy.IsTakenByOthers(way, wayM - radiusM, wayM + radiusM, beside, beside, LaneRoster.Driving)) continue;

            spotM = atM;
            spotPlace = new FootPlace(place.Edge, alongM);
            return true;
        }

        return false;
    }

    /// <summary>
    /// The pavement off a building's way in nearest a place, with its free spots (<see cref="_entryFree"/>) — or, for a
    /// way in no pavement is a hop from, the kerb beside the building's street.
    /// </summary>
    (FootPlace Place, uint Free) DoorExitOf(int building, Vector2 fromM)
    {
        if (EntryOf(building, fromM) is var entry and >= 0 && _entryPlace[entry].IsSomewhere)
        {
            return (_entryPlace[entry], _entryFree[entry]);
        }

        var street = _roadPlaceOfBuilding[building];
        if (!street.IsSomewhere) return (FootPlace.Nowhere, 0u);

        var kerb = KerbOf(street.Lane, street.AlongM);
        return (_kerbPlace[kerb], _kerbFree[kerb]);
    }

    /// <summary>The street lane nearest a point and where along it — a look-up, made only while the map is built.</summary>
    RoadPlace StreetNearest(ChainIndex.Scan scan, Vector2 pointM)
    {
        var lane = _roads.NearestStreetLane(scan, pointM, out var alongM);
        return lane < 0 ? RoadPlace.Nowhere : new RoadPlace(lane, alongM);
    }

    /// <summary>
    /// <b>Where on the road a body is</b>: the first street lane the world reads it on (TER-4c.2) and where along it its
    /// centre stands — standing in a box on nothing but its joins, the start of the lane the first of them leads onto —
    /// or, for somebody on the pavement, the street beside the pavement it is on, or last stood on.
    /// </summary>
    /// <remarks>
    /// <b>A box is road</b>: read as nowhere, a car stood down in the middle of a junction was never sent anywhere again,
    /// and stood in it for the rest of the run with the junction queued round it.
    /// </remarks>
    RoadPlace RoadPlaceOf(ReadOnlySpan<WayCover> under, Vector2 atM, in FootPlace footPlace)
    {
        foreach (ref readonly var cover in under)
        {
            if (_ways.KindOf(cover.Way) != WayKind.Lane) continue;

            var lane = _ways.RoadLaneOf(cover.Way);
            if (!_roads.IsAStreetLane(lane)) continue;

            var alongM = Spline.ProjectM(
                _roads.ArcsOf(lane), atM, (cover.FromM + cover.ToM) * 0.5f, cover.ToM - cover.FromM + _roads.LaneWidthM[lane]);
            return new RoadPlace(lane, alongM);
        }

        foreach (ref readonly var cover in under)
        {
            if (_ways.KindOf(cover.Way) != WayKind.Connector) continue;

            var onto = _roads.ConnectorTo(_ways.RoadConnectorOf(cover.Way));
            if (_roads.IsAStreetLane(onto)) return new RoadPlace(onto, 0f);
        }

        foreach (ref readonly var cover in under)
        {
            if (_ways.KindOf(cover.Way) == WayKind.Footway) return _streetBesideFootway[_ways.FootwayOf(cover.Way)];
        }

        return footPlace.IsSomewhere ? _streetBesideFootway[footPlace.Edge] : RoadPlace.Nowhere;
    }

    RoadPlace RoadPlaceOfWalker(int person) =>
        RoadPlaceOf(_world.View.CoversOfWalker(person), People.PositionM[person], _placeOf[person]);

    RoadPlace RoadPlaceOfCar(int car) => RoadPlaceOf(CoversOfCar(car), Cars.PositionM[car], FootPlace.Nowhere);

    /// <summary>
    /// Where a car sets off from: its bay's street where it stands in its bay, whose ways are the car park's and no
    /// street's, and the ways under it anywhere else.
    /// </summary>
    RoadPlace SetsOffFrom(int car) => StandsInItsBay(car) ? RoadPlaceOfBay(_parking.BayOf(car)) : RoadPlaceOfCar(car);

    /// <summary>The street a bay is worked off, at its mouth (<see cref="BayStreets"/>).</summary>
    RoadPlace RoadPlaceOfBay(int bay)
    {
        foreach (var lane in _bayStreets.LanesOf(bay)) return new RoadPlace(lane, _bayStreets.AtLaneM(bay, lane));

        return RoadPlace.Nowhere;
    }

    /// <summary>A place on the road this far further along its lane — back along it for a negative distance — kept on the lane.</summary>
    RoadPlace OnAlong(in RoadPlace place, float byM) =>
        new(place.Lane, Math.Clamp(place.AlongM + byM, 0f, _roads.LaneLengthM[place.Lane]));

    /// <summary>Where a place on the road stands.</summary>
    Vector2 PointOf(in RoadPlace place) => Spline.SampleAt(_roads.ArcsOf(place.Lane), place.AlongM).PositionM;

    /// <summary>Which of the kerb places beside a lane stands beside a place on it (<see cref="_kerbPlace"/>).</summary>
    int KerbOf(int lane, float alongM)
    {
        var steps = _kerbOfLane[lane + 1] - _kerbOfLane[lane];
        return _kerbOfLane[lane] + Math.Clamp((int)MathF.Round(alongM / _config.LaneWidthM), 0, steps - 1);
    }

    /// <summary>The pavement beside where a car stands, on the pavement (<see cref="ExitBesideTheCar"/>).</summary>
    FootPlace PlaceBesideTheCar(int car) => ExitBesideTheCar(car).Place;

    /// <summary>
    /// The pavement beside where a car stands, with its free spots: in a bay its way in, and otherwise the kerb beside
    /// the first lane the world reads its wheels on (TER-4c.2), or the first lane of its line.
    /// </summary>
    (FootPlace Place, uint Free) ExitBesideTheCar(int car)
    {
        var bay = _parking.BayOf(car);
        if (bay >= 0)
        {
            var wayIn = (2 * bay) + (_parking.StandsNoseIn(bay, Cars.HeadingRad[car]) ? 1 : 0);
            return (_wayInPlace[wayIn], _wayInFree[wayIn]);
        }

        var kerb = -1;
        foreach (ref readonly var cover in CoversOfCar(car))
        {
            if (_ways.KindOf(cover.Way) != WayKind.Lane) continue;

            kerb = KerbOf(_ways.RoadLaneOf(cover.Way), (cover.FromM + cover.ToM) * 0.5f);
            break;
        }

        if (kerb < 0 && Cars.Line[car].LaneCount > 0)
        {
            var lane = Cars.ChainOf(car)[0];
            kerb = KerbOf(lane, Math.Clamp(Cars.ProgressM[car], 0f, _roads.LaneLengthM[lane]));
        }

        return kerb < 0 ? (FootPlace.Nowhere, 0u) : (_kerbPlace[kerb], _kerbFree[kerb]);
    }

    /// <summary>Where a place on the pavement stands.</summary>
    Vector2 PointOf(in FootPlace place) => Spline.SampleAt(_foot.ArcsOf(place.Edge), place.AlongM).PositionM;

    /// <summary>
    /// <b>A walker's place on the pavement, kept as it walks</b>: the way of its route it is on and how far along it,
    /// while its body is on that way's ground. A body shoved off it keeps the place it left from.
    /// </summary>
    void KeepItsPlace(int person)
    {
        var code = People.CurrentRouteWay(person);
        if (code < 0 || WalkingNetwork.IsACorner(code) || !_walkingGround.IsOnItsWay(person)) return;

        _placeOf[person] = _walking.PlaceOnLane(code, People.OnWayM[person]);
    }

    /// <summary>The way in of a building nearest a place, on the pavement (<see cref="DoorOf"/>).</summary>
    FootPlace DoorPlaceOf(int building, Vector2 fromM) => EntryOf(building, fromM) is var entry and >= 0
        ? _entryPlace[entry]
        : FootPlace.Nowhere;
}
