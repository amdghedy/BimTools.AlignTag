using System.Collections.Generic;

namespace AlignTool.Models
{
    public class AlignmentResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int MovedCount { get; set; }
        public int CrossingCount { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();

        public static AlignmentResult Ok(string message, int moved = 0) =>
            new AlignmentResult { Success = true, Message = message, MovedCount = moved };

        public static AlignmentResult Fail(string message) =>
            new AlignmentResult { Success = false, Message = message };
    }
}
