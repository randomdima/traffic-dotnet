namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The reservations as they stand, as data</b> — what a device lays into its own buffers to answer the plans beside
/// the host (<c>app/devicetick/DeviceClaims</c>). Read and never written.
/// </summary>
internal sealed partial class LaneOccupancy
{
    /// <summary>Every reservation laid this rebuild, taken ones included, by slot.</summary>
    public ReadOnlySpan<LaneClaim> Slots => _slots.AsSpan(0, _slotCount);

    /// <summary>The way each slot is on.</summary>
    public ReadOnlySpan<int> SlotWays => _slotWay.AsSpan(0, _slotCount);

    /// <summary>Whether each slot has been taken out of its way's list since it was laid.</summary>
    public ReadOnlySpan<bool> SlotsGone => _gone.AsSpan(0, _slotCount);

    /// <summary>
    /// How far along its way each main claim's holder's body already reaches (<see cref="Propose"/>) — meaningful for a
    /// main claim of a hold answered with the plans and for no other slot.
    /// </summary>
    public ReadOnlySpan<float> SlotStandsToM => _slotStandsToM.AsSpan(0, _slotCount);

    /// <summary>How many holds one rebuild may lay.</summary>
    public int HoldCapacity => _holdFirst.Length;

    /// <summary>What a hold's holder reads of whatever refuses it (<see cref="Terms"/>).</summary>
    public HolderTerms TermsOf(int hold) => _holdTerms[hold];

    /// <summary>One way's bodies, or its planned pieces, nearest first, as the slots they are laid in.</summary>
    /// <returns>How many were written, which <paramref name="into"/>'s length bounds.</returns>
    public int ListOf(int way, bool bodies, Span<int> into)
    {
        var written = 0;
        for (var at = (bodies ? _bodies : _planned)[way]; at != NoSlot && written < into.Length; at = _next[at])
        {
            into[written++] = at;
        }

        return written;
    }

    /// <summary>The slots a hold's pieces were laid in, in the order its holder's line runs over them.</summary>
    public (int First, int End) PiecesOf(int hold) => (_holdFirst[hold], _holdEnd[hold]);

    /// <summary>The ground a hold's holder keeps off whatever cuts it (<see cref="BeginHold"/>).</summary>
    public float StandingMarginM(int hold) => _holdStandingMarginM[hold];
}
