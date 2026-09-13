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
    public partial class MameScannerViewModel : ObservableObject
    {
        private readonly RomRebuilderService _rebuilderService = new();
        private const string SettingsFileName = "mame_scanner_settings.json";

        // --- Paths ---
        [ObservableProperty] private string _mameDatPath = string.Empty;
        [ObservableProperty] private ObservableCollection<string> _mameSourceDirs = new();
        [ObservableProperty] private string? _selectedMameSourceDir;

        // --- Structure Context ---
        [ObservableProperty] private RebuildMode _selectedRebuildMode = RebuildMode.Split;
        public List<RebuildMode> RebuildModeOptions { get; } = new() { RebuildMode.Split, RebuildMode.Merged, RebuildMode.NonMerged };

        // --- Target Selection ---
        [ObservableProperty] private bool _auditRoms = true;
        [ObservableProperty] private bool _auditDisks = true;
        [ObservableProperty] private bool _auditSamples = false;
        [ObservableProperty] private bool _auditBios = true;

        // --- Deep Verification Flags ---
        [ObservableProperty] private bool _decompressAndVerify = false;
        [ObservableProperty] private bool _chdDeepVerify = false;

        // --- Fix & Cleanup Options ---
        [ObservableProperty] private bool _fixMissingFiles = false;
        [ObservableProperty] private bool _fixUnneededFiles = false;

        // --- Sorting & Filtering ---
        [ObservableProperty] private string _selectedSortOption = "Machine Name";
        public List<string> SortOptions { get; } = new() { "Machine Name", "Description", "Status", "Missing Files" };

        partial void OnSelectedSortOptionChanged(string value)
        {
            ApplySorting();
        }

        // --- Progress & UI State ---
        [ObservableProperty] private bool _isWorking;
        [ObservableProperty] private bool _isIndeterminate;
        [ObservableProperty] private int _progressValue;
        [ObservableProperty] private int _progressMaximum = 100;
        [ObservableProperty] private string _statusMessage = "Ready to scan";
        [ObservableProperty] private bool _hasAuditItems;

        // --- Results ---
        [ObservableProperty] private ObservableCollection<MachineAuditItem> _mameAuditItems = new();
        private List<MachineAuditItem> _unfilteredAuditCache = new();
        [ObservableProperty] private MachineAuditItem? _selectedAuditItem;

        public MameScannerViewModel()
        {
            LoadSettings();
        }

        public void SetDatPath(string path)
        {
            MameDatPath = path;
            SaveSettings();
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new MameScannerSettingsDto
                {
                    DatPath = MameDatPath,
                    SourceDirs = MameSourceDirs.ToList(),
                    RebuildMode = SelectedRebuildMode,
                    AuditRoms = AuditRoms,
                    AuditDisks = AuditDisks,
                    AuditSamples = AuditSamples,
                    AuditBios = AuditBios,
                    DecompressAndVerify = DecompressAndVerify,
                    ChdDeepVerify = ChdDeepVerify,
                    FixMissingFiles = FixMissingFiles,
                    FixUnneededFiles = FixUnneededFiles
                };
                File.WriteAllText(SettingsFileName, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFileName))
                {
                    var settings = JsonSerializer.Deserialize<MameScannerSettingsDto>(File.ReadAllText(SettingsFileName));
                    if (settings != null)
                    {
                        MameDatPath = settings.DatPath ?? string.Empty;
                        if (settings.SourceDirs != null) MameSourceDirs = new ObservableCollection<string>(settings.SourceDirs);
                        SelectedRebuildMode = settings.RebuildMode;
                        AuditRoms = settings.AuditRoms;
                        AuditDisks = settings.AuditDisks;
                        AuditSamples = settings.AuditSamples;
                        AuditBios = settings.AuditBios;
                        DecompressAndVerify = settings.DecompressAndVerify;
                        ChdDeepVerify = settings.ChdDeepVerify;
                        FixMissingFiles = settings.FixMissingFiles;
                        FixUnneededFiles = settings.FixUnneededFiles;
                    }
                }
            }
            catch { }
        }

        [RelayCommand]
        public void AddSource(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !MameSourceDirs.Contains(path))
            {
                MameSourceDirs.Add(path.Trim());
                SaveSettings();
            }
        }

        [RelayCommand]
        public void RemoveSource(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                MameSourceDirs.Remove(path);
                SaveSettings();
            }
        }

        [RelayCommand]
        private void ClearScan()
        {
            if (IsWorking) return;
            
            _unfilteredAuditCache.Clear();
            MameAuditItems.Clear();
            SelectedAuditItem = null;
            HasAuditItems = false;
            ProgressValue = 0;
            ProgressMaximum = 100;
            StatusMessage = "Scan cleared. Ready.";
        }

        private void ApplySorting()
        {
            if (_unfilteredAuditCache.Count == 0) return;

            var sorted = SelectedSortOption switch
            {
                "Description" => _unfilteredAuditCache.OrderBy(x => x.Description).ThenBy(x => x.Name),
                "Status" => _unfilteredAuditCache.OrderBy(x => x.Status).ThenBy(x => x.Name),
                "Missing Files" => _unfilteredAuditCache.OrderByDescending(x => x.MissingFiles?.Count ?? 0).ThenBy(x => x.Name),
                _ => _unfilteredAuditCache.OrderBy(x => x.Name).AsEnumerable()
            };

            MameAuditItems = new ObservableCollection<MachineAuditItem>(sorted);
        }

        [RelayCommand]
        private async Task AuditMameAsync()
        {
            if (IsWorking) return;
            IsWorking = true;
            IsIndeterminate = true;
            StatusMessage = "Auditing MAME sets...";
            ClearScan();

            var auditList = new List<MachineAuditItem>();
            try
            {
                var progress = new Progress<RebuildProgressReport>(r =>
                {
                    IsIndeterminate = false;
                    if (r.Total > 0) ProgressMaximum = r.Total;
                    if (r.Current > 0) ProgressValue = r.Current;
                    if (!string.IsNullOrEmpty(r.CurrentMessage)) StatusMessage = r.CurrentMessage;
                });

                await Task.Run(async () =>
                {
                    await _rebuilderService.RunMameAudit(
                        MameSourceDirs,
                        MameDatPath,
                        SelectedRebuildMode,
                        progress,
                        item => 
                        { 
                            lock (auditList) 
                            { 
                                auditList.Add(item); 
                            } 
                        }
                    );
                });

                Dispatcher.UIThread.Post(() =>
                {
                    _unfilteredAuditCache = auditList;
                    ApplySorting();
                    HasAuditItems = MameAuditItems.Count > 0;
                    StatusMessage = $"Audit completed. Total machines scanned: {MameAuditItems.Count}";
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Audit Error: {ex.Message}";
            }
            finally
            {
                IsWorking = false;
                IsIndeterminate = false;
            }
        }
    }

    internal class MameScannerSettingsDto
    {
        public string DatPath { get; set; } = string.Empty;
        public List<string> SourceDirs { get; set; } = new();
        public RebuildMode RebuildMode { get; set; }
        public bool AuditRoms { get; set; }
        public bool AuditDisks { get; set; }
        public bool AuditSamples { get; set; }
        public bool AuditBios { get; set; }
        public bool DecompressAndVerify { get; set; }
        public bool ChdDeepVerify { get; set; }
        public bool FixMissingFiles { get; set; }
        public bool FixUnneededFiles { get; set; }
    }
}
