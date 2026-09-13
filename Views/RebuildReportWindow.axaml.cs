using Avalonia.Controls;
using Avalonia.Interactivity;
using RomRebuilderUI.Models;

namespace RomRebuilderUI.Views
{
    public partial class RebuildReportWindow : Window
    {
        public RebuildReportWindow()
        {
            InitializeComponent();
        }

        public RebuildReportWindow(RebuildReportModel model) : this()
        {
            DataContext = model;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
