using System;
using AlignTool.Helpers;
using AlignTool.Models;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AlignTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class FlipTagsSideCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            View view = doc?.ActiveView;
            if (doc == null) { message = "No document."; return Result.Failed; }
            try
            {
                var tags = SelectionHelper.GetSelectedTags(uidoc, allowPick: true, "Select tags to flip");
                int n = 0;
                using (var tg = new TransactionGroup(doc, "Flip Tags"))
                {
                    tg.Start();
                    foreach (var tag in tags)
                    {
                        if (tag.Pinned) continue;
                        XYZ end = TagInfo.GetLeaderEndSafe(tag, doc, view, out _);
                        if (end == null) continue;
                        XYZ h = tag.TagHeadPosition;
                        XYZ dest = new XYZ(2 * end.X - h.X, 2 * end.Y - h.Y, h.Z);
                        using (var t = new Transaction(doc, "Flip"))
                        {
                            t.Start();
                            if (tag.TrySetHeadPosition(dest)) { t.Commit(); n++; }
                            else t.RollBack();
                        }
                    }
                    tg.Assimilate();
                }
                TaskDialog.Show("Flip", $"{n} tag(s) flipped.");
                return Result.Succeeded;
            }
            catch (Exception ex) { Logger.Error("Flip failed", ex); message = ex.Message; return Result.Failed; }
        }
    }
}
