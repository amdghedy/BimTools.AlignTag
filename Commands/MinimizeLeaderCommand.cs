using System;
using AlignTool.Helpers;
using AlignTool.Models;
using AlignTool.Services;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AlignTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class MinimizeLeaderCommand : IExternalCommand
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
                double len = UnitHelper.MmToFeet(SettingsService.Instance.Current.MinimizeLeaderMm);
                int n = 0;
                using (var tg = new TransactionGroup(doc, "Minimize Leaders"))
                {
                    tg.Start();
                    foreach (var tag in tags)
                    {
                        if (tag.Pinned) continue;
                        XYZ end = TagInfo.GetLeaderEndSafe(tag, doc, view, out _);
                        if (end == null) continue;
                        XYZ dir = tag.TagHeadPosition - end;
                        if (dir.GetLength() < 1e-9) dir = new XYZ(-1, 0, 0);
                        XYZ dest = end + dir.Normalize() * len;
                        using (var t = new Transaction(doc, "Minimize"))
                        {
                            t.Start();
                            if (tag.TrySetHeadPosition(dest)) { t.Commit(); n++; }
                            else t.RollBack();
                        }
                    }
                    tg.Assimilate();
                }
                TaskDialog.Show("Minimize", $"{n} leader(s) shortened.");
                return Result.Succeeded;
            }
            catch (Exception ex) { Logger.Error("Minimize failed", ex); message = ex.Message; return Result.Failed; }
        }
    }
}
