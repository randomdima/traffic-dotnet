using System.Numerics;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>Parking at the kerb</b> (GEN-4n): a gap found along a roadside where a car means to stand, opened there as a place
/// of the register for as long as a car is on its way to it or standing in it — and the gaps read off the bodies standing
/// on the roadside and the places already opened on it, and off nothing laid for the purpose.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>The stretches of roadside a car may park along (<see cref="KerbStretches"/>).</summary>
    KerbStretches _kerbs = null!;

    /// <summary>
    /// <b>The roadsides laid as places to park</b>, once the marks are: every stretch clear of a junction and a crossing by
    /// <see cref="Core.Config.RoadFigures.KerbParkingClearM"/>, long enough for the nominal car and a gap either side.
    /// </summary>
    void LayTheKerbs() =>
        _kerbs = KerbStretches.Build(
            _roads, _ways, _occupancy.Marks, _config.Road.KerbParkingClearM, _config.Car.LengthM + (2f * _config.Road.KerbParkingGapM),
            _config.Road.KerbParkingLaneRunM);

    /// <summary>
    /// <b>The nearest gap at a kerb within <paramref name="withinM"/> of a place, opened for a car</b> — or −1. Looked
    /// for along the roadsides beside the place's lane and beside the lane back the other way, and where in a gap the car
    /// would stand is as near the place as the gap lets it.
    /// </summary>
    /// <param name="car">The car that would stand there, whose length the gap must hold and whose own body is no objection.</param>
    int KerbPlaceNear(in RoadPlace near, Vector2 ofM, float withinM, int car)
    {
        if (!near.IsSomewhere || _kerbs.Count == 0) return -1;

        var lengthM = Cars.BuildOf(car).LengthM;
        var best = (Stretch: -1, AtM: 0f, FarM: withinM);
        foreach (var lane in (ReadOnlySpan<int>)[near.Lane, _roads.LaneReverse[near.Lane]])
        {
            if (lane < 0) continue;

            foreach (var stretch in _kerbs.BesideLane(lane))
            {
                if (NearestGap(stretch, ofM, lengthM, car, out var atM) is var farM && farM < best.FarM) best = (stretch, atM, farM);
            }
        }

        return best.Stretch < 0 ? -1 : OpenTheKerb(best.Stretch, best.AtM, lengthM);
    }

    /// <summary>
    /// <b>How near a place a car this long could stand in a stretch</b>, and where along the roadside its middle would
    /// be — <see cref="float.PositiveInfinity"/> where no gap holds it. A gap runs between the bodies on the roadside and
    /// the places opened on it that somebody holds, <see cref="Core.Config.RoadFigures.KerbParkingGapM"/> short of each.
    /// </summary>
    float NearestGap(int stretch, Vector2 ofM, float lengthM, int car, out float atM)
    {
        atM = 0f;
        var roadside = _kerbs.RoadsideOf(stretch);
        var arcs = _roads.ArcsOf(roadside);
        var (fromM, toM) = (_kerbs.FromM(stretch), _kerbs.ToM(stretch));
        var aimM = Spline.ProjectM(arcs, ofM, (fromM + toM) * 0.5f, toM - fromM);
        var gapM = _config.Road.KerbParkingGapM;
        var halfM = (lengthM * 0.5f) + gapM;
        var way = _ways.OfRoadLane(roadside);

        var bestM = float.PositiveInfinity;
        for (var startsM = fromM; startsM < toM;)
        {
            var (endsM, resumesM) = (toM, toM);
            if (_occupancy.FirstBodySeen(way, startsM, toM, car, LaneRoster.Driving, out var body)) (endsM, resumesM) = (body.FromM, body.ToM);

            for (var kerb = _parking.FirstKerbOn(roadside); kerb >= 0; kerb = _parking.NextKerbOnTheLane(kerb))
            {
                if (_parking.IsFree(kerb) || _parking.KerbToM(kerb) <= startsM || _parking.KerbFromM(kerb) >= endsM) continue;

                (endsM, resumesM) = (_parking.KerbFromM(kerb), MathF.Max(_parking.KerbToM(kerb), startsM));
            }

            if (endsM - startsM >= 2f * halfM)
            {
                var standsM = Math.Clamp(aimM, startsM + halfM, endsM - halfM);
                var farM = Vector2.Distance(Spline.SampleAt(arcs, standsM).PositionM, ofM);
                if (farM < bestM) (bestM, atM) = (farM, standsM);
            }

            startsM = MathF.Max(resumesM, startsM + LineTolerance.RoundingM);
        }

        return bestM;
    }

    /// <summary>
    /// <b>A kerb place opened in a stretch for a body this long</b>, its middle at a metre of the roadside: heading the way
    /// the roadside runs, worked off the lane beside it, its way in on the kerb's side and never the traffic's — or −1
    /// where the register has no place left to open.
    /// </summary>
    int OpenTheKerb(int stretch, float atM, float lengthM)
    {
        var roadside = _kerbs.RoadsideOf(stretch);
        var beside = _kerbs.BesideOf(stretch);
        var at = Spline.SampleAt(_roads.ArcsOf(roadside), atM);
        var besideArcs = _roads.ArcsOf(beside);
        var atLaneM = Spline.ProjectM(besideArcs, at.PositionM, _roads.LaneLengthM[beside] * 0.5f, _roads.LaneLengthM[beside]);
        var towardKerb = at.PositionM - Spline.SampleAt(besideArcs, atLaneM).PositionM;
        var doorM = (towardKerb.LengthSquared() > 0f ? Vector2.Normalize(towardKerb) : at.Right)
                    * ((_config.Car.WidthM * 0.5f) + _config.PersonDiameterM);

        var halfM = lengthM * 0.5f;
        var bay = _parking.OpenKerb(roadside, atM - halfM, atM + halfM, at.PositionM, at.HeadingRad, beside, atLaneM, doorM);
        if (bay < 0) return -1;

        var kerb = KerbOf(roadside, atM);
        _wayInPlace[2 * bay] = _wayInPlace[(2 * bay) + 1] = _kerbPlace[kerb];
        if (_kerbFree.Length > 0) _wayInFree[2 * bay] = _wayInFree[(2 * bay) + 1] = _kerbFree[kerb];

        return bay;
    }

    /// <summary>
    /// <b>The kerb place a car the plan stood at the kerb stands in</b> (GEN-7, GEN-4n), opened for it — or −1 where it
    /// stands in no stretch, or in one another car already stands in.
    /// </summary>
    int KerbPlaceUnder(Vector2 positionM, float lengthM)
    {
        if (_kerbs.Count == 0) return -1;

        // The lanes are filed by the street's own and a roadside is none of them, so it is found off the lanes beside it.
        Span<int> around = stackalloc int[KerbLanesLookedAt];
        var found = Math.Min(around.Length, _roads.LanesAround(positionM, _config.LaneWidthM + _config.CityGen.TracedRoadsideWidthM, around));
        foreach (var lane in around[..found])
        {
            foreach (var stretch in _kerbs.BesideLane(lane))
            {
                var roadside = _kerbs.RoadsideOf(stretch);
                var arcs = _roads.ArcsOf(roadside);
                var (fromM, toM) = (_kerbs.FromM(stretch), _kerbs.ToM(stretch));
                var atM = Spline.ProjectM(arcs, positionM, (fromM + toM) * 0.5f, toM - fromM, out var offSq);
                var halfWidthM = _roads.LaneWidthM[roadside] * 0.5f;
                if (offSq > halfWidthM * halfWidthM || atM < fromM || atM > toM || !IsClearOfTheKerbPlaces(roadside, atM, lengthM)) continue;

                return OpenTheKerb(stretch, atM, lengthM);
            }
        }

        return -1;
    }

    /// <summary>How many lanes about a place are read for the roadside it stands on: a bound on a stack span.</summary>
    const int KerbLanesLookedAt = 32;

    /// <summary>Whether a body this long stands at a metre of a roadside clear of every place opened on it by a gap.</summary>
    bool IsClearOfTheKerbPlaces(int roadside, float atM, float lengthM)
    {
        var halfM = (lengthM * 0.5f) + _config.Road.KerbParkingGapM;
        for (var kerb = _parking.FirstKerbOn(roadside); kerb >= 0; kerb = _parking.NextKerbOnTheLane(kerb))
        {
            if (!_parking.IsFree(kerb) && _parking.KerbToM(kerb) > atM - halfM && _parking.KerbFromM(kerb) < atM + halfM) return false;
        }

        return true;
    }
}
