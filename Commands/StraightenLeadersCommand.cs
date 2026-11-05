using System;
using AlignTool.Helpers;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AlignTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class StraightenLeadersCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null) { message = "No document."; return Result.Failed; }
            try
            {
                var tags = SelectionHelper.GetSelectedTags(uidoc, allowPick: true, "Select tags");
                int n = 0;
                using (var t = new Transaction(doc, "Straighten Leaders"))
                {
                    t.Start();
                    foreach (var tag in tags)
                    {
                        try
                        {
                            // Toggle leader off/on to reset elbow if API supports
                            if (tag.HasLeader)
                            {
                                tag.LeaderEndCondition = LeaderEndCondition.Free;
                                n++;
                            }
                        }
                        catch { }
                    }
                    t.Commit();
                }
                TaskDialog.Show("Straighten", $"{n} leader(s) processed.");
                return Result.Succeeded;
            }
            catch (Exception ex) { Logger.Error("Straighten failed", ex); message = ex.Message; return Result.Failed; }
        }
    }
}
