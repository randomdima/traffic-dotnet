using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Traced;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>What each building is for</b> (<see cref="FootprintUse"/>), read off OSM's tags on it by OSM's own values, and
/// where those say nothing off the land use it stands in.
/// </summary>
/// <remarks>
/// <b>Its own tags first, the land use only where they say nothing</b>: a <c>building=house</c> in industrial ground is a
/// house, and a <c>building=yes</c> in a garage cooperative is a lock-up. Of the land uses round a building, the
/// smallest that tells is read — a school's grounds inside a residential quarter are the school's.
/// </remarks>
internal sealed class FootprintUses
{
    /// <summary>The cell the land uses are filed in, in metres: a few blocks, so a building asks a handful of them.</summary>
    const double CellM = 250;

    /// <summary>How tall a home whose survey says no more stands before it is a block of flats: three storeys, over which nobody builds a house.</summary>
    const float FlatsHeightM = 9f;

    /// <summary>How tall a block of flats stands before it is a tower: eight storeys, the panel blocks' lowest.</summary>
    const float TowerHeightM = 24f;

    /// <summary>The most a home of no stated height covers and is a shed rather than a house.</summary>
    const float ShedLargestM2 = 20f;

    /// <summary>The most a home of no stated height covers and is a house rather than a block of flats.</summary>
    const float HouseLargestM2 = 300f;

    /// <summary>
    /// The least a building nothing says anything of covers and is a works rather than a home: a block of flats this
    /// size is a courtyard block, and those stand in residential ground.
    /// </summary>
    const float WorksSmallestM2 = 1500f;

    /// <summary>
    /// <b>What a building is drawn as</b>, off what the survey says it is for and, for a home it says no more of, how
    /// tall it stands or else how much ground it covers.
    /// </summary>
    public static BuildingLook LookOf(FootprintUse use, float heightM, float areaM2) => use switch
    {
        FootprintUse.House => BuildingLook.House,
        FootprintUse.Apartments => heightM >= TowerHeightM ? BuildingLook.Tower : BuildingLook.Apartments,
        FootprintUse.Garages => BuildingLook.Garages,
        FootprintUse.Shed => BuildingLook.Shed,
        FootprintUse.Kiosk => areaM2 <= ShedLargestM2 ? BuildingLook.Kiosk : BuildingLook.Retail,
        FootprintUse.Retail => BuildingLook.Retail,
        FootprintUse.Office => BuildingLook.Office,
        FootprintUse.Industrial or FootprintUse.School or FootprintUse.Hospital or FootprintUse.Religious
            when areaM2 <= ShedLargestM2 => BuildingLook.Shed,
        FootprintUse.Industrial => BuildingLook.Industrial,
        FootprintUse.School => BuildingLook.School,
        FootprintUse.Hospital => BuildingLook.Hospital,
        FootprintUse.Religious => BuildingLook.Religious,
        FootprintUse.Canopy => BuildingLook.Canopy,
        FootprintUse.Greenhouse => BuildingLook.Greenhouse,
        FootprintUse.Unknown when heightM <= 0f && areaM2 >= WorksSmallestM2 => BuildingLook.Industrial,
        _ => HomeOf(heightM, areaM2),
    };

    static BuildingLook HomeOf(float heightM, float areaM2)
    {
        if (heightM >= TowerHeightM) return BuildingLook.Tower;
        if (heightM >= FlatsHeightM || areaM2 > HouseLargestM2) return BuildingLook.Apartments;
        return heightM <= 0f && areaM2 <= ShedLargestM2 ? BuildingLook.Shed : BuildingLook.House;
    }

    readonly List<(Pt[][] Rings, double AreaM2, FootprintUse Use)> _zones = [];
    readonly Dictionary<(int, int), List<int>> _cells = [];

    public FootprintUses(List<ZoneRecord> zones, Plane plane)
    {
        foreach (var zone in zones)
        {
            if (zone.Outer is null || ZoneUse(zone.Kind) is not { } use) continue;

            var rings = zone.Outer.Concat(zone.Inner ?? []).Select(plane.Line).Where(ring => ring.Length >= 3).ToArray();
            if (rings.Length == 0) continue;

            var box = Box.Of(rings.SelectMany(ring => ring).ToArray());
            var index = _zones.Count;
            _zones.Add((rings, zone.AreaM2 ?? Shape.Area(rings, []), use));
            for (var x = (int)Math.Floor(box.MinX / CellM); x <= (int)Math.Floor(box.MaxX / CellM); x++)
            {
                for (var y = (int)Math.Floor(box.MinY / CellM); y <= (int)Math.Floor(box.MaxY / CellM); y++)
                {
                    if (!_cells.TryGetValue((x, y), out var filed)) _cells[(x, y)] = filed = [];
                    filed.Add(index);
                }
            }
        }
    }

    /// <summary>What a building is for, its outline standing about <paramref name="centreM"/>.</summary>
    public FootprintUse Of(BuildingRecord building, Pt centreM)
    {
        var own = OwnUse(building);
        if (own != FootprintUse.Unknown) return own;

        var best = (Use: own, AreaM2: double.MaxValue);
        if (_cells.TryGetValue(((int)Math.Floor(centreM.X / CellM), (int)Math.Floor(centreM.Y / CellM)), out var near))
        {
            foreach (var index in near)
            {
                var (rings, areaM2, use) = _zones[index];
                if (areaM2 < best.AreaM2 && Shape.Inside(centreM, rings)) best = (use, areaM2);
            }
        }

        return best.Use;
    }

    /// <summary>
    /// What its own tags say, by OSM's values: its <c>building</c> value, else its <c>amenity</c> or <c>shop</c>.
    /// <see cref="FootprintUse.Unknown"/> where they say a building and nothing more, as a machine-traced outline's
    /// say nothing at all.
    /// </summary>
    static FootprintUse OwnUse(BuildingRecord building)
    {
        if (building.Source != "osm") return FootprintUse.Unknown;

        var said = building.Use switch
        {
            "house" or "detached" or "semidetached_house" or "bungalow" or "cabin" or "farm" => FootprintUse.House,
            "apartments" or "dormitory" or "terrace" => FootprintUse.Apartments,
            "residential" => FootprintUse.Residential,
            "garage" or "garages" or "carport" or "parking" => FootprintUse.Garages,
            "shed" or "hut" or "service" or "guardhouse" or "toilets" or "farm_auxiliary" or "barn" or "stable" => FootprintUse.Shed,
            "kiosk" => FootprintUse.Kiosk,
            "retail" or "commercial" or "supermarket" or "shop" or "mall" => FootprintUse.Retail,
            "office" or "civic" or "public" or "government" or "hotel" or "train_station" or "transportation" => FootprintUse.Office,
            "industrial" or "warehouse" or "factory" or "manufacture" or "hangar" or "silo" or "storage_tank" => FootprintUse.Industrial,
            "school" or "kindergarten" or "college" or "university" => FootprintUse.School,
            "hospital" => FootprintUse.Hospital,
            "church" or "chapel" or "cathedral" or "temple" or "mosque" or "synagogue" or "religious" or "monastery" => FootprintUse.Religious,
            "roof" => FootprintUse.Canopy,
            "greenhouse" => FootprintUse.Greenhouse,
            _ => FootprintUse.Unknown,
        };
        if (said != FootprintUse.Unknown) return said;

        building.Tags.TryGetValue("amenity", out var amenity);
        building.Tags.TryGetValue("shop", out var shop);
        return amenity switch
        {
            "school" or "kindergarten" or "college" or "university" => FootprintUse.School,
            "hospital" or "clinic" or "doctors" => FootprintUse.Hospital,
            "place_of_worship" => FootprintUse.Religious,
            "shelter" or "toilets" => FootprintUse.Shed,
            "parking" => FootprintUse.Garages,
            "townhall" or "police" or "library" or "theatre" or "community_centre" or "bank" or "post_office" => FootprintUse.Office,
            "cafe" or "restaurant" or "fast_food" or "bar" or "pharmacy" or "fuel" or "nightclub" => FootprintUse.Retail,
            _ when shop is "kiosk" => FootprintUse.Kiosk,
            _ when shop is not null => FootprintUse.Retail,
            _ => FootprintUse.Unknown,
        };
    }

    /// <summary>What a building standing in a land use of this kind is for, or null where the land use does not tell.</summary>
    static FootprintUse? ZoneUse(string kind) => kind switch
    {
        "residential" => FootprintUse.Residential,
        "industrial" or "port" or "railway" or "military" => FootprintUse.Industrial,
        "garages" => FootprintUse.Garages,
        "education" => FootprintUse.School,
        "health" => FootprintUse.Hospital,
        "commercial" or "retail" or "fuel" => FootprintUse.Retail,
        "market" => FootprintUse.Kiosk,
        "religious" => FootprintUse.Religious,
        _ => null,
    };
}
