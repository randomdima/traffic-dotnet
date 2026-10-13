using System.Numerics;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.App.Camera;

/// <summary>
/// <b>Where a run opens looking</b> (OBS-1b): the middle of the town, or — where no road is in the frame
/// there at all — the nearest ground a car could be on.
/// </summary>
/// <remarks>
/// <para>
/// <b>The middle of a city is a street, and the middle of a ring is the field inside it.</b> A camera put
/// down on the geometric centre and left there opens on grass wherever a map's own subject is not in the
/// middle of its bounding box: the idle ring if it ever outgrew its window, and anything laid around a
/// park, a lake or a bay.
/// </para>
/// <para>
/// <b>Road already in the frame keeps the camera where it is.</b> What is wanted is something to look at
/// rather than a road under the middle of the screen, so a map whose middle is a field with road around
/// the edge of it is framed on its middle exactly as a city is.
/// </para>
/// <para>
/// <b>What it is not is a fit to the whole town.</b> The opening span is a figure
/// (<see cref="Core.Config.ViewFigures.CameraDefaultViewM"/>) and a whole-town fit on a small map is unreadably
/// small; what moves here is where the camera stands and never how much it shows.
/// </para>
/// </remarks>
internal static class Opening
{
    /// <param name="inViewM">
    /// How far from the middle the opening view already reaches, which is half its short side: road inside
    /// that is road the reader can see without the camera moving anywhere.
    /// </param>
    /// <remarks>
    /// <b>The player's look-up</b> (the root decision log, 2026-10-13): the nearest street lane to the middle, off the
    /// road's own index — the town holds no ground of its own while it runs.
    /// </remarks>
    public static Vector2 LooksAtM(RoadGraph roads, Vector2 worldSizeM, float inViewM)
    {
        var middleM = worldSizeM * 0.5f;
        var lane = roads.NearestStreetLane(middleM, out var alongM);
        if (lane < 0) return middleM;

        var roadM = Spline.SampleAt(roads.ArcsOf(lane), alongM).PositionM;
        return (roadM - middleM).Length() <= inViewM ? middleM : roadM;
    }
}
