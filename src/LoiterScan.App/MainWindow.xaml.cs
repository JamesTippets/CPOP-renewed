using System.Windows;
using LoiterScan.App.ViewModels;

namespace LoiterScan.App;

public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
