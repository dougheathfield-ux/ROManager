using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RomRebuilderUI.Models;

namespace RomRebuilderUI.ViewModels
{
    public partial class InspectorViewModel : ObservableObject
    {
        private ObservableCollection<MachineAuditItem> _allAuditItems = new();

        [ObservableProperty]
        private ObservableCollection<MachineAuditItem> _filteredAuditItems = new();

        [ObservableProperty]
        private string _searchFilter = string.Empty;

        [ObservableProperty]
        private string _selectedStatusFilter = "All";

        [ObservableProperty]
        private MachineAuditItem? _selectedItem;

        [ObservableProperty]
        private string _statusMessage = "Inspector ready. Run an audit in MAME or Console workflow to view results.";

        public string[] StatusFilterOptions => new[] { "All", "Matched", "Missing", "Unknown", "Bad/Broken" };

        partial void OnSearchFilterChanged(string value) => ApplyFilters();
        partial void OnSelectedStatusFilterChanged(string value) => ApplyFilters();

        public void LoadAuditData(ObservableCollection<MachineAuditItem> items)
        {
            _allAuditItems = items;
            ApplyFilters();
            StatusMessage = $"Loaded {_allAuditItems.Count} items into Inspector.";
        }

        private void ApplyFilters()
        {
            var query = _allAuditItems.AsEnumerable();

            // Text search filter
            if (!string.IsNullOrWhiteSpace(SearchFilter))
            {
                query = query.Where(i => 
                    (!string.IsNullOrEmpty(i.Name) && i.Name.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(i.Description) && i.Description.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase))
                );
            }

            // Status filter
            if (SelectedStatusFilter != "All")
            {
                query = query.Where(i => i.Status.Contains(SelectedStatusFilter, StringComparison.OrdinalIgnoreCase));
            }

            FilteredAuditItems = new ObservableCollection<MachineAuditItem>(query);
        }

        [RelayCommand]
        private async Task ExportMissingReportAsync()
        {
            try
            {
                var missingItems = _allAuditItems.Where(i => i.Status.Contains("Missing", StringComparison.OrdinalIgnoreCase)).ToList();
                var lines = missingItems.Select(i => $"{i.Name} - {i.Description}");
                await File.WriteAllLinesAsync("missing_roms_report.txt", lines);
                StatusMessage = $"Successfully exported {missingItems.Count} missing items to missing_roms_report.txt";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ExportFullReportAsync()
        {
            try
            {
                var lines = _allAuditItems.Select(i => $"{i.Name} | {i.Status} | {i.Description}");
                await File.WriteAllLinesAsync("full_audit_report.txt", lines);
                StatusMessage = $"Successfully exported full report to full_audit_report.txt";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
            }
        }
    }
}
