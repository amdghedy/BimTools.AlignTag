using System;
using AlignTool.Helpers;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AlignTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AlignTagOrientationCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null) { message = "No document."; return Result.Failed; }
            try
            {
                var tags = SelectionHelper.GetSelectedTags(uidoc, allowPick: true, "Select tags");
                if (tags.Count == 0) { TaskDialog.Show("Orient", "No tags selected."); return Result.Cancelled; }
                int n = 0;
                using (var t = new Transaction(doc, "Orient Tags"))
                {
                    t.Start();
                    foreach (var tag in tags)
                    {
                        try
                        {
                            // Force horizontal orientation when API allows
                            var prop = tag.GetType().GetProperty("TagOrientation");
                            if (prop != null && prop.CanWrite)
                            {
                                var horizontal = Enum.Parse(prop.PropertyType, "Horizontal");
                                prop.SetValue(tag, horizontal);
                                n++;
                            }
                        }
                        catch { }
                    }
                    t.Commit();
                }
                TaskDialog.Show("Orient", $"{n} tag(s) set horizontal.");
                return Result.Succeeded;
            }
            catch (Exception ex) { Logger.Error("Orient failed", ex); message = ex.Message; return Result.Failed; }
        }
    }
}
