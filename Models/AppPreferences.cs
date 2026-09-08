using System.Collections.Generic;

namespace RomRebuilderUI.Models
{
    public class AppPreferences
    {
        public List<string> LastMameSourceDirs { get; set; } = new();
        public string LastMameDatPath { get; set; } = string.Empty;
        public string LastMameOutputDir { get; set; } = string.Empty;

        public List<string> LastConsoleSourceDirs { get; set; } = new();
        public string LastConsoleDatPath { get; set; } = string.Empty;
        public string LastConsoleOutputDir { get; set; } = string.Empty;
        public bool LastEnable1G1R { get; set; } = true;
        public string LastRegionPriorities { get; set; } = "USA, World, Europe, Japan";
    }
}
