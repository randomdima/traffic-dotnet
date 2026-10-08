using System.Numerics;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>What a tick the device ran asks of the town</b> (<see cref="ITownDevice"/>): each consequence the town's own tick
/// would have taken in the middle of it — a line shifted on, lost, rejoined or stepped off onto the lane beside — taken
/// here with the town's own code once the device hands the tick back, and the tables the hand-back writes into.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// A device ticked this car past its first lane (<c>LineReader.Advance</c>): the town's own line shifted on as the
    /// device's was, and what is measured along it moved with it (<see cref="AdvanceLane"/>).
    /// </summary>
    public void ShiftedOnTheDevice(int car) => AdvanceLane(car, Cars.ShiftTheLine(car));

    /// <summary>A device found this car off its line and stopped it (CAR-10a): into Rejoin, its junction forgotten.</summary>
    public void LostItsLineOnTheDevice(int car) => _rejoining.Lose(car);

    /// <summary>A device ticked a car waiting to rejoin its line and found it back on it: following again (<c>Rejoining.Tick</c>).</summary>
    public void RejoinedOnTheDevice(int car) => Enter(car, CarAction.Follow);

    /// <summary>A device ticked this car's switch onto the lane beside (CAR-53): its line laid down that lane.</summary>
    public void SteppedOutOnTheDevice(int car)
    {
        var town = new CarTown(this);
        _switching.SteppedOut(ref town, car, Cars.ProgressM[car]);
    }

    /// <summary>A device drove this car's pass to its end (CAR-46): made, and following again.</summary>
    public void MadeThePassOnTheDevice(int car) => _overtaking.DrivenToItsEnd(car);

    /// <summary>A device found this car's line short of its sight: its next decision lays the rest (<see cref="_asksTheRoute"/>).</summary>
    public void AskedTheRouteOnTheDevice(int car) => _asksTheRoute[car] = true;

    /// <summary>A device spent this car's braking margin (<see cref="HardBrakings"/>).</summary>
    public void BrakedHardOnTheDevice() => HardBrakings++;

    /// <summary>Whether a hand walks this walker this tick, which the town's own tick does (CTL-6).</summary>
    public bool WalksUnderAHand(int person) =>
        !People.Inside[person].Any && People.Acts(person) && _hands.Held && _selected.Holds(SelectionKind.Person, person);

    /// <summary>The impulse each of this car's wheels spends this tick, for a device to write what its tyres left.</summary>
    /// <remarks>
    /// The ground under each wheel and its scrub are not asked for: they are the tyre pass's own scratch
    /// (<see cref="WheelScratch"/>), and nothing after the pass reads them for a car the town did not tick.
    /// </remarks>
    public Span<WheelImpulse> WheelImpulsesFor(int car) => _wheels.ImpulsesOf(car);

    /// <summary>The impulse this walker spends this tick, for a device to write what its tick left.</summary>
    public ref Vector2 WalkerImpulseFor(int person) => ref _impulseNs[person];
}
