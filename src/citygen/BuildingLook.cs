namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>What a prefab building is drawn as</b>, which is also what a traced map's zone says is built in it (GEN-57): a
/// prefab names one and a zone names one, and the walk beside a zone is built of prefabs of its look.
/// </summary>
/// <remarks>
/// The names are the prefab file's own <c>look</c> values, lower-cased, so the catalogue reads them with no table.
/// </remarks>
internal enum BuildingLook : byte
{
    House,

    /// <summary>A block of flats of up to eight storeys.</summary>
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
