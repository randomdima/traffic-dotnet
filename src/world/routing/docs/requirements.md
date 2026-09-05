# Routing — requirements

How every agent gets from where it is to where it is going. **Both agent kinds use the same two tiers and
the same search**; what differs is only what their network is made of — the lane graph
([world/road](../../road/docs/requirements.md)) or the pavement's own
([world/foot](../../foot/)).

## The split

| Tier | Answers | Reads | Owned by |
|---|---|---|---|
| **Global** | which *ways* the whole trip uses, end to end | link weights, turn prices, where each link's own two ends are — **nothing else** | one planner, over an abstract graph |
| **Local** | how to get from the end of one link onto the next | road or pavement geometry, lane direction, static objects | each agent kind's own |

Below the local tier is the **manoeuvre**, which is more detailed again: it is what waits at a red light,
picks its way down a bay to a car door, gives way. A local line is a combination of planned manoeuvres; a
manoeuvre is not a line at all.

## Where a link ends

**A link ends where an agent can go more than one way, and nowhere else.** Both ends are fixed points on
the map, shared by every agent of its kind, laid with the town and never touched again.

- **A line, however it bends, ends no link.** A plan cuts a street wherever it wants a junction disc,
  and a body arriving at one of those has exactly one way on — no decision can be made there, you are
  following a road. Both networks are therefore **contracted**: everything between two decisions is one
  link.
- **There is no intersection without links that meet there.** Two ways that cross with no way on between
  them is a place bodies pass through each other and nothing in the town notices (TER-4b).
- **A car park does not end a link because it is a car park.** Nor does a doorway or a bay: a destination
  is a **place on a link**, and getting to it off the link is the local tier's problem and then a
  manoeuvre's. What puts the ends of a parking section on the network is that a leg has to be able to name
  them (GEN-4h), not that a decision is taken there.

The price of the first rule is real and accepted: **a route can no longer turn round at a bend.** A
two-road junction ends no link, so the way back is taken at a junction with a choice at it, or at a dead
end.

**And no junction turns a route round at all** (TER-5f). The two lanes of one stretch have no turn between
them, so the only links a route may put back to back that way are the two sides of a stretch a car can come
back down some other way: a car park's frontage, where it parks and unparks (`GEN-4l`), and a dead end,
where it works itself round (`P-19`). Both are priced well above three sides of any block, because turning
round is what a driver does when there is no block to take.

## What the global tier may not know

**The travel graph is a standalone abstract weighted directed graph of links and nothing more** — directed
links, a weight on each, the links each may be left for, and a price on each of those turns. It could not
tell a four-lane boulevard from a zebra crossing, and that is the point: *which way to go* is a question
about the network and does not get a better answer for being asked in metres.

**There is no node table.** A link states which links it may be left for, the way a lane states which lanes
it may be left for; where several of them meet is not a record the graph keeps, because a junction is what
a set of crossed ways happens to make (TER-5d) and a search able to name one would be entitled to settle
it. Where the ways of a network meet is derived from what joins them, once, and is the fine tier's
(`LanePlaces`).

Its one geometric fact is **a link's own two ends**, used for exactly two things — aiming a search and
bounding it. **Enforce at insertion the two relations the second depends on: a link is never priced below
the span between its ends, and two links are only joined where one ends exactly where the other starts.**
Together those make the straight line an admissible heuristic and therefore let the search be A\* rather
than a flood; drop the second and a route's spans no longer add up to the line the first is measured
against.

**A link is a way on, not a lane and not a road.** How many lanes a direction carries, which one a body
ends up on, what shape any of it is and what is standing on it are all the local tier's.

## The search state is a link, never a place

What a turn costs depends on the way the body arrived as well as the way it leaves, so **the cheapest way
to a junction is not a fact about the junction**. Settle where links meet and the planner quietly returns
routes that are not the cheapest — not visibly wrong, just wrong. **One search state per directed link**,
which is also what lets the goal be a *place on a link*.

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
