namespace TrafficSimulation.App.Render;

/// <summary>
/// How much room each run of the sprite pass has, in the order they are drawn: what lies under everything that stands
/// (marks, scenery), the buildings and props — laid on the device once (<see cref="StandingSprites"/>) — what moves over
/// them (walkers, cars, signal heads), and the bodies on the level above; then the ground's ring of marks and the scenery
/// as the desktop keeps them on the device.
/// </summary>
/// <remarks>
/// <b>Under is the page's</b>: a browser writes the marks and the scenery as instances, where the desktop draws them out
/// of their own rows (<see cref="Marks"/>, <see cref="SceneryPieces"/>) and a frame writes only what is new of the one and
/// the runs in view of the other.
/// </remarks>
internal readonly record struct SpriteRoom(
    int Under, int Standing, int Over, int Above, int Marks = 1, int SceneryPieces = 1, int SceneryRows = 1)
{
    /// <summary>A renderer with no town to draw — a menu's.</summary>
    public static SpriteRoom Nothing => new(1, 1, 1, 0);

    /// <summary>What a frame writes into the page's instance buffer: every run but the one laid once.</summary>
    public int Written => Under + Over + Above;

    /// <summary>What a frame writes into the desktop's: what moves, on the ground and above it.</summary>
    public int Moving => Over + Above;
}

/// <summary>What one frame draws of each run: how many of the three it wrote, and which stretch of the laid one.</summary>
internal readonly record struct SpriteCounts(int Under, int StandingFirst, int StandingCount, int Over, int Above)
{
    public int Drawn => Under + StandingCount + Over + Above;
}
