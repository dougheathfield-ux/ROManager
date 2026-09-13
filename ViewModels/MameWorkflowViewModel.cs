using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RomRebuilderUI.Models;
using RomRebuilderUI.Services;

namespace RomRebuilderUI.ViewModels
{
    public partial class MameWorkflowViewModel : ObservableObject
    {
        private readonly RomRebuilderService _rebuilderService = new();

        // --- MAME Configuration Properties ---
        [ObservableProperty] private string _mameDatPath = "mame.xml";
        [ObservableProperty] private ObservableCollection<string> _mameSourceDirs = new() { "./mame_roms" };
        [ObservableProperty] private string? _selectedMameSourceDir;
        [ObservableProperty] private string _mameOutputDir = "./mame_rebuilt";
        [ObservableProperty] private RebuildMode _selectedRebuildMode = RebuildMode.NonMerged;
        [ObservableProperty] private OutputFormat _mameOutputFormat = OutputFormat.Zip;

        public Array RebuildModes => Enum.GetValues(typeof(RebuildMode));
        public Array OutputFormats => Enum.GetValues(typeof(OutputFormat));

        // --- Progress & Status ---
        [ObservableProperty] private bool _isWorking = false;
        [ObservableProperty] private int _progressValue;
        [ObservableProperty] private int _progressMaximum = 100;
        [ObservableProperty] private string _statusMessage = "Ready";

        // --- Real-time Statistics ---
        [ObservableProperty] private int _totalScanned;
        [ObservableProperty] private int _matchedCount;
        [ObservableProperty] private int _unknownCount;
        [ObservableProperty] private int _badOrNotNeededCount;
        [ObservableProperty] private int _missingCount;

        // --- Source Directory Commands ---
        [RelayCommand]
        public void AddMameSource(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !MameSourceDirs.Contains(path))
            {
                MameSourceDirs.Add(path);
            }
        }

        [RelayCommand]
        public void RemoveMameSource(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                MameSourceDirs.Remove(path);
            }
        }

        // --- Audit Command ---
        [RelayCommand]
        private async Task AuditMameAsync()
        {
            if (IsWorking) return;
            IsWorking = true;
            ProgressValue = 0;
            ProgressMaximum = 100;
            StatusMessage = "Initializing MAME Audit...";

            var progress = new Progress<RebuildProgressReport>(report =>
            {
                if (report.Total > 0) ProgressMaximum = report.Total;
                if (report.Current > 0) ProgressValue = report.Current;
                if (!string.IsNullOrEmpty(report.CurrentMessage)) StatusMessage = report.CurrentMessage;
            });

            var auditItems = new List<MachineAuditItem>();

            try
            {
                await _rebuilderService.RunMameAudit(
                    MameSourceDirs, 
                    MameDatPath, 
                    SelectedRebuildMode, 
                    progress, 
                    item => 
                    {
                        auditItems.Add(item);
                        Dispatcher.UIThread.Post(() =>
                        {
                            TotalScanned = auditItems.Count;
                            MatchedCount = auditItems.Count(i => i.Status == "Matched");
                            UnknownCount = auditItems.Count(i => i.Status == "Unknown");
                            BadOrNotNeededCount = auditItems.Count(i => i.Status.Contains("Broken") || i.Status.Contains("Bad"));
                            MissingCount = auditItems.Count(i => i.Status.Contains("Missing"));
                        });
                    }
                );
                StatusMessage = "MAME Audit Completed Successfully.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsWorking = false;
            }
        }

        // --- Rebuild Command ---
        [RelayCommand]
        private async Task RebuildMameAsync()
        {
            if (IsWorking) return;
            IsWorking = true;
            ProgressValue = 0;
            ProgressMaximum = 100;
            StatusMessage = "Initializing MAME Rebuild...";

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
                StatusMessage = "MAME Rebuild Completed Successfully.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsWorking = false;
            }
        }
    }
}
