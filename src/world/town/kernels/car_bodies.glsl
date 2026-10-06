// Every car's body laid onto the ways under it, on the device (TownWorld.LayTheCarsBody, DrivingGround.IsOnItsLine):
// the atlas's ways under its box (atlas.glsl), and on each whether the car — or the truck it hangs from (EVA-5) — is
// travelling that way, how fast, where its line goes next, and whether it is at rest — as body rows in the claims'
// layout (claims_rows.glsl), in the order the atlas found them.
//
// The including kernel includes rounded.glsl, atlas.glsl, claims_rows.glsl, car_line.glsl, road_joins.glsl and
// line_onward.glsl, declares CAR_LINES (CarLineRow[]), and supplies BODY_FIGURES.groundChannel — what every way is on
// where the atlas keeps no channels (RibbonAtlas.ChannelsOf).

// What the laying reads of one car (CarFleet): its box and channels, and of the car it is laid as (TownWorld.LaidAs) —
// itself, or the truck it hangs from — how fast it goes along its line and what the town says of it this rebuild.
struct CarBodyRow
{
    vec2 centreM;
    vec2 forward;
    float halfLengthM;
    float halfWidthM;
    uint channels;
    int occupant;
    float alongMps;
    uint flags;
    float pad0;
    float pad1;
};

// CarBodyRow.flags: of the car it is laid as, whether it drives its route's line (TownWorld.IsUnderWay) and whether
// that line runs on past its leading edge (DrivingGround.RunsOnPastItself); of the car itself, whether it is at rest,
// and whether it is blocked there (DriveContext.Blocked).
const uint BodyTravelling = 1u;
const uint BodyRunsOnPastItself = 2u;
const uint BodyStill = 4u;
const uint BodyBlocked = 8u;

// One car's body on every way the atlas finds under its box (TownWorld.LayTheCarsBody), into rows[]; how many, and the
// channels of the ways it stands over (its own where it stands over none).
int bodyLayTheCar(CarBodyRow car, out ClaimRow rows[AtlasMostWaysUnderABody], out uint channels, out uint dropped)
{
    AtlasCover found[AtlasMostWaysUnderABody];
    dropped = 0u;
    int count = atlasUnderBox(car.centreM, car.forward, car.halfLengthM, car.halfWidthM, car.channels, found, dropped);

    bool travelling = (car.flags & BodyTravelling) != 0u;
    bool still = (car.flags & BodyStill) != 0u;
    bool blocked = still && (car.flags & BodyBlocked) != 0u;
    uint stillFlag = still ? ClaimStill : 0u;
    channels = 0u;
    for (int at = 0; at < count; at++)
    {
        uint way = uint(found[at].way);
        channels |= ATLAS_FIGURES.hasChannels != 0u ? atlasChannelsOf(int(way)) : BODY_FIGURES.groundChannel;
        int onward = LineNoWay;
        bool onItsLine = travelling && lineIsOnItsLine(
            uint(car.occupant), CAR_LINES[car.occupant].laneCount, (car.flags & BodyRunsOnPastItself) != 0u, way, onward);
        if (blocked) onward = LineNoWay;

        rows[at] = ClaimRow(
            found[at].fromM, found[at].toM, onItsLine ? car.alongMps : 0.0, 0.0, 0.0, -atlasInfinity(), found[at].fromM,
            car.occupant, -1, onward, way, RungHard | (onItsLine ? ClaimOnItsLine : 0u) | stillFlag);
    }

    if (channels == 0u) channels = car.channels;
    return count;
}
