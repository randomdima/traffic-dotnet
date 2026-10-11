// The town's chains of arcs as Spline walks them (arc.glsl, arc_chain.glsl): a projection or a sample reads whichever
// table townArcs is pointed at — the lanes', the connectors' or the cars' lines' — over a run of absolute indices into it,
// so one port of Spline walks every chain the town holds. Point it before the walk; nothing else sets it.

#ifndef TOWN_ARCS_GLSL
#define TOWN_ARCS_GLSL

#include "world/town/kernels/town_map.glsl"
#include "core/geometry/kernels/arc.glsl"

layout(buffer_reference, std430, buffer_reference_align = 16) buffer ArcChainRows { Arc v[]; };

// The table arc_chain.glsl's SPLINE_ARCS reads.
ArcChainRows townArcs;

#define SPLINE_ARCS townArcs.v
#include "core/geometry/kernels/arc_chain.glsl"

void townArcsOfLanes() { townArcs = TOWN(ArcChainRows, RoadsLaneArcsId); }

void townArcsOfConnectors() { townArcs = TOWN(ArcChainRows, RoadsConnectorArcsId); }

void townArcsOfLines() { townArcs = TOWN(ArcChainRows, CarsArcs); }

// A lane's run of arcs, a connector's, and a car's line's (CarFleet.LineOf: its first townCounts.arcsPerCar a car).
uint townLaneArcsFrom(int lane) { return uint(roads_LaneArcOffsets(lane)); }
uint townLaneArcsTo(int lane) { return uint(roads_LaneArcOffsets(lane + 1)); }
uint townConnectorArcsFrom(int connector) { return uint(roads_ConnectorArcOffsets(connector)); }
uint townConnectorArcsTo(int connector) { return uint(roads_ConnectorArcOffsets(connector + 1)); }
uint townLineArcsFrom(uint car) { return car * townCounts.arcsPerCar; }

Arc townLaneArc(int at) { return Arc(roads_LaneArcA(at), roads_LaneArcB(at)); }
Arc townLineArc(uint car, uint at) { return Arc(cars_LineArcA(car, at), cars_LineArcB(car, at)); }

void townSetLineArc(uint car, uint at, Arc arc)
{
    cars_LineArcA(car, at) = arc.a;
    cars_LineArcB(car, at) = arc.b;
}

// A piece as the host's ArcSeg constructor lays one: its first heading's unit vector taken off the heading (Heading.Unit).
Arc townArcOf(vec2 startM, float headingRad, float lengthM, float curvature)
{
    return Arc(vec4(startM, trigUnit(headingRad)), vec4(headingRad, lengthM, curvature, 0.0));
}

#endif
