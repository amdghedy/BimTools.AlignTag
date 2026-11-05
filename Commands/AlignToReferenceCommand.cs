using System;
using AlignTool.Helpers;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace AlignTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AlignToReferenceCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null) { message = "No document."; return Result.Failed; }
            try
            {
                var tags = SelectionHelper.GetSelectedTags(uidoc, allowPick: true, "Select tags to move");
                if (tags.Count == 0) { TaskDialog.Show("To Ref", "No tags selected."); return Result.Cancelled; }
                Reference r;
                try { r = uidoc.Selection.PickObject(ObjectType.Element, "Pick reference tag"); }
                catch { return Result.Cancelled; }
                var master = doc.GetElement(r) as IndependentTag;
                if (master == null) { TaskDialog.Show("To Ref", "Pick an IndependentTag."); return Result.Failed; }
                double y = master.TagHeadPosition.Y;
                int n = 0;
                using (var tg = new TransactionGroup(doc, "Align To Reference"))
                {
                    tg.Start();
                    foreach (var tag in tags)
                    {
                        if (tag.Id == master.Id || tag.Pinned) continue;
                        XYZ p = tag.TagHeadPosition;
                        using (var t = new Transaction(doc, "Move"))
                        {
                            t.Start();
                            if (tag.TrySetHeadPosition(new XYZ(p.X, y, p.Z))) { t.Commit(); n++; }
                            else t.RollBack();
                        }
                    }
                    tg.Assimilate();
                }
                TaskDialog.Show("To Ref", $"{n} tag(s) aligned to reference Y.");
                return Result.Succeeded;
            }
            catch (Exception ex) { Logger.Error("AlignToReference failed", ex); message = ex.Message; return Result.Failed; }
        }
    }
}
