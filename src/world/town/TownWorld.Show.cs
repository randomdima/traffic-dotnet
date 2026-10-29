using TrafficSimulation.Agents.Person.Actions;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The showroom's people</b> (PER-30): every person look once, standing still in a row down the middle of the car
/// park (<c>ShowroomPlan</c>) until a hand takes it (CTL-6). Nothing on show decides anything of its own.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>A look on show stands where it is</b>, takes no trip and holds nothing ahead; a walker a hand lets go of
    /// stands wherever the hand left it.
    /// </summary>
    void StandOnShow(int person)
    {
        Enter(person, PersonAction.Show);
        People.Walking[person] = false;
        People.DestinationM[person] = People.PositionM[person];
    }

    /// <summary>Whether this walker is a look on show, standing rather than on a trip, and under no hand (PER-30).</summary>
    bool IsOnShow(int person) => _showroom && People.Action[person] != PersonAction.Hand;
}
