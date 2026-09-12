using System.Collections.Generic;
using Avalonia.Media;

namespace RomRebuilderUI.Models
{
    public class MachineAuditItem
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string RomCountSummary { get; set; } = string.Empty;
        public List<string> MissingFiles { get; set; } = new();

        // Dynamic color mapping including the new Unknown status
        public IBrush StatusColor => Status switch
        {
            "Complete" => Brushes.LightGreen,
            "Incomplete" => Brushes.Yellow,
            "Missing" => Brushes.Red,
            "Unknown" => Brushes.MediumPurple,
            _ => Brushes.DarkGray
        };
    }

    public class AuditSummary
    {
        public int TotalFiles { get; set; }
        public int ValidFiles { get; set; }
        public int UnknownFilesCount => UnknownFiles?.Count ?? 0;
        public List<MachineAuditItem> Items { get; set; } = new();
        public List<string> UnknownFiles { get; set; } = new();
    }

    public enum RebuildMode
    {
        Standard,
        Full,
        Merged,
        Split,
        NonMerged
    }

    public enum OutputFormat
    {
        Zip,
        SevenZ,
        Folder
    }
}
