namespace AssetProcessor;

/// <summary>One recorded pipeline problem with enough context to debug without reading the full console log.</summary>
public sealed class PipelineIssue
{
    public required string Code { get; init; }
    public required string Severity { get; init; }
    public required string Message { get; init; }
    public required string RootCause { get; init; }
    public string? Hint { get; init; }
    public string? Phase { get; init; }
    public string? ChildGuid { get; init; }
    public string? ChildDisplayName { get; init; }
    public string? ParentGuid { get; init; }
    public string? RelatedGuid { get; init; }
    public string? FilePath { get; init; }
    public string? Detail { get; init; }
}
