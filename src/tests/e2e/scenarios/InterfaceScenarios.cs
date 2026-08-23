using System.Numerics;

namespace TrafficSimulation.Tests.E2E.Scenarios;

/// <summary>
/// What the player is shown: the ordinary interface, the menu the game opens on, the instruments, and
/// the debug layers. Every layer here is off in the ordinary game and was switched on for the frame,
/// which is why none of these is part of the fixture's core set.
/// </summary>
internal static class InterfaceScenarios
{
    /// <summary>
    /// What has to be said about a reference frame the other build took with a one-off script rather
    /// than through its own scenario table: the framing was never recorded, so it is the same subject
    /// somewhere else on the map, at some other zoom. Every scenario staged on a named place says
    /// nothing of the sort, because those two frames are the same ground to the metre.
    /// </summary>
    const string AdHocReference =
        "The reference was taken by a one-off script in the other build and its framing was never "
        + "written down: expect the same subject at a different place on the map and a different zoom. "
        + "Compare what is drawn — the layer, the geometry, the paint — never where it is.";

    public static VisualScenario[] All =>
    [
        new(
            Name: "hud",
            Group: "core",
            Map: "Test",
            Subject: "The game as a player sees it, with its ordinary interface up and no debug layer "
                     + "on.",
            // The cap height of the smallest panel text, in world terms.
            FrameWidthM: 120f, FinestFeatureM: 0.6f,
            AtM: new Vector2(240f, 160f), Seconds: 30, Ui: [],
            Expect:
            [
                "The interface panels are on screen and none is clipped by the edge of the frame.",
                "No two panels overlap one another.",
                "Text in the panels is legible and not cut off mid-word.",
                "The panels do not cover the middle of the view where the town is.",
                "The scale legend is in the bottom-right corner, drawn straight on the town with "
                + "nothing behind it: one graduated bar ending on a large mark at each end, its large "
                + "marks carrying figures above them, the last of which names the unit.",
            ],
            Expected: "hud.png"),

        new(
            Name: "start-menu",
            Group: "wider",
            Map: "Test",
            Subject: "The start menu, which is what the game opens on: no city is built until one is "
                     + "picked (GEN-1b). The list is the one list — the same one the command line and "
                     + "the in-game picker read (OBS-2a).",
            // No ground in any of these claims: the frame is the interface at its own scale.
            FrameWidthM: 0f, FinestFeatureM: 0f,
            AtM: null, Seconds: 0, Ui: ["menu"],
            Expect:
            [
                "The game shows a menu rather than a town: nothing has been built yet.",
                "Every place the build ships is a row, each with a name and a one-line description of "
                + "what it is.",
                "Scenarios and check scenes are behind their own pages rather than mixed in with the "
                + "places — a menu of two cities should not read as a menu of two cities and a "
                + "laboratory.",
                "The way out of the game is a button inside the menu, not a key that quits from "
                + "nowhere.",
                "Nothing on the menu is clipped, overlapping or unreadable.",
            ],
            Expected: "start-menu.png"),

        new(
            Name: "ruler-and-legend",
            Group: "wider",
            Map: "Test",
            Subject: "The ruler with two finished measurements kept on the ground and the scale "
                     + "legend in the bottom-right, at a district framing (OBS-2e, OBS-2f). The "
                     + "clicks that made both were fed through the ordinary input path.",
            FrameWidthM: 170.7f, FinestFeatureM: 0.6f,
            AtM: new Vector2(240f, 160f), Seconds: 30, Ui: ["ruler"],
            Expect:
            [
                "Two separate measurements are on the ground at once — a finished measurement is kept "
                + "and the next is laid beside it.",
                "Each tape is graduated, and its graduations are at round numbers of metres.",
                "Each tape carries its total at its far end, with its unit written on it.",
                "The tapes are legible against whatever ground they cross.",
                "The scale legend stands in the bottom-right corner, graduated on the same ladder as "
                + "the tapes, with nothing drawn behind it.",
                "The legend's bar ends on a large mark at both ends.",
            ],
            Expected: "ruler-and-legend.png",
            ExpectedNote: AdHocReference,
            // Two tapes: one along a street and one across country at an angle to it, both ending
            // well inside the frame — a tape whose total is written off the edge cannot be read.
            RulerPointsM:
            [
                new Vector2(150f, 165f), new Vector2(270f, 165f),
                new Vector2(275f, 175f), new Vector2(305f, 205f),
            ]),

        new(
            Name: "debug-networks",
            Group: "wider",
            Map: "Test",
            Subject: "Both routing networks drawn over the lit crossroads — the walking network in "
                     + "green (its nodes as discs, chevrons along each pavement lane and across each "
                     + "crossing) and the driving network in amber (its node at the junction, and the "
                     + "movements a car may make through the box drawn as arcs).",
            FrameWidthM: 56.9f, FinestFeatureM: 0.25f, // the stroke of a chevron
            AtM: new Vector2(270f, 165f), Seconds: 30, Ui: ["nodes"],
            Expect:
            [
                "A node stands only where a body can go more than one way — at the junction and at "
                + "the crossing ends, not part-way along a street.",
                "Every pavement carries two lines of chevrons running opposite ways, one lane each "
                + "way.",
                "Chevrons run across each crossing, joining the pavements on the two banks: a "
                + "crossing is a link of the walking network.",
                "The junction's amber arcs are the movements a car may make through it — straight "
                + "through, and the turns — and each is a curve a car could actually drive rather "
                + "than a corner cut square.",
                "Nothing is drawn running through a building, over a verge or across the box "
                + "off-line.",
                "The two networks are drawn in different colours.",
            ],
            Expected: "debug-networks.png",
            ExpectedNote: AdHocReference),

        new(
            Name: "debug-car-lines",
            Group: "wider",
            Map: "Test",
            Subject: "The same traffic with the driving layer on: every car's manoeuvre named where "
                     + "it happens, and the line it is actually holding drawn ahead of it (OBS-2d).",
            FrameWidthM: 64f, FinestFeatureM: 0.3f,
            AtM: new Vector2(270f, 165f), Seconds: 90, Ui: ["car-lines"],
            Expect:
            [
                "Every driving car carries a label naming one manoeuvre from the catalogue — never a "
                + "generic state such as 'driving' or 'busy'.",
                "Each line runs ahead of its own car, from where the body is, and not from where it "
                + "started.",
                "A line is drawn for the rear axle: it leaves the body along the car's own axis "
                + "rather than swinging out of the middle of it.",
                "Each line stays on the carriageway it is driving — it does not cross a far kerb, cut "
                + "a corner over the pavement, or trail off into open ground.",
                "A line through the junction is one of the movements the junction offers, and joins "
                + "its arm's lane at both ends.",
                "Nothing about a car is written as a number in a panel that could have been drawn "
                + "where it happens.",
            ],
            Expected: "debug-car-leave-bay.png",
            ExpectedNote: "The reference is one car executing P-2 leave the bay at a lot; this frame "
                          + "is the same layer over moving traffic. Compare what the layer draws — "
                          + "the label, the rear-axle line, the pose it ends on — not the place."),

        new(
            Name: "debug-collision",
            Group: "wider",
            Map: "Test",
            Subject: "A lit T junction at close range with collision shapes switched on (OBS-2c) — "
                     + "the outlines are the shapes the physics engine actually holds, not the art.",
            FrameWidthM: 42.7f, FinestFeatureM: 0.15f,
            AtM: new Vector2(270f, 60f), Seconds: 90, Ui: ["collision"],
            Expect:
            [
                "Every body draws a shape, and the shape is the one the engine has — a circle for a "
                + "person and for a prop, a box for a car.",
                "A shape is centred on its body and is the size the body is drawn at; none is offset, "
                + "doubled or left behind.",
                "No two shapes overlap one another.",
                "The junction's tarmac is one continuous shape, its corners rounded, with the "
                + "crossings striped across each arm.",
                "Every signal head stands on the pavement or verge, upright and square to the arm it "
                + "governs.",
                "The stop bar on each arm covers the approaching lane only.",
            ],
            Expected: "debug-collision.png",
            ExpectedNote: AdHocReference),
    ];
}
