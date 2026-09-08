using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        [ObservableProperty] private ObservableCollection<string> _logEntries = new() { "Ready. Configure your paths and options, then run audit or rebuild." };
        [ObservableProperty] private bool _isWorking = false;
        [ObservableProperty] private string _searchFilter = string.Empty;
        [ObservableProperty] private ObservableCollection<MachineAuditItem> _filteredMachines = new();

        // --- Audit Summary Statistics Properties ---
        [ObservableProperty] private int _totalScanned;
        [ObservableProperty] private int _matchedCount;
        [ObservableProperty] private int _unknownCount;
        [ObservableProperty] private int _badOrNotNeededCount;

        // --- Rebuild Operation Statistics Properties ---
        [ObservableProperty] private int _rebuildProcessedCount;
        [ObservableProperty] private int _rebuildMovedCount;
        [ObservableProperty] private int _rebuildFailedCount;

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

        // --- Folder Management & Log Commands ---
        [RelayCommand]
        public void ClearLog()
        {
            LogEntries.Clear();
            WriteDebugLog("Log cleared by user.");
        }

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
            }
            catch { }
        }

        private void AppendLog(string message)
        {
            Dispatcher.UIThread.Post(() =>
            {
                LogEntries.Add(message);
            });
            WriteDebugLog(message);
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
            AppendLog($"Initializing MAME Audit ({SelectedRebuildMode})...");
            WriteDebugLog("Starting MAME Audit...");

            var progress = new Progress<string>(msg => AppendLog(msg));

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
                AppendLog($"[Error] {ex.Message}");
                WriteDebugLog($"Exception in MAME Audit: {ex}");
            }
            finally 
            { 
                IsWorking = false; 
                AppendLog("=== MAME Audit Complete ==="); 
            }
        }

        [RelayCommand]
        private async Task RebuildMameAsync()
        {
            if (IsWorking) return;
            SavePreferences();
            IsWorking = true;
            AppendLog($"Initializing MAME Rebuild ({SelectedRebuildMode}, Format: {MameOutputFormat})...");
            WriteDebugLog("Starting MAME Rebuild...");

            RebuildProcessedCount = 0;
            RebuildMovedCount = 0;
            RebuildFailedCount = 0;

            var progress = new Progress<string>(msg => AppendLog(msg));

            try
            {
                var result = await _rebuilderService.RunMameRebuild(
                    MameSourceDirs, 
                    MameOutputDir, 
                    MameDatPath, 
                    SelectedRebuildMode, 
                    MameOutputFormat, 
                    progress,
                    onProgressUpdate: currentResult =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            RebuildProcessedCount = currentResult.Processed;
                            RebuildMovedCount = currentResult.Moved;
                            RebuildFailedCount = currentResult.Failed;
                        });
                    }
                );

                RebuildProcessedCount = result.Processed;
                RebuildMovedCount = result.Moved;
                RebuildFailedCount = result.Failed;

                WriteDebugLog("MAME Rebuild completed successfully.");
            }
            catch (Exception ex) 
            { 
                AppendLog($"[Error] {ex.Message}");
                RebuildFailedCount++;
                WriteDebugLog($"Exception in MAME Rebuild: {ex}");
            }
            finally 
            { 
                IsWorking = false; 
                AppendLog("=== MAME Rebuild Complete ==="); 
            }
        }

        [RelayCommand]
        private async Task AuditConsoleAsync()
        {
            if (IsWorking) return;

            WriteDebugLog("AuditConsoleAsync triggered.");

            if (!File.Exists(ConsoleDatPath))
            {
                AppendLog($"[Error] DAT file not found at: {ConsoleDatPath}");
                return;
            }

            if (!ConsoleSourceDirs.Any(d => Directory.Exists(d)))
            {
                AppendLog($"[Error] No valid source directories found in the list.");
                return;
            }

            SavePreferences();
            IsWorking = true;
            AppendLog($"Initializing Console Audit (1G1R: {Enable1G1R})...");
            var progress = new Progress<string>(msg => AppendLog(msg));

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
                AppendLog($"[Error] {ex.Message}");
                WriteDebugLog($"EXCEPTION in Console Audit: {ex}");
            }
            finally 
            { 
                IsWorking = false; 
                AppendLog("=== Console Audit Complete ==="); 
            }
        }

        [RelayCommand]
        private async Task RebuildConsoleAsync()
        {
            if (IsWorking) return;

            WriteDebugLog("RebuildConsoleAsync triggered.");

            if (!File.Exists(ConsoleDatPath))
            {
                AppendLog($"[Error] DAT file not found at: {ConsoleDatPath}");
                return;
            }

            if (!ConsoleSourceDirs.Any(d => Directory.Exists(d)))
            {
                AppendLog($"[Error] No valid source directories found in the list.");
                return;
            }

            SavePreferences();
            IsWorking = true;
            AppendLog($"Initializing Console Rebuild (Format: {ConsoleOutputFormat}, 1G1R: {Enable1G1R})...");
            
            RebuildProcessedCount = 0;
            RebuildMovedCount = 0;
            RebuildFailedCount = 0;

            var progress = new Progress<string>(msg => AppendLog(msg));

            try
            {
                var result = await _rebuilderService.RunConsoleRebuild(
                    ConsoleSourceDirs, 
                    ConsoleOutputDir, 
                    ConsoleDatPath, 
                    Enable1G1R, 
                    RegionPriorities, 
                    ConsoleOutputFormat, 
                    progress,
                    onProgressUpdate: currentResult =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            RebuildProcessedCount = currentResult.Processed;
                            RebuildMovedCount = currentResult.Moved;
                            RebuildFailedCount = currentResult.Failed;
                        });
                    }
                );

                RebuildProcessedCount = result.Processed;
                RebuildMovedCount = result.Moved;
                RebuildFailedCount = result.Failed;

                WriteDebugLog("Console Rebuild completed successfully.");
            }
            catch (Exception ex) 
            { 
                AppendLog($"[Error] {ex.Message}");
                RebuildFailedCount++;
                WriteDebugLog($"EXCEPTION in Console Rebuild: {ex}");
            }
            finally 
            { 
                IsWorking = false; 
                AppendLog("=== Console Rebuild Complete ==="); 
            }
        }
    }
}
