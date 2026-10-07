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
