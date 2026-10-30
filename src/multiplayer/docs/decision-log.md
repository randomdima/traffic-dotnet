# Multiplayer — decisions

Why this slice reads as it does. What it *must* do is [requirements.md](requirements.md); the survey it was built from
is [network/](../../../network/index.md).

## A player is a person on foot, and its area is round whatever it is (2026-10-30)

The owner asked for players to start, and start again after dying, as somebody new on the pavement, with `E` or
`Enter` to get into a car or a train and out again — putting out whoever drove it. That made "the player's car" no
longer a thing every player has, and **MP-1 (`P0`) named its area as the square round the player's car**. The owner
amended it, in as many words, to the area round the player — its body, or the car or train it is in — with the
guarantee itself unchanged: nothing outside that area is ever sent.

The owner also settled the two choices the feature turned on: **every vehicle may be taken**, a service vehicle
included, its errand waiting under the hand as it does under the reader's; and **a press in a moving vehicle gets out
at once**, the vehicle let go of where it is.

## A player's page is sized by its area, not by the town (2026-10-30)

The owner asked for the page's memory, device memory and network use cut to what a player needs. **A page lays a
`TownScene`**, the groundwork as far as the lights and the crossings' lamps, and never a `TownWorld`: no fleet, route
network, atlas or world. **The replica's fleets, its snapshot ring and the sprites' room are a snapshot long**
(`MostMoversSent`), a mover keeping a slot while it is drawn. Saltbay's page stands in 265 MiB where a page laying the
whole town stands in 351.

**A player's atlas packs what its town draws** (`TownSprites.SheetsAsShown`): a roof or a prop its town stands none of
is one clear texel, and every other sheet is whole, held in BC7 or ASTC (render log, 2026-10-30). **A player zooms in as
far as a reader does**: the owner ruled out packing a player's cars and walkers at half their texels behind a closer
zoom stop, which took a second copy of every one of them into each sheet pack. Saltbay's player atlas is seven pages
where it was four and a watched page's is eight; a pack is 78 MB where it was 85.

**The area stays a square round the player**, its widest view, its camera's longest lead and drag and the margin — the
owner ruled out centring it on the lead or shaping it to the screen.

**A snapshot sends steps where they are short** (protocol 4): a move in six, nine or the margin's bits, a heading as its
turn, a road wheel or a lean as its nudge at a coarser quantum of its own, and a position to two centimetres. **The
baseline stays the acknowledged one**, though a WebSocket loses nothing, so the codec is ready for datagrams
(network/mvp-plan.md); what bounds how stale it gets is MP-14's window, which also stops a hidden tab being buried.

**A player sends its keys when they say something new**, at most once a tick and at least once a keep-alive, rather than
every frame: a 144 Hz page sent seven inputs for each one the server read.

**A served town's page is cached**: a framework file named by its hash for good, everything else revalidated by its tag,
so joining again downloads nothing that has not changed — the art alone is 28 MB.

## The bodies are laid with the town, not added to it

The roster is laid once and never grown (network/fit.md, "identity is free"), and a player's mover number has to mean
the same thing to every page for the whole run. So a served town lays `BodiesPerSeat` bodies a seat, offstage, after
everybody else — a container that is nowhere, so PHY-7 already says what they are: not drawn, not struck, not stepped.
A town nobody is served lays none, which is what keeps every other town's roster, tests and figures as they were.

**More than one a seat, because a body a player leaves lying is the town's.** It is rescued, treated and put through
the hospital like anybody, and only then comes back offstage; a seat stood up afresh takes one of its others meanwhile.
In a town with no hospital, or one whose call was given up, nobody comes — so a seat with nothing offstage clears away
one of its own lying where nobody is coming for it, which is the one case a body leaves the road unrescued.

**Respawned off a hospital's door**, the nearest to where the player fell: a body walked out of a building is the
town's own way of putting somebody on the pavement (PHY-7a), and it puts a new player somewhere it can be found.

## Getting in is a press within reach, not a walk to a door

A town's owner walks to the car's way in and is let in by it (PER-29); a player presses a key beside a vehicle and is
in. A walk the player does not steer is a body the player has lost, and a door to find is a target a pointer-less page
cannot aim at. The reach is to the vehicle's body, edge to edge, so a bus and a hatchback are as easy to get into.

**Getting out is never refused** (PHY-7a's one exception): a player that presses and stays in reads as a key that does
nothing. Where the pavement beside it is taken, the body is put down off the door, as a crash's are.
