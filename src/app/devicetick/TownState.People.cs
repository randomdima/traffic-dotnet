using System.Runtime.InteropServices;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The people's fields no row holds</b> (<c>world/town/kernels/town_people.glsl</c>, ids 288–383): every per-walker
/// array of <see cref="Agents.Person.Body.PersonFleet"/> the walkers' rows do not carry, and the town's own.
/// </summary>
internal sealed partial class TownState
{
    const int PeopleDrawId = 288;
    const int PeopleManualId = 289;
    const int PeopleRecklessId = 290;
    const int PeopleVariantId = 291;
    const int PeopleDestinationBuildingId = 292;
    const int PeopleTimerSId = 293;
    const int PeopleInsideId = 294;
    const int TownWoundedWordsId = 295;

    /// <summary>Where somebody is (<c>Contained</c>): the kind widened to a word, so no padding byte is laid.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct ContainedRow(uint Kind, int Index)
    {
        public static ContainedRow Of(Contained inside) => new((uint)inside.Kind, inside.Index);
    }

    void LayThePeople(TownWorld town)
    {
        var people = town.People;
        Lay<Rng>(PeopleDrawId, people.Draw);
        LayFlags(PeopleManualId, people.Manual);
        LayFlags(PeopleRecklessId, people.Reckless);
        LayWidened<byte>(PeopleVariantId, people.Variant);
        Lay<int>(PeopleDestinationBuildingId, people.DestinationBuilding);
        Lay<float>(PeopleTimerSId, people.TimerS);
        Lay(PeopleInsideId, people.Inside, ContainedRow.Of);
        LayWords(TownWoundedWordsId, town.Settled.Wounded, people.Capacity);
    }
}
