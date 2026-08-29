using System.Numerics;

namespace TrafficSimulation.Tests.E2E.Scenarios;

/// <summary>
/// Where roads meet, what is painted there and what governs it — the four kinds of junction, the
/// paint at close range, the signal heads, and the crossing that is deliberately off square.
/// </summary>
internal static class JunctionScenarios
{
    public static VisualScenario[] All =>
    [
        new(
            Name: "junction-kinds",
            Group: "core",
            Map: "Test",
            Subject: "The four kinds of junction the town lays, one frame each in this order: 1 the "
                     + "lit crossroads, 2 a lit T, 3 the inline junction (two collinear arms carrying "
                     + "one lit mid-block crossing), 4 a dead end (a turning head). The claims are "
                     + "asked of EVERY frame.",
            // A stop bar's thickness, not a line's width: these claims are about which paint is
            // there and what shape the tarmac is, and the width of a marking is junction-paint's
            // subject at three times this scale.
            FrameWidthM: 46f, FinestFeatureM: 0.5f,
            AtM: new Vector2(270f, 165f), Seconds: 10, Ui: ["none"],
            Expect:
            [
                "Frame 1 is a crossroads: four arms meeting, each with a stop bar and a zebra.",
                "Frame 2 is a T: three arms, and the fourth direction is not a road.",
                "Frame 3 is a mid-block crossing on an otherwise straight road: one zebra, no side "
                + "road.",
                "Frame 4 is a dead end: one arm, opening into a rounded head with no road beyond it.",
                "In every frame the junction's tarmac is one continuous shape with no gap, notch or "
                + "overlapping patch in it.",
                "In every frame the corners between arms are rounded arcs, not sharp right angles.",
                "In every frame, no marking is left stranded on grass.",
            ],
            Expected: "junction-kinds.png",
            ExpectedNote: "The reference is the same four subjects tiled into one 2x2 sheet with "
                          + "magenta gutters, in this same reading order and at this same 46 m "
                          + "framing. The magenta is a gutter and never part of a picture.",
            Cells:
            [
                ("crossroads", new Vector2(270f, 165f)),
                ("tee-north", new Vector2(270f, 60f)),
                ("inline-crossing", new Vector2(330f, 60f)),
                ("dead-end-east", new Vector2(452f, 60f)),
            ]),

        new(
            Name: "junction-paint",
            Group: "core",
            Map: "Test",
            Subject: "The lit crossroads at close range: its stop bars, its zebras and the kerb arcs "
                     + "rounding its corners. Its signal heads are in frame too, but they are "
                     + "junction-heads' subject.",
            FrameWidthM: 52f, FinestFeatureM: 0.15f,
            AtM: new Vector2(270f, 165f), Seconds: 10, Ui: ["none"],
            Expect:
            [
                "Every arm has a stop bar: a solid line drawn square across the road, perpendicular "
                + "to that arm's direction rather than to the screen.",
                "A stop bar covers the approaching lane only — half the carriageway, from the "
                + "centreline to its own kerb — and not the full width of the road.",
                "Each stop bar stops at the kerb; none overhangs onto the pavement or the grass.",
                "Each zebra is a set of parallel bars, evenly spaced and all the same width, running "
                + "along the direction of the traffic that crosses them.",
                "Unlike a stop bar, a zebra spans the whole carriageway, kerb to kerb.",
                "Each zebra sits between its stop bar and the junction, and does not overlap the bar.",
                "The dashed centreline on each arm stops before the junction rather than running into "
                + "it.",
            ],
            Expected: "junction-paint.png"),

        new(
            Name: "junction-heads",
            Group: "core",
            Map: "Test",
            Subject: "The signal heads at a lit T junction, half a minute into the run.",
            // Half a lens: "exactly one lit lamp" is a claim about telling lenses apart.
            FrameWidthM: 40f, FinestFeatureM: 0.15f,
            AtM: new Vector2(270f, 60f), Seconds: 30, Ui: ["none"],
            Expect:
            [
                "Every signal head stands beside the carriageway, on the pavement or verge — never "
                + "out on the tarmac.",
                "Each head is upright and square to the arm it governs, not tilted at a random angle.",
                "Each car head shows exactly one lit lamp; none shows two and none shows none.",
                "Heads facing opposite arms of the same axis show the same colour as each other.",
                "Where a car head is green, the pedestrian head for the crossing over that arm is "
                + "red, and vice versa.",
                "No head stands on top of another, or overlaps a building, tree or kerb.",
            ],
            Expected: "junction-heads.png"),

        new(
            Name: "crossing-skewed",
            Group: "wider",
            Map: "Zebras",
            Subject: "The scenario map's skewed crossing — paint deliberately laid off square to its "
                     + "street, with the body this map paces over it and the walking layer on. This is "
                     + "the one case in the set that can fail while every square crossing passes, which "
                     + "is why the fixture exists.",
            FrameWidthM: 42.7f, FinestFeatureM: 0.15f,
            AtM: new Vector2(500f, 301f), Seconds: 45, Ui: ["walker-lines"],
            Expect:
            [
                "The street, its pavements, its kerb line and its centreline all run at the street's "
                + "own angle — nothing is drawn square to the screen.",
                "The paint is skewed to the street rather than square to it, and its bars stay "
                + "parallel to one another and evenly spaced.",
                "The crossing spans the whole carriageway, kerb to kerb, even though it is skewed.",
                "The pavement runs on through the crossing on both banks rather than stopping at it.",
                "No dash of the centreline is drawn over the crossing's paint.",
                "One person is on or beside the crossing, and the line drawn ahead of them runs across "
                + "the road over the paint rather than off down the carriageway.",
            ],
            Expected: "zebra-skewed.png",
            ExpectedNote: "The reference was taken by a one-off script in the other build and its "
                          + "framing was never written down: it is a closer view of a skewed crossing "
                          + "somewhere on this map, with a walker on it. Compare what is drawn — the "
                          + "paint, the pavement, the line the walker is holding — and never where it "
                          + "is."),
    ];
}
