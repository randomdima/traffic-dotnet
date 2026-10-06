using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Zones;

namespace TrafficSimulation.Tests.CityGen.Zones;

/// <summary>Zones laid by hand for a town a test lays: the whole map with settings of its own, and zones inside it.</summary>
internal static class Zoned
{
    /// <summary>A zone inside the whole map: its kind, its outline and what it says of itself.</summary>
    internal readonly record struct Zone(ZoneKind Kind, Vector2[] Outline, (ZoneParam, float)[] Settings, int Parent = TownMap.ZoneArrays.Root);

    /// <summary>The whole map, a town of the size given, saying the settings given and holding the zones given.</summary>
    public static TownMap.ZoneArrays Town(float widthM, float heightM, (ZoneParam, float)[] settings, params Zone[] zones)
    {
        var builder = new TownMap.ZoneArrays.Builder();
        builder.Add(-1, ZoneKind.Town, settings, [TownMap.WholeOutline(new Vector2(widthM, heightM))]);
        foreach (var zone in zones) builder.Add(zone.Parent, zone.Kind, zone.Settings, [zone.Outline]);

        return builder.Arrays();
    }

    /// <summary>
    /// <b>A street of houses built end to end</b>: every metre of its frontage built, every front on the building line and
    /// square to the street, a house each — what a test that counts what stands needs, nothing drawn but which prefab.
    /// </summary>
    public static Zone Terrace(Vector2[] outline, BuildingLook look = BuildingLook.House) => new(
        ZoneKind.Suburb, outline,
        [(ZoneParam.Frontage, 1f), (ZoneParam.FrontM, 0f), (ZoneParam.FrontSpreadM, 0f), (ZoneParam.SkewDeg, 0f), (ZoneParam.Variety, 1f), (ZoneParams.Of(look), 1f)]);
}
