namespace AssetSplitterUI.ViewModels;

public partial class MainWindowViewModel
{
    /// <summary>Glowing step hints — first launch only, hidden during a run.</summary>
    public bool ShowFirstRunSteps =>
        _settingsCoordinator.IsFirstRunExperience && !IsProcessing;

    public bool FirstRunStep1Active => ShowFirstRunSteps && !IsGamePathRecognized;
    public bool FirstRunStep1Complete => ShowFirstRunSteps && IsGamePathRecognized;

    public bool FirstRunStep2Locked => ShowFirstRunSteps && !IsGamePathRecognized;
    public bool FirstRunStep2Active =>
        ShowFirstRunSteps && IsGamePathRecognized && string.IsNullOrWhiteSpace(OutputPath);
    public bool FirstRunStep2Complete =>
        ShowFirstRunSteps && !string.IsNullOrWhiteSpace(OutputPath);

    public bool FirstRunStep3Locked =>
        ShowFirstRunSteps && (!IsGamePathRecognized || string.IsNullOrWhiteSpace(OutputPath));
    public bool FirstRunStep3Active => ShowFirstRunSteps && CanStartExtraction;

    private void NotifyFirstRunStepHintsChanged()
    {
        OnPropertyChanged(nameof(ShowFirstRunSteps));
        OnPropertyChanged(nameof(FirstRunStep1Active));
        OnPropertyChanged(nameof(FirstRunStep1Complete));
        OnPropertyChanged(nameof(FirstRunStep2Locked));
        OnPropertyChanged(nameof(FirstRunStep2Active));
        OnPropertyChanged(nameof(FirstRunStep2Complete));
        OnPropertyChanged(nameof(FirstRunStep3Locked));
        OnPropertyChanged(nameof(FirstRunStep3Active));
    }
}
