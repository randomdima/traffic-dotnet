// An agent asking the route service (route_search.comp) where its C# searched in place (TownWorld.TryPlan, LayWalk,
// RouteExistsToTheBay): one ask an agent at a time, held in a request slot until its answer is read. A search does not
// answer in the decision that asks it, so the asker answers *pending* and is decided again later; the same ask made again
// once the search has run is answered from the slot. Agents are the roster's numbers: walkers, then cars.

#ifndef ROUTE_ASKS_GLSL
#define ROUTE_ASKS_GLSL

#include "world/routing/kernels/route_tables.glsl"

// Per agent: the slot its ask is held in, or -1; and the stamp it last asked with.
const uint RoutesAskSlot = 428u;
const uint RoutesAskStamp = 429u;

#define routeAsks_Slot(agent) TOWN(Ints, RoutesAskSlot).v[agent]
#define routeAsks_Stamp(agent) TOWN(Uints, RoutesAskStamp).v[agent]

// What asking came to.
const uint RouteAskLanded = 0u;
const uint RouteAskPending = 1u;
const uint RouteAskRefused = 2u;

// Whether two requests ask the same search: everything but the slot's state, the clock and who asked.
bool routeRequestsAlike(RouteRequest one, RouteRequest other)
{
    if (one.network != other.network || one.entryCount != other.entryCount || one.goalCount != other.goalCount
        || one.closedApply != other.closedApply)
    {
        return false;
    }

    for (uint at = 0u; at < one.entryCount; at++)
    {
        if (one.entries[at] != other.entries[at]) return false;
    }

    for (uint at = 0u; at < one.goalCount; at++)
    {
        if (one.goals[at] != other.goals[at]) return false;
    }

    return true;
}

// The slot an agent's ask is held in let go, its answer read or no longer wanted.
void routes_Release(uint agent)
{
    int held = routeAsks_Slot(agent);
    if (held < 0) return;

    routes_Request(uint(held)).state = RouteSlotEmpty;
    routeAsks_Slot(agent) = -1;
}

// An ask of the route service: landed, with its answer in `slot` (routes_Result, routes_ResultLink) where the same ask was
// made before and searched since; pending where it is asked now or is still waiting for its search; refused where every
// slot is held, to be asked again on a later decision. A different ask let go of the last one first — but never one
// still waiting for its search, which answers pending until it is done.
uint routes_Ask(uint agent, RouteRequest ask, out uint slot)
{
    slot = 0u;
    int held = routeAsks_Slot(agent);
    if (held >= 0)
    {
        slot = uint(held);
        RouteRequest asked = routes_Request(slot);
        if (routeRequestsAlike(asked, ask)) return asked.state == RouteSlotDone ? RouteAskLanded : RouteAskPending;
        if (asked.state == RouteSlotPending) return RouteAskPending;

        routes_Release(agent);
    }

    uint slots = routes_Figures.slots;
    for (uint tried = 0u; tried < slots; tried++)
    {
        uint at = (agent + tried) % slots;
        if (atomicCompSwap(TOWN(RouteRequests, RoutesRequests).v[at].state, RouteSlotEmpty, RouteSlotPending) != RouteSlotEmpty)
        {
            continue;
        }

        uint stamp = routeAsks_Stamp(agent) + 1u;
        routeAsks_Stamp(agent) = stamp;
        ask.state = RouteSlotPending;
        ask.owner = agent;
        ask.stamp = stamp;
        routes_Request(at) = ask;
        routeAsks_Slot(agent) = int(at);
        slot = at;
        return RouteAskPending;
    }

    return RouteAskRefused;
}

#endif
