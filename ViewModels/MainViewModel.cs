using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RomRebuilderUI.Services;
using RomRebuilderUI.Models;

namespace RomRebuilderUI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly RomRebuilderService _rebuilderService = new();
        private readonly Stopwatch _stopwatch = new();
        private List<MachineAuditItem> _allAuditItems = new();
        private const string LogFileName = "rom_rebuilder_debug.log";

        // --- MAME Properties ---
        [ObservableProperty] private string _mameDatPath = "mame.xml";
        [ObservableProperty] private ObservableCollection<string> _mameSourceDirs = new() { "./mame_roms" };
        [ObservableProperty] private string? _selectedMameSourceDir;
        [ObservableProperty] private string _mameOutputDir = "./mame_rebuilt";
        [ObservableProperty] private RebuildMode _selectedRebuildMode = RebuildMode.NonMerged;
        [ObservableProperty] private OutputFormat _mameOutputFormat = OutputFormat.Zip;

        // --- Console Properties ---
        [ObservableProperty] private string _consoleDatPath = "console.dat";
        [ObservableProperty] private ObservableCollection<string> _consoleSourceDirs = new() { "./console_roms" };
        [ObservableProperty] private string? _selectedConsoleSourceDir;
        [ObservableProperty] private string _consoleOutputDir = "./console_rebuilt";
        [ObservableProperty] private OutputFormat _consoleOutputFormat = OutputFormat.Zip;
        [ObservableProperty] private bool _enable1G1R = true;
        [ObservableProperty] private string _regionPriorities = "USA, World, Europe, Japan";

        public Array RebuildModes => Enum.GetValues(typeof(RebuildMode));
        public Array OutputFormats => Enum.GetValues(typeof(OutputFormat));

        [ObservableProperty] private bool _isWorking = false;
        [ObservableProperty] private string _searchFilter = string.Empty;
        [ObservableProperty] private ObservableCollection<MachineAuditItem> _filteredMachines = new();

        // --- Live Log Property ---
        [ObservableProperty] private ObservableCollection<string> _logEntries = new();

        // --- Rebuild Information Tab Properties ---
        [ObservableProperty] private string _lastRebuildTitle = "No Rebuild Performed Yet";
        [ObservableProperty] private int _lastRebuildProcessed;
        [ObservableProperty] private int _lastRebuildSuccessful;
        [ObservableProperty] private int _lastRebuildFailed;
        [ObservableProperty] private int _lastRebuildBadDumps;
        [ObservableProperty] private int _lastRebuildUnknown;
        [ObservableProperty] private int _lastRebuildMissing;
        [ObservableProperty] private string _lastRebuildElapsedTime = "00:00";

        // --- Progress & Timer Properties ---
        [ObservableProperty] private int _progressValue;
        [ObservableProperty] private int _progressMaximum = 100;
        [ObservableProperty] private string _statusMessage = "Ready";
        [ObservableProperty] private string _elapsedTimeSpan = "00:00";

        // --- Audit Summary Statistics Properties ---
        [ObservableProperty] private int _totalScanned;
        [ObservableProperty] private int _matchedCount;
        [ObservableProperty] private int _unknownCount;
        [ObservableProperty] private int _badOrNotNeededCount;

        // --- Optional callback if popup is still desired ---
        public Action<RebuildReportModel>? ShowReportCallback { get; set; }

        public MainViewModel()
        {
            LoadPreferences();
        }

        partial void OnSearchFilterChanged(string value) => ApplyFilter();

        private void LoadPreferences()
        {
            var prefs = SettingsManager.LoadPreferences();
            if (prefs != null)
            {
                if (prefs.LastMameSourceDirs != null && prefs.LastMameSourceDirs.Count > 0)
                {
                    MameSourceDirs = new ObservableCollection<string>(prefs.LastMameSourceDirs);
                }
                if (!string.IsNullOrEmpty(prefs.LastMameDatPath)) MameDatPath = prefs.LastMameDatPath;
                if (!string.IsNullOrEmpty(prefs.LastMameOutputDir)) MameOutputDir = prefs.LastMameOutputDir;

                if (prefs.LastConsoleSourceDirs != null && prefs.LastConsoleSourceDirs.Count > 0)
                {
                    ConsoleSourceDirs = new ObservableCollection<string>(prefs.LastConsoleSourceDirs);
                }
                if (!string.IsNullOrEmpty(prefs.LastConsoleDatPath)) ConsoleDatPath = prefs.LastConsoleDatPath;
                if (!string.IsNullOrEmpty(prefs.LastConsoleOutputDir)) ConsoleOutputDir = prefs.LastConsoleOutputDir;
                Enable1G1R = prefs.LastEnable1G1R;
                if (!string.IsNullOrEmpty(prefs.LastRegionPriorities)) RegionPriorities = prefs.LastRegionPriorities;
            }
        }

        private void SavePreferences()
        {
            var prefs = new AppPreferences
            {
                LastMameSourceDirs = MameSourceDirs.ToList(),
                LastMameDatPath = MameDatPath,
                LastMameOutputDir = MameOutputDir,
                LastConsoleSourceDirs = ConsoleSourceDirs.ToList(),
                LastConsoleDatPath = ConsoleDatPath,
                LastConsoleOutputDir = ConsoleOutputDir,
                LastEnable1G1R = Enable1G1R,
                LastRegionPriorities = RegionPriorities
            };
            SettingsManager.SavePreferences(prefs);
        }

        private async Task StartOperationTimer()
        {
            _stopwatch.Restart();
            while (IsWorking)
            {
                ElapsedTimeSpan = _stopwatch.Elapsed.ToString(@"mm\:ss");
                await Task.Delay(1000);
            }
        }

        // --- Folder Management Commands ---
        [RelayCommand]
        public void AddMameSource(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !MameSourceDirs.Contains(path))
            {
                MameSourceDirs.Add(path);
                WriteDebugLog($"Added MAME source directory: {path}");
            }
        }

        [RelayCommand]
        public void RemoveMameSource(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                MameSourceDirs.Remove(path);
                WriteDebugLog($"Removed MAME source directory: {path}");
            }
        }

        [RelayCommand]
        public void AddConsoleSource(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !ConsoleSourceDirs.Contains(path))
            {
                ConsoleSourceDirs.Add(path);
                WriteDebugLog($"Added Console source directory: {path}");
            }
        }

        [RelayCommand]
        public void RemoveConsoleSource(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                ConsoleSourceDirs.Remove(path);
                WriteDebugLog($"Removed Console source directory: {path}");
            }
        }

        private void WriteDebugLog(string message)
        {
            try
            {
                var formattedMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}";
                File.AppendAllText(LogFileName, formattedMessage + Environment.NewLine);
                
                Dispatcher.UIThread.Post(() =>
                {
                    LogEntries.Add(formattedMessage);
                });
            }
            catch { }
        }

        private void ApplyFilter()
        {
            Dispatcher.UIThread.Post(() =>
            {
                FilteredMachines.Clear();
                var query = SearchFilter?.Trim() ?? string.Empty;
                var matches = string.IsNullOrEmpty(query) 
                    ? _allAuditItems 
                    : _allAuditItems.Where(m => m.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || 
                                                m.Description.Contains(query, StringComparison.OrdinalIgnoreCase));

                foreach (var item in matches) FilteredMachines.Add(item);
            });
        }

        private void UpdateAuditStatistics()
        {
            TotalScanned = _allAuditItems.Count;
            MatchedCount = _allAuditItems.Count(i => i.Status == "Matched");
            UnknownCount = _allAuditItems.Count(i => i.Status == "Unknown");
            BadOrNotNeededCount = _allAuditItems.Count(i => i.Status.Contains("Broken") || i.Status.Contains("Bad"));
        }

        [RelayCommand]
        private async Task AuditMameAsync()
        {
            if (IsWorking) return;
            SavePreferences();
            IsWorking = true;
            ProgressValue = 0;
            ProgressMaximum = 100;

            _ = StartOperationTimer();
            WriteDebugLog($"Initializing MAME Audit ({SelectedRebuildMode})...");

            var progress = new Progress<RebuildProgressReport>(report =>
            {
                if (report.Total > 0) ProgressMaximum = report.Total;
                if (report.Current > 0) ProgressValue = report.Current;
                if (!string.IsNullOrEmpty(report.CurrentMessage)) StatusMessage = report.CurrentMessage;
            });

            _allAuditItems.Clear();
            FilteredMachines.Clear();
            UpdateAuditStatistics();

            try
            {
                await _rebuilderService.RunMameAudit(
                    MameSourceDirs, 
                    MameDatPath, 
                    SelectedRebuildMode, 
                    progress, 
                    item => 
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            _allAuditItems.Add(item);
                            if (string.IsNullOrEmpty(SearchFilter) || 
                                item.Name.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) || 
                                item.Description.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase))
                            {
                                FilteredMachines.Add(item);
                            }
                            UpdateAuditStatistics();
                        });
                    }
                );
                WriteDebugLog($"MAME Audit completed successfully with {_allAuditItems.Count} items.");
            }
            catch (Exception ex) 
            { 
                WriteDebugLog($"Exception in MAME Audit: {ex}");
            }
            finally 
            { 
                _stopwatch.Stop();
                IsWorking = false; 
                WriteDebugLog("=== MAME Audit Complete ==="); 
            }
        }

        [RelayCommand]
        private async Task RebuildMameAsync()
        {
            if (IsWorking) return;
            SavePreferences();
            IsWorking = true;
            ProgressValue = 0;
            ProgressMaximum = 100;

            _ = StartOperationTimer();
            WriteDebugLog($"Initializing MAME Rebuild ({SelectedRebuildMode}, Format: {MameOutputFormat})...");

            var progress = new Progress<RebuildProgressReport>(report =>
            {
                if (report.Total > 0) ProgressMaximum = report.Total;
                if (report.Current > 0) ProgressValue = report.Current;
                if (!string.IsNullOrEmpty(report.CurrentMessage)) StatusMessage = report.CurrentMessage;
            });

            try
            {
                await _rebuilderService.RunMameRebuild(
                    MameSourceDirs, 
                    MameOutputDir, 
                    MameDatPath, 
                    SelectedRebuildMode, 
                    MameOutputFormat, 
                    progress
                );

                WriteDebugLog("MAME Rebuild completed successfully.");
            }
            catch (Exception ex) 
            { 
                WriteDebugLog($"Exception in MAME Rebuild: {ex}");
            }
            finally 
            { 
                _stopwatch.Stop();
                IsWorking = false; 
                WriteDebugLog("=== MAME Rebuild Complete ==="); 

                // Populate Rebuild Info tab fields
                LastRebuildTitle = "MAME Rebuild Statistics";
                LastRebuildProcessed = TotalScanned;
                LastRebuildSuccessful = MatchedCount;
                LastRebuildUnknown = UnknownCount;
                LastRebuildBadDumps = BadOrNotNeededCount;
                LastRebuildMissing = _allAuditItems.Count(i => i.Status.Contains("Missing"));
                LastRebuildElapsedTime = ElapsedTimeSpan;
            }
        }

        [RelayCommand]
        private async Task AuditConsoleAsync()
        {
            if (IsWorking) return;

            if (!File.Exists(ConsoleDatPath))
            {
                WriteDebugLog($"[Error] DAT file not found at: {ConsoleDatPath}");
                return;
            }

            if (!ConsoleSourceDirs.Any(d => Directory.Exists(d)))
            {
                WriteDebugLog($"[Error] No valid source directories found in the list.");
                return;
            }

            SavePreferences();
            IsWorking = true;
            ProgressValue = 0;
            ProgressMaximum = 100;

            _ = StartOperationTimer();
            WriteDebugLog($"Initializing Console Audit (1G1R: {Enable1G1R})...");

            var progress = new Progress<RebuildProgressReport>(report =>
            {
                if (report.Total > 0) ProgressMaximum = report.Total;
                if (report.Current > 0) ProgressValue = report.Current;
                if (!string.IsNullOrEmpty(report.CurrentMessage)) StatusMessage = report.CurrentMessage;
            });

            _allAuditItems.Clear();
            FilteredMachines.Clear();
            UpdateAuditStatistics();

            try
            {
                await _rebuilderService.RunConsoleAudit(
                    ConsoleSourceDirs, 
                    ConsoleDatPath, 
                    Enable1G1R, 
                    RegionPriorities, 
                    progress, 
                    item => 
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            _allAuditItems.Add(item);
                            if (string.IsNullOrEmpty(SearchFilter) || 
                                item.Name.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) || 
                                item.Description.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase))
                            {
                                FilteredMachines.Add(item);
                            }
                            UpdateAuditStatistics();
                        });
                    }
                );
                WriteDebugLog($"Console Audit successfully loaded {_allAuditItems.Count} items into DataGrid.");
            }
            catch (Exception ex) 
            { 
                WriteDebugLog($"EXCEPTION in Console Audit: {ex}");
            }
            finally 
            { 
                _stopwatch.Stop();
                IsWorking = false; 
                WriteDebugLog("=== Console Audit Complete ==="); 
            }
        }

        [RelayCommand]
        private async Task RebuildConsoleAsync()
        {
            if (IsWorking) return;

            if (!File.Exists(ConsoleDatPath))
            {
                WriteDebugLog($"[Error] DAT file not found at: {ConsoleDatPath}");
                return;
            }

            if (!ConsoleSourceDirs.Any(d => Directory.Exists(d)))
            {
                WriteDebugLog($"[Error] No valid source directories found in the list.");
                return;
            }

            SavePreferences();
            IsWorking = true;
            ProgressValue = 0;
            ProgressMaximum = 100;

            _ = StartOperationTimer();
            WriteDebugLog($"Initializing Console Rebuild (Format: {ConsoleOutputFormat}, 1G1R: {Enable1G1R})...");

            var progress = new Progress<RebuildProgressReport>(report =>
            {
                if (report.Total > 0) ProgressMaximum = report.Total;
                if (report.Current > 0) ProgressValue = report.Current;
                if (!string.IsNullOrEmpty(report.CurrentMessage)) StatusMessage = report.CurrentMessage;
            });

            try
            {
                await _rebuilderService.RunConsoleRebuild(
                    ConsoleSourceDirs, 
                    ConsoleOutputDir, 
                    ConsoleDatPath, 
                    Enable1G1R, 
                    RegionPriorities, 
                    ConsoleOutputFormat, 
                    progress
                );

                WriteDebugLog("Console Rebuild completed successfully.");
            }
            catch (Exception ex) 
            { 
                WriteDebugLog($"EXCEPTION in Console Rebuild: {ex}");
            }
            finally 
            { 
                _stopwatch.Stop();
                IsWorking = false; 
                WriteDebugLog("=== Console Rebuild Complete ==="); 

                // Populate Rebuild Info tab fields
                LastRebuildTitle = "Console Rebuild Statistics";
                LastRebuildProcessed = TotalScanned;
                LastRebuildSuccessful = MatchedCount;
                LastRebuildUnknown = UnknownCount;
                LastRebuildBadDumps = BadOrNotNeededCount;
                LastRebuildMissing = _allAuditItems.Count(i => i.Status.Contains("Missing"));
                LastRebuildElapsedTime = ElapsedTimeSpan;
            }
        }
    }
}
