# Routing — requirements

How every agent gets from where it is to where it is going. **Both agent kinds use the same two tiers and
the same search**; what differs is only what their network is made of — the lane graph
([world/road](../../road/docs/requirements.md)) or the pavement's own
([world/foot](../../foot/docs/requirements.md)).

## The split

| Tier | Answers | Reads | Owned by |
|---|---|---|---|
| **Global** | which *ways* the whole trip uses, end to end | link weights and turn prices — **nothing else** | one planner, over an abstract graph |
| **Local** | how to get from the end of one link onto the next | road or pavement geometry, lane direction, static objects | each agent kind's own |

## Where a link ends

**A link ends where an agent can go more than one way, and nowhere else.** Both ends are fixed points on
the map, shared by every agent of its kind, laid with the town and never touched again.

- **A line, however it bends, ends no link.** A plan cuts a street wherever it wants a junction disc,
  and a body arriving at one of those has exactly one way on — no decision can be made there, you are
  following a road. Both networks are therefore **contracted**: everything between two decisions is one
  link.
- **There is no intersection without links that meet there.** Two ways that cross with no way on between
  them is a place bodies pass through each other and nothing in the town notices (TER-4b).
- **Nothing ends a link for being somewhere a leg is aimed at.** A doorway or a bay is a destination, and
  a destination is a **place on a link**, carried with how far into that link it stands; getting to it off
  the link is the local tier's problem. A leg into a bay names the metre of its lane the bay's mouth stands
  abeam of, and wants no node there.
- **A car park ends no link** (GEN-4h): its bays are joined to nothing, so the street past one is the link it
  would be without it, and turning into a bay is a manoeuvre and not a choice the network offers.

The price of the first rule is real and accepted: **a route can no longer turn round at a bend.** A
two-road junction ends no link, so the way back is taken at a junction with a choice at it, or at a dead
end.

**And no junction turns a route round at all** (TER-5f). The two lanes of one stretch have no turn between
them, so the only links a route may put back to back that way are the two sides of a stretch a car can come
back down some other way: a car park's frontage, where it parks and unparks (`GEN-4l`), and a dead end,
where the road runs out. Both are priced well above three sides of any block, because turning
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

**And it holds no geometry at all**, not so much as a point. A link is a weight and a list of links it may
be left for; where any of it stands on the ground is the local tier's (`RunNetwork`). The search is
therefore a flood and not a directed one — nothing in it may read a coordinate, because a search steered by
where the town happens to have been laid is a search whose answers depend on the graph's numbers agreeing
with ground it is not supposed to know about. What that costs is measured rather than argued: `--bench
census` prints the links a route settles on its way to an answer.

**A link is a way on, not a lane and not a road.** How many lanes a direction carries, which one a body
ends up on, what shape any of it is and what is standing on it are all the local tier's.

**And the cells the search is sectioned by are grown over the graph, not laid over the ground** (`RouteCells`):
a cell is every link nearer one middle link than any other, measured along the links. The search still has
no coordinate to read.

## The search state is a link, never a place

What a turn costs depends on the way the body arrived as well as the way it leaves, so **the cheapest way
to a junction is not a fact about the junction**. Settle where links meet and the planner quietly returns
routes that are not the cheapest — not visibly wrong, just wrong. **One search state per directed link**,
which is also what lets the goal be a *place on a link*. Three consequences:

- **A goal on the link a body is already committed to is still a search.** A link runs one way, so a
  destination twenty metres *behind* is round the block and down this link again. Track the goal link
  apart from the frontier, or a link settled cheaply is never reached again.
- **What a link costs as the *last* one is not its weight**, because the route stops part-way along it.
- **Lights never enter pathfinding** (TLT-2a): a signal wait may not mark a road blocked.

The cells are the one place a search settles anything other than a link, and what they settle is which
cells the link search may enter — never which link a route takes.

## The search is sectioned

**A route is found a section at a time.** The cells the trip crosses are searched first, over the cells'
own graph; then the links of the first few of them in detail, inside a window of those cells and every cell
one way on from them. The route is handed over as far as the first link into the cell past the section, and
the rest is asked for again from there — exactly as a route that ran out of room always was. What one plan
costs is then the window, whatever the length of the trip; a flood over the whole graph costs every link
cheaper than its answer, which is half a town.

- **A trip no longer than a section is searched exactly**, over the whole graph: what that costs is the short
  reach of its own answer, and a window could miss the cheapest way round a corner of the cells it crosses.
- **The section's last turn is chosen with the ground past it in view.** The search reaches into the core of
  the cell after the section — the overlap — before it hands the section over, and that ground is searched
  again by the next section rather than travelled on this one's say.
- **A section is not the cheapest route, and how far it is from one is measured.** The cells' prices are an
  estimate, so a route followed section by section can come out dearer than the one a whole-graph search
  would have found; `--bench census` follows sampled routes to their goals and prints by how much.
- **Nothing the window cannot settle is settled in it.** A window that holds a closed link or a priced-up one
  — the two things laid since the cells were, and so the two the cells cannot see — or one the link search
  finds no way through even a ring wider, is searched again over the whole graph. A route that exists is
  never refused for the cells it was looked for in.
- **The cells never refuse what the links allow.** Every turn between two cells is a way between them, so a
  goal no run of cells reaches is refused without a link search at all.

## The search is asked once a leg, not once a junction

**A leg is routed and then travelled.** The global search runs when the leg is drawn, again where the route
in hand runs out — out of room, or at the end of its section — and again where something has invalidated it — a stretch a leg priced up after getting
nowhere, a destination it gave up for one nearer (CAR-15a). Between those the way ahead is *read*: the
pieces of a link are contracted with the town and expanded into the chain of ways the body travels
(`RouteChain`).

Nothing about a body's own progress is a reason to search again. A car re-deriving its way at every
junction drives exactly the same and costs tens of searches a leg, so the fault is invisible from
outside: what reports it is `RouteSearches` against the legs begun over the same window, bounded by a test
(`ALegIsRoutedAHandfulOfTimes`).

## How a soft rule reaches the planner

`SIM-6` ([docs/requirements.md](../../../../docs/requirements.md#the-two-rule-classes)) binds a rule here
either **as a ban** — the option is absent from the graph rather than costed, so there is no edge of the
walking network that touches a carriageway except a crossing, and none that enters a parking lot at all —
or **as a price**, which distance is meant to outbid.

The consequence for this slice: **a banned option is never in the graph**, so the planner has no lifting
mechanism of its own. Where a ban must lift, the graph is built differently for that search.

**A ban that comes and goes is handed to the search as the links it may not enter** (`RoutePlanner.Plan`'s
`closed`): a road the police have closed (`SRV-10`) is out of the graph for as long as it is closed, and the
caller lifts it for the one agent whose goal lies inside by handing in none (`SIM-6`). **The link a body is
already on is never refused**, since it is where the search starts from and the way off it is what a car
inside a closure needs. It is a flag a link laid with the network and read by the relaxation, so a search
still allocates nothing.
