// RingSides on the device: how many times a set of closed rings winds round a point, off the same lattice — a
// lookup almost everywhere, and in a cell a ring passes through a walk up the cell's left edge and across, counting
// the pieces it crosses (RingSides.WindingAt). In doubles, as on the host: a corner is a cell number times the cell,
// and a crossing on a bend kilometres round is a difference of squares a float would cancel.
//
// The lattice and its pieces, and a piece's own arithmetic; the walk over a lattice's buffers is ring_winding.glsl's.
// The including kernel enables 64-bit floats.

struct RingLattice
{
    int fromX;
    int fromY;
    int width;
    int height;
    float perCellM;
    uint cellBase;
    uint filingBase;
    uint pieceBase;
    double cellM;
    double pad;
};

// A piece runs one way in x and one way in y; an arc is held by its start and the way back to its centre
// (RingSides.Piece).
struct RingPiece
{
    vec2 fromM;
    vec2 toM;
    vec2 radialM;
    vec2 slope;
    int sideX;
    int sideY;
    int bends;
    int pad;
};

const int RingWindingBits = 6;

double ringYAt(RingPiece piece, double x)
{
    if (x == double(piece.fromM.x)) return double(piece.fromM.y);
    if (x == double(piece.toM.x)) return double(piece.toM.y);

    precise double acrossM = x - double(piece.fromM.x);
    if (piece.bends == 0)
    {
        precise double y = double(piece.fromM.y) + (acrossM * double(piece.slope.y));
        return y;
    }

    double outM = double(piece.radialM.y);
    precise double shortfall = acrossM * ((2.0lf * double(piece.radialM.x)) + acrossM);
    precise double sum = outM + (double(piece.sideY) * sqrt(max(0.0lf, (outM * outM) - shortfall)));
    if (sum == 0.0lf) return double(piece.fromM.y);

    precise double y = double(piece.fromM.y) - (shortfall / sum);
    return y;
}

double ringXAt(RingPiece piece, double y)
{
    if (y == double(piece.fromM.y)) return double(piece.fromM.x);
    if (y == double(piece.toM.y)) return double(piece.toM.x);

    precise double upM = y - double(piece.fromM.y);
    if (piece.bends == 0)
    {
        precise double x = double(piece.fromM.x) + (upM * double(piece.slope.x));
        return x;
    }

    double outM = double(piece.radialM.x);
    precise double shortfall = upM * ((2.0lf * double(piece.radialM.y)) + upM);
    precise double sum = outM + (double(piece.sideX) * sqrt(max(0.0lf, (outM * outM) - shortfall)));
    if (sum == 0.0lf) return double(piece.fromM.x);

    precise double x = double(piece.fromM.x) - (shortfall / sum);
    return x;
}

int ringUp(RingPiece piece, double x, double fromY, double toY)
{
    double y = ringYAt(piece, x);
    bool climbs = piece.toM.y != piece.fromM.y && (piece.toM.y > piece.fromM.y) == (piece.toM.x > piece.fromM.x);
    if (y < fromY || (y == fromY && !climbs)) return 0;
    if (y > toY || (y == toY && climbs)) return 0;

    return piece.toM.x > piece.fromM.x ? 1 : -1;
}

int ringAcross(RingPiece piece, double y, double fromX, double toX)
{
    double x = ringXAt(piece, y);
    if (x <= fromX || x > toX) return 0;

    return piece.toM.y > piece.fromM.y ? 1 : -1;
}

