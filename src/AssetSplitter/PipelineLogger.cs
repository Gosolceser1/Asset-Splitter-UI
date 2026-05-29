namespace AssetProcessor;

/// <summary>Logger wired to debug mode and unified console output.
/// <c>Debug</c> and <c>Write</c> only write in debug mode.
/// <c>Write</c> with <c>always: true</c> writes in both debug and non-debug modes.</summary>
public sealed class PipelineLogger(PipelineContext context)
{
    public void Debug(string message)
    {
        if (context.DebugMode)
            AssetProcessorConsole.WriteColoredMessage(message, "DEBUG");
    }

    public void Write(string type, string message, bool always = false)
    {
        if (context.DebugMode || always)
            AssetProcessorConsole.WriteColoredMessage(message, type);
    }
}
