using System;
using System.Linq;
using AlignTool.Helpers;
using AlignTool.Models;
using AlignTool.Services;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AlignTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AlignHorizontallyCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null) { message = "No document."; return Result.Failed; }

            try
            {
                var tags = SelectionHelper.GetSelectedTags(uidoc, allowPick: true, "Select tags to align horizontally");
                if (tags.Count < 2)
                {
                    TaskDialog.Show("Align Horizontal", "Select at least two tags.");
                    return Result.Cancelled;
                }

                double masterY = tags[0].TagHeadPosition.Y;
                int moved = 0;
                using (var tg = new TransactionGroup(doc, "Align Tags Horizontally"))
                {
                    tg.Start();
                    for (int i = 1; i < tags.Count; i++)
                    {
                        var tag = tags[i];
                        if (tag.Pinned) continue;
                        XYZ p = tag.TagHeadPosition;
                        XYZ dest = new XYZ(p.X, masterY, p.Z);
                        using (var t = new Transaction(doc, "Align Tag H"))
                        {
                            t.Start();
                            if (tag.TrySetHeadPosition(dest)) { t.Commit(); moved++; }
                            else t.RollBack();
                        }
                    }
                    tg.Assimilate();
                }

                TaskDialog.Show("Align Horizontal", $"{moved} tag(s) aligned to first tag Y.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Error("AlignHorizontally failed", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
