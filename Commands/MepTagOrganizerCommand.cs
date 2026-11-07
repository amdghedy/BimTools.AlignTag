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
                    TaskDialog.Show("Composite MEP Tag Layout",
                        "Select at least two supported MEP tags, or run the command in a composite view containing tagged MEP elements.");
                    return Result.Cancelled;
                }

                MepTagOrganizerService.ResultSummary plan =
                    MepTagOrganizerService.BuildPlan(doc, doc.ActiveView, tags);
                if (plan.Plan.Count == 0)
                {
                    TaskDialog.Show("Composite MEP Tag Layout",
                        "No movable supported MEP tags were found. Pinned tags are skipped.");
                    return Result.Cancelled;
                }

                TaskDialogResult answer = TaskDialog.Show("Composite MEP Tag Layout - Preview",
                    MepTagOrganizerService.Describe(plan) + "\n\nApply this drawing-wide layout?",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
                if (answer != TaskDialogResult.Yes) return Result.Cancelled;

                MepTagOrganizerService.Apply(doc, plan);
                TaskDialog.Show("Composite MEP Tag Layout - Result",
                    "Tag heads moved: " + plan.Moved +
                    "\nLeader elbows adjusted: " + plan.ElbowsSet +
                    "\nLeader elbows unavailable: " + plan.ElbowFailures +
                    "\nFailed tags: " + plan.Failed +
                    "\n\nReview the final view. The complete operation is one Revit transaction and can be undone.");
                return plan.Failed == 0 ? Result.Succeeded : Result.Failed;
            }
            catch (Exception ex)
            {
                AlignTool.Helpers.Logger.Error("Composite MEP Tag Layout failed", ex);
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
                IList<IndependentTag> tags = MepTagOrganizerService.GetMepTagsInView(
                    uidoc.Document, uidoc.Document.ActiveView);
                uidoc.Selection.SetElementIds(tags.Select(t => t.Id).ToList());
                TaskDialog.Show("MEP Select", tags.Count + " supported MEP tags selected, including tags referencing linked MEP models.");
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
                IList<IndependentTag> tags = MepTagOrganizerService.GetMepTagsInView(
                    uidoc.Document, uidoc.Document.ActiveView);
                int pinned = tags.Count(t => t.Pinned);
                int leaderless = tags.Count(t => { try { return !t.HasLeader; } catch { return true; } });
                int linked = tags.Count(t =>
                {
                    Reference r = MepTagOrganizerService.GetMepLeaderReference(t, uidoc.Document);
                    RevitLinkInstance link;
                    MepTagOrganizerService.ResolveReferencedHost(uidoc.Document, r, out link);
                    return link != null;
                });
                TaskDialog.Show("MEP Tag Audit",
                    "Supported MEP tags: " + tags.Count +
                    "\nLinked-model tags: " + linked +
                    "\nNative-model tags: " + (tags.Count - linked) +
                    "\nPinned: " + pinned +
                    "\nLeaderless: " + leaderless);
                return Result.Succeeded;
            }
            catch (Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }
}
