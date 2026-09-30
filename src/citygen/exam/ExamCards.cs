using static TrafficSimulation.CityGen.Exam.ExamArm;
using static TrafficSimulation.CityGen.Exam.ExamSide;

namespace TrafficSimulation.CityGen.Exam;

/// <summary>
/// <b>The scenario map's cards</b>: traffic staged one scenario to a junction of the lattice, each saying what
/// the engine is to make of it and which of the engine's own rules that exercises.
/// </summary>
/// <remarks>
/// <para>
/// <b>A card is an end-to-end case of this engine and not of anybody's rule book.</b> What it expects is what
/// the town's own rules say — a right of way that orders who waits (TER-5e), the ladder it is read off
/// (TER-5g), ways that meet only where their ribbons do (TER-5c), a red, somebody on the paint — and the
/// larger part of the table is the plainest of it: a car alone, and cars whose ways never meet, none of which
/// may be held at all. <b>What the engine refuses to do is not asked of it</b>: nothing turns round in a box
/// (TER-5f) or at a dead end (TER-5a). One card is about getting past something (CAR-46), on a street short
/// of the box — a pass is never taken over a zebra's paint, and a lattice junction has one on every arm.
/// </para>
/// <para>
/// <b>Every card is written in its own frame</b>, north up and traffic keeping right: a car from the south
/// turns right into the east arm and left into the west. A T is open to the north, so its stem is the south
/// arm; a dead end's one arm is its south.
/// </para>
/// <para>
/// <b>A card is asked, not assumed</b>: the car that has to give way stands a little nearer than the one it
/// gives way to, so it would be there first if it did not; somebody on foot steps out as the car they are
/// about comes within a stated distance; and a card about a light sends its car on the colour it is about.
/// </para>
/// <para>
/// <b>A card's cars wear the fleet's looks in the order the table stands them</b>, and a look is a build —
/// a weight and a pull away — so a card is added at the end of the table rather than inserted, or every card
/// after it is asked of different cars.
/// </para>
/// </remarks>
internal static class ExamCards
{
    /// <summary>How far back a car starts by default: room to pull away, reach a town speed and stop again.</summary>
    const float BackM = 35f;

    /// <summary>
    /// How far back the car that has to give way starts — <b>nearer than the one it gives way to</b>, so a
    /// car that did not give way would be over the shared ground first.
    /// </summary>
    const float NearerM = 30f;

    /// <summary>The car in front of a pair on one lane: behind the paint on its own arm, and no nearer.</summary>
    const float AheadM = 20f;

    /// <summary>A second car on one lane, far enough back to be a second car rather than the same one.</summary>
    const float BehindM = 45f;

    /// <summary>And a third.</summary>
    const float ThirdM = 60f;

    /// <summary>Far enough back that the other car is through the junction before this one reaches it.</summary>
    const float FarM = 60f;

    /// <summary>Near enough to the box that the car is over it before anything farther off arrives.</summary>
    const float CloseM = 12f;

    /// <summary>How far past the box a driver is sent: clear of the junction and well short of the next one.</summary>
    const float RunOnM = 40f;

    /// <summary>
    /// <b>Where the first of two cars leaving by one arm is sent</b>, past where the second is — so the one the
    /// right of way sends first is not stood in the lane the other has still to drive down. A car stood on its
    /// own place is a car the other queues behind for good.
    /// </summary>
    const float WellPastM = 55f;

    /// <summary>And the first of three.</summary>
    const float FurthestM = 70f;

    /// <summary>
    /// How far past the box a car stands in the lane leaving it, when the card is about the box being blocked
    /// on the far side — close enough that nothing fits between it and the box.
    /// </summary>
    const float BlockingM = 12f;

    /// <summary>How long a car blocking the far side stands there before it drives on.</summary>
    const float BlocksForS = 10f;

    /// <summary>A car short of a dead end's head, in the lane leaving it.</summary>
    const float ShortOfTheHeadM = 20f;

    /// <summary>And where it is sent down the spur: far enough to be a drive, short of the junction at the other end.</summary>
    const float DownTheSpurM = 50f;

    public static ReadOnlySpan<ExamCard> All => Table;

    /// <summary>How a movement is laid through a junction, which is all a car alone asks of one.</summary>
    const string Movements = "TER-5d";

    const string Ring = "GEN-19";

    /// <summary>Two ways meet only where their ribbons do, so ways that share no ground never hold each other.</summary>
    const string Apart = "TER-5c";

    /// <summary>A body standing is on the ways its collider is over and on nothing beside them.</summary>
    const string Standing = "TER-4c.2";

    const string RightOfWay = "TER-5e, TER-5g";

    /// <summary>A plan is cut at the first body in front of it.</summary>
    const string Queue = "TER-4c.1";

    const string Light = "CAR-6.3, TLT-4";

    const string Paint = "PER-27, TER-5g";

    const string Call = "AMB-4, TER-5g";

    const string OneWay = "TER-4d";

    static readonly ExamCard[] Table =
    [
        // A car alone: every movement every shape offers.
        Card("Straight through an empty crossroads", ExamFamily.Alone, ExamShape.Crossroads,
            "With nothing else at the junction a car goes straight through without stopping.", Movements,
            [Drives(South, North)], [Free(0)]),
        Card("Right turn at an empty crossroads", ExamFamily.Alone, ExamShape.Crossroads,
            "With nothing else at the junction a car turns right without stopping.", Movements,
            [Drives(South, East)], [Free(0)]),
        Card("Left turn at an empty crossroads", ExamFamily.Alone, ExamShape.Crossroads,
            "With nothing else at the junction a car turns left without stopping.", Movements,
            [Drives(South, West)], [Free(0)]),
        Card("Right out of the stem of an empty T", ExamFamily.Alone, ExamShape.Tee,
            "With nothing on the through road a car turns right out of the stem without stopping.", Movements,
            [Drives(South, East)], [Free(0)]),
        Card("Left out of the stem of an empty T", ExamFamily.Alone, ExamShape.Tee,
            "With nothing on the through road a car turns left out of the stem without stopping.", Movements,
            [Drives(South, West)], [Free(0)]),
        Card("Right into the stem of an empty T", ExamFamily.Alone, ExamShape.Tee,
            "With nothing else at the junction a car turns right into the stem without stopping.", Movements,
            [Drives(West, South)], [Free(0)]),
        Card("Left into the stem of an empty T", ExamFamily.Alone, ExamShape.Tee,
            "With nothing else at the junction a car turns left into the stem without stopping.", Movements,
            [Drives(East, South)], [Free(0)]),
        Card("Straight along the top of an empty T", ExamFamily.Alone, ExamShape.Tee,
            "With nothing at the stem a car goes straight past it without stopping.", Movements,
            [Drives(West, East)], [Free(0)]),
        Card("Round an empty roundabout to the first exit", ExamFamily.Alone, ExamShape.Roundabout,
            "A car enters an empty roundabout and leaves at the first exit without stopping.", Ring,
            [Drives(South, East)], [Free(0)]),
        Card("Round an empty roundabout to the second exit", ExamFamily.Alone, ExamShape.Roundabout,
            "A car enters an empty roundabout and leaves at the exit opposite without stopping.", Ring,
            [Drives(South, North)], [Free(0)]),
        Card("Round an empty roundabout to the third exit", ExamFamily.Alone, ExamShape.Roundabout,
            "A car enters an empty roundabout and goes three quarters round without stopping.", Ring,
            [Drives(South, West)], [Free(0)]),
        Card("Out of a dead end", ExamFamily.Alone, ExamShape.DeadEnd,
            "A car facing out of a dead end drives down the spur without stopping.", "TER-5a",
            [Leaves(South, ShortOfTheHeadM, DownTheSpurM)], [Free(0)]),

        // Cars whose ways share no ground: passing each other, standing beside each other, turning on
        // corners of their own. None of them may be held at all.
        Card("Two straights pass each other through a crossroads", ExamFamily.Apart, ExamShape.Crossroads,
            "Two cars going straight on from opposite arms keep to their own lanes, and neither waits.", Apart,
            [Drives(South, North), Drives(North, South, BackM, WellPastM)], [Free(0), Free(1)]),
        Card("Two cars pass each other on a road", ExamFamily.Apart, ExamShape.Crossroads,
            "A car coming up a road and one going down it pass each other in their own lanes, and neither slows to a stop.",
            Apart, [Drives(South, North), Leaves(South, BlockingM, WellPastM)], [Free(0), Free(1)]),
        Card("Passing a car standing in the oncoming lane", ExamFamily.Apart, ExamShape.Crossroads,
            "A car standing in its own lane is on that lane alone, and a car driving past it the other way is not held.",
            Standing, [Stands(North, AheadM), Drives(South, North)], [Free(1)]),
        Card("Passing a line of cars standing in the oncoming lane", ExamFamily.Apart, ExamShape.Crossroads,
            "A queue standing in its own lane holds nothing of the lane beside it.", Standing,
            [Stands(North, AheadM), Stands(North, BehindM), Drives(South, North, BackM, WellPastM)], [Free(2)]),
        Card("Two cars pass each other along the top of a T", ExamFamily.Apart, ExamShape.Tee,
            "Two cars on the through road going opposite ways keep to their own lanes, and neither waits.", Apart,
            [Drives(West, East), Drives(East, West, BackM, WellPastM)], [Free(0), Free(1)]),
        Card("Two right turns from opposite arms", ExamFamily.Apart, ExamShape.Crossroads,
            "Two right turns from opposite arms each keep to their own corner, and neither waits.", Apart,
            [Drives(South, East), Drives(North, West)], [Free(0), Free(1)]),
        Card("Straight past an oncoming right turn", ExamFamily.Apart, ExamShape.Crossroads,
            "A car going straight on and an oncoming car turning right share no ground, and neither waits.", Apart,
            [Drives(South, North), Drives(North, West)], [Free(0), Free(1)]),
        Card("Four right turns at once", ExamFamily.Apart, ExamShape.Crossroads,
            "Four cars turning right from every arm each keep to their own corner, and none waits.", Apart,
            [Drives(South, East), Drives(East, North), Drives(North, West), Drives(West, South)],
            [Free(0), Free(1), Free(2), Free(3)]),
        Card("Right turns from neighbouring arms", ExamFamily.Apart, ExamShape.Crossroads,
            "A right turn and the right turn from the arm on its left share no ground, and neither waits.", Apart,
            [Drives(South, East), Drives(West, South)], [Free(0), Free(1)]),
        Card("A straight beside a right turn from its left", ExamFamily.Apart, ExamShape.Crossroads,
            "A car going straight on and a car on its left turning right onto its arm pass each other, and neither waits.",
            Apart, [Drives(South, North), Drives(West, South)], [Free(0), Free(1)]),
        Card("Right out of the stem past through traffic from the right", ExamFamily.Apart, ExamShape.Tee,
            "A right turn out of the stem and a car on the through road from the right share no ground, and neither waits.",
            Apart, [Drives(South, East), Drives(East, West)], [Free(0), Free(1)]),
        Card("Right into the stem and left out of it", ExamFamily.Apart, ExamShape.Tee,
            "A right turn into the stem keeps to its own corner and crosses nothing of a left turn out of it, so neither waits.",
            Apart, [Drives(South, West), Drives(West, South)], [Free(0), Free(1)]),
        Card("Two cars pass each other round a roundabout", ExamFamily.Apart, ExamShape.Roundabout,
            "Two cars going round to the exits opposite from opposite arms use opposite halves of the ring, and neither waits.",
            Apart, [Drives(South, North), Drives(North, South, BackM, WellPastM)], [Free(0), Free(1)]),
        Card("Two cars into a roundabout from opposite arms", ExamFamily.Apart, ExamShape.Roundabout,
            "Two cars entering from opposite arms and leaving at the first exit share no ground, and neither waits.", Apart,
            [Drives(South, East), Drives(North, West)], [Free(0), Free(1)]),
        Card("Two straights pass each other on a green", ExamFamily.Apart, ExamShape.Crossroads,
            "On one green, two cars going straight on from opposite arms both go without stopping.", Apart,
            [OnGreen(South, North), OnGreen(North, South, BackM, WellPastM)], [Free(0), Free(1), NoRed(0), NoRed(1)])
            with { Lit = true },
        Card("Two cars pass each other along a lit T on green", ExamFamily.Apart, ExamShape.Tee,
            "On the through road's green, two cars going opposite ways both go without stopping.", Apart,
            [OnGreen(West, East), OnGreen(East, West, BackM, WellPastM)], [Free(0), Free(1), NoRed(0), NoRed(1)])
            with { Lit = true },

        // Cars whose ways cross: the right of way decides who waits, and the one it favours is never held.
        Card("A left turn gives way to the oncoming straight", ExamFamily.Across, ExamShape.Crossroads,
            "A turn across the oncoming stream is the weakest movement, and the oncoming straight is never held by it.",
            RightOfWay, [Drives(South, West, NearerM), Drives(North, South)], [Yields(0, 1)]),
        Card("A left turn gives way to the oncoming right turn", ExamFamily.Across, ExamShape.Crossroads,
            "A turn across the oncoming stream gives way to the oncoming near-side turn into the same road.", RightOfWay,
            [Drives(South, West, NearerM), Drives(North, West, BackM, WellPastM)], [Yields(0, 1)]),
        Card("A left turn gives way to two oncoming cars", ExamFamily.Across, ExamShape.Crossroads,
            "A turn across the oncoming stream gives way to every oncoming car, not only the first.", RightOfWay,
            [Drives(South, West, NearerM), Drives(North, South, BackM, WellPastM), Drives(North, South, BehindM)],
            [Yields(0, 1), Yields(0, 2)]),
        Card("A left turn goes first when the oncoming car is far off", ExamFamily.Across, ExamShape.Crossroads,
            "With the oncoming car still standing far off the left turn is made first, and the oncoming car is not held.",
            RightOfWay, [Drives(South, West, CloseM), DrivesAfter(North, South, FarM, StandsFarOffS)], [Before(0, 1), Free(1)]),
        Card("Two opposing left turns both get through", ExamFamily.Across, ExamShape.Crossroads,
            "Two left turns from opposite arms share ground in the box; they settle it between them and nobody touches.",
            RightOfWay, [Drives(South, West), Drives(North, East)], []),
        Card("Two straights across each other both get through", ExamFamily.Across, ExamShape.Crossroads,
            "Two straights across each other are the same rung, and whoever gets there first goes; nobody touches.",
            RightOfWay, [Drives(South, North, NearerM), Drives(East, West)], []),
        Card("Two straights across each other, counted the other way round", ExamFamily.Across, ExamShape.Crossroads,
            "Two straights across each other are settled the same whichever of them the town counted first.", RightOfWay,
            [Drives(East, West), Drives(South, North, NearerM)], []),
        Card("A right turn gives way to a straight into the same road", ExamFamily.Across, ExamShape.Crossroads,
            "Straight through is stronger than the near-side turn, so a right turn never holds the straight it merges with.",
            RightOfWay, [Drives(South, East), Drives(West, East, NearerM, WellPastM)], [Yields(0, 1)]),
        Card("A left turn from the right gives way to the straight it crosses", ExamFamily.Across, ExamShape.Crossroads,
            "A turn across the oncoming stream never holds a straight it crosses, whichever side that comes from.",
            RightOfWay, [Drives(South, North, NearerM), Drives(East, South)], [Yields(1, 0)]),
        Card("A left turn gives way to a straight from its right", ExamFamily.Across, ExamShape.Crossroads,
            "A turn across the oncoming stream never holds a straight from its right.", RightOfWay,
            [Drives(South, West, NearerM), Drives(East, West, BackM, WellPastM)], [Yields(0, 1)]),
        Card("A left turn gives way to a straight from its left", ExamFamily.Across, ExamShape.Crossroads,
            "A turn across the oncoming stream never holds a straight from its left.", RightOfWay,
            [Drives(South, West), Drives(West, East, NearerM)], [Yields(0, 1)]),
        Card("The nearer of two straights goes first", ExamFamily.Across, ExamShape.Crossroads,
            "Of two straights across each other the one with less road to cover goes first, and is never held.",
            RightOfWay, [Drives(South, North, AheadM), Drives(East, West)], [Before(0, 1), Free(0)]),
        Card("Three straights at once all get through", ExamFamily.Across, ExamShape.Crossroads,
            "Three straights arriving together are settled between them, and nobody touches.", RightOfWay,
            [Drives(South, North), Drives(East, West), Drives(West, East)], []),
        Card("Three cars with a left turn among them all get through", ExamFamily.Across, ExamShape.Crossroads,
            "Two straights across each other and a left turn across one of them are settled, and nobody touches.",
            RightOfWay, [Drives(South, West), Drives(North, South), Drives(East, West, BackM, WellPastM)], []),
        Card("Three cars each crossing the next all get through", ExamFamily.Across, ExamShape.Crossroads,
            "Three cars each crossing the next are settled without anybody touching or staying.", RightOfWay,
            [Drives(South, West), Drives(West, East), Drives(North, South)], []),
        Card("Four straights at once all get through", ExamFamily.Across, ExamShape.Crossroads,
            "A car on every arm going straight on: they are settled between them, and nobody touches.", RightOfWay,
            [Drives(South, North), Drives(East, West), Drives(North, South), Drives(West, East)], []),
        Card("Four left turns at once all get through", ExamFamily.Across, ExamShape.Crossroads,
            "Four cars turning left together are settled without anybody touching.", RightOfWay,
            [Drives(South, West), Drives(West, North), Drives(North, East), Drives(East, South)], []),
        Card("A car well ahead crosses before one from its right arrives", ExamFamily.Across, ExamShape.Crossroads,
            "A car over the junction long before the one across it arrives goes, and that one is not held.", RightOfWay,
            [Drives(West, East, CloseM), Drives(South, North, FarM)], [Before(0, 1), Free(1)]),
        Card("Left out of the stem gives way to through traffic from the right", ExamFamily.Across, ExamShape.Tee,
            "A left turn out of the stem never holds a car on the through road from its right.", RightOfWay,
            [Drives(South, West, NearerM), Drives(East, West, BackM, WellPastM)], [Yields(0, 1)]),
        Card("Left out of the stem gives way to through traffic from the left", ExamFamily.Across, ExamShape.Tee,
            "A left turn out of the stem never holds a car on the through road from its left.", RightOfWay,
            [Drives(South, West), Drives(West, East, NearerM)], [Yields(0, 1)]),
        Card("Right out of the stem gives way to through traffic into the same road", ExamFamily.Across, ExamShape.Tee,
            "A right turn out of the stem never holds the straight on the through road it merges with.", RightOfWay,
            [Drives(South, East), Drives(West, East, NearerM, WellPastM)], [Yields(0, 1)]),
        Card("Left into the stem gives way to the oncoming straight", ExamFamily.Across, ExamShape.Tee,
            "A left turn into the stem never holds the oncoming straight on the through road.", RightOfWay,
            [Drives(East, South, NearerM), Drives(West, East)], [Yields(0, 1)]),
        Card("Left out of the stem and left into it both get through", ExamFamily.Across, ExamShape.Tee,
            "Two turns across, one into the stem and one out of it, are the same rung and settled; nobody touches.",
            RightOfWay, [Drives(South, West, NearerM), Drives(East, South)], []),
        Card("Left into the stem gives way to the oncoming right into it", ExamFamily.Across, ExamShape.Tee,
            "A left turn into the stem never holds the oncoming right turn into it.", RightOfWay,
            [Drives(East, South, NearerM), Drives(West, South, BackM, WellPastM)], [Yields(0, 1)]),
        Card("Left out of the stem gives way to two cars from the right", ExamFamily.Across, ExamShape.Tee,
            "A left turn out of the stem gives way to every car from its right, not only the first.", RightOfWay,
            [Drives(South, West, NearerM), Drives(East, West, BackM, FurthestM), Drives(East, West, BehindM, WellPastM)],
            [Yields(0, 1), Yields(0, 2)]),
        Card("Three cars at a T all get through", ExamFamily.Across, ExamShape.Tee,
            "A left turn out of the stem between cars on the through road both ways is settled, and nobody touches.",
            RightOfWay, [Drives(South, West), Drives(East, West, BackM, WellPastM), Drives(West, East)], []),
        Card("Entering a roundabout gives way to a car already on it", ExamFamily.Across, ExamShape.Roundabout,
            "Going round is straight on and coming in off an arm is a turn, so a car entering never holds one already on the ring.",
            $"{Ring}, {RightOfWay}", [DrivesAfter(South, North, BackM, OnTheRingFirstS), Drives(West, East, CloseM)],
            [Yields(0, 1)]),

        // Queues.
        Card("Following the car in front through the junction", ExamFamily.Queues, ExamShape.Crossroads,
            "A car follows the one in front through the junction, in order and keeping its distance.", Queue,
            [Drives(South, North, AheadM, WellPastM), Drives(South, North, BehindM)], [Before(0, 1)]),
        Card("A queue of three moves off in order", ExamFamily.Queues, ExamShape.Crossroads,
            "Cars queued in one lane go through in the order they stand.", Queue,
            [Drives(South, North, AheadM, FurthestM), Drives(South, North, BehindM, WellPastM), Drives(South, North, ThirdM)],
            [Before(0, 1), Before(1, 2)]),
        Card("Queueing behind a car waiting to turn left", ExamFamily.Queues, ExamShape.Crossroads,
            "A car behind one waiting to turn left waits behind it; the left turn never holds the oncoming car.", Queue,
            [Drives(South, West, AheadM), Drives(South, North, BehindM), Drives(North, South)],
            [Yields(0, 2), Before(0, 1)]),
        Card("A queue past the junction holds the car behind it until it moves", ExamFamily.Queues, ExamShape.Crossroads,
            "A car behind a queue standing past the junction follows it out once it moves, and nobody touches.", Queue,
            [Blocks(North, BlockingM), Drives(South, North)], []),
        Card("Two cars queued in the stem", ExamFamily.Queues, ExamShape.Tee,
            "The second car in the stem goes after the first.", Queue,
            [Drives(South, East, AheadM, WellPastM), Drives(South, East, BehindM)], [Before(0, 1)]),
        Card("Waiting behind a car turning left into the stem", ExamFamily.Queues, ExamShape.Tee,
            "A car behind one waiting to turn left into the stem waits behind it; the left turn never holds the oncoming car.",
            Queue, [Drives(East, South, AheadM), Drives(East, West, BehindM), Drives(West, East)], [Yields(0, 2), Before(0, 1)]),
        Card("A queue past a T holds the car behind it until it moves", ExamFamily.Queues, ExamShape.Tee,
            "A car on the through road behind a queue standing past the junction follows it out once it moves.", Queue,
            [Blocks(East, BlockingM), Drives(West, East)], []),
        Card("A queue past a roundabout's exit holds the car behind it", ExamFamily.Queues, ExamShape.Roundabout,
            "A car bound for an exit with a queue standing past it follows the queue out once it moves.", Queue,
            [Blocks(East, BlockingM), Drives(South, East)], []),

        // The box a light governs.
        Card("Stopping at a red light", ExamFamily.Signals, ExamShape.Crossroads,
            "A red holds a car behind its bar until green.", Light,
            [OnRed(South, North)], [Waits(0)]) with { Lit = true },
        Card("Going on a green light", ExamFamily.Signals, ExamShape.Crossroads,
            "A green with the junction clear lets a car through without stopping.", Light,
            [OnGreen(South, North)], [Free(0), NoRed(0)]) with { Lit = true },
        Card("The cross traffic waits out its red", ExamFamily.Signals, ExamShape.Crossroads,
            "While one axis has green the other has red, and its traffic waits at the bar.", Light,
            [OnGreen(West, East), OnRed(South, North)], [Free(0), Waits(1)]) with { Lit = true },
        Card("Left turn on green gives way to the oncoming straight", ExamFamily.Signals, ExamShape.Crossroads,
            "On a shared green the turn across never holds the oncoming straight, and never goes on red.",
            $"{Light}, {RightOfWay}", [OnGreen(South, West, NearerM), OnGreen(North, South)], [Yields(0, 1), NoRed(0)])
            with { Lit = true },
        Card("Left turn on green gives way to the oncoming right turn", ExamFamily.Signals, ExamShape.Crossroads,
            "On a shared green the turn across never holds the oncoming right turn into the same road.",
            $"{Light}, {RightOfWay}", [OnGreen(South, West, NearerM), OnGreen(North, West, BackM, WellPastM)],
            [Yields(0, 1)]) with { Lit = true },
        Card("Straight on green is not held by an oncoming left turn", ExamFamily.Signals, ExamShape.Crossroads,
            "On a shared green the straight goes without stopping and the oncoming left turn waits for it.",
            $"{Light}, {RightOfWay}", [OnGreen(South, North), OnGreen(North, East, NearerM)], [Free(0), Yields(1, 0)])
            with { Lit = true },
        Card("Right turn on green gives way to somebody crossing", ExamFamily.Signals, ExamShape.Crossroads,
            "Turning on a green, a car gives way to somebody crossing the road it turns into.", $"{Light}, {Paint}",
            [OnGreen(South, East)], ForThePedestrian(0), Pauses(East, Right, 0)) with { Lit = true },
        Card("Left turn on green gives way to somebody crossing", ExamFamily.Signals, ExamShape.Crossroads,
            "Turning on a green, a car gives way to somebody crossing the road it turns into.", $"{Light}, {Paint}",
            [OnGreen(South, West)], ForThePedestrian(0), Pauses(West, Right, 0)) with { Lit = true },
        Card("A red holds a right turn", ExamFamily.Signals, ExamShape.Crossroads,
            "A red holds a car turning right as it holds one going straight on.", Light,
            [OnRed(South, East)], [Waits(0)]) with { Lit = true },
        Card("A red holds a left turn", ExamFamily.Signals, ExamShape.Crossroads,
            "A red holds a car turning left.", Light,
            [OnRed(South, West)], [Waits(0)]) with { Lit = true },
        Card("A queue at a red goes on green in order", ExamFamily.Signals, ExamShape.Crossroads,
            "Two cars stop at a red, one behind the other, and go on green in the order they stand.", $"{Light}, {Queue}",
            [OnRed(South, North, AheadM), OnRed(South, North, BehindM)], [Waits(0), Waits(1), Before(0, 1)])
            with { Lit = true },
        Card("A queue past a lit junction never sends the car behind it on red", ExamFamily.Signals, ExamShape.Crossroads,
            "A car behind a queue standing past a lit junction follows it out and never goes past its bar on red.",
            $"{Light}, {Queue}", [Blocks(North, BlockingM, ExamStart.OnGreen, BlocksTheGreenForS), OnGreen(South, North)],
            [NoRed(1)]) with { Lit = true },
        Card("Two left turns on the same green both get through", ExamFamily.Signals, ExamShape.Crossroads,
            "Two left turns from opposite arms on one green are settled between them, and neither goes on red.",
            $"{Light}, {RightOfWay}", [OnGreen(South, West), OnGreen(North, East)], [NoRed(0), NoRed(1)])
            with { Lit = true },
        Card("The stem waits out its red", ExamFamily.Signals, ExamShape.Tee,
            "A red holds the stem while the through road has green.", Light,
            [OnRed(South, East)], [Waits(0)]) with { Lit = true },
        Card("Through traffic stops at a red at a T", ExamFamily.Signals, ExamShape.Tee,
            "A red holds the through road while the stem has green.", Light,
            [OnRed(West, East)], [Waits(0)]) with { Lit = true },
        Card("Left into the stem on green gives way to the oncoming", ExamFamily.Signals, ExamShape.Tee,
            "On the through road's green a left turn into the stem never holds the oncoming straight.",
            $"{Light}, {RightOfWay}", [OnGreen(East, South, NearerM), OnGreen(West, East)], [Yields(0, 1)])
            with { Lit = true },
        Card("Left out of the stem on its own green", ExamFamily.Signals, ExamShape.Tee,
            "On its own green the stem turns left without stopping, the through road being held at red.", Light,
            [OnGreen(South, West), OnRed(East, West, BackM)], [Free(0), Waits(1)]) with { Lit = true },

        // Somebody on foot: on a zebra, or walking the pavement past one.
        Card("Stopping for somebody stepping onto the zebra", ExamFamily.Walkers, ExamShape.Crossroads,
            "A car gives way to somebody walking the zebra in front of it.", Paint,
            [Drives(South, North, BehindM)], ForThePedestrian(0), Pauses(South, Left, 0)),
        Card("Stopping for somebody on the zebra past the junction", ExamFamily.Walkers, ExamShape.Crossroads,
            "A car gives way to somebody on the zebra on the far side of the junction too.", Paint,
            [Drives(South, North)], ForThePedestrian(0), Pauses(North, Right, 0)),
        Card("Right turn gives way on the zebra it turns over", ExamFamily.Walkers, ExamShape.Crossroads,
            "A car turning right gives way to somebody crossing the road it turns into.", Paint,
            [Drives(South, East)], ForThePedestrian(0), Pauses(East, Right, 0)),
        Card("Left turn gives way on the zebra it turns over", ExamFamily.Walkers, ExamShape.Crossroads,
            "A car turning left gives way to somebody crossing the road it turns into.", Paint,
            [Drives(South, West)], ForThePedestrian(0), Pauses(West, Right, 0)),
        Card("The car behind waits for the zebra too", ExamFamily.Walkers, ExamShape.Crossroads,
            "A car behind one stopped at a zebra stops too, and goes after it once the walker is over.", Paint,
            [Drives(South, North, BehindM, WellPastM), Drives(South, North, ThirdM)],
            [.. ForThePedestrian(0), .. ForThePedestrian(1), Before(0, 1)], Pauses(South, Left, 0)),
        Card("Leaving a roundabout gives way on the zebra", ExamFamily.Walkers, ExamShape.Roundabout,
            "A car leaving a roundabout gives way to somebody crossing the road it leaves by.", Paint,
            [Drives(South, East)], ForThePedestrian(0), Pauses(East, Right, 0)),
        Card("Somebody steps onto the zebra past the junction early", ExamFamily.Walkers, ExamShape.Crossroads,
            "A car still short of the junction when somebody steps onto the zebra past it gives way to them.", Paint,
            [Drives(South, North)], ForThePedestrian(0), Pauses(North, Right, 0, StepOutPastTheBoxM)),
        Card("Left turn waits for the oncoming car and then the zebra", ExamFamily.Walkers, ExamShape.Crossroads,
            "A car turning left never holds the oncoming car, then gives way to somebody on the zebra it turns over.",
            $"{Paint}, {RightOfWay}", [Drives(South, West, NearerM), Drives(North, South)],
            [Yields(0, 1), .. ForThePedestrian(0)], Pauses(West, Right, 0)),
        Card("Turning into the stem gives way on its zebra", ExamFamily.Walkers, ExamShape.Tee,
            "A car turning into the stem gives way to somebody crossing it.", Paint,
            [Drives(West, South)], ForThePedestrian(0), Pauses(South, Right, 0)),
        Card("Coming out of the stem gives way on its zebra", ExamFamily.Walkers, ExamShape.Tee,
            "A car coming out of the stem gives way to somebody on the zebra across it.", Paint,
            [Drives(South, East, BehindM)], ForThePedestrian(0), Pauses(South, Left, 0)),
        Card("Right out of the stem gives way on the zebra it turns over", ExamFamily.Walkers, ExamShape.Tee,
            "A car turning right out of the stem gives way to somebody crossing the road it turns into.", Paint,
            [Drives(South, East)], ForThePedestrian(0), Pauses(East, Right, 0)),
        Card("Left out of the stem gives way on the zebra it turns over", ExamFamily.Walkers, ExamShape.Tee,
            "A car turning left out of the stem gives way to somebody crossing the road it turns into.", Paint,
            [Drives(South, West)], ForThePedestrian(0), Pauses(West, Right, 0)),
        Card("Straight past somebody walking round the near corner", ExamFamily.Walkers, ExamShape.Crossroads,
            "Somebody walking the pavement past the end of a zebra crosses nothing, and a car driving by is not held.",
            $"{Standing}, {Apart}", [Drives(South, North)], [Free(0)], Strolls(South, East, 0)),
        Card("A right turn past somebody walking round its corner", ExamFamily.Walkers, ExamShape.Crossroads,
            "Somebody walking round the corner a car turns round crosses nothing of it, and the car is not held.",
            $"{Standing}, {Apart}", [Drives(South, East)], [Free(0)], Strolls(South, East, 0)),
        Card("Straight past somebody walking round the far corner", ExamFamily.Walkers, ExamShape.Crossroads,
            "Somebody walking the pavement beside the lane a car leaves by crosses nothing, and the car is not held.",
            $"{Standing}, {Apart}", [Drives(South, North)], [Free(0)], Strolls(North, East, 0)),

        // A car on a call.
        Card("Cross traffic gives way to a car on a call", ExamFamily.Emergency, ExamShape.Crossroads,
            "A call is stronger than every movement, and a car crossing its way never holds it.", Call,
            [Siren(West, East), Drives(South, North, NearerM)], [Yields(1, 0)]),
        Card("The oncoming car gives way to a car on a call turning left", ExamFamily.Emergency, ExamShape.Crossroads,
            "A car on a call turning across the oncoming stream is never held by it.", Call,
            [Siren(South, West), Drives(North, South, NearerM)], [Yields(1, 0)]),
        Card("A straight gives way to a car on a call from its left", ExamFamily.Emergency, ExamShape.Crossroads,
            "A straight across the way of a car on a call never holds it, whichever side it comes from.", Call,
            [Siren(South, North), Drives(East, West, NearerM)], [Yields(1, 0)]),
        Card("A car on a call through a red light", ExamFamily.Emergency, ExamShape.Crossroads,
            "A car on a call goes through a red, and the traffic on green never holds it.", $"{Call}, {Light}",
            [Siren(South, North, BackM, ExamStart.OnRed), OnGreen(West, East, NearerM)], [Yields(1, 0)]) with { Lit = true },
        Card("Two cars hold for a car on a call", ExamFamily.Emergency, ExamShape.Crossroads,
            "Both cars across the way of a car on a call give way to it, whatever they are to each other.", Call,
            [Siren(West, East), Drives(South, North, NearerM), Drives(North, South, NearerM, WellPastM)],
            [Yields(1, 0), Yields(2, 0)]),
        Card("Through traffic gives way to a car on a call out of the stem", ExamFamily.Emergency, ExamShape.Tee,
            "A car on the through road never holds a car on a call coming out of the stem.", Call,
            [Siren(South, West, BackM, ExamStart.AtOnce, WellPastM), Drives(East, West, NearerM)], [Yields(1, 0)]),
        Card("The oncoming car gives way to a car on a call turning into the stem", ExamFamily.Emergency, ExamShape.Tee,
            "A car on a call turning left into the stem is never held by the oncoming car.", Call,
            [Siren(East, South), Drives(West, East, NearerM)], [Yields(1, 0)]),

        // One-way streets.
        Card("Left turn into a one-way street gives way to the oncoming", ExamFamily.OneWay, ExamShape.Crossroads,
            "A left turn into a one-way street never holds the oncoming straight.", $"{OneWay}, {RightOfWay}",
            [Drives(South, West, NearerM), Drives(North, South)], [Yields(0, 1)]) with { OneWayOut = [West] },
        Card("Out of a one-way street, giving way to a straight from the right", ExamFamily.OneWay, ExamShape.Crossroads,
            "A left turn out of a one-way street never holds a straight from its right.", $"{OneWay}, {RightOfWay}",
            [Drives(South, West, NearerM), Drives(East, West, BackM, WellPastM)], [Yields(0, 1)]) with
        {
            OneWayIn = [South],
        },
        Card("Out of the stem onto a one-way road", ExamFamily.OneWay, ExamShape.Tee,
            "Out of the stem onto a one-way road a car turns the way the road runs, without stopping.", OneWay,
            [Drives(South, East)], [Free(0)]) with { OneWayIn = [West], OneWayOut = [East] },
        Card("Into the stem off a one-way road", ExamFamily.OneWay, ExamShape.Tee,
            "A car on a one-way road turns into the stem with nothing oncoming, without stopping.", OneWay,
            [Drives(West, South)], [Free(0)]) with { OneWayIn = [West], OneWayOut = [East] },

        // The same T questions asked on another edge, turned another way: a fault of handedness shows in one
        // orientation and not in the others.
        Card("Left out of the stem gives way to the right, another way round", ExamFamily.Across, ExamShape.Tee,
            "A left turn out of the stem never holds a car on the through road from its right.", RightOfWay,
            [Drives(South, West, NearerM), Drives(East, West, BackM, WellPastM)], [Yields(0, 1)]),
        Card("Left into the stem gives way to the oncoming straight, another way round", ExamFamily.Across, ExamShape.Tee,
            "A left turn into the stem never holds the oncoming straight on the through road.", RightOfWay,
            [Drives(East, South, NearerM), Drives(West, East)], [Yields(0, 1)]),

        // Getting past what stands in the lane: the one card whose car goes round another rather than waiting.
        Card("Getting past a car stood in its lane", ExamFamily.Queues, ExamShape.Crossroads,
            "A car behind one stood in its lane goes round it over the free lane beside, and on through the junction.",
            Pass, [Stands(South, BackM), Drives(South, North, FarM)], []),
        Card("A car on a call gets past a queue at a red", ExamFamily.Emergency, ExamShape.Crossroads,
            "A car on a call behind a queue waiting at a red goes round the whole of it over the free lane beside, the "
            + "paint and the box, and is through before its head.", $"{Call}, {Pass}, {Light}",
            [
                Queued(0), Queued(1), Queued(2), Queued(3), Queued(4),
                Siren(South, North, QueueCallBackM, ExamStart.OnRed, FurthestM),
            ],
            [Before(5, 0)]) with { Lit = true },
    ];

    /// <summary>
    /// The <paramref name="place"/>th car from the head of a queue waiting at a red on the south arm: stood
    /// <see cref="QueuedApartM"/> further back than the one in front and sent that much less far on, so none is
    /// stood on its place in the lane of one still to come.
    /// </summary>
    static ExamDriver Queued(int place) =>
        OnRed(South, North, AheadM + (place * QueuedApartM), WellPastM - (place * QueuedApartM));

    /// <summary>How far apart two cars of a queue are stood, and their places are: a car and its stand-off, and over.</summary>
    const float QueuedApartM = 8f;

    /// <summary>
    /// How far back a car on a call stands behind a queue it gets past: behind the last of it, and far enough
    /// that the queue has closed up at the bar before it comes up.
    /// </summary>
    const float QueueCallBackM = 95f;

    /// <summary>A car gets past a body going nowhere in its lane, over the lane beside where that is free.</summary>
    const string Pass = "CAR-46, TER-4c.6";

    static ExamCard Card(
        string name, ExamFamily family, ExamShape shape, string expects, string rules, ExamDriver[] drivers,
        ExamClaim[] claims, ExamWalker[]? walkers = null) =>
        new(name, family, shape, expects, rules) { Drivers = drivers, Claims = claims, Walkers = walkers ?? [] };

    static ExamDriver Drives(ExamArm from, ExamArm to, float backM = BackM, float runOnM = RunOnM) =>
        new(from, to, backM, runOnM, ExamStart.AtOnce, 0f, Emergency: false, Parked: false);

    /// <summary>A car that stands where it starts for a while before it is sent.</summary>
    static ExamDriver DrivesAfter(ExamArm from, ExamArm to, float backM, float afterS) =>
        new(from, to, backM, RunOnM, ExamStart.AtOnce, afterS, Emergency: false, Parked: false);

    /// <summary>
    /// <b>How long a car far off stands before it is sent</b>: a car moving plans a stop from the speed it is
    /// pulling up to, which from a standstill reaches the box well before the car does — so a car that set off
    /// at once would never be far off to anything across it. Standing, it plans the room to pull away and no more
    /// (TER-4c.1).
    /// </summary>
    const float StandsFarOffS = 3f;

    /// <summary>
    /// <b>How long a car entering a roundabout stands before it is sent</b>: long enough that the car it gives
    /// way to is past its own entry and going round. A car still coming in off its arm holds the ring at its
    /// entry's rung (TER-5g.1), and between two entries the ring is nobody's.
    /// </summary>
    const float OnTheRingFirstS = 4f;

    static ExamDriver OnRed(ExamArm from, ExamArm to, float backM = BackM, float runOnM = RunOnM) =>
        new(from, to, backM, runOnM, ExamStart.OnRed, 0f, Emergency: false, Parked: false);

    static ExamDriver OnGreen(ExamArm from, ExamArm to, float backM = BackM, float runOnM = RunOnM) =>
        new(from, to, backM, runOnM, ExamStart.OnGreen, 0f, Emergency: false, Parked: false);

    static ExamDriver Siren(
        ExamArm from, ExamArm to, float backM = BackM, ExamStart start = ExamStart.AtOnce, float runOnM = RunOnM) =>
        new(from, to, backM, runOnM, start, 0f, Emergency: true, Parked: false);

    /// <summary>A car stood in its own lane, a distance short of the box, and held there for the whole card.</summary>
    static ExamDriver Stands(ExamArm on, float backM) =>
        new(on, on, backM, 0f, ExamStart.AtOnce, 0f, Emergency: false, Parked: true);

    /// <summary>A car stood in the lane leaving the box on one arm, a distance out, and sent on down it at once.</summary>
    static ExamDriver Leaves(ExamArm on, float outM, float runOnM) =>
        new(on, on, outM, runOnM, ExamStart.AtOnce, 0f, Emergency: false, Parked: false, Outbound: true);

    /// <summary>
    /// A car stood in the lane leaving the box on one arm, a distance out — a queue on the far side — which
    /// drives on down that lane once it has stood there a while.
    /// </summary>
    /// <remarks>
    /// <b>At a light, what it stands through is a stretch of the green</b> (<see cref="ExamStart.OnGreen"/>):
    /// the lane it stands in is the far side of the same axis as the car it blocks, so it drives on a while
    /// after that axis turns green — and a car that went in on the green would be standing in the box.
    /// </remarks>
    static ExamDriver Blocks(ExamArm on, float outM, ExamStart start = ExamStart.AtOnce, float forS = BlocksForS) =>
        new(on, on, outM, WellPastM, start, forS, Emergency: false, Parked: false, Outbound: true);

    /// <summary>
    /// How long into its green a car blocking the far side of a lit junction stands there — <b>most of the
    /// green</b>, so the car behind reaches the junction while the far side is still blocked.
    /// </summary>
    const float BlocksTheGreenForS = 6f;

    /// <summary>
    /// How near a car's nose is to the middle of the paint when somebody steps out in front of it: <b>outside
    /// the distance it needs to stop in</b> at a town speed, so what is asked is whether it gives way and never
    /// whether it could have.
    /// </summary>
    const float StepOutM = 20f;

    /// <summary>
    /// <b>Somebody stepping out as a car comes within <see cref="StepOutM"/></b>, into the middle of that car's
    /// lane on the paint, standing there a moment and going on over. <b>A crossing in one is not enough</b>: a
    /// body crosses a carriageway in about a second at the town's pace, so one that starts on the car's own
    /// side is out of its lane before the car could reach it, and a car that gave nobody way would still never
    /// be on the paint with them. Standing in the lane is what makes the car stop.
    /// </summary>
    /// <remarks>
    /// <b>Not a group, and not somebody pacing back and forth</b>: a zebra is walked along one line, so a
    /// group's bodies walk into one another; and a body turned round at the far kerb steps back out as a car is
    /// arriving too near to stop, which is a card about the pedestrian and not about the car.
    /// </remarks>
    static ExamWalker[] Pauses(ExamArm crossing, ExamSide from, int driver, float withinM = StepOutM) =>
        [new(crossing, from, 0f, driver, withinM, PausesForS)];

    /// <summary>
    /// How near a car is to a zebra on the far side of the junction when somebody steps out onto it early: <b>its
    /// stopping distance short of the box</b> and not of the paint, so it is still short of the junction when it
    /// has to give way.
    /// </summary>
    const float StepOutPastTheBoxM = 32f;

    /// <summary>How long they stand in the lane: long enough that a car which did not stop would be on the paint with them.</summary>
    const float PausesForS = 3f;

    /// <summary>
    /// <b>Somebody walking the pavement round a corner</b>, from one arm to the next, setting off as a car
    /// comes within <see cref="StrollWithinM"/> of them — so the car passes them on the stretch past the zebra's
    /// kerb end, which is where the pavement's ribbon comes nearest a lane's.
    /// </summary>
    static ExamWalker[] Strolls(ExamArm from, ExamArm to, int driver) =>
        [new(from, Left, 0f, driver, StrollWithinM, 0f, to, StrollOutM)];

    /// <summary>How far out along each arm a walk round a corner sets off and ends: just past the zebra there.</summary>
    const float StrollOutM = 14f;

    /// <summary>How near the car is to the walker when it sets off: a couple of seconds of driving at a town speed.</summary>
    const float StrollWithinM = 25f;

    /// <summary>A car giving way to the one walker a card stands on its paint.</summary>
    static ExamClaim[] ForThePedestrian(int driver) => [ForWalker(driver, 0)];

    static ExamClaim Yields(int subject, int to) => new(ExamRule.YieldsTo, subject, to);

    static ExamClaim Before(int subject, int other) => new(ExamRule.Before, subject, other);

    static ExamClaim Free(int subject) => new(ExamRule.Unhindered, subject);

    static ExamClaim NoRed(int subject) => new(ExamRule.NeverOnRed, subject);

    static ExamClaim Waits(int subject) => new(ExamRule.Waits, subject);

    static ExamClaim ForWalker(int subject, int walker) => new(ExamRule.ForWalker, subject, walker);
}
