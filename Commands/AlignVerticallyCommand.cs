using System;
using AlignTool.Helpers;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AlignTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AlignVerticallyCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null) { message = "No document."; return Result.Failed; }

            try
            {
                var tags = SelectionHelper.GetSelectedTags(uidoc, allowPick: true, "Select tags to align vertically");
                if (tags.Count < 2)
                {
                    TaskDialog.Show("Align Vertical", "Select at least two tags.");
                    return Result.Cancelled;
                }

                double masterX = tags[0].TagHeadPosition.X;
                int moved = 0;
                using (var tg = new TransactionGroup(doc, "Align Tags Vertically"))
                {
                    tg.Start();
                    for (int i = 1; i < tags.Count; i++)
                    {
                        var tag = tags[i];
                        if (tag.Pinned) continue;
                        XYZ p = tag.TagHeadPosition;
                        XYZ dest = new XYZ(masterX, p.Y, p.Z);
                        using (var t = new Transaction(doc, "Align Tag V"))
                        {
                            t.Start();
                            if (tag.TrySetHeadPosition(dest)) { t.Commit(); moved++; }
                            else t.RollBack();
                        }
                    }
                    tg.Assimilate();
                }

                TaskDialog.Show("Align Vertical", $"{moved} tag(s) aligned to first tag X.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Error("AlignVertically failed", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
