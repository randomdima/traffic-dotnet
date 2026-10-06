namespace TrafficSimulation.App.Render;

/// <summary>
/// How many instances each run of the sprite pass has room for, in the order they are drawn: what lies under
/// everything that stands (marks, scenery), the buildings and props — laid on the device once
/// (<see cref="StandingSprites"/>) — what moves over them (walkers, cars, signal heads), and the bodies on the
/// level above.
/// </summary>
internal readonly record struct SpriteRoom(int Under, int Standing, int Over, int Above)
{
    /// <summary>A renderer with no town to draw — a menu's.</summary>
    public static SpriteRoom Nothing => new(1, 1, 1, 0);

    /// <summary>What a frame writes into the instance buffer, every run but the one laid once.</summary>
    public int Written => Under + Over + Above;
}

/// <summary>What one frame draws of each run: how many of the three it wrote, and which stretch of the laid one.</summary>
internal readonly record struct SpriteCounts(int Under, int StandingFirst, int StandingCount, int Over, int Above)
{
    public int Drawn => Under + StandingCount + Over + Above;
}
