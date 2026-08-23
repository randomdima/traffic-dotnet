using System.Globalization;

namespace TrafficSimulation.App.Screen;

/// <summary>
/// A line of text built into a caller's own <c>stackalloc</c> buffer. Every read-out on screen is
/// assembled through one of these.
/// </summary>
/// <remarks>
/// <para>
/// <b>It exists because rule 2 is about the frame as well as the tick.</b> Interpolated strings,
/// <c>string.Format</c> and <c>ToString()</c> all allocate, and a read-out that prints the frame cost
/// sixty times a second would be the largest allocator in a build whose whole claim is that the
/// steady state allocates nothing. <see cref="ISpanFormattable.TryFormat"/> writes into a span
/// instead, and a buffer too small silently stops rather than growing — the caller sizes it.
/// </para>
/// <para>
/// <b>The methods return nothing on purpose.</b> A fluent chain over a mutable struct appends to a
/// copy after the first call, which is a bug that reads as working code.
/// </para>
/// </remarks>
internal ref struct TextBuffer(Span<char> into)
{
    readonly Span<char> _into = into;
    int _length;

    public readonly ReadOnlySpan<char> Written => _into[.._length];

    public readonly int Length => _length;

    public void Add(scoped ReadOnlySpan<char> text)
    {
        var room = Math.Min(text.Length, _into.Length - _length);
        if (room > 0)
        {
            text[..room].CopyTo(_into[_length..]);
            _length += room;
        }
    }

    public void Add(char character)
    {
        if (_length < _into.Length) _into[_length++] = character;
    }

    public void Add<T>(T value, ReadOnlySpan<char> format = default) where T : ISpanFormattable
    {
        if (value.TryFormat(_into[_length..], out var written, format, CultureInfo.InvariantCulture)) _length += written;
    }

    /// <summary>Pads to a column so a table of read-outs lines up without anybody measuring a glyph.</summary>
    public void PadTo(int column)
    {
        while (_length < column && _length < _into.Length) _into[_length++] = ' ';
    }

    public void Clear() => _length = 0;
}
