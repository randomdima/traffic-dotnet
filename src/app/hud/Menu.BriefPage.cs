using System.Numerics;
using TrafficSimulation.App.Screen;
using TrafficSimulation.CityGen.Gen;
using GeneratedTowns = TrafficSimulation.CityGen.Maps;

namespace TrafficSimulation.App.Hud;

/// <summary>
/// <b>The brief a generated town was laid off, turned and laid again</b> (GEN-60): its stages' seeds and its main
/// parameters, each a row stepped down or up, and the town regenerated off the brief as it then stands — and, where the
/// run has the brief's file, saved over it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Steps and not a field</b>: no head reads typed text (a phone has no keyboard), so a seed is moved
/// a step at a time like every other figure here. Every seed but the town's own stands for its stage's: stepping it
/// lays that stage and the ones after it again, and nothing before (GEN-11).
/// </para>
/// <para>
/// <b>Regenerating opens the town again</b> under its own name, laid off the edited brief (<see cref="GeneratedTowns.Edit"/>)
/// — the open every map goes through, so nothing about a regenerated town differs from one picked off the list.
/// </para>
/// </remarks>
internal sealed partial class Menu
{
    /// <summary>One row of the brief: what it is called, what it reads, and the brief it steps to either way.</summary>
    readonly record struct BriefKnob(string Name, BriefReads Reads, Func<TownBrief, int, TownBrief> Step);

    delegate void BriefReads(TownBrief brief, ref TextBuffer into);

    /// <summary>The rows, in the order the stages read them: the seeds, the terrain, the connectors, the town.</summary>
    static readonly BriefKnob[] Knobs =
    [
        new("Town seed", static (TownBrief brief, ref TextBuffer into) => into.Add(brief.Seeds.Town),
            static (brief, way) => brief with { Seeds = brief.Seeds with { Town = Stepped(brief.Seeds.Town, way) } }),
        new("Terrain seed", static (TownBrief brief, ref TextBuffer into) => Seed(brief.Seeds.Terrain, ref into),
            static (brief, way) => brief with { Seeds = brief.Seeds with { Terrain = Stepped(brief.Seeds.Terrain ?? brief.Seeds.Town, way) } }),
        new("Connectors seed", static (TownBrief brief, ref TextBuffer into) => Seed(brief.Seeds.Connectors, ref into),
            static (brief, way) => brief with { Seeds = brief.Seeds with { Connectors = Stepped(brief.Seeds.Connectors ?? brief.Seeds.Town, way) } }),
        new("Districts seed", static (TownBrief brief, ref TextBuffer into) => Seed(brief.Seeds.Districts, ref into),
            static (brief, way) => brief with { Seeds = brief.Seeds with { Districts = Stepped(brief.Seeds.Districts ?? brief.Seeds.Town, way) } }),
        new("Streets seed", static (TownBrief brief, ref TextBuffer into) => Seed(brief.Seeds.Streets, ref into),
            static (brief, way) => brief with { Seeds = brief.Seeds with { Streets = Stepped(brief.Seeds.Streets ?? brief.Seeds.Town, way) } }),
        new("Sea", static (TownBrief brief, ref TextBuffer into) => Share(brief.Sea?.Share ?? 0f, ref into),
            static (brief, way) => brief with { Sea = Sea(brief.Sea, way) }),
        new("Sea bearing", static (TownBrief brief, ref TextBuffer into) => Degrees(brief.Sea?.BearingDeg ?? 0f, ref into),
            static (brief, way) => brief.Sea is { } sea ? brief with { Sea = sea with { BearingDeg = Wrapped(sea.BearingDeg + (way * 15f)) } } : brief),
        new("Rivers a km", static (TownBrief brief, ref TextBuffer into) => into.Add(brief.Rivers.PerKm, "F2"),
            static (brief, way) => brief with { Rivers = brief.Rivers with { PerKm = MathF.Max(0f, brief.Rivers.PerKm + (way * 0.05f)) } }),
        new("River width", static (TownBrief brief, ref TextBuffer into) => Metres(brief.Rivers.WidthM, ref into),
            static (brief, way) => brief with { Rivers = brief.Rivers with { WidthM = MathF.Max(10f, brief.Rivers.WidthM + (way * 10f)) } }),
        new("Tributaries", static (TownBrief brief, ref TextBuffer into) => into.Add(brief.Rivers.Tributaries),
            static (brief, way) => brief with { Rivers = brief.Rivers with { Tributaries = Math.Max(0, brief.Rivers.Tributaries + way) } }),
        new("Intercity roads", static (TownBrief brief, ref TextBuffer into) => into.Add(brief.Connectors.IntercityRoads),
            static (brief, way) => brief with { Connectors = brief.Connectors with { IntercityRoads = Math.Max(0, brief.Connectors.IntercityRoads + way) } }),
        new("Railways", static (TownBrief brief, ref TextBuffer into) => into.Add(brief.Connectors.Railways),
            static (brief, way) => brief with { Connectors = brief.Connectors with { Railways = Math.Max(0, brief.Connectors.Railways + way) } }),
        new("Grid districts", static (TownBrief brief, ref TextBuffer into) => Share(brief.Districts.GridShare, ref into),
            static (brief, way) => brief with { Districts = brief.Districts with { GridShare = Math.Clamp(brief.Districts.GridShare + (way * 0.1f), 0f, 1f) } }),
        new("Buildings", static (TownBrief brief, ref TextBuffer into) => into.Add(brief.Roster.Buildings),
            static (brief, way) => brief with { Roster = brief.Roster with { Buildings = Scaled(brief.Roster.Buildings, way) } }),
        new("People", static (TownBrief brief, ref TextBuffer into) => into.Add(brief.Roster.People),
            static (brief, way) => brief with { Roster = brief.Roster with { People = Scaled(brief.Roster.People, way) } }),
        new("Cars", static (TownBrief brief, ref TextBuffer into) => into.Add(brief.Roster.Cars),
            static (brief, way) => brief with { Roster = brief.Roster with { Cars = Scaled(brief.Roster.Cars, way) } }),
    ];

    /// <summary>The rows under the knobs: the two buttons, and the line that says where the brief stands.</summary>
    const int BriefFootRows = 2;

    /// <summary>How wide a step button is: room for its one sign inside the face's own inset either side.</summary>
    const float StepButtonPx = Theme.RowPx * 1.5f;

    /// <summary>The brief of the town standing, as edited on this panel; none where the town was not laid off a brief.</summary>
    TownBrief? _brief;

    /// <summary>Where its rows were laid, the two buttons last.</summary>
    readonly Rect[] _briefRows = new Rect[Knobs.Length + BriefFootRows];

    /// <summary>What the panel last did with the brief, said under its buttons.</summary>
    string _briefSaid = string.Empty;

    /// <summary>The town now standing: its brief taken up for the panel, if it was laid off one.</summary>
    public void TownIs(string map)
    {
        _brief = GeneratedTowns.IsGenerated(map) ? GeneratedTowns.Brief(map) : null;
        _briefSaid = string.Empty;
    }

    static float BriefHeightPx => (Knobs.Length + BriefFootRows) * LinePitchPx;

    void LayBrief(float paneX, float topY, float paneWidthPx)
    {
        for (var row = 0; row < _briefRows.Length; row++)
        {
            _briefRows[row] = new Rect(new Vector2(paneX, topY + (row * LinePitchPx)), new Vector2(paneWidthPx, Theme.RowPx));
        }
    }

    /// <summary>The two halves of the row the buttons stand on.</summary>
    (Rect Regenerate, Rect Save) BriefButtons()
    {
        var row = _briefRows[Knobs.Length];
        var halfPx = (row.SizePx.X - Theme.GapPx) * 0.5f;
        return (new Rect(row.AtPx, new Vector2(halfPx, row.SizePx.Y)),
            new Rect(row.AtPx + new Vector2(halfPx + Theme.GapPx, 0f), new Vector2(halfPx, row.SizePx.Y)));
    }

    /// <summary>The step buttons at a row's right end: down, then up.</summary>
    static (Rect Down, Rect Up) StepButtons(Rect row)
    {
        var size = new Vector2(StepButtonPx, row.SizePx.Y);
        return (new Rect(new Vector2(row.Right - (2f * StepButtonPx) - Theme.GapPx, row.AtPx.Y), size),
            new Rect(new Vector2(row.Right - StepButtonPx, row.AtPx.Y), size));
    }

    MenuChoice ClickedBrief(Vector2 pointPx)
    {
        if (_brief is not { } brief) return MenuChoice.None;

        for (var knob = 0; knob < Knobs.Length; knob++)
        {
            var (down, up) = StepButtons(_briefRows[knob]);
            var way = down.Contains(pointPx) ? -1 : up.Contains(pointPx) ? 1 : 0;
            if (way == 0) continue;

            _brief = Knobs[knob].Step(brief, way);
            _briefSaid = "edited - Regenerate lays it";
            return MenuChoice.None;
        }

        var (regenerate, save) = BriefButtons();
        if (regenerate.Contains(pointPx))
        {
            GeneratedTowns.Edit(brief);
            return new MenuChoice(MenuAction.OpenMap, brief.Name);
        }

        if (save.Contains(pointPx) && GeneratedTowns.CanSave(brief.Name))
        {
            GeneratedTowns.Edit(brief);
            GeneratedTowns.Save(brief.Name);
            _briefSaid = "saved to its brief";
        }

        return MenuChoice.None;
    }

    void DrawBrief(ref ScreenDraw draw, Vector2 pointerPx)
    {
        if (_brief is not { } brief)
        {
            Reading(ref draw, _briefRows[0], "This town was not laid off a brief.");
            return;
        }

        Span<char> text = stackalloc char[32];
        for (var knob = 0; knob < Knobs.Length; knob++)
        {
            var row = _briefRows[knob];
            var (down, up) = StepButtons(row);
            var reading = new TextBuffer(text);
            Knobs[knob].Reads(brief, ref reading);

            var readingPx = GlyphSheet.WidthPx(reading.Written.Length, Theme.TextPx);
            var textY = row.AtPx.Y + ((row.SizePx.Y - Theme.TextPx) * 0.5f);
            draw.TextFitted(
                new Vector2(row.AtPx.X + Theme.InsetPx, textY), Knobs[knob].Name, Theme.TextPx, Theme.Text,
                down.AtPx.X - readingPx - (2f * Theme.GapPx) - row.AtPx.X - Theme.InsetPx);
            draw.Text(new Vector2(down.AtPx.X - Theme.GapPx - readingPx, textY), reading.Written, Theme.TextPx, Theme.Heading);
            Theme.Button(ref draw, down, pointerPx, "-");
            Theme.Button(ref draw, up, pointerPx, "+");
        }

        var (regenerate, save) = BriefButtons();
        Theme.Button(ref draw, regenerate, pointerPx, "Regenerate", Theme.Accent);
        Theme.Button(ref draw, save, pointerPx, GeneratedTowns.CanSave(brief.Name) ? "Save brief" : "No brief file", Theme.RowRest);
        Reading(ref draw, _briefRows[Knobs.Length + 1], _briefSaid);
    }

    static ulong Stepped(ulong seed, int way) => way < 0 && seed == 0 ? 0 : seed + (ulong)(long)way;

    static void Seed(ulong? seed, ref TextBuffer into)
    {
        if (seed is { } own) into.Add(own);
        else into.Add("town's");
    }

    static void Share(float share, ref TextBuffer into)
    {
        into.Add((int)MathF.Round(share * 100f));
        into.Add('%');
    }

    static void Degrees(float degrees, ref TextBuffer into)
    {
        into.Add((int)MathF.Round(degrees));
        into.Add(" deg");
    }

    static void Metres(float metres, ref TextBuffer into)
    {
        into.Add((int)MathF.Round(metres));
        into.Add(" m");
    }

    static float Wrapped(float degrees) => degrees - (360f * MathF.Floor(degrees / 360f));

    /// <summary>A count a tenth further either way, and at least one further, never below none.</summary>
    static int Scaled(int count, int way) => Math.Max(0, count + (way * Math.Max(1, count / 10)));

    /// <summary>The sea a twentieth of the map further in or out, taken away below that and laid again above none.</summary>
    static SeaBrief? Sea(SeaBrief? sea, int way)
    {
        var share = (sea?.Share ?? 0f) + (way * 0.05f);
        return share < 0.05f ? null : (sea ?? new SeaBrief()) with { Share = MathF.Min(share, 0.45f) };
    }
}
