using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RomRebuilderUI.ViewModels
{
    public partial class MonitorViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _logContent = "Log viewer initialized. Click 'Refresh Logs' to load latest activity from rom_rebuilder_debug.log.";

        [ObservableProperty]
        private string _statusMessage = "Monitor ready.";

        [RelayCommand]
        private async Task RefreshLogsAsync()
        {
            try
            {
                string logPath = "rom_rebuilder_debug.log";
                if (File.Exists(logPath))
                {
                    // Use FileShare.ReadWrite to safely read while background processes write to it
                    using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var sr = new StreamReader(fs);
                    LogContent = await sr.ReadToEndAsync();
                    StatusMessage = $"Logs refreshed at {DateTime.Now:T}";
                }
                else
                {
                    LogContent = "No debug log file found yet (rom_rebuilder_debug.log).";
                    StatusMessage = "Log file not found.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error reading log: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ClearLogAsync()
        {
            try
            {
                string logPath = "rom_rebuilder_debug.log";
                if (File.Exists(logPath))
                {
                    await Task.Run(() => File.WriteAllText(logPath, string.Empty));
                    LogContent = string.Empty;
                    StatusMessage = "Debug log cleared successfully.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error clearing log: {ex.Message}";
            }
        }
    }
}
