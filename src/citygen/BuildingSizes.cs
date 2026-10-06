using System.Numerics;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The footprints a town sizes its buildings at</b> (GEN-54): the one roof each service use was drawn at, and the
/// prefabs every other building is stood as (GEN-58).
/// </summary>
/// <remarks>
/// <para>
/// <b>A building is sized by the roof it will wear</b>, so the sizes are the art's and the placement is the
/// plan's. Authored as a band of its own instead, the plan would state a size the catalogue then answered
/// with the nearest thing it had — two answers about one rectangle, kept in step by hand.
/// </para>
/// <para>
/// <b>It is data crossing a seam and not a type</b> ([the slice map](../../docs/slice-map.md)). The
/// catalogue is <c>World.Statics.BuildingCatalog</c>, which sits above the plan; what comes down is the
/// measurements it read off the sheets, and a prefab is named to the plan by its place in <see cref="PrefabM"/>.
/// </para>
/// </remarks>
/// <param name="PrefabM">Each prefab's footprint, its door's wall first, in the catalogue's own order.</param>
/// <param name="PrefabCornerM">The radius each prefab's corners are rounded at.</param>
/// <param name="PrefabLook">And what each is drawn as, which is what a footprint is fitted by.</param>
internal readonly record struct BuildingSizes(Vector2[] ByUseM, Vector2[] PrefabM, float[] PrefabCornerM, BuildingLook[] PrefabLook)
{
    /// <summary>How many uses there are to carry a footprint for, which is the whole of <see cref="BuildingUse"/>.</summary>
    public const int Uses = (int)BuildingUse.Depot + 1;

    /// <summary>No art at all — a town laid in code, which stands nothing that wears a roof.</summary>
    public static BuildingSizes None => new(new Vector2[Uses], [], [], []);

    /// <summary>The footprint the roof a use names was drawn at (AMB-1a, SRV-1a).</summary>
    public Vector2 Of(BuildingUse use) => ByUseM[(int)use];
}
