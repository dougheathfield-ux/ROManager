using System.Collections.Generic;
using Avalonia.Media; // Required for Avalonia Brushes

namespace RomRebuilderUI.Models
{
    public enum RebuildMode
    {
        NonMerged,
        Split,
        Merged
    }

    public enum OutputFormat
    {
        Zip,
        SevenZip
    }

    public class MachineAuditItem
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RomCountSummary { get; set; } = string.Empty;
        public string Status { get; set; } = "Unknown";

        // Returns an Avalonia Brush for UI text color binding
        public IBrush StatusColor => Status switch
        {
            "Matched" => Brushes.Green,
            "Broken (Bad Dump)" => Brushes.DarkOrange,
            _ => Brushes.Red
        };
    }

    public class AuditSummary
    {
        public int TotalFiles { get; set; }
        public int ValidFiles { get; set; }
        public int MissingFiles { get; set; }
        public List<MachineAuditItem> Items { get; set; } = new();
    }
}
