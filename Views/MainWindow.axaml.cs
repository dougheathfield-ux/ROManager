using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RomRebuilderUI.ViewModels;
using RomRebuilderUI.Models;
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
                    vm.LogEntries.CollectionChanged += LogEntries_CollectionChanged;
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

        private void LogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && sender is INotifyCollectionChanged collection)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    // Find the ListBox inside the Live Log tab and scroll to the latest entry
                    // (Assuming you named the ListBox x:Name="LogListBox" in your XAML, or we can scroll via standard items count if available)
                    var logListBox = this.FindControl<ListBox>("LogListBox");
                    if (logListBox != null && logListBox.ItemCount > 0)
                    {
                        logListBox.ScrollIntoView(logListBox.ItemCount - 1);
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
