using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DTP.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DTP.ViewModels;

public partial class RawDataRow : ObservableObject
{
    [ObservableProperty] private string _teilebezeichnung = string.Empty;
    [ObservableProperty] private string _seriennummer = string.Empty;
}

public partial class PreviewRow : ObservableObject
{
    [ObservableProperty] private bool _isIncluded = true;
    public string[] RawData { get; }

    public PreviewRow(string[] raw)
    {
        RawData = raw;
    }

    public string this[int index] => index < RawData.Length ? RawData[index] : "";
}

public partial class Step1ImportViewModel : ViewModelBase
{
    [ObservableProperty] private RawDataRow? _selectedRow;
    [ObservableProperty] private bool _isPreviewOverlayVisible;
    [ObservableProperty] private ObservableCollection<PreviewRow> _previewData = new();
    [ObservableProperty] private List<string> _columnNames = new();
    [ObservableProperty] private int _selectedBezIndex = 0;
    [ObservableProperty] private int _selectedSnIndex = 1;
    private List<string[]> _rawRecordsCache = new();

    public ObservableCollection<RawDataRow> RawData { get; } = new();

    [RelayCommand]
    private void RemoveDuplicates()
    {
        var distinctItems = RawData
            .GroupBy(x => x.Seriennummer)
            .Select(g => g.First())
            .ToList();

        RawData.Clear();
        foreach (var item in distinctItems)
        {
            RawData.Add(item);
        }
    }

    [RelayCommand]
    private void SortData()
    {
        // Sortiert primär nach Name, sekundär nach Seriennummer
        var sorted = RawData
            .OrderBy(x => x.Teilebezeichnung)
            .ThenBy(x => x.Seriennummer)
            .ToList();

        RawData.Clear();
        foreach (var item in sorted)
        {
            RawData.Add(item);
        }

        SystemLogger.Add("Rohdaten wurden alphabetisch und numerisch sortiert.");
    }

    [RelayCommand]
    private void ClearData()
    {
        RawData.Clear();
    }

    [RelayCommand]
    private void AddRow()
    {
        RawData.Add(new RawDataRow { Teilebezeichnung = "Neues Teil", Seriennummer = "Neue SN" });
    }

    [RelayCommand]
    private void DeleteRow()
    {
        if (SelectedRow != null)
        {
            RawData.Remove(SelectedRow);
        }
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desktop?.MainWindow == null) return;

        var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Daten auswählen (*.csv, *.txt)",
            AllowMultiple = false
        });

        if (files.Count > 0)
        {
            await ProcessFileAsync(files[0]);
        }
    }

    public async Task ProcessFileAsync(IStorageFile file)
    {
        _rawRecordsCache.Clear();
        int maxCols = 0;

        await using (var stream = await file.OpenReadAsync())
        using (var reader = new StreamReader(stream, System.Text.Encoding.Default))
        {
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] columns;

                if (line.Contains(';')) columns = line.Split(';');
                else if (line.Contains('\t')) columns = line.Split('\t');
                else if (line.Contains("\"\" \"\"")) columns = line.Trim().Trim('"').Split(new[] { "\"\" \"\"" }, StringSplitOptions.None);
                else if (line.Contains("\" \"")) columns = line.Trim().Trim('"').Split(new[] { "\" \"" }, StringSplitOptions.None);
                else columns = line.Split(' ');

                for (int i = 0; i < columns.Length; i++) columns[i] = columns[i].Trim();
                if (columns.Length > maxCols) maxCols = columns.Length;

                if (columns.Length > 0) _rawRecordsCache.Add(columns);
            }
        }

        if (_rawRecordsCache.Count == 0) return;

        var colNames = new List<string>();
        for (int i = 0; i < maxCols; i++) colNames.Add($"Spalte {i + 1}");
        ColumnNames = colNames;

        var firstRow = _rawRecordsCache[0];
        var nonEmptyIndices = firstRow.Select((val, idx) => new { val, idx })
                                      .Where(x => !string.IsNullOrWhiteSpace(x.val))
                                      .Select(x => x.idx).ToList();

        SelectedBezIndex = nonEmptyIndices.Count > 0 ? nonEmptyIndices[0] : 0;
        SelectedSnIndex = nonEmptyIndices.Count > 1 ? nonEmptyIndices[1] : 1;

        PreviewData.Clear();
        foreach (var record in _rawRecordsCache)
        {
            PreviewData.Add(new PreviewRow(record));
        }

        IsPreviewOverlayVisible = true;
    }

    [RelayCommand]
    private void CancelPreview()
    {
        IsPreviewOverlayVisible = false;
    }

    [RelayCommand]
    private void ConfirmImport()
    {
        int initialCount = RawData.Count;

        foreach (var row in PreviewData)
        {
            if (!row.IsIncluded) continue;

            string bez = row.RawData.Length > SelectedBezIndex ? row.RawData[SelectedBezIndex] : "";
            string sn = row.RawData.Length > SelectedSnIndex ? row.RawData[SelectedSnIndex] : "";

            if (!string.IsNullOrWhiteSpace(bez) || !string.IsNullOrWhiteSpace(sn))
            {
                RawData.Add(new RawDataRow { Teilebezeichnung = bez, Seriennummer = sn });
            }
        }

        int added = RawData.Count - initialCount;
        SystemLogger.Add($"{added} Datensätze (Zeilen) erfolgreich importiert.");
        IsPreviewOverlayVisible = false;
    }
}