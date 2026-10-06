using System.Numerics;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>A map authored off a brief</b> (GEN-58, <c>--author</c>): the brief's water drawn once and fixed in the map as the
/// course it runs along (<see cref="TownMap.Courses"/>), and its wheel drawn once and zoned — the whole map a
/// <see cref="ZoneKind.Wheel"/> saying its hub, its spokes, its orbital and the town's counts, and a zone for each
/// district saying the sector it is and the lattice its streets are laid on. Everything else is laid off those when the
/// map is opened (<see cref="TownPlan"/>), off the brief's seed — its streets as the brief's own generator laid them.
/// </summary>
/// <remarks>
/// <para>
/// <b>A district is zoned as what a wheel's district is built as</b>: inside the orbital a strict one is an old town and
/// a loose one a residential quarter, outside it a strict one an estate of towers and a loose one a suburb — each laid
/// as its kind is (<see cref="ZoneFigures"/>) until the map is edited to say otherwise. Its outline is its sector,
/// cut to the map; its streets ask the wheel which district a place is in, so an outline is what its buildings and
/// props are laid by and not its streets.
/// </para>
/// <para>
/// <b>The map is handed back as it reads back once written</b> (<see cref="TownMap.Held"/>): a town laid off a brief in
/// memory is the town its written map lays.
/// </para>
/// </remarks>
internal static class TownAuthor
{
    /// <summary>How finely an orbital's arc is drawn into a district's outline: a degree's chord stands off it by centimetres.</summary>
    const float ArcStepRad = MathF.PI / 180f;

    public static TownMap Of(TownBrief brief, SimConfig config)
    {
        brief.Check(brief.Name);
        var worldSizeM = new Vector2(brief.WidthM, brief.HeightM);

        var terrain = new Rng(brief.Seed, Streams.Terrain);
        var water = TerrainStage.Lay(brief, config, ref terrain);
        var wet = new GroundShapes(GroundPieces.None(brief.Seed, worldSizeM, config.PavementWidthM).With(water.Rings), config);

        var district = new Rng(brief.Seed, Streams.District);
        var districts = Districts.Lay(brief, config, wet, water, ref district);
        var wheel = districts.Wheel;

        var zones = new TownMap.ZoneArrays.Builder();
        zones.Add(
            -1, ZoneKind.Wheel,
            [
                (ZoneParam.People, brief.People), (ZoneParam.Cars, brief.Cars), (ZoneParam.Buildings, brief.Buildings),
                (ZoneParam.UnregulatedShare, brief.UnregulatedJunctionShare), (ZoneParam.HubXM, wheel.HubM.X), (ZoneParam.HubYM, wheel.HubM.Y),
                (ZoneParam.RingRadiusM, wheel.RingRadiusM), (ZoneParam.FirstSpokeRad, wheel.FirstSpokeRad), (ZoneParam.Spokes, wheel.Spokes),
            ],
            [TownMap.WholeOutline(worldSizeM)]);

        for (var at = 0; at < wheel.Count; at++)
        {
            var laid = districts[at];
            var outline = Sector(wheel, districts.SpokeBearingRad(laid.Sector), districts.SpokeBearingRad(laid.Sector + 1), laid.Inside, worldSizeM);
            if (outline.Length < 3) continue;

            zones.Add(
                TownMap.ZoneArrays.Root, KindOf(laid),
                [
                    (ZoneParam.Sector, laid.Sector), (ZoneParam.Inside, laid.Inside ? 1f : 0f), (ZoneParam.BearingRad, laid.BearingRad),
                    (ZoneParam.BlockAlongM, laid.BlockAlongM), (ZoneParam.BlockAcrossM, laid.BlockAcrossM), (ZoneParam.Strict, laid.Strict ? 1f : 0f),
                    (ZoneParam.StraightShare, laid.StraightShare),
                ],
                [outline]);
        }

        var courses = water.Any
            ? new TownMap.CourseArrays
            {
                Kind = [water.Kind == WaterKind.River ? TownMap.WaterBody.River : TownMap.WaterBody.Sea], Across = [water.Sideways],
                NearM = [water.NearM], FarM = [water.FarM], PointOffsets = [0, water.CentreM.Length], PointM = water.CentreM,
            }
            : TownMap.CourseArrays.None;

        var map = TownMap.Bare(brief.Name, brief.Description, brief.Seed, worldSizeM, ZoneKind.Wheel, []) with
        {
            Courses = courses,
            Zones = zones.Arrays(),
        };
        return map.Held();
    }

    /// <summary>What a wheel's district is built as, by where it stands and how strictly it is laid.</summary>
    static ZoneKind KindOf(District district) => (district.Inside, district.Strict) switch
    {
        (true, true) => ZoneKind.OldTown,
        (true, false) => ZoneKind.Residential,
        (false, true) => ZoneKind.HighRise,
        (false, false) => ZoneKind.Suburb,
    };

    /// <summary>
    /// <b>One sector of a wheel as an outline cut to the map</b>: from its first spoke to the next, inside the orbital
    /// or between it and far past the map's edge — or out to past the edge where the wheel has no orbital.
    /// </summary>
    static Vector2[] Sector(DistrictWheel wheel, float fromRad, float toRad, bool inside, Vector2 worldSizeM)
    {
        var farM = worldSizeM.Length() * 2f;
        var points = new List<Vector2>();
        if (inside || !wheel.HasRing) points.Add(wheel.HubM);
        else Arc(wheel.RingRadiusM, toRad, fromRad);

        Arc(inside && wheel.HasRing ? wheel.RingRadiusM : farM, fromRad, toRad);
        return Clipped(points, worldSizeM);

        void Arc(float radiusM, float startRad, float endRad)
        {
            var steps = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(endRad - startRad) / ArcStepRad));
            for (var step = 0; step <= steps; step++) points.Add(wheel.HubM + (Heading.Unit(startRad + ((endRad - startRad) * step / steps)) * radiusM));
        }
    }

    /// <summary>A polygon cut to the map's rectangle, one side at a time (Sutherland–Hodgman).</summary>
    static Vector2[] Clipped(List<Vector2> polygon, Vector2 worldSizeM)
    {
        ReadOnlySpan<(Vector2 Normal, float OffsetM)> sides = [(Vector2.UnitX, 0f), (-Vector2.UnitX, -worldSizeM.X), (Vector2.UnitY, 0f), (-Vector2.UnitY, -worldSizeM.Y)];
        var points = polygon;
        foreach (var (normal, offsetM) in sides)
        {
            var kept = new List<Vector2>(points.Count + 4);
            for (var at = 0; at < points.Count; at++)
            {
                var (a, b) = (points[at], points[(at + 1) % points.Count]);
                var (aIn, bIn) = (Vector2.Dot(a, normal) - offsetM, Vector2.Dot(b, normal) - offsetM);
                if (aIn >= 0f) kept.Add(a);
                if ((aIn >= 0f) != (bIn >= 0f)) kept.Add(Vector2.Lerp(a, b, aIn / (aIn - bIn)));
            }

            points = kept;
            if (points.Count == 0) break;
        }

        return [.. points];
    }
}
