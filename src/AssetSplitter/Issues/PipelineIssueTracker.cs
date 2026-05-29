using System.Threading;

namespace AssetProcessor;

/// <summary>Thread-safe collector for structured pipeline issues (warnings/errors).</summary>
public sealed class PipelineIssueTracker
{
    private readonly Lock _lock = new();
    private readonly List<PipelineIssue> _issues = [];

    public void Add(PipelineIssue issue)
    {
        lock (_lock)
        {
            _issues.Add(issue);
        }
    }

    public IReadOnlyList<PipelineIssue> GetAll()
    {
        lock (_lock)
        {
            return _issues.ToList();
        }
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _issues.Count;
            }
        }
    }

    public void ReportParentNotInGuidIndex(string parentGuid, string? childDisplayName, string? childFilePath)
    {
        Add(new PipelineIssue
        {
            Code = PipelineIssueCodes.ParentAssetNotInGuidIndex,
            Severity = "Warning",
            Phase = "DependencyResolution",
            Message = string.Format(ConsoleMessages.Get("issueParentNotFoundMessage"), parentGuid),
            RootCause = ConsoleMessages.Get("issueParentNotFoundRootCause"),
            Hint = ConsoleMessages.Get("issueParentNotFoundHint"),
            ParentGuid = parentGuid,
            ChildDisplayName = childDisplayName,
            FilePath = childFilePath,
        });
    }

    public void ReportParentLoadFailed(string parentGuid, string detail, string? childDisplayName, string? childFilePath)
    {
        Add(new PipelineIssue
        {
            Code = PipelineIssueCodes.ParentAssetLoadFailed,
            Severity = "Error",
            Phase = "DependencyResolution",
            Message = string.Format(ConsoleMessages.Get("issueParentLoadFailedMessage"), parentGuid, detail),
            RootCause = ConsoleMessages.Get("issueParentLoadFailedRootCause"),
            Hint = ConsoleMessages.Get("issueParentLoadFailedHint"),
            ParentGuid = parentGuid,
            ChildDisplayName = childDisplayName,
            FilePath = childFilePath,
            Detail = detail,
        });
    }

    public void ReportExtractAssetFailed(string guid, string detail) =>
        Add(new PipelineIssue
        {
            Code = PipelineIssueCodes.ExtractAssetFailed,
            Severity = "Error",
            Phase = "Extraction",
            Message = string.Format(ConsoleMessages.Get("extractAssetFailed"), guid, detail),
            RootCause = ConsoleMessages.Get("issueExtractFailedRootCause"),
            Hint = ConsoleMessages.Get("issueExtractFailedHint"),
            ChildGuid = guid,
            Detail = detail,
        });

    public void ReportMergeAssetFailed(string filePath, string detail) =>
        Add(new PipelineIssue
        {
            Code = PipelineIssueCodes.MergeAssetFailed,
            Severity = "Error",
            Phase = "TemplateMerge",
            Message = string.Format(ConsoleMessages.Get("mergeAssetFailed"), filePath, detail),
            RootCause = ConsoleMessages.Get("issueMergeFailedRootCause"),
            Hint = ConsoleMessages.Get("issueMergeFailedHint"),
            FilePath = filePath,
            Detail = detail,
        });

    public void ReportFormatFileFailed(string filePath, string detail) =>
        Add(new PipelineIssue
        {
            Code = PipelineIssueCodes.FormatFileFailed,
            Severity = "Error",
            Phase = "Formatting",
            Message = string.Format(ConsoleMessages.Get("formatSingleFileFailed"), filePath, detail),
            RootCause = ConsoleMessages.Get("issueFormatFailedRootCause"),
            Hint = ConsoleMessages.Get("issueFormatFailedHint"),
            FilePath = filePath,
            Detail = detail,
        });

    public void ReportUnexpectedFileError(string detail) =>
        Add(new PipelineIssue
        {
            Code = PipelineIssueCodes.UnexpectedFileProcessingError,
            Severity = "Error",
            Phase = "Formatting",
            Message = string.Format(ConsoleMessages.Get("unexpectedFileProcessingError"), detail),
            RootCause = ConsoleMessages.Get("issueUnexpectedFileRootCause"),
            Hint = ConsoleMessages.Get("issueUnexpectedFileHint"),
            Detail = detail,
        });

    public void ReportMoveToTemplateFolderFailed(string detail) =>
        Add(new PipelineIssue
        {
            Code = PipelineIssueCodes.MoveToTemplateFolderFailed,
            Severity = "Warning",
            Phase = "Formatting",
            Message = string.Format(ConsoleMessages.Get("couldNotMoveToTemplateFolder"), detail),
            RootCause = ConsoleMessages.Get("issueMoveTemplateRootCause"),
            Hint = ConsoleMessages.Get("issueMoveTemplateHint"),
            Detail = detail,
        });

    public void ReportModPackageSkipped(string filePath) =>
        Add(new PipelineIssue
        {
            Code = PipelineIssueCodes.ModPackageSkippedInvalidXml,
            Severity = "Warning",
            Phase = "AssetMods",
            Message = string.Format(ConsoleMessages.Get("assetModsSkippingInvalidXml"), filePath),
            RootCause = ConsoleMessages.Get("issueModSkipRootCause"),
            Hint = ConsoleMessages.Get("issueModSkipHint"),
            FilePath = filePath,
        });

    public void ReportModPackageReadFailed(string filePath, string detail) =>
        Add(new PipelineIssue
        {
            Code = PipelineIssueCodes.ModPackageReadXmlFailed,
            Severity = "Warning",
            Phase = "AssetMods",
            Message = string.Format(ConsoleMessages.Get("assetModsReadXmlWarning"), filePath, detail),
            RootCause = ConsoleMessages.Get("issueModReadRootCause"),
            Hint = ConsoleMessages.Get("issueModReadHint"),
            FilePath = filePath,
            Detail = detail,
        });

    public void ReportFatal(string code, string message, string rootCause, string? hint = null, string? detail = null) =>
        Add(new PipelineIssue
        {
            Code = code,
            Severity = "Error",
            Phase = "Pipeline",
            Message = message,
            RootCause = rootCause,
            Hint = hint,
            Detail = detail,
        });
}
