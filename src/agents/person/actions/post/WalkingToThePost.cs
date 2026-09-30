using System.Numerics;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.Person.Actions;

/// <summary>
/// <b>Post</b> (SRV-11): an officer on duty walking straight to the place their closure names — the post in the lane,
/// the kerb beside it while a call goes through, the car's door — and standing on it, over every way the straight
/// crosses, the carriageway among them. No route and no clock: where they stand is the closure's.
/// </summary>
internal sealed class WalkingToThePost(WalkingGround ground, PersonActions actions)
{
    PersonFleet People => ground.People;

    /// <summary>An officer sent to a place, on the closure's word.</summary>
    public void Begin(int officer, Vector2 atM)
    {
        People.DestinationM[officer] = atM;
        People.GoalM[officer] = atM;
        People.Walking[officer] = true;
        actions.Enter(officer, PersonAction.Post);
    }

    /// <summary>The ground the walk to the post covers, nearest first: every way the straight to it crosses.</summary>
    public Span<LineWay> WaysOf(int officer, Span<LineWay> into) =>
        into[..ground.TheStraightAhead(officer, People.GoalM[officer], People.RadiusM[officer], ground.PlansAheadM, into)];

    /// <summary>Aimed down the straight to the post, no further than it was granted.</summary>
    public void Aim(int officer) => ground.AimAlongTheStraight(officer, People.GoalM[officer]);
}
