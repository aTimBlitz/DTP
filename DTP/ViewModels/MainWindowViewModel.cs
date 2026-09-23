using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DTP.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DTP.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private int _currentStep = 0;
    [ObservableProperty] private string _stepTitle = "Schritt 1: Daten laden";
    [ObservableProperty] private ViewModelBase _currentPage;

    private readonly ViewModelBase _step1ImportVM;
    private readonly ViewModelBase _step2SetupVM;
    private readonly ViewModelBase _step3ReviewVM;
    private readonly ViewModelBase _step4ExportVM;

    [ObservableProperty] private bool _isMetadataOverlayVisible = false;
    [ObservableProperty] private bool _isInfoOverlayVisible = false;
    [ObservableProperty] private bool _isLogOverlayVisible = false;

    public ObservableCollection<string> LogHistory => SystemLogger.Messages;
    public ObservableCollection<string> CustomGlobals { get; } = new();
    public ObservableCollection<string> CustomLocals { get; } = new();

    [ObservableProperty] private string? _selectedGlobalItem;
    [ObservableProperty] private string? _selectedLocalItem;
    [ObservableProperty] private string _newGlobalName = "";
    [ObservableProperty] private string _newLocalName = "";
    [ObservableProperty] private bool _saveMetadataPermanently = true;
    [ObservableProperty] private bool _isHelpOverlayVisible = false;
    [ObservableProperty] private bool _canGoBack = false;
    [ObservableProperty] private bool _canGoNext = true;

    [RelayCommand]
    private void OpenHelp()
    {
        IsHelpOverlayVisible = true;
    }

    [RelayCommand]
    private void CloseHelpOverlay()
    {
        IsHelpOverlayVisible = false;
    }

    public MainWindowViewModel()
    {
        SystemLogger.Add("System gestartet. DTP Version 1.0 (rc1) geladen.");

        _step1ImportVM = new Step1ImportViewModel();
        _step2SetupVM = new Step2SetupViewModel();
        _step3ReviewVM = new Step3ReviewViewModel();
        _step4ExportVM = new Step4ExportViewModel();

        if (_step4ExportVM is Step4ExportViewModel step4)
        {
            step4.SaveConfigCallback = SaveConfig;
        }

        CurrentPage = _step1ImportVM;
        UpdateNavigationState();
        LoadConfig();
    }

    [RelayCommand]
    private void GoNext()
    {
        if (CurrentStep < 3)
        {
            if (CurrentStep == 0 && _step1ImportVM is Step1ImportViewModel step1 && _step2SetupVM is Step2SetupViewModel step2)
            {
                step2.InitializeFromRawData(step1.RawData);
                step2.InitializeGlobalFields(CustomGlobals);
            }
            else if (CurrentStep == 1 && _step1ImportVM is Step1ImportViewModel s1 && _step2SetupVM is Step2SetupViewModel s2 && _step3ReviewVM is Step3ReviewViewModel s3)
            {
                s3.GenerateOrders(s1.RawData, s2.PartDistributions, s2.TargetPackageCount, CustomLocals, s2.UserTag, s2.TargetDate, s2.CustomGlobalValues);
            }
            else if (CurrentStep == 2 && _step3ReviewVM is Step3ReviewViewModel step3 && _step4ExportVM is Step4ExportViewModel step4)
            {
                step4.OrdersToExport = step3.Orders;
                step4.UpdatePlaceholderStatus();
            }

            CurrentStep++;
            UpdateNavigationState();
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        if (CurrentStep > 0)
        {
            if (CurrentStep == 3) SaveConfig();

            CurrentStep--;
            UpdateNavigationState();
        }
    }

    private void UpdateNavigationState()
    {
        CanGoBack = CurrentStep > 0;
        CanGoNext = CurrentStep < 3;

        switch (CurrentStep)
        {
            case 0: StepTitle = "Schritt 1: Rohdaten laden & prüfen"; CurrentPage = _step1ImportVM; break;
            case 1: StepTitle = "Schritt 2: Globale Daten & Set-Bildung"; CurrentPage = _step2SetupVM; break;
            case 2: StepTitle = "Schritt 3: Auftragsdaten & Verteilung"; CurrentPage = _step3ReviewVM; break;
            case 3: StepTitle = "Schritt 4: Export"; CurrentPage = _step4ExportVM; break;
        }
    }

    [RelayCommand]
    private void ExitApp()
    {
        SaveConfig();

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    [RelayCommand]
    private void OpenLog()
    {
        IsLogOverlayVisible = true;
    }

    [RelayCommand]
    private void CloseLogOverlay()
    {
        IsLogOverlayVisible = false;
    }

    [RelayCommand]
    private void ClearLog()
    {
        SystemLogger.Messages.Clear();
        SystemLogger.Add("Logbuch geleert.");
    }

    [RelayCommand]
    private void OpenMetadata()
    {
        IsMetadataOverlayVisible = true;
    }

    public void LoadConfig()
    {
        if (File.Exists("dtp_config.json"))
        {
            try
            {
                var json = File.ReadAllText("dtp_config.json");
                var config = JsonDocument.Parse(json).RootElement;

                CustomGlobals.Clear();
                if (config.TryGetProperty("CustomGlobals", out var globals))
                {
                    foreach (var item in globals.EnumerateArray()) CustomGlobals.Add(item.GetString() ?? "");
                }

                CustomLocals.Clear();
                if (config.TryGetProperty("CustomLocals", out var locals))
                {
                    foreach (var item in locals.EnumerateArray()) CustomLocals.Add(item.GetString() ?? "");
                }

                if (_step4ExportVM is Step4ExportViewModel step4)
                {
                    if (config.TryGetProperty("LastExportPath", out var path))
                        step4.ExportFolderPath = path.GetString() ?? "";

                    if (config.TryGetProperty("GenerateSingleFiles", out var single))
                        step4.GenerateSingleFiles = single.GetBoolean();

                    if (config.TryGetProperty("GenerateCombinedFile", out var combined))
                        step4.GenerateCombinedFile = combined.GetBoolean();
                }
            }
            catch {  }
        }
    }

    [RelayCommand]
    public void SaveConfig()
    {
        var step4 = _step4ExportVM as Step4ExportViewModel;

        var configObj = new
        {
            CustomGlobals = CustomGlobals.ToArray(),
            CustomLocals = CustomLocals.ToArray(),

            LastExportPath = step4?.ExportFolderPath ?? "",
            GenerateSingleFiles = step4?.GenerateSingleFiles ?? true,
            GenerateCombinedFile = step4?.GenerateCombinedFile ?? false
        };

        var json = JsonSerializer.Serialize(configObj, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText("dtp_config.json", json);
    }

    [RelayCommand]
    private void AddGlobalField()
    {
        if (!string.IsNullOrWhiteSpace(NewGlobalName) && !CustomGlobals.Contains(NewGlobalName))
        {
            CustomGlobals.Add(NewGlobalName.Trim());
            NewGlobalName = "";
        }
    }

    [RelayCommand]
    private void RemoveGlobalField()
    {
        if (SelectedGlobalItem != null) CustomGlobals.Remove(SelectedGlobalItem);
    }

    [RelayCommand]
    private void AddLocalField()
    {
        if (!string.IsNullOrWhiteSpace(NewLocalName) && !CustomLocals.Contains(NewLocalName))
        {
            CustomLocals.Add(NewLocalName.Trim());
            NewLocalName = "";
        }
    }

    [RelayCommand]
    private void RemoveLocalField()
    {
        if (SelectedLocalItem != null) CustomLocals.Remove(SelectedLocalItem);
    }

    [RelayCommand]
    private void CloseMetadataOverlay()
    {
        IsMetadataOverlayVisible = false;
    }

    [RelayCommand]
    private void SaveAndCloseMetadata()
    {
        if (SaveMetadataPermanently)
        {
            SaveConfig();
        }
        IsMetadataOverlayVisible = false;
    }

    [RelayCommand]
    private void OpenInfo()
    {
        IsInfoOverlayVisible = true;
    }

    [RelayCommand]
    private void CloseInfoOverlay()
    {
        IsInfoOverlayVisible = false;
    }
}