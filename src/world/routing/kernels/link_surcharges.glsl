// LinkSurcharges.Mark on the device, into a network's table (route_tables.glsl's RouteSurchargeTable): a link priced up for
// a while, in the slot the town's next mark would take — the next of those laid, modulo the table's room, so a full table
// overwrites its oldest. A search reads every slot and asks each its life, so nothing lists the live ones.
//
// Marks laid by several threads at once take a slot each in whichever order they reach it, which is not the order the
// town would have laid them in.

#ifndef LINK_SURCHARGES_GLSL
#define LINK_SURCHARGES_GLSL

#include "world/routing/kernels/route_tables.glsl"

void routes_Mark(uint network, int link, float priceM, float forS)
{
    RouteSurchargeTable table = routes_Surcharges(network);
    uint laid = atomicAdd(table.marked, 1u);
    uint slot = laid % table.room;
    precise float untilS = table.nowS + forS;
    table.marks[slot] = RouteMark(link, untilS, priceM);
    atomicMax(table.count, min(laid + 1u, table.room));
}

#endif
