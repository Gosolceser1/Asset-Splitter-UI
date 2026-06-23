namespace AssetProcessor;

internal static class LineIdValue
{
    public static bool IsNegativeLineId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        ReadOnlySpan<char> span = value.AsSpan().Trim();
        if (span.Length is < 11 or > 21 || span[0] != '-')
        {
            return false;
        }

        for (int i = 1; i < span.Length; i++)
        {
            if (!char.IsDigit(span[i]))
            {
                return false;
            }
        }
        return true;
    }
}
