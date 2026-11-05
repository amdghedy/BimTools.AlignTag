using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace AlignTool.Models
{
    public class AlignmentResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int MovedCount { get; set; }
        public int TagsProcessed { get; set; }
        public int TagsMoved { get; set; }
        public int TagsFailed { get; set; }
        public int CrossingCount { get; set; }
        public List<ElementId> MovedIds { get; set; } = new List<ElementId>();
        public List<string> Warnings { get; set; } = new List<string>();

        public static AlignmentResult Ok(string message, int moved = 0) =>
            new AlignmentResult { Success = true, Message = message, MovedCount = moved, TagsMoved = moved };

        public static AlignmentResult Fail(string message) =>
            new AlignmentResult { Success = false, Message = message };
    }
}
