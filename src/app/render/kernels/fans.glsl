// The fans a roof or a vehicle draws (FanTable, OBJ-6, TRN-13): a run a sheet slot, then each fan's two rows — its hub
// and its circle's radius in its sheet's own units, nought to one across; then its rim's ellipse in the circle's own
// units, the hub off its middle over its half-axes and the radius over them — and past the fans the looks a fan is drawn
// in, two rows each: where the look was packed, then its layer and how many looks there are. A fan is nothing but this:
// one of the looks, laid over its body's picture inside the rim and turned as the body is drawn (sprite.vert's quadEmit,
// sprite.frag). Binding 16 (TownRenderer.FanTableBinding), read by both stages.

layout(set = 0, binding = 16) uniform Fans {
    uvec4 runs[512];   // SheetAtlas.Slots / 4: a run its first fan in the low sixteen bits and how many in the high
    vec4 fans[2048];   // FanTable.MostFans, two rows a fan, then FanTable.MostLooks, two rows a look
} fanTable;

// FanTable.LookRow: the row the looks start at.
const uint FAN_LOOK_ROW = 2016u;

// What a body's ID is mixed with before it picks its fans' look (fansShare), so the look is not its start of a turn.
const uint FAN_LOOK_SALT = 0x5bd1e995u;

// One sheet's run of fans, drawn `widthM` across; nought for a sheet that draws none, or whose widest fan is less than
// `leastAcrossM` across this frame (CameraView.Turning), whose body is then drawn as it is.
uint fansRun(uint sheet, float widthM, float leastAcrossM) {
    uint run = fanTable.runs[sheet >> 2u][sheet & 3u];
    uint first = run & 0xffffu;
    for (uint fan = 0u; fan < (run >> 16u); fan++) {
        if (2.0 * fanTable.fans[2u * (first + fan)].z * widthM >= leastAcrossM) return run;
    }

    return 0u;
}

// A body's own share of a turn, off its ID (SpriteInstance.Id): every fan turns at the one rate, each body's from its own
// start. lowbias32, so neighbouring IDs land far apart.
float fansShare(uint id) {
    id ^= id >> 16u;
    id *= 0x7feb352du;
    id ^= id >> 15u;
    id *= 0x846ca68bu;
    id ^= id >> 16u;
    return float(id) * (1.0 / 4294967296.0);
}

// Whether a fragment at `local` on its sheet is inside one of the run's fans, and if it is, where it reads the body's
// look (`lookShare` of the way through the looks): the look's middle on the hub and its edge on the rim, the share of
// the way out to the rim it stands along its own way (DrawnFan.RimReachM) the share of the way out to the look's edge,
// turned — so a look covers a fan the art drew squashed or off its hub, and turns round. The body's share of a turn is
// `turns`; each further fan of it is a golden section on.
bool fansLook(vec2 local, uint run, float turns, float lookShare, out vec3 lookUv) {
    lookUv = vec3(0.0);
    uint looks = uint(fanTable.fans[FAN_LOOK_ROW + 1u].y);
    if (looks == 0u) return false;

    uint look = min(uint(lookShare * float(looks)), looks - 1u);
    vec4 place = fanTable.fans[FAN_LOOK_ROW + 2u * look];
    float layer = fanTable.fans[FAN_LOOK_ROW + 2u * look + 1u].x;
    uint first = run & 0xffffu;
    uint count = run >> 16u;
    for (uint fan = 0u; fan < count; fan++) {
        vec4 at = fanTable.fans[2u * (first + fan)];
        // Over the radius each way, so the circle is one across in metres whatever the sheet's aspect.
        vec2 off = (local - at.xy) / at.zw;
        float outSq = dot(off, off);
        if (outSq >= 1.0) continue;

        float fromHub = sqrt(outSq);
        vec2 way = off / max(fromHub, 1e-6);
        vec4 rim = fanTable.fans[2u * (first + fan) + 1u];
        vec2 along = way * rim.zw;
        float a = dot(along, along);
        float b = dot(rim.xy, along);
        float reach = (-b + sqrt(max(0.0, b * b - a * (dot(rim.xy, rim.xy) - 1.0)))) / a;
        float share = fromHub / reach;
        if (share >= 1.0) continue;

        float turnedRad = fract(turns + float(fan) * 0.618034) * 6.2831853;
        float c = cos(turnedRad);
        float s = sin(turnedRad);
        vec2 turned = vec2(c * way.x + s * way.y, c * way.y - s * way.x);
        lookUv = vec3(place.xy + (vec2(0.5) + turned * (share * 0.5)) * place.zw, layer);
        return true;
    }

    return false;
}
