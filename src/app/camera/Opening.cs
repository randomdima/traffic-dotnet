using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Terrain;

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
/// (<see cref="ViewFigures.CameraDefaultViewM"/>) and a whole-town fit on a small map is unreadably
/// small; what moves here is where the camera stands and never how much it shows.
/// </para>
/// </remarks>
internal static class Opening
{
    /// <summary>How far out the search gives up, as a share of the town. Past this there is nothing to find.</summary>
    const float OfTheTown = 0.5f;

    /// <param name="inViewM">
    /// How far from the middle the opening view already reaches, which is half its short side: road inside
    /// that is road the reader can see without the camera moving anywhere.
    /// </param>
    public static Vector2 LooksAtM(TerrainGrid terrain, Vector2 worldSizeM, float inViewM)
    {
        var middleM = worldSizeM * 0.5f;
        var roadM = NearestRoadM(terrain, middleM, worldSizeM);
        return (roadM - middleM).Length() <= inViewM ? middleM : roadM;
    }

    /// <summary>The nearest ground a car could be on, or the middle itself where the town holds none.</summary>
    static Vector2 NearestRoadM(TerrainGrid terrain, Vector2 middleM, Vector2 worldSizeM)
    {
        if (terrain.At(middleM).Drivable) return middleM;

        var stepM = terrain.CellSizeM;
        var reachM = MathF.Max(worldSizeM.X, worldSizeM.Y) * OfTheTown;
        for (var ringM = stepM; ringM <= reachM; ringM += stepM)
        {
            // The ring is walked as a square rather than a circle: the cells are square, the answer is the
            // same ground, and a trigonometric sweep of a grid is arithmetic spent on nothing.
            var steps = (int)MathF.Ceiling(ringM / stepM);
            for (var step = -steps; step <= steps; step++)
            {
                var alongM = step * stepM;
                if (Drivable(terrain, middleM + new Vector2(alongM, -ringM), out var atM)) return atM;
                if (Drivable(terrain, middleM + new Vector2(alongM, ringM), out atM)) return atM;
                if (Drivable(terrain, middleM + new Vector2(-ringM, alongM), out atM)) return atM;
                if (Drivable(terrain, middleM + new Vector2(ringM, alongM), out atM)) return atM;
            }
        }

        return middleM;
    }

    static bool Drivable(TerrainGrid terrain, Vector2 pointM, out Vector2 atM)
    {
        atM = pointM;
        return terrain.At(pointM).Drivable;
    }
}
