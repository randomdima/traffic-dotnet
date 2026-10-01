using System.Numerics;
using TrafficSimulation.Agents.Ambulance;
using TrafficSimulation.Agents.Evacuator;
using TrafficSimulation.Agents.Service;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Parking;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The district beat</b> (SRV-5): half of every service building's fleet driving the streets of the district
/// it stands in (GEN-56), and the other half standing on its apron — an ambulance's, a police car's and an
/// evacuator's alike. What each does when a call comes is its own errand's; what is here is where a patrolling
/// vehicle goes when none has.
/// </summary>
/// <remarks>
/// <b>The beat is kept to the district and the calls are not.</b> Whoever is nearest and free takes a call
/// (AMB-5, SRV-6, EVA-3), a patrolling vehicle as much as a standing one, and after it the vehicle goes back to
/// what it was: a patrol to its district's streets, and the rest to their bays.
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>Which district each service vehicle keeps to, and whether it patrols it (SRV-5).</summary>
    public ServiceBeat ServiceBeat => _serviceBeat;

    /// <summary>
    /// <b>Each district's streets</b>: <c>[offsets[d], offsets[d + 1])</c> of <see cref="_districtLanes"/> are the
    /// lanes whose middle stands in district <c>d</c>, never a bay — laid once, since the town does not move.
    /// </summary>
    int[] _districtLaneOffsets = [0];

    int[] _districtLanes = [];

    /// <summary>How many people ride in each service vehicle: the figure, and never more than it has seats for (SRV-3).</summary>
    static int CrewAboard(SimConfig config) => Math.Min(config.Service.CrewPerVehicle, Containers.CrewSeats);

    void LayTheDistrictStreets()
    {
        var districts = _plan.Districts;
        var of = new int[_roads.LaneCount];
        var counts = new int[districts.Count + 1];
        for (var lane = 0; lane < of.Length; lane++)
        {
            if (_roads.IsABayArm(lane))
            {
                of[lane] = ServiceBeat.NoDistrict;
                continue;
            }

            var middleM = Spline.SampleAt(_roads.ArcsOf(lane), _roads.LaneLengthM[lane] * 0.5f).PositionM;
            of[lane] = districts.At(middleM);
            counts[of[lane] + 1]++;
        }

        for (var district = 0; district < districts.Count; district++) counts[district + 1] += counts[district];

        _districtLaneOffsets = counts;
        _districtLanes = new int[counts[^1]];
        var filled = new int[districts.Count];
        for (var lane = 0; lane < of.Length; lane++)
        {
            if (of[lane] == ServiceBeat.NoDistrict) continue;

            _districtLanes[counts[of[lane]] + filled[of[lane]]++] = lane;
        }
    }

    /// <summary>
    /// <b>A service vehicle given its district and its half</b> (SRV-5): the district its building stands in, and
    /// whether it is one of the fleet that patrols — with its first stand drawn from its own stream.
    /// </summary>
    void JoinTheDistrictBeat(int car, int building, bool patrols)
    {
        _serviceBeat.District[car] = _plan.Districts.At(_plan.Buildings.CentreM[building]);
        _serviceBeat.Patrols[car] = patrols;
        _serviceBeat.SetsOutAtS[car] = Cars.Draw[car].NextFloat(
            _config.Service.FirstBeatAfterMinS, _config.Service.FirstBeatAfterMaxS);
    }

    /// <summary>Whether this vehicle patrols and its first stand is over — what sends a standing one out on its beat.</summary>
    bool IsDueOnTheBeat(int car) => _serviceBeat.IsDue(car, _elapsedS);

    /// <summary>
    /// <b>The next place on this vehicle's beat, and the leg to it</b> (SRV-5): its errand's own patrolling stage
    /// entered, and a place along one of its district's streets drawn from its own stream. False where there is
    /// no street to draw, which leaves the vehicle as it was.
    /// </summary>
    /// <remarks>
    /// <b>Drawn and never searched for</b>: nothing in the town asks for a patrol, so a beat is aimed at nothing.
    /// <b>Somewhere along a lane and never a junction's middle</b>, because a leg ends by the car standing where it
    /// got to and the middle of a junction is the one place standing still is being driven into — aimed at the
    /// junction centres, the fixture town's patrol was wrecked inside the first box it reached. <b>And a street
    /// and never a bay</b> (GEN-4h), a bay being a space joined to nothing. A district with no street of its
    /// own draws from the town's.
    /// </remarks>
    bool DriveTheBeat(int car)
    {
        var district = _serviceBeat.District[car];
        var from = district >= 0 ? _districtLaneOffsets[district] : 0;
        var to = district >= 0 ? _districtLaneOffsets[district + 1] : _districtLanes.Length;
        if (from == to)
        {
            from = 0;
            to = _districtLanes.Length;
        }

        if (from == to) return false;

        // A street runs over a district's edge as often as not, and the part of it past the edge is the next
        // district's: a place drawn there is moved to the street's middle, which is this district's.
        ref var draw = ref Cars.Draw[car];
        var lane = _districtLanes[from + draw.NextInt(to - from)];
        var arcs = _roads.ArcsOf(lane);
        var placeM = Spline.SampleAt(arcs, draw.NextFloat() * _roads.LaneLengthM[lane]).PositionM;
        if (district >= 0 && _plan.Districts.At(placeM) != district)
        {
            placeM = Spline.SampleAt(arcs, _roads.LaneLengthM[lane] * 0.5f).PositionM;
        }

        if (Cars.Ambulance[car]) EnterTheStage(car, RescueStage.Patrolling);
        else if (IsAnEvacuator(car)) EnterTheRecoveryStage(car, RecoveryStage.Patrolling);
        else EnterThePatrolStage(car, PatrolStage.Patrolling);

        SendTo(car, placeM, ParkingRegistry.NoBay);
        return true;
    }

    /// <summary>
    /// <b>Whether an ambulance or an evacuator is driving its beat</b> — a leg aimed at a place on a lane rather
    /// than a bay (<see cref="IsAimedAtAPlaceInTheRoad"/>). A police car's is <see cref="IsOnItsBeatOrToAScene"/>.
    /// </summary>
    bool IsDrivingItsBeat(int car) =>
        (Cars.Ambulance[car] && _duty.Stage[car] == RescueStage.Patrolling)
        || (IsAnEvacuator(car) && _recovery.Stage[car] == RecoveryStage.Patrolling);

    /// <summary>
    /// <b>A place on the beat done with</b> — arrived and standing on it, the leg over some other way, or the leg out
    /// of clock: a patrol has nowhere it must be, so a road that will not let it through costs it the next street and
    /// nothing more.
    /// </summary>
    bool IsDoneWithThePlace(int car, float legS) =>
        !Cars.Driven[car] || StandsAtItsPlace(car) || legS >= _config.PatrolGiveUpS;
}
