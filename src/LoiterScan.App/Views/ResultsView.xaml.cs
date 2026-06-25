using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LoiterScan.App.ViewModels;
using LoiterScan.Data.Entities;

namespace LoiterScan.App.Views;

public partial class ResultsView : UserControl
{
    public ResultsView() => InitializeComponent();

    // Left-click on a row → navigate to event detail
    private void ResultsGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<DataGridRow>((DependencyObject)e.OriginalSource) is null)
            return;

        if (DataContext is ResultsViewModel vm)
            vm.NavigateToSelected();
    }

    // Right-click → select the row under the cursor (so the context menu has the right
    // CommandParameter) but do NOT trigger navigation.
    private void ResultsGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = FindVisualParent<DataGridRow>((DependencyObject)e.OriginalSource);
        if (row?.DataContext is LoiteringEventEntity entity)
            ResultsGrid.SelectedItem = entity;

        // Suppress the DataGrid's own selection-change routing so OnSelectedEventChanged
        // is not called a second time via the bubbling path.
        e.Handled = true;
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T t) return t;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }
}
