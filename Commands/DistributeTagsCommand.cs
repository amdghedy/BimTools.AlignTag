using System;
using AlignTool.Helpers;
using AlignTool.Services;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AlignTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class DistributeTagsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            View view = doc?.ActiveView;
            if (doc == null) { message = "No document."; return Result.Failed; }
            try
            {
                var tags = SelectionHelper.GetSelectedTags(uidoc, allowPick: true, "Select tags");
                var result = new AlignBoxService(doc, view).Distribute(tags, horizontal: true);
                TaskDialog.Show("Distribute", result.Message);
                return result.Success ? Result.Succeeded : Result.Failed;
            }
            catch (Exception ex) { Logger.Error("Distribute failed", ex); message = ex.Message; return Result.Failed; }
        }
    }
}
