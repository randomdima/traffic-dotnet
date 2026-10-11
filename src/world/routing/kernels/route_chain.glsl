// RouteChain.LayInto on the device: a search's run-links expanded into the network's own pieces, from where the body
// joins the first to where the destination stands on the last, bounded and saying so.
//
// GLSL has no generics, so this is laid once a network's joins: the includer defines, before including,
// ROUTE_CHAIN_LAY_INTO (the function's name), ROUTE_CHAIN_JOINS (the joins' state, IRouteJoins' struct) and the joins'
// answers ROUTE_CHAIN_JOINED_AT(joins, link, from), ROUTE_CHAIN_REACHES(joins, from, onto) and
// ROUTE_CHAIN_BETWEEN(joins, from, onto); the network's runs as ROUTE_CHAIN_PIECES_FROM(link), ROUTE_CHAIN_PIECES_TO(link),
// ROUTE_CHAIN_PIECE(at) and ROUTE_CHAIN_PIECE_AT(link, alongM) (RunNetwork.PieceAt's slot); the search's links as
// ROUTE_CHAIN_LINK(links, at), `links` naming whose; and what is written as ROUTE_CHAIN_INTO(into, at), `into` naming
// whose, ROUTE_CHAIN_ROOM pieces of it. route_tables.glsl first for RouteGoal, and the RouteChainNoWay a way between two
// pieces is none at.

// How many were written; ranOut where the room filled with route still to come.
int ROUTE_CHAIN_LAY_INTO(
    inout ROUTE_CHAIN_JOINS joins, uint links, uint into, int linkCount, int fromWay, int firstSlot, RouteGoal goal, out bool ranOut)
{
    int written = 0;
    int last = fromWay;
    bool reaches = true;
    ranOut = false;

    for (int index = 0; index < linkCount && reaches && !ranOut; index++)
    {
        int link = ROUTE_CHAIN_LINK(links, index);
        int first = ROUTE_CHAIN_PIECES_FROM(link);
        int pieces = ROUTE_CHAIN_PIECES_TO(link) - first;

        int from = index == 0 && firstSlot >= 0 ? firstSlot : ROUTE_CHAIN_JOINED_AT(joins, link, last);

        // The last link is only travelled as far as the destination stands along it.
        int to = pieces;
        if (index == linkCount - 1 && link == goal.link) to = min(to, ROUTE_CHAIN_PIECE_AT(link, goal.alongM) + 1);

        for (int slot = from; slot < to; slot++)
        {
            if (written == int(ROUTE_CHAIN_ROOM))
            {
                ranOut = true;
                break;
            }

            int onto = ROUTE_CHAIN_PIECE(first + slot);
            if (!ROUTE_CHAIN_REACHES(joins, last, onto))
            {
                reaches = false;
                break;
            }

            // The way between two pieces goes in before the piece it leads onto, where the network holds one.
            int between = ROUTE_CHAIN_BETWEEN(joins, last, onto);
            if (between != RouteChainNoWay)
            {
                ROUTE_CHAIN_INTO(into, written++) = between;
                if (written == int(ROUTE_CHAIN_ROOM))
                {
                    ranOut = true;
                    break;
                }
            }

            last = onto;
            ROUTE_CHAIN_INTO(into, written++) = last;
        }
    }

    return written;
}
