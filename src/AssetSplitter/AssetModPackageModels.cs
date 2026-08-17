namespace AssetProcessor;

public sealed record AssetModPackageExportResult(
    string OutputRoot,
    int CreatedCount,
    int SkippedCount,
    bool Prepared = true);

internal sealed record AssetModMetadata(
    string Guid,
    string DisplayName,
    string InternalName,
    string PathHint,
    string SourceFile)
{
    public bool HasDistinctInternalName =>
        !string.IsNullOrWhiteSpace(InternalName) &&
        !InternalName.Equals(DisplayName, StringComparison.OrdinalIgnoreCase);
}

internal sealed record AssetModIndexEntry(
    string Guid,
    string DisplayName,
    string InternalName,
    string PathHint,
    string ModFolderName);
