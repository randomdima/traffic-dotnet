// The pavement's ways as rows on the device (WalkingNetwork), field for field with app/devicetick/DeviceWalkers' WalkWayRow.

// WalkingNetwork.NoLane.
const int WalkNoLane = -1;

// One way of the pavement: its line's run of arcs, its length, the width of its ground, for a corner how far short of
// the arriving lane's end it leaves it and how far into the lane it leaves for it arrives, and for a lane the zebra it
// is the paint of, or -1.
struct WalkWayRow
{
    uint arcFirst;
    uint arcEnd;
    float lengthM;
    float widthM;
    float joinFromM;
    float joinToM;
    int zebra;
    uint pad;
};
