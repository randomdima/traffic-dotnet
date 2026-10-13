// A body's filter in one word, as every kernel reads it (DeviceSolver.FilterOf): its flags (BodyFlags) in the low
// byte, so a flag's own bit tests it; the layer it is on and the layers it meets (CollisionLayer) in the next two; and
// the channels it is on (PHY-1a) in the top byte.

#ifndef FILTER_GLSL
#define FILTER_GLSL

const uint FilterByte = 0xffu;
const uint FilterLayerShift = 8u;
const uint FilterMaskShift = 16u;
const uint FilterChannelsShift = 24u;

uint filterLayer(uint filtered) { return (filtered >> FilterLayerShift) & FilterByte; }
uint filterMask(uint filtered) { return (filtered >> FilterMaskShift) & FilterByte; }
uint filterChannels(uint filtered) { return filtered >> FilterChannelsShift; }

uint filterOnChannels(uint filtered, uint channels)
{
    return (filtered & ~(FilterByte << FilterChannelsShift)) | ((channels & FilterByte) << FilterChannelsShift);
}

uint filterOnLayer(uint filtered, uint layer, uint mask)
{
    uint kept = filtered & ~((FilterByte << FilterLayerShift) | (FilterByte << FilterMaskShift));
    return kept | ((layer & FilterByte) << FilterLayerShift) | ((mask & FilterByte) << FilterMaskShift);
}

#endif
