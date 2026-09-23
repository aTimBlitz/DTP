using CommunityToolkit.Mvvm.ComponentModel;
using DTP.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DTP.ViewModels;

public partial class PartGroup : ObservableObject
{
    [ObservableProperty] private string _partName = string.Empty;

    public ObservableCollection<string> SerialNumbers { get; } = new();
}

public partial class OrderPackage : ObservableObject
{
    [ObservableProperty] private string _orderName = string.Empty;

    public ObservableCollection<GlobalFieldValue> LocalMetadata { get; } = new();
    public ObservableCollection<PartGroup> AssignedParts { get; } = new();
}

public partial class Step3ReviewViewModel : ViewModelBase
{
    public ObservableCollection<PartGroup> LeftoverPool { get; } = new();
    public ObservableCollection<OrderPackage> Orders { get; } = new();

    public void GenerateOrders(
        ObservableCollection<RawDataRow> rawData,
        ObservableCollection<PartDistributionItem> rules,
        int targetOrderCount,
        IEnumerable<string> customLocalKeys,
        string userTag,
        string targetDate,
        IEnumerable<GlobalFieldValue> globalValues)
    {
        Orders.Clear();
        LeftoverPool.Clear();

        var pool = new Dictionary<string, Queue<string>>();
        foreach (var row in rawData.Where(x => !string.IsNullOrWhiteSpace(x.Teilebezeichnung) && !string.IsNullOrWhiteSpace(x.Seriennummer)))
        {
            if (!pool.ContainsKey(row.Teilebezeichnung)) pool[row.Teilebezeichnung] = new Queue<string>();
            pool[row.Teilebezeichnung].Enqueue(row.Seriennummer);
        }

        for (int i = 1; i <= targetOrderCount; i++)
        {
            var newOrder = new OrderPackage { OrderName = $"Auftrag #{i}" };

            newOrder.LocalMetadata.Add(new GlobalFieldValue { Key = "Nutzer", Value = userTag });
            newOrder.LocalMetadata.Add(new GlobalFieldValue { Key = "Datum", Value = targetDate });

            foreach (var g in globalValues)
            {
                newOrder.LocalMetadata.Add(new GlobalFieldValue { Key = g.Key, Value = g.Value });
            }

            newOrder.LocalMetadata.Add(new GlobalFieldValue { Key = "Auftrag", Value = "" });
            foreach (var key in customLocalKeys)
            {
                newOrder.LocalMetadata.Add(new GlobalFieldValue { Key = key, Value = "" });
            }

            foreach (var rule in rules)
            {
                if (rule.ItemsPerPackage > 0 && pool.ContainsKey(rule.Teilebezeichnung))
                {
                    var partGroup = new PartGroup { PartName = rule.Teilebezeichnung };
                    for (int j = 0; j < rule.ItemsPerPackage; j++)
                    {
                        if (pool[rule.Teilebezeichnung].Count > 0) partGroup.SerialNumbers.Add(pool[rule.Teilebezeichnung].Dequeue());
                        else partGroup.SerialNumbers.Add("FEHLT!");
                    }
                    newOrder.AssignedParts.Add(partGroup);
                }
            }
            Orders.Add(newOrder);
        }

        foreach (var kvp in pool)
        {
            if (kvp.Value.Count > 0)
            {
                var leftoverGroup = new PartGroup { PartName = kvp.Key };
                while (kvp.Value.Count > 0) leftoverGroup.SerialNumbers.Add(kvp.Value.Dequeue());
                LeftoverPool.Add(leftoverGroup);
            }
        }

        SystemLogger.Add($"Verteilung abgeschlossen: {Orders.Count} Aufträge generiert. Übrige SNs im Pool abgelegt.");
    }
}