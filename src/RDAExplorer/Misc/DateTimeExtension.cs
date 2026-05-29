namespace RDAExplorer.Misc;

/// <summary>Converts Unix timestamp (seconds since 1970-01-01 UTC) to <see cref="DateTime"/>. Used for RDA file timestamps.</summary>
public static class DateTimeExtension
{
    private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly ulong MaxUnixTimestamp = (ulong)(DateTime.MaxValue - UnixEpoch).TotalSeconds;

    /// <summary>Converts a Unix timestamp (seconds since epoch) to a UTC <see cref="DateTime"/>.</summary>
    public static DateTime FromTimeStamp(ulong timestamp)
    {
        if (timestamp > MaxUnixTimestamp)
        {
            throw new InvalidDataException("RDA file timestamp is outside the supported DateTime range.");
        }

        return UnixEpoch.AddSeconds(timestamp);
    }
}
