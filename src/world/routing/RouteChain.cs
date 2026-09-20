namespace TrafficSimulation.World.Routing;

/// <summary>
/// What one network says about travelling from one of its pieces onto the next — <b>the whole of what
/// expanding a route needs to know about the ground</b>, so that both agent kinds expand one the same way
/// over the same links.
/// </summary>
/// <remarks>
/// A struct rather than a class at every call site, so the calls below devirtualise and an expansion
/// allocates nothing (rule 2).
/// </remarks>
internal interface IRouteJoins
{
    /// <summary>
    /// Which piece of a run the way behind hands over onto. <b>Nought where nothing joins it at all</b>, so
    /// that an expansion fails on the first piece rather than on a slot chosen to make it fail.
    /// </summary>
    int JoinedAt(ReadOnlySpan<int> pieces, int from);

    /// <summary>
    /// Whether the way behind reaches this piece at all. False ends the chain where it stands, and it is
    /// the network's business to record why.
    /// </summary>
    bool Reaches(int from, int onto);

    /// <summary>
    /// The way travelled <em>between</em> two pieces, or <see cref="RouteChain.NoWay"/> where they are
    /// travelled straight on. A pavement's corner is one of these because a walker is held on the
    /// network's own mitre; a junction's is not, because a driven line assembles its own.
    /// </summary>
    int Between(int from, int onto);
}

/// <summary>
/// <b>A route as the chain of ways it is travelled</b>: the run-links a search returned, expanded into the
/// network's own pieces and taken one at a time.
/// </summary>
/// <remarks>
/// <para>
/// <b>One expansion for both agent kinds</b>, because at this tier they are the same thing — the pieces of
/// each link in order, from where the body joins the first to where the destination stands on the last.
/// What a piece is made of, what shape it is and how a body follows one are the local tier's and differ
/// completely; which pieces, in what order, does not.
/// </para>
/// <para>
/// <b>A way is an integer the network numbers</b>, and the two networks number different things — a road
/// lane, or a pavement stretch and the complement of the corner onto it. Nothing here reads one; they are
/// carried through to whoever laid them.
/// </para>
/// <para>
/// <b>The chain is bounded and says so.</b> A route that will not fit stops, and <c>ranOut</c> is the
/// difference between a route that ends and one that stops — the caller lays the rest again from wherever
/// the body has got to by then.
/// </para>
/// </remarks>
internal static class RouteChain
{
    /// <summary>What a slot of a chain holds where it holds no way of the network: the hop off it onto a doorstep.</summary>
    public const int NoWay = int.MinValue;

    /// <summary>
    /// Expands <paramref name="links"/> into the ways they are travelled as, and answers how many were
    /// written.
    /// </summary>
    /// <param name="fromWay">The way the body is already on, which the chain is joined onto and never holds.</param>
    /// <param name="firstSlot">
    /// Which piece of the first link to begin at, or negative to let the network say
    /// (<see cref="IRouteJoins.JoinedAt"/>). A driver skips the lane under it, having driven it; a walker
    /// begins on the piece it is standing on, the rest of which it has still to cover.
    /// </param>
    /// <param name="ranOut">Whether <paramref name="into"/> filled with route still to come.</param>
    public static int LayInto<TJoins>(
        ref TJoins joins, RunNetwork runs, ReadOnlySpan<int> links, int fromWay, int firstSlot,
        RouteGoal goal, Span<int> into, out bool ranOut)
        where TJoins : struct, IRouteJoins
    {
        var written = 0;
        var last = fromWay;
        var reaches = true;
        ranOut = false;

        for (var index = 0; index < links.Length && reaches && !ranOut; index++)
        {
            var link = links[index];
            var pieces = runs.PiecesOf(link);

            var from = index == 0 && firstSlot >= 0 ? firstSlot : joins.JoinedAt(pieces, last);

            // The last link is only travelled as far as the destination stands along it.
            var to = pieces.Length;
            if (index == links.Length - 1 && link == goal.Link)
            {
                to = Math.Min(to, runs.PieceAt(link, goal.AlongM, out _) + 1);
            }

            for (var slot = from; slot < to; slot++)
            {
                if (written == into.Length)
                {
                    ranOut = true;
                    break;
                }

                var onto = pieces[slot];
                if (!joins.Reaches(last, onto))
                {
                    reaches = false;
                    break;
                }

                // The way between two pieces is a piece of the route in its own right where the network
                // holds one, and it goes in before the piece it leads onto.
                var between = joins.Between(last, onto);
                if (between != NoWay)
                {
                    into[written++] = between;
                    if (written == into.Length)
                    {
                        ranOut = true;
                        break;
                    }
                }

                last = onto;
                into[written++] = last;
            }
        }

        return written;
    }
}
