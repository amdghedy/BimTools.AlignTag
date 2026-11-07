using System;
using AlignTool.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AlignTool.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public sealed class ClearanceProfileEditorCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Document document = commandData.Application.ActiveUIDocument == null
                    ? null : commandData.Application.ActiveUIDocument.Document;
                if (document == null)
                {
                    TaskDialog.Show("ALIGN", "Open a Revit project before editing clearance profiles.");
                    return Result.Cancelled;
                }
                var window = new ClearanceProfileWindow(document.Title);
                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("ALIGN - Clearance Profiles", ex.ToString());
                return Result.Failed;
            }
        }
    }
}
