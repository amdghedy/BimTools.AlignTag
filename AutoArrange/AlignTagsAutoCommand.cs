using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using View = Autodesk.Revit.DB.View;

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

        /// <summary>Preselection -> use it; else every tag in the view (if asked); else "Select Tags To Be Aligned".</summary>
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

        public static void Report(string title, LeaderAlignResult res, bool showCrossings)
        {
            string msg = string.Format("Processed: {0}\nSkipped: {1}", res.Aligned, res.Skipped);
            if (showCrossings) msg += "\nLeader crossings left: " + res.Crossings;
            if (res.ElbowFailures > 0) msg += "\nElbow could not be set on " + res.ElbowFailures + " item(s).";
            if (res.Notes.Count > 0) msg += "\n\n" + string.Join("\n", res.Notes.Take(8));
            TaskDialog.Show(title, msg);
        }
    }

    /// <summary>"Align Tags Automatically": Choose Angle -> select tags -> Pick Lowest / Highest Tag Head.</summary>
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
                targets = CommandHelper.GetTargets(uidoc, view, s.IncludeNotes, s.IncludeSpatial, s.AllInView, "Select Tags To Be Aligned");
                if (targets.Count == 0) return Result.Cancelled;
                if (!s.AutoColumn)
                {
                    CommandHelper.EnsureWorkPlane(doc, view);
                    s.LowestHead = uidoc.Selection.PickPoint(ObjectSnapTypes.None, "Pick Lowest Tag Head");
                    s.HighestHead = uidoc.Selection.PickPoint(ObjectSnapTypes.None, "Pick Highest Tag Head");
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }

            LeaderAlignResult res;
            using (Transaction t = new Transaction(doc, "Align Tags Automatically"))
            {
                t.Start();
                res = LeaderAlignEngine.Run(doc, view, targets, s);
                t.Commit();
            }
            CommandHelper.Report("Align Tags Automatically", res, true);
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
            try { targets = CommandHelper.GetTargets(uidoc, view, false, s.IncludeSpatial, s.AllInView, "Select Tags To Re-shape"); }
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
            try { targets = CommandHelper.GetTargets(uidoc, view, notes, spatial, false, "Select Tags To Align"); }
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

