using AssetSplitterUI.Services;

namespace AssetSplitterUI.Tests;

public sealed class PipelineProgressTrackerTests
{
    [Fact]
    public void Feed_TracksProgressBeforePlanLine()
    {
        var tracker = new PipelineProgressTracker();

        tracker.Feed("[ 50.0%] [     1/     2] - Extracting from RDA: data0.rda");

        Assert.True(tracker.OverallPercent > 0);
        Assert.InRange(tracker.OverallPercent, 49.0, 51.0);
    }

    [Fact]
    public void Feed_UsesPlanFloorAfterPlanAndAdditivePlan()
    {
        var tracker = new PipelineProgressTracker();

        tracker.Feed("[PLAN] 2");
        tracker.Feed("[ 100.0%] [     2/     2] - Extracting from RDA: data1.rda");
        double afterPhase1 = tracker.OverallPercent;
        Assert.InRange(afterPhase1, 99.0, 100.0);

        tracker.Feed("[PLAN+] 8");
        // PLAN+ expands the budget: bar must re-anchor (not stay pinned at 100%).
        Assert.InRange(tracker.OverallPercent, 19.0, 21.0);

        tracker.Feed("[ 25.0%] [     2/     8] - Indexing: assets");
        // RDA(2) + Indexing(2) = 4 of floor 10 → ~40%.
        Assert.InRange(tracker.OverallPercent, 39.0, 41.0);
    }

    [Fact]
    public void Feed_Phase1Complete_ThenExtracting_DoesNotStayPinnedAt100()
    {
        // Anno 117 / 1800 symptom: Phase 1 fills the bar, then Extracting n/total with n < total
        // must re-anchor overall % (counts stay step-local; bar is overall).
        var tracker = new PipelineProgressTracker();

        tracker.Feed("[PLAN] 2");
        tracker.Feed("[100.0%] [     2/     2] - Extracting from RDA: complete");
        Assert.InRange(tracker.OverallPercent, 99.0, 100.0);

        tracker.Feed("[PLAN+] 96102");
        Assert.True(tracker.OverallPercent < 5.0);

        tracker.Feed("[ 98.0%] [ 94208/ 96102] - Extracting: 4811 - TransportAirship_Escort_Pool (TextPool)");
        // completed ≈ 2 + 94208, budget ≈ 2 + 96102 → ~98% of extract-inclusive budget, not 100% from Phase 1.
        Assert.InRange(tracker.OverallPercent, 97.0, 99.0);
        Assert.True(tracker.OverallPercent < 100.0);
    }

    [Fact]
    public void Feed_Phase1Complete_LanguageProgress_ReanchorsWithoutPlanPlus()
    {
        // Tracker must drop when budget grows from a new operation total (language 10k),
        // even if the backend forgot [PLAN+] — belt-and-suspenders for both games.
        var tracker = new PipelineProgressTracker();

        tracker.Feed("[PLAN] 24");
        tracker.Feed("[100.0%] [    24/    24] - Extracting from RDA: complete");
        Assert.InRange(tracker.OverallPercent, 99.0, 100.0);

        tracker.Feed("[ 50.0%] [  5000/ 10000] - reading language file");
        Assert.InRange(tracker.OverallPercent, 49.0, 55.0);
    }

    [Fact]
    public void Feed_LegacyFormattingLine_DoesNotDoubleCountAgainstProcessing()
    {
        var tracker = new PipelineProgressTracker();

        tracker.Feed("[PLAN] 200");
        tracker.Feed("[100.0%] [   100/   100] - Processing: 1 - A (T)");
        Assert.InRange(tracker.OverallPercent, 49.0, 51.0);

        tracker.Feed("[100.0%] [   100/   100] - Formatting...");

        // Mapped onto the same Processing bucket — must stay ~50%, not jump to 100%.
        Assert.InRange(tracker.OverallPercent, 49.0, 51.0);
    }

    [Fact]
    public void Feed_FormattingReferences_KeepsMainProcessingProgress()
    {
        var tracker = new PipelineProgressTracker();

        tracker.Feed("[PLAN] 200");
        tracker.Feed("[100.0%] [   150/   150] - Processing: 1 - A (T)");
        double afterProcessing = tracker.OverallPercent;
        Assert.InRange(afterProcessing, 74.0, 76.0);

        tracker.Feed("[PLAN+] 50");
        // Budget grows to 250; completed still 150 → ~60%.
        Assert.InRange(tracker.OverallPercent, 59.0, 61.0);

        tracker.Feed("[  2.0%] [     1/    50] - Formatting references: 2 - B (T)");

        // 151/250 ≈ 60.4%; within-phase ticks stay monotonic via Math.Max.
        Assert.True(tracker.OverallPercent >= 60.0);
        Assert.True(tracker.OverallPercent <= 100.0);
    }

    [Fact]
    public void Reset_ClearsProgress()
    {
        var tracker = new PipelineProgressTracker();
        tracker.Feed("[PLAN] 10");
        tracker.Feed("[ 10.0%] [     1/    10] - Extracting: sample");

        tracker.Reset();

        Assert.Equal(0, tracker.OverallPercent);
    }
}
