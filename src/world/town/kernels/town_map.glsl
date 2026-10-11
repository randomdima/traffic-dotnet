// The static map the town's decisions read, by id (640–767) and by the C# name of what keeps it: roads_LaneWidthM(lane)
// is TownWorld._roads.LaneWidthM[lane] (RoadGraph, its LaneLines' arrays), driving_ and walking_ the two networks' (their
// RunNetworks drivingRuns_ and walkingRuns_), foot_ the pavement's fine graph, ways_ the one numbering of every way,
// crossingEdges_, marks_ (WayCrossings), furniture_ (LaneFurniture), signals_ (SignalService) and the town's district
// streets. Laid once off the town (app/devicetick/TownState.Map) and never written. A run of a CSR table is from its
// offsets(i) up to offsets(i + 1); an arc is two vec4s as arc.glsl's Arc. What is asked of these is
// town_map_queries.glsl's.

#ifndef TOWN_MAP_GLSL
#define TOWN_MAP_GLSL

#include "world/town/kernels/town_tables.glsl"
#include "world/road/kernels/crossed_section.glsl"

// LaneTurn, WayKind, FootEdgeKind.
const uint LaneTurnStraight = 0u;
const uint LaneTurnNearSide = 1u;
const uint LaneTurnFarSide = 2u;
const uint WayKindLane = 0u;
const uint WayKindConnector = 1u;
const uint WayKindFootway = 2u;
const uint WayKindMitre = 3u;
const uint FootEdgeKindPavement = 0u;
const uint FootEdgeKindCrossing = 1u;

// ClaimPriority, every rung; LaneRoster.
const uint ClaimPriorityHard = 0u;
const uint ClaimPriorityCommitted = 1u;
const uint ClaimPrioritySpecial = 2u;
const uint ClaimPrioritySignal = 4u;
const uint ClaimPriorityAfoot = 5u;
const uint ClaimPriorityFirmStraight = 6u;
const uint ClaimPriorityFirm = 7u;
const uint ClaimPriorityFirmAcross = 8u;
const uint ClaimPriorityBacking = 9u;
const uint LaneRosterDriving = 0u;
const uint LaneRosterWalking = 1u;
const uint LaneRosterSignal = 2u;

// RoadGraph.NoLane and NoConnector; TravelGraph.NoLink; WalkingNetwork.NoTurn and NoLane; RouteChain.NoWay;
// SignalService.NoAxis; RibbonMarks.NoZebra; CityPlan.NoRecord; CityPlan.RoadArrays.Ground.
const int RoadGraphNoLane = -1;
const int RoadGraphNoConnector = -1;
const int TravelGraphNoLink = -1;
const int WalkingNetworkNoTurn = -1;
const int WalkingNetworkNoLane = -1;
const int RouteChainNoWay = int(0x80000000u);
const int SignalServiceNoAxis = -1;
const int RibbonMarksNoZebra = -1;
const int CityPlanNoRecord = -1;
const uint RoadArraysGround = 0u;

// How much of each the map has and where each block of the ways' one numbering begins; the district wheel
// (CityPlan.Districts, DistrictWheel); whether any road is driven on a level of its own.
struct MapState
{
    int laneCount;
    int connectorCount;
    int junctionCount;
    int firstRoadside;
    int placeCount;
    int mostLanesOfAWay;
    int drivingLinkCount;
    int walkingEdgeCount;
    int walkingTurnCount;
    int walkingLinkCount;
    int wayCount;
    int firstFootwayWay;
    int firstMitreWay;
    int signalJunctionCount;
    int signalCrossingCount;
    uint levelled;
    vec2 districtHubM;
    float districtRingRadiusM;
    float districtFirstSpokeRad;
    int districtSpokes;
    int pad0;
    int pad1;
    int pad2;
};

layout(buffer_reference, std430, buffer_reference_align = 4) buffer CrossedSectionRows { CrossedSection r[]; };
layout(buffer_reference, std430, buffer_reference_align = 8) buffer MapStateRows { MapState r[]; };

// One of RoadGraph's lane indexes (ChainIndex) as it stands: its window's cells (GridWindow) at their level (cells a
// metre, a cell's metres), and how many chains it holds. Its slots' chains and boxes, and its cells' runs of slots, are
// tables of their own (RoadsIndexIds of them an index, from its first); a chain's arcs and length are its lane's.
struct ChainIndexWindow
{
    int fromX;
    int fromY;
    int width;
    int height;
    float perCellM;
    float cellM;
    int chains;
    int pad;
};

// RoadGraph's four lane indexes (RoadsIndexNearest …), and whether it keeps the carriageway's a level at a time.
struct LaneIndexState
{
    ChainIndexWindow indexes[4];
    uint levelled;
    uint pad0;
    uint pad1;
    uint pad2;
};

layout(buffer_reference, std430, buffer_reference_align = 4) buffer LaneIndexStateRows { LaneIndexState r[]; };

const uint RoadsLaneRoadId = 640u;
const uint RoadsLaneWidthMId = 641u;
const uint RoadsLaneFromJunctionId = 642u;
const uint RoadsLaneToJunctionId = 643u;
const uint RoadsLaneForwardId = 644u;
const uint RoadsLaneLengthMId = 645u;
const uint RoadsLaneLevelId = 646u;
const uint RoadsLaneReverseId = 647u;
const uint RoadsLaneOverOneLineId = 648u;
const uint RoadsLaneCrossesToPassId = 649u;
const uint RoadsLaneFromKerbId = 650u;
const uint RoadsLaneInwardId = 651u;
const uint RoadsLaneOutwardId = 652u;
const uint RoadsLaneIsBayId = 653u;
const uint RoadsLaneArcOffsetsId = 654u;
const uint RoadsLaneArcsId = 655u;
const uint RoadsConnectorAtId = 656u;
const uint RoadsConnectorToLaneId = 657u;
const uint RoadsConnectorFromLaneId = 658u;
const uint RoadsConnectorKindId = 659u;
const uint RoadsConnectorArcOffsetsId = 660u;
const uint RoadsConnectorArcsId = 661u;
const uint RoadsConnectorLengthMId = 662u;
const uint RoadsBreaksTheLineId = 663u;
const uint RoadsJunctionOutOffsetsId = 664u;
const uint RoadsJunctionOutLanesId = 665u;
const uint RoadsJunctionInOffsetsId = 666u;
const uint RoadsJunctionInLanesId = 667u;
const uint RoadsPlaceOfEndId = 668u;
const uint RoadsPlaceAnchorMId = 669u;
const uint RoadsPlaceLeavingAtId = 670u;
const uint RoadsPlaceLeavingId = 671u;
const uint RoadsPlaceArrivingAtId = 672u;
const uint RoadsPlaceArrivingId = 673u;
const uint DrivingLinkOfLaneId = 674u;
const uint DrivingSlotOfLaneId = 675u;
const uint DrivingRunsPieceOffsetsId = 676u;
const uint DrivingRunsPiecesId = 677u;
const uint DrivingRunsStationMId = 678u;
const uint DrivingRunsLengthMId = 679u;
const uint WalkingLinkOfEdgeId = 680u;
const uint WalkingSlotOfEdgeId = 681u;
const uint WalkingLaneArcOffsetsId = 682u;
const uint WalkingLaneArcsId = 683u;
const uint WalkingLaneLengthMId = 684u;
const uint WalkingTailLengthMId = 685u;
const uint WalkingHeadLengthMId = 686u;
const uint WalkingEndLengthMId = 687u;
const uint WalkingTurnOffsetsId = 688u;
const uint WalkingTurnToEdgeId = 689u;
const uint WalkingJoinArcOffsetsId = 690u;
const uint WalkingJoinArcsId = 691u;
const uint WalkingJoinFromMId = 692u;
const uint WalkingJoinToMId = 693u;
const uint WalkingJoinLengthMId = 694u;
const uint WalkingWalkedFromMId = 695u;
const uint WalkingWalkedToMId = 696u;
const uint WalkingTailSlotId = 697u;
const uint WalkingRunsPieceOffsetsId = 698u;
const uint WalkingRunsPiecesId = 699u;
const uint WalkingRunsStationMId = 700u;
const uint WalkingRunsLengthMId = 701u;
const uint FootLengthMId = 702u;
const uint FootBandMId = 703u;
const uint FootKindId = 704u;
const uint FootArcOffsetsId = 705u;
const uint FootArcsId = 706u;
const uint WaysLengthMId = 707u;
const uint CrossingEdgesCrossingOfEdgeId = 708u;
const uint MarksOffsetsId = 709u;
const uint MarksSectionsId = 710u;
const uint FurnitureStopBarMId = 711u;
const uint FurnitureStopBarThicknessMId = 712u;
const uint TownDistrictLaneOffsetsId = 713u;
const uint TownDistrictLanesId = 714u;
const uint SignalsLitId = 715u;
const uint SignalsOffsetSId = 716u;
const uint SignalsLaneAxisId = 717u;
const uint SignalsLaneJunctionId = 718u;
const uint SignalsCrossingAxisId = 719u;
const uint SignalsCrossingJunctionId = 720u;
const uint MapStateId = 721u;
const uint RoadsNearestChainsId = 722u;
const uint RoadsNearestLeastMId = 723u;
const uint RoadsNearestMostMId = 724u;
const uint RoadsNearestCellStartId = 725u;
const uint RoadsNearestEntrySlotId = 726u;
const uint RoadsNearestStreetChainsId = 727u;
const uint RoadsNearestStreetLeastMId = 728u;
const uint RoadsNearestStreetMostMId = 729u;
const uint RoadsNearestStreetCellStartId = 730u;
const uint RoadsNearestStreetEntrySlotId = 731u;
const uint RoadsNearestLevel0ChainsId = 732u;
const uint RoadsNearestLevel0LeastMId = 733u;
const uint RoadsNearestLevel0MostMId = 734u;
const uint RoadsNearestLevel0CellStartId = 735u;
const uint RoadsNearestLevel0EntrySlotId = 736u;
const uint RoadsNearestLevel1ChainsId = 737u;
const uint RoadsNearestLevel1LeastMId = 738u;
const uint RoadsNearestLevel1MostMId = 739u;
const uint RoadsNearestLevel1CellStartId = 740u;
const uint RoadsNearestLevel1EntrySlotId = 741u;
const uint LaneIndexStateId = 742u;

#define mapState TOWN(MapStateRows, MapStateId).r[0]

// RoadGraph's lane indexes: every lane's (_nearest), the carriageway's (_nearestStreet) and the carriageway's on each of
// two levels (_nearestStreetOn) — index k's tables from RoadsNearestChainsId + RoadsIndexIds·k: each slot's chain (its
// lane) and box, and each cell's run of slots, from roads_IndexCellStart(k, cell) up to that of cell + 1.
const uint RoadsIndexNearest = 0u;
const uint RoadsIndexNearestStreet = 1u;
const uint RoadsIndexNearestStreetOn = 2u;
const uint RoadsIndexIds = 5u;
#define laneIndexState TOWN(LaneIndexStateRows, LaneIndexStateId).r[0]
#define roads_IndexWindow(k) laneIndexState.indexes[k]
#define roads_IndexChain(k, slot) TOWN(Ints, RoadsNearestChainsId + RoadsIndexIds * (k)).v[slot]
#define roads_IndexLeastM(k, slot) TOWN(Vec2s, RoadsNearestLeastMId + RoadsIndexIds * (k)).v[slot]
#define roads_IndexMostM(k, slot) TOWN(Vec2s, RoadsNearestMostMId + RoadsIndexIds * (k)).v[slot]
#define roads_IndexCellStart(k, cell) TOWN(Ints, RoadsNearestCellStartId + RoadsIndexIds * (k)).v[cell]
#define roads_IndexEntrySlot(k, entry) TOWN(Ints, RoadsNearestEntrySlotId + RoadsIndexIds * (k)).v[entry]
#define roads_IndexesLevelled laneIndexState.levelled

// RoadGraph: each lane's road, width, plan junctions at either end, whether it runs with its road, length, level, the
// lane the other way, whether it and that one are one line, whether it may cross the line to pass, where it stands from
// the kerb, the lanes beside it either way (LaneInward, LaneOutward), whether it is a bay, and its arcs.
#define roads_LaneCount mapState.laneCount
#define roads_ConnectorCount mapState.connectorCount
#define roads_JunctionCount mapState.junctionCount
#define roads_FirstRoadside mapState.firstRoadside
#define roads_MostLanesOfAWay mapState.mostLanesOfAWay
#define roads_LaneRoad(l) TOWN(Ints, RoadsLaneRoadId).v[l]
#define roads_LaneWidthM(l) TOWN(Floats, RoadsLaneWidthMId).v[l]
#define roads_LaneFromJunction(l) TOWN(Ints, RoadsLaneFromJunctionId).v[l]
#define roads_LaneToJunction(l) TOWN(Ints, RoadsLaneToJunctionId).v[l]
#define roads_LaneForward(l) TOWN(Uints, RoadsLaneForwardId).v[l]
#define roads_LaneLengthM(l) TOWN(Floats, RoadsLaneLengthMId).v[l]
#define roads_LaneLevel(l) TOWN(Uints, RoadsLaneLevelId).v[l]
#define roads_LaneReverse(l) TOWN(Ints, RoadsLaneReverseId).v[l]
#define roads_LaneOverOneLine(l) TOWN(Uints, RoadsLaneOverOneLineId).v[l]
#define roads_LaneCrossesToPass(l) TOWN(Uints, RoadsLaneCrossesToPassId).v[l]
#define roads_LaneFromKerb(l) TOWN(Uints, RoadsLaneFromKerbId).v[l]
#define roads_LaneInward(l) TOWN(Ints, RoadsLaneInwardId).v[l]
#define roads_LaneOutward(l) TOWN(Ints, RoadsLaneOutwardId).v[l]
#define roads_LaneIsBay(l) TOWN(Uints, RoadsLaneIsBayId).v[l]
#define roads_LaneArcOffsets(l) TOWN(Ints, RoadsLaneArcOffsetsId).v[l]
#define roads_LaneArcA(i) TOWN(ArcRows, RoadsLaneArcsId).r[2u * uint(i)]
#define roads_LaneArcB(i) TOWN(ArcRows, RoadsLaneArcsId).r[2u * uint(i) + 1u]

// The connectors: those out of lane l from roads_ConnectorAt(l) up to that of l + 1, each's lanes, turn, arcs, length,
// and whether it breaks the line.
#define roads_ConnectorAt(l) TOWN(Ints, RoadsConnectorAtId).v[l]
#define roads_ConnectorToLane(c) TOWN(Ints, RoadsConnectorToLaneId).v[c]
#define roads_ConnectorFromLane(c) TOWN(Ints, RoadsConnectorFromLaneId).v[c]
#define roads_ConnectorKind(c) TOWN(Uints, RoadsConnectorKindId).v[c]
#define roads_ConnectorArcOffsets(c) TOWN(Ints, RoadsConnectorArcOffsetsId).v[c]
#define roads_ConnectorArcA(i) TOWN(ArcRows, RoadsConnectorArcsId).r[2u * uint(i)]
#define roads_ConnectorArcB(i) TOWN(ArcRows, RoadsConnectorArcsId).r[2u * uint(i) + 1u]
#define roads_ConnectorLengthM(c) TOWN(Floats, RoadsConnectorLengthMId).v[c]
#define roads_BreaksTheLine(c) TOWN(Uints, RoadsBreaksTheLineId).v[c]

// The lanes out of and into each of the plan's junctions, and where the lanes meet (RoadGraph.Places, LanePlaces): the
// place each end of a lane is at (start 2l, end 2l + 1), each place's anchor, and the lanes leaving and arriving at it.
#define roads_JunctionOutOffsets(j) TOWN(Ints, RoadsJunctionOutOffsetsId).v[j]
#define roads_JunctionOutLane(i) TOWN(Ints, RoadsJunctionOutLanesId).v[i]
#define roads_JunctionInOffsets(j) TOWN(Ints, RoadsJunctionInOffsetsId).v[j]
#define roads_JunctionInLane(i) TOWN(Ints, RoadsJunctionInLanesId).v[i]
#define roads_PlaceCount mapState.placeCount
#define roads_PlaceOfEnd(i) TOWN(Ints, RoadsPlaceOfEndId).v[i]
#define roads_PlaceAnchorM(p) TOWN(Vec2s, RoadsPlaceAnchorMId).v[p]
#define roads_PlaceLeavingAt(p) TOWN(Ints, RoadsPlaceLeavingAtId).v[p]
#define roads_PlaceLeaving(i) TOWN(Ints, RoadsPlaceLeavingId).v[i]
#define roads_PlaceArrivingAt(p) TOWN(Ints, RoadsPlaceArrivingAtId).v[p]
#define roads_PlaceArriving(i) TOWN(Ints, RoadsPlaceArrivingId).v[i]

// DrivingNetwork: each lane's link and its slot among the link's pieces; its RunNetwork's pieces a link (the lanes in
// travel order), where along its link each piece begins, and each link's length.
#define driving_LinkOfLane(l) TOWN(Ints, DrivingLinkOfLaneId).v[l]
#define driving_SlotOfLane(l) TOWN(Ints, DrivingSlotOfLaneId).v[l]
#define drivingRuns_LinkCount mapState.drivingLinkCount
#define drivingRuns_PieceOffsets(k) TOWN(Ints, DrivingRunsPieceOffsetsId).v[k]
#define drivingRuns_Piece(i) TOWN(Ints, DrivingRunsPiecesId).v[i]
#define drivingRuns_StationM(i) TOWN(Floats, DrivingRunsStationMId).v[i]
#define drivingRuns_LengthM(k) TOWN(Floats, DrivingRunsLengthMId).v[k]

// WalkingNetwork, a pavement lane an edge of the foot graph: each's link and slot, its walked line and its lengths
// (the whole, its tail, its head, its end), the turns (mitres) out of it — slot by slot each's edge, arcs, where it is
// joined and left, its length — where along it it is walked from and to, and the turn it carries at its tail; its
// RunNetwork, as the driving one's.
#define walking_EdgeCount mapState.walkingEdgeCount
#define walking_TurnCount mapState.walkingTurnCount
#define walking_LinkOfEdge(e) TOWN(Ints, WalkingLinkOfEdgeId).v[e]
#define walking_SlotOfEdge(e) TOWN(Ints, WalkingSlotOfEdgeId).v[e]
#define walking_LaneArcOffsets(e) TOWN(Ints, WalkingLaneArcOffsetsId).v[e]
#define walking_LaneArcA(i) TOWN(ArcRows, WalkingLaneArcsId).r[2u * uint(i)]
#define walking_LaneArcB(i) TOWN(ArcRows, WalkingLaneArcsId).r[2u * uint(i) + 1u]
#define walking_LaneLengthM(e) TOWN(Floats, WalkingLaneLengthMId).v[e]
#define walking_TailLengthM(e) TOWN(Floats, WalkingTailLengthMId).v[e]
#define walking_HeadLengthM(e) TOWN(Floats, WalkingHeadLengthMId).v[e]
#define walking_EndLengthM(e) TOWN(Floats, WalkingEndLengthMId).v[e]
#define walking_TurnOffsets(e) TOWN(Ints, WalkingTurnOffsetsId).v[e]
#define walking_TurnToEdge(slot) TOWN(Ints, WalkingTurnToEdgeId).v[slot]
#define walking_JoinArcOffsets(slot) TOWN(Ints, WalkingJoinArcOffsetsId).v[slot]
#define walking_JoinArcA(i) TOWN(ArcRows, WalkingJoinArcsId).r[2u * uint(i)]
#define walking_JoinArcB(i) TOWN(ArcRows, WalkingJoinArcsId).r[2u * uint(i) + 1u]
#define walking_JoinFromM(slot) TOWN(Floats, WalkingJoinFromMId).v[slot]
#define walking_JoinToM(slot) TOWN(Floats, WalkingJoinToMId).v[slot]
#define walking_JoinLengthM(slot) TOWN(Floats, WalkingJoinLengthMId).v[slot]
#define walking_WalkedFromM(e) TOWN(Floats, WalkingWalkedFromMId).v[e]
#define walking_WalkedToM(e) TOWN(Floats, WalkingWalkedToMId).v[e]
#define walking_TailOf(e) TOWN(Ints, WalkingTailSlotId).v[e]
#define walkingRuns_LinkCount mapState.walkingLinkCount
#define walkingRuns_PieceOffsets(k) TOWN(Ints, WalkingRunsPieceOffsetsId).v[k]
#define walkingRuns_Piece(i) TOWN(Ints, WalkingRunsPiecesId).v[i]
#define walkingRuns_StationM(i) TOWN(Floats, WalkingRunsStationMId).v[i]
#define walkingRuns_LengthM(k) TOWN(Floats, WalkingRunsLengthMId).v[k]

// FootGraph: each edge's length, band (WalkingNetwork.LaneWidthM), kind and line.
#define foot_LengthM(e) TOWN(Floats, FootLengthMId).v[e]
#define foot_BandM(e) TOWN(Floats, FootBandMId).v[e]
#define foot_KindOf(e) TOWN(Uints, FootKindId).v[e]
#define foot_ArcOffsets(e) TOWN(Ints, FootArcOffsetsId).v[e]
#define foot_ArcA(i) TOWN(ArcRows, FootArcsId).r[2u * uint(i)]
#define foot_ArcB(i) TOWN(ArcRows, FootArcsId).r[2u * uint(i) + 1u]

// TownWays: every way's length in the one numbering (lanes, then connectors, footways, mitres) and where each block
// begins; the crossing each foot edge is, or CityPlanNoRecord; the ways crossing each way (WayCrossings).
#define ways_Count mapState.wayCount
#define ways_FirstConnector mapState.laneCount
#define ways_FirstFootwayWay mapState.firstFootwayWay
#define ways_FirstMitreWay mapState.firstMitreWay
#define ways_LengthM(w) TOWN(Floats, WaysLengthMId).v[w]
#define crossingEdges_CrossingOf(e) TOWN(Ints, CrossingEdgesCrossingOfEdgeId).v[e]
#define marks_Offsets(w) TOWN(Ints, MarksOffsetsId).v[w]
#define marks_Section(i) TOWN(CrossedSectionRows, MarksSectionsId).r[i]

// LaneFurniture: where along each lane its stop bar stands (+inf for none) and how thick it is.
#define furniture_StopBarAlongM(l) TOWN(Floats, FurnitureStopBarMId).v[l]
#define furniture_StopBarThicknessM(l) TOWN(Floats, FurnitureStopBarThicknessMId).v[l]

// Each district's streets, district d's from town_DistrictLaneOffsets(d) up to that of d + 1; the wheel they are cut by.
#define town_DistrictLaneOffsets(d) TOWN(Ints, TownDistrictLaneOffsetsId).v[d]
#define town_DistrictLane(i) TOWN(Ints, TownDistrictLanesId).v[i]
#define districts_HubM mapState.districtHubM
#define districts_RingRadiusM mapState.districtRingRadiusM
#define districts_FirstSpokeRad mapState.districtFirstSpokeRad
#define districts_Spokes mapState.districtSpokes
#define town_Levelled mapState.levelled

// SignalService: which junctions are lit and each's offset, each lane's axis and junction, each crossing's.
#define signals_JunctionCount mapState.signalJunctionCount
#define signals_CrossingCount mapState.signalCrossingCount
#define signals_Lit(j) TOWN(Uints, SignalsLitId).v[j]
#define signals_OffsetS(j) TOWN(Floats, SignalsOffsetSId).v[j]
#define signals_LaneAxis(l) TOWN(Ints, SignalsLaneAxisId).v[l]
#define signals_LaneJunction(l) TOWN(Ints, SignalsLaneJunctionId).v[l]
#define signals_CrossingAxis(z) TOWN(Ints, SignalsCrossingAxisId).v[z]
#define signals_CrossingJunction(z) TOWN(Ints, SignalsCrossingJunctionId).v[z]

#endif
