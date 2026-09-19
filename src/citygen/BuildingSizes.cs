using System.Numerics;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The footprints a town sizes its buildings at</b> (GEN-54): the ordinary roofs to draw one from, and
/// the one roof each service use was drawn at.
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
/// measurements it read off the sheets.
/// </para>
/// </remarks>
internal readonly record struct BuildingSizes(Vector2[] OrdinaryM, Vector2[] ByUseM)
{
    /// <summary>How many uses there are to carry a footprint for, which is the whole of <see cref="BuildingUse"/>.</summary>
    public const int Uses = (int)BuildingUse.Depot + 1;

    /// <summary>No art at all — a town laid in code, which stands nothing that wears a roof.</summary>
    public static BuildingSizes None => new([], new Vector2[Uses]);

    /// <summary>The footprint the roof a use names was drawn at (AMB-1a, SRV-1a).</summary>
    public Vector2 Of(BuildingUse use) => ByUseM[(int)use];
}
