using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using View = Autodesk.Revit.DB.View;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using TaskDialogCommonButtons = Autodesk.Revit.UI.TaskDialogCommonButtons;
using TaskDialogCommandLinkId = Autodesk.Revit.UI.TaskDialogCommandLinkId;
using TaskDialogResult = Autodesk.Revit.UI.TaskDialogResult;

namespace BIMTools.TagAlign.Leaders
{
    /// <summary>"Resolve Overlaps": nudge only the tags whose text boxes collide.</summary>
    [Transaction(TransactionMode.Manual)]
    public class ResolveOverlapsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View view = doc.ActiveView;
            if (!CommandHelper.ViewOk(view)) return Result.Cancelled;

            LeaderAlignSettings s = LeaderAlignSettings.Load();
            using (ResolveForm dlg = new ResolveForm(s))
            {
                if (dlg.ShowDialog(new RevitWindow()) != DialogResult.OK) return Result.Cancelled;
                s = dlg.Settings;
            }
            s.Save();

            List<Element> targets;
            try { targets = CommandHelper.GetTargets(uidoc, view, s.IncludeNotes, s.IncludeSpatial, s.AllInView, "Select the tags to de-clutter (or tick 'all tags in the view')"); }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            if (targets.Count == 0) return Result.Cancelled;

            LeaderAlignResult res;
            using (Transaction t = new Transaction(doc, "Resolve Overlaps"))
            {
                t.Start();
                res = OverlapEngine.Run(doc, view, targets, s);
                t.Commit();
            }
            CommandHelper.ReportOverlaps(res);
            return Result.Succeeded;
        }
    }

    /// <summary>"Flip Tag Side": put tags on the other side of their elements, or move a whole column across.</summary>
    [Transaction(TransactionMode.Manual)]
    public class FlipTagsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View view = doc.ActiveView;
            if (!CommandHelper.ViewOk(view)) return Result.Cancelled;

            FlipMode mode; bool spatial;
            using (FlipForm dlg = new FlipForm())
            {
                if (dlg.ShowDialog(new RevitWindow()) != DialogResult.OK) return Result.Cancelled;
                mode = dlg.Mode; spatial = dlg.IncludeSpatial;
            }

            List<Element> targets;
            try { targets = CommandHelper.GetTargets(uidoc, view, false, spatial, false, "Select the tags to flip"); }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            if (targets.Count == 0) return Result.Cancelled;

            LeaderAlignResult res;
            using (Transaction t = new Transaction(doc, "Flip Tag Side"))
            {
                t.Start();
                res = FlipTool.Run(doc, view, targets, mode, spatial);
                t.Commit();
            }
            CommandHelper.Report("Flip Tag Side", res, false);
            return Result.Succeeded;
        }
    }

    /// <summary>"Tag Audit": find untagged elements, duplicate tags and orphan tags in the active view.</summary>
    [Transaction(TransactionMode.Manual)]
    public class TagAuditCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View view = doc.ActiveView;
            if (!CommandHelper.ViewOk(view)) return Result.Cancelled;

            List<AuditCategory> cats = TagAudit.Categories(doc, view);
            bool untagged, dups, orphans; List<string> chosen;
            using (AuditForm dlg = new AuditForm(cats))
            {
                if (dlg.ShowDialog(new RevitWindow()) != DialogResult.OK) return Result.Cancelled;
                untagged = dlg.CheckUntagged; dups = dlg.CheckDuplicates; orphans = dlg.CheckOrphans; chosen = dlg.SelectedCategories;
            }

            AuditResult r = TagAudit.Run(doc, view, chosen, untagged, dups, orphans);

            string body = "Tags in this view: " + r.TagCount;
            if (untagged)
            {
                body += "\nElements checked: " + r.ElementCount + "   without a tag: " + r.Untagged.Count;
                foreach (KeyValuePair<string, int> kv in r.UntaggedByCategory.OrderByDescending(k => k.Value).Take(6))
                    body += "\n     " + kv.Key + ": " + kv.Value;
            }
            if (dups) body += "\nDuplicate tags: " + r.Duplicates.Count;
            if (orphans) body += "\nOrphan tags: " + r.Orphans.Count;
            body += "\n\nTags on elements of linked models are not counted as orphans, and linked elements are not checked.";

            int problems = r.Untagged.Count + r.Duplicates.Count + r.Orphans.Count;
            TaskDialog td = new TaskDialog("Tag Audit");
            td.MainInstruction = problems == 0 ? "Nothing to fix in this view" : problems + " finding(s) in this view";
            td.MainContent = body;
            if (r.Untagged.Count > 0) td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Select the " + r.Untagged.Count + " untagged element(s)",
                                                       "Then use Revit's Tag by Category / Tag All, and finish with Smart Tag Arrange.");
            if (r.Duplicates.Count > 0) td.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Select the " + r.Duplicates.Count + " duplicate tag(s)");
            if (r.Orphans.Count > 0) td.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "Select the " + r.Orphans.Count + " orphan tag(s)");
            if (r.Duplicates.Count + r.Orphans.Count > 0)
                td.AddCommandLink(TaskDialogCommandLinkId.CommandLink4, "Delete the duplicate and orphan tags", "One Undo step. The first tag of every duplicate set is kept.");
            td.CommonButtons = TaskDialogCommonButtons.Close;

            switch (td.Show())
            {
                case TaskDialogResult.CommandLink1: uidoc.Selection.SetElementIds(r.Untagged); break;
                case TaskDialogResult.CommandLink2: uidoc.Selection.SetElementIds(r.Duplicates); break;
                case TaskDialogResult.CommandLink3: uidoc.Selection.SetElementIds(r.Orphans); break;
                case TaskDialogResult.CommandLink4:
                    {
                        List<ElementId> del = r.Duplicates.Concat(r.Orphans).Distinct().ToList();
                        using (Transaction t = new Transaction(doc, "Delete duplicate and orphan tags"))
                        {
                            t.Start();
                            doc.Delete(del);
                            t.Commit();
                        }
                        TaskDialog.Show("Tag Audit", del.Count + " tag(s) deleted.");
                        break;
                    }
            }
            return Result.Succeeded;
        }
    }
}
