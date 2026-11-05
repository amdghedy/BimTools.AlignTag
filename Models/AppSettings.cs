namespace AlignTool.Models
{
    public class AppSettings
    {
        public bool EnableFileLogging { get; set; } = true;
        public bool EnableVerboseLogging { get; set; } = false;
        public int LogRetentionDays { get; set; } = 14;
        public double StackSpacingMm { get; set; } = 45;
        public double MaxMoveMm { get; set; } = 1200;
        public double MinHeadDistanceMm { get; set; } = 40;
        public double MinimizeLeaderMm { get; set; } = 150;
    }
}
