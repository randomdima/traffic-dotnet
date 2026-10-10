// RouteSearch ported (world/routing/RouteSearch.cs): one network's search, sectioned — the cells the route crosses
// first, then the links of the first few of them in detail through a window, widened once, and the whole graph where
// the window cannot settle the answer — function for function, over one request slot's own scratch.

#ifndef ROUTE_SEARCH_GLSL
#define ROUTE_SEARCH_GLSL

#include "world/routing/kernels/route_planner.glsl"

// One slot's search over one network (RouteSearch's fields and what its last plan answered).
struct RouteSearch
{
    uint network;
    RouteGraph fine;
    RouteGraph coarse;
    RoutePlanner planner;
    RoutePlanner coarsePlanner;
    RouteLaidInts cellOf;
    RouteLaidFloats nearM;
    RouteLaidUints cellLinkStart;
    RouteLaidInts cellLinks;
    uint cellCount;
    uint cellRoom;
    uint sectionCells;
    float aimWithinM;
    Ints links;
    int mostLinks;
    Ints crossed;
    Uints inWindow;
    Ints windowCells;
    uint stamp;
    uint windowCount;
    bool stopsShort;
    bool overTheWholeGraph;
    uint settledLinks;
    float costM;
};

RouteSearch routeSearchFor(uint slot, uint network)
{
    RoutesRow figures = routes_Figures;
    RouteNetworkRow row = routes_Network(network);
    RouteSlotRow carried = routes_Slot(slot);

    RouteSearch search;
    search.network = network;
    search.fine = routeGraph(routes_Table(network, RouteFine));
    search.coarse = routeGraph(routes_Table(network, RouteCoarse));
    search.planner = RoutePlanner(
        RouteLinkStates(routesSlotAddress(RoutesFineState, slot, figures.fineRoom, RouteLinkStateBytes)),
        RouteHeap(routesSlotAddress(RoutesFineHeap, slot, figures.fineRoom, RouteHeapEntryBytes)), figures.fineRoom,
        carried.fineGeneration, 0, 0u);
    search.coarsePlanner = RoutePlanner(
        RouteLinkStates(routesSlotAddress(RoutesCoarseState, slot, figures.cellRoom, RouteLinkStateBytes)),
        RouteHeap(routesSlotAddress(RoutesCoarseHeap, slot, figures.cellRoom, RouteHeapEntryBytes)), figures.cellRoom,
        carried.coarseGeneration, 0, 0u);
    search.cellOf = TOWN(RouteLaidInts, routes_Table(network, RouteCellOf));
    search.nearM = TOWN(RouteLaidFloats, routes_Table(network, RouteNearM));
    search.cellLinkStart = TOWN(RouteLaidUints, routes_Table(network, RouteCellLinkStart));
    search.cellLinks = TOWN(RouteLaidInts, routes_Table(network, RouteCellLinks));
    search.cellCount = row.cellCount;
    search.cellRoom = figures.cellRoom;
    search.sectionCells = row.sectionCells;
    search.aimWithinM = row.aimWithinM;
    search.links = Ints(routesSlotAddress(RoutesResultLinks, slot, figures.mostLinks, RouteWordBytes));
    search.mostLinks = int(figures.mostLinks);
    search.crossed = Ints(routesSlotAddress(RoutesCrossed, slot, figures.cellRoom + 1u, RouteWordBytes));
    search.inWindow = Uints(routesSlotAddress(RoutesInWindow, slot, figures.cellRoom, RouteWordBytes));
    search.windowCells = Ints(routesSlotAddress(RoutesWindowCells, slot, figures.cellRoom, RouteWordBytes));
    search.stamp = carried.windowStamp;
    search.windowCount = 0u;
    return search;
}

// What the slot's searches carry to its next one.
void routeSearchKeep(RouteSearch search, uint slot)
{
    routes_Slot(slot) = RouteSlotRow(search.planner.generation, search.coarsePlanner.generation, search.stamp, 0u);
}

// RouteSearch.CrossTheCells: the cells the route crosses, in order, into crossed; nought where no run of cells reaches
// a goal, and the goal reached named either way.
int routeCrossTheCells(
    inout RouteSearch search, RouteEntry entries[ROUTE_MOST_ENTRIES], uint entryCount, RouteGoal goals[ROUTE_MOST_GOALS],
    uint goalCount, out int goalSlot)
{
    RouteEntry cellEntries[ROUTE_MOST_ENTRIES];
    for (uint slot = 0u; slot < entryCount; slot++)
    {
        cellEntries[slot] = RouteEntry(search.cellOf.v[entries[slot].link], 0.0, 0.0, 0.0);
    }

    RouteGoal cellGoals[ROUTE_MOST_GOALS];
    for (uint slot = 0u; slot < goalCount; slot++) cellGoals[slot] = RouteGoal(search.cellOf.v[goals[slot].link], 0.0);

    // No surcharges, no closed links and no window: the network's own tables stand in, never read.
    RouteMarks noMarks = RouteMarks(routes_Surcharges(search.network), 0u, 0.0);
    RouteClosedLinks noneClosed = RouteClosedLinks(TOWN(Uints, routes_Table(search.network, RouteClosed)), false);
    float costM;
    return routePlan(
        search.coarsePlanner, search.coarse, cellEntries, entryCount, cellGoals, goalCount, noMarks, noneClosed,
        routeWholeGraph(search.cellOf, search.nearM, search.inWindow), search.crossed, int(search.cellCount) + 1, costM,
        goalSlot);
}

void routeWindowCell(inout RouteSearch search, int cell)
{
    if (search.inWindow.v[cell] == search.stamp) return;

    search.inWindow.v[cell] = search.stamp;
    search.windowCells.v[search.windowCount++] = cell;
}

// RouteSearch.Widen: every cell one way on from any cell already in the window.
void routeWiden(inout RouteSearch search)
{
    uint before = search.windowCount;
    for (uint slot = 0u; slot < before; slot++)
    {
        int cell = search.windowCells.v[slot];
        uint lastTurn = search.coarse.turnStart.v[cell + 1];
        for (uint turn = search.coarse.turnStart.v[cell]; turn < lastTurn; turn++) routeWindowCell(search, search.coarse.turnTo.v[turn]);
    }
}

// RouteSearch.OpenTheWindow: the entries' cells, the section, the overlap and a ring round them; answers the overlap's
// cell. A stamp run round clears the slot's every cell — either network's, since the slot serves both.
int routeOpenTheWindow(inout RouteSearch search, RouteEntry entries[ROUTE_MOST_ENTRIES], uint entryCount)
{
    if (search.stamp == 0xFFFFFFFFu)
    {
        for (uint cell = 0u; cell < search.cellRoom; cell++) search.inWindow.v[cell] = 0u;
        search.stamp = 0u;
    }

    search.stamp++;
    search.windowCount = 0u;
    for (uint at = 0u; at < entryCount; at++) routeWindowCell(search, search.cellOf.v[entries[at].link]);

    for (uint at = 0u; at <= search.sectionCells; at++) routeWindowCell(search, search.crossed.v[at]);

    routeWiden(search);
    return search.crossed.v[search.sectionCells];
}

// RouteSearch.TheWindowIsClear: nothing in the window closed or priced up, the two things the cells cannot see.
bool routeTheWindowIsClear(RouteSearch search, RouteMarks marks, RouteClosedLinks closed)
{
    for (uint slot = 0u; slot < marks.count; slot++)
    {
        RouteMark mark = marks.table.marks[slot];
        int link = mark.untilS > marks.nowS ? mark.link : RouteNoLink;
        if (link != RouteNoLink && search.inWindow.v[search.cellOf.v[link]] == search.stamp) return false;
    }

    if (!closed.apply) return true;

    for (uint slot = 0u; slot < search.windowCount; slot++)
    {
        int cell = search.windowCells.v[slot];
        uint last = search.cellLinkStart.v[cell + 1];
        for (uint at = search.cellLinkStart.v[cell]; at < last; at++)
        {
            if (closed.flags.v[search.cellLinks.v[at]] != 0u) return false;
        }
    }

    return true;
}

int routeInTheWindow(
    inout RouteSearch search, RouteEntry entries[ROUTE_MOST_ENTRIES], uint entryCount, RouteGoal goals[ROUTE_MOST_GOALS],
    uint goalCount, RouteMarks marks, RouteClosedLinks closed, int aimCell, bool anywhereInTheAim, out float costM,
    out int goalSlot)
{
    RouteWindow window = RouteWindow(
        true, search.cellOf, search.nearM, search.inWindow, search.stamp, aimCell,
        anywhereInTheAim ? routeInfinity() : search.aimWithinM);
    int count = routePlan(
        search.planner, search.fine, entries, entryCount, goals, goalCount, marks, closed, window, search.links,
        search.mostLinks, costM, goalSlot);
    search.settledLinks += search.planner.settledLinks;
    return count;
}

// RouteSearch.Exactly: the cheapest route, searched over the whole graph.
int routeExactly(
    inout RouteSearch search, RouteEntry entries[ROUTE_MOST_ENTRIES], uint entryCount, RouteGoal goals[ROUTE_MOST_GOALS],
    uint goalCount, RouteMarks marks, RouteClosedLinks closed, out int goalSlot)
{
    float costM;
    int count = routePlan(
        search.planner, search.fine, entries, entryCount, goals, goalCount, marks, closed,
        routeWholeGraph(search.cellOf, search.nearM, search.inWindow), search.links, search.mostLinks, costM, goalSlot);
    search.settledLinks += search.planner.settledLinks;
    search.costM = costM;
    return count;
}

int routeOverTheWhole(
    inout RouteSearch search, RouteEntry entries[ROUTE_MOST_ENTRIES], uint entryCount, RouteGoal goals[ROUTE_MOST_GOALS],
    uint goalCount, RouteMarks marks, RouteClosedLinks closed, out int goalSlot)
{
    search.overTheWholeGraph = true;
    return routeExactly(search, entries, entryCount, goals, goalCount, marks, closed, goalSlot);
}

// RouteSearch.Handed: a route that finished at its aim handed over as far as the first link into the aim's cell.
int routeHanded(RouteSearch search, int count, int aimCell)
{
    for (int at = 1; at < count; at++)
    {
        if (search.cellOf.v[search.links.v[at]] == aimCell) return at + 1;
    }

    return count;
}

// RouteSearch.Plan: the route from any entry to any goal, or the section of it in front of the body (stopsShort), in
// which case goalSlot names the goal the cells lead to.
int routeSearchPlan(
    inout RouteSearch search, RouteEntry entries[ROUTE_MOST_ENTRIES], uint entryCount, RouteGoal goals[ROUTE_MOST_GOALS],
    uint goalCount, RouteMarks marks, RouteClosedLinks closed, out int goalSlot)
{
    search.stopsShort = false;
    search.overTheWholeGraph = false;
    search.settledLinks = 0u;
    search.costM = routeInfinity();

    int crossed = routeCrossTheCells(search, entries, entryCount, goals, goalCount, goalSlot);
    if (crossed == 0) return 0;

    if (crossed <= int(search.sectionCells) + 1) return routeExactly(search, entries, entryCount, goals, goalCount, marks, closed, goalSlot);

    int aimCell = routeOpenTheWindow(search, entries, entryCount);
    if (!routeTheWindowIsClear(search, marks, closed)) return routeOverTheWhole(search, entries, entryCount, goals, goalCount, marks, closed, goalSlot);

    float costM;
    int slot;
    int count = routeInTheWindow(search, entries, entryCount, goals, goalCount, marks, closed, aimCell, false, costM, slot);
    if (count == 0)
    {
        // Asked again a ring wider and anywhere in the aim's cell, before the whole graph.
        routeWiden(search);
        if (!routeTheWindowIsClear(search, marks, closed)) return routeOverTheWhole(search, entries, entryCount, goals, goalCount, marks, closed, goalSlot);

        count = routeInTheWindow(search, entries, entryCount, goals, goalCount, marks, closed, aimCell, true, costM, slot);
        if (count == 0) return routeOverTheWhole(search, entries, entryCount, goals, goalCount, marks, closed, goalSlot);
    }

    if (slot != RouteAtTheAim)
    {
        goalSlot = slot;
        search.costM = costM;
        return count;
    }

    search.stopsShort = true;
    count = routeHanded(search, count, aimCell);
    search.costM = routeReachedM(search.planner, search.links.v[count - 1]);
    return count;
}

#endif
