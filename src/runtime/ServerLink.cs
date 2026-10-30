using System.Buffers.Binary;

namespace TrafficSimulation.Runtime;

/// <summary>Where a link to a served town stands.</summary>
internal enum LinkState : byte
{
    None = 0,
    Opening = 1,
    Open = 2,
    Closed = 3,
}

/// <summary>
/// <b>A player's line to a served town</b> (MP-3): whole messages in and out, through two buffers the head's machine
/// fills and empties once a frame. It knows nothing of what the messages say.
/// </summary>
/// <remarks>
/// <para>
/// <b>On a page the socket is the page's</b> (<c>town.js</c>) and the buffers ride the pump the frame already makes, so
/// being joined costs a frame no crossing (WEB-2). What arrived since the last pump is in <see cref="Inbox"/>; what is
/// written to <see cref="Send"/> goes at the next.
/// </para>
/// <para>
/// Each buffer is its used length, four bytes little-endian, then its messages, each its own length and then itself:
/// the inbox's from <see cref="Head"/>, past the link's state, and the outbox's straight after the length.
/// </para>
/// <para>
/// <b>Only a page opens one</b>: a desktop head has no half of <see cref="OpenOnThisHead"/>, so its link stays
/// <see cref="LinkState.None"/>.
/// </para>
/// </remarks>
internal sealed partial class ServerLink
{
    /// <summary>Where a buffer's messages start.</summary>
    const int Head = 8;

    /// <summary>The most one pump carries in, which is more than the largest snapshot a server writes.</summary>
    const int InboxBytes = 256 * 1024;

    /// <summary>The most one frame sends, which is a few inputs.</summary>
    const int OutboxBytes = 4 * 1024;

    readonly byte[] _inbox = GC.AllocateArray<byte>(InboxBytes, pinned: true);
    readonly byte[] _outbox = GC.AllocateArray<byte>(OutboxBytes, pinned: true);
    int _read;

    /// <summary>The buffer the machine writes what arrived into.</summary>
    public Span<byte> Inbox => _inbox;

    /// <summary>And the one it sends from and empties.</summary>
    public Span<byte> Outbox => _outbox;

    public LinkState State => (LinkState)_inbox[4];

    /// <summary>A link to the server at <paramref name="url"/> asked for; it opens over the frames that follow.</summary>
    public void Open(string url) => OpenOnThisHead(url);

    partial void OpenOnThisHead(string url);

    /// <summary>The pump's inbox read again from its first message.</summary>
    public void Rewind() => _read = 0;

    /// <summary>The next message that arrived at the last pump, or false when there is none left.</summary>
    public bool TryRead(out ReadOnlySpan<byte> message)
    {
        var used = BinaryPrimitives.ReadInt32LittleEndian(_inbox);
        if (_read + 4 > used)
        {
            message = default;
            return false;
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(_inbox.AsSpan(Head + _read));
        message = _inbox.AsSpan(Head + _read + 4, length);
        _read += 4 + length;
        return true;
    }

    /// <summary>A message to go at the next pump, and whether there was room for it.</summary>
    public bool Send(ReadOnlySpan<byte> message)
    {
        var used = BinaryPrimitives.ReadInt32LittleEndian(_outbox);
        if (4 + used + 4 + message.Length > _outbox.Length) return false;

        BinaryPrimitives.WriteInt32LittleEndian(_outbox.AsSpan(4 + used), message.Length);
        message.CopyTo(_outbox.AsSpan(4 + used + 4));
        BinaryPrimitives.WriteInt32LittleEndian(_outbox, used + 4 + message.Length);
        return true;
    }
}
