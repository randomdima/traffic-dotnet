namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// <b>Transverse Mercator on WGS84 about one meridian, at a scale of one</b> (Snyder, USGS PP 1395, 8-9 and
/// 8-10): east and north in metres off a central meridian and latitude. Within a tenth of a degree of that
/// meridian it is true to well under a millimetre a kilometre, which is what lets a traced map be read at 1:1.
/// </summary>
/// <remarks>
/// <b>Worked in doubles throughout</b>: a place is a float only once it is a metre off the map's own corner,
/// where a float holds it to two millimetres across the widest map a survey has laid.
/// </remarks>
internal sealed class TransverseMercator
{
    const double SemiMajorM = 6378137.0;
    const double Flattening = 1 / 298.257223563;

    readonly double _e2;
    readonly double _ep2;
    readonly double _lon0Rad;
    readonly double _m0;

    public TransverseMercator(double lat0Deg, double lon0Deg)
    {
        _e2 = Flattening * (2 - Flattening);
        _ep2 = _e2 / (1 - _e2);
        _lon0Rad = double.DegreesToRadians(lon0Deg);
        _m0 = Meridian(double.DegreesToRadians(lat0Deg));
    }

    public (double EastM, double NorthM) Project(double latDeg, double lonDeg)
    {
        var phi = double.DegreesToRadians(latDeg);
        var (sin, cos) = Math.SinCos(phi);
        var tan = sin / cos;
        var nu = SemiMajorM / Math.Sqrt(1 - (_e2 * sin * sin));
        var t = tan * tan;
        var c = _ep2 * cos * cos;
        var a = (double.DegreesToRadians(lonDeg) - _lon0Rad) * cos;
        var a2 = a * a;
        var east = nu * (a + ((1 - t + c) * a2 * a / 6)
                         + ((5 - (18 * t) + (t * t) + (72 * c) - (58 * _ep2)) * a2 * a2 * a / 120));
        var north = Meridian(phi) - _m0 + (nu * tan * ((a2 / 2)
                    + ((5 - t + (9 * c) + (4 * c * c)) * a2 * a2 / 24)
                    + ((61 - (58 * t) + (t * t) + (600 * c) - (330 * _ep2)) * a2 * a2 * a2 / 720)));
        return (east, north);
    }

    /// <summary>The distance along the meridian from the equator to a latitude.</summary>
    double Meridian(double phi)
    {
        var e4 = _e2 * _e2;
        var e6 = e4 * _e2;
        return SemiMajorM * (((1 - (_e2 / 4) - (3 * e4 / 64) - (5 * e6 / 256)) * phi)
                             - (((3 * _e2 / 8) + (3 * e4 / 32) + (45 * e6 / 1024)) * Math.Sin(2 * phi))
                             + (((15 * e4 / 256) + (45 * e6 / 1024)) * Math.Sin(4 * phi))
                             - (35 * e6 / 3072 * Math.Sin(6 * phi)));
    }
}
