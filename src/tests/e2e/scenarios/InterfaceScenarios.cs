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
                "One line in the top-left corner says what the run is: its frame rate, the name of the "
                + "map, and its pace. It is collapsed, so nothing else is written under it.",
                "Two square buttons stand in the top-right corner, one marked with a question mark and "
                + "one with a menu glyph, and neither has a panel open under it.",
                "The scale legend is in the bottom-right corner, drawn straight on the town with "
                + "nothing behind it: one graduated bar ending on a large mark at each end, its large "
                + "marks carrying figures above them, the last of which names the unit.",
            ],
            Expected: "hud.png",
            ExpectedNote: "The reference is the older interface, whose top-left corner was two boxes of "
                          + "text and whose top-right was one button. Judge the claims against this "
                          + "frame; the corners differing from the reference is the change itself."),

        new(
            Name: "turned-town",
            Group: "wider",
            Map: "Test",
            Subject: "The same town drawn turned (OBS-1c), which is the one thing about the view that "
                     + "nothing but a picture can check: the turn is applied in every vertex stage, and "
                     + "a sign wrong in one of them shows as that layer alone standing the wrong way.",
            FrameWidthM: 120f, FinestFeatureM: 0.6f,
            AtM: new Vector2(240f, 160f), Seconds: 30, Ui: [],
            Expect:
            [
                "The roads run diagonally across the frame rather than square to its edges.",
                "The ground, the buildings, the road markings, the crossings and the vehicles are all "
                + "turned by the same angle: no layer stands square while the rest lean.",
                "Every vehicle sits along the lane it is in, and every building sits square to the "
                + "street it stands on.",
                "The interface is not turned: the panels, their text and the scale legend are upright "
                + "and square to the edges of the frame.",
                "A third square button stands in the top-right corner, to the left of the question "
                + "mark, carrying a two-tone needle leaning by about the angle the town is turned by.",
            ],
            TurnDeg: 30f),

        new(
            Name: "status-panel",
            Group: "wider",
            Map: "Test",
            Subject: "The status panel with its body open: the run's own line, and under it what the "
                     + "frame cost, where the tick went, and the census the figures were taken over.",
            FrameWidthM: 120f, FinestFeatureM: 0.6f,
            AtM: new Vector2(240f, 160f), Seconds: 30, Ui: ["frame"],
            Expect:
            [
                "The panel is in the top-left corner and its first line carries the frame rate, the "
                + "map name and the pace.",
                "Under a rule below that line the rows are indented under headings, and each heading "
                + "is marked to say whether what is under it is showing.",
                "Every row carries a figure with its unit, and the figures line up in one column.",
                "The panel's rows all end inside it: nothing is drawn through its bottom edge.",
                "The offscreen path times no frames, so the frame heading says it was not measured "
                + "rather than printing zeroes under it.",
            ]),

        new(
            Name: "start-menu",
            Group: "wider",
            Map: "Idle",
            Subject: "The start menu, which is what the game opens on: no city is built until one is "
                     + "picked, and what the panel stands over is the idle ring (GEN-1b). The map list "
                     + "is the one list — the same one the command line and the in-game picker read "
                     + "(OBS-2a).",
            FrameWidthM: 0f, FinestFeatureM: 0f,
            AtM: null, Seconds: 30, Ui: ["menu"],
            Expect:
            [
                "The panel stands in the middle of the screen rather than hanging off a button in a "
                + "corner, and there are no corner buttons drawn at all.",
                "It sits inside the ring of road, on the grass in the middle of it: the loop is a "
                + "square with rounded corners, it is unbroken all the way round, and none of the "
                + "panel's four corners reaches it.",
                "There is no tab strip. The panel's name is across the top and a red button that "
                + "leaves the game stands at the end of that same line.",
                "Every place the build ships is a row carrying its name, written large, over a "
                + "description of what it is that runs onto a second line rather than being cut.",
                "Both groups are open — the places and the scenarios under them — and the list is "
                + "longer than the panel, so it carries a scroll bar down its right-hand edge.",
                "There is no close button on the panel, and no frame-rate read-out or scale bar over "
                + "the ring behind it.",
                "Nothing on the menu is clipped, overlapping or unreadable.",
            ],
            Expected: "start-menu.png",
            ExpectedNote: "The reference is the older menu, which hung under the corner button over an "
                          + "empty screen and cut its pages four ways. Judge the claims against this "
                          + "frame; where it stands, what is behind it and how many tabs it has are "
                          + "the change itself."),

        new(
            Name: "controls-card",
            Group: "wider",
            Map: "Test",
            Subject: "The control legend, which is its own popup under the question mark beside the "
                     + "gear rather than a page of the menu.",
            FrameWidthM: 120f, FinestFeatureM: 0.6f,
            AtM: new Vector2(240f, 160f), Seconds: 30, Ui: ["controls"],
            Expect:
            [
                "The panel hangs under the question-mark button in the top-right corner, aligned to "
                + "that button's outer edge.",
                "It is two columns: the key on the left and what it does on the right, one pair a row.",
                "Every row's key column carries something legible — no row has an empty key.",
                "The camera, the selection, the orders, the drive keys, the handbrake, the pace and "
                + "freeze keys, fullscreen and Escape are all named.",
                "Nothing on it is clipped, overlapping or unreadable.",
            ]),

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
                + "way, and the chevrons of the two lines stand square across the pavement from each "
                + "other rather than drifting out of step along it.",
                "A line runs across each crossing, joining the pavements on the two banks: a crossing "
                + "is a link of the walking network.",
                "The crossings are marked with bars square across them rather than with chevrons, "
                + "because a crossing is too narrow for a lane each way and is therefore one line "
                + "walked in both directions.",
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
            Name: "debug-bay-ways",
            Group: "wider",
            Map: "Test",
            Subject: "One parking lot with the network layer on: the two ways at every bay — the line a "
                     + "car is driven in on and the line it is backed out on — which are ways of the "
                     + "driving network like the movements through a junction (GEN-4f).",
            FrameWidthM: 24f, FinestFeatureM: 0.25f, // the stroke of a chevron
            AtM: new Vector2(306.5f, 181.5f), Seconds: 12, Ui: ["nodes"],
            Expect:
            [
                "Every bay of the lot has amber lines running between it and the road — a bay that "
                + "nothing reaches would be a car park no route can use.",
                "Each of those lines leaves or meets the carriageway on a curve a car could actually "
                + "drive, rather than a corner cut square.",
                "The lines arriving at a bay end inside it, lined up with the bay's own markings "
                + "rather than at an angle across them.",
                "Each line carries a dot at both ends: where it meets the road and where it reaches "
                + "the bay.",
                "The chevrons on a line all point the same way along it, and the lines into the bays "
                + "point the opposite way from the lines out of them.",
                "The two lines at one bay are told apart by their shade as well as by their chevrons: "
                + "the line in is the amber the rest of the driving network is drawn in, and the line "
                + "out — which is driven backwards — is a paler shade of it.",
                "The bay lines are shades of the amber the movements through junctions elsewhere in "
                + "the frame are drawn in, and not a colour of their own.",
                "No bay line runs through a building or through a parked car's body.",
            ]),

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

        new(
            Name: "debug-turn-circles",
            Group: "wider",
            Map: "Skidpad",
            Subject: "One square of the skidpad with the turn-circle layer on (OBS-2j): the circle the "
                     + "car's axles ask for, drawn over the circle its tyres wrote on the road. How far "
                     + "the two differ is the measurement the pad exists to take and the probe is what "
                     + "takes it; what this frame is for is that the construction drawn is the right one.",
            FrameWidthM: 40f, FinestFeatureM: 0.15f,
            AtM: new Vector2(250f, 50f), Seconds: 20, Ui: ["turn-circles"],
            // Claims here are answerable off shapes a reviewer can see whole. Two things are not: the
            // sprite draws no steering angle on its wheels, so "the front wheels are turned to the left"
            // sends a reviewer cropping for art that does not exist; and a corner of a car 4 m wide in a
            // 40 m frame is a few pixels of anti-aliasing, so which corner a line touches is not a
            // question the frame answers. Both cost the judge its whole budget and settled nothing.
            Expect:
            [
                "Three straight spokes are drawn, and all three meet at one point, which is the centre "
                + "of the drawn circle.",
                "Every one of the three spokes reaches the car: none stops short in open road, and none "
                + "runs out past the car on the far side.",
                "The centre stands off to one side of the car rather than under it, and the tyre tracks "
                + "on the road curve around that same side.",
                "The whole of the ground in the frame is road: no grass, no kerb and no pavement.",
            ]),

        // The two squares the pad exists to be read against each other. Both are the ahead, full-pedal
        // row at 150 s, by when every circle on it has settled: one look holds the circle its axles ask
        // for and one runs many times wide of it, and the layer draws the same construction over both.
        new(
            Name: "debug-turn-circle-runs-wide",
            Group: "wider",
            Map: "Skidpad",
            Subject: "The muscle car's square of the skidpad, driving ahead on full left lock with the "
                     + "pedal pinned, once its circle has settled (OBS-2j). The frame is the whole circle "
                     + "it turns, with the circle its axles ask for drawn over the car.",
            // Mid-pad rather than the leftmost column: a circle this wide framed on column one reaches
            // off the map, and the ground in the frame is part of what is claimed.
            FrameWidthM: 130f, FinestFeatureM: 0.4f,
            AtM: new Vector2(850f, 250f), Seconds: 150, Ui: ["turn-circles"],
            Expect:
            [
                "The tyre tracks on the road form one closed ring, and the car stands on that ring.",
                "The drawn circle is a small ring beside the car, many times smaller than the ring the "
                + "tyres wrote — the car is going round far wider than its own axles ask for.",
                "The drawn centre stands to one side of the car, and the ring the tyres wrote is "
                + "centred to that same side — the two agree about which way round the car is going.",
                "The ring the tyres wrote is a circle and not a spiral: its arcs close on themselves "
                + "rather than opening steadily outwards.",
                "The whole of the ground in the frame is road: no grass, no kerb and no pavement.",
            ]),

        new(
            Name: "debug-turn-circle-holds",
            Group: "wider",
            Map: "Skidpad",
            Subject: "The sports car's square of the same row, at the same moment — a look whose tyres "
                     + "hold what its lock asks for, so that the two circles can be seen to agree "
                     + "(OBS-2j).",
            FrameWidthM: 40f, FinestFeatureM: 0.22f,
            AtM: new Vector2(350f, 250f), Seconds: 150, Ui: ["turn-circles"],
            Expect:
            [
                "The drawn circle and the tyre tracks are arcs about one centre, within about a car's "
                + "width of each other — the drawn circle is comparable in size to them and not a "
                + "fraction of them.",
                "The tracks are several concentric arcs, one a wheel, and the drawn circle lies among "
                + "them or just inside the innermost.",
                "The drawn centre stands where the three spokes meet, to the side of the car the tracks "
                + "curve towards.",
                "The whole of the ground in the frame is road: no grass, no kerb and no pavement.",
            ]),
    ];
}
