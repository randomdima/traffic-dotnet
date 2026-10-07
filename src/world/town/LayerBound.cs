namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>Per-agent state that means something only with the claims layer it was laid with</b> — a hold's number, whose ground a
/// body was laid as, whether a pass's ask is in the layer — kept for the layer every reader reads while a laying a device
/// answers later makes the next one (<see cref="TownWorld.ReadersOccupancy"/>).
/// </summary>
/// <remarks>
/// Saved as the readers have it before the laying writes it (<see cref="Save"/>); set back to that once the laying is done,
/// the laying's own put aside (<see cref="Park"/>); and the laying's taken up when its layer is written back
/// (<see cref="Adopt"/>) — <b>but wherever a reader has written it since</b>, which stands.
/// </remarks>
internal interface ILayerBound
{
    void Save();

    void Park();

    void Adopt();
}

/// <inheritdoc cref="ILayerBound"/>
internal sealed class LayerBound<T>(T[] live) : ILayerBound where T : unmanaged
{
    readonly T[] _readers = new T[live.Length];
    readonly T[] _laid = new T[live.Length];

    public void Save() => live.CopyTo(_readers, 0);

    public void Park()
    {
        live.CopyTo(_laid, 0);
        _readers.CopyTo(live, 0);
    }

    public void Adopt()
    {
        var same = EqualityComparer<T>.Default;
        for (var at = 0; at < live.Length; at++)
        {
            if (same.Equals(live[at], _readers[at])) live[at] = _laid[at];
        }
    }
}
