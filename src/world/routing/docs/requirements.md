# Routing — requirements

How every agent gets from where it is to where it is going. **Both agent kinds use the same two tiers and
the same search**; what differs is only what their network is made of — the lane graph
([world/road](../../road/docs/requirements.md)) or the pavement's own
([world/foot](../../foot/)).

## The split

| Tier | Answers | Reads | Owned by |
|---|---|---|---|
| **Global** | which *ways* the whole trip uses, end to end | link weights, turn prices, node anchors — **nothing else** | one planner, over an abstract graph |
| **Local** | how to get from one node to the next | road or pavement geometry, lane direction, static objects | each agent kind's own |

Below the local tier is the **manoeuvre**, which is more detailed again: it is what waits at a red light,
picks its way down a bay to a car door, gives way. A local path is a combination of planned manoeuvres; a
manoeuvre is not a path at all.

## What a node is

**A node is a place an agent can go more than one way, and nothing else is a node.** Every one is a fixed
point on the map, shared by every agent of its kind, laid with the town and never touched again.

- **A line, however it bends, produces no nodes.** A plan cuts a street wherever it wants a junction disc,
  and a body arriving at one of those has exactly one way on — no decision can be made there, you are
  following a road. Both networks are therefore **contracted**: everything between two decisions is one
  link.
- **There is no intersection without a node.** Two ways that cross with no node between them is a place
  bodies pass through each other and nothing in the town notices (TER-4b).
- **A car park is not a node.** Nor is a doorway, a bay, or anything else a trip *ends* at: a destination
  is a **place on a link**, and getting to it off the link is the local tier's problem and then a
  manoeuvre's.

The price of the first rule is real and accepted: **a route can no longer turn round at a bend.** A
two-road junction is not a node, so the way back is taken at a junction with a choice at it, or at a dead
end.

**And no junction turns a route round at all** (TER-5f). The two lanes of one stretch have no turn between
them, so the only links a route may put back to back that way are the two sides of a stretch a car can come
back down some other way: a car park's frontage, where it parks and unparks (`GEN-4l`), and a dead end,
where it works itself round (`P-19`). Both are priced well above three sides of any block, because turning
round is what a driver does when there is no block to take.

## What the global tier may not know

**The travel graph is a standalone abstract weighted directed graph and nothing more** — nodes, directed
links, a weight on each link, a price on each turn out of one. It could not tell a four-lane boulevard
from a zebra crossing, and that is the point: *which way to go* is a question about the network and does
not get a better answer for being asked in metres.

Its one geometric fact is a **node anchor**, used for exactly two things — finding the node nearest a
place, and bounding the search. **Enforce at insertion the relation the second depends on: a link is never
priced below the span between its two anchors.** That is what makes the straight line an admissible
heuristic and therefore what lets the search be A\* rather than a flood.

**A link is a way on, not a lane and not a road.** How many lanes a direction carries, which one a body
ends up on, what shape any of it is and what is standing on it are all the local tier's.

## The search state is a link, never a node

What a turn costs depends on the way the body arrived as well as the way it leaves, so **the cheapest way
to a junction is not a fact about the junction**. Settle nodes and the planner quietly returns routes
that are not the cheapest — not visibly wrong, just wrong. **One search state per directed link**, which
is also what lets the goal be a *place on a link*.

Three consequences, each a bug before it was a rule:

- **A goal on the link a body is already committed to is still a search.** A link runs one way, so a
  destination twenty metres *behind* is round the block and down this link again. Track the goal link
  apart from the frontier, or a link settled cheaply is never reached again.
- **What a link costs as the *last* one is not its weight**, because the route stops part-way along it.
- **Lights never enter pathfinding** (TLT-2a): a signal wait may not mark a road blocked.

## The search is asked once a leg, not once a junction

**A leg is routed and then driven.** The global search runs when the leg is drawn, again where the route
in hand runs out, and again where something has invalidated it — a stretch priced up by `E-7`, a
destination given up by `E-6`. Between those the way ahead is *read*: the pieces of a link are contracted
with the town and copied into the lane queue, and the geometry over them is assembled once per lane the
body leaves.

Nothing about a body's own progress is a reason to search again. A car re-deriving its way at every
junction drives exactly the same and costs tens of searches a leg, so the fault is invisible from
outside: what reports it is `RouteSearches` against the legs begun over the same window, printed by
`--bench maneuvers` and bounded by a test.

## How a soft rule reaches the planner

`SIM-6` ([docs/requirements.md](../../../../docs/requirements.md#the-two-rule-classes)) binds a rule here
either **as a ban** — the option is absent from the graph rather than costed, so there is no edge of the
walking network that touches a carriageway except a crossing, and none that enters a parking lot at all —
or **as a price**, which distance is meant to outbid.

The consequence for this slice: **a banned option is never in the graph**, so the planner has no lifting
mechanism of its own. Where a ban must lift, the graph is built differently for that search.
