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
    public partial class MameWorkflowViewModel : ObservableObject
    {
        private readonly RomRebuilderService _rebuilderService = new();
        private const string SettingsFileName = "mame_rebuilder_settings.json";

        // --- Paths ---
        [ObservableProperty] private string _mameDatPath = string.Empty;
        [ObservableProperty] private ObservableCollection<string> _sourceDirs = new();
        [ObservableProperty] private string? _selectedSourceDir;

        [ObservableProperty] private ObservableCollection<string> _addPaths = new();
        [ObservableProperty] private string? _selectedAddPath;

        [ObservableProperty] private string _outputDirectory = string.Empty;

        // --- Architecture & Format ---
        [ObservableProperty] private RebuildMode _selectedRebuildMode = RebuildMode.Split;
        public List<RebuildMode> RebuildModeOptions { get; } = new() { RebuildMode.Split, RebuildMode.Merged, RebuildMode.NonMerged };

        [ObservableProperty] private string _selectedCompressionFormat = ".zip";
        public List<string> CompressionFormatOptions { get; } = new() { ".zip", ".7z", "Folder" };

        // --- Advanced Rebuilder Engine Toggles ---
        [ObservableProperty] private bool _separateBiosSets = true;
        [ObservableProperty] private bool _recompressFiles = false;
        [ObservableProperty] private bool _removeMatchedSourceFiles = false;
        [ObservableProperty] private bool _verifyHashesOnMatch = true;

        // --- Progress & UI State ---
        [ObservableProperty] private bool _isWorking;
        [ObservableProperty] private bool _isIndeterminate;
        [ObservableProperty] private int _progressValue;
        [ObservableProperty] private int _progressMaximum = 100;
        [ObservableProperty] private string _statusMessage = "Ready to rebuild";

        public MameWorkflowViewModel()
        {
            LoadSettings();
        }

        public void SetDatPath(string path)
        {
            MameDatPath = path;
            SaveSettings();
        }

        public void SetOutputDirectory(string path)
        {
            OutputDirectory = path;
            SaveSettings();
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new MameRebuilderSettingsDto
                {
                    DatPath = MameDatPath,
                    SourceDirs = SourceDirs.ToList(),
                    AddPaths = AddPaths.ToList(),
                    OutputDirectory = OutputDirectory,
                    RebuildMode = SelectedRebuildMode,
                    CompressionFormat = SelectedCompressionFormat,
                    SeparateBiosSets = SeparateBiosSets,
                    RecompressFiles = RecompressFiles,
                    RemoveMatchedSourceFiles = RemoveMatchedSourceFiles,
                    VerifyHashesOnMatch = VerifyHashesOnMatch
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
                    var settings = JsonSerializer.Deserialize<MameRebuilderSettingsDto>(File.ReadAllText(SettingsFileName));
                    if (settings != null)
                    {
                        MameDatPath = settings.DatPath ?? string.Empty;
                        if (settings.SourceDirs != null) SourceDirs = new ObservableCollection<string>(settings.SourceDirs);
                        if (settings.AddPaths != null) AddPaths = new ObservableCollection<string>(settings.AddPaths);
                        OutputDirectory = settings.OutputDirectory ?? string.Empty;
                        SelectedRebuildMode = settings.RebuildMode;
                        SelectedCompressionFormat = settings.CompressionFormat ?? ".zip";
                        SeparateBiosSets = settings.SeparateBiosSets;
                        RecompressFiles = settings.RecompressFiles;
                        RemoveMatchedSourceFiles = settings.RemoveMatchedSourceFiles;
                        VerifyHashesOnMatch = settings.VerifyHashesOnMatch;
                    }
                }
            }
            catch { }
        }

        [RelayCommand]
        public void AddSource(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !SourceDirs.Contains(path))
            {
                SourceDirs.Add(path.Trim());
                SaveSettings();
            }
        }

        [RelayCommand]
        public void RemoveSource(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                SourceDirs.Remove(path);
                SaveSettings();
            }
        }

        [RelayCommand]
        public void AddAddPath(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !AddPaths.Contains(path))
            {
                AddPaths.Add(path.Trim());
                SaveSettings();
            }
        }

        [RelayCommand]
        public void RemoveAddPath(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                AddPaths.Remove(path);
                SaveSettings();
            }
        }

        [RelayCommand]
        private async Task RunRebuildAsync()
        {
            if (IsWorking) return;
            if (string.IsNullOrWhiteSpace(MameDatPath) || string.IsNullOrWhiteSpace(OutputDirectory))
            {
                StatusMessage = "Error: Please specify a DAT file and an Output Directory.";
                return;
            }

            IsWorking = true;
            IsIndeterminate = true;
            StatusMessage = "Starting MAME arcade rebuild...";

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
                    await _rebuilderService.RunMameRebuild(
                        SourceDirs,
                        AddPaths,
                        OutputDirectory,
                        MameDatPath,
                        SelectedRebuildMode,
                        SelectedCompressionFormat,
                        SeparateBiosSets,
                        RecompressFiles,
                        RemoveMatchedSourceFiles,
                        VerifyHashesOnMatch,
                        progress
                    );
                });

                Dispatcher.UIThread.Post(() =>
                {
                    StatusMessage = "Rebuild completed successfully!";
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Rebuild Error: {ex.Message}";
            }
            finally
            {
                IsWorking = false;
                IsIndeterminate = false;
            }
        }
    }

    internal class MameRebuilderSettingsDto
    {
        public string DatPath { get; set; } = string.Empty;
        public List<string> SourceDirs { get; set; } = new();
        public List<string> AddPaths { get; set; } = new();
        public string OutputDirectory { get; set; } = string.Empty;
        public RebuildMode RebuildMode { get; set; }
        public string CompressionFormat { get; set; } = ".zip";
        public bool SeparateBiosSets { get; set; }
        public bool RecompressFiles { get; set; }
        public bool RemoveMatchedSourceFiles { get; set; }
        public bool VerifyHashesOnMatch { get; set; }
    }
}
