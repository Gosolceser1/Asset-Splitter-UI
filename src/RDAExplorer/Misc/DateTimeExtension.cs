namespace RDAExplorer.Misc;

/// <summary>Converts Unix timestamp (seconds since 1970-01-01 UTC) to <see cref="DateTime"/>. Used for RDA file timestamps.</summary>
public static class DateTimeExtension
{
    private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    // Integer math: the double TotalSeconds rounded up to 253402300800, which then made AddSeconds
    // throw ArgumentOutOfRangeException instead of the documented InvalidDataException.
    private static readonly ulong MaxUnixTimestamp = (ulong)((DateTime.MaxValue - UnixEpoch).Ticks / TimeSpan.TicksPerSecond);

    /// <summary>Converts a Unix timestamp (seconds since epoch) to a UTC <see cref="DateTime"/>.</summary>
    public static DateTime FromTimeStamp(ulong timestamp)
    {
        if (timestamp > MaxUnixTimestamp)
        {
            throw new InvalidDataException("RDA file timestamp is outside the supported DateTime range.");
        }

        return UnixEpoch.AddSeconds(timestamp);
    }

    /// <summary>
    /// Like <see cref="FromTimeStamp"/>, but returns <see cref="DateTime.MinValue"/> for an
    /// out-of-range value. The timestamp is metadata only; one bad directory entry must not make
    /// the whole archive unreadable.
    /// </summary>
    public static DateTime FromTimeStampOrMin(ulong timestamp) =>
        timestamp > MaxUnixTimestamp ? DateTime.MinValue : UnixEpoch.AddSeconds(timestamp);
}
