// Ground an action swept once and committed to, on the device (SweptGround): the ways under a body stood at stations
// along a path, one holder's kept as runs at SWEPT_RUNS[first .. first + count).
//
// The including kernel includes swept_rows.glsl, declares SWEPT_RUNS (SweptRun[]) and includes atlas.glsl first.

// The ways under the body at one more station, atM along the path — the first underCount of atlasFound, as the read
// there left them: each grown into the run its way had at the station before while that run spans less than spanM, and
// a run of its own otherwise — false where there is no room left for one (SweptGround.Station). stationAtM is the
// holder's last station, minus infinity before the first.
bool sweptStation(uint first, inout uint count, uint room, inout float stationAtM, float atM, float spanM, int underCount)
{
    float previousAtM = stationAtM;
    for (int cover = 0; cover < underCount; cover++)
    {
        AtlasCover under = atlasFound[cover];
        int open = -1;
        for (int at = int(count) - 1; at >= 0; at--)
        {
            SweptRun run = SWEPT_RUNS[first + uint(at)];
            precise float spannedM = atM - run.firstAtM;
            if (spannedM > spanM) break;
            if (run.way != under.way || run.lastAtM != previousAtM) continue;

            open = at;
            break;
        }

        if (open >= 0)
        {
            SweptRun run = SWEPT_RUNS[first + uint(open)];
            SWEPT_RUNS[first + uint(open)] = SweptRun(
                run.way, min(run.fromM, under.fromM), max(run.toM, under.toM), run.firstAtM, atM);
            continue;
        }

        if (count == room) return false;

        SWEPT_RUNS[first + count] = SweptRun(under.way, under.fromM, under.toM, atM, atM);
        count++;
    }

    stationAtM = atM;
    return true;
}
