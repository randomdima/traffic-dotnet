# Parking — requirements

What a bay is, where a lot may stand, and what must be true of one before a car aims at it. Which bay a
trip claims is the walker's ([agents/person](../../../agents/person/docs/requirements.md)); how a car
gets into one is the driver's catalogue
([agents/car](../../../agents/car/docs/requirements.md)).

The `GEN-` rules below are laid by whatever generates a town. **This project reads plans and does not lay
them** ([citygen](../../../citygen/docs/requirements.md)), so they bind the exporter and are checked here
only in the sense that a bay which fails them cannot be used.

**GEN-4** Every parking space is reachable by car from the road network and by pedestrians from walkable
terrain, and is **enterable *and* exitable by a legal manoeuvre**, reverse permitted.

**GEN-4e** **The way in is the bay's and not the car's**: where a walker is aimed to reach a car parked in
a space is a fact about that space, settled with the ground it was painted on, and it is the ground off the
driver's door of a body standing square in it. Read instead off wherever the car has actually come to rest,
the point moves whenever anything nudges the body, and a walk already under way is re-planned round the lot
by a shove nobody chose.

**GEN-4b** Parking is laid as **lots** — a handful of spaces each, every space square to its kerb — and
the count is whatever satisfies the relation that matters: **every building stands within a walking
distance of a lot**. A lot is an oriented rectangle laid along the chord of the kerb it hangs off, offered
only where that kerb stays close to its own chord over the lot's length. The promise is not "a lot per
building" but a density: any scan of frontage carries roughly as many bays as buildings.

**GEN-4c** A parking space exceeds the car footprint by the clearance margin on all sides, and all of
that ground is the lot's.

**GEN-4d** A lot keeps its distance, both figures measured **along the kerb it hangs off**: clear of a
junction, on top of everything the junction already takes, so a car park's flank is not in the face of
anybody waiting to turn out; and clear of the next lot, claimed along the lot's own bearing only and
tested both ways round the pair — two lots facing each other across a carriageway are the two sides of a
street and stay legal, while two sharing a kerb read as one long apron and do not.

That every space is demonstrably enterable and leavable is `VER-2`, in
[docs/verification.md](../../../../docs/verification.md#the-verification-intentions).

## What this slice must produce

- A registry of every lot and bay with claim / release / who-holds-it, so that a bay is claimed **before**
  a car is routed to it and released when the car leaves.
- Which lane a bay is entered from, which is arithmetic off the lot's own bearing and not a stored
  choice.
