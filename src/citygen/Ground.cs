namespace TrafficSimulation.CityGen;

/// <summary>
/// The seven kinds of ground a town is laid out of. The type is named for the ground rather than for the
/// folder it sits in, because a type called <c>Terrain</c> inside <c>…World.Terrain</c> is ambiguous at
/// every call site that imports both.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the plan's vocabulary and lives with the plan</b>, so that <see cref="CityPlan"/> is pure
/// data that everything else reads and nothing the plan needs points back out of this folder. What each
/// member is <em>permitted</em> to do is <c>World.Terrain.GroundCatalog</c>'s, and that is the direction
/// every other consumer already goes.
/// </para>
/// <para>
/// <b>Two folders name a member of this enum and no third one may</b> (TER-2a): <c>citygen/</c>, which lays
/// the ground and answers what is on it (<see cref="GroundShapes"/>), and <c>world/terrain/</c>, which says
/// what each kind permits. Everywhere above those, <b>a rule is written against a permission set and never
/// against a type</b> — one written against <c>Sidewalk</c> breaks the day a town gains a boardwalk, and one
/// written against <em>walkable</em> does not.
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
    Sidewalk = 6,
}
