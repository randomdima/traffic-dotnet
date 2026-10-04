namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>What a prefab building is drawn as</b>, which is also what a traced footprint is fitted by (GEN-57): a prefab
/// names one and a footprint is read as one, and a footprint wears a prefab of its own look.
/// </summary>
/// <remarks>
/// The names are the prefab file's own <c>look</c> values, lower-cased, so the catalogue reads them with no table.
/// </remarks>
internal enum BuildingLook : byte
{
    House,

    /// <summary>A block of flats of up to <see cref="Core.Config.CityGenFigures.TracedTowerHeightM"/>.</summary>
    Apartments,

    /// <summary>A block of flats as tall as that or taller.</summary>
    Tower,
    Garages,
    Shed,
    Kiosk,
    Retail,
    Office,
    Industrial,
    School,
    Hospital,
    Religious,
    Canopy,
    Greenhouse,
}
