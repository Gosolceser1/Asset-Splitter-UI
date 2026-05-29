namespace RDAExplorer;

internal static class RDAValidation
{
    // The extractor materializes archive data in byte arrays, so reject hostile
    // lengths before they can turn into giant allocations.
    public const int MaxArchiveBufferSize = 512 * 1024 * 1024;

    public static int ToSupportedArrayLength(ulong size, string fieldName)
    {
        if (size > MaxArchiveBufferSize)
        {
            throw new InvalidDataException(
              $"{fieldName} is {size:N0} bytes, which exceeds the supported limit of {MaxArchiveBufferSize:N0} bytes.");
        }

        return (int)size;
    }

    public static long ToStreamOffset(ulong offset, string fieldName)
    {
        if (offset > long.MaxValue)
        {
            throw new InvalidDataException($"{fieldName} is too large for a stream offset.");
        }

        return (long)offset;
    }

    public static ulong SubtractOffset(ulong value, ulong subtract, string fieldName)
    {
        if (subtract > value)
        {
            throw new InvalidDataException($"{fieldName} points before the start of the RDA file.");
        }

        return value - subtract;
    }

    public static void EnsureRangeWithinStream(Stream stream, ulong offset, ulong length, string fieldName)
    {
        if (offset > long.MaxValue)
        {
            throw new InvalidDataException($"{fieldName} is too large for a stream offset.");
        }

        if (!stream.CanSeek)
        {
            return;
        }

        ulong streamLength = (ulong)stream.Length;
        if (offset > streamLength || length > streamLength - offset)
        {
            throw new EndOfStreamException($"{fieldName} extends beyond the end of the RDA file.");
        }
    }

    public static byte[] ReadExact(BinaryReader reader, int count, string fieldName)
    {
        byte[] bytes = reader.ReadBytes(count);
        if (bytes.Length != count)
        {
            throw new EndOfStreamException($"Unexpected end of stream while reading {fieldName}.");
        }

        return bytes;
    }

    public static void ReadExact(Stream stream, byte[] buffer, string fieldName)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int bytesRead = stream.Read(buffer, totalRead, buffer.Length - totalRead);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException($"Unexpected end of stream while reading {fieldName}.");
            }

            totalRead += bytesRead;
        }
    }
}
