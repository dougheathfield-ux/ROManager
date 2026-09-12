using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RomRebuilderUI.ViewModels;

namespace RomRebuilderUI.Views
{
    public partial class ConsoleWorkflowView : UserControl
    {
        public ConsoleWorkflowView()
        {
            InitializeComponent();
        }

        private async void OnBrowseDatClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not ConsoleWorkflowViewModel vm) return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Console XML/DAT File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("DAT / XML Files") { Patterns = new[] { "*.xml", "*.dat" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            });

            if (files.Count > 0)
            {
                vm.ConsoleDatPath = files[0].Path.LocalPath;
            }
        }

        private async void OnAddSourceClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not ConsoleWorkflowViewModel vm) return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select ROM Source Directory",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                vm.AddConsoleSource(folders[0].Path.LocalPath);
            }
        }

        private async void OnBrowseOutputClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not ConsoleWorkflowViewModel vm) return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Output Directory",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                vm.ConsoleOutputDir = folders[0].Path.LocalPath;
            }
        }
    }
}
