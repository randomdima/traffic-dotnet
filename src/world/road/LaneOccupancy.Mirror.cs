namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The reservations as a device laid and answered them, written in place of the host's own rebuild</b>
/// (<c>app/devicetick/DeviceRebuild</c>): every slot as the device numbered and cut it, every hold with what it came to, and
/// each way's two lists in the order the device keeps them — so every reader asks the questions it always asks.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as the answers left it</b>: no plan is being answered, every hold is shown whole, and what each was cut at is
/// its last answer — or, laid where it stands, what it was laid to.
/// </para>
/// <para>
/// <b>The ways laid on are counted in the order the host's own laying first touches them</b>, the order of their lowest
/// slot, so <see cref="OccupiedWays"/> reads as it would have.
/// </para>
/// </remarks>
internal sealed partial class LaneOccupancy
{
    /// <summary>
    /// <b>A rebuild laid elsewhere begun</b>: everything laid last dropped (<see cref="Begin"/>), and room taken for
    /// <paramref name="slots"/> slots and <paramref name="holds"/> holds, every one written next.
    /// </summary>
    /// <param name="everyWay">
    /// Whether every way's lists are written next — or only some ways' (<see cref="KnowTheWay"/>), every other way's
    /// planned list holding the lights' holds on it alone.
    /// </param>
    /// <param name="everyHold">
    /// Whether every hold is written next — or only those of the agents the town reads, each written as it is asked for
    /// (<see cref="MirrorTheHold"/>) until the next mirror begins.
    /// </param>
    public void BeginTheMirror(int slots, int holds, bool everyWay = true, bool everyHold = true)
    {
        if (slots > _slots.Length || holds > _holdFirst.Length)
        {
            throw new InvalidOperationException($"{slots} slots and {holds} holds were laid elsewhere, past the {_slots.Length} and {_holdFirst.Length} there is room for.");
        }

        Begin();
        Mirrors++;
        KnowEveryWay(everyWay);
        KnowEveryHold(everyHold);
        _slotCount = slots;
        _holdCount = holds;
    }

    /// <summary>One slot as it was laid and cut: its claim, its way, whether it was taken out, and how far its holder's body reaches along the way.</summary>
    public void MirrorTheSlot(int slot, int way, in LaneClaim claim, bool gone, float standsToM)
    {
        _slots[slot] = claim;
        _slotWay[slot] = way;
        _gone[slot] = gone;
        _slotStandsToM[slot] = standsToM;
        _next[slot] = NoSlot;
        _prev[slot] = NoSlot;
        if (_laidOn[way]) return;

        _laidOn[way] = true;
        _touched[_touchedCount++] = way;
    }

    /// <summary>
    /// One hold as it was laid and answered: its slots, its stand-off, whether it was answered with the plans and on its
    /// holder's terms, and what it came to.
    /// </summary>
    public void MirrorTheHold(
        int hold, int firstSlot, int endSlot, float standingMarginM, bool answered, in HolderTerms terms, float cutLineM,
        float cutMarginM, in LaneClaim cutBy, int cutOn)
    {
        _holdFirst[hold] = firstSlot;
        _holdEnd[hold] = endSlot;
        _holdStandingMarginM[hold] = standingMarginM;
        _answered[hold] = answered;
        _holdTerms[hold] = terms;
        _holdCutLineM[hold] = cutLineM;
        _holdCutMarginM[hold] = cutMarginM;
        _holdCutBy[hold] = cutBy;
        _holdCutOn[hold] = cutOn;
        _shownToLineM[hold] = float.PositiveInfinity;
        _holdKnownIn[hold] = Mirrors;
    }

    /// <summary>
    /// <b>One way's bodies, or its planned pieces, as the device lists them</b> — nearest first, of two from one metre the
    /// one laid first — every slot taken out since left off it.
    /// </summary>
    public void MirrorTheList(int way, bool bodies, ReadOnlySpan<uint> slots)
    {
        var heads = bodies ? _bodies : _planned;
        var last = NoSlot;
        foreach (var listed in slots)
        {
            var slot = (int)listed;
            if (_gone[slot]) continue;

            _prev[slot] = last;
            if (last == NoSlot) heads[way] = slot;
            else _next[last] = slot;

            last = slot;
        }
    }
}
