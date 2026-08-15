using System.Buffers.Binary;
using System.Text;

namespace ERSwapper.Core;

public record BundleEntry(string Path, long AbsoluteOffset, long Size)
{
    public bool IsResS => Path.EndsWith(".resS", StringComparison.OrdinalIgnoreCase);
}

public class BundleFormatException : Exception
{
    public BundleFormatException(string message) : base(message) { }
}

public static class UnityBundleReader
{
    private const uint CompressionMask = 0x3F;
    private const uint BlocksInfoAtEnd = 0x80;
    private const uint BlockInfoNeedPaddingAtStart = 0x200;

    public static long? TryFindResSOffset(string bundlePath)
    {
        try
        {
            BundleEntry? resS = ReadEntries(bundlePath).FirstOrDefault(e => e.IsResS);
            return resS?.AbsoluteOffset;
        }
        catch
        {
            return null;
        }
    }

    public static IReadOnlyList<BundleEntry> ReadEntries(string bundlePath)
    {
        using var stream = new FileStream(
            bundlePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024);

        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        string magic = ReadNullTerminated(reader);
        if (magic != "UnityFS")
            throw new BundleFormatException($"Not a UnityFS bundle (found '{magic}').");

        uint version = ReadUInt32BigEndian(reader);
        ReadNullTerminated(reader);
        ReadNullTerminated(reader);

        ReadInt64BigEndian(reader);
        uint compressedBlocksInfoSize = ReadUInt32BigEndian(reader);
        uint uncompressedBlocksInfoSize = ReadUInt32BigEndian(reader);
        uint flags = ReadUInt32BigEndian(reader);

        if (version >= 7) AlignTo(stream, 16);

        long afterHeader = stream.Position;
        byte[] compressed;
        long dataStart;

        if ((flags & BlocksInfoAtEnd) != 0)
        {
            stream.Position = stream.Length - compressedBlocksInfoSize;
            compressed = reader.ReadBytes((int)compressedBlocksInfoSize);
            dataStart = afterHeader;
        }
        else
        {
            compressed = reader.ReadBytes((int)compressedBlocksInfoSize);

            if ((flags & BlockInfoNeedPaddingAtStart) != 0) AlignTo(stream, 16);
            dataStart = stream.Position;
        }

        byte[] directory = Decompress(compressed, (int)uncompressedBlocksInfoSize, flags & CompressionMask);

        return ParseDirectory(directory, dataStart);
    }

    private static byte[] Decompress(byte[] compressed, int uncompressedSize, uint compression)
    {
        return compression switch
        {
            0 => compressed,
            2 or 3 => Lz4Block.Decompress(compressed, uncompressedSize),
            1 => throw new BundleFormatException(
                "The bundle directory is LZMA-compressed, which this reader does not handle."),
            _ => throw new BundleFormatException($"Unknown bundle compression type {compression}."),
        };
    }

    private static IReadOnlyList<BundleEntry> ParseDirectory(byte[] directory, long dataStart)
    {
        int position = 16;

        int blockCount = ReadInt32BigEndian(directory, ref position);
        if (blockCount < 0 || blockCount > 100_000)
            throw new BundleFormatException($"Implausible block count {blockCount}.");

        for (int i = 0; i < blockCount; i++)
        {
            ReadUInt32BigEndian(directory, ref position);
            ReadUInt32BigEndian(directory, ref position);
            ushort blockFlags = ReadUInt16BigEndian(directory, ref position);

            if ((blockFlags & CompressionMask) != 0)
            {
                throw new BundleFormatException(
                    "The bundle's data blocks are compressed, so texture data is not stored " +
                    "contiguously and cannot be located this way.");
            }
        }

        int nodeCount = ReadInt32BigEndian(directory, ref position);
        if (nodeCount < 0 || nodeCount > 100_000)
            throw new BundleFormatException($"Implausible node count {nodeCount}.");

        var entries = new List<BundleEntry>(nodeCount);

        for (int i = 0; i < nodeCount; i++)
        {
            long offset = ReadInt64BigEndian(directory, ref position);
            long size = ReadInt64BigEndian(directory, ref position);
            ReadUInt32BigEndian(directory, ref position);
            string path = ReadNullTerminated(directory, ref position);

            entries.Add(new BundleEntry(path, dataStart + offset, size));
        }

        return entries;
    }

    private static void AlignTo(Stream stream, int alignment)
    {
        long remainder = stream.Position % alignment;
        if (remainder != 0) stream.Position += alignment - remainder;
    }

    private static string ReadNullTerminated(BinaryReader reader)
    {
        var builder = new StringBuilder();

        while (true)
        {
            byte b = reader.ReadByte();
            if (b == 0) return builder.ToString();

            builder.Append((char)b);
        }
    }

    private static string ReadNullTerminated(byte[] data, ref int position)
    {
        int start = position;
        while (position < data.Length && data[position] != 0) position++;

        string value = Encoding.UTF8.GetString(data, start, position - start);
        position++;

        return value;
    }

    private static uint ReadUInt32BigEndian(BinaryReader reader)
        => BinaryPrimitives.ReadUInt32BigEndian(reader.ReadBytes(4));

    private static long ReadInt64BigEndian(BinaryReader reader)
        => BinaryPrimitives.ReadInt64BigEndian(reader.ReadBytes(8));

    private static uint ReadUInt32BigEndian(byte[] data, ref int position)
    {
        uint value = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position));
        position += 4;
        return value;
    }

    private static int ReadInt32BigEndian(byte[] data, ref int position)
        => (int)ReadUInt32BigEndian(data, ref position);

    private static ushort ReadUInt16BigEndian(byte[] data, ref int position)
    {
        ushort value = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(position));
        position += 2;
        return value;
    }

    private static long ReadInt64BigEndian(byte[] data, ref int position)
    {
        long value = BinaryPrimitives.ReadInt64BigEndian(data.AsSpan(position));
        position += 8;
        return value;
    }
}
