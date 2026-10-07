// Every way's two lists laid from the rows (LaneOccupancy.Link): the rows counted onto their ways, the counts scanned
// into where each way's run starts (bodies first, every way's, then planned pieces), every row filed into its way's run,
// and each run sorted nearest first and, from one metre, laid first first (LaneOccupancy.Precedes) — which is what the
// host's lists are, whatever order the rows were filed in. The including kernel has included claims_rows.glsl and
// claims_tally.glsl and declared its push block as `figures` (wayCount) first; how many rows there are is the tally's.

layout(std430, binding = 0) readonly buffer Rows { ClaimRow rows[]; };
layout(std430, binding = 1) buffer Counts { uint counts[]; };
layout(std430, binding = 2) buffer Cursor { uint cursor[]; };
layout(std430, binding = 3) buffer Order { uint order[]; };
layout(std430, binding = 4) writeonly buffer BodyRuns { uvec2 bodyRuns[]; };
layout(std430, binding = 5) writeonly buffer PlannedRuns { uvec2 plannedRuns[]; };
layout(std430, binding = 6) readonly buffer Starts { uint starts[]; };
layout(std430, binding = 7) readonly buffer Tally { uint tally[]; };

// Which of the two lists a row is filed in, as where its way's count stands: bodies first, planned pieces after.
uint claimsListAt(ClaimRow row)
{
    return (claimHasBody(row) ? 0u : figures.wayCount) + row.way;
}

// Whether one row stands before another in a way's list: nearest first, then laid first (LaneOccupancy.Precedes).
bool claimsPrecedes(uint one, uint other)
{
    float oneM = rows[one].fromM;
    float otherM = rows[other].fromM;
    return oneM != otherM ? oneM < otherM : one < other;
}

// One run of the order sorted in place: a way's list is a handful, so an insertion sort a thread.
void claimsSortRun(uint start, uint count)
{
    for (uint at = start + 1u; at < start + count; at++)
    {
        uint row = order[at];
        uint into = at;
        while (into > start && claimsPrecedes(row, order[into - 1u]))
        {
            order[into] = order[into - 1u];
            into--;
        }

        order[into] = row;
    }
}
