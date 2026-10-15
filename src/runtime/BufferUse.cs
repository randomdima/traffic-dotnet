namespace TrafficSimulation.Runtime;

/// <summary>
/// <b>What a buffer is used for</b>, in words both heads take: Vulkan's own bits on the desktop, where the values are
/// <c>VkBufferUsageFlags</c>' and cast straight across, and a page's every buffer a kernel may touch (<c>town.js</c>).
/// </summary>
[Flags]
internal enum BufferUse : uint
{
    TransferSrc = 0x1,
    TransferDst = 0x2,
    Storage = 0x20,
    Vertex = 0x80,
    Indirect = 0x100,
}

/// <summary>One run of bytes copied from one buffer to another, as <c>VkBufferCopy</c> has it.</summary>
internal readonly record struct CopyRun(ulong FromBytes, ulong ToBytes, ulong Bytes);
