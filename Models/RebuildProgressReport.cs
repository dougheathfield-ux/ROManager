namespace RomRebuilderUI.Models
{
    public class RebuildProgressReport
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public string CurrentMessage { get; set; } = string.Empty;
    }
}
