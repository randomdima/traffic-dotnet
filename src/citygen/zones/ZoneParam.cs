namespace TrafficSimulation.CityGen.Zones;

/// <summary>
/// <b>One setting a zone's builders read</b> (GEN-58): a zone sets what it says of itself, and whatever it leaves unsaid
/// is its kind's — or, for a district, the zone's round it (<see cref="ZoneTree"/>).
/// </summary>
/// <remarks>
/// <b>Written into a map by number</b> (<see cref="Map.TownMap"/>), each to its own step (<see cref="ZoneParams.StepOf"/>),
/// so a setting is added and never renumbered. <b>What is built is one setting a look</b>, at
/// <see cref="LookFirst"/> on: the share of the buildings that are of it.
/// </remarks>
internal enum ZoneParam : byte
{
    /// <summary>How much of the street frontage is built, as a share: a terrace all of it, a street of houses with gardens some.</summary>
    Frontage,

    /// <summary>How far a building's front stands off the carriageway's edge, in metres — never nearer than the building line.</summary>
    FrontM,

    /// <summary>How far either way of that a front is drawn to stand, in metres: nought for a street built to one line.</summary>
    FrontSpreadM,

    /// <summary>How far either way of square to its street a building is drawn to turn, in degrees.</summary>
    SkewDeg,

    /// <summary>
    /// How many of the buildings along a street are drawn afresh rather than repeating the one before, as a share: a row of
    /// one design is nought, a street of no two alike one.
    /// </summary>
    Variety,

    /// <summary>How much of the ground behind the frontage is built over, as a share: an estate's towers in their open ground.</summary>
    Interior,

    /// <summary>The bearing the buildings behind the frontage are laid square to, in degrees east of north; unsaid, the nearest street's.</summary>
    BearingDeg,

    /// <summary>How thickly what grows wild covers the open ground, as a share of the most the ground holds (GEN-6b).</summary>
    Growth,

    /// <summary>The town's: how many people it stands at its doors (GEN-7).</summary>
    People,

    /// <summary>The town's: how many cars it stands where nobody lives in it (GEN-7).</summary>
    Cars,

    /// <summary>
    /// The town's: how many buildings it plans — what its car parks are counted off (GEN-53), and at most how many it
    /// stands of what its zones would.
    /// </summary>
    Buildings,

    /// <summary>The town's: how many of the junctions that could carry lights are left to the ranking instead (TER-5e).</summary>
    UnregulatedShare,

    /// <summary>A wheel's hub, metres east of the map's west edge.</summary>
    HubXM,

    /// <summary>And metres south of its north edge.</summary>
    HubYM,

    /// <summary>A wheel's orbital, as its radius about the hub; nought lays none.</summary>
    RingRadiusM,

    /// <summary>The bearing a wheel's first spoke runs out on, in radians.</summary>
    FirstSpokeRad,

    /// <summary>How many spokes a wheel runs out on.</summary>
    Spokes,

    /// <summary>Which of a wheel's sectors a district is, counted from its first spoke.</summary>
    Sector,

    /// <summary>Whether a wheel's district is inside its orbital (one) or outside it (nought).</summary>
    Inside,

    /// <summary>The bearing a district's streets are laid on, in radians.</summary>
    BearingRad,

    /// <summary>How far apart a district's streets stand along its bearing.</summary>
    BlockAlongM,

    /// <summary>And across it.</summary>
    BlockAcrossM,

    /// <summary>Whether a district is laid as a strict grid (one) or a loose one (nought).</summary>
    Strict,

    /// <summary>How many of a district's streets are laid straight, as a share (GEN-47).</summary>
    StraightShare,

    /// <summary>The ground a building along the street typically covers, in square metres; unsaid, any its looks are drawn at.</summary>
    FootprintM2,

    /// <summary>And a building behind the frontage.</summary>
    BehindM2,

    /// <summary>The first look's share of what is built; look <c>n</c>'s is this plus <c>n</c> (<see cref="BuildingLook"/>).</summary>
    LookFirst = 64,
}

/// <summary>How each setting is written and named.</summary>
internal static class ZoneParams
{
    /// <summary>One past the last look's setting.</summary>
    public const int LookPast = (int)ZoneParam.LookFirst + Looks;

    /// <summary>How many looks there are to give a share of.</summary>
    public const int Looks = (int)BuildingLook.Greenhouse + 1;

    /// <summary>The setting that is one look's share.</summary>
    public static ZoneParam Of(BuildingLook look) => (ZoneParam)((int)ZoneParam.LookFirst + (int)look);

    /// <summary>The look a setting is the share of, if it is one.</summary>
    public static BuildingLook? LookOf(ZoneParam param) =>
        (int)param is >= (int)ZoneParam.LookFirst and < LookPast ? (BuildingLook)((int)param - (int)ZoneParam.LookFirst) : null;

    /// <summary>Whether a map's number names a setting this build knows.</summary>
    public static bool Known(ZoneParam param) => param <= ZoneParam.BehindM2 || LookOf(param) is not null;

    /// <summary>
    /// <b>Whether a setting is written as it is</b>, a float's own four bytes: a wheel's own figures, which every street
    /// of it is laid off, so a wheel authored off a brief is laid as the brief's own wheel to the last bit.
    /// </summary>
    public static bool Exact(ZoneParam param) => param is ZoneParam.HubXM or ZoneParam.HubYM or ZoneParam.RingRadiusM
        or ZoneParam.FirstSpokeRad or ZoneParam.BearingRad or ZoneParam.BlockAlongM or ZoneParam.BlockAcrossM or ZoneParam.StraightShare;

    /// <summary>
    /// <b>The step a setting that is not <see cref="Exact"/> is written in</b>: a value is held as a whole number of these,
    /// so a map read back is the map written. A share to a thousandth, a distance or an angle to a tenth, a count whole.
    /// </summary>
    public static double StepOf(ZoneParam param) => param switch
    {
        ZoneParam.FrontM or ZoneParam.FrontSpreadM or ZoneParam.SkewDeg or ZoneParam.BearingDeg => 0.1,
        ZoneParam.People or ZoneParam.Cars or ZoneParam.Buildings or ZoneParam.Spokes or ZoneParam.Sector or ZoneParam.Inside
            or ZoneParam.Strict or ZoneParam.FootprintM2 or ZoneParam.BehindM2 => 1.0,
        _ => 0.001,
    };

    /// <summary>A value as the whole number of steps it is written as.</summary>
    public static long Steps(ZoneParam param, float value) => checked((long)Math.Round(value / StepOf(param)));

    /// <summary>A value held to its step, as it reads back.</summary>
    public static float Held(ZoneParam param, float value) => Exact(param) ? value : (float)(Steps(param, value) * StepOf(param));
}
