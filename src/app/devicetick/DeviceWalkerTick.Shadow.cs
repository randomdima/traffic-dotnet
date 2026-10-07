using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>The walkers' tick in shadow: every walker laid as the town's tick reads it, and held to what that tick did.</summary>
internal sealed partial class DeviceWalkerTick
{
    /// <summary>How many walkers boarded something or stepped off it between the tick the device was asked of and the step.</summary>
    public int Boarded { get; private set; }

    /// <summary>
    /// <b>Every walker laid whole as the town's tick reads it</b> — where it stands and moves, its pace, aim, heading and
    /// flags, how far it has walked, and the impulse last left in the town's array — and which body it is, sent only where
    /// that changed.
    /// </summary>
    /// <returns>How many walkers the town has.</returns>
    public int Lay(TownWorld town)
    {
        var people = town.People;
        var rows = _walkers.All;
        var impulses = _impulses.All;
        var paused = town.HoldAgents;
        for (var person = 0; person < people.Count; person++)
        {
            var flags = (people.Walking[person] ? WalkerRow.Walking : 0u) | (people.Wounded[person] ? WalkerRow.Wounded : 0u)
                        | (people.Inside[person].Any ? WalkerRow.Inside : 0u) | (paused ? WalkerRow.Paused : 0u);
            rows[person] = new WalkerRow(
                people.PositionM[person], people.VelocityMps[person], people.DeclaredMps[person], people.DestinationM[person],
                people.HeadingRad[person], people.MassKg[person], flags, people.DistanceWalkedM[person]);
            impulses[person] = town.WalkerImpulseNs(person);

            ref var body = ref _bodies.Rows(person)[0];
            if (body == people.Body[person].Index) continue;

            body = people.Body[person].Index;
            _bodies.Mark(person);
        }

        _walkers.Mark(0, people.Count);
        _impulses.Mark(0, people.Count);
        return people.Count;
    }

    /// <summary>
    /// <b>What the device's tick of a walker did that the town's did not</b>, asked once the town's casualties have slid
    /// (<see cref="TownWorld.BeforeTheImpulses"/>): the way it faces, the pace it declared and the impulse it left — within
    /// the device's arctangent, or null where they agree.
    /// </summary>
    public string? Disagreement(TownWorld town, int person)
    {
        var people = town.People;

        // Boarding and stepping off are decisions, taken after the tick the device was asked of and before the step.
        if (people.Inside[person].Any != ((_walkers.All[person].Flags & WalkerRow.Inside) != 0))
        {
            Boarded++;
            return null;
        }

        var row = WalkersAfter[person];
        var impulseNs = ImpulsesAfter[person];
        var hostNs = town.WalkerImpulseNs(person);
        return Near(people.HeadingRad[person], row.HeadingRad) && Near(people.DeclaredMps[person].X, row.DeclaredMps.X)
               && Near(people.DeclaredMps[person].Y, row.DeclaredMps.Y) && Near(hostNs.X, impulseNs.X) && Near(hostNs.Y, impulseNs.Y)
            ? null
            : $"walker {person}: town heading {people.HeadingRad[person]} pace {people.DeclaredMps[person]} impulse {hostNs}, device {row} {impulseNs}";
    }

    /// <summary>Within a part in a thousand and a hundredth absolute — the device's trigonometry, as every kernel test reads it.</summary>
    static bool Near(float host, float onDevice) => host == onDevice || MathF.Abs(host - onDevice) <= 1e-2f + (1e-3f * MathF.Abs(host));
}
