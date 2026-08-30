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

    // A map, fetched and then stood up. The plan is not in the file system until this has run, which
    // is why the menu's click only writes the name down (Game.Web.cs) and this is what acts on it.
    async Task Open(string map)
    {
        Say($"fetching {map}…");
        await Data.Town(map);
        Say($"standing {map} up…");
        game.Start(map);
        Say(string.Empty);
    }

    game.Switch(options.Ui);
    if (options.Map is { } map) await Open(map);

    Say(string.Empty);

    // The loop, handed back to the browser: it calls this between paints, and a run that held on to
    // the thread instead would be a page that never painted at all.
    var step = game.Step;
    WebGpu.Ticker(step);

    // And Main never returns, which is the whole of what keeps the town standing between those
    // callbacks. <b>A timer and not an infinite wait</b>: the runtime shuts down when nothing is
    // pending, an infinite delay schedules nothing, and an animation callback the browser holds is
    // not something it counts — so the page would tear the device down between the first frame and
    // the second, and report it as a device lost for no reason anybody could see.
    //
    // Short enough that Exit is answered while the hand is still on the mouse. It costs a wake-up
    // five times a second against a callback sixty, and it is what the run is waited on with — and
    // what a map picked on the menu is fetched from, because this is the only place in the run where
    // waiting for it is allowed.
    while (!game.Closed)
    {
        if (game.TakeWanted() is { } picked) await Open(picked);
        await Task.Delay(200);
    }

    // <b>Exit has to be visible, and on this machine nothing else makes it so.</b> A window that
    // closes is its own announcement; a canvas holds its last frame for as long as the tab is open,
    // so a town shut down and a town wedged look exactly alike. The banner is the difference.
    Say("the town has been shut down — reload the page to open it again.");
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
