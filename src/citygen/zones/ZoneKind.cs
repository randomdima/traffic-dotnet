namespace TrafficSimulation.CityGen.Zones;

/// <summary>
/// <b>What a zone is, which is which builders lay it and what they lay by default</b> (GEN-58): a kind's defaults are
/// <see cref="Core.Config.ZoneFigures"/>'s, and a zone's own settings in its map are laid over them
/// (<see cref="ZoneTree"/>).
/// </summary>
/// <remarks>
/// <b>Written into a map by number</b> (<see cref="Map.TownMap"/>), so a kind is added at the end and never renumbered.
/// </remarks>
internal enum ZoneKind : byte
{
    /// <summary>The whole map, whose streets are its own or none: the root of every map that is not a wheel.</summary>
    Town,

    /// <summary>
    /// The whole map, laid as a wheel — a hub, its spokes and an orbital — with a lattice of streets in each of its
    /// districts (<see cref="ZoneParam.Sector"/>).
    /// </summary>
    Wheel,

    /// <summary>A historic core: flats and shops in unbroken terraces on the walk, yards behind.</summary>
    OldTown,

    /// <summary>Blocks of flats along the streets, with gaps between them.</summary>
    Residential,

    /// <summary>Towers and slabs standing back off the streets, in open ground of their own.</summary>
    HighRise,

    /// <summary>Houses on plots of their own, with gardens between them.</summary>
    Suburb,

    /// <summary>Shops, offices and markets.</summary>
    Commercial,

    /// <summary>Works and sheds on big plots, and the yards between them.</summary>
    Industrial,

    /// <summary>A school, a hospital or a church, and its grounds.</summary>
    Civic,

    /// <summary>Rows of lock-up garages.</summary>
    Garages,

    /// <summary>A car park: paved, and nothing built on it.</summary>
    Parking,

    /// <summary>A park, a garden, a cemetery: grass and trees, and nothing built.</summary>
    Park,

    /// <summary>Wood, scrub, heath and marsh: thick with trees, and nothing built.</summary>
    Wild,

    /// <summary>Fields and allotments: open, with the odd shed and greenhouse.</summary>
    Farmland,

    /// <summary>Open ground nothing is built on and little grows on: a square, a beach, a building site, an airfield.</summary>
    Open,
}
