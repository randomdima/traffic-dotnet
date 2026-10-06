// How much of a car's line there is (DrivenLine), and whether it ends where the leg does — one row a car, beside the
// line's own arrays in the fleet's layout (line.glsl).

struct CarLineRow
{
    uint arcCount;
    uint laneCount;
    float lengthM;
    uint finalApproach;
};
