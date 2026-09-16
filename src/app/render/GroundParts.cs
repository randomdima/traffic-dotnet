namespace TrafficSimulation.App.Render;

/// <summary>
/// One layer of the town's standing ground, in the order <see cref="GroundMesh.Build"/> lays them
/// (TER-7b). <b>A part is a stretch of the mesh and not a class of triangle</b>: each is laid whole
/// before the next one starts, so what it came to is a run of the index buffer with a first and a
/// count, and leaving one out is a shorter draw rather than a different mesh.
/// </summary>
internal enum GroundPart : byte
{
    Grass,
    Walk,
    WalkKerb,
    Water,
    Decks,
    Carriageway,
    Slabs,
    Kerb,
    Paint,
}

/// <summary>
/// What one part of the ground came to: where its triangles stand in the mesh, how many corners it
/// added that no earlier part had already stood at, and what laying it cost.
/// </summary>
/// <remarks>
/// <b>The corners are the ones this part brought and not the ones it uses</b> (<c>GroundMesh.Vertex</c>):
/// the ground is welded, so a part laid over a boundary an earlier one already walked shares its
/// stations and is charged for none of them. The two readings together are what says which parts are
/// paying for the same line twice.
/// </remarks>
internal readonly record struct GroundTally(int FirstIndex, int IndexCount, int Corners, double LaidMs)
{
    public int Triangles => IndexCount / 3;
}

/// <summary>The parts as a set: how many there are, and what each is called where a reader is shown one.</summary>
internal static class GroundParts
{
    public const int Count = (int)GroundPart.Paint + 1;

    /// <summary>
    /// Every part shown, which is what a run that has asked for nothing draws: the picture is the town
    /// and not instrumentation, so the switches here start at the whole of it rather than at none of it
    /// (OBS-2v).
    /// </summary>
    public const uint All = (1u << Count) - 1;

    /// <summary>
    /// <b>Printable ASCII only</b>, like every other string the interface draws, and <b>the words the
    /// mesh and the requirements use</b> — a page naming the walk a sidewalk would be a second vocabulary
    /// for the one shape.
    /// </summary>
    public static readonly string[] Names =
    [
        "Grass", "Walk", "Walk kerb", "Water and shore", "Bridge decks", "Carriageway", "Paved slabs",
        "Town kerb", "Paint",
    ];

    /// <summary>
    /// And what a script names one with — <c>--ui hide-carriageway</c>. <b>A word apiece and not the name
    /// lowercased</b>: what a reader is shown says which layer of the town it is, and what a script types
    /// has to be short enough to type.
    /// </summary>
    public static readonly string[] Words =
    [
        "grass", "walk", "walk-kerb", "water", "decks", "carriageway", "slabs", "kerb", "paint",
    ];

    public static uint Bit(GroundPart part) => 1u << (int)part;
}
