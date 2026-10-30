namespace TrafficSimulation.Multiplayer.Wire;

/// <summary>
/// <b>Who a seat's player says it is</b> (MP-15): the colour it is drawn in, as <c>0xRRGGBB</c>, and the name written over
/// it. A player says its own in its hello; the server says a seat's to a player in the first snapshot that seat's mover is
/// in, and again whenever the seat changes hands — never of a seat outside the area (MP-1).
/// </summary>
/// <remarks>
/// <b>A name is printable ASCII</b>, because that is every glyph a page can draw; a byte outside it fails the message
/// (MP-10), and <see cref="Clean"/> is what a player sends so its own never does.
/// </remarks>
internal readonly record struct SeatCard(uint Rgb, string Name)
{
    /// <summary>The longest a name may be, in characters.</summary>
    public const int LongestName = 24;

    /// <summary>A seat as a snapshot names it: a byte, as <see cref="MoverState.Driver"/> carries it.</summary>
    public const int SeatBits = 8;

    const int RgbBits = 24;
    const int LengthBits = 5;
    const int CharBits = 7;
    const char FirstChar = ' ';
    const char LastChar = '~';

    /// <summary>The most bits one card takes.</summary>
    public const int MostBits = RgbBits + LengthBits + LongestName * CharBits;

    /// <summary>The most bits the cards of a snapshot take, every one of <paramref name="seats"/> said once.</summary>
    public static int MostSectionBits(int seats) => seats * (1 + SeatBits + MostBits) + 1;

    /// <summary><paramref name="name"/> as it may be sent: printable ASCII alone, trimmed, and cut to <see cref="LongestName"/>.</summary>
    public static string Clean(string name)
    {
        Span<char> kept = stackalloc char[LongestName];
        var count = 0;
        foreach (var c in name.Trim())
        {
            if (count == LongestName) break;
            if (c is >= FirstChar and <= LastChar) kept[count++] = c;
        }

        return new string(kept[..count]).TrimEnd();
    }

    public void Write(ref BitWriter w)
    {
        w.Write(Rgb & 0xFF_FFFFu, RgbBits);
        w.Write((uint)Name.Length, LengthBits);
        foreach (var c in Name) w.Write(c, CharBits);
    }

    /// <summary>A card; a name longer than <see cref="LongestName"/> or holding a byte that is not printable fails the reader.</summary>
    public static SeatCard Read(ref BitReader r)
    {
        var rgb = r.Read(RgbBits);
        var length = (int)r.Read(LengthBits, LongestName);
        Span<char> name = stackalloc char[LongestName];
        for (var i = 0; i < length; i++)
        {
            var c = (char)r.Read(CharBits);
            if (c is < FirstChar or > LastChar) r.Fail();
            name[i] = c;
        }

        return new SeatCard(rgb, new string(name[..length]));
    }

    /// <summary>
    /// <b>The cards a snapshot carries</b>, after its header: each a set bit, its seat and the card, and a clear bit after
    /// the last — read into <paramref name="into"/> by seat, as many as were said.
    /// </summary>
    public static void ReadSection(ref BitReader r, SeatCard[] into)
    {
        while (r.ReadBool())
        {
            var seat = (int)r.Read(SeatBits);
            var card = Read(ref r);
            if (!r.Failed) into[seat] = card;
        }
    }
}
