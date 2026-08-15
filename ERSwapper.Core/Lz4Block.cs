namespace ERSwapper.Core;

public static class Lz4Block
{
    public static byte[] Decompress(ReadOnlySpan<byte> source, int uncompressedSize)
    {
        if (uncompressedSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(uncompressedSize));

        byte[] destination = new byte[uncompressedSize];

        int src = 0;
        int dst = 0;

        while (src < source.Length)
        {
            int token = source[src++];

            int literalLength = token >> 4;
            if (literalLength == 15) literalLength += ReadLengthExtension(source, ref src);

            if (literalLength > 0)
            {
                if (src + literalLength > source.Length || dst + literalLength > destination.Length)
                    throw new InvalidDataException("LZ4 stream ends mid-literal.");

                source.Slice(src, literalLength).CopyTo(destination.AsSpan(dst));
                src += literalLength;
                dst += literalLength;
            }

            if (src >= source.Length) break;
            if (src + 2 > source.Length) throw new InvalidDataException("LZ4 stream ends mid-offset.");

            int offset = source[src] | (source[src + 1] << 8);
            src += 2;

            if (offset == 0 || offset > dst)
                throw new InvalidDataException($"LZ4 match offset {offset} is out of range.");

            int matchLength = token & 0x0F;
            if (matchLength == 15) matchLength += ReadLengthExtension(source, ref src);
            matchLength += 4;

            if (dst + matchLength > destination.Length)
                throw new InvalidDataException("LZ4 match runs past the end of the output.");

            int match = dst - offset;
            for (int i = 0; i < matchLength; i++) destination[dst++] = destination[match++];
        }

        if (dst != uncompressedSize)
            throw new InvalidDataException($"LZ4 produced {dst} bytes, expected {uncompressedSize}.");

        return destination;
    }

    private static int ReadLengthExtension(ReadOnlySpan<byte> source, ref int index)
    {
        int extra = 0;

        while (true)
        {
            if (index >= source.Length) throw new InvalidDataException("LZ4 length runs past the end.");

            int value = source[index++];
            extra += value;

            if (value != 255) return extra;
        }
    }
}
