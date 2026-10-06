namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>What a holder reads of whatever refuses its plan</b>, given with it (<see cref="LaneOccupancy.Terms"/>): how far
/// short of something it may get past it comes to rest, whether it is on a call, and how slow traffic has to be going
/// for a call to get past it.
/// </summary>
/// <param name="KeptOffWhatItMayPassM">The room to step out round what it passes — its stand-off where it passes nothing.</param>
/// <param name="PassesBelowMps">A call gets past traffic going slower than this (AMB-4); minus infinity for anybody else.</param>
internal readonly record struct HolderTerms(float KeptOffWhatItMayPassM, bool OnACall, float PassesBelowMps);

/// <summary>
/// <b>Every plan answered at once</b> (TER-4c.1, TER-4c.3): each hold laid whole as it was asked for, then every one of
/// them answered against what all the others came to the pass before, pass after pass, until no answer moves — so
/// which of two holds keeps a piece of ground never turns on which was laid first, and every holder can be answered
/// on its own thread.
/// </summary>
/// <remarks>
/// <para>
/// <b>A pass reads the last and writes the next</b>, and the first is answered against the bodies alone, which is no
/// less than any answer can be. Each pass after that is answered against more or less than the one before by turns — a
/// hold shown longer refuses more — and the two runs close on the layer where every answer is what it was answered
/// against.
/// </para>
/// <para>
/// <b>A layer left moving is still disjoint</b>, where the passes run out: an answer read against holds shown no
/// shorter than they end up is cut wherever anything it meets keeps the ground, so the bound stops the passes on one
/// answered against a longer layer than its own (<see cref="AnswerThePlans"/>).
/// </para>
/// <para>
/// <b>A plan's own reach is its holder's answer</b> — the first of its main claims cut short, read in the order its
/// line runs, and kept off what cut it by the holder's own terms (<see cref="HolderTerms"/>) — <b>and its secondary
/// claims are answered for it</b>: taken where a main claim of another hold over one keeps the ground, which cuts the
/// hold at the metre its main claim came to the mark. Those are the two halves of every meeting, so of two claims that
/// meet, exactly one gives the ground up.
/// </para>
/// <para>
/// <b>A hold laid where it stands</b> — what a hand or a truck cannot stop short of, and nothing else that lays ground
/// — <b>is never taken while the plans are answered</b>: it is nobody's plan, and answered once against the bodies
/// before them, it holds what it holds.
/// </para>
/// </remarks>
internal sealed partial class LaneOccupancy
{
    /// <summary>
    /// How far along its holder's line each hold is shown to the others while the plans are answered — everything
    /// short of the metre, the main claim running over it ending there — and the whole of it at any other time.
    /// </summary>
    readonly float[] _shownToLineM;

    /// <summary>Whether a hold is answered with the plans (<see cref="Propose"/>, <see cref="Place"/>) rather than laid where it stands.</summary>
    readonly bool[] _answered;

    /// <summary>What each hold's holder reads of whatever refuses it (<see cref="Terms"/>).</summary>
    readonly HolderTerms[] _holdTerms;

    /// <summary>For each main claim proposed, how far along its way its holder's own body already reaches — what its answer is read past.</summary>
    readonly float[] _slotStandsToM;

    /// <summary>What a placed hold was laid to end at — its answer, since nothing of its own is asked.</summary>
    readonly PlanAnswer[] _laidAnswer;

    /// <summary>What each hold came to on the last pass.</summary>
    readonly PlanAnswer[] _answer;

    /// <summary>Whether the plans are being answered, which is when a hold laid where it stands is taken by none of them.</summary>
    bool _answering;

    /// <summary>
    /// Whether this pass is the first, which is read against the bodies alone — no less than any answer can be — so
    /// what every plan is shown as is not read at all.
    /// </summary>
    bool _plansHidden;

    /// <summary>
    /// The pass a hold with a piece on each way last moved in. <b>Only a hold on a way something moved on is answered
    /// again</b>: every other is answered against what it was answered against the pass before.
    /// </summary>
    readonly int[] _wayMovedAt;

    /// <summary>The pass whose moves the next answers read — counted on across rebuilds, so nothing has to be cleared.</summary>
    int _movedPass;

    /// <summary>
    /// <b>One piece of a plan laid whole</b> over <c>[ask.FromM, toM)</c> of one way, and its secondary claims with it
    /// — taking nothing: what it keeps is what it is answered (<see cref="AnswerThePlans"/>).
    /// </summary>
    /// <param name="standsToM">How far along the way its holder's own body already reaches (<see cref="Reach"/>'s own).</param>
    public void Propose(in PlannedAsk ask, int way, float toM, float standsToM)
    {
        if (ask.Hold == NoHold) return;

        _answered[ask.Hold] = true;
        if (toM <= ask.FromM) return;

        PlaceTheMarks(ask, way, ask.FromM, toM);
        var slot = _slotCount;
        Append(_planned, way, ask.Laid(toM));
        if (slot < _slotCount) _slotStandsToM[slot] = standsToM;
    }

    /// <summary>The holder's terms, given with its plan — its stand-off, on no call, until given.</summary>
    public void Terms(int hold, in HolderTerms terms)
    {
        if (hold != NoHold) _holdTerms[hold] = terms;
    }

    /// <summary>Whether a hold is answered with the plans rather than laid where it stands.</summary>
    public bool IsAnswered(int hold) => hold != NoHold && _answered[hold];

    /// <summary>
    /// <b>Every plan answered, pass after pass</b>, until no answer moves or <paramref name="mostPasses"/> have run and
    /// one more was read against a longer layer than its own — and every hold then cut where its last answer says, the
    /// piece it was refused on ending on the very metre it was refused at.
    /// </summary>
    /// <param name="settled">False where the passes ran out with a plan still moving: disjoint, but not where its answers say.</param>
    /// <returns>How many times an answer moved from one pass to the next, past the first.</returns>
    public int AnswerThePlans(int mostPasses, out bool settled)
    {
        BeginTheAnswers();
        var laidAgain = 0;
        settled = true;
        for (var pass = 0; ; pass++)
        {
            var moved = ShowTheAnswers();
            if (pass == 0) continue;

            laidAgain += moved;
            if (moved == 0) break;
            if (pass >= mostPasses && pass % 2 == 1)
            {
                settled = false;
                break;
            }
        }

        EndTheAnswers();
        return laidAgain;
    }

    void BeginTheAnswers()
    {
        _answering = true;
        _plansHidden = true;
        for (var hold = 0; hold < _holdCount; hold++)
        {
            if (!_answered[hold]) continue;

            _shownToLineM[hold] = float.NegativeInfinity;
            _laidAnswer[hold] = new PlanAnswer(_holdCutLineM[hold], _holdCutMarginM[hold], _holdCutBy[hold], _holdCutOn[hold]);
        }
    }

    /// <summary>
    /// Whether a hold answered with the plans has anything new to be answered against this pass: the first pass, or a
    /// hold that moved last pass on a way one of its pieces is on.
    /// </summary>
    bool NeedsAnswer(int hold)
    {
        if (!_answered[hold]) return false;
        if (_plansHidden) return true;

        for (var at = _holdFirst[hold]; at < _holdEnd[hold]; at++)
        {
            if (_wayMovedAt[_slotWay[at]] == _movedPass) return true;
        }

        return false;
    }

    /// <summary>
    /// <b>One pass</b>: every hold with anything new to be answered against answered — its main claims' reach and its
    /// secondary claims taken, the nearer cut of the two — then all of them shown; how many now end anywhere else.
    /// </summary>
    int ShowTheAnswers()
    {
        for (var hold = 0; hold < _holdCount; hold++)
        {
            if (!NeedsAnswer(hold)) continue;

            var main = MainAnswer(hold);
            var taken = _plansHidden ? PlanAnswer.Whole : SecondaryCut(hold);
            _answer[hold] = taken.CutLineM < main.CutLineM ? taken : main;
        }

        var pass = _movedPass + 1;
        var moved = 0;
        for (var hold = 0; hold < _holdCount; hold++)
        {
            if (!_answered[hold] || _answer[hold].CutLineM == _shownToLineM[hold]) continue;

            _shownToLineM[hold] = _answer[hold].CutLineM;
            moved++;
            for (var at = _holdFirst[hold]; at < _holdEnd[hold]; at++) _wayMovedAt[_slotWay[at]] = pass;
        }

        _movedPass = pass;
        _plansHidden = false;
        return moved;
    }

    void EndTheAnswers()
    {
        for (var hold = 0; hold < _holdCount; hold++)
        {
            if (!_answered[hold]) continue;

            ref readonly var answer = ref _answer[hold];
            _shownToLineM[hold] = float.PositiveInfinity;
            CutTo(hold, answer);
            _holdCutLineM[hold] = answer.CutLineM;
            _holdCutMarginM[hold] = answer.MarginM;
            _holdCutBy[hold] = answer.CutBy;
            _holdCutOn[hold] = answer.CutOn;
        }

        _answering = false;
    }

    /// <summary>
    /// <b>How far a hold's own main claims reach</b> against what the others were shown as — the first of them cut short,
    /// in the order its holder's line runs over them, kept off what cut it by its holder's terms. A hold with none of its
    /// own, placed rather than asked, is answered where it was laid to end.
    /// </summary>
    PlanAnswer MainAnswer(int hold)
    {
        var asked = false;
        for (var at = _holdFirst[hold]; at < _holdEnd[hold]; at++)
        {
            ref readonly var piece = ref _slots[at];
            if (piece.Secondary) continue;

            asked = true;
            var way = _slotWay[at];
            var reachM = Reach(AskOf(piece), way, piece.ToM, _slotStandsToM[at], out var cutBy);
            if (reachM >= piece.ToM) continue;

            return new PlanAnswer(piece.LineFromM + (reachM - piece.FromM), KeptOffM(hold, cutBy), cutBy, way, reachM);
        }

        return asked ? PlanAnswer.Whole : _laidAnswer[hold];
    }

    /// <summary>
    /// <b>How far short of what cut it a holder comes to rest</b> (S-2a): its stand-off, whatever that was — and behind a
    /// body going nowhere (<see cref="LaneClaim.GoesNowhere"/>) that it could get past, the room its terms keep for
    /// stepping out round it (CAR-46).
    /// </summary>
    /// <remarks>
    /// <b>A queue is waited behind at the stand-off whatever its next movement</b>, so a line of cars at a light closes
    /// up behind one turning off. The room is kept only for what the holder may come to pass — <b>and a call keeps it
    /// behind anything at rest it may pass</b>, a queue included, and behind traffic slower than it means to go
    /// (<see cref="LaneClaim.MayBePassedBy"/>).
    /// </remarks>
    float KeptOffM(int hold, in LaneClaim cutBy)
    {
        ref readonly var terms = ref _holdTerms[hold];
        var roomFor = terms.OnACall
            ? cutBy.MayBePassedBy(NoWay, onACall: true) && (cutBy.Still || cutBy.AlongMps < terms.PassesBelowMps)
            : cutBy.GoesNowhere;
        return roomFor ? terms.KeptOffWhatItMayPassM : _holdStandingMarginM[hold];
    }

    /// <summary>
    /// <b>Whether a piece of a plan is shown to the others</b>, and how far along its way: nothing of it past where its
    /// hold is shown to, the main claim running over that metre ending there.
    /// </summary>
    bool Shows(in LaneClaim piece, out float toM)
    {
        toM = piece.ToM;
        var shownM = _shownToLineM[piece.Hold];
        if (piece.LineFromM >= shownM) return false;
        if (!piece.Secondary) toM = MathF.Min(toM, piece.FromM + (shownM - piece.LineFromM));

        return true;
    }

    /// <summary>Whether a planned piece is held against an asker however the two compare: one laid where it stands, met by a plan while the plans are answered.</summary>
    bool HeldAgainst(in PlannedAsk ask, in LaneClaim other) =>
        _answering && ask.Hold != NoHold && _answered[ask.Hold] && !_answered[other.Hold];

    /// <summary>
    /// <b>Where a hold's secondary claims are taken</b>: the nearest along its holder's line of any a main claim of
    /// another hold over it keeps — or the whole hold, where none is.
    /// </summary>
    PlanAnswer SecondaryCut(int hold)
    {
        var cut = PlanAnswer.Whole;
        for (var at = _holdFirst[hold]; at < _holdEnd[hold]; at++)
        {
            if (_gone[at]) continue;

            ref readonly var piece = ref _slots[at];
            if (!piece.Secondary || piece.LineFromM >= cut.CutLineM) continue;

            var way = _slotWay[at];
            if (TakenBy(way, piece, out var taker)) cut = new PlanAnswer(piece.LineFromM, _holdStandingMarginM[hold], taker, way);
        }

        return cut;
    }

    /// <summary>
    /// Whether a main claim of another hold over a secondary claim keeps the ground — as its own holder's reach would
    /// have read the two (<see cref="Reach"/>) — and which claim that is.
    /// </summary>
    bool TakenBy(int way, in LaneClaim secondary, out LaneClaim taker)
    {
        for (var at = _planned[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var main = ref _slots[at];
            if (main.FromM >= secondary.ToM) break;
            if (main.Secondary || (main.Occupant == secondary.Occupant && main.Of == secondary.Of)) continue;
            if (!Shows(main, out var mainToM) || mainToM <= secondary.FromM) continue;

            var atM = MathF.Max(main.FromM, secondary.FromM);
            var overToM = MathF.Min(mainToM, secondary.ToM);

            // A main claim laid where it stands takes whatever it lies over (<see cref="HeldAgainst"/>).
            if (_answered[main.Hold] && !Beats(
                    AskOf(main), atM, StandsOn(way, main.Occupant, main.Of, atM, overToM), secondary,
                    StandsOn(way, secondary.Occupant, secondary.Of, atM, overToM)))
            {
                continue;
            }

            taker = main;
            return true;
        }

        taker = LaneClaim.Nothing;
        return false;
    }

    /// <summary>The terms a main claim was asked on, read back off the piece it laid (<see cref="PlannedAsk.Laid"/>).</summary>
    static PlannedAsk AskOf(in LaneClaim main) =>
        new(main.Hold, main.Occupant, main.Of, main.Priority, main.FromM, main.LineFromM, main.AheadM, main.CommittedToM, main.AlongMps);

    /// <summary>
    /// <b>A hold cut where its answer says</b>: every piece of it at or past the metre taken out, and the main claim
    /// running over it ending there — on the way it was refused on, at the exact metre of that way it was refused at,
    /// so no hair of the two is left lying over the other.
    /// </summary>
    void CutTo(int hold, in PlanAnswer answer)
    {
        var lineM = answer.CutLineM;
        if (float.IsPositiveInfinity(lineM)) return;

        for (var at = _holdFirst[hold]; at < _holdEnd[hold]; at++)
        {
            if (_gone[at]) continue;

            ref readonly var piece = ref _slots[at];
            if (piece.LineFromM >= lineM)
            {
                Unlink(at);
                continue;
            }

            if (piece.Secondary || piece.LineFromM + (piece.ToM - piece.FromM) <= lineM) continue;

            var refusedHere = _slotWay[at] == answer.CutOn && answer.CutWayM > piece.FromM && answer.CutWayM < piece.ToM;
            var toM = refusedHere ? answer.CutWayM : piece.FromM + (lineM - piece.LineFromM);
            if (toM <= piece.FromM) Unlink(at);
            else _slots[at] = piece with { ToM = toM };
        }
    }
}
