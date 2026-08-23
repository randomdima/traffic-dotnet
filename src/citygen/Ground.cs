namespace TrafficSimulation.CityGen;

/// <summary>
/// The eight kinds of ground a town is laid out of, in the order the <c>.town</c> file's cell bytes
/// carry them. The type is named for the ground rather than for the folder it sits in, because a type
/// called <c>Terrain</c> inside <c>…World.Terrain</c> is ambiguous at every call site that imports both.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the plan's vocabulary and lives with the plan</b>, so that <see cref="CityPlan"/> is pure
/// data that everything else reads and nothing the plan needs points back out of this folder. What each
/// member is <em>permitted</em> to do is <c>World.Terrain.GroundCatalog</c>'s, and that is the direction
/// every other consumer already goes.
/// </para>
/// <para>
/// <b>Nothing outside <c>world/terrain/</c> may name a member of this enum</b>: a rule is written
/// against a permission set and never against a type. A rule written against <c>Sidewalk</c> breaks the
/// day a town gains a boardwalk; one written against <em>walkable</em> does not.
/// </para>
/// </remarks>
internal enum Ground : byte
{
    Grass = 0,
    Road = 1,
    Intersection = 2,
    Crosswalk = 3,
    Parking = 4,
    Water = 5,
    Footway = 6,
    Sidewalk = 7,
}

internal static class Grounds
{
    /// <summary>
    /// How many kinds there are, which is the bound a cell byte read off a file is checked against.
    /// It is the enum's own last member rather than a written figure, so adding a kind moves it.
    /// </summary>
    public const int Kinds = (int)Ground.Sidewalk + 1;
}
