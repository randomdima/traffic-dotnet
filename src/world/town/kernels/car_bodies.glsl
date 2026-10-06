// Every car's body laid onto the ways under it, on the device (TownWorld.LayTheCarsBody, DrivingGround.IsOnItsLine):
// the atlas's ways under its box (atlas.glsl), and on each whether the car — or the truck it hangs from (EVA-5) — is
// travelling that way, how fast, where its line goes next, and whether it is at rest — as body rows in the claims'
// layout (claims_rows.glsl), in the order the atlas found them.
//
// The including kernel includes rounded.glsl, atlas.glsl, claims_rows.glsl, car_line.glsl and road_joins.glsl, declares
// LANE_CHAIN (int[], BODY_FIGURES.mostLanes a car) and CAR_LINES (CarLineRow[]), and supplies BODY_FIGURES.firstConnector, mostLanes and groundChannel — what every way is on where the atlas
// keeps no channels (RibbonAtlas.ChannelsOf).

// LaneOccupancy.NoWay and RunsOn.
const int BodyNoWay = -1;
const int BodyRunsOn = -2;

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

// Whether one of the town's ways is one a car's own line runs over, and which way the line takes after it
// (DrivingGround.IsOnItsLine).
bool bodyIsOnItsLine(int car, bool runsOnPastItself, uint way, out int onward)
{
    onward = BodyNoWay;
    uint lanes = CAR_LINES[car].laneCount;
    uint slots = uint(car) * BODY_FIGURES.mostLanes;
    for (uint index = 0u; index < lanes; index++)
    {
        int lane = LANE_CHAIN[slots + index];
        int join = index + 1u == lanes ? -1 : roadConnectorBetween(lane, LANE_CHAIN[slots + index + 1u]);
        int joinWay = join < 0 ? BodyNoWay : int(BODY_FIGURES.firstConnector) + join;
        if (uint(lane) == way)
        {
            onward = joinWay != BodyNoWay ? joinWay : runsOnPastItself ? BodyRunsOn : BodyNoWay;
            return true;
        }

        if (joinWay != BodyNoWay && uint(joinWay) == way)
        {
            onward = LANE_CHAIN[slots + index + 1u];
            return true;
        }
    }

    return false;
}

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
        int onward = BodyNoWay;
        bool onItsLine = travelling && bodyIsOnItsLine(car.occupant, (car.flags & BodyRunsOnPastItself) != 0u, way, onward);
        if (blocked) onward = BodyNoWay;

        rows[at] = ClaimRow(
            found[at].fromM, found[at].toM, onItsLine ? car.alongMps : 0.0, 0.0, 0.0, -atlasInfinity(), found[at].fromM,
            car.occupant, -1, onward, way, RungHard | (onItsLine ? ClaimOnItsLine : 0u) | stillFlag);
    }

    if (channels == 0u) channels = car.channels;
    return count;
}
