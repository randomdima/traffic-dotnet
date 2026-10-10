// RoutePlanner ported (world/routing/RoutePlanner.cs): one Dijkstra over a graph's directed links, the goal tracked
// apart from the frontier, through a window or over the whole graph, the closed links never entered (SRV-10) and the
// surcharges priced in — the same binary heap, compared in the same order, and the same float sums, so a search answers
// bit for bit what the C# answers. The including kernel searches with one invocation a workgroup.

#ifndef ROUTE_PLANNER_GLSL
#define ROUTE_PLANNER_GLSL

#include "world/routing/kernels/route_tables.glsl"

const int RouteNotInHeap = -1;

float routeInfinity() { return uintBitsToFloat(0x7F800000u); }

// The top of a slot's heap — every pop and nearly every sift — held in the workgroup's own memory, the places past it in
// the slot's table. A slot plans one search at a time and a search reads no place of its heap it has not written, so its
// two planners share one.
#define ROUTE_SHARED_HEAP 2048
shared RouteHeapEntry routeSharedHeap[ROUTE_SHARED_HEAP];

// A graph's tables (TravelGraph), read through once rather than through the root on every relaxation.
struct RouteGraph
{
    RouteLaidUints turnStart;
    RouteLaidInts turnTo;
    RouteLaidFloats turnPriceM;
    RouteLaidFloats weightM;
};

RouteGraph routeGraph(uint first)
{
    return RouteGraph(
        TOWN(RouteLaidUints, first + GraphTurnStart), TOWN(RouteLaidInts, first + GraphTurnTo),
        TOWN(RouteLaidFloats, first + GraphTurnPriceM), TOWN(RouteLaidFloats, first + GraphWeightM));
}

// One planner's own state, one slot's (RoutePlanner's fields): its links' search state and its heap, room of each.
struct RoutePlanner
{
    RouteLinkStates links;
    RouteHeap heap;
    uint room;
    uint generation;
    int heapCount;
    uint settledLinks;
};

// The surcharges one search reads (LinkSurcharges), at the clock it asks them at: none where count is nought.
struct RouteMarks
{
    RouteSurchargeTable table;
    uint count;
    float nowS;
};

// The links a search may not enter, or none where apply is false (the C#'s empty span).
struct RouteClosedLinks
{
    Uints flags;
    bool apply;
};

// Where a search may go (RouteWindow): the whole graph where bounded is false.
struct RouteWindow
{
    bool bounded;
    RouteLaidInts cellOf;
    RouteLaidFloats nearM;
    Uints cellStamp;
    uint stamp;
    int aimCell;
    float aimWithinM;
};

// The tables are any the caller holds and never read: a reference made of a constant address is refused by the SPIR-V
// optimiser.
RouteWindow routeWholeGraph(RouteLaidInts cellOf, RouteLaidFloats nearM, Uints cellStamp)
{
    return RouteWindow(false, cellOf, nearM, cellStamp, 0u, RouteNoCell, 0.0);
}

bool routeWindowAims(RouteWindow window) { return window.bounded && window.aimCell != RouteNoCell; }

bool routeWindowHolds(RouteWindow window, int link)
{
    return !window.bounded || window.cellStamp.v[window.cellOf.v[link]] == window.stamp;
}

bool routeWindowIsAim(RouteWindow window, int link)
{
    return window.cellOf.v[link] == window.aimCell && window.nearM.v[link] <= window.aimWithinM;
}

// LinkSurcharges.PriceM: every slot laid asked its life, which answers what walking the live ones does.
float routePriceM(RouteMarks marks, int link)
{
    float priceM = 0.0;
    for (uint slot = 0u; slot < marks.count; slot++)
    {
        RouteMark mark = marks.table.marks[slot];
        if (mark.link == link && mark.untilS > marks.nowS) priceM = max(priceM, mark.priceM);
    }

    return priceM;
}

// A search started clean without clearing the links: the stamp a link carries says whether it is this search's. A
// generation run round clears them, so a stamp left from 2³² searches ago is never taken for this one's.
void routeNextGeneration(inout RoutePlanner planner)
{
    planner.generation++;
    if (planner.generation != 0u) return;

    for (uint link = 0u; link < planner.room; link++) planner.links.v[link].stamp = 0u;
    planner.generation = 1u;
}

// RoutePlanner.Touch, answering the link's state as it leaves it.
RouteLinkState routeTouched(RoutePlanner planner, int link)
{
    RouteLinkState state = planner.links.v[link];
    if (state.stamp == planner.generation) return state;

    state = RouteLinkState(routeInfinity(), RouteNoLink, planner.generation, RouteNotInHeap);
    planner.links.v[link] = state;
    return state;
}

RouteHeapEntry routeHeapAt(RoutePlanner planner, int at)
{
    return at < ROUTE_SHARED_HEAP ? routeSharedHeap[at] : planner.heap.v[at];
}

void routeHeapPut(RoutePlanner planner, int at, RouteHeapEntry entry)
{
    if (at < ROUTE_SHARED_HEAP) routeSharedHeap[at] = entry;
    else planner.heap.v[at] = entry;
    planner.links.v[entry.link].heapAt = at;
}

// RoutePlanner.SiftUp of an entry standing at a place: each entry it passes moved down a place, as the C#'s swaps move
// it, and the entry written once where the swaps leave it. Answers that place.
int routeSiftUp(RoutePlanner planner, int at, RouteHeapEntry moving)
{
    while (at > 0)
    {
        int parent = (at - 1) / 2;
        RouteHeapEntry above = routeHeapAt(planner, parent);
        if (above.keyM <= moving.keyM) break;

        routeHeapPut(planner, at, above);
        at = parent;
    }

    routeHeapPut(planner, at, moving);
    return at;
}

// RoutePlanner.SiftDown, as routeSiftUp: the smaller child moved up a place at each step the C# swaps at.
void routeSiftDown(RoutePlanner planner, int at, RouteHeapEntry moving)
{
    while (true)
    {
        int left = at * 2 + 1;
        if (left >= planner.heapCount) break;

        int smallest = left;
        RouteHeapEntry child = routeHeapAt(planner, left);
        if (left + 1 < planner.heapCount)
        {
            RouteHeapEntry right = routeHeapAt(planner, left + 1);
            if (right.keyM < child.keyM)
            {
                smallest = left + 1;
                child = right;
            }
        }

        if (moving.keyM <= child.keyM) break;

        routeHeapPut(planner, at, child);
        at = smallest;
    }

    routeHeapPut(planner, at, moving);
}

// RoutePlanner.Push of a link whose heap place is heapAt — its own, as touched; answers where it settles in the heap.
int routePush(inout RoutePlanner planner, int link, float keyM, int heapAt)
{
    int at = heapAt == RouteNotInHeap ? planner.heapCount++ : heapAt;
    return routeSiftUp(planner, at, RouteHeapEntry(link, keyM));
}

int routePop(inout RoutePlanner planner)
{
    int link = routeHeapAt(planner, 0).link;
    planner.links.v[link].heapAt = RouteNotInHeap;

    int last = --planner.heapCount;
    if (last > 0) routeSiftDown(planner, 0, routeHeapAt(planner, last));

    return link;
}

// A link reached at reachedM from cameFrom, its state written whole and its place in the heap set.
void routeReached(inout RoutePlanner planner, int link, float reachedM, int cameFrom, int heapAt)
{
    planner.links.v[link] = RouteLinkState(reachedM, cameFrom, planner.generation, heapAt);
    routePush(planner, link, reachedM, heapAt);
}

// RoutePlanner.ReachedM: what reaching the far end of a link cost the last search, or infinity where it never did.
float routeReachedM(RoutePlanner planner, int link)
{
    RouteLinkState state = planner.links.v[link];
    return state.stamp == planner.generation ? state.costM : routeInfinity();
}

// RoutePlanner.Plan: the cheapest route from any entry to any goal, written into into.v[0 ..] in the order it is
// travelled; how many links, or nought where there is none or it will not fit in intoRoom.
int routePlan(
    inout RoutePlanner planner, RouteGraph graph, RouteEntry entries[ROUTE_MOST_ENTRIES], uint entryCount,
    RouteGoal goals[ROUTE_MOST_GOALS], uint goalCount, RouteMarks marks, RouteClosedLinks closed, RouteWindow window,
    Ints into, int intoRoom, out float costM, out int goalSlot)
{
    routeNextGeneration(planner);
    planner.heapCount = 0;
    planner.settledLinks = 0u;
    costM = routeInfinity();
    goalSlot = RouteNoGoal;

    int bestPred = RouteNoLink;
    int aimedAt = RouteNoLink;
    bool aims = routeWindowAims(window);

    for (uint at = 0u; at < entryCount; at++)
    {
        RouteEntry entry = entries[at];
        RouteLinkState state = routeTouched(planner, entry.link);
        float reachedM = entry.enterM + entry.remainingM;
        if (reachedM < state.costM) routeReached(planner, entry.link, reachedM, RouteNoLink, state.heapAt);

        // The goal on the link the body is already committed to, and only where it is still ahead.
        for (uint slot = 0u; slot < goalCount; slot++)
        {
            if (goals[slot].link != entry.link || goals[slot].alongM < entry.alongM) continue;

            float directM = entry.enterM + goals[slot].alongM - entry.alongM;
            if (directM >= costM) continue;

            costM = directM;
            bestPred = RouteNoLink;
            goalSlot = int(slot);
        }
    }

    while (planner.heapCount > 0)
    {
        float boundM = routeHeapAt(planner, 0).keyM;
        if (boundM >= costM) break;

        int link = routePop(planner);
        planner.settledLinks++;

        // What reaching this link cost, read once: only a turn back onto itself writes it again, and that turn keeps it.
        float linkCostM = planner.links.v[link].costM;
        uint lastTurn = graph.turnStart.v[link + 1];
        for (uint turn = graph.turnStart.v[link]; turn < lastTurn; turn++)
        {
            int onto = graph.turnTo.v[turn];
            if (closed.apply && closed.flags.v[onto] != 0u) continue;
            if (!routeWindowHolds(window, onto)) continue;

            float enterM = graph.turnPriceM.v[turn] + routePriceM(marks, onto);

            for (uint slot = 0u; slot < goalCount; slot++)
            {
                if (goals[slot].link != onto) continue;

                // The last link costs the run into the destination and no more.
                float arriveM = linkCostM + enterM + goals[slot].alongM;
                if (arriveM >= costM) continue;

                costM = arriveM;
                bestPred = link;
                goalSlot = int(slot);
            }

            float throughM = linkCostM + enterM + graph.weightM.v[onto];
            if (aims && throughM < costM && routeWindowIsAim(window, onto))
            {
                costM = throughM;
                bestPred = link;
                aimedAt = onto;
                goalSlot = RouteAtTheAim;
            }

            RouteLinkState state = routeTouched(planner, onto);
            if (throughM >= state.costM) continue;

            routeReached(planner, onto, throughM, link, state.heapAt);
            if (onto == link) linkCostM = throughM;
        }
    }

    if (goalSlot == RouteNoGoal) return 0;

    int length = 1;
    for (int back = bestPred; back != RouteNoLink; back = planner.links.v[back].cameFrom) length++;
    if (length > intoRoom)
    {
        costM = routeInfinity();
        goalSlot = RouteNoGoal;
        return 0;
    }

    into.v[length - 1] = goalSlot == RouteAtTheAim ? aimedAt : goals[goalSlot].link;
    int write = length - 2;
    for (int back = bestPred; back != RouteNoLink; back = planner.links.v[back].cameFrom) into.v[write--] = back;

    return length;
}

#endif
