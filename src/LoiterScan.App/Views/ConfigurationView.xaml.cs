using System.Windows;
using System.Windows.Controls;
using LoiterScan.App.ViewModels;

namespace LoiterScan.App.Views;

public partial class ConfigurationView : UserControl
{
    public ConfigurationView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ConfigurationViewModel oldVm)
            oldVm.PasswordSync -= SyncPasswordBox;

        if (e.NewValue is ConfigurationViewModel newVm)
            newVm.PasswordSync += SyncPasswordBox;
    }

    // PasswordBox.Password is not a DependencyProperty so it can't be data-bound.
    // The view owns the raw string; it pushes it to the VM on change.
    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ConfigurationViewModel vm)
            vm.Password = ((PasswordBox)sender).Password;
    }

    // Called when the VM changes the password programmatically (e.g. source switch).
    private void SyncPasswordBox(string password) => PasswordBox.Password = password;
}
