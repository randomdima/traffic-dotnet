// RibbonAtlas.Find, Foot and PointsOf on the device: the lattice points one piece of a way's line reads, each measured as
// the host measures it, and which of a way's pieces keeps a point two of them read — line for line, `precise` wherever the
// host rounds, every quotient and root rounded.glsl's and the bearing trig.glsl's, so each point is filed as the host files
// it (RibbonAtlas.Filing hands the pieces over, AtlasFiling).
//
// The including kernel defines FILING_FIGURES, whose fields are straightCurvature, leastFromCentreM, tau, perCellM and
// cellM, and FILING_PIECES_BINDING, where the pieces bind; and includes core/geometry/kernels/trig.glsl first.

// One piece of a way's line, as RibbonAtlas.Filing laid it (FilingPiece, row for row).
struct FilingPiece
{
    vec2 startM;
    vec2 startUnit;
    float lengthM;
    float curvature;
    float wayFromM;
    float halfM;
    int way;
    int fromRow;
    int toRow;
    int fromColumn;
    int toColumn;
    int firstOfWay;
    int pastOfWay;
    float wayLengthM;
};

layout(std430, binding = FILING_PIECES_BINDING) readonly buffer Pieces { FilingPiece pieces[]; };

// One reading of a lattice point (RibbonAtlas.Sample, less the point): how far along the way, how far inside its edges,
// and whether its foot fell on the piece that read it.
struct FilingSample
{
    float alongM;
    float marginM;
    bool onItsPiece;
};

bool filingIsStraight(FilingPiece piece) { return abs(piece.curvature) < FILING_FIGURES.straightCurvature; }

float filingCross(vec2 a, vec2 b)
{
    precise float c = a.x * b.y - a.y * b.x;
    return c;
}

float filingDot(vec2 a, vec2 b)
{
    precise float d = a.x * b.x + a.y * b.y;
    return d;
}

// GridLevel.MiddleM at the points' level.
float filingMiddleM(int cell)
{
    precise float middleM = (float(cell) + 0.5) * FILING_FIGURES.cellM;
    return middleM;
}

int filingFirstMiddleFrom(float atM)
{
    precise float cells = atM * FILING_FIGURES.perCellM - 0.5;
    return int(ceil(cells));
}

int filingLastMiddleTo(float atM)
{
    precise float cells = atM * FILING_FIGURES.perCellM - 0.5;
    return int(floor(cells));
}

// Where a point's foot on one piece's own circle or line stands, measured from the piece's start and unclamped, and how
// far off it the point is (RibbonAtlas.Foot).
bool filingFoot(FilingPiece piece, vec2 pointM, out float alongM, out float offM)
{
    vec2 along = piece.startUnit;
    if (filingIsStraight(piece))
    {
        precise vec2 toPoint = pointM - piece.startM;
        alongM = filingDot(toPoint, along);
        offM = abs(filingCross(along, toPoint));
        return true;
    }

    float radiusM = roundedQuotient(1.0, piece.curvature);
    precise vec2 centreM = piece.startM + radiusM * vec2(-along.y, along.x);
    precise vec2 fromCentre = piece.startM - centreM;
    precise vec2 toPointFromCentre = pointM - centreM;
    float distanceM = roundedSqrt(filingDot(toPointFromCentre, toPointFromCentre));
    if (distanceM < FILING_FIGURES.leastFromCentreM)
    {
        alongM = 0.0;
        offM = 0.0;
        return false;
    }

    float turnRad = trigAtan2(filingCross(fromCentre, toPointFromCentre), filingDot(fromCentre, toPointFromCentre));
    alongM = roundedQuotient(turnRad, piece.curvature);

    // The far half of the circle is behind the start as easily as past the end.
    float roundM = roundedQuotient(FILING_FIGURES.tau, abs(piece.curvature));
    precise float roundOn = alongM + roundM;
    precise float halfRound = roundM * 0.5;
    precise float roundLimit = piece.lengthM + halfRound;
    if (alongM < 0.0 && roundOn <= roundLimit) alongM = roundOn;

    precise float off = abs(distanceM - abs(radiusM));
    offM = off;
    return true;
}

float filingClamp(float value, float least, float most) { return value < least ? least : (value > most ? most : value); }

// Whether one piece reads a lattice point at all, and its reading (Find's tests, past the box the point is asked from).
bool filingReads(FilingPiece piece, int index, vec2 pointM, out FilingSample taken)
{
    taken.alongM = 0.0;
    taken.marginM = 0.0;
    taken.onItsPiece = false;

    float onArcM;
    float offM;
    if (!filingFoot(piece, pointM, onArcM, offM)) return false;

    float behindM = index == piece.firstOfWay ? 0.0 : piece.halfM;
    float pastM = index == piece.pastOfWay - 1 ? 0.0 : piece.halfM;
    precise float endM = piece.lengthM + pastM;
    if (onArcM < -behindM || onArcM > endM || offM >= piece.halfM) return false;

    precise float onWayM = piece.wayFromM + onArcM;
    if (onWayM < 0.0 || onWayM > piece.wayLengthM) return false;

    precise float alongM = piece.wayFromM + filingClamp(onArcM, 0.0, piece.lengthM);
    precise float marginM = piece.halfM - offM;
    taken.alongM = alongM;
    taken.marginM = marginM;
    taken.onItsPiece = onArcM >= 0.0 && onArcM <= piece.lengthM;
    return true;
}

// RibbonAtlas.Deeper: the reading kept over another.
bool filingDeeper(FilingSample one, FilingSample other)
{
    if (one.onItsPiece != other.onItsPiece) return one.onItsPiece;
    if (one.marginM != other.marginM) return one.marginM > other.marginM;
    return one.alongM < other.alongM;
}

bool filingSame(FilingSample one, FilingSample other)
{
    return one.onItsPiece == other.onItsPiece && one.marginM == other.marginM && one.alongM == other.alongM;
}

// Whether the point a piece read is the reading its way keeps (PointsOf): no other piece of the way whose box holds the
// point reads it deeper — or as deep and earlier, where the two readings are the same and either is the one kept.
bool filingKept(int index, FilingPiece piece, int column, int row, vec2 pointM, FilingSample taken)
{
    for (int other = piece.firstOfWay; other < piece.pastOfWay; other++)
    {
        if (other == index) continue;

        FilingPiece rival = pieces[other];
        if (row < rival.fromRow || row > rival.toRow || column < rival.fromColumn || column > rival.toColumn) continue;

        FilingSample reading;
        if (!filingReads(rival, other, pointM, reading)) continue;
        if (filingDeeper(reading, taken) || (other < index && filingSame(reading, taken))) return false;
    }

    return true;
}

// Where along one row a piece's band can reach, as at most two runs of metres across the town: never narrower than the
// host's (RibbonAtlas.RunsAcross), which is in doubles and never narrower than the band as Foot reads it — so a column
// past the host's run is a point no test here keeps, and the points read are the host's. Wider by `slackM` more and, about
// an arc's centre, by a share of its radius, which a float's rounding of the centre and of the row cannot get past.
int filingRunsAcross(FilingPiece piece, int index, float yM, out vec2 one, out vec2 other)
{
    one = vec2(1.0, 0.0);
    other = vec2(1.0, 0.0);
    float behindM = index == piece.firstOfWay ? 0.0 : piece.halfM;
    float pastM = index == piece.pastOfWay - 1 ? 0.0 : piece.halfM;
    float slackM = 2.0 * FILING_FIGURES.cellM;
    vec2 along = piece.startUnit;
    vec2 across = vec2(-along.y, along.x);

    if (filingIsStraight(piece))
    {
        float fromM = -behindM;
        float toM = piece.lengthM + pastM;
        vec2 corners[4] = vec2[](
            piece.startM + along * fromM - across * piece.halfM, piece.startM + along * toM - across * piece.halfM,
            piece.startM + along * toM + across * piece.halfM, piece.startM + along * fromM + across * piece.halfM);

        float leastX = 3.0e38;
        float mostX = -3.0e38;
        for (int corner = 0; corner < 4; corner++)
        {
            vec2 a = corners[corner];
            vec2 b = corners[(corner + 1) % 4];
            if ((a.y - yM) * (b.y - yM) > 0.0 && abs(a.y - yM) > slackM && abs(b.y - yM) > slackM) continue;

            if (abs(b.y - a.y) <= slackM)
            {
                leastX = min(leastX, min(a.x, b.x));
                mostX = max(mostX, max(a.x, b.x));
                continue;
            }

            float t = clamp((yM - a.y) / (b.y - a.y), 0.0, 1.0);
            float xM = a.x + t * (b.x - a.x);
            leastX = min(leastX, xM);
            mostX = max(mostX, xM);
        }

        if (leastX > mostX) return 0;

        one = vec2(leastX - slackM, mostX + slackM);
        return 1;
    }

    float radiusM = 1.0 / abs(piece.curvature);
    vec2 centreM = piece.startM + (1.0 / piece.curvature) * across;
    float roundingM = slackM + radiusM * 1.0e-5;
    float outerM = radiusM + piece.halfM + roundingM;
    float innerM = radiusM - piece.halfM - roundingM;
    float offRowM = abs(yM - centreM.y);
    if (offRowM >= outerM) return 0;

    float outsideM = sqrt((outerM - offRowM) * (outerM + offRowM)) + slackM;
    float insideM = innerM > offRowM ? sqrt((innerM - offRowM) * (innerM + offRowM)) - slackM : 0.0;
    if (insideM <= 0.0)
    {
        one = vec2(centreM.x - outsideM, centreM.x + outsideM);
        return 1;
    }

    one = vec2(centreM.x - outsideM, centreM.x - insideM);
    other = vec2(centreM.x + insideM, centreM.x + outsideM);
    return 2;
}
