// What each car's actions keep of it between ticks (agents/car/actions), by id (1024–1151) and by its C# name:
// overtaking_LooksInS(car) is Overtaking._looksInS[car]. Laid once off the town (app/devicetick/TownState.CarActions).
//
// A table kept a holder rather than a car is indexed as the C# indexes it: Overtaking's two shapes a car at
// overtaking_Holder(car, shape), a bay manoeuvre's two pieces at bayManoeuvring_Holder(car, piece). Ground swept once
// (SweptGround) is three tables — its runs, MostEach a holder, how many each holds, and the station it was last swept at.

#ifndef TOWN_CAR_ACTIONS_GLSL
#define TOWN_CAR_ACTIONS_GLSL

#include "world/town/kernels/town_tables.glsl"

// Overtaking's shapes (FromRest, AtSpeed), the bodies a shape passes and the runs its ground is kept in; Switching's;
// a bay manoeuvre's pieces and arcs (BayManoeuvre.MostPieces, MostArcs), and the covers a car's station keeps
// (RibbonAtlas.MostWaysUnderABody).
const uint OvertakingFromRest = 0u;
const uint OvertakingAtSpeed = 1u;
const uint OvertakingShapes = 2u;
const uint OvertakingMostPassedAtOnce = 24u;
const uint OvertakingMostRunsOfAPass = 64u;
const uint SwitchingMostRunsOfASwitch = 64u;
const uint BayManoeuvringMostRunsOfAPiece = 256u;
const uint BayManoeuvreMostPieces = 2u;
const uint BayManoeuvreMostArcs = 8u;
const uint RibbonAtlasMostWaysUnderABody = 48u;

// An Overtake's words, as town_rows.glsl's cars_PassWord holds a car's pass (overtake.glsl's OvertakeRow).
const uint OvertakeWords = 12u;

// SwitchOutcome.
const uint SwitchOutcomeBegun = 0u;
const uint SwitchOutcomeLetsGo = 1u;
const uint SwitchOutcomeWaits = 2u;
const uint SwitchOutcomeLooked = 3u;
const uint SwitchOutcomeUnknown = 4u;
const uint SwitchOutcomeAsks = 5u;

// Switching.RoadShape, a flag a bit.
const uint RoadShapeNone = 0u;
const uint RoadShapeAsks = 1u;
const uint RoadShapeStopsToMoveAcross = 2u;
const uint RoadShapeWants = 4u;
const uint RoadShapeWantsWhenHeldUp = 8u;
const uint RoadShapeWantsWhenFree = 16u;

// ManoeuvreKind and ManoeuvreStage; Manoeuvres.NoBay and NoLane.
const uint ManoeuvreKindNone = 0u;
const uint ManoeuvreKindPark = 1u;
const uint ManoeuvreKindLeave = 2u;
const uint ManoeuvreStageNone = 0u;
const uint ManoeuvreStageShaped = 1u;
const uint ManoeuvreStageAsked = 2u;
const uint ManoeuvreStageBegun = 3u;
const int ManoeuvresNoBay = -1;
const int ManoeuvresNoLane = -1;

// BayManoeuvring.Standing: a car's station read in one rebuild — on which piece, where along it, how many covers it kept.
struct BayManoeuvringStanding
{
    int rebuild;
    int piece;
    float atM;
    int covers;
};

// BayManoeuvre.Shape: how many arcs each piece is, and whether each is driven in reverse.
struct ManoeuvreShape
{
    int firstArcs;
    int secondArcs;
    uint firstReverse;
    uint secondReverse;
};

// The actions' counts since the town was laid, and the rebuild BayManoeuvring's kept stations were read in.
struct CarActionsCounts
{
    int64_t overtakingAsked;
    int64_t overtakingWithdrawn;
    int64_t overtakingMade;
    int64_t switchingAsked;
    int64_t switchingWithdrawn;
    int64_t switchingMade;
    int64_t backingUpCarTicksBackingUp;
    int64_t backingUpCarTicksBlocked;
    int64_t bayManoeuvringAsked;
    int64_t bayManoeuvringWithdrawn;
    int64_t bayManoeuvringBegun;
    int64_t bayManoeuvringParkedNoseIn;
    int64_t bayManoeuvringParkedBackedIn;
    double bayManoeuvringStreetM;
    int bayManoeuvringRebuild;
    int pad;
};

layout(buffer_reference, std430, buffer_reference_align = 4) buffer BayManoeuvringStandingRows { BayManoeuvringStanding r[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) buffer ManoeuvreShapeRows { ManoeuvreShape r[]; };
layout(buffer_reference, std430, buffer_reference_align = 8) buffer CarActionsCountsRows { CarActionsCounts r[]; };

const uint CarActionsEntriesId = 1024u;
const uint DrivingGroundPlanHoldId = 1025u;
const uint RejoiningLooksInSId = 1026u;
const uint OvertakingShapesId = 1027u;
const uint OvertakingSweptRunsId = 1028u;
const uint OvertakingSweptCountId = 1029u;
const uint OvertakingSweptStationAtMId = 1030u;
const uint OvertakingAskedRunsId = 1031u;
const uint OvertakingAskedCountId = 1032u;
const uint OvertakingAskedStationAtMId = 1033u;
const uint OvertakingPassedId = 1034u;
const uint OvertakingPassedCountId = 1035u;
const uint OvertakingAskingId = 1036u;
const uint OvertakingLooksInSId = 1037u;
const uint OvertakingWaitedSId = 1038u;
const uint OvertakingDecidesInSId = 1039u;
const uint OvertakingLooksId = 1040u;
const uint OvertakingAskedAsId = 1041u;
const uint OvertakingLaidForId = 1042u;
const uint OvertakingRoomMId = 1043u;
const uint OvertakingRoomLaneId = 1044u;
const uint SwitchingSweptRunsId = 1045u;
const uint SwitchingSweptCountId = 1046u;
const uint SwitchingSweptStationAtMId = 1047u;
const uint SwitchingAskedRunsId = 1048u;
const uint SwitchingAskedCountId = 1049u;
const uint SwitchingAskedStationAtMId = 1050u;
const uint SwitchingInwardId = 1051u;
const uint SwitchingAsideMId = 1052u;
const uint SwitchingRoomToMId = 1053u;
const uint SwitchingFromRestMId = 1054u;
const uint SwitchingLooksInSId = 1055u;
const uint SwitchingAskedAsId = 1056u;
const uint SwitchingLaidForId = 1057u;
const uint BackingUpAskedMId = 1058u;
const uint BayManoeuvringPiecesRunsId = 1059u;
const uint BayManoeuvringPiecesCountId = 1060u;
const uint BayManoeuvringPiecesStationAtMId = 1061u;
const uint BayManoeuvringAskedAsId = 1062u;
const uint BayManoeuvringSweptForId = 1063u;
const uint BayManoeuvringLaidForId = 1064u;
const uint BayManoeuvringStandingOfId = 1065u;
const uint BayManoeuvringStandingCoversId = 1066u;
const uint ManoeuvresArcsId = 1067u;
const uint ManoeuvresShapeId = 1068u;
const uint ManoeuvresKindId = 1069u;
const uint ManoeuvresStageId = 1070u;
const uint ManoeuvresBayId = 1071u;
const uint ManoeuvresLaneId = 1072u;
const uint ManoeuvresPieceId = 1073u;
const uint ManoeuvresStreetMId = 1074u;
const uint CarActionsCountsId = 1075u;

// CarActions._entries: how many times the car was handed to another action (EntriesOf).
#define carActions_Entries(c) TOWN(Uints, CarActionsEntriesId).v[c]

// DrivingGround.PlanHold (TownWorld._carHold): the hold each car's plan laid this rebuild.
#define drivingGround_PlanHold(c) TOWN(Ints, DrivingGroundPlanHoldId).v[c]

#define rejoining_LooksInS(c) TOWN(Floats, RejoiningLooksInSId).v[c]

// Overtaking: a shape at its holder, word for word an Overtake (Lane, OutM, OutStepM, BackM, BackStepM, AsideM, OutMps,
// BackMps, ClearsM, Begun).
#define overtaking_Holder(c, shape) ((c) * OvertakingShapes + (shape))
#define overtaking_ShapeLane(h) TOWN(Ints, OvertakingShapesId).v[OvertakeWords * (h)]
#define overtaking_ShapeOutM(h) TOWN(Floats, OvertakingShapesId).v[OvertakeWords * (h) + 1u]
#define overtaking_ShapeOutStepM(h) TOWN(Floats, OvertakingShapesId).v[OvertakeWords * (h) + 2u]
#define overtaking_ShapeBackM(h) TOWN(Floats, OvertakingShapesId).v[OvertakeWords * (h) + 3u]
#define overtaking_ShapeBackStepM(h) TOWN(Floats, OvertakingShapesId).v[OvertakeWords * (h) + 4u]
#define overtaking_ShapeAsideM(h) TOWN(Floats, OvertakingShapesId).v[OvertakeWords * (h) + 5u]
#define overtaking_ShapeOutMps(h) TOWN(Floats, OvertakingShapesId).v[OvertakeWords * (h) + 6u]
#define overtaking_ShapeBackMps(h) TOWN(Floats, OvertakingShapesId).v[OvertakeWords * (h) + 7u]
#define overtaking_ShapeClearsM(h) TOWN(Floats, OvertakingShapesId).v[OvertakeWords * (h) + 8u]
#define overtaking_ShapeBegun(h) TOWN(Uints, OvertakingShapesId).v[OvertakeWords * (h) + 9u]
#define overtaking_SweptRun(h, i) TOWN(SweptRunRows, OvertakingSweptRunsId).r[(h) * OvertakingMostRunsOfAPass + (i)]
#define overtaking_SweptCount(h) TOWN(Ints, OvertakingSweptCountId).v[h]
#define overtaking_SweptStationAtM(h) TOWN(Floats, OvertakingSweptStationAtMId).v[h]
#define overtaking_AskedRun(h, i) TOWN(SweptRunRows, OvertakingAskedRunsId).r[(h) * OvertakingMostRunsOfAPass + (i)]
#define overtaking_AskedCount(h) TOWN(Ints, OvertakingAskedCountId).v[h]
#define overtaking_AskedStationAtM(h) TOWN(Floats, OvertakingAskedStationAtMId).v[h]

// A body a shape passes (LaneClaim) as a claim row, its way the one it was met on (_passedOn).
#define overtaking_Passed(h, i) TOWN(ClaimRows, OvertakingPassedId).r[(h) * OvertakingMostPassedAtOnce + (i)]
#define overtaking_PassedOn(h, i) TOWN(ClaimRows, OvertakingPassedId).r[(h) * OvertakingMostPassedAtOnce + (i)].way
#define overtaking_PassedCount(h) TOWN(Ints, OvertakingPassedCountId).v[h]
#define overtaking_Asking(c) TOWN(Ints, OvertakingAskingId).v[c]
#define overtaking_LooksInS(c) TOWN(Floats, OvertakingLooksInSId).v[c]
#define overtaking_WaitedS(c) TOWN(Floats, OvertakingWaitedSId).v[c]
#define overtaking_DecidesInS(c) TOWN(Floats, OvertakingDecidesInSId).v[c]
#define overtaking_Looks(c) TOWN(Uints, OvertakingLooksId).v[c]
#define overtaking_AskedAs(c) TOWN(Int64s, OvertakingAskedAsId).v[c]
#define overtaking_LaidFor(c) TOWN(Int64s, OvertakingLaidForId).v[c]
#define overtaking_RoomM(c) TOWN(Floats, OvertakingRoomMId).v[c]
#define overtaking_RoomLane(c) TOWN(Ints, OvertakingRoomLaneId).v[c]

// Switching, a car its own holder.
#define switching_SweptRun(c, i) TOWN(SweptRunRows, SwitchingSweptRunsId).r[(c) * SwitchingMostRunsOfASwitch + (i)]
#define switching_SweptCount(c) TOWN(Ints, SwitchingSweptCountId).v[c]
#define switching_SweptStationAtM(c) TOWN(Floats, SwitchingSweptStationAtMId).v[c]
#define switching_AskedRun(c, i) TOWN(SweptRunRows, SwitchingAskedRunsId).r[(c) * SwitchingMostRunsOfASwitch + (i)]
#define switching_AskedCount(c) TOWN(Ints, SwitchingAskedCountId).v[c]
#define switching_AskedStationAtM(c) TOWN(Floats, SwitchingAskedStationAtMId).v[c]
#define switching_Inward(c) TOWN(Uints, SwitchingInwardId).v[c]
#define switching_AsideM(c) TOWN(Floats, SwitchingAsideMId).v[c]
#define switching_RoomToM(c) TOWN(Floats, SwitchingRoomToMId).v[c]
#define switching_FromRestM(c) TOWN(Floats, SwitchingFromRestMId).v[c]
#define switching_LooksInS(c) TOWN(Floats, SwitchingLooksInSId).v[c]
#define switching_AskedAs(c) TOWN(Int64s, SwitchingAskedAsId).v[c]
#define switching_LaidFor(c) TOWN(Int64s, SwitchingLaidForId).v[c]

#define backingUp_AskedM(c) TOWN(Floats, BackingUpAskedMId).v[c]

// BayManoeuvring: a piece's ground at its holder, and the station a car stood at with the covers it kept.
#define bayManoeuvring_Holder(c, piece) ((c) * BayManoeuvreMostPieces + (piece))
#define bayManoeuvring_PiecesRun(h, i) TOWN(SweptRunRows, BayManoeuvringPiecesRunsId).r[(h) * BayManoeuvringMostRunsOfAPiece + (i)]
#define bayManoeuvring_PiecesCount(h) TOWN(Ints, BayManoeuvringPiecesCountId).v[h]
#define bayManoeuvring_PiecesStationAtM(h) TOWN(Floats, BayManoeuvringPiecesStationAtMId).v[h]
#define bayManoeuvring_AskedAs(c) TOWN(Int64s, BayManoeuvringAskedAsId).v[c]
#define bayManoeuvring_SweptFor(c) TOWN(Int64s, BayManoeuvringSweptForId).v[c]
#define bayManoeuvring_LaidFor(c) TOWN(Int64s, BayManoeuvringLaidForId).v[c]
#define bayManoeuvring_StandingOf(c) TOWN(BayManoeuvringStandingRows, BayManoeuvringStandingOfId).r[c]
#define bayManoeuvring_StandingCover(c, i) \
    TOWN(WayCoverRows, BayManoeuvringStandingCoversId).r[(c) * RibbonAtlasMostWaysUnderABody + (i)]

// Manoeuvres: a car's shape's arcs, both pieces back to back (two vec4s an arc, as arc.glsl's Arc), and the rest.
#define manoeuvres_ArcA(c, i) TOWN(ArcRows, ManoeuvresArcsId).r[2u * ((c) * BayManoeuvreMostArcs + (i))]
#define manoeuvres_ArcB(c, i) TOWN(ArcRows, ManoeuvresArcsId).r[2u * ((c) * BayManoeuvreMostArcs + (i)) + 1u]
#define manoeuvres_Shape(c) TOWN(ManoeuvreShapeRows, ManoeuvresShapeId).r[c]
#define manoeuvres_Kind(c) TOWN(Uints, ManoeuvresKindId).v[c]
#define manoeuvres_Stage(c) TOWN(Uints, ManoeuvresStageId).v[c]
#define manoeuvres_Bay(c) TOWN(Ints, ManoeuvresBayId).v[c]
#define manoeuvres_Lane(c) TOWN(Ints, ManoeuvresLaneId).v[c]
#define manoeuvres_Piece(c) TOWN(Ints, ManoeuvresPieceId).v[c]
#define manoeuvres_StreetM(c) TOWN(Floats, ManoeuvresStreetMId).v[c]

// The counts, and BayManoeuvring._rebuild.
#define carActionsCounts TOWN(CarActionsCountsRows, CarActionsCountsId).r[0]
#define overtaking_Asked carActionsCounts.overtakingAsked
#define overtaking_Withdrawn carActionsCounts.overtakingWithdrawn
#define overtaking_Made carActionsCounts.overtakingMade
#define switching_Asked carActionsCounts.switchingAsked
#define switching_Withdrawn carActionsCounts.switchingWithdrawn
#define switching_Made carActionsCounts.switchingMade
#define backingUp_CarTicksBackingUp carActionsCounts.backingUpCarTicksBackingUp
#define backingUp_CarTicksBlocked carActionsCounts.backingUpCarTicksBlocked
#define bayManoeuvring_Asked carActionsCounts.bayManoeuvringAsked
#define bayManoeuvring_Withdrawn carActionsCounts.bayManoeuvringWithdrawn
#define bayManoeuvring_Begun carActionsCounts.bayManoeuvringBegun
#define bayManoeuvring_ParkedNoseIn carActionsCounts.bayManoeuvringParkedNoseIn
#define bayManoeuvring_ParkedBackedIn carActionsCounts.bayManoeuvringParkedBackedIn
#define bayManoeuvring_StreetM carActionsCounts.bayManoeuvringStreetM
#define bayManoeuvring_Rebuild carActionsCounts.bayManoeuvringRebuild

#endif
