using System.Numerics;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>How a town grades out to the country</b> (GEN-76): built solid about its core, each district further out building
/// less of what its kind builds, down to <see cref="CityGenFigures.TownEdgeFill"/> at its furthest land — and some of the
/// districts on the map's rim left to the country, farmed or wild.
/// </summary>
/// <remarks>
/// <b>Only the rim is left to the country</b>: a district given up inside the town could cut the districts beyond it off
/// from the rest, and a street network in pieces keeps only its largest (GEN-69).
/// </remarks>
internal static class TownExtent
{
    /// <summary>The stream whether a district left to the country is farmed or wild is drawn on.</summary>
    const ulong CountryStream = 0x636F_756E_7472_7900;

    /// <summary>The stream how far a district's distance from the core is moved is drawn on.</summary>
    const ulong GrowthStream = 0x6772_6F77_7468_0000;

    /// <summary>The stream how far below its place's a district's fill is drawn on.</summary>
    const ulong FillStream = 0x6669_6C6C_0000_0000;

    /// <summary>The stream whether a district on the rim is left to the country is drawn on.</summary>
    const ulong RimStream = 0x7269_6D00_0000_0000;

    /// <summary>
    /// <b>How far the land's furthest ground stands from the core</b>, which a district's way out is a share of: measured
    /// on the land's districts, before any is left to the country.
    /// </summary>
    public static float FurthestM(Partition land, Vector2 coreM)
    {
        var furthestM = 0f;
        for (var cell = 0; cell < land.Label.Length; cell++)
        {
            if (land.Label[cell] >= 0) furthestM = MathF.Max(furthestM, Vector2.Distance(land.Grid.CentreOf(cell), coreM));
        }

        return furthestM;
    }

    /// <summary>
    /// <b>Which of the land's districts the town is built in</b>: all but those on the map's rim drawn to the country, at
    /// <see cref="CityGenFigures.CountryRimShare"/> of them at the furthest land and none within the solid middle.
    /// </summary>
    /// <param name="roadsIn">The roads into the town from outside, which it is built thicker along.</param>
    public static bool[] Built(Partition land, Vector2 coreM, float furthestM, IReadOnlyList<Polyline> roadsIn, SimConfig config, ulong seed)
    {
        var built = new bool[land.Count];
        var rim = Rim(land);
        for (var district = 0; district < land.Count; district++)
        {
            var share = config.CityGen.CountryRimShare * Beyond(land, district, coreM, furthestM, roadsIn, config, seed);
            built[district] = !rim[district] || Drawn(land, district, RimStream, seed) >= share;
        }

        return built;
    }

    /// <summary>
    /// <b>How much of what its kind builds each district builds</b>: all of it within the solid middle, falling to
    /// <see cref="CityGenFigures.TownEdgeFill"/> at the furthest land, and drawn below that by up to
    /// <see cref="CityGenFigures.TownFillSpread"/> of it.
    /// </summary>
    public static float[] Fill(Partition partition, Vector2 coreM, float furthestM, IReadOnlyList<Polyline> roadsIn, SimConfig config, ulong seed)
    {
        var figures = config.CityGen;
        var fill = new float[partition.Count];
        for (var district = 0; district < partition.Count; district++)
        {
            var placed = 1f + ((figures.TownEdgeFill - 1f) * Beyond(partition, district, coreM, furthestM, roadsIn, config, seed));
            fill[district] = placed * (1f - (figures.TownFillSpread * Drawn(partition, district, FillStream, seed)));
        }

        return fill;
    }

    /// <summary>
    /// <b>What a district left to the country is</b>: farmland, or wild at <see cref="CityGenFigures.CountryWildShare"/>,
    /// drawn on a stream keyed on where it stands (GEN-11).
    /// </summary>
    public static ZoneKind Country(Partition partition, int district, ulong seed, SimConfig config) =>
        Drawn(partition, district, CountryStream, seed) < config.CityGen.CountryWildShare ? ZoneKind.Wild : ZoneKind.Farmland;

    /// <summary>
    /// <b>How far past the solid middle a district stands</b>, as a share of the way from its edge to the furthest land:
    /// nought within it, one at the furthest.
    /// </summary>
    static float Beyond(Partition partition, int district, Vector2 coreM, float furthestM, IReadOnlyList<Polyline> roadsIn, SimConfig config, ulong seed)
    {
        var solid = config.CityGen.TownSolidShare;
        var outward = furthestM > 0f ? Reach(partition, district, coreM, roadsIn, config, seed) / furthestM : 0f;
        return Math.Clamp((outward - solid) / (1f - solid), 0f, 1f);
    }

    /// <summary>
    /// <b>How far out a district stands</b>: its middle's distance from the core, less
    /// <see cref="CityGenFigures.TownRoadwardShare"/> of it where a road in passes its middle, falling to none
    /// <see cref="CityGenFigures.TownRoadwardReachM"/> off — and moved by up to <see cref="CityGenFigures.TownEdgeJitterShare"/>
    /// either way.
    /// </summary>
    static float Reach(Partition partition, int district, Vector2 coreM, IReadOnlyList<Polyline> roadsIn, SimConfig config, ulong seed)
    {
        var figures = config.CityGen;
        var middleM = partition.CentreM[district];
        var offRoadM = float.PositiveInfinity;
        foreach (var road in roadsIn) offRoadM = MathF.Min(offRoadM, road.Nearest(middleM).AwayM);

        var beside = MathF.Max(0f, 1f - (offRoadM / figures.TownRoadwardReachM));
        var jitter = (Drawn(partition, district, GrowthStream, seed) * 2f) - 1f;
        return Vector2.Distance(middleM, coreM) * (1f - (figures.TownRoadwardShare * beside)) * (1f + (figures.TownEdgeJitterShare * jitter));
    }

    /// <summary>A district's draw on one stream, keyed on where it stands (GEN-11) so a cut that keeps it keeps its draws.</summary>
    static float Drawn(Partition partition, int district, ulong stream, ulong seed) =>
        new Rng(seed, Streams.Keyed(Streams.District ^ stream, Districts.KeyOf(partition, district))).NextFloat();

    /// <summary>Which districts reach the map's edge: hold a cell on it, or beside the margin kept along it (<see cref="Barrier.Edge"/>).</summary>
    static bool[] Rim(Partition partition)
    {
        var (grid, rim) = (partition.Grid, new bool[partition.Count]);
        for (var cell = 0; cell < partition.Label.Length; cell++)
        {
            var district = partition.Label[cell];
            if (district < 0 || rim[district]) continue;

            var (column, row) = (cell % grid.Columns, cell / grid.Columns);
            var onTheEdge = column == 0 || row == 0 || column == grid.Columns - 1 || row == grid.Rows - 1;
            for (var dy = -1; dy <= 1 && !onTheEdge; dy += 2) onTheEdge = partition.Barrier[grid.Index(column, row + dy)] == Barrier.Edge;
            for (var dx = -1; dx <= 1 && !onTheEdge; dx += 2) onTheEdge = partition.Barrier[grid.Index(column + dx, row)] == Barrier.Edge;

            rim[district] = onTheEdge;
        }

        return rim;
    }
}
