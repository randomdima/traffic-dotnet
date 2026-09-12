namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>A line the town strikes off its own boundary, by name</b> (TER-3c.3) — one entry a distance, and the
/// distance is <see cref="GroundRings"/>'s to hold.
/// </summary>
/// <remarks>
/// <para>
/// <b>Named because a bare distance says nothing about what it is for.</b> Every line beside a road is now
/// the same ring moved by a figure, which is what makes them all one construction — and is also what makes
/// them indistinguishable at the call site: <c>At(1.8f)</c> and <c>At(2f)</c> are two floats, and whichever
/// of them a reader meant is not written down anywhere. A name carries the intent, the figure stays on
/// <c>SimConfig</c> where a figure belongs, and the two are joined in exactly one place.
/// </para>
/// <para>
/// <b>The normal of every one of them points inside the perimeter</b> (TER-3c.9). A ring is walked with the driven
/// ground on the walker's right throughout (<see cref="LaneShell"/>) and the offset keeps that order, so
/// the right of travel is the ground side on the ring round the town and on the ring round every block it
/// encloses. Whatever is laid along one of these lines can therefore read its own inward side off the line
/// itself, with nothing to look up and no ring to identify as the outer one.
/// </para>
/// <para>
/// <b>The kerb is a line like any other and is nought.</b> It is the edge of the driven ground, which is
/// where every other distance here is measured from, so it is in the table rather than being the absence
/// of one.
/// </para>
/// </remarks>
internal enum GroundLine : byte
{
    /// <summary>The edge of the driven ground itself, at nought off itself.</summary>
    Kerb = 0,

    /// <summary>
    /// <b>The roadside perimeter</b>: half a lane out from the kerb
    /// (<see cref="Core.Config.SimConfig.RoadsidePerimeterOutM"/>).
    /// </summary>
    Roadside = 1,
}
