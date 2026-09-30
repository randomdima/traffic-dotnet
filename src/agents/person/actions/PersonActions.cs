using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;

namespace TrafficSimulation.Agents.Person.Actions;

/// <summary>
/// <b>What each walker is doing, one action at a time</b> (PER-25b): the one place a walker's action changes, and what
/// the action it leaves owned — a pass — dropped with it.
/// </summary>
internal sealed class PersonActions(PersonFleet people)
{
    /// <summary>
    /// <b>A walker handed over to another action</b>, and what the one it leaves owned let go: a pass is its
    /// sidestep's.
    /// </summary>
    public void Enter(int person, PersonAction action)
    {
        var was = people.Action[person];
        if (was == action) return;

        if (was == PersonAction.Sidestep) people.Pass[person] = Sidestep.None;
        people.Action[person] = action;
    }

    /// <summary>
    /// <b>Whether a walker has somewhere to walk</b>, as its trip says — and with it the action that goes with that:
    /// down its route, or standing.
    /// </summary>
    public void SetWalking(int person, bool walking)
    {
        people.Walking[person] = walking;
        Enter(person, walking ? PersonAction.Walk : PersonAction.Stand);
    }

    /// <summary>Whether a walker's action walks the ways of its route — down them, round somebody on them, or back onto them.</summary>
    public static bool WalksItsRoute(PersonAction action) =>
        action is PersonAction.Walk or PersonAction.Sidestep or PersonAction.Rejoin;

    /// <summary>
    /// <b>Whether a walker walks on its own account</b> — down its route, round somebody on it or back onto it, or an
    /// officer to their post — and so claims what it walks (PER-25b). Standing, inside, down or under a hand, it
    /// claims nothing and is granted nothing.
    /// </summary>
    public bool WalksOnItsOwnFeet(int person) =>
        people.Walking[person] && (WalksItsRoute(people.Action[person]) || people.Action[person] == PersonAction.Post);
}
