using System.Diagnostics;
using System.Xml;
using AssetProcessor;

namespace AssetSplitter.Tests;

[CollectionDefinition("GauntletErrorHandling", DisableParallelization = true)]
public sealed class GauntletErrorHandlingCollection;

/// <summary>
/// Wave 1 gauntlet: exception, cancellation, retry, and exit-code behavior.
/// Tests assert observed production behavior (including defects) so failures mean the code changed.
/// </summary>
[Collection("GauntletErrorHandling")]
public sealed class GauntletErrorHandlingTests
{
    [Fact]
    public void ResolveExitCode_ErrorsForceNonZero_EvenWhenOrchestratorReturnedSuccess()
    {
        var context = new PipelineContext();
        context.Issues.ReportExtractAssetFailed("1010017", "disk full");
        Assert.True(context.Issues.HasErrors);
        Assert.Equal(1, PipelineRunSupport.ResolveExitCode(context, orchestratorExitCode: 0));
    }

    [Fact]
    public void ResolveExitCode_WarningsAloneKeepSuccess()
    {
        var context = new PipelineContext();
        context.Issues.ReportParentNotInGuidIndex("999", "Child", @"C:\child.xml");
        context.Issues.ReportModPackageSkipped(@"C:\skip.xml");
        Assert.False(context.Issues.HasErrors);
        Assert.Equal(0, PipelineRunSupport.ResolveExitCode(context, orchestratorExitCode: 0));
    }

    [Fact]
    public void FinishPipelineRun_WithErrors_ReturnsOneAndDoesNotTreatRunAsSuccess()
    {
        using TempWorkspace workspace = new();
        PipelineContext context = CreateContext(workspace.Root);
        context.Issues.ReportFormatFileFailed(Path.Combine(context.AssetOutputDir, "a.xml"), "boom");

        int exitCode = PipelineRunSupport.FinishPipelineRun(
            context,
            Stopwatch.StartNew(),
            assetCount: 1,
            succeeded: true);

        Assert.Equal(1, exitCode);
        PipelineIssue issue = Assert.Single(context.Issues.GetAll());
        Assert.Equal(PipelineIssueCodes.FormatFileFailed, issue.Code);
        Assert.Equal(PipelineIssueSeverity.Error, issue.Severity);
    }

    [Fact]
    public void IssueReporter_WritesJsonWithUsefulFields_WhenErrorsExist()
    {
        using TempWorkspace workspace = new();
        PipelineContext context = CreateContext(workspace.Root);
        context.DebugMode = true;
        context.Issues.ReportMergeAssetFailed(@"C:\out\broken.xml", "root element missing");

        string? reportPath = PipelineIssueReporter.WriteSummary(context, runSucceeded: false);
        Assert.False(string.IsNullOrWhiteSpace(reportPath));
        Assert.True(File.Exists(reportPath));
        string json = File.ReadAllText(reportPath);
        Assert.Contains("MergeAssetFailed", json, StringComparison.Ordinal);
        Assert.Contains("\"RunSucceeded\": false", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("broken.xml", json, StringComparison.Ordinal);
        Assert.Contains("root element missing", json, StringComparison.Ordinal);
    }

    [Fact]
    public void IssueReporter_WritesEmptySummary_EvenWithoutDebugMode()
    {
        using TempWorkspace workspace = new();
        PipelineContext context = CreateContext(workspace.Root);
        context.DebugMode = false;

        string? reportPath = PipelineIssueReporter.WriteSummary(context, runSucceeded: true);
        Assert.False(string.IsNullOrWhiteSpace(reportPath));
        Assert.True(File.Exists(reportPath));
        string json = File.ReadAllText(reportPath!);
        Assert.Contains("\"Total\": 0", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"RunSucceeded\": true", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MoveToTemplateFolderFailed", json, StringComparison.Ordinal);
    }

    [Fact]
    public void IssueReporter_EmptySummary_SupersedesPriorFailedReportOnDisk()
    {
        using TempWorkspace workspace = new();
        PipelineContext context = CreateContext(workspace.Root);
        context.DebugMode = false;
        context.Issues.ReportMoveToTemplateFolderFailed("file already exists");

        string? failedPath = PipelineIssueReporter.WriteSummary(context, runSucceeded: false);
        Assert.True(File.Exists(failedPath));

        context.Issues.Clear();
        string? successPath = PipelineIssueReporter.WriteSummary(context, runSucceeded: true);
        Assert.False(string.IsNullOrWhiteSpace(successPath));
        Assert.NotEqual(failedPath, successPath);

        string successJson = File.ReadAllText(successPath!);
        Assert.Contains("\"Total\": 0", successJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MoveToTemplateFolderFailed", successJson, StringComparison.Ordinal);

        string failedJson = File.ReadAllText(failedPath!);
        Assert.Contains("MoveToTemplateFolderFailed", failedJson, StringComparison.Ordinal);
    }

    [Fact]
    public void GuidFileIndex_DuplicatePaths_DoNotRecordPipelineIssues()
    {
        using TempWorkspace workspace = new();
        PipelineContext context = CreateContext(workspace.Root);
        string a = Path.Combine(context.AssetOutputDir, "1010017 - [Coins].xml");
        string b = Path.Combine(context.AssetOutputDir, "1010017 - [Coins]-2.xml");
        File.WriteAllText(a, "<Asset/>");
        File.WriteAllText(b, "<Asset/>");

        var index = new GuidFileIndex();
        index.Build([a, b], context);

        Assert.Equal(1, index.Count);
        Assert.Empty(context.Issues.GetAll());
        Assert.Equal(a, index.Find("1010017"));
    }

    [Fact]
    public void FormatXml_MalformedFile_RecordsIssueAndReturnsNonZero()
    {
        using TempWorkspace workspace = new();
        PipelineContext context = CreateContext(workspace.Root);
        string badFile = Path.Combine(context.AssetOutputDir, "1010017 - [Coins].xml");
        File.WriteAllText(badFile, "this is not xml <<<<");

        int result = FormattingService.FormatXml(
            context,
            [badFile],
            PropertyScanner.Empty,
            gameType: "anno1800",
            allowMove: false,
            skipInitialMessage: true);

        Assert.Equal(1, result);
        Assert.True(context.Issues.HasErrors);
        Assert.Contains(context.Issues.GetAll(), issue => issue.Code == PipelineIssueCodes.FormatFileFailed);
        Assert.True(File.Exists(badFile));
        Assert.Equal("this is not xml <<<<", File.ReadAllText(badFile));
    }

    [Fact]
    public void FormatXml_ValidFileThenMalformed_FormatsGoodFileAndReportsBadSibling()
    {
        using TempWorkspace workspace = new();
        PipelineContext context = CreateContext(workspace.Root);
        string goodFile = Path.Combine(context.AssetOutputDir, "1 - [Good].xml");
        string badFile = Path.Combine(context.AssetOutputDir, "2 - [Bad].xml");
        File.WriteAllText(goodFile, MinimalAssetXml("1", "Good"));
        File.WriteAllText(badFile, "<not-closed");

        int result = FormattingService.FormatXml(
            context,
            [goodFile, badFile],
            PropertyScanner.Empty,
            gameType: "anno1800",
            allowMove: false,
            skipInitialMessage: true);

        Assert.Equal(1, result);
        Assert.True(context.Issues.HasErrors);
        Assert.True(File.Exists(goodFile));
        Assert.Contains("<Asset>", File.ReadAllText(goodFile), StringComparison.Ordinal);
        Assert.Equal("<not-closed", File.ReadAllText(badFile));
    }

    [Fact]
    public void TranslationDictionaryLoader_CorruptFile_ContinuesWithoutIssue_TreatedAsSuccess()
    {
        using TempWorkspace workspace = new();
        PipelineContext context = CreateContext(workspace.Root);
        File.WriteAllText(Path.Combine(context.SourceXmlFolder, context.AssetLanguage), "<Texts>broken");

        int loadResult = TranslationDictionaryLoader.Load(context);

        Assert.Equal(0, loadResult);
        Assert.Empty(context.Translator);
        Assert.False(context.Issues.HasErrors);
        Assert.Empty(context.Issues.GetAll());
    }

    [Fact]
    public void AssetNameRegistry_CorruptAssetsXml_ReturnsZeroWithoutPipelineIssue()
    {
        using TempWorkspace workspace = new();
        PipelineContext context = CreateContext(workspace.Root);
        File.WriteAllText(Path.Combine(context.SourceXmlFolder, "assets.xml"), "not xml");

        int loadResult = AssetNameRegistry.Load(context);

        Assert.Equal(0, loadResult);
        Assert.False(context.Issues.HasErrors);
        Assert.Empty(context.Issues.GetAll());
    }

    [Fact]
    public void Phase1_MissingGameData_ReturnsOne_WithoutRdaExtractionFailedIssueOrSummary()
    {
        using TempWorkspace workspace = new();
        PipelineContext context = CreateContext(workspace.Root);
        Directory.CreateDirectory(context.AssetRoot);
        context.DetectedGameType = "anno1800";

        int exitCode = Phase1SourceAcquisition.ExecutePhase1RdaExtraction(
            context,
            gameType: "anno1800",
            gameOutputRoot: context.GameOutputRoot);

        Assert.Equal(1, exitCode);
        Assert.DoesNotContain(
            context.Issues.GetAll(),
            issue => issue.Code == PipelineIssueCodes.RdaExtractionFailed);
        Assert.Empty(context.Issues.GetAll());
        string? emptyReport = PipelineIssueReporter.WriteSummary(context, runSucceeded: false);
        Assert.False(string.IsNullOrWhiteSpace(emptyReport));
        Assert.True(File.Exists(emptyReport));
        string json = File.ReadAllText(emptyReport);
        Assert.Contains("\"Total\": 0", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PipelineOrchestrator_MissingMaindata_ExitsOne_WritesNoIssueReport()
    {
        using TempWorkspace workspace = new();
        string gameRoot = Path.Combine(workspace.Root, "Anno 1800");
        string outputRoot = Path.Combine(workspace.Root, "output");
        Directory.CreateDirectory(gameRoot);
        Directory.CreateDirectory(outputRoot);

        int exitCode = PipelineOrchestrator.Run([gameRoot, outputRoot, "english"]);

        Assert.Equal(1, exitCode);
        string annoAssets = Path.Combine(outputRoot, "AnnoAssets");
        if (Directory.Exists(annoAssets))
        {
            string[] reports = Directory.GetFiles(annoAssets, "issues_*.json", SearchOption.AllDirectories);
            Assert.Empty(reports);
        }
    }

    [Fact]
    public void PipelineOrchestrator_Help_ExitsZero()
    {
        Assert.Equal(0, PipelineOrchestrator.Run(["--help"]));
    }

    [Fact]
    public void PipelineOrchestrator_InvalidLanguage_ExitsOne()
    {
        using TempWorkspace workspace = new();
        string gameRoot = Path.Combine(workspace.Root, "Anno 1800");
        Directory.CreateDirectory(gameRoot);
        Assert.Equal(1, PipelineOrchestrator.Run([gameRoot, workspace.Root, "../evil"]));
    }

    [Fact]
    public void Phase3_CorruptAssetsXml_ReturnsExitCodeWithoutThrowing()
    {
        using TempWorkspace workspace = new();
        PipelineContext context = CreateContext(workspace.Root);
        File.WriteAllText(Path.Combine(context.SourceXmlFolder, "assets.xml"), "<Assets><Asset></Assets>");

        int exit = Phase3AssetExtraction.ExecutePhase3AssetExtraction(
            context,
            singleAssetGuid: "",
            gameType: "anno1800",
            gameOutputRoot: context.GameOutputRoot,
            out _,
            out _);

        Assert.Equal(1, exit);
        Assert.Empty(Directory.GetFiles(context.AssetOutputDir, "*.xml", SearchOption.AllDirectories));
    }

    [Fact]
    public void DependencyChildLoadFailed_RecordsError_ButOrchestratorIsVoidSoCallerMustCheckHasErrors()
    {
        using TempWorkspace workspace = new();
        PipelineContext context = CreateContext(workspace.Root);
        context.FixDependencies = true;
        context.GuidIndex = new GuidFileIndex();
        context.GuidIndex.Build([]);
        string childPath = Path.Combine(context.AssetOutputDir, "5 - [Child].xml");
        File.WriteAllText(childPath, "<not-xml");

        DependencyResolutionOrchestrator.Execute(context, [childPath]);

        Assert.True(context.Issues.HasErrors);
        PipelineIssue issue = Assert.Single(context.Issues.GetAll());
        Assert.Equal(PipelineIssueCodes.DependencyChildLoadFailed, issue.Code);
        Assert.Equal(PipelineIssueSeverity.Error, issue.Severity);
        Assert.Equal(childPath, issue.FilePath);
        Assert.False(string.IsNullOrWhiteSpace(issue.RootCause));
        Assert.False(string.IsNullOrWhiteSpace(issue.Hint));
    }

    [Fact]
    public void ParentMissingFromGuidIndex_IsWarningOnly_ExitStaysZero()
    {
        using TempWorkspace workspace = new();
        PipelineContext context = CreateContext(workspace.Root);
        context.FixDependencies = true;
        context.GuidIndex = new GuidFileIndex();
        context.GuidIndex.Build([]);
        string childPath = Path.Combine(context.AssetOutputDir, "6 - [Orphan].xml");
        File.WriteAllText(childPath, """
            <ModOps>
              <ModOp GUID="6" Type="Replace" Path="/">
                <Asset>
                  <BaseAssetGUID>999999</BaseAssetGUID>
                  <Values><Standard><GUID>6</GUID><Name>Orphan</Name></Standard></Values>
                </Asset>
              </ModOp>
            </ModOps>
            """);

        DependencyResolutionOrchestrator.Execute(context, [childPath]);

        Assert.False(context.Issues.HasErrors);
        PipelineIssue issue = Assert.Single(context.Issues.GetAll());
        Assert.Equal(PipelineIssueCodes.ParentAssetNotInGuidIndex, issue.Code);
        Assert.Equal(PipelineIssueSeverity.Warning, issue.Severity);
        Assert.Equal(0, PipelineRunSupport.ResolveExitCode(context, 0));
        Assert.True(File.Exists(childPath));
    }

    [Fact]
    public void XmlDocumentLoadRetry_MalformedXml_FailsFastWithoutRetryBackoff()
    {
        using TempWorkspace workspace = new();
        string path = Path.Combine(workspace.Root, "bad.xml");
        File.WriteAllText(path, "<root>");
        var document = new XmlDocument();
        var timer = Stopwatch.StartNew();

        Assert.Throws<XmlException>(() => XmlDocumentLoadRetry.Load(document, path));

        timer.Stop();
        Assert.True(timer.ElapsedMilliseconds < 1500, $"XmlException was retried; elapsed {timer.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void XmlDocumentLoadRetry_MissingFile_RetriesAsIfTransient_ThenThrows()
    {
        using TempWorkspace workspace = new();
        string missing = Path.Combine(workspace.Root, "does-not-exist.xml");
        var document = new XmlDocument();
        var timer = Stopwatch.StartNew();

        Assert.ThrowsAny<IOException>(() => XmlDocumentLoadRetry.Load(document, missing));

        timer.Stop();
        Assert.True(
            timer.ElapsedMilliseconds >= 2500,
            $"Expected retry backoff for FileNotFound/IOException; elapsed {timer.ElapsedMilliseconds}ms");
        Assert.True(
            timer.ElapsedMilliseconds < 20000,
            $"Retry loop hung; elapsed {timer.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void XmlDocumentLoadRetry_RejectsDtd()
    {
        using TempWorkspace workspace = new();
        string path = Path.Combine(workspace.Root, "dtd.xml");
        File.WriteAllText(path, """
            <?xml version="1.0"?>
            <!DOCTYPE root [
              <!ELEMENT root ANY>
            ]>
            <root/>
            """);
        var document = new XmlDocument();
        Assert.Throws<XmlException>(() => XmlDocumentLoadRetry.Load(document, path));
    }

    [Fact]
    public void GeneratedXmlFootprint_SaveOutsideRoot_RecordsIssueAndThrows()
    {
        using TempWorkspace workspace = new();
        PipelineContext context = CreateContext(workspace.Root);
        string outside = Path.Combine(workspace.Root, "escape.xml");
        var document = new XmlDocument();
        document.LoadXml("<Asset/>");

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
            () => GeneratedXmlFootprint.Save(document, outside, context));

        Assert.Contains("Refusing to write outside", thrown.Message, StringComparison.Ordinal);
        Assert.True(context.Issues.HasErrors);
        Assert.Equal(PipelineIssueCodes.UnexpectedFileProcessingError, Assert.Single(context.Issues.GetAll()).Code);
        Assert.False(File.Exists(outside));
    }

    [Fact]
    public void Cancellation_CancelFile_TripsToken_AndThrowIfCancellationRequested()
    {
        using TempWorkspace workspace = new();
        PipelineContext context = CreateContext(workspace.Root);

        using IDisposable watch = PipelineCancellation.Attach(context);
        Assert.False(context.CancellationToken.IsCancellationRequested);

        PipelineCancellation.RequestCancel(context.AnnoAssetsRoot);
        Assert.True(SpinWait.SpinUntil(() => context.CancellationToken.IsCancellationRequested, TimeSpan.FromSeconds(3)));
        Assert.Throws<OperationCanceledException>(context.ThrowIfCancellationRequested);
    }

    [Fact]
    public void FormatXml_PreCancelledToken_ThrowsOperationCanceled_LeavesExistingOutput()
    {
        using TempWorkspace workspace = new();
        PipelineContext context = CreateContext(workspace.Root);
        string leftover = Path.Combine(context.AssetOutputDir, "partial - [Leftover].xml");
        File.WriteAllText(leftover, MinimalAssetXml("7", "Leftover"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        context.CancellationToken = cts.Token;

        Assert.ThrowsAny<OperationCanceledException>(() =>
            FormattingService.FormatXml(
                context,
                [leftover],
                PropertyScanner.Empty,
                gameType: "anno1800",
                allowMove: false,
                skipInitialMessage: true));

        Assert.True(File.Exists(leftover));
        Assert.Contains("Leftover", File.ReadAllText(leftover), StringComparison.Ordinal);
    }

    [Fact]
    public void RestartPrepare_ClearsPartialAssetOutput_FromCancelledRun()
    {
        using TempWorkspace workspace = new();
        PipelineContext context = CreateContext(workspace.Root);
        string leftover = Path.Combine(context.AssetOutputDir, "partial.xml");
        File.WriteAllText(leftover, "<half-written");

        Assert.True(OutputDirectoryManager.TryPrepareAssetOutputDirectory(context));
        Assert.False(File.Exists(leftover));
        Assert.True(Directory.Exists(context.AssetOutputDir));
    }

    [Fact]
    public void OutputRunLock_DisposeDoesNotThrow_AndReleasesFile()
    {
        using TempWorkspace workspace = new();
        PipelineContext context = CreateContext(workspace.Root);
        IDisposable? held = OutputRunLock.TryAcquire(context);
        Assert.NotNull(held);
        string lockPath = Path.Combine(context.AnnoAssetsRoot, OutputRunLock.LockFileName);
        Assert.True(File.Exists(lockPath));
        held!.Dispose();
        Assert.False(File.Exists(lockPath));
        Assert.Null(Record.Exception(held.Dispose));
    }

    [Fact]
    public void CleanupHelpers_DoNotThrow_WhenScratchFileMissingOrLogsDirCreatable()
    {
        using TempWorkspace workspace = new();
        PipelineContext context = CreateContext(workspace.Root);
        Assert.Null(Record.Exception(() => OutputDirectoryManager.TryDeleteFixerScratchFile(context.BaseOutputDir)));
        Assert.Null(Record.Exception(() => OutputDirectoryManager.TryDeleteFixerScratchFile(Path.Combine(workspace.Root, "missing-dir"))));
        string? logs = OutputDirectoryManager.TryPrepareLogsDirectory(context.AnnoAssetsRoot);
        Assert.False(string.IsNullOrWhiteSpace(logs));
        Assert.True(Directory.Exists(logs));
    }

    [Fact]
    public void PipelineCancellation_ClearCancelFile_DoesNotThrowWhenMissing()
    {
        using TempWorkspace workspace = new();
        PipelineContext context = CreateContext(workspace.Root);
        Assert.Null(Record.Exception(() => PipelineCancellation.ClearCancelFile(context)));
    }

    [Fact]
    public void RdaExtractionFailedCode_ExistsButPhase1CatchDoesNotRecordIt()
    {
        Assert.Equal("RdaExtractionFailed", PipelineIssueCodes.RdaExtractionFailed);
        Assert.Equal("PipelineFatalError", PipelineIssueCodes.PipelineFatalError);
        using TempWorkspace workspace = new();
        PipelineContext context = CreateContext(workspace.Root);
        Directory.CreateDirectory(context.AssetRoot);

        _ = Phase1SourceAcquisition.ExecutePhase1RdaExtraction(context, "anno1800", context.GameOutputRoot);

        Assert.DoesNotContain(
            context.Issues.GetAll(),
            issue => issue.Code is PipelineIssueCodes.RdaExtractionFailed or PipelineIssueCodes.PipelineFatalError);
    }

    private static PipelineContext CreateContext(string root)
    {
        string annoAssets = Path.Combine(root, "AnnoAssets");
        string gameOutput = Path.Combine(annoAssets, "Anno1800");
        string assetOut = Path.Combine(gameOutput, "output_xml_anno1800");
        string sourceXml = Path.Combine(gameOutput, "source_xml_anno1800");
        Directory.CreateDirectory(assetOut);
        Directory.CreateDirectory(sourceXml);

        var context = new PipelineContext
        {
            AssetRoot = Path.Combine(root, "Anno 1800"),
            BaseOutputDir = root,
            AnnoAssetsRoot = annoAssets,
            GameOutputRoot = gameOutput,
            AssetOutputDir = assetOut,
            SourceXmlFolder = sourceXml + Path.DirectorySeparatorChar,
            AssetLanguage = "texts_english.xml",
            DetectedGameType = "anno1800",
            NoDefaultProperties = true,
        };
        context.ProgressReporter.Initialize(context);
        return context;
    }

    private static string MinimalAssetXml(string guid, string name) =>
        $"""
        <Asset>
          <Template>Product</Template>
          <Values>
            <Standard>
              <GUID>{guid}</GUID>
              <Name>{name}</Name>
            </Standard>
          </Values>
        </Asset>
        """;

    private sealed class TempWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "as-gauntlet-" + Guid.NewGuid().ToString("N"));

        public TempWorkspace() => Directory.CreateDirectory(Root);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
