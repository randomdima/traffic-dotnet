using System.Numerics;

namespace TrafficSimulation.Tests.E2E.Scenarios;

/// <summary>
/// The ground: the town as a whole, the materials where they meet, and the one place the town is
/// carried over water.
/// </summary>
internal static class GroundScenarios
{
    public static VisualScenario[] All =>
    [
        new(
            Name: "map-overview",
            Group: "core",
            Map: "Test",
            Subject: "The WHOLE fixture town from above — the frame is the map's own size, so this is "
                     + "all of it. Asked only whether it reads as a consistent town with no mistake "
                     + "visible from here; everything detailed is asked of a named place. NOTE: the "
                     + "frame is a little wider and taller than the map, so a plain band along the "
                     + "edges is the outside of the world and not a patch of missing ground.",
            // The finest thing these claims name is a street — not a building's bearing, which at
            // whole-town framing is a handful of pixels however the arithmetic is done.
            FrameWidthM: 489.6f, FinestFeatureM: 7f,
            AtM: new Vector2(240f, 160f), Seconds: 10, Ui: ["none"],
            Expect:
            [
                "The town reads as a town: streets that connect to one another, blocks of buildings "
                + "between them, open ground around it.",
                "Every street either runs to a junction at both ends or finishes in a rounded turning "
                + "head — none simply stops in the middle of open ground.",
                "The street network is one connected whole: there is no island of streets cut off "
                + "from the rest with no road reaching it.",
                "Inside the map every part of the frame is some kind of ground — no bare, black or "
                + "transparent patch where terrain was not laid.",
                "Nothing is drawn on top of the water: no road, building or tree stands in it, and "
                + "the road crossing it does so on a bridge.",
                "Buildings and planting are spread through the town rather than piled in one place or "
                + "missing from a whole district.",
                "Nothing is obviously duplicated on top of itself — no building drawn over a "
                + "building, no road drawn over a road.",
            ],
            Expected: "map-overview.png"),

        new(
            Name: "terrain-seams",
            Group: "core",
            Map: "Test",
            Subject: "Where several kinds of ground meet at the west-bank junction — grass, tarmac, "
                     + "pavement and the paint on it.",
            FrameWidthM: 40f, FinestFeatureM: 0.4f, // the grain of a texture
            AtM: new Vector2(28f, 165f), Seconds: 10, Ui: ["none"],
            Expect:
            [
                "Each kind of ground reads as its own material: grass looks like grass, tarmac like "
                + "tarmac, paving like paving.",
                "The textures tile without an obvious repeating motif — no grid of identical patches, "
                + "no one feature recurring on a regular pitch.",
                "The boundaries between grounds are clean lines with no fringe of the wrong material "
                + "bleeding across them.",
                "No ground is stretched or smeared: the grain of each texture is the same size "
                + "everywhere it appears in the frame.",
                "There is no gap, seam or flicker of background colour between two adjacent grounds.",
            ],
            Expected: "terrain-seams.png"),

        new(
            Name: "bridge",
            Group: "core",
            Map: "Test",
            Subject: "The one bridge over the river, whose pavement is the only way to the west bank "
                     + "on foot.",
            FrameWidthM: 60f, FinestFeatureM: 0.3f, // the claims reach down to the markings on the deck
            AtM: new Vector2(89f, 165f), Seconds: 10, Ui: ["none"],
            Expect:
            [
                "The deck spans the water with land at both ends; the road does not stop short of "
                + "either bank.",
                "A pale pavement runs along both sides of the carriageway for the whole length of the "
                + "deck, the same width as the pavement on the approach — it does not narrow, step "
                + "sideways or change material where the deck begins.",
                "Outside each pavement there is a narrower band of deck stone, of even width, between "
                + "the walk and the deck's own edge.",
                "The deck's two edges are parallel to each other and to the road on it.",
                "A darker line runs along the outer edge of each pavement and along each edge of the "
                + "deck, following them exactly rather than wandering on or off.",
                "The water reads as water on both sides and does not encroach onto the deck.",
                "The road markings continue across the deck in line with those on the approach.",
                "The join between deck and bank is clean — no gap, no step, no overlapping patch.",
            ],
            Expected: "bridge.png"),

        new(
            Name: "city-odesa",
            Group: "wider",
            Map: "Odesa",
            Subject: "A whole hand-traced city, 3 km across: two grids on their own bearings, an "
                     + "orbital ring tying them together, and the sea to the east as the ground legal "
                     + "to nobody. A city is asked this one question only — a detailed question asked "
                     + "of whatever a city happens to contain is a different question every time "
                     + "somebody edits it.",
            // Wide enough that the 3000 x 2304 m map is inside the frame with a margin: a city
            // photographed with its edge outside the picture cannot answer "no street stops in open
            // ground".
            FrameWidthM: 3120f, FinestFeatureM: 7f,
            AtM: null, Seconds: 10, Ui: ["none"],
            Expect:
            [
                "It reads as a city rather than as a lattice: two districts on different bearings, "
                + "tied together by a ring.",
                "The ring turns on the roads between its junctions, not at the junctions themselves.",
                "No street simply stops in open ground: every one runs to a junction or a turning "
                + "head.",
                "Nothing is built on the water, and the coastline is a boundary rather than a hole in "
                + "the map.",
                "Buildings and planting follow the streets everywhere — no district is bare, none is "
                + "piled up.",
                "The whole map is covered by ground, with no bare patch anywhere inside it.",
            ],
            Expected: "city-odesa.png"),
    ];
}
