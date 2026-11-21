using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using View = Autodesk.Revit.DB.View;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace BIMTools.TagAlign.Leaders
{
    class AnnotFilter : ISelectionFilter
    {
        readonly bool _notes, _spatial;
        public AnnotFilter(bool notes, bool spatial) { _notes = notes; _spatial = spatial; }
        public bool AllowElement(Element e) { return AnnotFactory.IsSupported(e, _notes, _spatial); }
        public bool AllowReference(Reference r, XYZ p) { return false; }
    }

    static class CommandHelper
    {
        public static bool ViewOk(View view)
        {
            switch (view.ViewType)
            {
                case ViewType.FloorPlan: case ViewType.CeilingPlan: case ViewType.EngineeringPlan:
                case ViewType.AreaPlan: case ViewType.Section: case ViewType.Elevation: case ViewType.Detail:
                    return true;
            }
            TaskDialog.Show("BIM TOOLS", "Open a plan, section or elevation view first.");
            return false;
        }

        /// <summary>Preselection -> use it; else every tag in the view (if asked); else "Select the tags to arrange".</summary>
        public static List<Element> GetTargets(UIDocument uidoc, View view, bool notes, bool spatial, bool allInView, string prompt)
        {
            Document doc = uidoc.Document;
            if (allInView) return AnnotFactory.AllInView(doc, view, notes, spatial);
            List<Element> pre = uidoc.Selection.GetElementIds().Select(id => doc.GetElement(id))
                                     .Where(e => AnnotFactory.IsSupported(e, notes, spatial)).ToList();
            if (pre.Count > 0) return pre;
            IList<Reference> refs = uidoc.Selection.PickObjects(ObjectType.Element, new AnnotFilter(notes, spatial), prompt);
            return refs.Select(r => doc.GetElement(r)).ToList();
        }

        public static void EnsureWorkPlane(Document doc, View view)
        {
            if (view.SketchPlane != null) return;
            using (Transaction t = new Transaction(doc, "Set work plane"))
            {
                t.Start();
                view.SketchPlane = SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(view.ViewDirection, view.Origin));
                t.Commit();
            }
        }

        static string Arrow(double before, double after, string unit)
        {
            string t = Math.Round(before) + " " + unit + "  ->  " + Math.Round(after) + " " + unit;
            if (before > 0.5) t += "   (" + (after <= before ? "-" : "+") + Math.Abs(Math.Round((after - before) / before * 100)) + "%)";
            return t;
        }

        public static void Report(string title, LeaderAlignResult res, bool showCrossings)
        {
            string msg = string.Format("Processed: {0}\nSkipped: {1}", res.Aligned, res.Skipped);
            if (showCrossings)
            {
                if (res.CrossingsBefore >= 0) msg += "\nLeader crossings: " + res.CrossingsBefore + "  ->  " + res.Crossings;
                else msg += "\nLeader crossings left: " + res.Crossings;
                if (res.OverlapsBefore >= 0 && res.OverlapsAfter >= 0) msg += "\nOverlapping tags: " + res.OverlapsBefore + "  ->  " + res.OverlapsAfter;
                if (res.LenBeforeMm > 0 && res.LenAfterMm >= 0) msg += "\nLeader length on sheet: " + Arrow(res.LenBeforeMm, res.LenAfterMm, "mm");
                if (res.Groups > 1) msg += "\nGroups: " + res.Groups;
            }
            if (res.ElbowFailures > 0) msg += "\nElbow could not be set on " + res.ElbowFailures + " item(s).";
            if (res.Notes.Count > 0) msg += "\n\n" + string.Join("\n", res.Notes.Take(8));
            TaskDialog.Show(title, msg);
        }

        public static void ReportOverlaps(LeaderAlignResult res)
        {
            string msg = "Tags moved: " + res.Aligned + "\nSkipped: " + res.Skipped;
            if (res.OverlapsBefore >= 0) msg += "\nOverlapping pairs: " + res.OverlapsBefore + "  ->  " + res.OverlapsAfter;
            if (res.OverlapsAfter > 0) msg += "\n\n" + res.OverlapsAfter + " overlap(s) remain because the tags reached the maximum move distance. " +
                                              "Raise that limit, or use Smart Tag Arrange for a crowded group.";
            if (res.Notes.Count > 0) msg += "\n\n" + string.Join("\n", res.Notes.Take(8));
            TaskDialog.Show("Resolve Overlaps", msg);
        }
    }

    /// <summary>"Smart Tag Arrange": layout dialog -> select tags -> click the column start (and optionally its end).</summary>
    [Transaction(TransactionMode.Manual)]
    public class AlignTagsAutoCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View view = doc.ActiveView;
            if (!CommandHelper.ViewOk(view)) return Result.Cancelled;

            LeaderAlignSettings s = LeaderAlignSettings.Load();
            using (AlignOptionsForm dlg = new AlignOptionsForm(s))
            {
                if (dlg.ShowDialog(new RevitWindow()) != DialogResult.OK) return Result.Cancelled;
                s = dlg.Settings;
            }
            s.Save();

            List<Element> targets;
            try
            {
                targets = CommandHelper.GetTargets(uidoc, view, s.IncludeNotes, s.IncludeSpatial, s.AllInView, "Select the tags to arrange");
                if (targets.Count == 0) return Result.Cancelled;
                s.ReferenceHead = null; s.LowestHead = null; s.HighestHead = null;
                if (s.UseReferenceTag)
                {
                    Reference rr = uidoc.Selection.PickObject(ObjectType.Element, new AnnotFilter(s.IncludeNotes, s.IncludeSpatial),
                                                              "Pick the existing tag whose column you want to continue upward");
                    Element refEl = doc.GetElement(rr);
                    List<Annot> ra = AnnotFactory.Build(new List<Element> { refEl }, true, true);
                    XYZ rh, re, rel;
                    if (ra.Count == 0 || !ra[0].Read(out rh, out re, out rel)) { TaskDialog.Show("Smart Tag Arrange", "The picked element has no readable tag position."); return Result.Cancelled; }
                    s.ReferenceHead = rh;
                    targets.RemoveAll(e => e.Id.Equals(refEl.Id));
                    if (targets.Count == 0) return Result.Cancelled;
                }
                else if (!s.AutoColumn)
                {
                    CommandHelper.EnsureWorkPlane(doc, view);
                    s.LowestHead = uidoc.Selection.PickPoint(ObjectSnapTypes.None, "Click where the first (bottom) row should start");
                    s.HighestHead = s.DistributeBetweenPicks
                        ? uidoc.Selection.PickPoint(ObjectSnapTypes.None, "Click where the last (top) row should end")
                        : null;
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }

            LeaderAlignResult res;
            using (Transaction t = new Transaction(doc, "Smart Tag Arrange"))
            {
                t.Start();
                res = LeaderAlignEngine.Run(doc, view, targets, s);
                t.Commit();
            }
            if (!s.DetailedReport) { res.CrossingsBefore = -1; res.OverlapsBefore = -1; res.OverlapsAfter = -1; res.LenBeforeMm = -1; res.Groups = 0; }
            CommandHelper.Report("Smart Tag Arrange", res, true);
            return Result.Succeeded;
        }
    }

    /// <summary>"Leader Tuner": change angle / landing / straighten leaders in place (tag heads do not move).</summary>
    [Transaction(TransactionMode.Manual)]
    public class LeaderTunerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View view = doc.ActiveView;
            if (!CommandHelper.ViewOk(view)) return Result.Cancelled;

            LeaderAlignSettings s = LeaderAlignSettings.Load();
            using (TunerForm dlg = new TunerForm(s))
            {
                if (dlg.ShowDialog(new RevitWindow()) != DialogResult.OK) return Result.Cancelled;
                s = dlg.Settings;
            }
            s.Save();

            List<Element> targets;
            try { targets = CommandHelper.GetTargets(uidoc, view, false, s.IncludeSpatial, s.AllInView, "Select the tags to re-shape"); }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            if (targets.Count == 0) return Result.Cancelled;

            LeaderAlignResult res;
            using (Transaction t = new Transaction(doc, "Leader Tuner"))
            {
                t.Start();
                res = AnnotTools.Retune(doc, view, targets, s);
                t.Commit();
            }
            CommandHelper.Report("Leader Tuner", res, false);
            return Result.Succeeded;
        }
    }

    /// <summary>"Align Tag Heads": align left/center/right/top/middle/bottom or distribute selected heads.</summary>
    [Transaction(TransactionMode.Manual)]
    public class AlignHeadsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View view = doc.ActiveView;
            if (!CommandHelper.ViewOk(view)) return Result.Cancelled;

            HeadAlign mode; bool keep, notes, spatial;
            using (HeadAlignForm dlg = new HeadAlignForm())
            {
                if (dlg.ShowDialog(new RevitWindow()) != DialogResult.OK) return Result.Cancelled;
                mode = dlg.Mode; keep = dlg.KeepLeaderShape; notes = dlg.IncludeNotes; spatial = dlg.IncludeSpatial;
            }

            List<Element> targets;
            try { targets = CommandHelper.GetTargets(uidoc, view, notes, spatial, false, "Select the tags to align"); }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            if (targets.Count == 0) return Result.Cancelled;

            LeaderAlignResult res;
            using (Transaction t = new Transaction(doc, "Align Tag Heads"))
            {
                t.Start();
                res = AnnotTools.AlignHeads(doc, view, targets, mode, keep, notes, spatial);
                t.Commit();
            }
            CommandHelper.Report("Align Tag Heads", res, false);
            return Result.Succeeded;
        }
    }
}
