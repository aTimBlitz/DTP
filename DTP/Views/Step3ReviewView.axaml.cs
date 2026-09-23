using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace DTP.Views;

public partial class Step3ReviewView : UserControl
{
    private Point _dragStartPoint;
    private bool _isDragging;
    private ListBox? _sourceListBox;
    private string? _draggedItem;
    private PointerPressedEventArgs? _lastPointerPressedEventArgs;

    public Step3ReviewView()
    {
        InitializeComponent();
    }

    private void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            _dragStartPoint = e.GetPosition(control);
            _lastPointerPressedEventArgs = e;
            _isDragging = false;

            _sourceListBox = control.FindAncestorOfType<ListBox>();
        }
    }

    private async void OnItemPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is Control control && e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            var point = e.GetPosition(control);

            if (!_isDragging && _lastPointerPressedEventArgs != null && _sourceListBox != null &&
               (Math.Abs(point.X - _dragStartPoint.X) > 3 || Math.Abs(point.Y - _dragStartPoint.Y) > 3))
            {
                _isDragging = true;

                if (control.DataContext is string itemData)
                {
                    _draggedItem = itemData;

                    var dragData = new DataTransfer();
                    dragData.Add(DataTransferItem.CreateText(itemData));

                    await DragDrop.DoDragDropAsync(_lastPointerPressedEventArgs, dragData, DragDropEffects.Move);

                    _isDragging = false;
                    _sourceListBox = null;
                    _draggedItem = null;
                }
            }
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer != null && e.DataTransfer.Formats.Contains(DataFormat.Text))
        {
            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
        }
        else e.DragEffects = DragDropEffects.None;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (sender is ListBox targetListBox && e.DataTransfer != null && e.DataTransfer.Formats.Contains(DataFormat.Text) && _draggedItem != null && _sourceListBox != null)
        {
            var targetList = targetListBox.ItemsSource as ObservableCollection<string>;
            var sourceList = _sourceListBox.ItemsSource as ObservableCollection<string>;

            if (targetList != null && sourceList != null)
            {
                sourceList.Remove(_draggedItem);

                int dropIndex = targetList.Count;

                if (e.Source is Control visual)
                {
                    var targetListBoxItem = visual.FindAncestorOfType<ListBoxItem>();
                    if (targetListBoxItem != null && targetListBoxItem.DataContext is string targetData)
                    {
                        dropIndex = targetList.IndexOf(targetData);
                        if (dropIndex < 0) dropIndex = targetList.Count;
                    }
                }

                targetList.Insert(dropIndex, _draggedItem);
                e.Handled = true;
            }
        }
    }
}