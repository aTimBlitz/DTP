using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DTP.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DTP.ViewModels;

public enum PlaceholderState
{
    Missing,
    Empty,
    Filled
}

public partial class PlaceholderItem : ObservableObject
{
    public string Name { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackgroundColor))]
    private PlaceholderState _state = PlaceholderState.Missing;

    public string BackgroundColor => State switch
    {
        PlaceholderState.Filled => "#4CAF50",
        PlaceholderState.Empty => "#FF9800",
        _ => "#808080"
    };
}

public partial class TemplateFileItem : ObservableObject
{
    [ObservableProperty] private string _filePath = string.Empty;
    [ObservableProperty] private string _fileName = string.Empty;
    [ObservableProperty] private bool _isGlobal = false;

    public ObservableCollection<PlaceholderItem> FoundPlaceholders { get; } = new();
}

public partial class Step4ExportViewModel : ViewModelBase
{
    [ObservableProperty] private string _exportFolderPath = string.Empty;
    [ObservableProperty] private bool _generateSingleFiles = true;
    [ObservableProperty] private bool _generateCombinedFile = false;

    public ObservableCollection<TemplateFileItem> SelectedTemplates { get; } = new();
    public ObservableCollection<OrderPackage> OrdersToExport { get; set; } = new();

    [ObservableProperty] private bool _isExporting = false;
    [ObservableProperty] private bool _isExportFinished = false;
    [ObservableProperty] private int _exportProgress = 0;
    [ObservableProperty] private string _exportStatusText = "Warte auf Start...";

    public System.Action? SaveConfigCallback { get; set; }

    public Step4ExportViewModel()
    {
    }

    [RelayCommand]
    private async Task SelectExportFolderAsync()
    {
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desktop?.MainWindow == null) return;

        var folder = await desktop.MainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Ziel-Ordner für den Export auswählen"
        });

        if (folder.Count > 0)
        {
            ExportFolderPath = folder[0].Path.LocalPath;
            SystemLogger.Add($"Export-Zielordner gesetzt auf: {ExportFolderPath}");
        }
    }

    [RelayCommand]
    private async Task AddTemplatesAsync()
    {
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desktop?.MainWindow == null) return;

        var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Word / Excel Vorlagen auswählen",
            AllowMultiple = true,
            FileTypeFilter = new[] { new FilePickerFileType("Office Dateien") { Patterns = new[] { "*.docx", "*.xlsx" } } }
        });

        if (files.Count > 0)
        {
            ProcessTemplateFiles(files);
        }
    }

    public void ProcessTemplateFiles(IEnumerable<IStorageFile> files)
    {
        foreach (var file in files)
        {
            if (SelectedTemplates.Any(x => x.FilePath == file.Path.LocalPath)) continue;

            var item = new TemplateFileItem
            {
                FilePath = file.Path.LocalPath,
                FileName = file.Name
            };

            ExtractPlaceholders(item);
            SelectedTemplates.Add(item);

            SystemLogger.Add($"Vorlage hinzugefügt: {file.Name} ({item.FoundPlaceholders.Count} Platzhalter gefunden)");
        }

        UpdatePlaceholderStatus();
    }

    public void UpdatePlaceholderStatus()
    {
        var normalKeys = new Dictionary<string, bool>();
        var globalKeys = new Dictionary<string, bool>();

        globalKeys["Gesamt_Auftraege"] = OrdersToExport.Count > 0;
        globalKeys["Datum"] = true;

        var typeCounts = new Dictionary<string, int>();

        int orderIndex = 1;
        foreach (var order in OrdersToExport)
        {
            foreach (var meta in order.LocalMetadata)
            {
                bool hasContent = !string.IsNullOrWhiteSpace(meta.Value?.ToString());

                if (!normalKeys.ContainsKey(meta.Key)) normalKeys[meta.Key] = hasContent;
                else normalKeys[meta.Key] |= hasContent;

                globalKeys[$"Auftrag_{orderIndex}_{meta.Key}"] = hasContent;
            }

            string rawOrderId = order.OrderName;
            var auftragMeta = order.LocalMetadata.FirstOrDefault(m => m.Key == "Auftragsnummer");
            if (auftragMeta != null && auftragMeta.Value?.ToString() is string val && !string.IsNullOrWhiteSpace(val))
            {
                rawOrderId = val;
            }
            globalKeys[$"Auftrag_{orderIndex}"] = !string.IsNullOrWhiteSpace(rawOrderId);

            foreach (var part in order.AssignedParts)
            {
                string safeKey = part.PartName.Replace(" ", "_").Replace("-", "_").Replace(".", "_");
                bool hasFirstContent = part.SerialNumbers.Count > 0 && !string.IsNullOrWhiteSpace(part.SerialNumbers[0]);

                if (!normalKeys.ContainsKey(safeKey)) normalKeys[safeKey] = hasFirstContent;
                else normalKeys[safeKey] |= hasFirstContent;

                if (!typeCounts.ContainsKey(safeKey)) typeCounts[safeKey] = 0;
                typeCounts[safeKey] += part.SerialNumbers.Count;

                for (int i = 0; i < part.SerialNumbers.Count; i++)
                {
                    string keyIter = $"{safeKey}_{i + 1}";
                    bool hasIterContent = !string.IsNullOrWhiteSpace(part.SerialNumbers[i]);

                    if (!normalKeys.ContainsKey(keyIter)) normalKeys[keyIter] = hasIterContent;
                    else normalKeys[keyIter] |= hasIterContent;

                    globalKeys[$"{safeKey}_{i + 1}_{orderIndex}"] = hasIterContent;
                }
            }
            orderIndex++;
        }

        foreach (var kvp in typeCounts)
        {
            globalKeys[$"Summe_{kvp.Key}"] = kvp.Value > 0;
        }

        var existingGlobal = globalKeys.Keys.ToList();
        foreach (var k in existingGlobal) globalKeys[$" {k} "] = globalKeys[k];

        var existingNormal = normalKeys.Keys.ToList();
        foreach (var k in existingNormal) normalKeys[$" {k} "] = normalKeys[k];

        foreach (var template in SelectedTemplates)
        {
            var targetDictionary = template.IsGlobal ? globalKeys : normalKeys;

            foreach (var ph in template.FoundPlaceholders)
            {
                if (targetDictionary.TryGetValue(ph.Name, out bool hasContent))
                {
                    ph.State = hasContent ? PlaceholderState.Filled : PlaceholderState.Empty;
                }
                else
                {
                    ph.State = PlaceholderState.Missing;
                }
            }
        }
    }

    [RelayCommand]
    private void RefreshPlaceholderStatus()
    {
        UpdatePlaceholderStatus();
    }

    [RelayCommand]
    private void RemoveTemplate(TemplateFileItem item)
    {
        if (item != null) SelectedTemplates.Remove(item);
    }

    private void ExtractPlaceholders(TemplateFileItem item)
    {
        try
        {
            using var fileStream = new System.IO.FileStream(item.FilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
            using var archive = new System.IO.Compression.ZipArchive(fileStream, System.IO.Compression.ZipArchiveMode.Read);

            var targetEntry = archive.Entries.FirstOrDefault(e => e.FullName == "word/document.xml" || e.FullName == "xl/sharedStrings.xml");
            if (targetEntry != null)
            {
                using var stream = targetEntry.Open();
                using var reader = new System.IO.StreamReader(stream);
                var xml = reader.ReadToEnd();

                var plainText = System.Text.RegularExpressions.Regex.Replace(xml, "<.*?>", string.Empty);
                var matches = System.Text.RegularExpressions.Regex.Matches(plainText, @"\{\{(.*?)\}\}");

                var uniquePlaceholders = matches.Select(m => m.Groups[1].Value.Trim()).Distinct();
                foreach (var ph in uniquePlaceholders)
                {
                    item.FoundPlaceholders.Add(new PlaceholderItem { Name = ph });
                }
            }
        }
        catch { }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (!string.IsNullOrWhiteSpace(ExportFolderPath) && Directory.Exists(ExportFolderPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ExportFolderPath,
                UseShellExecute = true,
                Verb = "open"
            });
        }
    }

    [RelayCommand]
    private async Task StartExportAsync()
    {
        if (SelectedTemplates.Count == 0 || string.IsNullOrWhiteSpace(ExportFolderPath) || OrdersToExport.Count == 0) return;

        SaveConfigCallback?.Invoke();

        IsExporting = true;
        IsExportFinished = false;
        ExportProgress = 0;

        bool actualGenerateSingleFiles = GenerateSingleFiles || !GenerateCombinedFile;

        SystemLogger.Add($"Export gestartet. Einzeldokumente: {actualGenerateSingleFiles}, Combined: {GenerateCombinedFile}");

        await Task.Run(() =>
        {
            try
            {
                var normalTemplates = SelectedTemplates.Where(t => !t.IsGlobal).ToList();
                var globalTemplates = SelectedTemplates.Where(t => t.IsGlobal).ToList();

                int totalTasks = (OrdersToExport.Count * normalTemplates.Count) + globalTemplates.Count;
                int currentTask = 0;

                var generatedFilesMap = new Dictionary<TemplateFileItem, List<string>>();
                foreach (var t in SelectedTemplates) generatedFilesMap[t] = new List<string>();

                string tempDir = Path.Combine(Path.GetTempPath(), "DTP_TempFiles_" + Guid.NewGuid());
                Directory.CreateDirectory(tempDir);

                var safeTemplatePaths = new Dictionary<TemplateFileItem, string>();
                foreach (var template in SelectedTemplates)
                {
                    string tempPath = Path.Combine(tempDir, "TMP_" + template.FileName);
                    File.Copy(template.FilePath, tempPath, true);
                    safeTemplatePaths[template] = tempPath;
                }

                var allRequestedPlaceholders = new HashSet<string>(SelectedTemplates.SelectMany(t => t.FoundPlaceholders.Select(p => p.Name)));
                var globalContext = new Dictionary<string, object>();

                globalContext["Gesamt_Auftraege"] = OrdersToExport.Count;
                globalContext["Datum"] = DateTime.Now.ToString("dd.MM.yyyy");

                var typeCounts = new Dictionary<string, int>();

                int orderIndexForGlobal = 1;
                foreach (var order in OrdersToExport)
                {
                    string rawOrderId = order.OrderName;
                    var auftragMeta = order.LocalMetadata.FirstOrDefault(m => m.Key == "Auftrag");
                    if (auftragMeta != null && auftragMeta.Value?.ToString() is string val && !string.IsNullOrWhiteSpace(val))
                    {
                        rawOrderId = val;
                    }
                    globalContext[$"Auftrag_{orderIndexForGlobal}"] = rawOrderId;

                    foreach (var meta in order.LocalMetadata)
                    {
                        globalContext[$"Auftrag_{orderIndexForGlobal}_{meta.Key}"] = meta.Value;
                    }

                    foreach (var part in order.AssignedParts)
                    {
                        string safeKey = part.PartName.Replace(" ", "_").Replace("-", "_").Replace(".", "_");

                        if (!typeCounts.ContainsKey(safeKey)) typeCounts[safeKey] = 0;
                        typeCounts[safeKey] += part.SerialNumbers.Count;

                        for (int i = 0; i < part.SerialNumbers.Count; i++)
                        {
                            globalContext[$"{safeKey}_{i + 1}_{orderIndexForGlobal}"] = part.SerialNumbers[i];
                        }
                    }
                    orderIndexForGlobal++;
                }

                foreach (var kvp in typeCounts)
                {
                    globalContext[$"Summe_{kvp.Key}"] = kvp.Value;
                }

                var existingKeys = globalContext.Keys.ToList();
                foreach (var k in existingKeys)
                {
                    globalContext[$" {k} "] = globalContext[k];
                }

                foreach (var order in OrdersToExport)
                {
                    var context = new Dictionary<string, object>();
                    foreach (var meta in order.LocalMetadata) context[meta.Key] = meta.Value;

                    foreach (var part in order.AssignedParts)
                    {
                        string safeKey = part.PartName.Replace(" ", "_").Replace("-", "_").Replace(".", "_");
                        context[safeKey] = part.SerialNumbers.FirstOrDefault() ?? "";

                        for (int i = 0; i < part.SerialNumbers.Count; i++)
                        {
                            context[$"{safeKey}_{i + 1}"] = part.SerialNumbers[i];
                        }
                    }

                    foreach (var ph in allRequestedPlaceholders)
                    {
                        if (!context.ContainsKey(ph)) context[ph] = "";
                    }

                    string rawOrderId = order.OrderName;
                    if (context.TryGetValue("Auftragsnummer", out object? metaObj) && metaObj?.ToString() is string val && !string.IsNullOrWhiteSpace(val))
                    {
                        rawOrderId = val;
                    }

                    string safeOrderId = string.Join("_", rawOrderId.Split(Path.GetInvalidFileNameChars()));

                    foreach (var template in normalTemplates)
                    {
                        string rawTName = Path.GetFileNameWithoutExtension(template.FileName);
                        string safeTName = string.Join("_", rawTName.Split(Path.GetInvalidFileNameChars()));
                        string ext = Path.GetExtension(template.FileName).ToLower();

                        Dispatcher.UIThread.Post(() => ExportStatusText = $"Generiere: {safeTName} ({safeOrderId})...");

                        bool isExcel = (ext == ".xlsx");
                        string saveDir = (actualGenerateSingleFiles || isExcel) ? ExportFolderPath : tempDir;

                        string dateString = DateTime.Now.ToString("yyyyMMdd");
                        string baseName = $"{safeTName}_Einzel_{dateString}_Auftrag-{safeOrderId}";
                        string savePath = Path.Combine(saveDir, baseName + ext);

                        int counter = 1;
                        while (File.Exists(savePath) && IsFileLocked(savePath))
                        {
                            savePath = Path.Combine(saveDir, $"{baseName}_{counter}{ext}");
                            counter++;
                        }

                        if (ext == ".docx")
                        {
                            MiniSoftware.MiniWord.SaveAsByTemplate(savePath, safeTemplatePaths[template], context);
                            generatedFilesMap[template].Add(savePath);
                        }
                        else if (ext == ".xlsx")
                        {
                            MiniExcelLibs.MiniExcel.SaveAsByTemplate(savePath, safeTemplatePaths[template], context);
                            generatedFilesMap[template].Add(savePath);
                        }

                        currentTask++;
                        Dispatcher.UIThread.Post(() => ExportProgress = (int)(((double)currentTask / totalTasks) * 100));
                        SystemLogger.Add($"Generiert: {Path.GetFileName(savePath)}");
                    }
                }

                foreach (var template in globalTemplates)
                {
                    foreach (var ph in allRequestedPlaceholders)
                    {
                        if (!globalContext.ContainsKey(ph)) globalContext[ph] = "";
                    }

                    string rawTName = Path.GetFileNameWithoutExtension(template.FileName);
                    string safeTName = string.Join("_", rawTName.Split(Path.GetInvalidFileNameChars()));
                    string ext = Path.GetExtension(template.FileName).ToLower();

                    Dispatcher.UIThread.Post(() => ExportStatusText = $"Generiere globales Dokument: {safeTName}...");

                    bool isExcel = (ext == ".xlsx");
                    string saveDir = (actualGenerateSingleFiles || isExcel) ? ExportFolderPath : tempDir;

                    string dateString = DateTime.Now.ToString("yyyyMMdd");
                    string baseName = $"{safeTName}_GLOBAL_{dateString}";
                    string savePath = Path.Combine(saveDir, baseName + ext);

                    int counter = 1;
                    while (File.Exists(savePath) && IsFileLocked(savePath))
                    {
                        savePath = Path.Combine(saveDir, $"{baseName}_{counter}{ext}");
                        counter++;
                    }

                    if (ext == ".docx")
                    {
                        MiniSoftware.MiniWord.SaveAsByTemplate(savePath, safeTemplatePaths[template], globalContext);
                        generatedFilesMap[template].Add(savePath);
                    }
                    else if (ext == ".xlsx")
                    {
                        MiniExcelLibs.MiniExcel.SaveAsByTemplate(savePath, safeTemplatePaths[template], globalContext);
                        generatedFilesMap[template].Add(savePath);
                    }

                    currentTask++;
                    Dispatcher.UIThread.Post(() => ExportProgress = (int)(((double)currentTask / totalTasks) * 100));
                    SystemLogger.Add($"Global generiert: {Path.GetFileName(savePath)}");
                }

                if (GenerateCombinedFile)
                {
                    Dispatcher.UIThread.Post(() => ExportStatusText = "Starte Word-Schnittstelle für den Merge...");
                    Type? wordType = Type.GetTypeFromProgID("Word.Application");
                    if (wordType != null)
                    {
                        dynamic wordApp = Activator.CreateInstance(wordType)!;
                        wordApp.Visible = false;

                        try
                        {
                            foreach (var kvp in generatedFilesMap)
                            {
                                var template = kvp.Key;
                                var files = kvp.Value;

                                if (files.Count > 0 && template.FileName.ToLower().EndsWith(".docx"))
                                {
                                    string rawTName = Path.GetFileNameWithoutExtension(template.FileName);
                                    string safeTName = string.Join("_", rawTName.Split(Path.GetInvalidFileNameChars()));

                                    Dispatcher.UIThread.Post(() => ExportStatusText = $"Führe Dokumente für {safeTName} zusammen...");
                                    SystemLogger.Add($"Führe Dokumente für {safeTName} zusammen...");

                                    dynamic masterDoc = wordApp.Documents.Open(files[0]);
                                    dynamic selection = wordApp.Selection;

                                    for (int i = 1; i < files.Count; i++)
                                    {
                                        selection.EndKey(6);
                                        selection.InsertBreak(7);
                                        selection.InsertFile(files[i]);
                                    }

                                    string masterDate = DateTime.Now.ToString("yyyyMMdd");
                                    string scopeTag = template.IsGlobal ? "GLOBAL_Gesamt" : "Gesamt";
                                    string baseMasterName = $"{safeTName}_{scopeTag}_{masterDate}";

                                    string masterPath = Path.Combine(ExportFolderPath, baseMasterName + ".docx");
                                    int masterCounter = 1;
                                    while (File.Exists(masterPath) && IsFileLocked(masterPath))
                                    {
                                        masterPath = Path.Combine(ExportFolderPath, $"{baseMasterName}_{masterCounter}.docx");
                                        masterCounter++;
                                    }

                                    masterDoc.SaveAs2(masterPath);
                                    masterDoc.Close();

                                    SystemLogger.Add($"Masterdokument erstellt: {Path.GetFileName(masterPath)}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            SystemLogger.Add("Fehler beim Word Merge: " + ex.Message);
                        }
                        finally
                        {
                            wordApp.Quit();
                            System.Runtime.InteropServices.Marshal.ReleaseComObject(wordApp);
                        }
                    }
                }

                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }

                SystemLogger.Add("Export vollständig und fehlerfrei abgeschlossen!");

                Dispatcher.UIThread.Post(() =>
                {
                    ExportStatusText = "Export erfolgreich abgeschlossen!";
                    IsExportFinished = true;
                });
            }
            catch (Exception ex)
            {
                SystemLogger.Add($"Schwerer Fehler beim Export Prozess: {ex.Message}");
                Dispatcher.UIThread.Post(() => ExportStatusText = "FEHLER beim Export!");
            }
        });
    }

    [RelayCommand]
    private void ResetExport()
    {
        IsExporting = false;
        IsExportFinished = false;
        ExportProgress = 0;
    }

    private bool IsFileLocked(string filePath)
    {
        try
        {
            using (var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.None))
            {
                stream.Close();
            }
        }
        catch (System.IO.IOException)
        {
            return true;
        }
        return false;
    }
}