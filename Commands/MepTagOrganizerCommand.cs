using System;
using System.Collections.Generic;
using System.Linq;
using AlignTool.Services;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AlignTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class MepTagOrganizerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            Document doc = uidoc == null ? null : uidoc.Document;
            if (doc == null) { message = "No active Revit document."; return Result.Failed; }
            try
            {
                IList<IndependentTag> tags = uidoc.Selection.GetElementIds()
                    .Select(id => doc.GetElement(id)).OfType<IndependentTag>()
                    .Where(t => MepTagOrganizerService.GetMepLeaderReference(t, doc) != null).ToList();
                if (tags.Count == 0)
                    tags = MepTagOrganizerService.GetMepTagsInView(doc, doc.ActiveView);
                if (tags.Count < 2)
                {
                    TaskDialog.Show("MEP Tag Organizer", "Select at least two duct, pipe or cable-tray tags, or run the command in a view containing at least two supported MEP tags.");
                    return Result.Cancelled;
                }

                var plan = MepTagOrganizerService.BuildPlan(doc, doc.ActiveView, tags);
                if (plan.Plan.Count == 0)
                {
                    TaskDialog.Show("MEP Tag Organizer", "No movable supported MEP tags were found. Check whether the tags are pinned or their hosts are unsupported.");
                    return Result.Cancelled;
                }

                TaskDialogResult answer = TaskDialog.Show("MEP Tag Organizer - Preview",
                    MepTagOrganizerService.Describe(plan) + "\n\nApply this arrangement?", TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
                if (answer != TaskDialogResult.Yes) return Result.Cancelled;

                MepTagOrganizerService.Apply(doc, plan);
                TaskDialog.Show("MEP Tag Organizer",
                    "Tag heads moved: " + plan.Moved +
                    "\nLeader elbows adjusted: " + plan.ElbowsSet +
                    "\nFailed tags: " + plan.Failed +
                    "\n\nPlease inspect the view and run again if further cleanup is needed.");
                return plan.Failed == 0 ? Result.Succeeded : Result.Failed;
            }
            catch (Exception ex)
            {
                AlignTool.Helpers.Logger.Error("MEP Tag Organizer failed", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class MepSelectTagsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            if (uidoc == null) { message = "No active Revit document."; return Result.Failed; }
            try
            {
                IList<IndependentTag> tags = MepTagOrganizerService.GetMepTagsInView(uidoc.Document, uidoc.Document.ActiveView);
                uidoc.Selection.SetElementIds(tags.Select(t => t.Id).ToList());
                TaskDialog.Show("MEP Select", tags.Count + " supported MEP tag(s) selected.");
                return Result.Succeeded;
            }
            catch (Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class MepTagAuditCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            if (uidoc == null) { message = "No active Revit document."; return Result.Failed; }
            try
            {
                IList<IndependentTag> tags = MepTagOrganizerService.GetMepTagsInView(uidoc.Document, uidoc.Document.ActiveView);
                int pinned = tags.Count(t => t.Pinned);
                int leaderless = tags.Count(t => { try { return !t.HasLeader; } catch { return true; } });
                TaskDialog.Show("MEP Tag Audit",
                    "Supported MEP tags: " + tags.Count +
                    "\nPinned: " + pinned +
                    "\nLeaderless: " + leaderless +
                    "\nMovable with leaders: " + (tags.Count - pinned - leaderless));
                return Result.Succeeded;
            }
            catch (Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }
}
