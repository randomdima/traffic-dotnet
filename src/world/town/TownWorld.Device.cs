using System.Numerics;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Control;

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

    /// <summary>
    /// <b>Whether an instrument reads every agent every tick</b> — a watch's claims over the whole fleet, and over every
    /// walker — so a device hands back every car's grant left with every tick (<see cref="GrantLeftM"/>), and every walker a
    /// claims rebuild changed (<see cref="ReadsTheWalker"/>).
    /// </summary>
    public bool WatchesEveryAgent { get; set; }

    /// <summary>
    /// <b>How far this car may yet go on its grant</b> (<see cref="CarFleet.GrantLeftM"/>), where a device keeps the car as
    /// its last tick left it (<see cref="ITownDevice.GrantLeftM"/>) — for an instrument reading every car
    /// (<see cref="WatchesEveryAgent"/>) without having each handed back.
    /// </summary>
    public float GrantLeftM(int car) => Device is { } device ? device.GrantLeftM(this, car) : Cars.GrantLeftM(car);

    /// <summary>
    /// <b>Whether every car is read as this tick leaves it</b> — by a frame drawn after it off the town's copy, whose lamps,
    /// indicators and front wheels read each car's command, junction ahead and line, where they are not drawn off the
    /// device's own tables — so a device hands every car back with it (<see cref="ReadsTheTickOf"/>).
    /// </summary>
    public bool ReadsEveryCarAfterTheTick { get; set; }

    /// <summary>
    /// <b>Whether the town reads this car's tick as the device leaves it, before the device ticks it again</b> — so it is
    /// handed back, where every other car's stays on the device: its decision is due on the tick being handed back, or was
    /// due on the one before and taken while it ran, which may have set it doing something else and lays it whole from what
    /// the town holds; or the town reads it every tick whatever the decisions' clock — a switch's step or a pass kept or
    /// withdrawn and laid, a rejoin, a call's closure, a tow, the selection — or every car, read after this tick
    /// (<see cref="ReadsEveryCarAfterTheTick"/>).
    /// </summary>
    public bool ReadsTheTickOf(int car)
    {
        if (ReadsEveryCarAfterTheTick) return true;

        // Where the device decides the cars, it hands back what a decision it leaves to the town reads.
        var agent = Roster.AgentOfCar(car);
        if (!CarsDecidedOnTheDevice && (_decisions.Turn(_agentsTick, agent) || _decisions.Turn(_agentsTick - 1, agent))) return true;

        return Cars.Action[car] is CarAction.Switch or CarAction.Overtake or CarAction.Rejoin || Cars.BlueLight[car]
               || _recovery.Towing[car] >= 0 || _selected.Holds(SelectionKind.Car, car);
    }

    /// <summary>
    /// <b>Whether the town reads a walker as a claims rebuild on the device leaves it</b>, whenever its decision is due: every
    /// walker where it watches every agent, one whose decision it takes whole — indoors, wounded or on duty — and the one
    /// selected, which a hand may walk.
    /// </summary>
    public bool ReadsTheWalker(int person) =>
        WatchesEveryAgent || People.Inside[person].Any || People.Wounded[person] || People.Stage[person] == TripStage.OnDuty
        || _selected.Holds(SelectionKind.Person, person);

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
