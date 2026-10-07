using Silk.NET.Vulkan;
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
        Rebuild.InTheTown(town, stage);
        if (stage != ClaimsStage.GrantsRead) return;

        foreach (var car in town.CarsAskedThisLaying) _cars.AssumeTheGrantOf(town, car);
    }

    /// <summary>Whether a rebuild laid here waits for its backing and grants to be recorded (<see cref="RecordTheRest"/>).</summary>
    public bool Pending => Rebuild.Pending;

    /// <inheritdoc cref="DeviceRebuild.RecordTheRest"/>
    public void RecordTheRest(CommandBuffer commands) => Rebuild.RecordTheRest(commands);

    public void Dispose() => Rebuild.Dispose();
}
