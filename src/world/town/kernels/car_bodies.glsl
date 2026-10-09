// Every car's body laid onto the ways under it, on the device (TownWorld.LayTheCarsBody, DrivingGround.IsOnItsLine):
// the atlas's ways under its four wheels (atlas.glsl), and on each whether the car — or the truck it hangs from (EVA-5)
// — is travelling that way, how fast, where its line goes next, and whether it is at rest — as body rows in the claims'
// layout (claims_rows.glsl), in the order the atlas found them.
//
// The including kernel includes rounded.glsl, atlas.glsl, claims_rows.glsl, car_line.glsl, road_joins.glsl and
// line_onward.glsl, declares CAR_LINES (CarLineRow[]) and BODY_ROWS (ClaimRow[], written straight from a car's first row
// rather than through an array of the thread's own, which the compiler keeps in scratch memory), and supplies
// BODY_FIGURES.groundChannel — what every way is on
// where the atlas keeps no channels (RibbonAtlas.ChannelsOf) — and BODY_FIGURES.aroundAWheelM
// (RoadFigures.HeldAroundAWheelM).

#include "world/town/kernels/car_body_row.glsl"

// Where a car's four wheels stand, in the tyre model's order (TyreModel.WheelPointsM).
void bodyWheelsOf(CarBodyRow car, out vec2 wheelsM[4])
{
    vec2 right = vec2(-car.forward.y, car.forward.x);
    for (int wheel = 0; wheel < 4; wheel++)
    {
        vec2 atBody = vec2(wheel < 2 ? car.frontAxleM : car.rearAxleM, wheel % 2 == 0 ? car.halfTrackM : -car.halfTrackM);
        precise vec2 at = car.centreM + (car.forward * atBody.x) + (right * atBody.y);
        wheelsM[wheel] = at;
    }
}

// One car's body on every way the atlas finds under its wheels (TownWorld.LayTheCarsBody), into BODY_ROWS from `first`;
// how many, the channels of the ways it stands over (its own where it stands over none), and which of its wheels stand
// on one (CarFleet.WheelsOnAWay).
int bodyLayTheCar(CarBodyRow car, uint first, out uint channels, out uint dropped, out uint wheelsOnAWay)
{
    dropped = 0u;
    vec2 wheelsM[4];
    bodyWheelsOf(car, wheelsM);
    int count = atlasUnderWheels(wheelsM, BODY_FIGURES.aroundAWheelM, car.channels, dropped, wheelsOnAWay);

    bool travelling = (car.flags & BodyTravelling) != 0u;
    bool still = (car.flags & BodyStill) != 0u;
    bool blocked = still && (car.flags & BodyBlocked) != 0u;
    uint stillFlag = still ? ClaimStill : 0u;
    channels = 0u;
    for (int at = 0; at < count; at++)
    {
        AtlasCover found = atlasFound[at];
        uint way = uint(found.way);
        channels |= ATLAS_FIGURES.hasChannels != 0u ? atlasChannelsOf(int(way)) : BODY_FIGURES.groundChannel;
        int onward = LineNoWay;
        bool onItsLine = travelling && lineIsOnItsLine(
            uint(car.occupant), CAR_LINES[car.occupant].laneCount, (car.flags & BodyRunsOnPastItself) != 0u, way, onward);
        if (blocked) onward = LineNoWay;

        BODY_ROWS[first + uint(at)] = ClaimRow(
            found.fromM, found.toM, onItsLine ? car.alongMps : 0.0, 0.0, 0.0, -atlasInfinity(), found.fromM,
            car.occupant, -1, onward, way, RungHard | (onItsLine ? ClaimOnItsLine : 0u) | stillFlag);
    }

    if (channels == 0u) channels = car.channels;
    return count;
}
