namespace TrafficSimulation.World.Physics;

/// <summary>
/// <b>What a device taking the table on reads of it</b>: what each body is (<see cref="TagOf"/>) and which pairs the
/// table's own last step left touching (<see cref="TouchingKeys"/>).
/// </summary>
internal sealed partial class PhysicsWorld
{
    void RestAfresh(int index) => _restTicks[index] = 0;

    /// <summary>What a body is, as it was tagged (<see cref="Tag"/>).</summary>
    public BodyTag TagOf(BodyId body) => BodyTag.Unpack(_tag[body.Index]);

    /// <summary>
    /// The pairs touching as the last step of the table's own left them, held ones among them, by the lower body then the
    /// higher — for a device that takes the steps on from here.
    /// </summary>
    public ReadOnlySpan<ulong> TouchingKeys => _previousKey.AsSpan(0, _previousCount);
}
