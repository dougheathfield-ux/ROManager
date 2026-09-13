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

        public async void OnBrowseDatClick(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is not Window window) return;

            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select MAME DAT / XML File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("DAT / XML Files") { Patterns = new[] { "*.xml", "*.dat" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            });

            if (files.Count > 0 && DataContext is MameWorkflowViewModel vm)
            {
                vm.SetDatPath(files[0].Path.LocalPath);
            }
        }

        public async void OnAddSourceClick(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is not Window window) return;

            var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Source ROM Directory",
                AllowMultiple = false
            });

            if (folders.Count > 0 && DataContext is MameWorkflowViewModel vm)
            {
                vm.AddSource(folders[0].Path.LocalPath);
            }
        }

        public async void OnBrowseOutputClick(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is not Window window) return;

            var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Output Directory",
                AllowMultiple = false
            });

            if (folders.Count > 0 && DataContext is MameWorkflowViewModel vm)
            {
                vm.SetOutputDir(folders[0].Path.LocalPath);
            }
        }
    }
}
