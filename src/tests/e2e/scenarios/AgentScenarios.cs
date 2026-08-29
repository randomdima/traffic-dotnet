using System.Numerics;

namespace TrafficSimulation.Tests.E2E.Scenarios;

/// <summary>
/// The bodies and what they do: cars where they are kept, cars as art, cars in motion, and people on
/// foot. Everything here is a picture of the simulation rather than of the plan, so every frame is
/// taken well into a seeded run.
/// </summary>
internal static class AgentScenarios
{
    public static VisualScenario[] All =>
    [
        new(
            Name: "parking",
            Group: "core",
            Map: "Test",
            Subject: "A parking lot off the south-east street, with cars in it.",
            FrameWidthM: 36f, FinestFeatureM: 0.15f,
            AtM: new Vector2(309.1f, 180.8f), Seconds: 10, Ui: ["none"],
            Expect:
            [
                "The bay markings are parallel to one another and evenly pitched across the lot.",
                "The markings lie on the lot's tarmac and none runs off it onto grass.",
                "Every parked car sits square within a bay, aligned with the bay's markings rather "
                + "than at an angle to them.",
                "No car straddles two bays and no two cars share one.",
                "The lot has a clear opening onto the road it is served from.",
                "The lot's tarmac meets the road's without a gap or an overlapping patch.",
            ],
            Expected: "parking.png"),

        new(
            Name: "car-fleet",
            Group: "core",
            Map: "Test",
            Subject: "Four parked cars from four different lots at extreme close range — the car art "
                     + "itself, one car per cell. The claims are asked of EVERY cell.",
            FrameWidthM: 8f, FinestFeatureM: 0.04f, // a tread block
            AtM: new Vector2(306.5f, 180.5f), Seconds: 10, Ui: ["none"],
            Expect:
            [
                "Each car reads as a car seen from above: a body with a windscreen and four wheels.",
                "All four wheels sit at the corners of the body — none floating clear of it, none "
                + "buried inside it.",
                "The wheels are all the same size, and the rear pair is parallel to the body's long "
                + "axis.",
                "Each tyre carries a tread pattern rather than a flat block of colour; it repeats at "
                + "an even pitch, runs along the direction the wheel rolls, and is neither stretched "
                + "nor torn where it wraps.",
                "The body's proportions look like a car's — not squashed, stretched or sheared.",
                "Each car sits on the ground with no gap or shadow-seam showing between body and "
                + "surface, and no car's art is drawn over the top of another object.",
            ],
            Expected: "car-variants.png",
            ExpectedNote: "The reference is the same kind of sheet — one car per cell at this same 8 m "
                          + "framing — but its cells were found at run time in the other build, so it "
                          + "shows different cars in different bays. Compare car against car, never "
                          + "cell against cell.",
            // Four cars standing in four different lots at t=10 s. Fixed places, not a search of the
            // town: a sheet that picked "the four nearest cars" showed the same variant twice while
            // claiming to be a catalogue.
            Cells:
            [
                ("north-lot", new Vector2(262.4f, 96.9f)),
                ("west-lot", new Vector2(217.0f, 156.3f)),
                ("south-lot", new Vector2(306.5f, 180.5f)),
                ("east-lot", new Vector2(333.9f, 181.4f)),
            ]),

        new(
            Name: "traffic",
            Group: "core",
            Map: "Test",
            Subject: "Moving traffic at the lit crossroads, a minute and a half into the run. The "
                     + "fixture carries 25 cars over the whole town, so a handful in frame is the "
                     + "expected density — the claims are about the ones that are there.",
            // "Straddles the centreline" is a claim about a car against a painted line.
            FrameWidthM: 64f, FinestFeatureM: 0.3f,
            AtM: new Vector2(270f, 165f), Seconds: 90, Ui: ["none"],
            Expect:
            [
                "Every car is within its own lane, on the correct side of the centreline for the "
                + "direction it is pointing.",
                "No car straddles the centreline or a kerb line.",
                "Each car points along its lane rather than across it.",
                "No two cars overlap one another.",
                "Cars following one another leave a visible gap; they are not nose-to-tail touching.",
                "No car is on the pavement, the grass or a building.",
                "A car stopped at an arm whose head is red is behind that arm's stop bar, not over it.",
            ],
            Expected: "traffic.png",
            ExpectedNote: "The reference frame is 64 m across like this one but is NOT this place: the "
                          + "other build chose its spot at run time, by looking for wherever its own "
                          + "cars happened to be, and that spot is empty road in this one. Compare "
                          + "what traffic looks like, not where it is."),

        new(
            Name: "car-lamps",
            Group: "core",
            Map: "Test",
            Subject: "A taxi held at the crossroads' painted bar with its lamps lit (CAR-14). What the "
                     + "lamps SAY is arithmetic and is tested as such; what no number can answer is "
                     + "whether they read as lamps on a car — sections of it that light — from the "
                     + "height a street is watched at.",
            FrameWidthM: 10f, FinestFeatureM: 0.04f, // a lens's own edge, which is what "on the corner" is judged on
            AtM: new Vector2(268f, 155.8f), Seconds: 29, Ui: ["none"],
            Expect:
            [
                "Two red lamps are lit, one at each corner of the same end of the car.",
                "An amber lamp is lit at one corner of the other end.",
                "Each lit lamp is a small bright patch with light spilling a little around it, sitting "
                + "on the corner of the bodywork rather than floating over the middle of a panel.",
                "Every lamp, lit or not, lies on the car's own bodywork — none hangs off its outline "
                + "onto the road beside or beyond it.",
                "The two red lamps are the same distance in from their nearest corner as one another.",
                "No lamp washes the whole car out: the bodywork's own colour still reads under and "
                + "around them.",
            ]),

        new(
            Name: "service-lamps",
            Group: "core",
            Map: "Odesa",
            Subject: "Ambulances standing on their hospital's apron, none of them out on a call "
                     + "(CAR-14a, AMB-4b). Their art is painted with a two-section lamp bar across "
                     + "the roof; what no number can answer is whether that bar reads as unlit glass "
                     + "rather than as a beacon that is on.",
            FrameWidthM: 16f, FinestFeatureM: 0.05f, // one end of a beacon bar
            AtM: new Vector2(1489f, 1035f), Seconds: 45, Ui: ["none"],
            Expect:
            [
                "No lamp on any of these cars is lit: nothing on them glows or spills light onto the "
                + "bodywork or the ground.",
                "Each roof bar still reads as two lensed sections either side of a dark centre — "
                + "darkened glass, not a black gap in the roof.",
                "The corner lamps at each end of each car are dark in the same way.",
            ]),

        new(
            Name: "tow-arm",
            Group: "core",
            Map: "Test",
            Subject: "The evacuator standing in its own bay at the depot, with nothing on the arm, so "
                     + "the arm is drawn in over its own deck (EVA-5). The arm is the one part of any "
                     + "vehicle here drawn as a picture of its own, and what no number can answer is "
                     + "whether it reads as one machine with its gear stowed rather than as two things "
                     + "parked on top of each other.",
            FrameWidthM: 14f, FinestFeatureM: 0.08f, // the fork at the far end of the arm
            AtM: new Vector2(305.1f, 180.4f), Seconds: 10, Ui: ["none"],
            Expect:
            [
                "One recovery truck reads as a single vehicle: a cab, a flat deck behind it, and gear "
                + "folded down onto that deck rather than a second machine standing on it.",
                "The stowed gear is in line with the truck's own long axis, not skewed across it, and "
                + "it reaches about as far back as the truck's own tail and no further.",
                "It sits on the deck rather than floating clear of the bodywork, and the end nearest "
                + "the tail is wider than the part in front of it.",
                "The truck's four wheels are visible at the corners of its body, none of them hidden "
                + "under the stowed gear's own picture.",
                "Nothing on the truck is lit: no lamp glows or spills light onto the bodywork or ground.",
            ]),

        new(
            Name: "pedestrians",
            Group: "core",
            Map: "Test",
            Subject: "People walking on the pavement south-east of the crossroads.",
            FrameWidthM: 20f, FinestFeatureM: 0.25f, // a person is about a metre across; a limb is this
            AtM: new Vector2(290f, 176f), Seconds: 30, Ui: ["none"],
            Expect:
            [
                "Every person stands on grass, pavement, footway or a crossing — none is out on a "
                + "carriageway away from a crossing.",
                "Each person reads as a person seen from above, upright and of a consistent size.",
                "No two people overlap one another.",
                "Nobody stands inside a building, a tree or a car.",
                "People who are walking face the way they are going.",
            ],
            Expected: "pedestrians.png",
            ExpectedNote: "The reference frame is 20 m across like this one but is NOT this place: the "
                          + "other build chose its spot at run time, by looking for wherever its own "
                          + "walkers happened to be. Compare what people look like and where they "
                          + "stand, not which pavement they are on."),
    ];
}
