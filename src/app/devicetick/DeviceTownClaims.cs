using System.Diagnostics;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The town's claims laid on the device, in step</b> (<see cref="ITownClaims"/>): every rebuild the device can lay laid
/// by <see cref="DeviceRebuild.InTheTown"/> into the claims and the car tick's tables it is given, and the rest left to the
/// town and counted.
/// </summary>
/// <remarks>
/// <para>
/// <b>The town lays a rebuild itself</b> where more cars are on a pass than there is room to ask for, and while the town's
/// agents are held.
/// </para>
/// <para>
/// <b>Once every grant is read</b>, what the device is to write of each asked car's grant is taken as what the car tick's
/// tables hold (<see cref="DeviceCarTick.AssumeTheGrantOf"/>), so a grant the town changes after is sent and no other.
/// </para>
/// </remarks>
internal sealed class DeviceTownClaims : ITownClaims, IDisposable
{
    readonly DeviceCarTick _cars;

    /// <param name="claims">The claims the rebuild numbers into and the car tick reads as cut.</param>
    /// <param name="cars">The car tick whose lines the plans read and whose grant tables the grants are written into.</param>
    /// <param name="walkers">
    /// The walker tick whose table the walkers are read in from, where the device keeps the walkers as its own
    /// (<see cref="KeepsTheWalkers"/>) — or none, and the town stations them and reads their grants.
    /// </param>
    /// <param name="rooms">The rooms the rebuild begins with (<see cref="DeviceRebuild.RoomsToStart"/> where none).</param>
    public DeviceTownClaims(
        Vk vk, SimConfig config, TownWorld town, DeviceClaims claims, DeviceCarTick cars, DeviceWalkerTick? walkers = null,
        DeviceRebuild.Rooms? rooms = null)
    {
        _cars = cars;
        _held = new int[town.Cars.Capacity];
        Rebuild = new DeviceRebuild(vk, config, town, claims, cars, walkers, rooms);
    }

    /// <summary>Room for the cars the town holds, which are the only ones that may be on a begun pass.</summary>
    readonly int[] _held;

    /// <summary>The rebuild laid here, for whoever reads its times and what it crossed.</summary>
    public DeviceRebuild Rebuild { get; }

    /// <summary>
    /// How many claims rebuilds were laid here, and how many the town laid itself — of them, how many for more cars on a
    /// pass than there is room to ask for.
    /// </summary>
    public (long Here, long ByTheTown, long TooManyOnAPass) Rebuilds { get; private set; }

    /// <summary>
    /// Whether the town laid claims itself since this was last asked — or before the device was set, where none was laid here
    /// since — so they are sent as cut.
    /// </summary>
    public bool TakeLaidByTheTown()
    {
        var laid = _laidByTheTown;
        _laidByTheTown = false;
        return laid;
    }

    /// <summary>Begun so: until a rebuild is laid, the device's claims are the town's, and its memory is not.</summary>
    bool _laidByTheTown = true;

    public bool LaysTheClaims(TownWorld town)
    {
        var (here, byTheTown, tooMany) = Rebuilds;
        var crowded = !town.HoldAgents
                      && !DeviceRebuild.HasRoomForThePasses(town, _cars.TheCarsTheTownHolds(town.Cars.Count, (Span<int>)_held));
        if (town.HoldAgents || crowded)
        {
            _laidByTheTown = true;
            Rebuilds = (here, byTheTown + 1, tooMany + (crowded ? 1 : 0));
            return false;
        }

        Rebuilds = (here + 1, byTheTown, tooMany);
        _laidByTheTown = false;
        return true;
    }

    public void ClaimsAt(TownWorld town, ClaimsStage stage)
    {
        var arrived = Stopwatch.GetTimestamp();
        if (stage != ClaimsStage.Begun) _townMs[(int)stage] = Stopwatch.GetElapsedTime(_left, arrived).TotalMilliseconds;

        Laying = stage != ClaimsStage.GrantsRead;
        Rebuild.InTheTown(town, stage);
        var due = Rebuild.AnswersDue;
        if (stage == ClaimsStage.PlansAnswered && !due) Grant(town);
        if (stage == ClaimsStage.GrantsRead && !due) TakeTheGrants(town);
        if (stage == ClaimsStage.GrantsRead && due) _cars.GrantsArePending(town.CarsAskedThisLaying);

        _left = Stopwatch.GetTimestamp();
        _hereMs[(int)stage] = Stopwatch.GetElapsedTime(arrived, _left).TotalMilliseconds;
    }

    /// <summary>Whether a laying's stages are under way: begun (<see cref="ClaimsStage.Begun"/>) and its grants not yet read.</summary>
    public bool Laying { get; private set; }

    public bool ReadTheGrants => true;

    public bool KeepsTheWalkers => Rebuild.Walkers.Keeps;

    public bool LandsLater => Rebuild.LandsLater;

    public void LandTheLastLaying(TownWorld town)
    {
        TakeTheAnswers(town);
        Rebuild.Land(town);
    }

    /// <summary>
    /// <b>A rebuild that ran on while the town went on, its answers written back</b> (<see cref="DeviceRebuild.AnswersDue"/>):
    /// every agent, then every asked car's grant into the town and taken as the car tick's tables hold it — as its stages
    /// would have written them had it been waited for. Nothing where none is due.
    /// </summary>
    public void TakeTheAnswers(TownWorld town)
    {
        if (!Rebuild.AnswersDue) return;

        // Its hold numbers are its laying's, put aside until its layer lands.
        town.TakeUpTheLaying();
        Rebuild.TakeTheAnswers(town);
        Grant(town);
        TakeTheGrants(town);
        town.PutTheLayingAsideAgain();
    }

    /// <summary>
    /// Every asked car's grant and every walker's as the rebuild read them with the answers, written into the town — of the
    /// cars only those it handed back (<see cref="DeviceRebuild.AnsweredInPart"/>).
    /// </summary>
    void Grant(TownWorld town)
    {
        if (Rebuild.AnsweredInPart)
        {
            foreach (var car in town.CarsAskedThisLaying)
            {
                if (!Rebuild.HandedBack(car, out var said)) continue;

                var grant = said.Grant;
                town.GrantedOnTheDevice(car, grant.AuthorityM, grant.GrantMarginM, (HeadwayKind)grant.GrantCutBy);
            }
        }
        else
        {
            var read = Rebuild.GrantsRead;
            foreach (var car in town.CarsAskedThisLaying)
            {
                ref readonly var row = ref read[car];
                town.GrantedOnTheDevice(car, row.AuthorityM, row.GrantMarginM, (HeadwayKind)row.GrantCutBy);
            }
        }

        // Kept walkers' grants were taken up with everything else the device made of them.
        if (KeepsTheWalkers) return;

        var walks = Rebuild.WalksRead;
        for (var person = 0; person < walks.Length; person++)
        {
            ref readonly var walk = ref walks[person];
            town.WalkerGrantedOnTheDevice(
                person, (walk.Bits & DeviceRebuild.WalkOut.LaidAHold) != 0, walk.EndsAtM, walk.MarginM, walk.CutOn);
        }
    }

    /// <summary>
    /// Every asked car's grant as the car tick's tables now hold it: as the rebuild read it with the answers and handed it
    /// back (<see cref="DeviceCarTick.TakeTheGrantOf"/>); or, of a car the rebuild did not hand back, left to the device
    /// (<see cref="DeviceCarTick.LeaveTheGrantOnTheDevice"/>) — but where the town holds it as the device left it since,
    /// its grant having come back with its reading.
    /// </summary>
    void TakeTheGrants(TownWorld town)
    {
        if (Rebuild.AnsweredInPart)
        {
            foreach (var car in town.CarsAskedThisLaying)
            {
                if (Rebuild.HandedBack(car, out var said)) _cars.TakeTheGrantOf(car, said.Grant.ToGrantRow());
                else if (!_cars.TownHolds(car)) _cars.LeaveTheGrantOnTheDevice(town.Cars, car);
            }

            return;
        }

        var read = Rebuild.GrantsRead;
        foreach (var car in town.CarsAskedThisLaying) _cars.TakeTheGrantOf(car, read[car]);
    }

    long _left;
    readonly double[] _townMs = new double[Enum.GetValues<ClaimsStage>().Length];
    readonly double[] _hereMs = new double[Enum.GetValues<ClaimsStage>().Length];

    /// <summary>
    /// The host's time over the last rebuild laid here, stage by stage, in milliseconds: the town's own laying from the
    /// stage before up to each, and the work done here at each — the submit and the write-back at
    /// <see cref="ClaimsStage.PlansLaid"/> among it.
    /// </summary>
    public (double[] Town, double[] Here) StageMs => (_townMs, _hereMs);

    /// <summary>Whether a rebuild was laid here since the last tick recorded behind one (<see cref="RecordTheTickBehind"/>).</summary>
    public bool Pending => Rebuild.Pending;

    /// <inheritdoc cref="DeviceRebuild.RecordTheTickBehind"/>
    public void RecordTheTickBehind(CommandBuffer commands) => Rebuild.RecordTheTickBehind(commands);

    public void Dispose() => Rebuild.Dispose();
}
