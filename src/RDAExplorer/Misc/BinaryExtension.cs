using System.Buffers.Binary;

namespace RDAExplorer.Misc;

/// <summary>
/// Helpers for RDA binary format: decryption (LCG XOR cipher) and version-specific decryption seeds.
/// RDA V2.0 and V2.2 use different seeds for encrypted blocks.
/// </summary>
public static class BinaryExtension
{
    /// <summary>Returns the decryption seed for the given RDA version. Required for <see cref="Decrypt"/>.</summary>
    public static int GetDecryptionSeed(FileHeader.RdaVersion version) => version switch
    {
        FileHeader.RdaVersion.Invalid => throw new ArgumentException("Invalid file version", nameof(version)),
        FileHeader.RdaVersion.Version20 => 666666,
        FileHeader.RdaVersion.Version22 => 1908874353,
        _ => throw new ArgumentException($"Files of version {Enum.GetName(version)} cannot be decrypted yet.", nameof(version))
    };

    /// <summary>
    /// Decrypts RDA block data using a linear congruential generator (LCG) XOR cipher.
    /// Each 16-bit word is XORed with the next LCG output.
    /// </summary>
    /// <param name="buffer">Encrypted bytes (from RDA block).</param>
    /// <param name="seed">Decryption seed from <see cref="GetDecryptionSeed"/>.</param>
    /// <returns>Decrypted bytes (same length; odd trailing byte copied unchanged).</returns>
    public static byte[] Decrypt(byte[] buffer, int seed)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (buffer.Length == 0)
        {
            return buffer;
        }

        if (seed == 0)
        {
            throw new ArgumentException("Invalid decryption seed");
        }

        byte[] output = new byte[buffer.Length];
        int lcgState = seed;
        for (int i = 0; i + 1 < buffer.Length; i += 2)
        {
            short encryptedWord = BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(i, 2));
            unchecked
            {
                lcgState = lcgState * 214013 + 2531011;
            }

            short xorKey = (short)((lcgState >> 16) & short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(i, 2), (short)(encryptedWord ^ xorKey));
        }

        if (buffer.Length % 2 != 0)
        {
            output[^1] = buffer[^1];
        }

        return output;
    }
}
