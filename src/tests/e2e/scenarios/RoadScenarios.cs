using System.Numerics;

namespace TrafficSimulation.Tests.E2E.Scenarios;

/// <summary>
/// The carriageway and what runs alongside it: the straight that everything else is judged against,
/// the two ways a road changes direction, and the pavement round a corner.
/// </summary>
internal static class RoadScenarios
{
    public static VisualScenario[] All =>
    [
        new(
            Name: "road-straight",
            Group: "core",
            Map: "Test",
            Subject: "An ordinary straight street with pavement either side — the control the bend is "
                     + "judged against.",
            FrameWidthM: 26f, FinestFeatureM: 0.15f, // a painted line
            AtM: new Vector2(210f, 165f), Seconds: 10, Ui: ["none"],
            Expect:
            [
                "The dashed centreline runs down the middle of the carriageway, equidistant from both "
                + "kerbs.",
                "The dashes are all the same length and the gaps between them are all the same "
                + "length.",
                "The dashes lie along the road, not at an angle to it, and none is bent or forked.",
                "A solid kerb line runs along each side, parallel to the centreline at a constant "
                + "distance.",
                "No paint of any kind lies on the grass or the pavement — every marking is on tarmac.",
                "The pavement is a band of even width along each side, with no notch, splinter or gap "
                + "of grass cutting across it.",
                "A darker line of even width runs along the outer edge of the pavement where it meets "
                + "the grass, on the pavement side of that boundary rather than out on the verge.",
            ],
            Expected: "road-straight.png"),

        new(
            Name: "road-bend",
            Group: "core",
            Map: "Test",
            Subject: "The south street, which bows gently between its two junctions.",
            FrameWidthM: 40f, FinestFeatureM: 0.15f,
            AtM: new Vector2(330f, 165f), Seconds: 10, Ui: ["none"],
            Expect:
            [
                "The centreline is a smooth curve, not a chain of straight segments with visible "
                + "corners between them.",
                "The dashes stay evenly pitched round the bend; they do not bunch on the inside or "
                + "stretch on the outside.",
                "The centreline stays midway between the two kerbs the whole way round.",
                "The carriageway keeps a constant width round the bend.",
            ],
            Expected: "road-bend.png"),

        new(
            Name: "road-corner",
            Group: "core",
            Map: "Test",
            Subject: "Where the arterial turns south into the west street. This is a ROAD THAT TURNS, "
                     + "not a junction — it carries no crossings and no lights, and that is deliberate.",
            FrameWidthM: 44f, FinestFeatureM: 0.15f,
            AtM: new Vector2(150f, 60f), Seconds: 10, Ui: ["none"],
            Expect:
            [
                "The carriageway turns as one continuous piece of tarmac; there is no junction box, "
                + "no stop bar and no zebra anywhere in the frame.",
                "There are no traffic lights in the frame.",
                "The centreline follows the turn smoothly and stays midway between the two kerbs "
                + "throughout.",
                "The kerb lines stay parallel to the carriageway edge round the whole turn; neither "
                + "cuts the corner nor throws a spur of paint onto the verge.",
                "The tarmac has no fin, wedge or overshoot where the turn meets either straight.",
                "The pavement follows the turn on both sides at a constant width.",
            ],
            Expected: "road-corner.png"),

        new(
            Name: "pavement-corner",
            Group: "core",
            Map: "Test",
            Subject: "The pavement swept round the crossroads' corner, at close range.",
            FrameWidthM: 26f, FinestFeatureM: 0.15f,
            AtM: new Vector2(270f, 165f), Seconds: 10, Ui: ["none"],
            Expect:
            [
                "The pavement follows the kerb round the corner as a smooth swept band.",
                "Its width is the same all the way round — it does not pinch to nothing at the apex "
                + "or balloon on the outside.",
                "There is no splinter, sliver or wedge of grass cutting across the pavement.",
                "The pavement does not turn inside out, cross over itself, or leave a triangular hole "
                + "at the join.",
                "Where the pavement meets a crossing it lines up with it rather than stopping short "
                + "or running past.",
                "The darker edge line follows the pavement's outer edge round the corner as one "
                + "unbroken curve — it does not step, double back, or strike across the walk where "
                + "two pieces of pavement meet.",
            ],
            Expected: "pavement-corner.png"),
    ];
}
