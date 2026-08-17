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

    /// <summary>Drops all recorded issues (e.g. before a new logical run reuses a context).</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _issues.Clear();
        }
    }

    public IReadOnlyList<PipelineIssue> GetAll()
    {
        lock (_lock)
        {
            return _issues.ToList();
        }
    }

    public bool HasErrors
    {
        get
        {
            lock (_lock)
            {
                return _issues.Any(i => i.Severity == PipelineIssueSeverity.Error);
            }
        }
    }

    public void ReportParentNotInGuidIndex(string parentGuid, string? childDisplayName, string? childFilePath)
    {
        Add(new PipelineIssue
        {
            Code = PipelineIssueCodes.ParentAssetNotInGuidIndex,
            Severity = PipelineIssueSeverity.Warning,
            Phase = PipelineIssuePhase.DependencyResolution,
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
            Severity = PipelineIssueSeverity.Error,
            Phase = PipelineIssuePhase.DependencyResolution,
            Message = string.Format(ConsoleMessages.Get("issueParentLoadFailedMessage"), parentGuid, detail),
            RootCause = ConsoleMessages.Get("issueParentLoadFailedRootCause"),
            Hint = ConsoleMessages.Get("issueParentLoadFailedHint"),
            ParentGuid = parentGuid,
            ChildDisplayName = childDisplayName,
            FilePath = childFilePath,
            Detail = detail,
        });
    }

    public void ReportDependencyChildLoadFailed(string filePath, string detail, string? childDisplayName)
    {
        Add(new PipelineIssue
        {
            Code = PipelineIssueCodes.DependencyChildLoadFailed,
            Severity = PipelineIssueSeverity.Error,
            Phase = PipelineIssuePhase.DependencyResolution,
            Message = string.Format(ConsoleMessages.Get("issueDependencyChildLoadFailedMessage"), filePath, detail),
            RootCause = ConsoleMessages.Get("issueDependencyChildLoadFailedRootCause"),
            Hint = ConsoleMessages.Get("issueDependencyChildLoadFailedHint"),
            ChildDisplayName = childDisplayName,
            FilePath = filePath,
            Detail = detail,
        });
    }

    public void ReportExtractAssetFailed(string guid, string detail) =>
        Add(new PipelineIssue
        {
            Code = PipelineIssueCodes.ExtractAssetFailed,
            Severity = PipelineIssueSeverity.Error,
            Phase = PipelineIssuePhase.Extraction,
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
            Severity = PipelineIssueSeverity.Error,
            Phase = PipelineIssuePhase.TemplateMerge,
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
            Severity = PipelineIssueSeverity.Error,
            Phase = PipelineIssuePhase.Formatting,
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
            Severity = PipelineIssueSeverity.Error,
            Phase = PipelineIssuePhase.Formatting,
            Message = string.Format(ConsoleMessages.Get("unexpectedFileProcessingError"), detail),
            RootCause = ConsoleMessages.Get("issueUnexpectedFileRootCause"),
            Hint = ConsoleMessages.Get("issueUnexpectedFileHint"),
            Detail = detail,
        });

    public void ReportMoveToTemplateFolderFailed(string detail) =>
        Add(new PipelineIssue
        {
            Code = PipelineIssueCodes.MoveToTemplateFolderFailed,
            Severity = PipelineIssueSeverity.Warning,
            Phase = PipelineIssuePhase.Formatting,
            Message = string.Format(ConsoleMessages.Get("couldNotMoveToTemplateFolder"), detail),
            RootCause = ConsoleMessages.Get("issueMoveTemplateRootCause"),
            Hint = ConsoleMessages.Get("issueMoveTemplateHint"),
            Detail = detail,
        });

    public void ReportModPackageSkipped(string filePath) =>
        Add(new PipelineIssue
        {
            Code = PipelineIssueCodes.ModPackageSkippedInvalidXml,
            Severity = PipelineIssueSeverity.Warning,
            Phase = PipelineIssuePhase.AssetMods,
            Message = string.Format(ConsoleMessages.Get("assetModsSkippingInvalidXml"), filePath),
            RootCause = ConsoleMessages.Get("issueModSkipRootCause"),
            Hint = ConsoleMessages.Get("issueModSkipHint"),
            FilePath = filePath,
        });

    public void ReportModPackageReadFailed(string filePath, string detail) =>
        Add(new PipelineIssue
        {
            Code = PipelineIssueCodes.ModPackageReadXmlFailed,
            Severity = PipelineIssueSeverity.Warning,
            Phase = PipelineIssuePhase.AssetMods,
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
            Severity = PipelineIssueSeverity.Error,
            Phase = PipelineIssuePhase.Pipeline,
            Message = message,
            RootCause = rootCause,
            Hint = hint,
            Detail = detail,
        });
}
