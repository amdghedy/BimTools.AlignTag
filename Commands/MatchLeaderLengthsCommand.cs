using System;
using System.Linq;
using AlignTool.Helpers;
using AlignTool.Models;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AlignTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class MatchLeaderLengthsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            View view = doc?.ActiveView;
            if (doc == null) { message = "No document."; return Result.Failed; }
            try
            {
                var tags = SelectionHelper.GetSelectedTags(uidoc, allowPick: true, "Select tags");
                if (tags.Count < 2) { TaskDialog.Show("Match Len", "Select at least two tags."); return Result.Cancelled; }
                var lengths = tags.Select(t =>
                {
                    XYZ e = TagInfo.GetLeaderEndSafe(t, doc, view, out _);
                    return e == null ? 0 : t.TagHeadPosition.DistanceTo(e);
                }).Where(l => l > 1e-6).ToList();
                if (lengths.Count == 0) { TaskDialog.Show("Match Len", "No leaders measured."); return Result.Failed; }
                double target = lengths.Average();
                int n = 0;
                using (var tg = new TransactionGroup(doc, "Match Lengths"))
                {
                    tg.Start();
                    foreach (var tag in tags)
                    {
                        if (tag.Pinned) continue;
                        XYZ end = TagInfo.GetLeaderEndSafe(tag, doc, view, out _);
                        if (end == null) continue;
                        XYZ dir = tag.TagHeadPosition - end;
                        if (dir.GetLength() < 1e-9) dir = new XYZ(-1, 0, 0);
                        XYZ dest = end + dir.Normalize() * target;
                        using (var t = new Transaction(doc, "Match"))
                        {
                            t.Start();
                            if (tag.TrySetHeadPosition(dest)) { t.Commit(); n++; }
                            else t.RollBack();
                        }
                    }
                    tg.Assimilate();
                }
                TaskDialog.Show("Match Len", $"{n} tag(s) matched.");
                return Result.Succeeded;
            }
            catch (Exception ex) { Logger.Error("MatchLen failed", ex); message = ex.Message; return Result.Failed; }
        }
    }
}
