using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RomRebuilderUI.ViewModels;

namespace RomRebuilderUI.Views
{
    public partial class MameWorkflowView : UserControl
    {
        public MameWorkflowView()
        {
            InitializeComponent();
        }

        private async void OnBrowseDatClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MameWorkflowViewModel vm)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return;

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select MAME DAT / XML File",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("DAT & XML Files") { Patterns = new[] { "*.dat", "*.xml" } },
                        new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                    }
                });

                if (files.Count > 0)
                {
                    vm.SetDatPath(files[0].Path.LocalPath);
                }
            }
        }

        private async void OnAddSourceClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MameWorkflowViewModel vm)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return;

                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Primary Source ROM Directory",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    vm.AddSource(folders[0].Path.LocalPath);
                }
            }
        }

        private async void OnAddAddPathClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MameWorkflowViewModel vm)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return;

                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Auxiliary Add-Path Directory",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    vm.AddAddPath(folders[0].Path.LocalPath);
                }
            }
        }

        private async void OnBrowseOutputClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MameWorkflowViewModel vm)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return;

                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Output Destination Directory",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    vm.SetOutputDirectory(folders[0].Path.LocalPath);
                }
            }
        }
    }
}
