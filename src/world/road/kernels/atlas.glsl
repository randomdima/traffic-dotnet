// RibbonAtlas.UnderBox and UnderDisc on the device: every way a body stands over, and the stretch of each, read off the
// atlas's own tables (AtlasTable, laid by app/devicetick/DeviceAtlas) — the same ways in the same order over the same
// stretches the host finds (TER-4c.4). Line for line with RibbonAtlas.Read, Grow and Widened and the Box and Disc
// bodies, `precise` wherever the host rounds; the quotients and roots are rounded.glsl's, since which lattice points a
// body takes in, and which bands it reaches over, is decided on their last bit.
//
// The including kernel defines ATLAS_BINDING (the tables bind from there on, AtlasBindings of them), supplies the figures
// as ATLAS_FIGURES' fields — cellsFromX, cellsFromY, cellsWidth, cellsHeight, perCellM, cellM, shift, mask, startShift,
// rowStartsShift, startsPerCell, metresPerOutside, alongPerMetre, hasChannels — enables 64-bit floats and includes
// core/geometry/kernels/rounded.glsl first.

// RibbonAtlas.MostWaysUnderABody: a body's room, past which a way is dropped and counted.
const int AtlasMostWaysUnderABody = 48;

// One way under a body and the stretch of it the body stands over (WayCover, row for row). While a read gathers, the
// stretch is in the tenths the atlas files a metre in (RibbonAtlas.Found).
struct AtlasCover
{
    int way;
    float fromM;
    float toM;
};

layout(std430, binding = ATLAS_BINDING + 0) readonly buffer AtlasCellSlots { int atlasCellSlot[]; };
layout(std430, binding = ATLAS_BINDING + 1) readonly buffer AtlasSlotFirsts { int atlasSlotFirst[]; };
layout(std430, binding = ATLAS_BINDING + 2) readonly buffer AtlasPointStarts { uint atlasPointStarts[]; };
layout(std430, binding = ATLAS_BINDING + 3) readonly buffer AtlasEntries { uvec2 atlasEntries[]; };
layout(std430, binding = ATLAS_BINDING + 4) readonly buffer AtlasWayLengths { float atlasLengthM[]; };
layout(std430, binding = ATLAS_BINDING + 5) readonly buffer AtlasWayChannels { uint atlasWayChannels[]; };

// How many bindings the tables take, for whatever a kernel binds after them.
const int AtlasBindings = 6;

// A point start is a ushort, two to a word.
uint atlasPointStart(int at) { return (atlasPointStarts[at >> 1] >> ((at & 1) << 4)) & 0xFFFFu; }

// A way's channels are a byte, four to a word.
uint atlasChannelsOf(int way) { return (atlasWayChannels[way >> 2] >> ((way & 3) << 3)) & 0xFFu; }

float atlasInfinity() { return uintBitsToFloat(0x7F800000u); }

// GridLevel.FirstMiddleFrom, LastMiddleTo and MiddleM at the points' level.
int atlasFirstMiddleFrom(float atM)
{
    precise float cells = atM * ATLAS_FIGURES.perCellM - 0.5;
    return int(ceil(cells));
}

int atlasLastMiddleTo(float atM)
{
    precise float cells = atM * ATLAS_FIGURES.perCellM - 0.5;
    return int(floor(cells));
}

float atlasMiddleM(int cell)
{
    precise float middleM = (float(cell) + 0.5) * ATLAS_FIGURES.cellM;
    return middleM;
}

float atlasDot(vec2 a, vec2 b)
{
    precise float d = a.x * b.x + a.y * b.y;
    return d;
}

// A car's box (RibbonAtlas.Box) or a walker's disc (RibbonAtlas.Disc), whose radius is the half length.
struct AtlasBody
{
    vec2 centreM;
    vec2 forward;
    vec2 right;
    float halfLengthM;
    float halfWidthM;
    vec2 a;
    vec2 b;
    vec2 c;
    vec2 d;
    bool disc;
};

AtlasBody atlasBox(vec2 centreM, vec2 forward, float halfLengthM, float halfWidthM)
{
    AtlasBody body;
    body.centreM = centreM;
    body.forward = forward;
    body.right = vec2(-forward.y, forward.x);
    body.halfLengthM = halfLengthM;
    body.halfWidthM = halfWidthM;
    body.disc = false;

    precise vec2 alongM = forward * halfLengthM;
    precise vec2 acrossM = body.right * halfWidthM;
    precise vec2 a = centreM + alongM + acrossM;
    precise vec2 b = centreM + alongM - acrossM;
    precise vec2 c = centreM - alongM - acrossM;
    precise vec2 d = centreM - alongM + acrossM;
    body.a = a;
    body.b = b;
    body.c = c;
    body.d = d;
    return body;
}

AtlasBody atlasDisc(vec2 centreM, float radiusM)
{
    AtlasBody body;
    body.centreM = centreM;
    body.forward = vec2(0.0);
    body.right = vec2(0.0);
    body.halfLengthM = radiusM;
    body.halfWidthM = 0.0;
    body.a = vec2(0.0);
    body.b = vec2(0.0);
    body.c = vec2(0.0);
    body.d = vec2(0.0);
    body.disc = true;
    return body;
}

float atlasLeastY(AtlasBody body)
{
    precise float leastY = body.centreM.y - body.halfLengthM;
    return body.disc ? leastY : min(min(body.a.y, body.b.y), min(body.c.y, body.d.y));
}

float atlasMostY(AtlasBody body)
{
    precise float mostY = body.centreM.y + body.halfLengthM;
    return body.disc ? mostY : max(max(body.a.y, body.b.y), max(body.c.y, body.d.y));
}

// How far inside the body a point lies (IBody.DepthM).
float atlasDepthM(AtlasBody body, vec2 pointM)
{
    precise vec2 offM = pointM - body.centreM;
    if (body.disc)
    {
        precise float depthM = body.halfLengthM - roundedSqrt(atlasDot(offM, offM));
        return depthM;
    }

    precise float alongM = body.halfLengthM - abs(atlasDot(offM, body.forward));
    precise float acrossM = body.halfWidthM - abs(atlasDot(offM, body.right));
    return min(alongM, acrossM);
}

// Where the line meets one edge of a box, added to the span (Box.Cross).
void atlasCross(vec2 one, vec2 other, float y, inout float fromX, inout float toX)
{
    if ((y < one.y && y < other.y) || (y > one.y && y > other.y)) return;

    if (one.y == other.y)
    {
        fromX = min(fromX, min(one.x, other.x));
        toX = max(toX, max(one.x, other.x));
        return;
    }

    precise float spanM = (y - one.y) * (other.x - one.x);
    precise float x = one.x + roundedQuotient(spanM, other.y - one.y);
    fromX = min(fromX, x);
    toX = max(toX, x);
}

// Where one horizontal line crosses the body, as the span of x inside it (IBody.Across).
bool atlasAcross(AtlasBody body, float y, out float fromX, out float toX)
{
    if (body.disc)
    {
        precise float offY = y - body.centreM.y;
        precise float halfSq = body.halfLengthM * body.halfLengthM - offY * offY;
        if (halfSq < 0.0)
        {
            fromX = 0.0;
            toX = 0.0;
            return false;
        }

        float halfM = roundedSqrt(halfSq);
        precise float leftM = body.centreM.x - halfM;
        precise float rightM = body.centreM.x + halfM;
        fromX = leftM;
        toX = rightM;
        return true;
    }

    fromX = atlasInfinity();
    toX = -atlasInfinity();
    atlasCross(body.a, body.b, y, fromX, toX);
    atlasCross(body.b, body.c, y, fromX, toX);
    atlasCross(body.c, body.d, y, fromX, toX);
    atlasCross(body.d, body.a, y, fromX, toX);
    return toX >= fromX;
}

// The ways under a body's points, a row at a time, each point counted only where the body reaches further past it than
// it stands outside that way's band (RibbonAtlas.Read, with Grow); the stretches are left in tenths. A way past the room
// is counted in `dropped`.
int atlasRead(AtlasBody body, uint channels, inout AtlasCover found[AtlasMostWaysUnderABody], inout uint dropped)
{
    int firstRow = atlasFirstMiddleFrom(atlasLeastY(body));
    int lastRow = atlasLastMiddleTo(atlasMostY(body));
    int written = 0;
    for (int row = firstRow; row <= lastRow; row++)
    {
        float y = atlasMiddleM(row);
        float fromX;
        float toX;
        if (!atlasAcross(body, y, fromX, toX)) continue;

        int fromColumn = atlasFirstMiddleFrom(fromX);
        int toColumn = atlasLastMiddleTo(toX);
        if (toColumn < fromColumn) continue;

        int cellRow = row >> ATLAS_FIGURES.shift;
        int rowFirstStart = (row & ATLAS_FIGURES.mask) << ATLAS_FIGURES.rowStartsShift;
        for (int cellColumn = fromColumn >> ATLAS_FIGURES.shift; cellColumn <= (toColumn >> ATLAS_FIGURES.shift); cellColumn++)
        {
            if (uint(cellColumn - ATLAS_FIGURES.cellsFromX) >= uint(ATLAS_FIGURES.cellsWidth)
                || uint(cellRow - ATLAS_FIGURES.cellsFromY) >= uint(ATLAS_FIGURES.cellsHeight)) continue;

            int slot = atlasCellSlot[
                (cellRow - ATLAS_FIGURES.cellsFromY) * ATLAS_FIGURES.cellsWidth + (cellColumn - ATLAS_FIGURES.cellsFromX)];
            if (slot < 0) continue;

            int cellFirstColumn = cellColumn << ATLAS_FIGURES.shift;
            int first = atlasSlotFirst[slot];
            int starts = slot * ATLAS_FIGURES.startsPerCell + rowFirstStart;
            int firstColumn = max(fromColumn - cellFirstColumn, 0);
            int lastColumn = min(toColumn - cellFirstColumn, ATLAS_FIGURES.mask);
            uint columns = uint(lastColumn - firstColumn);
            int end = first + int(atlasPointStart(starts + (lastColumn >> ATLAS_FIGURES.startShift) + 1));
            for (int at = first + int(atlasPointStart(starts + (firstColumn >> ATLAS_FIGURES.startShift))); at < end; at++)
            {
                uvec2 entry = atlasEntries[at];
                int column = int((entry.x >> 8) & 0xFFu);
                if (uint(column - firstColumn) > columns) continue;

                int way = int(entry.y);
                if (ATLAS_FIGURES.hasChannels != 0u && (atlasChannelsOf(way) & channels) == 0u) continue;

                uint outside = entry.x & 0xFFu;
                if (outside != 0u)
                {
                    float depthM = atlasDepthM(body, vec2(atlasMiddleM(cellFirstColumn + column), y));
                    precise float outsideM = float(outside) * ATLAS_FIGURES.metresPerOutside;
                    if (depthM <= outsideM) continue;
                }

                float alongTenths = float(entry.x >> 16);
                bool grown = false;
                for (int held = 0; held < written; held++)
                {
                    if (found[held].way != way) continue;

                    found[held].fromM = min(found[held].fromM, alongTenths);
                    found[held].toM = max(found[held].toM, alongTenths);
                    grown = true;
                    break;
                }

                if (grown) continue;

                if (written == AtlasMostWaysUnderABody)
                {
                    dropped++;
                    continue;
                }

                found[written] = AtlasCover(way, alongTenths, alongTenths);
                written++;
            }
        }
    }

    return written;
}

// A point stands for the half step either side of it along the way, so each stretch is widened by that much, held to
// the way's own two ends and turned from tenths into metres (RibbonAtlas.Widened).
void atlasWidened(inout AtlasCover found[AtlasMostWaysUnderABody], int written)
{
    precise float halfM = ATLAS_FIGURES.cellM * 0.5;
    for (int held = 0; held < written; held++)
    {
        AtlasCover one = found[held];
        precise float fromM = roundedQuotient(one.fromM, ATLAS_FIGURES.alongPerMetre) - halfM;
        precise float toM = roundedQuotient(one.toM, ATLAS_FIGURES.alongPerMetre) + halfM;
        found[held] = AtlasCover(one.way, max(0.0, fromM), min(atlasLengthM[one.way], toM));
    }
}

// Every way a box stands over, and the stretch of each, into `covers`; how many (RibbonAtlas.UnderBox). `forward` is
// the host's unit vector of the heading, so no corner is the device's own trigonometry.
int atlasUnderBox(
    vec2 centreM, vec2 forward, float halfLengthM, float halfWidthM, uint channels, inout AtlasCover covers[AtlasMostWaysUnderABody],
    inout uint dropped)
{
    int written = atlasRead(atlasBox(centreM, forward, halfLengthM, halfWidthM), channels, covers, dropped);
    atlasWidened(covers, written);
    return written;
}

// The same for a disc — a walker's collider (RibbonAtlas.UnderDisc).
int atlasUnderDisc(vec2 centreM, float radiusM, uint channels, inout AtlasCover covers[AtlasMostWaysUnderABody], inout uint dropped)
{
    int written = atlasRead(atlasDisc(centreM, radiusM), channels, covers, dropped);
    atlasWidened(covers, written);
    return written;
}
