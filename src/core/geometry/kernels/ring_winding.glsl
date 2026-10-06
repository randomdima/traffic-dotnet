// How many times a lattice's rings wind round a point (RingSides.WindingAt), off the buffers the including kernel
// names RING_CELLS, RING_FILING and RING_PIECES and declares before this is included. The pieces' arithmetic is
// ringsides.glsl's.

int ringWinding(RingLattice lattice, vec2 pointM)
{
    if (lattice.width == 0 || lattice.height == 0) return 0;

    double x = double(pointM.x);
    double y = double(pointM.y);
    precise float scaledX = pointM.x * lattice.perCellM;
    precise float scaledY = pointM.y * lattice.perCellM;
    int column = int(floor(scaledX));
    int row = int(floor(scaledY));

    // The corner placed the way the lattice laid it; a point a rounding short of it is the cell's below.
    precise double cornerX = double(column) * lattice.cellM;
    precise double cornerY = double(row) * lattice.cellM;
    if (x < cornerX)
    {
        column--;
        cornerX = double(column) * lattice.cellM;
    }

    if (y < cornerY)
    {
        row--;
        cornerY = double(row) * lattice.cellM;
    }

    if (uint(column - lattice.fromX) >= uint(lattice.width) || uint(row - lattice.fromY) >= uint(lattice.height)) return 0;

    uint cell = uint((row - lattice.fromY) * lattice.width + (column - lattice.fromX));
    int word = RING_CELLS[lattice.cellBase + cell];
    int winding = (word << (32 - RingWindingBits)) >> (32 - RingWindingBits);
    uint first = uint(word) >> RingWindingBits;
    uint last = uint(RING_CELLS[lattice.cellBase + cell + 1u]) >> RingWindingBits;
    for (uint entry = first; entry < last; entry++)
    {
        RingPiece piece = RING_PIECES[lattice.pieceBase + uint(RING_FILING[lattice.filingBase + entry])];
        if ((double(piece.fromM.x) > cornerX) != (double(piece.toM.x) > cornerX)) winding += ringUp(piece, cornerX, cornerY, y);
        if ((double(piece.fromM.y) > y) != (double(piece.toM.y) > y)) winding -= ringAcross(piece, y, cornerX, x);
    }

    return winding;
}
