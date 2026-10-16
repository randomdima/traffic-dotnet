using System.Globalization;
using System.Text.Json;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>A coordinate as OSM stores it</b>: an integer count of 1e-7°, read off the decimal every source writes it as so
/// the integer comes back exact.
/// </summary>
internal static class OsmDegrees
{
    public const decimal UnitsPerDegree = 10_000_000m;

    /// <summary>Overpass prints OSM's stored integer as seven decimals, so the decimal read back is exact.</summary>
    public static int Units(JsonElement degrees) => (int)Math.Round(degrees.GetDecimal() * UnitsPerDegree);

    /// <summary>A coordinate written as text — an OSM change file's attribute, a timetable's column.</summary>
    public static int Units(string degrees) => (int)Math.Round(decimal.Parse(degrees, CultureInfo.InvariantCulture) * UnitsPerDegree);
}
