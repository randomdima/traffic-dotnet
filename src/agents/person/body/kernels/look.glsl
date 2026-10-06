// PersonCatalog's cell picking on the device: which facing row a heading is drawn from and which walk column a
// distance walked has reached (PersonCatalog.FacingRow, WalkColumn). The quotients are rounded.glsl's, since a last
// bit is a different row at an octant's edge; rounded.glsl is included first and the kernel enables doubles.

// PersonCatalog.FacingRows and WalkColumns: every sheet is eight facings of an eight-frame cycle.
const int PersonFacingRows = 8;
const int PersonWalkColumns = 8;

// PersonCatalog.FacingRow. fromRad is the host's −π/2 and stepRad its π/4, both as the host's floats.
int personFacingRow(float headingRad, float fromRad, float stepRad)
{
    precise float turnRad = fromRad - headingRad;
    int octant = int(roundEven(roundedQuotient(turnRad, stepRad)));
    return octant & (PersonFacingRows - 1);
}

// PersonCatalog.WalkColumn.
int personWalkColumn(float distanceWalkedM, float strideCycleM)
{
    if (strideCycleM <= 0.0) return 0;

    float cycles = roundedQuotient(distanceWalkedM, strideCycleM);
    precise float into = (cycles - floor(cycles)) * float(PersonWalkColumns);
    return clamp(int(floor(into)), 0, PersonWalkColumns - 1);
}
