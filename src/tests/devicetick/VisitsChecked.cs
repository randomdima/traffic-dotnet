using System.Runtime.CompilerServices;
using TrafficSimulation.App.DeviceTick;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>Every device town the suite runs checks what it does not walk</b> (<see cref="DeviceCarTick.ChecksTheVisits"/>): a car
/// the town changed while the device kept it, unmarked, throws in whichever test drove the town to it.
/// </summary>
static class VisitsChecked
{
    [ModuleInitializer]
    internal static void InEveryTown() => DeviceCarTick.ChecksTheVisitsInEveryTown = true;
}
