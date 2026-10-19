using TrafficSimulation.Runtime;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The device's body table as an instrument reads it</b> (<see cref="DeviceWorld.Bodies"/>): every body of every kind in
/// the one numbering, in the rows the solver steps (<see cref="BodyRows"/>).
/// </summary>
/// <param name="PosesAtView">Each view slot's poses (<see cref="DeviceWorld.KeepTheBodiesAtTheView"/>); empty until kept.</param>
/// <param name="Tags">What each body is, <see cref="World.Physics.BodyTag.Packed"/>.</param>
/// <param name="Links">The live links first (<see cref="DeviceSolver.LinkGpuRow"/>), <paramref name="LinkCount"/> of them.</param>
internal readonly record struct DeviceBodies(
    GpuBuffer[] PosesAtView, GpuBuffer Shape, GpuBuffer Filter, GpuBuffer Motion, GpuBuffer Tags, GpuBuffer Links, int LinkCount,
    int Count, int FirstMoving);
