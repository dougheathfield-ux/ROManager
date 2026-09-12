using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RomRebuilderUI.Models;
using RomRebuilderUI.Services;

namespace RomRebuilderUI.ViewModels
{
    public partial class ConsoleWorkflowViewModel : ObservableObject
    {
        private readonly RomRebuilderService _rebuilderService = new();
        private const string SettingsFileName = "console_settings.json";

        // --- Console Configuration Properties ---
        [ObservableProperty] private string _consoleDatPath = "console.xml";
        [ObservableProperty] private ObservableCollection<string> _consoleSourceDirs = new() { "./console_roms" };
        [ObservableProperty] private string? _selectedConsoleSourceDir;
        [ObservableProperty] private string _consoleOutputDir = "./console_rebuilt";

        // --- 1G1R & Region Filtering Properties ---
        [ObservableProperty] private bool _is1G1REnabled = true;
        [ObservableProperty] private bool _isRegionSortingEnabled = false;
        [ObservableProperty] private string _regionPriorities = "USA, World, Europe, Japan, Other";

        // --- Compression Options ---
        [ObservableProperty] private string _compressionFormat = ".zip";
        public List<string> CompressionFormatOptions { get; } = new() { ".zip", ".7z" };

        // --- Progress & Status ---
        [ObservableProperty] private bool _isWorking = false;
        [ObservableProperty] private bool _isIndeterminate = false;
        [ObservableProperty] private int _progressValue;
        [ObservableProperty] private int _progressMaximum = 100;
        [ObservableProperty] private string _statusMessage = "Ready";

        // --- Real-time Statistics ---
        [ObservableProperty] private int _totalScanned;
        [ObservableProperty] private int _matchedCount;
        [ObservableProperty] private int _unknownCount;
        [ObservableProperty] private int _filteredDuplicatesCount;
        [ObservableProperty] private int _missingCount;
        [ObservableProperty] private List<string> _unknownFilesList = new();

        // --- Dashboard & DataGrid Filtering Properties ---
        private List<MachineAuditItem> _allAuditItems = new();
        public List<string> StatusFilterOptions { get; } = new() { "All", "Complete", "Incomplete", "Missing", "Unknown" };

        [ObservableProperty]
        private string _selectedStatusFilter = "All";

        partial void OnSelectedStatusFilterChanged(string value)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (_allAuditItems == null) return;

            var filtered = SelectedStatusFilter == "All"
                ? _allAuditItems
                : _allAuditItems.Where(x => x.Status.Equals(SelectedStatusFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            ConsoleAuditItems = new ObservableCollection<MachineAuditItem>(filtered);
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CollectionHealthText))]
        private double _collectionHealthPercentage;

        public string CollectionHealthText => $"{CollectionHealthPercentage:F1}% Complete";

        [ObservableProperty] private ObservableCollection<MachineAuditItem> _consoleAuditItems = new();
        [ObservableProperty] private MachineAuditItem? _selectedAuditItem;
        [ObservableProperty] private bool _hasAuditItems;

        public ConsoleWorkflowViewModel()
        {
            LoadSettings();
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new ConsoleSettingsDto
                {
                    DatPath = ConsoleDatPath,
                    SourceDirs = ConsoleSourceDirs.ToList(),
                    OutputDir = ConsoleOutputDir,
                    Is1G1REnabled = Is1G1REnabled,
                    IsRegionSortingEnabled = IsRegionSortingEnabled,
                    RegionPriorities = RegionPriorities,
                    CompressionFormat = CompressionFormat
                };
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFileName, json);
            }
            catch (Exception) { }
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFileName))
                {
                    string json = File.ReadAllText(SettingsFileName);
                    var settings = JsonSerializer.Deserialize<ConsoleSettingsDto>(json);
                    if (settings != null)
                    {
                        if (!string.IsNullOrEmpty(settings.DatPath)) ConsoleDatPath = settings.DatPath;
                        if (!string.IsNullOrEmpty(settings.OutputDir)) ConsoleOutputDir = settings.OutputDir;
                        if (settings.SourceDirs != null && settings.SourceDirs.Count > 0)
                        {
                            ConsoleSourceDirs = new ObservableCollection<string>(settings.SourceDirs);
                        }
                        Is1G1REnabled = settings.Is1G1REnabled;
                        IsRegionSortingEnabled = settings.IsRegionSortingEnabled;
                        if (!string.IsNullOrEmpty(settings.RegionPriorities)) RegionPriorities = settings.RegionPriorities;
                        if (!string.IsNullOrEmpty(settings.CompressionFormat)) CompressionFormat = settings.CompressionFormat;
                    }
                }
            }
            catch (Exception) { }
        }

        partial void OnConsoleDatPathChanged(string value)
        {
            SaveSettings();
            ClearScanResults();
        }

        private void ClearScanResults()
        {
            _allAuditItems.Clear();
            ConsoleAuditItems.Clear();
            SelectedAuditItem = null;
            HasAuditItems = false;

            TotalScanned = 0;
            MatchedCount = 0;
            UnknownCount = 0;
            FilteredDuplicatesCount = 0;
            MissingCount = 0;
            UnknownFilesList = new List<string>();
            CollectionHealthPercentage = 0;

            StatusMessage = "New DAT file selected. Ready to scan.";
        }

        partial void OnConsoleOutputDirChanged(string value) => SaveSettings();
        partial void OnIs1G1REnabledChanged(bool value) => SaveSettings();
        partial void OnIsRegionSortingEnabledChanged(bool value) => SaveSettings();
        partial void OnRegionPrioritiesChanged(string value) => SaveSettings();
        partial void OnCompressionFormatChanged(string value) => SaveSettings();

        [RelayCommand]
        public void AddConsoleSource(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !ConsoleSourceDirs.Contains(path))
            {
                ConsoleSourceDirs.Add(path);
                SaveSettings();
            }
        }

        [RelayCommand]
        public void RemoveConsoleSource(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                ConsoleSourceDirs.Remove(path);
                SaveSettings();
            }
        }

        [RelayCommand]
        private async Task AuditConsoleAsync()
        {
            if (IsWorking) return;
            
            IsWorking = true;
            IsIndeterminate = true;
            ProgressValue = 0;
            ProgressMaximum = 100;
            StatusMessage = "Scanning source ROM files...";

            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Task.Delay(150);

            bool popupDismissed = false;

            var progress = new Progress<RebuildProgressReport>(report =>
            {
                IsIndeterminate = false;
                if (report.Total > 0) ProgressMaximum = report.Total;
                if (report.Current > 0) ProgressValue = report.Current;
                if (!string.IsNullOrEmpty(report.CurrentMessage))
                {
                    StatusMessage = report.CurrentMessage;

                    if (!popupDismissed && (report.CurrentMessage.Contains("audit", StringComparison.OrdinalIgnoreCase) || 
                                           report.CurrentMessage.Contains("analyzing", StringComparison.OrdinalIgnoreCase) ||
                                           report.CurrentMessage.Contains("comparing", StringComparison.OrdinalIgnoreCase)))
                    {
                        popupDismissed = true;
                        IsWorking = false;
                    }
                }
            });

            var auditItems = new List<MachineAuditItem>();
            AuditSummary? summary = null;

            try
            {
                await Task.Run(async () =>
                {
                    summary = await _rebuilderService.RunConsoleAudit(
                        ConsoleSourceDirs, 
                        ConsoleDatPath, 
                        Is1G1REnabled,
                        RegionPriorities,
                        progress, 
                        item => 
                        {
                            lock (auditItems)
                            {
                                auditItems.Add(item);
                            }
                        }
                    );
                });

                if (!popupDismissed)
                {
                    IsWorking = false;
                }

                Dispatcher.UIThread.Post(() =>
                {
                    var combinedItems = new List<MachineAuditItem>(auditItems);

                    // Add unknown files as audit items for the main grid
                    if (summary?.UnknownFiles != null)
                    {
                        foreach (var unknownPath in summary.UnknownFiles)
                        {
                            combinedItems.Add(new MachineAuditItem
                            {
                                Name = unknownPath,
                                Description = "Unneeded Source File",
                                Status = "Unknown",
                                Region = "N/A",
                                RomCountSummary = "Unneeded"
                            });
                        }
                    }

                    _allAuditItems = combinedItems;
                    ApplyFilter();

                    HasAuditItems = _allAuditItems.Count > 0;
                    TotalScanned = auditItems.Count;
                    MatchedCount = auditItems.Count(i => i.Status == "Complete");
                    FilteredDuplicatesCount = auditItems.Count(i => i.Status == "Incomplete");
                    MissingCount = auditItems.Count(i => i.Status == "Missing");
                    
                    UnknownCount = summary?.UnknownFiles?.Count ?? 0;
                    UnknownFilesList = summary?.UnknownFiles ?? new List<string>();

                    if (TotalScanned > 0)
                    {
                        CollectionHealthPercentage = ((double)MatchedCount / TotalScanned) * 100.0;
                    }

                    StatusMessage = "Console Audit Completed Successfully.";
                });
            }
            catch (Exception ex)
            {
                IsWorking = false;
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsWorking = false;
                IsIndeterminate = false;
            }
        }

        [RelayCommand]
        private async Task RebuildConsoleAsync()
        {
            if (IsWorking) return;
            IsWorking = true;
            IsIndeterminate = true;
            ProgressValue = 0;
            ProgressMaximum = 100;
            StatusMessage = "Loading DAT and initializing rebuild...";

            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Task.Delay(150);

            var progress = new Progress<RebuildProgressReport>(report =>
            {
                IsIndeterminate = false;
                if (report.Total > 0) ProgressMaximum = report.Total;
                if (report.Current > 0) ProgressValue = report.Current;
                if (!string.IsNullOrEmpty(report.CurrentMessage)) StatusMessage = report.CurrentMessage;
            });

            try
            {
                await Task.Run(async () =>
                {
                    await _rebuilderService.RunConsoleRebuild(
                        ConsoleSourceDirs, 
                        ConsoleOutputDir, 
                        ConsoleDatPath, 
                        Is1G1REnabled,
                        RegionPriorities,
                        IsRegionSortingEnabled,
                        CompressionFormat.Equals(".7z", StringComparison.OrdinalIgnoreCase) ? OutputFormat.SevenZ : OutputFormat.Zip, 
                        progress
                    );
                });

                StatusMessage = "Console Rebuild Completed Successfully.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsWorking = false;
                IsIndeterminate = false;
            }
        }

        [RelayCommand]
        private void ExportMissingList()
        {
            try
            {
                string exportPath = Path.Combine(Environment.CurrentDirectory, "missing_roms_report.txt");
                var lines = new List<string>
                {
                    $"=== ROM Rebuilder Missing List Report ===",
                    $"Generated: {DateTime.Now}",
                    string.Empty
                };

                foreach (var item in ConsoleAuditItems.Where(i => i.Status == "Incomplete" || i.Status == "Missing"))
                {
                    lines.Add($"Game: {item.Name} [{item.Status}] (Region: {item.Region})");
                    foreach (var file in item.MissingFiles)
                    {
                        lines.Add($"   - {file}");
                    }
                    lines.Add(string.Empty);
                }

                File.WriteAllLines(exportPath, lines);
                StatusMessage = $"Missing list exported successfully to: {exportPath}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export Error: {ex.Message}";
            }
        }
    }

    internal class ConsoleSettingsDto
    {
        public string DatPath { get; set; } = string.Empty;
        public List<string> SourceDirs { get; set; } = new();
        public string OutputDir { get; set; } = string.Empty;
        public bool Is1G1REnabled { get; set; }
        public bool IsRegionSortingEnabled { get; set; }
        public string RegionPriorities { get; set; } = string.Empty;
        public string CompressionFormat { get; set; } = string.Empty;
    }
}
