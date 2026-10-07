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
/// <b>The town lays a rebuild itself</b> where only its own laying can answer what is laid — a hand or a truck holding what
/// it cannot stop short of, a manoeuvre begun at a bay (<see cref="TownWorld.LaysWhatOnlyTheTownAnswers"/>) — where more
/// cars are on a pass than there is room to ask for, and while the town's agents are held.
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
    public DeviceTownClaims(Vk vk, SimConfig config, TownWorld town, DeviceClaims claims, DeviceCarTick cars)
    {
        _cars = cars;
        Rebuild = new DeviceRebuild(vk, config, town, claims, cars);
    }

    /// <summary>The rebuild laid here, for whoever reads its times and what it crossed.</summary>
    public DeviceRebuild Rebuild { get; }

    /// <summary>
    /// How many claims rebuilds were laid here, and how many the town laid itself — of them, how many for what only the
    /// town's laying answers, and how many for more cars on a pass than there is room to ask for.
    /// </summary>
    public (long Here, long ByTheTown, long OnlyTheTownAnswers, long TooManyOnAPass) Rebuilds { get; private set; }

    /// <summary>Whether the town laid claims itself since this was last asked, so they are sent as cut.</summary>
    public bool TakeLaidByTheTown()
    {
        var laid = _laidByTheTown;
        _laidByTheTown = false;
        return laid;
    }

    bool _laidByTheTown;

    public bool LaysTheClaims(TownWorld town)
    {
        var (here, byTheTown, onlyTheTown, tooMany) = Rebuilds;
        var only = !town.HoldAgents && town.LaysWhatOnlyTheTownAnswers();
        var crowded = !town.HoldAgents && !only && !DeviceRebuild.HasRoomForThePasses(town);
        if (town.HoldAgents || only || crowded)
        {
            _laidByTheTown = true;
            Rebuilds = (here, byTheTown + 1, onlyTheTown + (only ? 1 : 0), tooMany + (crowded ? 1 : 0));
            return false;
        }

        Rebuilds = (here + 1, byTheTown, onlyTheTown, tooMany);
        return true;
    }

    public void ClaimsAt(TownWorld town, ClaimsStage stage)
    {
        var arrived = Stopwatch.GetTimestamp();
        if (stage != ClaimsStage.Begun) _townMs[(int)stage] = Stopwatch.GetElapsedTime(_left, arrived).TotalMilliseconds;

        Rebuild.InTheTown(town, stage);
        if (stage == ClaimsStage.PlansAnswered && ReadTheGrants) Grant(town);
        if (stage == ClaimsStage.GrantsRead) TakeTheGrants(town);

        _left = Stopwatch.GetTimestamp();
        _hereMs[(int)stage] = Stopwatch.GetElapsedTime(arrived, _left).TotalMilliseconds;
    }

    public bool ReadTheGrants => Rebuild.GrantedWithTheAnswers;

    /// <summary>Every asked car's grant and every walker's as the rebuild read them with the answers, written into the town.</summary>
    void Grant(TownWorld town)
    {
        var read = Rebuild.GrantsRead;
        foreach (var car in town.CarsAskedThisLaying)
        {
            ref readonly var row = ref read[car];
            town.GrantedOnTheDevice(car, row.AuthorityM, row.GrantMarginM, (HeadwayKind)row.GrantCutBy);
        }

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
    /// back (<see cref="DeviceCarTick.TakeTheGrantOf"/>), or — where the town laid a backing after them — as the town's own
    /// reading of the claims (<see cref="DeviceCarTick.AssumeTheGrantOf"/>).
    /// </summary>
    void TakeTheGrants(TownWorld town)
    {
        if (!ReadTheGrants)
        {
            foreach (var car in town.CarsAskedThisLaying) _cars.AssumeTheGrantOf(town, car);
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

    /// <summary>Whether a rebuild laid here waits for its backing and grants to be recorded (<see cref="RecordTheRest"/>).</summary>
    public bool Pending => Rebuild.Pending;

    /// <inheritdoc cref="DeviceRebuild.RecordTheRest"/>
    public void RecordTheRest(CommandBuffer commands) => Rebuild.RecordTheRest(commands);

    public void Dispose() => Rebuild.Dispose();
}
