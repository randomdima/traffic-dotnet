// One body's gather (PhysicsWorld.Gather), shared by pairs.comp and woken.comp: the cells its box covers in the moving
// grid and in the furniture's, each body met kept once — in the first cell the two share (CellGrid.FirstShared) — where
// the boxes meet, the filters let them and the channels meet (PHY-1a).
//
// The includer declares the grids (movingStarts, movingItems, staticCounts, staticStarts, staticItems), the bounds and the
// filter, and defines whether a body takes a moving one it meets, before the bounds are asked (takes), and what it does
// with each one it keeps (keep).

#ifndef GATHER_GLSL
#define GATHER_GLSL

#include "filter.glsl"

bool takes(int owner, int other);
void keep(int owner, int other);

bool apart(vec4 a, vec4 b) { return a.z < b.x || a.x > b.z || a.w < b.y || a.y > b.w; }

bool meets(uint owner, uint other)
{
    return (other & enabledBit) != 0u
        && (filterLayer(owner) & filterMask(other)) != 0u && (filterLayer(other) & filterMask(owner)) != 0u
        && (filterChannels(owner) & filterChannels(other)) != 0u;
}

// The moving grid is hashed (step.glsl's slotOf): a bucket's run is from its start to the next bucket's, and each
// entry is a body and the cell it is filed for, so a cell's own are the entries naming it.
void gather(int owner, bool withFurniture)
{
    vec4 box = bounds[owner];
    uint ownerFilter = filter_[owner];
    int fromX = cellX(box.x);
    int fromY = cellY(box.y);
    int toX = cellX(box.z);
    int toY = cellY(box.w);
    for (int y = fromY; y <= toY; y++)
    {
        for (int x = fromX; x <= toX; x++)
        {
            uint cell = cellIndex(x, y);
            uint bucket = slotOf(cell);
            uint end = movingStarts[bucket + 1u];
            for (uint item = movingStarts[bucket]; item < end; item++)
            {
                uvec2 filed = movingItems[item];
                if (filed.y != cell) continue;

                int other = int(filed.x);
                if (!takes(owner, other)) continue;

                vec4 otherBox = bounds[other];
                if (apart(otherBox, box)) continue;
                if ((x != fromX && cellX(otherBox.x) != x) || (y != fromY && cellY(otherBox.y) != y)) continue;
                if (!meets(ownerFilter, filter_[other])) continue;

                keep(owner, other);
            }

            if (!withFurniture) continue;

            uint start = staticStarts[cell];
            end = start + staticCounts[cell];
            for (uint item = start; item < end; item++)
            {
                int other = staticItems[item];
                vec4 otherBox = bounds[other];
                if (apart(otherBox, box)) continue;
                if ((x != fromX && cellX(otherBox.x) != x) || (y != fromY && cellY(otherBox.y) != y)) continue;
                if (!meets(ownerFilter, filter_[other])) continue;

                keep(owner, other);
            }
        }
    }
}

#endif
