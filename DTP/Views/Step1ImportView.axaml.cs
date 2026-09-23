using Avalonia.Controls;
using System.ComponentModel;
using DTP.ViewModels;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using System.Linq;

namespace DTP.Views;

public partial class Step1ImportView : UserControl
{
    public Step1ImportView()
    {
        InitializeComponent();

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnFileDropped);

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    private async void OnFileDropped(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer != null && e.DataTransfer.Formats.Contains(DataFormat.File))
        {
            var files = e.DataTransfer.TryGetFiles();

            var validFile = files?.FirstOrDefault(f => f.Name.ToLower().EndsWith(".csv") || f.Name.ToLower().EndsWith(".txt"));

            if (validFile is IStorageFile storageFile && DataContext is Step1ImportViewModel vm)
            {
                e.Handled = true;
                await vm.ProcessFileAsync(storageFile);
            }
        }
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is Step1ImportViewModel vm)
        {
            vm.PropertyChanged += Vm_PropertyChanged;
            RebuildColumns(vm);
        }
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Step1ImportViewModel.ColumnNames) && sender is Step1ImportViewModel vm)
        {
            RebuildColumns(vm);
        }
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

    private void RebuildColumns(Step1ImportViewModel vm)
    {
        if (PreviewGrid == null || vm.ColumnNames == null) return;

        while (PreviewGrid.Columns.Count > 1)
        {
            PreviewGrid.Columns.RemoveAt(1);
        }

        for (int i = 0; i < vm.ColumnNames.Count; i++)
        {
            PreviewGrid.Columns.Add(new DataGridTextColumn
            {
                Header = vm.ColumnNames[i],
                Binding = new Avalonia.Data.Binding($"[{i}]"),
                IsReadOnly = true,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
        }
    }
}