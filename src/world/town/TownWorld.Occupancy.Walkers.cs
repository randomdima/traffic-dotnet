using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>The ways one walker's action planned its walk down this rebuild (<see cref="TownWorld.WhenAWalkIsPlanned"/>).</summary>
internal delegate void WalkPlanned(TownWorld town, int person, ReadOnlySpan<LineWay> ways);

/// <summary>
/// <b>The walkers' side of a claims rebuild handed to the instruments</b> — for a device stationing, laying, planning
/// and aiming the walkers beside the town (<c>app/devicetick/DeviceWalkers</c>), and what it reads of them. Nothing that
/// decides anything sets or reads any of it.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>For the instruments: handed every walk's ways as its action planned them, before they are claimed.</summary>
    public WalkPlanned? WhenAWalkIsPlanned { get; set; }

    /// <summary>The walker's side of the reservations (<see cref="Agents.Person.Actions.WalkingGround"/>).</summary>
    internal WalkingGround WalkingGround => _walkingGround;

    /// <summary>Where each walker walking back decided to walk back to (<see cref="Agents.Person.Actions.WalkingBack"/>).</summary>
    internal WalkingBack WalkingBack => _walkingBack;

    /// <summary>
    /// <b>The officer each car puts out at a closure</b> (SRV-11), by car — what makes an officer on duty that car's
    /// ground (<see cref="GroundHeldAs"/>).
    /// </summary>
    internal ReadOnlySpan<int> OfficerOfEachCar => _beat.Officer.AsSpan(0, Cars.Count);
}
