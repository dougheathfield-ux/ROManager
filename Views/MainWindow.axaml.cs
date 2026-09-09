using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RomRebuilderUI.ViewModels;
using RomRebuilderUI.Models;
using RomRebuilderUI.Views;
using System.Collections.Specialized;

namespace RomRebuilderUI.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            DataContextChanged += (sender, args) =>
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.FilteredMachines.CollectionChanged += FilteredMachines_CollectionChanged;
                    
                    // Hook up the report window popup callback
                    vm.ShowReportCallback = async (reportModel) =>
                    {
                        var reportWindow = new RebuildReportWindow(reportModel);
                        await reportWindow.ShowDialog(this);
                    };
                }
            };
        }

        private void FilteredMachines_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null && e.NewItems.Count > 0)
            {
                var addedItem = e.NewItems[e.NewItems.Count - 1];
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (addedItem != null)
                    {
                        AuditDataGrid.ScrollIntoView(addedItem, null);
                    }
                });
            }
        }

        // --- MAME Browser Handlers ---
        private async void OnBrowseMameDatClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select MAME DAT File",
                    AllowMultiple = false,
                    FileTypeFilter = new[] { new FilePickerFileType("DAT / XML Files") { Patterns = new[] { "*.xml", "*.dat" } } }
                });

                if (files.Count > 0)
                {
                    vm.MameDatPath = files[0].Path.LocalPath;
                }
            }
        }

        private async void OnAddMameFolderClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select MAME Source ROMs Directory",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    vm.AddMameSource(folders[0].Path.LocalPath);
                }
            }
        }

        private async void OnBrowseMameOutputClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select MAME Output Directory",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    vm.MameOutputDir = folders[0].Path.LocalPath;
                }
            }
        }

        // --- Console Browser Handlers ---
        private async void OnBrowseConsoleDatClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Console DAT File",
                    AllowMultiple = false,
                    FileTypeFilter = new[] { new FilePickerFileType("DAT / XML Files") { Patterns = new[] { "*.xml", "*.dat" } } }
                });

                if (files.Count > 0)
                {
                    vm.ConsoleDatPath = files[0].Path.LocalPath;
                }
            }
        }

        private async void OnAddConsoleFolderClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Console Source ROMs Directory",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    vm.AddConsoleSource(folders[0].Path.LocalPath);
                }
            }
        }

        private async void OnBrowseConsoleOutputClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Console Output Directory",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    vm.ConsoleOutputDir = folders[0].Path.LocalPath;
                }
            }
        }
    }
}
