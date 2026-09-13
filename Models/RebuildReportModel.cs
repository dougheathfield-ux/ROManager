namespace RomRebuilderUI.Models
{
    public class RebuildReportModel
    {
        public string Title { get; set; } = "Rebuild Statistics";
        public int TotalProcessed { get; set; }
        public int SuccessfulMoves { get; set; }
        public int FailedCount { get; set; }
        public int MissingCount { get; set; }
        public int BadDumpsCount { get; set; }
        public int UnknownCount { get; set; }
        public string ElapsedTime { get; set; } = "00:00";
    }
}
