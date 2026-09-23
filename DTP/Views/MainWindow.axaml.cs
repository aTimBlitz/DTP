using Avalonia.Controls;
using DTP.ViewModels;

namespace DTP.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.SaveConfig();
            }

            base.OnClosing(e);
        }
    }
}