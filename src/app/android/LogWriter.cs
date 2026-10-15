using System.Text;
using Android.Util;

namespace TrafficSimulation.App.Android;

/// <summary>
/// The run's own read-out, into logcat: everything the engine prints about itself comes out under the
/// <c>town</c> tag, so <c>adb logcat -s town</c> is what a handset has in place of a terminal.
/// </summary>
/// <remarks>
/// <b>A line at a time and not a write at a time.</b> The engine composes a line out of several writes
/// and logcat is line-oriented, so partial writes are gathered here and handed over on the newline.
/// <b>A string is taken whole</b>: the base class would copy it to an array and hand it over a character at a time.
/// </remarks>
internal sealed class LogWriter : TextWriter
{
    readonly StringBuilder _line = new();

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char letter) => Write(new ReadOnlySpan<char>(in letter));

    public override void Write(string? text) => Write(text.AsSpan());

    public override void Write(char[] buffer, int index, int count) => Write(buffer.AsSpan(index, count));

    public override void Write(ReadOnlySpan<char> text)
    {
        for (var end = text.IndexOf('\n'); end >= 0; end = text.IndexOf('\n'))
        {
            Gather(text[..end]);
            Log.Info(TownActivity.Tag, _line.ToString());
            _line.Clear();
            text = text[(end + 1)..];
        }

        Gather(text);
    }

    void Gather(ReadOnlySpan<char> text)
    {
        for (var cut = text.IndexOf('\r'); cut >= 0; cut = text.IndexOf('\r'))
        {
            _line.Append(text[..cut]);
            text = text[(cut + 1)..];
        }

        _line.Append(text);
    }
}
