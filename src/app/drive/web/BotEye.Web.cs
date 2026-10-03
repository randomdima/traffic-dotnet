using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Drive;

/// <summary>
/// <b>The eye a page never opens.</b> A second seat is driven from a file on a disk (DRV-8) and a page has
/// neither, so <c>Game.NewEye</c> answers <c>null</c> here and nothing constructs this.
/// </summary>
/// <remarks>
/// It exists because <see cref="BotSeat"/> and <c>Game</c> are shared and name the type; the desktop's
/// <c>BotEye.cs</c> draws through the shot's stage, which is the desktop's offscreen target.
/// </remarks>
internal sealed class BotEye : IDisposable
{
    BotEye()
    {
    }

    public string Draw(in DriveFrame wanted, TownWorld world, long tick) =>
        throw new PlatformNotSupportedException("A page has no second target to draw a bot's frame into.");

    public void Dispose()
    {
    }
}
