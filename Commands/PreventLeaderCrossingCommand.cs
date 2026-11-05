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
    public class PreventLeaderCrossingCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            View view = doc?.ActiveView;
            if (doc == null || view == null) { message = "No document."; return Result.Failed; }

            try
            {
                var tags = SelectionHelper.GetSelectedTags(uidoc, allowPick: false);
                if (tags.Count == 0)
                    tags = SelectionHelper.GetAllTagsInView(doc, view).ToList();
                if (tags.Count < 2)
                {
                    TaskDialog.Show("Stack / Uncross", "Need at least two tags.");
                    return Result.Cancelled;
                }

                var service = new LeaderCrossingService(doc, view);
                AlignmentResult result = service.ResolveCrossings(tags);
                TaskDialog.Show("Stack / Uncross", result.Message);
                return result.Success ? Result.Succeeded : Result.Failed;
            }
            catch (Exception ex)
            {
                Logger.Error("PreventLeaderCrossing failed", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
