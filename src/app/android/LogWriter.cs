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
/// </remarks>
internal sealed class LogWriter : TextWriter
{
    readonly StringBuilder _line = new();

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char letter)
    {
        if (letter == '\r') return;

        if (letter != '\n')
        {
            _line.Append(letter);
            return;
        }

        Log.Info(TownActivity.Tag, _line.ToString());
        _line.Clear();
    }
}
