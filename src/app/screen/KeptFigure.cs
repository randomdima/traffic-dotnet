namespace TrafficSimulation.App.Screen;

/// <summary>
/// A figure formatted once and kept until the value it was formatted from changes — for a read-out
/// drawn every frame over a value that moves far less often than that.
/// </summary>
internal struct KeptFigure<T>(string format, int mostChars)
    where T : struct, ISpanFormattable, IEquatable<T>
{
    readonly char[] _chars = new char[mostChars];
    int _length;
    T _of;
    bool _kept;

    public ReadOnlySpan<char> Of(T value)
    {
        if (!_kept || !value.Equals(_of))
        {
            var into = new TextBuffer(_chars);
            into.Add(value, format);
            _length = into.Length;
            _of = value;
            _kept = true;
        }

        return _chars.AsSpan(0, _length);
    }
}
