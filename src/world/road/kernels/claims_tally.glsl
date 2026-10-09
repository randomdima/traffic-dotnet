// How much the claims hold this rebuild, word by word (app/devicetick/DeviceClaims.Tally): the counts every kernel over
// the rows or the holds stops at, and the workgroups each is dispatched with — written by whoever laid the claims, the
// host or the rebuild's own numbering (world/town/kernels/rebuild_*.comp), so a count the host never sees is still the
// one every kernel stops at.

// The rows laid, and the holds.
const uint TallyRows = 0u;
const uint TallyHolds = 1u;

// The rebuild's own: the bodies' pieces before they are grown into one another, the bodies' rows once they are, the
// rows once every pass is laid after them, and the holds and the rows once every plan is.
const uint TallyCandidates = 2u;
const uint TallyBodies = 3u;
const uint TallyPassesEnd = 4u;
const uint TallyPlannedHolds = 5u;
const uint TallyPlannedRows = 6u;

// Where each indirect dispatch's three group counts start: over the bodies' pieces, the rows and the holds.
const uint TallyCandidatesGroups = 8u;
const uint TallyRowsGroups = 11u;
const uint TallyHoldsGroups = 14u;

// The rooms a rebuild numbered on the device lays into, as the host gives them each rebuild — the rows, a car's plan and a
// walker's walk — and the most any one car's plan and any one walker's walk came to.
const uint TallyRowRoom = 17u;
const uint TallyPieceRoom = 18u;
const uint TallyProposedRoom = 19u;
const uint TallyMostPieces = 20u;
const uint TallyMostProposed = 21u;

// Not nought where something laid passed its room: every row and hold filed after is left unfiled, every count the
// kernels stop at is nought, and what a rebuild hands on to the agents is left as the last one left it — the rebuild did
// not happen, and is laid again with more room (app/devicetick/DeviceRebuild).
const uint TallyOverflowed = 22u;

// The rows once the backing is laid after the answers (rebuild_backing.comp) — at most, where it passed the room.
const uint TallyBackedRows = 23u;

// How many rows the lists were last laid from: the rows still on a way (claims_lists.glsl), the length of the order.
const uint TallyListed = 24u;
