using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DTP.ViewModels;

public partial class GlobalFieldValue : ObservableObject
{
    [ObservableProperty] private string _key = string.Empty;
    [ObservableProperty] private string _value = string.Empty;
}

public partial class PartDistributionItem : ObservableObject
{
    [ObservableProperty] private string _teilebezeichnung = string.Empty;
    [ObservableProperty] private int _gesamtStueck;
    [ObservableProperty] private int _itemsPerPackage = 1;
}

public partial class Step2SetupViewModel : ViewModelBase
{
    [ObservableProperty] private string _userTag = "ttest";
    [ObservableProperty] private string _targetDate = System.DateTime.Now.ToString("dd.MM.yyyy");
    [ObservableProperty] private int _targetPackageCount = 1;

    public ObservableCollection<PartDistributionItem> PartDistributions { get; } = new();
    public ObservableCollection<GlobalFieldValue> CustomGlobalValues { get; } = new();

    public void InitializeFromRawData(ObservableCollection<RawDataRow> rawData)
    {
        PartDistributions.Clear();
        var grouped = rawData
            .GroupBy(x => x.Teilebezeichnung)
            .Select(g => new PartDistributionItem
            {
                Teilebezeichnung = string.IsNullOrWhiteSpace(g.Key) ? "Unbekannt" : g.Key,
                GesamtStueck = g.Count(),
                ItemsPerPackage = 1
            });

        foreach (var item in grouped) PartDistributions.Add(item);
    }

    public void InitializeGlobalFields(IEnumerable<string> globalKeys)
    {
        var existingValues = CustomGlobalValues.ToDictionary(x => x.Key, x => x.Value);

        CustomGlobalValues.Clear();
        foreach (var key in globalKeys)
        {
            CustomGlobalValues.Add(new GlobalFieldValue
            {
                Key = key,
                Value = existingValues.TryGetValue(key, out var savedValue) ? savedValue : ""
            });
        }
    }
}