namespace AssetProcessor;

/// <summary>Logger wired to debug mode and unified console output.
/// <c>Debug</c> and <c>Write</c> only write in debug mode.
/// <c>Write</c> with <c>always: true</c> writes in both debug and non-debug modes.</summary>
public sealed class PipelineLogger(PipelineContext context)
{
    public void Debug(string message)
    {
        if (context.DebugMode)
        {
            AssetProcessorConsole.WriteColoredMessage(message, ConsoleMessageKind.Debug);
        }
    }

    public void Write(ConsoleMessageKind kind, string message, bool always = false)
    {
        if (context.DebugMode || always)
        {
            AssetProcessorConsole.WriteColoredMessage(message, kind);
        }
    }

    /// <summary>
    /// Writes a <see cref="ConsoleMessages"/> line. When stdout is redirected to the GUI,
    /// emits a structured <c>[I18N]</c> marker instead of English text.
    /// </summary>
    public void WriteLocalized(ConsoleMessageKind kind, string consoleMessagesKey, bool always = false, params object[] args)
    {
        if (!(context.DebugMode || always))
        {
            return;
        }

        if (Console.IsOutputRedirected)
        {
            Console.WriteLine(BackendI18nLine.Format("consoleMessages." + consoleMessagesKey, args));
            return;
        }

        string template = ConsoleMessages.Get(consoleMessagesKey);
        string message = args.Length == 0 ? template : string.Format(template, args);
        AssetProcessorConsole.WriteColoredMessage(message.TrimStart('\r', '\n'), kind);
    }

    /// <summary>Legacy string type overload (parses known keys such as "ERROR", "CACHE").</summary>
    public void Write(string type, string message, bool always = false)
    {
        Write(AssetProcessorConsole.ParseMessageKind(type) ?? ConsoleMessageKind.Info, message, always);
    }
}
