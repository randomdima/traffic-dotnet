namespace TrafficSimulation.Runtime;

/// <summary>The page's half of <see cref="ServerLink"/>: the socket is opened in <c>town.js</c> and pumped with the keys.</summary>
internal sealed partial class ServerLink
{
    partial void OpenOnThisHead(string url) => WebGpu.Join(url);
}
