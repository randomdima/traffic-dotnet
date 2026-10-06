namespace TrafficSimulation.CityGen.Zones;

/// <summary>
/// <b>What one zone is laid by, every setting resolved</b> (<see cref="ZoneTree"/>): its own where it says one, else its
/// kind's (<see cref="Core.Config.ZoneFigures"/>) — or, for a district, the zone's round it.
/// </summary>
/// <param name="LookShares">
/// Each look's share of what is built, adding to one, or all nought where nothing is. <b>A zone that says any look says
/// its whole mix</b>: a look its parent builds and it does not name is not built in it.
/// </param>
/// <param name="BearingDeg">The bearing the buildings behind its frontage are laid square to, or null for the nearest street's.</param>
/// <param name="FootprintM2">The ground a building along the street typically covers, or nought for any.</param>
/// <param name="BehindM2">And a building behind the frontage.</param>
internal sealed record ZoneSettings(
    ZoneKind Kind, float Frontage, float FrontM, float FrontSpreadM, float SkewDeg, float Variety, float Interior, float? BearingDeg,
    float Growth, float FootprintM2, float BehindM2, float[] LookShares)
{
    /// <summary>Whether anything is built in it at all: some frontage or some ground behind it, and some look to build it in.</summary>
    public bool Builds => (Frontage > 0f || Interior > 0f) && Array.Exists(LookShares, share => share > 0f);

    /// <summary>A look drawn by its share, off a draw between nought and one.</summary>
    public BuildingLook Look(float draw)
    {
        var look = 0;
        for (; look < LookShares.Length - 1; look++)
        {
            draw -= LookShares[look];
            if (draw < 0f && LookShares[look] > 0f) return (BuildingLook)look;
        }

        while (look > 0 && LookShares[look] <= 0f) look--;
        return (BuildingLook)look;
    }
}
