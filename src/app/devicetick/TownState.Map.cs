using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Routing;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The static map the decisions read</b> (<c>world/town/kernels/town_map.glsl</c>, ids 640–767): the road graph and
/// where its lanes meet, both networks' lanes and links, the pavement's fine graph, the ways' one numbering and what
/// crosses each, the stop bars, the districts' streets and the lights' tables.
/// </summary>
/// <remarks>
/// A run of a CSR table is laid as the C# hands it out, run by run — so its offsets are the C#'s whether the C# keeps
/// them or not.
/// </remarks>
internal sealed partial class TownState
{
    const int RoadsLaneRoadId = 640;
    const int RoadsLaneWidthMId = 641;
    const int RoadsLaneFromJunctionId = 642;
    const int RoadsLaneToJunctionId = 643;
    const int RoadsLaneForwardId = 644;
    const int RoadsLaneLengthMId = 645;
    const int RoadsLaneLevelId = 646;
    const int RoadsLaneReverseId = 647;
    const int RoadsLaneOverOneLineId = 648;
    const int RoadsLaneCrossesToPassId = 649;
    const int RoadsLaneFromKerbId = 650;
    const int RoadsLaneInwardId = 651;
    const int RoadsLaneOutwardId = 652;
    const int RoadsLaneIsBayId = 653;
    const int RoadsLaneArcOffsetsId = 654;
    const int RoadsLaneArcsId = 655;
    const int RoadsConnectorAtId = 656;
    const int RoadsConnectorToLaneId = 657;
    const int RoadsConnectorFromLaneId = 658;
    const int RoadsConnectorKindId = 659;
    const int RoadsConnectorArcOffsetsId = 660;
    const int RoadsConnectorArcsId = 661;
    const int RoadsConnectorLengthMId = 662;
    const int RoadsBreaksTheLineId = 663;
    const int RoadsJunctionOutOffsetsId = 664;
    const int RoadsJunctionOutLanesId = 665;
    const int RoadsJunctionInOffsetsId = 666;
    const int RoadsJunctionInLanesId = 667;
    const int RoadsPlaceOfEndId = 668;
    const int RoadsPlaceAnchorMId = 669;
    const int RoadsPlaceLeavingAtId = 670;
    const int RoadsPlaceLeavingId = 671;
    const int RoadsPlaceArrivingAtId = 672;
    const int RoadsPlaceArrivingId = 673;
    const int DrivingLinkOfLaneId = 674;
    const int DrivingSlotOfLaneId = 675;
    const int DrivingRunsPieceOffsetsId = 676;
    const int DrivingRunsPiecesId = 677;
    const int DrivingRunsStationMId = 678;
    const int DrivingRunsLengthMId = 679;
    const int WalkingLinkOfEdgeId = 680;
    const int WalkingSlotOfEdgeId = 681;
    const int WalkingLaneArcOffsetsId = 682;
    const int WalkingLaneArcsId = 683;
    const int WalkingLaneLengthMId = 684;
    const int WalkingTailLengthMId = 685;
    const int WalkingHeadLengthMId = 686;
    const int WalkingEndLengthMId = 687;
    const int WalkingTurnOffsetsId = 688;
    const int WalkingTurnToEdgeId = 689;
    const int WalkingJoinArcOffsetsId = 690;
    const int WalkingJoinArcsId = 691;
    const int WalkingJoinFromMId = 692;
    const int WalkingJoinToMId = 693;
    const int WalkingJoinLengthMId = 694;
    const int WalkingWalkedFromMId = 695;
    const int WalkingWalkedToMId = 696;
    const int WalkingTailSlotId = 697;
    const int WalkingRunsPieceOffsetsId = 698;
    const int WalkingRunsPiecesId = 699;
    const int WalkingRunsStationMId = 700;
    const int WalkingRunsLengthMId = 701;
    const int FootLengthMId = 702;
    const int FootBandMId = 703;
    const int FootKindId = 704;
    const int FootArcOffsetsId = 705;
    const int FootArcsId = 706;
    const int WaysLengthMId = 707;
    const int CrossingEdgesCrossingOfEdgeId = 708;
    const int MarksOffsetsId = 709;
    const int MarksSectionsId = 710;
    const int FurnitureStopBarMId = 711;
    const int FurnitureStopBarThicknessMId = 712;
    const int TownDistrictLaneOffsetsId = 713;
    const int TownDistrictLanesId = 714;
    const int SignalsLitId = 715;
    const int SignalsOffsetSId = 716;
    const int SignalsLaneAxisId = 717;
    const int SignalsLaneJunctionId = 718;
    const int SignalsCrossingAxisId = 719;
    const int SignalsCrossingJunctionId = 720;
    const int MapStateId = 721;

    /// <summary>How much of each the map has, the ways' blocks and the district wheel (<c>MapState</c>), field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct MapStateRow(
        int LaneCount, int ConnectorCount, int JunctionCount, int FirstRoadside, int PlaceCount, int MostLanesOfAWay,
        int DrivingLinkCount, int WalkingEdgeCount, int WalkingTurnCount, int WalkingLinkCount, int WayCount, int FirstFootwayWay,
        int FirstMitreWay, int SignalJunctionCount, int SignalCrossingCount, uint Levelled, Vector2 DistrictHubM,
        float DistrictRingRadiusM, float DistrictFirstSpokeRad, int DistrictSpokes, int Pad0 = 0, int Pad1 = 0, int Pad2 = 0);

    /// <summary>The run of a CSR table at one index, as the C# hands it out.</summary>
    delegate ReadOnlySpan<T> RunOf<T>(int at);

    void LayTheMap(TownWorld town)
    {
        var roads = town.Roads;
        var lanes = roads.LaneCount;
        var connectors = roads.ConnectorCount;
        Lay<int>(RoadsLaneRoadId, roads.LaneRoad);
        Lay<float>(RoadsLaneWidthMId, roads.LaneWidthM);
        Lay<int>(RoadsLaneFromJunctionId, roads.LaneFromJunction);
        Lay<int>(RoadsLaneToJunctionId, roads.LaneToJunction);
        LayFlags(RoadsLaneForwardId, roads.LaneForward);
        Lay<float>(RoadsLaneLengthMId, roads.LaneLengthM);
        LayWidened<byte>(RoadsLaneLevelId, roads.LaneLevel);
        Lay<int>(RoadsLaneReverseId, roads.LaneReverse);
        LayFlags(RoadsLaneOverOneLineId, roads.LaneOverOneLine);
        LayFlags(RoadsLaneCrossesToPassId, roads.LaneCrossesToPass);
        LayWidened<byte>(RoadsLaneFromKerbId, roads.LaneFromKerb);

        var inward = new int[lanes];
        var outward = new int[lanes];
        var isBay = new bool[lanes];
        var firstRoadside = lanes;
        for (var lane = lanes - 1; lane >= 0; lane--)
        {
            inward[lane] = roads.LaneBeside(lane, inward: true);
            outward[lane] = roads.LaneBeside(lane, inward: false);
            isBay[lane] = roads.IsABayArm(lane);
            if (roads.IsARoadside(lane)) firstRoadside = lane;
        }

        Lay<int>(RoadsLaneInwardId, inward);
        Lay<int>(RoadsLaneOutwardId, outward);
        LayFlags(RoadsLaneIsBayId, isBay);
        LayArcRuns(RoadsLaneArcOffsetsId, RoadsLaneArcsId, lanes, roads.ArcsOf);

        var connectorAt = new int[lanes + 1];
        for (var lane = 0; lane < lanes; lane++) connectorAt[lane + 1] = connectorAt[lane] + roads.ConnectorsFrom(lane).Count;

        var toLane = new int[connectors];
        var fromLane = new int[connectors];
        var kind = new uint[connectors];
        var lengthM = new float[connectors];
        var breaks = new bool[connectors];
        for (var connector = 0; connector < connectors; connector++)
        {
            toLane[connector] = roads.ConnectorTo(connector);
            fromLane[connector] = roads.ConnectorFrom(connector);
            kind[connector] = (uint)roads.KindOf(connector);
            lengthM[connector] = roads.ConnectorLengthM(connector);
            breaks[connector] = roads.BreaksTheLine(connector);
        }

        Lay<int>(RoadsConnectorAtId, connectorAt);
        Lay<int>(RoadsConnectorToLaneId, toLane);
        Lay<int>(RoadsConnectorFromLaneId, fromLane);
        Lay<uint>(RoadsConnectorKindId, kind);
        LayArcRuns(RoadsConnectorArcOffsetsId, RoadsConnectorArcsId, connectors, roads.ConnectorArcs);
        Lay<float>(RoadsConnectorLengthMId, lengthM);
        LayFlags(RoadsBreaksTheLineId, breaks);
        LayRuns<int>(RoadsJunctionOutOffsetsId, RoadsJunctionOutLanesId, roads.JunctionCount, roads.LanesOutOfJunction);
        LayRuns<int>(RoadsJunctionInOffsetsId, RoadsJunctionInLanesId, roads.JunctionCount, roads.LanesIntoJunction);

        var places = roads.Places;
        var placeOfEnd = new int[lanes * 2];
        for (var lane = 0; lane < lanes; lane++)
        {
            placeOfEnd[lane * 2] = places.Starting(lane);
            placeOfEnd[(lane * 2) + 1] = places.Arriving(lane);
        }

        var anchorM = new Vector2[places.Count];
        for (var place = 0; place < anchorM.Length; place++) anchorM[place] = places.AnchorM(place);

        Lay<int>(RoadsPlaceOfEndId, placeOfEnd);
        Lay<Vector2>(RoadsPlaceAnchorMId, anchorM);
        LayRuns<int>(RoadsPlaceLeavingAtId, RoadsPlaceLeavingId, places.Count, places.LanesLeaving);
        LayRuns<int>(RoadsPlaceArrivingAtId, RoadsPlaceArrivingId, places.Count, places.LanesArriving);

        var driving = town.Driving;
        var linkOfLane = new int[lanes];
        var slotOfLane = new int[lanes];
        for (var lane = 0; lane < lanes; lane++)
        {
            linkOfLane[lane] = driving.LinkOfLane(lane);
            slotOfLane[lane] = driving.SlotOfLane(lane);
        }

        Lay<int>(DrivingLinkOfLaneId, linkOfLane);
        Lay<int>(DrivingSlotOfLaneId, slotOfLane);
        LayTheRuns(DrivingRunsPieceOffsetsId, DrivingRunsPiecesId, DrivingRunsStationMId, DrivingRunsLengthMId, driving.Runs);

        var walking = town.Walking;
        var foot = walking.Foot;
        var edges = foot.EdgeCount;
        var turns = walking.TurnCount;
        var linkOfEdge = new int[edges];
        var slotOfEdge = new int[edges];
        var laneLengthM = new float[edges];
        var tailM = new float[edges];
        var headM = new float[edges];
        var endM = new float[edges];
        var walkedFromM = new float[edges];
        var walkedToM = new float[edges];
        var tailSlot = new int[edges];
        var footLengthM = new float[edges];
        var bandM = new float[edges];
        var footKind = new uint[edges];
        for (var edge = 0; edge < edges; edge++)
        {
            linkOfEdge[edge] = walking.LinkOfEdge(edge);
            slotOfEdge[edge] = walking.SlotOfEdge(edge);
            laneLengthM[edge] = walking.LaneLengthM(edge);
            tailM[edge] = walking.TailLengthM(edge);
            headM[edge] = walking.HeadLengthM(edge);
            endM[edge] = walking.EndLengthM(edge);
            walkedFromM[edge] = walking.WalkedFromM(edge);
            walkedToM[edge] = walking.WalkedToM(edge);
            tailSlot[edge] = walking.TailOf(edge);
            footLengthM[edge] = foot.LengthM(edge);
            bandM[edge] = foot.BandM(edge);
            footKind[edge] = (uint)foot.KindOf(edge);
        }

        var joinFromM = new float[turns];
        var joinToM = new float[turns];
        var joinLengthM = new float[turns];
        for (var slot = 0; slot < turns; slot++)
        {
            joinFromM[slot] = walking.JoinFromM(slot);
            joinToM[slot] = walking.JoinToM(slot);
            joinLengthM[slot] = walking.JoinLengthM(slot);
        }

        Lay<int>(WalkingLinkOfEdgeId, linkOfEdge);
        Lay<int>(WalkingSlotOfEdgeId, slotOfEdge);
        LayArcRuns(WalkingLaneArcOffsetsId, WalkingLaneArcsId, edges, walking.LaneOf);
        Lay<float>(WalkingLaneLengthMId, laneLengthM);
        Lay<float>(WalkingTailLengthMId, tailM);
        Lay<float>(WalkingHeadLengthMId, headM);
        Lay<float>(WalkingEndLengthMId, endM);
        LayRuns<int>(WalkingTurnOffsetsId, WalkingTurnToEdgeId, edges, walking.TurnsFrom);
        LayArcRuns(WalkingJoinArcOffsetsId, WalkingJoinArcsId, turns, walking.JoinArcs);
        Lay<float>(WalkingJoinFromMId, joinFromM);
        Lay<float>(WalkingJoinToMId, joinToM);
        Lay<float>(WalkingJoinLengthMId, joinLengthM);
        Lay<float>(WalkingWalkedFromMId, walkedFromM);
        Lay<float>(WalkingWalkedToMId, walkedToM);
        Lay<int>(WalkingTailSlotId, tailSlot);
        LayTheRuns(WalkingRunsPieceOffsetsId, WalkingRunsPiecesId, WalkingRunsStationMId, WalkingRunsLengthMId, walking.Runs);
        Lay<float>(FootLengthMId, footLengthM);
        Lay<float>(FootBandMId, bandM);
        Lay<uint>(FootKindId, footKind);
        LayArcRuns(FootArcOffsetsId, FootArcsId, edges, foot.ArcsOf);

        var ways = town.Ways;
        var waysLengthM = new float[ways.Count];
        for (var way = 0; way < waysLengthM.Length; way++) waysLengthM[way] = ways.LengthM(way);

        Lay<float>(WaysLengthMId, waysLengthM);
        Lay<int>(CrossingEdgesCrossingOfEdgeId, town.CrossingEdges.CrossingOfEdge);
        var marks = town.Atlas.Marks;
        LayRuns<CrossedSection>(MarksOffsetsId, MarksSectionsId, marks.WayCount, marks.Of);

        var furniture = town.Furniture;
        var stopBarM = new float[lanes];
        var stopBarThicknessM = new float[lanes];
        for (var lane = 0; lane < lanes; lane++)
        {
            stopBarM[lane] = furniture.StopBarAlongM(lane);
            stopBarThicknessM[lane] = furniture.StopBarThicknessM(lane);
        }

        Lay<float>(FurnitureStopBarMId, stopBarM);
        Lay<float>(FurnitureStopBarThicknessMId, stopBarThicknessM);

        var districts = town.DistrictStreets;
        Lay<int>(TownDistrictLaneOffsetsId, districts.Offsets);
        Lay<int>(TownDistrictLanesId, districts.Lanes);

        var signals = town.Signals;
        var table = signals.Table;
        var lit = new bool[signals.JunctionCount];
        for (var junction = 0; junction < lit.Length; junction++) lit[junction] = signals.Lit(junction);

        LayFlags(SignalsLitId, lit);
        Lay<float>(SignalsOffsetSId, table.OffsetS);
        Lay<int>(SignalsLaneAxisId, table.LaneAxis);
        Lay<int>(SignalsLaneJunctionId, table.LaneJunction);
        Lay<int>(SignalsCrossingAxisId, table.CrossingAxis);
        Lay<int>(SignalsCrossingJunctionId, table.CrossingJunction);

        var wheel = town.Plan.Districts;
        LayOne(MapStateId, new MapStateRow(
            lanes, connectors, roads.JunctionCount, firstRoadside, places.Count, roads.MostLanesOfAWay, driving.Runs.LinkCount, edges,
            turns, walking.Runs.LinkCount, ways.Count, ways.FirstFootwayWay, ways.FirstMitreWay, signals.JunctionCount,
            signals.CrossingCount, town.Levelled ? 1u : 0u, wheel.HubM, wheel.RingRadiusM, wheel.FirstSpokeRad, wheel.Spokes));
    }

    /// <summary>A CSR table laid run by run: <paramref name="count"/> + 1 offsets, and the runs back to back.</summary>
    void LayRuns<T>(int offsetsId, int itemsId, int count, RunOf<T> runOf) where T : unmanaged
    {
        var offsets = new int[count + 1];
        for (var at = 0; at < count; at++) offsets[at + 1] = offsets[at] + runOf(at).Length;

        var items = new T[offsets[count]];
        for (var at = 0; at < count; at++) runOf(at).CopyTo(items.AsSpan(offsets[at]));

        Lay<int>(offsetsId, offsets);
        Lay<T>(itemsId, items);
    }

    /// <summary>And one of arcs, each as <c>arc.glsl</c>'s <c>Arc</c> reads it.</summary>
    void LayArcRuns(int offsetsId, int arcsId, int count, RunOf<ArcSeg> runOf)
    {
        var offsets = new int[count + 1];
        for (var at = 0; at < count; at++) offsets[at + 1] = offsets[at] + runOf(at).Length;

        var arcs = new ArcRow[offsets[count]];
        for (var at = 0; at < count; at++)
        {
            var run = runOf(at);
            for (var arc = 0; arc < run.Length; arc++) arcs[offsets[at] + arc] = ArcRow.Of(run[arc]);
        }

        Lay<int>(offsetsId, offsets);
        Lay<ArcRow>(arcsId, arcs);
    }

    /// <summary>A network's runs (<see cref="RunNetwork"/>): each link's pieces, where along it each begins, and its length.</summary>
    void LayTheRuns(int offsetsId, int piecesId, int stationsId, int lengthsId, RunNetwork runs)
    {
        var links = runs.LinkCount;
        LayRuns<int>(offsetsId, piecesId, links, runs.PiecesOf);

        var stationM = new List<float>();
        var lengthM = new float[links];
        for (var link = 0; link < links; link++)
        {
            foreach (var atM in runs.StationsOf(link)) stationM.Add(atM);
            lengthM[link] = runs.LengthM(link);
        }

        Lay<float>(stationsId, CollectionsMarshal.AsSpan(stationM));
        Lay<float>(lengthsId, lengthM);
    }
}
