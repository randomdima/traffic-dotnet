using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The walkers' half of the reservations</b> (PER-26, PER-27): the ground a body stands on, which the
/// physical layer holds (<see cref="LayTheWalkersBody"/>), and <b>the walk in front of it, claimed by the action it
/// is doing</b> — down its route, round somebody on it, back onto it or to a post — and answered like a driver's
/// road (<see cref="WalkingGround"/>); the grant it comes back as is what the walker walks to
/// (<see cref="PersonFleet.GrantM"/>).
/// </summary>
/// <remarks>
/// <b>Where a body stands is read off the way it is walking and never searched for across the town</b>: it is the
/// body's projection onto that way's own line, sought a stride either side of the metre it held a tick ago
/// (<see cref="WalkingGround.PlaceItOnItsWay"/>). Only a body that is on no way at all plans nothing, and walks
/// straight back onto the network (PER-25).
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>The pavement's two blocks of the town's numbering, as the runs of metres they are</b> — a lane
    /// each way down every stretch, and the mitre at every corner. Handed to <see cref="TownWays"/>, which
    /// is what makes them ways of the same table the carriageway's are.
    /// </summary>
    static float[] PavementLengthsM(WalkingNetwork walking, out float[] mitreLengthM)
    {
        var lanesM = new float[walking.Foot.EdgeCount];
        for (var edge = 0; edge < lanesM.Length; edge++) lanesM[edge] = walking.LaneLengthM(edge);

        mitreLengthM = new float[walking.TurnCount];
        for (var turn = 0; turn < mitreLengthM.Length; turn++) mitreLengthM[turn] = walking.JoinLengthM(turn);

        return lanesM;
    }

    /// <summary>
    /// <b>Where this walker stands on the pavement's own network</b>, or <see cref="PersonFleet.NoWay"/>
    /// where it is on none of it — <b>which is also what decides how it walks</b> (PER-25): a body on a way
    /// follows the line the network laid it, and a body on none of them walks straight at the nearest of
    /// them.
    /// </summary>
    /// <remarks>
    /// <b>Worked out before either network is laid, because both read it</b> (<see cref="RebuildLaneOccupancy"/>):
    /// the road needs the way a body on a crossing is walking to know which lane it stands in, and the
    /// statement in front of the body begins from the same place. Asked twice it was the same walk of the
    /// same line, and the two answers were a tick apart.
    /// </remarks>
    void StationTheWalker(int person)
    {
        People.OnWay[person] = PersonFleet.NoWay;

        // PHY-7: inside a container there is no body in the world and nothing in anybody's way.
        if (People.Inside[person].Any) return;

        // <b>Where the walk has got to, worked out once and written once</b> (SIM-7): the body's place on
        // the way it is walking, and the next way off the chain where it has walked this one out. Split
        // between here and the agent's own tick, the way and the metre along it were written at two different
        // moments and disagreed at the end of every one.
        _walkingItsRoute.WalkTheWay(person);

        if (_walkingGround.IsAfoot(person, out var way)) People.OnWay[person] = way;

        _walkingBack.Consider(person);
    }

    /// <summary>
    /// <b>The walk in front of this body, claimed</b> (PER-26, TER-4c.8) by the action it is doing, at one rung
    /// (<see cref="ClaimPriority.Afoot"/>, PER-27) — a zebra's paint and the pavement to it included. What makes a
    /// zebra the whole zebra is the marks and not the walk (TER-5c.3): a metre of the paint places the lanes under
    /// all of it, and the traffic's plan over any of those holds all of the paint.
    /// </summary>
    void PlanTheWalk(int person, Span<LineWay> walk)
    {
        _walkingGround.WalkHold[person] = LaneOccupancy.NoHold;
        if (!_personActions.WalksOnItsOwnFeet(person)) return;

        // <b>A walker on a pass is held only by a body inside it</b> (TER-4c.6): the ground up to its end is the
        // pass's, and what it plans begins past there.
        if (People.Pass[person].Begun && _sidestepping.TheBodyInTheSidestep(person, out var inTheWay, out var on))
        {
            _walkingGround.HoldShortOf(person, inTheWay, on);
            return;
        }

        var ways = WaysOfTheWalk(person, walk);
        var occupant = GroundHeldAs(person, out var roster);
        _walkingGround.Claim(person, occupant, roster, ways);
    }

    /// <summary>
    /// <b>This walker's claim answered again against what every other came to</b>, and laid again where the
    /// answer has moved (<see cref="SettleThePlans"/>) — true where it did.
    /// </summary>
    bool SettleTheWalk(int person, Span<LineWay> walk)
    {
        if (!_walkingGround.MayMove(person, out var endsAtM)) return false;

        var ways = WaysOfTheWalk(person, walk);
        var occupant = GroundHeldAs(person, out var roster);
        return _walkingGround.Settle(person, endsAtM, occupant, roster, ways);
    }

    /// <summary>The ground a walker's action walks, nearest first, as that action has it.</summary>
    Span<LineWay> WaysOfTheWalk(int person, Span<LineWay> into) => People.Action[person] switch
    {
        PersonAction.Rejoin => _walkingBack.WaysOf(person, into),
        PersonAction.Post => _walkingToThePost.WaysOf(person, into),
        _ => _walkingItsRoute.WaysOf(person, into),
    };

    /// <summary>
    /// <b>Where a walker aims, on the grant this rebuild gave it</b>, as the action it is doing has it — and never past
    /// the ground it was granted, so a walker with none granted stands where it is (PER-26).
    /// </summary>
    void AimTheWalker(int person)
    {
        if (!People.Walking[person]) return;

        switch (People.Action[person])
        {
            case PersonAction.Post:
                _walkingToThePost.Aim(person);
                return;

            case PersonAction.Rejoin:
                _walkingBack.Aim(person);
                return;

            case PersonAction.Walk or PersonAction.Sidestep:
                _walkingItsRoute.Aim(person);
                return;
        }
    }

    /// <summary>The hold a walker laid this rebuild, for an instrument asking what held it.</summary>
    public int WalkHold(int person) => _walkingGround.WalkHold[person];

    /// <summary>Whether one of the town's ways is the paint of a zebra, walked from one kerb to the other.</summary>
    public bool IsTheCrossing(int way) => _lines.IsTheCrossing(way);
}
