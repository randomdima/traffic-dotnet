using System.Reflection;
using TrafficSimulation.App.Main;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.Runtime;

// The browser's entry, and the counterpart of src/app/main/Program.cs. What it does that the desktop's
// does not is put the town's own files where the town expects to find them: everything above this reads
// assets/ and towns/ by walking up from where the binary landed, and in a page nothing landed anywhere,
// so they are fetched into the runtime's file system first and every reader above is untouched.
//
// The command line is the query string (WEB-5, see main.js): ?map=Test&ui=nodes is --map Test --ui nodes.
// What a page does not carry — the shot, the sheet, the probes, the workshop steps — is WEB-3.

var options = Options.Read(args);
Say("fetching the town…");

try
{
    var fetched = await Data.Fetch(Say);
    Say($"starting on {fetched} files…");

    var wgsl = Shader("Shaders/town.wgsl");
    var trouble = await WebGpu.Start(wgsl);
    if (trouble.Length > 0)
    {
        Say(trouble);
        return;
    }

    Say("reading the figures…");
    var config = SimConfig.Load();

    Say("laying the town's art…");
    using var game = new Game(
        config, width: 0, height: 0, validate: false, options.UiScale, Pacing.Fifo,
        fullscreen: false, display: null);

    game.Switch(options.Ui);
    if (options.Map is { } map)
    {
        Say($"standing {map} up…");
        game.Start(map);
    }

    Say(string.Empty);

    // The loop, handed back to the browser: it calls this between paints, and a run that held on to
    // the thread instead would be a page that never painted at all.
    WebGpu.Ticker(game.Step);

    // Main returns and the runtime stays up, which is what keeps the town standing between callbacks.
    await Task.Delay(Timeout.Infinite);
}
catch (Exception trouble)
{
    Say(trouble.Message);
    throw;
}

static void Say(string line)
{
    if (line.Length > 0) Console.WriteLine(line);
    WebGpu.Say(line);
}

/// <summary>The shader as the assembly ships it, which is how the desktop ships its SPIR-V too.</summary>
static string Shader(string resource)
{
    using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
                       ?? throw new InvalidOperationException($"No embedded resource {resource}.");
    using var reader = new StreamReader(stream);
    return reader.ReadToEnd();
}

/// <summary>What the query string asked for. The words are the desktop's, and the ones a page cannot answer are not offered.</summary>
file readonly record struct Options(string? Map, string[] Ui, float UiScale)
{
    public static Options Read(string[] args)
    {
        string? map = null;
        var ui = Array.Empty<string>();
        var uiScale = 0f;
        for (var at = 0; at < args.Length - 1; at++)
        {
            switch (args[at])
            {
                case "--map":
                    map = args[at + 1];
                    break;
                case "--ui":
                    ui = args[at + 1].Split(',', StringSplitOptions.RemoveEmptyEntries);
                    break;
                case "--ui-scale":
                    uiScale = float.Parse(args[at + 1]);
                    break;
            }
        }

        return new Options(map, ui, uiScale);
    }
}
