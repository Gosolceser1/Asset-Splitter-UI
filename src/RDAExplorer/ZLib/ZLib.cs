using System.IO.Compression;

namespace RDAExplorer.ZLib;

/// <summary>Zlib decompression used by <see cref="RDAReader"/> to decompress block and file data.</summary>
public static class ZLib
{
    /// <summary>Decompresses <paramref name="input"/>. <paramref name="uncompressedSize"/> is the expected output length.</summary>
    /// <returns>Decompressed bytes.</returns>
    public static byte[] Uncompress(byte[] input, int uncompressedSize)
    {
        if (uncompressedSize < 0)
        {
            throw new InvalidDataException("Uncompressed size cannot be negative.");
        }

        if (uncompressedSize > RDAValidation.MaxArchiveBufferSize)
        {
            throw new InvalidDataException(
              $"Uncompressed size is {uncompressedSize:N0} bytes, which exceeds the supported limit of {RDAValidation.MaxArchiveBufferSize:N0} bytes.");
        }

        byte[] output = new byte[uncompressedSize];
        using MemoryStream inputStream = new(input);
        using ZLibStream zlibStream = new(inputStream, CompressionMode.Decompress);

        int totalRead = 0;
        while (totalRead < output.Length)
        {
            int bytesRead = zlibStream.Read(output, totalRead, output.Length - totalRead);
            if (bytesRead == 0)
            {
                break;
            }

            totalRead += bytesRead;
        }

        if (totalRead != output.Length)
        {
            throw new InvalidDataException(
              $"Zlib stream ended after {totalRead:N0} bytes, expected {output.Length:N0} bytes.");
        }

        if (zlibStream.ReadByte() != -1)
        {
            throw new InvalidDataException("Zlib stream produced more data than the expected uncompressed size.");
        }

        return output;
    }
}
