using System.Globalization;
using System.Numerics;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Drive;

/// <summary>What one line of a drive script asks for (DRV-2).</summary>
internal enum DriveVerb
{
    /// <summary>A car by its number in the fleet.</summary>
    SelectCar,

    /// <summary>The unit whose own footprint covers a place — the click itself (CTL-1).</summary>
    SelectAt,

    /// <summary>The car standing nearest a place, clicked where it stands.</summary>
    SelectNearest,

    /// <summary>And the walker standing nearest one, on the same terms.</summary>
    SelectWalker,

    /// <summary>What is parked or driving near a place, as a list to pick a number off.</summary>
    Cars,

    /// <summary>And who is on foot near one, which is the same list for the other roster.</summary>
    People,

    /// <summary>The camera pinned to a place, instead of riding the car.</summary>
    LookAt,

    /// <summary>The camera back on the car under the hand, which is where it starts.</summary>
    LookAtCar,

    /// <summary>How much town a frame spans, in metres.</summary>
    View,

    /// <summary>The <c>--ui</c> words later frames are drawn with.</summary>
    Ui,

    /// <summary>The keys, held down for a while (CTL-5).</summary>
    Drive,

    /// <summary>The town driving itself for a while, with nothing held.</summary>
    Wait,

    /// <summary>The reset: the wheel and manual mode given back (CTL-4).</summary>
    Release,

    /// <summary>A right-click order at a place (CTL-8).</summary>
    Order,

    /// <summary>The unit's own action, worked once (CTL-7).</summary>
    Action,

    /// <summary>A frame of what the run looks like now.</summary>
    Shot,

    /// <summary>The town's own agents let go of or held, which is the player's <c>Pause</c> key.</summary>
    Agents,

    /// <summary>How fast the town runs against real time, which is the player's pace keys.</summary>
    Pace,
}

/// <summary>
/// One line of a drive script: a verb and whatever that verb takes. <see cref="Said"/> and
/// <see cref="Line"/> are what the log quotes, so a reading can be read against the line that asked for
/// it.
/// </summary>
/// <param name="Steered">
/// Whether the line named the wheel at all. <b>It is the difference between asking for a wheel straight
/// ahead and not asking about the wheel</b>, which is what lets a second driver leave it where it was
/// (DRV-8) — the figure alone cannot say, since nought is an angle like any other.
/// </param>
internal readonly record struct DriveStep(
    DriveVerb Verb,
    string Said,
    int Line,
    int Car = -1,
    Vector2 PointM = default,
    float Seconds = 0f,
    HandInput Hand = default,
    float ViewM = 0f,
    string[]? Ui = null,
    string? Name = null,
    bool On = false,
    bool Steered = false);

/// <summary>
/// <b>DRV-2 — the script, read.</b> One line is one step, a <c>#</c> starts a comment and a blank line is
/// nothing; a line this cannot parse is the run's own error and never a step quietly dropped.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every pedal figure is one a key could have produced</b> (DRV-1): the throttle, the brake and the
/// steering are shares between nought and one of what the hand has, and anything outside that is refused
/// rather than clamped — a script asking for two is a script that thinks it can ask for more car than the
/// player has.
/// </para>
/// <para>
/// <b>The brake and the reverse are one pedal</b>, exactly as they are on the keys: <c>brake</c> is the
/// throttle pushed backwards, which slows a car that is moving and reverses one that has stopped.
/// </para>
/// </remarks>
internal static class DriveScript
{
    /// <summary>What a script may say, quoted at whoever wrote a line this cannot read.</summary>
    public const string Verbs =
        "select car N | select at X Y | select nearest X Y | select walker X Y | cars X Y | people X Y | " +
        "look car | look X Y | view METRES | " +
        "ui WORDS | drive SECONDS [throttle=N] [brake=N] [steer=N] [handbrake=on] | coast SECONDS | " +
        "wait SECONDS | release | order X Y | action | shot NAME | agents on|off | pace N";

    public static DriveStep[] Read(string text)
    {
        var steps = Steps(text, 0);
        return steps.Length > 0
            ? steps
            : throw new ArgumentException($"The script says nothing. It may say: {Verbs}.");
    }

    /// <summary>
    /// The steps in a run of text, numbered from a line the caller is counting — <b>which is how a script
    /// somebody is still writing is read</b> (DRV-7): what has arrived since the last read is parsed on its
    /// own and still quotes the line it was written on.
    /// </summary>
    public static DriveStep[] Steps(string text, int fromLine)
    {
        var steps = new List<DriveStep>();
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var said = Bare(lines[i]);
            if (said.Length == 0) continue;

            steps.Add(Step(said, fromLine + i + 1, said.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
        }

        return [.. steps];
    }

    /// <summary>A line with its comment and its whitespace off, which is what is quoted in the log.</summary>
    static string Bare(string line)
    {
        var comment = line.IndexOf('#');
        return (comment >= 0 ? line[..comment] : line).Trim();
    }

    static DriveStep Step(string said, int line, string[] words) => words[0] switch
    {
        "select" => Select(said, line, words),
        "cars" => new DriveStep(DriveVerb.Cars, said, line, PointM: Place(said, line, words, 1)),
        "people" => new DriveStep(DriveVerb.People, said, line, PointM: Place(said, line, words, 1)),
        "look" => words.Length == 2 && words[1] == "car"
            ? new DriveStep(DriveVerb.LookAtCar, said, line)
            : new DriveStep(DriveVerb.LookAt, said, line, PointM: Place(said, line, words, 1)),
        "view" => new DriveStep(DriveVerb.View, said, line, ViewM: Positive(said, line, words, 1)),
        "ui" => new DriveStep(
            DriveVerb.Ui, said, line,
            Ui: Word(said, line, words, 1).Split(',', StringSplitOptions.RemoveEmptyEntries)),
        "drive" => Drive(said, line, words),
        "coast" => new DriveStep(
            DriveVerb.Drive, said, line, Seconds: Positive(said, line, words, 1), Hand: Held(0f, 0f, false)),
        "wait" => new DriveStep(DriveVerb.Wait, said, line, Seconds: Positive(said, line, words, 1)),
        "release" => new DriveStep(DriveVerb.Release, said, line),
        "order" => new DriveStep(DriveVerb.Order, said, line, PointM: Place(said, line, words, 1)),
        "action" => new DriveStep(DriveVerb.Action, said, line),
        "shot" => new DriveStep(DriveVerb.Shot, said, line, Name: Name(said, line, words)),
        "agents" => new DriveStep(
            DriveVerb.Agents, said, line, On: Switched(said, line, Word(said, line, words, 1))),
        "pace" => new DriveStep(DriveVerb.Pace, said, line, Seconds: Positive(said, line, words, 1)),
        _ => throw Bad(said, line, $"{words[0]} is not one of: {Verbs}"),
    };

    static DriveStep Select(string said, int line, string[] words) => words.Length < 2
        ? throw Bad(said, line, TheWaysToSelect)
        : words[1] switch
        {
            "car" => new DriveStep(DriveVerb.SelectCar, said, line, Car: Number(said, line, words, 2)),
            "at" => new DriveStep(DriveVerb.SelectAt, said, line, PointM: Place(said, line, words, 2)),
            "nearest" => new DriveStep(DriveVerb.SelectNearest, said, line, PointM: Place(said, line, words, 2)),
            "walker" => new DriveStep(DriveVerb.SelectWalker, said, line, PointM: Place(said, line, words, 2)),
            _ => throw Bad(said, line, TheWaysToSelect),
        };

    const string TheWaysToSelect = "select takes car N, at X Y, nearest X Y or walker X Y";

    /// <summary>
    /// <c>drive SECONDS</c> and the pedals held for them. <b>The pedals are named rather than positional</b>,
    /// so a line says what is being held and a reader of the log does not have to count arguments.
    /// </summary>
    static DriveStep Drive(string said, int line, string[] words)
    {
        if (words.Length < 2) throw Bad(said, line, "drive takes the seconds to hold the keys for, then what is held");

        var seconds = Above(said, line, words[1]);
        var throttle = 0f;
        var brake = 0f;
        var steer = 0f;
        var steered = false;
        var handbrake = false;

        for (var at = 2; at < words.Length; at++)
        {
            var split = words[at].IndexOf('=');
            if (split <= 0) throw Bad(said, line, $"{words[at]} is not name=value");

            var name = words[at][..split];
            var value = words[at][(split + 1)..];
            switch (name)
            {
                case "throttle":
                    throttle = Share(said, line, name, value);
                    break;
                case "brake":
                    brake = Share(said, line, name, value);
                    break;
                case "steer":
                    steer = Steer(said, line, value);
                    steered = true;
                    break;
                case "handbrake":
                    handbrake = Switched(said, line, value);
                    break;
                default:
                    throw Bad(said, line, $"{name} is not a pedal: throttle, brake, steer or handbrake");
            }
        }

        // One pedal at a time, because the keys are one axis: a line holding both says nothing a foot
        // could have done, and a hand that quietly won would be a script driving a car it cannot read.
        if (throttle > 0f && brake > 0f) throw Bad(said, line, "throttle and brake are one pedal: hold one of them");

        return new DriveStep(
            DriveVerb.Drive, said, line, Seconds: seconds, Hand: Held(throttle - brake, steer, handbrake),
            Steered: steered);
    }

    /// <summary>
    /// The hand as the keys report one (CTL-5b): <b>held even when nothing is pressed</b>, because letting
    /// go coasts rather than handing the unit back. The walk direction is the same pair of axes a walker
    /// under the same hand reads, so one line drives either kind of unit.
    /// </summary>
    static HandInput Held(float throttle, float steer, bool handbrake) =>
        new(true, throttle, steer, handbrake, new Vector2(steer, -throttle));

    static Vector2 Place(string said, int line, string[] words, int at)
    {
        if (words.Length != at + 2) throw Bad(said, line, $"{words[0]} takes a place: X Y in metres");

        return new Vector2(Figure(said, line, words[at]), Figure(said, line, words[at + 1]));
    }

    /// <summary>A verb whose whole argument is one figure above nought — a span to look across, or a while to wait.</summary>
    static float Positive(string said, int line, string[] words, int at)
    {
        if (words.Length != at + 1) throw Bad(said, line, $"{words[0]} takes one figure");

        return Above(said, line, words[at]);
    }

    /// <summary>A figure above nought: a length of time to hold something for, or a span to look across.</summary>
    static float Above(string said, int line, string word)
    {
        var figure = Figure(said, line, word);
        return figure > 0f ? figure : throw Bad(said, line, $"{figure} is not a length of time or a span");
    }

    static int Number(string said, int line, string[] words, int at)
    {
        if (words.Length != at + 1 || !int.TryParse(words[at], CultureInfo.InvariantCulture, out var number) ||
            number < 0)
        {
            throw Bad(said, line, "select car takes a number in the fleet");
        }

        return number;
    }

    static string Word(string said, int line, string[] words, int at)
    {
        if (words.Length != at + 1) throw Bad(said, line, $"{words[0]} takes one word");

        return words[at];
    }

    /// <summary>A frame's name, which is what it is written as — a file name and never a path.</summary>
    static string Name(string said, int line, string[] words)
    {
        var name = Word(said, line, words, 1);
        foreach (var letter in name)
        {
            if (!char.IsAsciiLetterOrDigit(letter) && letter != '-' && letter != '_')
            {
                throw Bad(said, line, $"{name} is not a frame name: letters, digits, - and _");
            }
        }

        return name;
    }

    static float Figure(string said, int line, string word) =>
        float.TryParse(word, CultureInfo.InvariantCulture, out var figure) && float.IsFinite(figure)
            ? figure
            : throw Bad(said, line, $"{word} is not a figure");

    /// <summary>A pedal, as the share of itself a key holds it at. Refused outside its travel rather than clamped.</summary>
    static float Share(string said, int line, string name, string word)
    {
        var figure = Figure(said, line, word);
        return figure is >= 0f and <= 1f
            ? figure
            : throw Bad(said, line, $"{name}={word} is outside a pedal's travel, which is 0 to 1");
    }

    /// <summary>The wheel, wound left at −1 and right at +1 — the two keys, as one figure.</summary>
    static float Steer(string said, int line, string word)
    {
        var figure = Figure(said, line, word);
        return figure is >= -1f and <= 1f
            ? figure
            : throw Bad(said, line, $"steer={word} is outside the wheel's travel, which is −1 (left) to 1 (right)");
    }

    static bool Switched(string said, int line, string word) => word switch
    {
        "on" => true,
        "off" => false,
        _ => throw Bad(said, line, $"handbrake={word} is on or off"),
    };

    static ArgumentException Bad(string said, int line, string why) =>
        new($"drive script line {line}, \"{said}\": {why}.");
}
