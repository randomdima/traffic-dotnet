using System.Numerics;
using TrafficSimulation.App.Screen;

namespace TrafficSimulation.App.Hud;

/// <summary>
/// <b>OBS-2f — a distance between two places is measurable without a rebuild.</b> Two clicks lay a
/// tape; a finished tape is kept and the next is laid beside it.
/// </summary>
/// <remarks>
/// <para>
/// <b>It takes the mouse for as long as it is ticked</b>, and it is offered input <em>before</em> the
/// selection layer — so while it is on, a click measures rather than selecting or ordering. That
/// ordering is the whole of what makes it a tool rather than a mode nobody can get out of.
/// </para>
/// <para>
/// <b>Its points are world coordinates.</b> A rebuild is told only that the town has changed, and
/// drops them: two points on a town that no longer exists are two points in the middle of a field.
/// </para>
/// </remarks>
internal sealed class Ruler
{
    /// <summary>How many finished tapes are kept before the oldest is dropped. Beyond a handful the ground is the instrument's rather than the town's.</summary>
    const int MostKept = 8;

    readonly Vector2[] _fromM = new Vector2[MostKept];
    readonly Vector2[] _toM = new Vector2[MostKept];
    int _kept;
    Vector2 _startedAtM;
    bool _started;

    /// <summary>A click while the switch is ticked: the first sets a tape's start, the second finishes it.</summary>
    public void Click(Vector2 pointM)
    {
        if (!_started)
        {
            _startedAtM = pointM;
            _started = true;
            return;
        }

        // Kept, and the next is laid beside it. The oldest goes rather than the newest, because the
        // one being compared against is usually the one just taken.
        if (_kept == MostKept)
        {
            Array.Copy(_fromM, 1, _fromM, 0, MostKept - 1);
            Array.Copy(_toM, 1, _toM, 0, MostKept - 1);
            _kept--;
        }

        _fromM[_kept] = _startedAtM;
        _toM[_kept] = pointM;
        _kept++;
        _started = false;
    }

    /// <summary>A right-click, or the switch being unticked: the tapes go together.</summary>
    public void Clear()
    {
        _kept = 0;
        _started = false;
    }

    /// <summary>The town has been rebuilt, so every point held is a point on a town that is gone.</summary>
    public void TownChanged() => Clear();

    public void Draw(ref ScreenDraw draw, Camera.Camera2D camera, Vector2 uiPx, Vector2 pointerM)
    {
        for (var tape = 0; tape < _kept; tape++) Tape(ref draw, camera, uiPx, _fromM[tape], _toM[tape]);

        if (_started) Tape(ref draw, camera, uiPx, _startedAtM, pointerM);
    }

    /// <summary>
    /// One tape: the line, its graduations on the legend's own ladder, and its total at the far end
    /// with the unit that figure suits.
    /// </summary>
    static void Tape(ref ScreenDraw draw, Camera.Camera2D camera, Vector2 uiPx, Vector2 fromM, Vector2 toM)
    {
        var pixelsPerMetre = camera.PixelsPerMetre;
        var alongM = toM - fromM;
        var lengthM = alongM.Length();
        if (lengthM <= 0f || pixelsPerMetre <= 0f) return;

        var direction = alongM / lengthM;
        var across = new Vector2(-direction.Y, direction.X);

        // In metres divided by the zoom, so a tape is the same weight on screen at any framing: a
        // line a metre wide is invisible across a city and covers the town at a car's framing.
        var lineM = 2f / pixelsPerMetre;
        var endM = 8f / pixelsPerMetre;

        draw.LineM(fromM, toM, lineM, Theme.RulerTape);
        draw.LineM(fromM - across * endM, fromM + across * endM, lineM, Theme.RulerTape);
        draw.LineM(toM - across * endM, toM + across * endM, lineM, Theme.RulerTape);

        var stepM = Ladder.StepM(60f / pixelsPerMetre);
        for (var at = stepM; at < lengthM; at += stepM)
        {
            var onM = fromM + direction * at;
            draw.LineM(onM - across * endM * 0.5f, onM + across * endM * 0.5f, lineM, Theme.RulerTape);
        }

        // The total is screen text at the tape's far end rather than a mark in the town: a figure
        // written in metres would shrink out of legibility at exactly the framing a long tape is
        // taken at, and the claim being checked is that it is legible.
        Span<char> text = stackalloc char[24];
        var written = new TextBuffer(text);
        Ladder.WriteDistance(ref written, lengthM);
        ScaleLegend.OutlinedText(
            ref draw, camera.ScreenAt(toM, uiPx) + new Vector2(10f, -Theme.TextPx * 0.5f),
            written.Written, Theme.TextPx);
    }
}
