using System;
using System.Linq;
using AlignTool.Services;
using AlignTool.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AlignTool.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public sealed class Revit3DCoordinationCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            if (uidoc == null || uidoc.Document == null)
            {
                message = "Open a Revit project before running 3D MEP coordination.";
                return Result.Failed;
            }
            try
            {
                var result = Revit3DCoordinationExtractionService.Extract(
                    uidoc.Document, uidoc.Document.ActiveView, uidoc.Selection.GetElementIds().ToList());
                if (result.Elements.Count == 0)
                {
                    TaskDialog.Show("ALIGN 3D Coordination",
                        "No supported MEP elements were found in the current selection or active view/loaded links.");
                    return Result.Cancelled;
                }
                new Revit3DCoordinationWindow(result).ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                AlignTool.Helpers.Logger.Error("3D MEP coordination extraction failed", ex);
                message = "3D MEP coordination failed: " + ex.Message;
                return Result.Failed;
            }
        }
    }
}