namespace TrafficSimulation.Core.Config;

/// <summary>
/// <b>What each kind of zone is laid by where its map says nothing of its own</b> (GEN-58): a map's zone says what it
/// knows of itself — a survey what it measured, a hand what it meant — and every setting it leaves unsaid is its kind's,
/// one group here a kind.
/// </summary>
/// <remarks>
/// <b>A kind that builds nothing still says how thickly it grows</b>: the whole map's own kinds build nothing of their
/// own accord, and what grows on ground no zone speaks for is the whole map's.
/// </remarks>
internal sealed class ZoneFigures
{
    public ZoneDefaults Town { get; init; } = new();

    public ZoneDefaults Wheel { get; init; } = new();

    public ZoneDefaults OldTown { get; init; } = new()
    {
        Frontage = 0.95f, FrontSpreadM = 0.2f, SkewDeg = 1f, Variety = 0.9f, Growth = 0.2f,
        Looks = new() { Apartments = 0.75f, Retail = 0.15f, Office = 0.1f },
    };

    public ZoneDefaults Residential { get; init; } = new()
    {
        Frontage = 0.6f, FrontSpreadM = 1.5f, SkewDeg = 3f, Variety = 0.6f, Interior = 0.05f, Growth = 0.4f,
        Looks = new() { Apartments = 0.6f, House = 0.3f, Retail = 0.1f },
    };

    public ZoneDefaults HighRise { get; init; } = new()
    {
        Frontage = 0.3f, FrontM = 12f, FrontSpreadM = 4f, SkewDeg = 5f, Variety = 0.3f, Interior = 0.12f, Growth = 0.5f,
        Looks = new() { Tower = 0.6f, Apartments = 0.3f, Retail = 0.1f },
    };

    public ZoneDefaults Suburb { get; init; } = new()
    {
        Frontage = 0.6f, FrontM = 3f, FrontSpreadM = 1.5f, SkewDeg = 6f, Variety = 0.9f, Growth = 0.5f,
        Looks = new() { House = 0.85f, Shed = 0.1f, Garages = 0.05f },
    };

    public ZoneDefaults Commercial { get; init; } = new()
    {
        Frontage = 0.7f, FrontSpreadM = 2f, SkewDeg = 3f, Variety = 0.8f, Interior = 0.05f, Growth = 0.2f,
        Looks = new() { Retail = 0.6f, Office = 0.3f, Kiosk = 0.1f },
    };

    public ZoneDefaults Industrial { get; init; } = new()
    {
        Frontage = 0.4f, FrontM = 6f, FrontSpreadM = 4f, SkewDeg = 4f, Variety = 0.5f, Interior = 0.15f, Growth = 0.3f,
        Looks = new() { Industrial = 0.7f, Shed = 0.2f, Office = 0.1f },
    };

    public ZoneDefaults Civic { get; init; } = new()
    {
        Frontage = 0.4f, FrontM = 8f, FrontSpreadM = 4f, SkewDeg = 3f, Variety = 0.5f, Interior = 0.05f, Growth = 0.5f,
        Looks = new() { School = 0.5f, Hospital = 0.2f, Religious = 0.2f, Office = 0.1f },
    };

    public ZoneDefaults Garages { get; init; } = new()
    {
        Frontage = 0.9f, FrontM = 2f, FrontSpreadM = 1f, SkewDeg = 2f, Variety = 0.1f, Interior = 0.3f, Growth = 0.1f,
        Looks = new() { Garages = 1f },
    };

    public ZoneDefaults Parking { get; init; } = new() { Growth = 0f };

    public ZoneDefaults Park { get; init; } = new() { Growth = 0.6f };

    public ZoneDefaults Wild { get; init; } = new();

    public ZoneDefaults Farmland { get; init; } = new()
    {
        Frontage = 0.1f, FrontM = 4f, FrontSpreadM = 4f, SkewDeg = 10f, Variety = 0.8f, Growth = 0.3f,
        Looks = new() { Greenhouse = 0.5f, Shed = 0.5f },
    };

    public ZoneDefaults Open { get; init; } = new() { Growth = 0.1f };
}

/// <summary>
/// <b>One kind's settings</b>, each a zone's own setting of the same name (<c>CityGen.Zones.ZoneParam</c>) where its
/// map leaves it unsaid. Unset, a kind builds nothing and grows as thickly as the ground holds.
/// </summary>
internal sealed class ZoneDefaults
{
    /// <summary>How much of the street frontage is built, as a share.</summary>
    public float Frontage { get; init; }

    /// <summary>How far a building's front stands off the carriageway's edge — never nearer than the building line.</summary>
    public float FrontM { get; init; }

    /// <summary>How far either way of that a front is drawn to stand.</summary>
    public float FrontSpreadM { get; init; }

    /// <summary>How far either way of square to its street a building is drawn to turn.</summary>
    public float SkewDeg { get; init; }

    /// <summary>How many of the buildings along a street are drawn afresh rather than repeating the one before, as a share.</summary>
    public float Variety { get; init; } = 1f;

    /// <summary>How much of the ground behind the frontage is built over, as a share.</summary>
    public float Interior { get; init; }

    /// <summary>The ground a building along the street typically covers; nought for any its looks are drawn at.</summary>
    public float FootprintM2 { get; init; }

    /// <summary>And a building behind the frontage.</summary>
    public float BehindM2 { get; init; }

    /// <summary>How thickly what grows wild covers the open ground, as a share of the most the ground holds (GEN-6b).</summary>
    public float Growth { get; init; } = 1f;

    /// <summary>What is built, as each look's share.</summary>
    public LookShares Looks { get; init; } = new();
}

/// <summary>
/// <b>What is built, as each look's share</b>: one figure a look of <c>CityGen.BuildingLook</c>, named as it is and
/// read in its order. Shares are weighed against each other, so they need not add to one.
/// </summary>
internal sealed class LookShares
{
    public float House { get; init; }
    public float Apartments { get; init; }
    public float Tower { get; init; }
    public float Garages { get; init; }
    public float Shed { get; init; }
    public float Kiosk { get; init; }
    public float Retail { get; init; }
    public float Office { get; init; }
    public float Industrial { get; init; }
    public float School { get; init; }
    public float Hospital { get; init; }
    public float Religious { get; init; }
    public float Canopy { get; init; }
    public float Greenhouse { get; init; }

    /// <summary>Every share in the looks' own order.</summary>
    public float[] InOrder() =>
        [House, Apartments, Tower, Garages, Shed, Kiosk, Retail, Office, Industrial, School, Hospital, Religious, Canopy, Greenhouse];
}
