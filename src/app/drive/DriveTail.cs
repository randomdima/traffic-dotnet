using System.Text;

namespace TrafficSimulation.App.Drive;

/// <summary>
/// <b>DRV-7 — the script as it is being written.</b> What has been appended since the last look, as steps:
/// a run watches the file it is driving from, so a hand can be changed while the town is standing and
/// somebody is watching it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Only whole lines are taken.</b> A file is written while this is reading it, so the tail stops at the
/// last newline and the half line after it is left for the next look — otherwise a step would be driven as
/// whatever of it had been flushed.
/// </para>
/// <para>
/// <b>It is read by how much it has grown</b>, so a file rewritten with the same lines plus one more reads
/// as that one more. A file that got <em>shorter</em> is a different script, and the run says so rather
/// than driving the tail of one script against the head of another.
/// </para>
/// </remarks>
internal sealed class DriveTail(string path)
{
    /// <summary>How much of the file has been driven, in bytes and in lines.</summary>
    long _taken;

    int _lines;

    /// <summary>Whatever has been appended since the last look, as steps. Empty while nothing has.</summary>
    public DriveStep[] More()
    {
        if (!File.Exists(path)) return [];

        var whole = File.ReadAllBytes(path);
        if (whole.LongLength == _taken) return [];

        if (whole.LongLength < _taken)
        {
            throw new ArgumentException(
                $"{path} got shorter while it was being driven, so it is not the script this run started on. " +
                "A live drive is appended to.");
        }

        // Up to the last newline, and never past it: the rest is a line still being written.
        var end = Array.LastIndexOf(whole, (byte)'\n') + 1;
        if (end <= _taken) return [];

        var text = Encoding.UTF8.GetString(whole, (int)_taken, (int)(end - _taken));
        var steps = DriveScript.Steps(text, _lines);
        _taken = end;
        _lines += text.Count(letter => letter == '\n');
        return steps;
    }
}
