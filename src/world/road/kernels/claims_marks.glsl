// The marks as rows on the device (WayCrossings), field for field with CrossedSection: a way's marks are the run
// MARK_AT[way] .. MARK_AT[way + 1] of the sections, nearest first.

// One mark: the section of another way a stretch of this one's ribbon lies over, filed under this way.
struct WayMark
{
    int onWay;
    float fromM;
    float toM;
    float mineFromM;
    float mineToM;
};
