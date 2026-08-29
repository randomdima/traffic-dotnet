using System.Numerics;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Containment;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The service crews on foot</b> (SRV-3): the one machine a paramedic, a recovery man and an officer all
/// run — out of the vehicle, over to the work, and back into the seat. What each of them does when they get
/// there is their own slice's (<c>TownWorld.Ambulance.cs</c>, <c>TownWorld.Recovery.cs</c>,
/// <c>TownWorld.Patrol.cs</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>A hand out is an ordinary walker</b> and nothing here pretends otherwise: it holds pavement in the
/// same book, is cut by the same grants, is knocked down by the same cars. What it does not have is a trip —
/// where it is going is its vehicle's errand, re-aimed on every decision, so a body that is shoved off its
/// line simply walks at the place again from wherever it ended up.
/// </para>
/// <para>
/// <b>Every crossing of this is bounded</b> (SRV-3, AMB-9's argument said of a walk). A pavement that will
/// not give a hand back is a vehicle stranded mid-errand and a station one vehicle short for the rest of the
/// run, so the recall is a clock and the last few metres are a placement when it runs out — which is the
/// winch's own fallback (EVA-5) said of a person rather than of a car.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>Who is out of each vehicle working</b>, or <see cref="NoHand"/> — the town's own note of it, laid
    /// over the whole fleet like every duty here, so a car's index means the same thing in it as everywhere
    /// else.
    /// </summary>
    int[] _handOut = [];

    /// <summary>
    /// And how long they have been walking back to it, which is the bound that ends a recall the pavement
    /// will not answer (SRV-3). <b>It is the vehicle's clock and not the body's</b>: what it bounds is how
    /// long a vehicle waits, and a walker's own timer already means a dwell.
    /// </summary>
    float[] _recallS = [];

    public const int NoHand = -1;

    /// <summary>
    /// <b>The crew member this vehicle sends out</b>: whoever is already out, else the hand in its first
    /// crew seat, else whoever has the wheel.
    /// </summary>
    /// <remarks>
    /// <b>The driver is the fallback and not the exception.</b> A hand can be knocked down at a scene like
    /// anybody standing in a street, and a vehicle that could then never work again would be a hospital
    /// quietly losing an ambulance every time a car came past one. The vehicle is standing still while the
    /// work is done either way, so which seat the body came out of changes nothing about the road.
    /// </remarks>
    int TheHandOf(int car)
    {
        if (_handOut[car] >= 0) return _handOut[car];

        var crew = _containers.CrewOf(car, 0);
        return crew >= 0 ? crew : _containers.DriverOf(car);
    }

    /// <summary>
    /// <b>Who is out of this vehicle working</b> (SRV-3), or <see cref="NoHand"/> — for whoever draws the
    /// town or asks it questions, since a hand out of its seat is in neither the crew register nor the
    /// vehicle's own arrays.
    /// </summary>
    public int HandOutOf(int car) => _handOut[car];

    /// <summary>Whether anybody of this vehicle's is out in the road working.</summary>
    bool TheHandIsOut(int car) => _handOut[car] >= 0;

    /// <summary>
    /// <b>The hand out and walking at a place</b>, and whether they have got there. The first call puts the
    /// body down beside the vehicle (PHY-7a); every later one re-aims the walk, because the place a crew is
    /// walking to moves — a casualty is shunted by whatever hits them, and a slot is looked up again.
    /// </summary>
    /// <remarks>
    /// <b>Refused is a wait and not a failure</b>, exactly as an ordinary exit is: every spot round the
    /// vehicle being taken is a doorway that empties as soon as whoever is standing in it walks off.
    /// </remarks>
    bool TheHandHasReached(int car, Vector2 placeM)
    {
        var hand = TheHandOf(car);
        if (hand < 0) return false;

        if (!TheHandIsOut(car))
        {
            if (!TryPutTheHandOut(car, hand, placeM)) return false;
        }

        // A body that has been knocked down while it was out is nobody to be waited on: the errand gives up
        // above this, and what it is waiting for is answered false until it does.
        if (People.Wounded[hand] || People.Inside[hand].Any) return false;

        // <b>Arrived is asked before the walk is re-aimed</b>, and that is not an optimisation: a body a
        // stride short of its place is inside the crew's reach and outside the follower's, so re-laying the
        // leg would set it walking again every decision — an officer shuffling on the spot for the whole of
        // a closure, and one whose stretch of road moves under him while he does it.
        if ((People.PositionM[hand] - placeM).Length() <= _config.Service.CrewReachM) return true;

        AimTheHandAt(hand, placeM);
        return false;
    }

    /// <summary>
    /// <b>And back into the seat</b>: the walk to the vehicle's own way in, and the seat taken once the body
    /// is within reach of it. True once nobody of this vehicle's is out — which is the state every leg that
    /// drives has to be in before it is laid.
    /// </summary>
    /// <param name="sinceLastDecisionS">
    /// What the recall's own clock integrates, which is the vehicle's elapsed and never the loop's nominal
    /// interval — the reason a call's is (<see cref="RunTheRescue"/>).
    /// </param>
    bool TheHandIsAboard(int car, float sinceLastDecisionS)
    {
        if (!TheHandIsOut(car)) return true;

        var hand = _handOut[car];

        // Knocked down out there, or already inside something else: the vehicle is not going to get this
        // body back and waiting for it would strand the vehicle. It is a casualty like anybody else and an
        // ambulance is what comes for it (PER-18).
        if (People.Wounded[hand] || People.Inside[hand].Any)
        {
            LetGoOfTheHand(car);
            return true;
        }

        var doorM = TheWorkingDoorM(car, People.PositionM[hand]);
        if ((People.PositionM[hand] - doorM).Length() <= _config.Service.CrewReachM)
        {
            return TakeTheHandAboard(car, hand);
        }

        AimTheHandAt(hand, doorM);

        // <b>The recall is bounded and the bound is spent as a placement</b> (SRV-3): a hand the pavement
        // will not give back is put in its own doorway rather than left to hold a vehicle out of service.
        // It is EVA-5's winch said of a person, and it is the only place in this errand a body is moved
        // by anything but its own feet.
        _recallS[car] += sinceLastDecisionS;
        if (_recallS[car] < _config.ServiceRecallS) return false;

        Place(hand, doorM, Cars.HeadingRad[car]);
        return TakeTheHandAboard(car, hand);
    }

    /// <summary>
    /// <b>The door on the side the work is</b> — a point clear of the vehicle's own footprint in whichever
    /// direction the thing being reached for lies.
    /// </summary>
    /// <remarks>
    /// <b>It is not the way in a walker is aimed at</b> (<see cref="WayInOf"/>), and the difference is the
    /// whole of whether a crew can get anywhere: that door is the flank away from the traffic, a casualty
    /// lying in the carriageway is on the traffic side, and a paramedic put down on the far side of his own
    /// ambulance walks straight into it — the follower steers at its aim and goes round nothing (PER-13).
    /// <b>The larger half-extent</b>, because a body at that radius is outside the footprint whichever way
    /// the vehicle happens to be pointing.
    /// </remarks>
    Vector2 TheWorkingDoorM(int car, Vector2 towardM)
    {
        var away = towardM - Cars.PositionM[car];
        var lengthM = away.Length();
        if (lengthM <= 1e-3f) return WayInOf(car);

        ref readonly var build = ref Cars.BuildOf(car);
        var clearM = MathF.Max(build.HalfLengthM, build.FlankM) + _config.PersonDiameterM;
        return Cars.PositionM[car] + (away / lengthM * clearM);
    }

    /// <summary>The body out of the vehicle and standing beside it, or false while there is nowhere to put it (PHY-7a).</summary>
    bool TryPutTheHandOut(int car, int hand, Vector2 towardM)
    {
        var doorM = TheWorkingDoorM(car, towardM);
        if (!ExitSpots.TryFind(
                _config, _terrain, _physics, _nearby, StandingPeople, doorM, towardM, _spotNearby, out var spotM,
                anyGround: true))
        {
            return false;
        }

        _containers.Alight(car, hand);
        Place(hand, spotM, MathF.Atan2(spotM.Y - Cars.PositionM[car].Y, spotM.X - Cars.PositionM[car].X));
        People.Stage[hand] = TripStage.Attending;
        _handOut[car] = hand;
        _recallS[car] = 0f;
        return true;
    }

    /// <summary>The seat taken back, and everything the walk was holding dropped with it.</summary>
    bool TakeTheHandAboard(int car, int hand)
    {
        if (!_containers.TryTakeACrewSeat(car, hand) && !_containers.TryBoard(car, hand)) return false;

        Contain(hand);
        People.Stage[hand] = TripStage.OnDuty;
        People.ClosesTheRoadM[hand] = 0f;
        _handOut[car] = NoHand;
        _recallS[car] = 0f;
        return true;
    }

    /// <summary>
    /// The vehicle's claim on a body it is not getting back — struck off the crew and left in the town as an
    /// ordinary walker, on EVA-7's terms for a crew whose vehicle broke under them (SRV-4).
    /// </summary>
    /// <remarks>
    /// <b>A body still on its feet is handed back to itself</b>, or it would stand in the street holding a
    /// stage nothing runs for: <see cref="TripStage.Attending"/> is where a crew is going, and where this one
    /// is going has stopped being anybody's business. One that is down or inside something is left alone —
    /// a casualty is somebody else's call (PER-18) and a dwell is its own clock.
    /// </remarks>
    void LetGoOfTheHand(int car)
    {
        var hand = _handOut[car];
        _handOut[car] = NoHand;
        _recallS[car] = 0f;
        if (hand < 0) return;

        People.ClosesTheRoadM[hand] = 0f;
        if (People.Wounded[hand] || People.Inside[hand].Any) return;

        People.Stage[hand] = TripStage.StandingBy;
        People.TimerS[hand] = 0f;
    }

    /// <summary>
    /// The walk re-aimed, and only where the place has actually moved: laying the same line again on every
    /// decision would restart the clock that says whether this body is getting anywhere, so a hand pinned
    /// against a wall would never be noticed as stuck.
    /// </summary>
    void AimTheHandAt(int hand, Vector2 placeM)
    {
        if (People.Walking[hand] && (People.GoalM[hand] - placeM).Length() <= People.RadiusM[hand]) return;

        WalkTo(hand, placeM);
    }

    /// <summary>
    /// <b>A body tugged along behind whoever has hold of it</b> (AMB-10) — the winch (EVA-5) said of a
    /// person: set down a stride behind the walker on every decision, so the pair moves together and the
    /// casualty is still a body in the world the whole way (PHY-5).
    /// </summary>
    /// <remarks>
    /// <b>It is a placement and not a coupling</b> (PHY-7a). A tow is two vehicles the solver holds together
    /// with an impulse and its opposite, because both have mass, wheels and a line; a person carrying
    /// somebody has none of that, and an arm modelled as a spring between two walkers would be a joint
    /// nothing else in this town has. What the pair costs the road is the ground each of them stands on,
    /// which the book already has.
    /// </remarks>
    void TugAlong(int hand, int casualty)
    {
        var behind = Heading.Unit(People.HeadingRad[hand]);
        var apartM = People.RadiusM[hand] + People.RadiusM[casualty];
        Place(casualty, People.PositionM[hand] - (behind * apartM), People.HeadingRad[hand]);
    }
}
