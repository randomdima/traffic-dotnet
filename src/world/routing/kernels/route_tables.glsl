// The route service's tables (app/devicetick/DeviceRoutes), at ids 384–447 of the root (town_root.glsl): each network's
// graph and its cells, laid once; the surcharges and closed links the town sets; and the request slots, their answers
// and each slot's own scratch. Both networks share one layout, network n's run of ids starting n runs on.

#ifndef ROUTE_TABLES_GLSL
#define ROUTE_TABLES_GLSL

#include "world/town/kernels/town_root.glsl"

// The room a request row has (DeviceRoutes.MostEntries, MostGoals): compile-time, since it lays the row out.
#define ROUTE_MOST_ENTRIES 8
#define ROUTE_MOST_GOALS 2

const uint RouteDriving = 0u;
const uint RouteWalking = 1u;

// Network n's tables, at RoutesNetworks + n * RouteNetworkIds + the table's place in the run.
const uint RoutesNetworks = 384u;
const uint RouteNetworkIds = 16u;

// A graph's four tables from its first id (TravelGraph): where each link's turns start (one more than the links), each
// turn's link, each turn's price, each link's weight.
const uint GraphTurnStart = 0u;
const uint GraphTurnTo = 1u;
const uint GraphTurnPriceM = 2u;
const uint GraphWeightM = 3u;

// A network's run (RouteCells).
const uint RouteFine = 0u;
const uint RouteCoarse = 4u;
const uint RouteCellOf = 8u;
const uint RouteNearM = 9u;
const uint RouteCellLinkStart = 10u;
const uint RouteCellLinks = 11u;
const uint RouteNetworkFigures = 12u;
const uint RouteSurcharges = 13u;
const uint RouteClosed = 14u;

// Both networks'.
const uint RoutesFigures = 416u;
const uint RoutesRequests = 417u;
const uint RoutesResults = 418u;
const uint RoutesResultLinks = 419u;
const uint RoutesSlotState = 420u;
const uint RoutesFineState = 421u;
const uint RoutesFineHeap = 422u;
const uint RoutesCoarseState = 423u;
const uint RoutesCoarseHeap = 424u;
const uint RoutesCrossed = 425u;
const uint RoutesInWindow = 426u;
const uint RoutesWindowCells = 427u;

const int RouteNoLink = -1;
const int RouteNoCell = -1;
const int RouteNoGoal = -1;
const int RouteAtTheAim = -2;

// A request slot's state: written pending by its asker, done by the search.
const uint RouteSlotEmpty = 0u;
const uint RouteSlotPending = 1u;
const uint RouteSlotDone = 2u;

struct RouteEntry
{
    int link;
    float alongM;
    float remainingM;
    float enterM;
};

struct RouteGoal
{
    int link;
    float alongM;
};

// closedApply: whether the closed links bar the search (SRV-10); nowS: the clock the surcharges are read at.
struct RouteRequest
{
    uint state;
    uint network;
    uint entryCount;
    uint goalCount;
    uint closedApply;
    float nowS;
    uint owner;
    uint stamp;
    RouteEntry entries[ROUTE_MOST_ENTRIES];
    RouteGoal goals[ROUTE_MOST_GOALS];
};

struct RouteResult
{
    uint count;
    int goalSlot;
    uint stopsShort;
    uint overTheWholeGraph;
    float costM;
    uint settledLinks;
    uint owner;
    uint stamp;
};

struct RouteNetworkRow
{
    uint linkCount;
    uint cellCount;
    uint sectionCells;
    float aimWithinM;
};

// fineRoom and cellRoom: a slot's scratch, the larger network's links and cells.
struct RoutesRow
{
    uint slots;
    uint fineRoom;
    uint cellRoom;
    uint mostLinks;
};

struct RouteMark
{
    int link;
    float untilS;
    float priceM;
};

// One link's search state (RoutePlanner's _costM, _cameFrom, _stamp, _heapAt).
struct RouteLinkState
{
    float costM;
    int cameFrom;
    uint stamp;
    int heapAt;
};

struct RouteHeapEntry
{
    int link;
    float keyM;
};

// What a slot's searches carry from one to the next (RoutePlanner._generation, RouteSearch._stamp).
struct RouteSlotRow
{
    uint fineGeneration;
    uint coarseGeneration;
    uint windowStamp;
    uint pad;
};

// A network's tables as a search reads them: laid once and never written, so their loads need not wait on its writes.
layout(buffer_reference, std430, buffer_reference_align = 4) readonly restrict buffer RouteLaidUints { uint v[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) readonly restrict buffer RouteLaidInts { int v[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) readonly restrict buffer RouteLaidFloats { float v[]; };

layout(buffer_reference, std430, buffer_reference_align = 4) buffer RouteRequests { RouteRequest v[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) buffer RouteResults { RouteResult v[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) buffer RouteNetworkRows { RouteNetworkRow v[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) buffer RoutesRows { RoutesRow v[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) buffer RouteLinkStates { RouteLinkState v[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) buffer RouteHeap { RouteHeapEntry v[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) buffer RouteSlotRows { RouteSlotRow v[]; };

// The marks a network's surcharges hold, live or spent (LinkSurcharges): count of them laid.
layout(buffer_reference, std430, buffer_reference_align = 4) buffer RouteSurchargeTable
{
    uint count;
    uint pad0;
    uint pad1;
    uint pad2;
    RouteMark marks[];
};

#define routes_Table(network, table) (RoutesNetworks + (network) * RouteNetworkIds + (table))

#define routes_Network(network) TOWN(RouteNetworkRows, routes_Table(network, RouteNetworkFigures)).v[0]
#define routes_CellOf(network, link) TOWN(Ints, routes_Table(network, RouteCellOf)).v[link]
#define routes_NearM(network, link) TOWN(Floats, routes_Table(network, RouteNearM)).v[link]
#define routes_CellLinkStart(network, cell) TOWN(Uints, routes_Table(network, RouteCellLinkStart)).v[cell]
#define routes_CellLink(network, at) TOWN(Ints, routes_Table(network, RouteCellLinks)).v[at]
#define routes_Surcharges(network) TOWN(RouteSurchargeTable, routes_Table(network, RouteSurcharges))
#define routes_Closed(network, link) TOWN(Uints, routes_Table(network, RouteClosed)).v[link]

#define graph_TurnStart(graph, link) TOWN(Uints, (graph) + GraphTurnStart).v[link]
#define graph_TurnTo(graph, turn) TOWN(Ints, (graph) + GraphTurnTo).v[turn]
#define graph_TurnPriceM(graph, turn) TOWN(Floats, (graph) + GraphTurnPriceM).v[turn]
#define graph_WeightM(graph, link) TOWN(Floats, (graph) + GraphWeightM).v[link]

#define routes_Figures TOWN(RoutesRows, RoutesFigures).v[0]
#define routes_Request(slot) TOWN(RouteRequests, RoutesRequests).v[slot]
#define routes_Result(slot) TOWN(RouteResults, RoutesResults).v[slot]
#define routes_ResultLink(slot, at) TOWN(Ints, RoutesResultLinks).v[(slot) * routes_Figures.mostLinks + (at)]
#define routes_Slot(slot) TOWN(RouteSlotRows, RoutesSlotState).v[slot]

// The widths of a scratch table's elements, as a slot's run of one is found.
const uint RouteWordBytes = 4u;
const uint RouteHeapEntryBytes = 8u;
const uint RouteLinkStateBytes = 16u;

// A slot's own run of a scratch table of elementBytes-wide elements, room of them a slot.
uint64_t routesSlotAddress(uint id, uint slot, uint room, uint elementBytes)
{
    return townRoot.at[id] + uint64_t(slot) * uint64_t(room) * uint64_t(elementBytes);
}

#endif
