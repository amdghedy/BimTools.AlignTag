using System;
using System.Linq;
using AlignTool.Helpers;
using AlignTool.Models;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace AlignTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AlignToLineCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null) { message = "No document."; return Result.Failed; }
            try
            {
                var tags = SelectionHelper.GetSelectedTags(uidoc, allowPick: true, "Select tags");
                if (tags.Count == 0) { TaskDialog.Show("To Line", "No tags."); return Result.Cancelled; }
                Reference lineRef;
                try { lineRef = uidoc.Selection.PickObject(ObjectType.Element, "Pick a line/grid"); }
                catch { return Result.Cancelled; }
                Element el = doc.GetElement(lineRef);
                Curve curve = null;
                if (el is DetailLine dl) curve = dl.GeometryCurve;
                else if (el is ModelLine ml) curve = ml.GeometryCurve;
                else if (el is Grid g) curve = g.Curve;
                else if (el.Location is LocationCurve lc) curve = lc.Curve;
                if (curve == null) { TaskDialog.Show("To Line", "Could not get curve."); return Result.Failed; }
                int n = 0;
                using (var tg = new TransactionGroup(doc, "Align To Line"))
                {
                    tg.Start();
                    foreach (var tag in tags)
                    {
                        if (tag.Pinned) continue;
                        IntersectionResult ir = curve.Project(tag.TagHeadPosition);
                        if (ir?.XYZPoint == null) continue;
                        XYZ dest = new XYZ(ir.XYZPoint.X, ir.XYZPoint.Y, tag.TagHeadPosition.Z);
                        using (var t = new Transaction(doc, "Project"))
                        {
                            t.Start();
                            if (tag.TrySetHeadPosition(dest)) { t.Commit(); n++; }
                            else t.RollBack();
                        }
                    }
                    tg.Assimilate();
                }
                TaskDialog.Show("To Line", n + " tag(s) projected.");
                return Result.Succeeded;
            }
            catch (Exception ex) { Logger.Error("AlignToLine failed", ex); message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class UntangleTagsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null) { message = "No document."; return Result.Failed; }
            try
            {
                var tags = SelectionHelper.GetSelectedTags(uidoc, allowPick: true, "Select tags").Where(t => !t.Pinned).ToList();
                if (tags.Count < 2) { TaskDialog.Show("Untangle", "Select at least two tags."); return Result.Cancelled; }
                double minDist = UnitHelper.MmToFeet(40);
                int n = 0;
                using (var tg = new TransactionGroup(doc, "Untangle"))
                {
                    tg.Start();
                    for (int pass = 0; pass < 6; pass++)
                    {
                        bool any = false;
                        for (int i = 0; i < tags.Count; i++)
                        for (int j = i + 1; j < tags.Count; j++)
                        {
                            XYZ a = tags[i].TagHeadPosition, b = tags[j].TagHeadPosition;
                            double d = a.DistanceTo(b);
                            if (d >= minDist || d < 1e-9) continue;
                            XYZ dir = (b - a).Normalize();
                            double push = (minDist - d) * 0.5;
                            using (var t = new Transaction(doc, "Push"))
                            {
                                t.Start();
                                bool ok = tags[i].TrySetHeadPosition(a - dir * push) && tags[j].TrySetHeadPosition(b + dir * push);
                                if (ok) { t.Commit(); n++; any = true; } else t.RollBack();
                            }
                        }
                        if (!any) break;
                    }
                    tg.Assimilate();
                }
                TaskDialog.Show("Untangle", n + " adjustment(s).");
                return Result.Succeeded;
            }
            catch (Exception ex) { Logger.Error("Untangle failed", ex); message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class PinTagsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return PinUnpin(commandData, ref message, true);
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class UnpinTagsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return PinUnpin(commandData, ref message, false);
        }
    }

    public static class PinUnpinHelper
    {
        public static Result Run(ExternalCommandData commandData, ref string message, bool pinned)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null) { message = "No document."; return Result.Failed; }
            try
            {
                var tags = SelectionHelper.GetSelectedTags(uidoc, allowPick: true, pinned ? "Select to pin" : "Select to unpin");
                int n = 0;
                using (var t = new Transaction(doc, pinned ? "Pin" : "Unpin"))
                {
                    t.Start();
                    foreach (var tag in tags)
                    {
                        if (tag.Pinned == pinned) continue;
                        tag.Pinned = pinned;
                        n++;
                    }
                    t.Commit();
                }
                TaskDialog.Show(pinned ? "Pin" : "Unpin", n + " tag(s).");
                return Result.Succeeded;
            }
            catch (Exception ex) { Logger.Error("Pin/Unpin failed", ex); message = ex.Message; return Result.Failed; }
        }
    }

    // local helpers used by PinTagsCommand / UnpinTagsCommand
    static class PinLocal
    {
        // intentionally empty – PinTagsCommand calls PinUnpin below
    }

    // shared method in namespace scope is invalid; use class methods instead
}

namespace AlignTool.Commands
{
    public partial class PinTagsCommand
    {
        // already defined above
    }

    // Pin/Unpin implementation helpers via extension methods on commands
    internal static class PinUnpinBridge
    {
        public static Result PinUnpin(ExternalCommandData commandData, ref string message, bool pinned)
            => PinUnpinHelper.Run(commandData, ref message, pinned);
    }
}
