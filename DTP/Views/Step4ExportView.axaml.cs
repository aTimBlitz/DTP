using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using DTP.ViewModels;
using System.Linq;

namespace DTP.Views;

public partial class Step4ExportView : UserControl
{
    public Step4ExportView()
    {
        InitializeComponent();

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnFileDropped);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer != null && e.DataTransfer.Formats.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnFileDropped(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer != null && e.DataTransfer.Formats.Contains(DataFormat.File))
        {
            var items = e.DataTransfer.TryGetFiles();
            if (items == null) return;

            var validFiles = items
                .OfType<IStorageFile>()
                .Where(f =>
                    f.Name.ToLower().EndsWith(".docx") ||
                    f.Name.ToLower().EndsWith(".xlsx"))
                .ToList();

            if (validFiles.Any() && DataContext is Step4ExportViewModel vm)
            {
                e.Handled = true;
                vm.ProcessTemplateFiles(validFiles);
            }
        }
    }
}