using System;

namespace RomRebuilderUI.Models
{
    public class RebuildResult
    {
        public bool Success { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public int FilesProcessed { get; set; }
        public int FilesMatched { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
