using System;
using System.Collections.Generic;
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
                TaskDialog.Show("To Line", $"{n} tag(s) projected.");
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
                TaskDialog.Show("Untangle", $"{n} adjustment(s).");
                return Result.Succeeded;
            }
            catch (Exception ex) { Logger.Error("Untangle failed", ex); message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class PinTagsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => SetPinned(c, ref m, true);
    }

    [Transaction(TransactionMode.Manual)]
    public class UnpinTagsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => SetPinned(c, ref m, false);
    }

    static class PinHelper
    {
        public static Result SetPinned(ExternalCommandData commandData, ref string message, bool pinned)
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
                TaskDialog.Show(pinned ? "Pin" : "Unpin", $"{n} tag(s).");
                return Result.Succeeded;
            }
            catch (Exception ex) { Logger.Error("Pin/Unpin failed", ex); message = ex.Message; return Result.Failed; }
        }
    }

    // bridge so PinTagsCommand can call SetPinned without nested class visibility issues
    static Result SetPinned(ExternalCommandData c, ref string m, bool pinned) => PinHelper.SetPinned(c, ref m, pinned);

    [Transaction(TransactionMode.Manual)]
    public class ArrangeLeftCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => ArrangeSide(c, ref m, true);
    }

    [Transaction(TransactionMode.Manual)]
    public class ArrangeRightCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => ArrangeSide(c, ref m, false);
    }

    static Result ArrangeSide(ExternalCommandData commandData, ref string message, bool left)
    {
        UIDocument uidoc = commandData.Application.ActiveUIDocument;
        Document doc = uidoc?.Document;
        View view = doc?.ActiveView;
        if (doc == null) { message = "No document."; return Result.Failed; }
        try
        {
            var tags = SelectionHelper.GetSelectedTags(uidoc, allowPick: false);
            if (tags.Count == 0) tags = SelectionHelper.GetAllTagsInView(doc, view).ToList();
            var ordered = tags.Where(t => !t.Pinned).OrderBy(t =>
            {
                XYZ end = TagInfo.GetLeaderEndSafe(t, doc, view, out _);
                return end != null ? end.Y : t.TagHeadPosition.Y;
            }).ToList();
            if (ordered.Count == 0) { TaskDialog.Show("Arrange", "No movable tags."); return Result.Cancelled; }
            double spacing = UnitHelper.MmToFeet(45);
            double avgX = ordered.Average(t => t.TagHeadPosition.X);
            double columnX = left ? avgX - UnitHelper.MmToFeet(400) : avgX + UnitHelper.MmToFeet(400);
            double midY = ordered.Average(t => t.TagHeadPosition.Y);
            double startY = midY - (ordered.Count - 1) * spacing * 0.5;
            int n = 0;
            using (var tg = new TransactionGroup(doc, left ? "Arrange Left" : "Arrange Right"))
            {
                tg.Start();
                for (int i = 0; i < ordered.Count; i++)
                {
                    XYZ dest = new XYZ(columnX, startY + i * spacing, ordered[i].TagHeadPosition.Z);
                    using (var t = new Transaction(doc, "Arrange"))
                    {
                        t.Start();
                        if (ordered[i].TrySetHeadPosition(dest)) { t.Commit(); n++; }
                        else t.RollBack();
                    }
                }
                tg.Assimilate();
            }
            TaskDialog.Show("Arrange", $"{n} tag(s) arranged.");
            return Result.Succeeded;
        }
        catch (Exception ex) { Logger.Error("Arrange failed", ex); message = ex.Message; return Result.Failed; }
    }

    [Transaction(TransactionMode.Manual)]
    public class EqualOffsetCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Reuse MatchLeaderLengths behaviour
            return new MatchLeaderLengthsCommand().Execute(commandData, ref message, elements);
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class ParallelLeadersCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            View view = doc?.ActiveView;
            if (doc == null) { message = "No document."; return Result.Failed; }
            try
            {
                var tags = SelectionHelper.GetSelectedTags(uidoc, allowPick: true, "Select tags (first = master)");
                if (tags.Count < 2) { TaskDialog.Show("Parallel", "Select at least two tags."); return Result.Cancelled; }
                XYZ masterEnd = TagInfo.GetLeaderEndSafe(tags[0], doc, view, out _);
                if (masterEnd == null) { TaskDialog.Show("Parallel", "Master has no end."); return Result.Failed; }
                XYZ masterDir = tags[0].TagHeadPosition - masterEnd;
                if (masterDir.GetLength() < 1e-9) { TaskDialog.Show("Parallel", "Zero length."); return Result.Failed; }
                masterDir = masterDir.Normalize();
                int n = 0;
                using (var tg = new TransactionGroup(doc, "Parallel Leaders"))
                {
                    tg.Start();
                    for (int i = 1; i < tags.Count; i++)
                    {
                        if (tags[i].Pinned) continue;
                        XYZ end = TagInfo.GetLeaderEndSafe(tags[i], doc, view, out _);
                        if (end == null) continue;
                        double len = tags[i].TagHeadPosition.DistanceTo(end);
                        if (len < 1e-6) len = UnitHelper.MmToFeet(150);
                        XYZ dest = end + masterDir * len;
                        using (var t = new Transaction(doc, "Parallel"))
                        {
                            t.Start();
                            if (tags[i].TrySetHeadPosition(dest)) { t.Commit(); n++; }
                            else t.RollBack();
                        }
                    }
                    tg.Assimilate();
                }
                TaskDialog.Show("Parallel", $"{n} leader(s) parallel.");
                return Result.Succeeded;
            }
            catch (Exception ex) { Logger.Error("Parallel failed", ex); message = ex.Message; return Result.Failed; }
        }
    }
}
