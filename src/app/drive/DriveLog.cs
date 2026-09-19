using System.Text;
using TrafficSimulation.Bench;

namespace TrafficSimulation.App.Drive;

/// <summary>
/// <b>What one step of a script left the town in</b> (DRV-3): the line that asked for it, when the town
/// had got to, and what the unit under the hand is doing.
/// </summary>
/// <param name="Rows">
/// The unit read-out, row for row as the panel draws it (<see cref="Hud.UnitReadout"/>). Empty where
/// nothing is picked out.
/// </param>
/// <param name="Told">
/// Whatever the step itself had to say — a listing of the cars near a place, where a frame went, whether a
/// lever had anything to work.
/// </param>
internal readonly record struct DriveReading(
    int Step,
    string Said,
    int Line,
    long Tick,
    float ElapsedS,
    string Unit,
    string Body,
    string Hand,
    string[] Rows,
    string[] Told);

/// <summary>
/// <b>DRV-3 — the run, as a script's own feedback.</b> Every step's reading, printed as the run goes for
/// whoever is reading the terminal and written as one document for whoever wants to look at the whole
/// drive.
/// </summary>
/// <remarks>
/// <b>The figures are the interface's own</b> (<see cref="Hud.UnitReadout"/>): this arranges them and
/// works none of them out, so a log and the panel in a frame beside it cannot disagree about what the car
/// was doing.
/// </remarks>
internal sealed class DriveLog(string map, string script)
{
    readonly List<DriveReading> _readings = [];

    /// <summary>What the town claimed about itself over the drive, and how long it was watched for.</summary>
    ScenarioWatch[] _watched = [];

    float _watchedS;

    /// <summary>Every step's reading, in the order the script asked for them.</summary>
    public IReadOnlyList<DriveReading> Readings => _readings;

    /// <summary>
    /// Whether a reading is said as it is taken. <b>A live drive echoes</b> (DRV-7): somebody is deciding
    /// the next step from the last one, so a log printed at the end would arrive after the drive it was
    /// about.
    /// </summary>
    public bool Echo { get; init; }

    public void Took(in DriveReading reading)
    {
        _readings.Add(reading);
        if (Echo) Say(reading);
    }

    /// <summary>
    /// A step that could not be carried out, said where the reading for it would have been. <b>A live drive
    /// goes on</b>: a line that asked for the impossible is one to write again.
    /// </summary>
    public void Refused(string why) => Console.WriteLine($"{"",5}refused: {why}");

    /// <summary>
    /// The watches the drive ran under, kept to be printed after the steps (DRV-5). <b>A claim broken under
    /// a hand is a reading</b>, so it is the last thing said and not the run's answer.
    /// </summary>
    public void Watched(ScenarioWatch[] scenario, float watchedS)
    {
        _watched = scenario;
        _watchedS = watchedS;
    }

    /// <summary>The whole run in the terminal: a head line a step, and the figures indented under it.</summary>
    public void Print()
    {
        Console.WriteLine($"{map}: {_readings.Count} step(s) of {script}");

        // A live drive has already said every one of them as it was taken, and a log said twice is a log
        // nobody can find the end of.
        if (!Echo)
        {
            foreach (var reading in _readings) Say(reading);
        }

        if (_watched.Length > 0) ScenarioReport.Print(map, _watched, _watchedS);
    }

    /// <summary>One reading in the terminal: the step, then what the town had to say about the unit.</summary>
    static void Say(in DriveReading reading)
    {
        Console.WriteLine(
            $"{reading.Step,3}  {reading.Said,-44} {reading.Unit}, tick {reading.Tick}, " +
            $"{reading.ElapsedS:F2} s");
        foreach (var line in reading.Told) Console.WriteLine($"{"",5}{line}");

        if (reading.Rows.Length == 0) return;

        Console.WriteLine($"{"",5}hand       {reading.Hand}");
        Console.WriteLine($"{"",5}body       {reading.Body}");
        foreach (var row in reading.Rows) Console.WriteLine($"{"",5}{row}");
    }

    /// <summary>
    /// The same run as a document, for a drive long enough that the terminal is not where it is read.
    /// <b>Markdown</b>, because everything written to be read in this project is.
    /// </summary>
    public void Write(string path)
    {
        var text = new StringBuilder();
        text.Append("# Driving ").Append(map).Append('\n').Append('\n');
        text.Append("Read off `").Append(script).Append("`, ").Append(_readings.Count)
            .Append(" step(s). Every figure below the pose is the unit panel's own row (OBS-2m).\n");

        foreach (var reading in _readings)
        {
            text.Append('\n').Append("## ").Append(reading.Step).Append(". `").Append(reading.Said)
                .Append("` — line ").Append(reading.Line).Append('\n').Append('\n');
            text.Append(reading.Unit).Append(", tick ").Append(reading.Tick).Append(", ")
                .Append(reading.ElapsedS.ToString("F2")).Append(" s\n");

            foreach (var line in reading.Told) text.Append('\n').Append(line).Append('\n');

            if (reading.Rows.Length == 0) continue;

            text.Append('\n').Append("```\n");
            text.Append("hand       ").Append(reading.Hand).Append('\n');
            text.Append("body       ").Append(reading.Body).Append('\n');
            foreach (var row in reading.Rows) text.Append(row).Append('\n');

            text.Append("```\n");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, text.ToString());
        Console.WriteLine($"{"",5}the whole drive is in {path}");
    }
}
