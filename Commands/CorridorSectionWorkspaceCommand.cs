using System;
using System.Linq;
using AlignTool.Helpers;
using AlignTool.Services;
using AlignTool.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AlignTool.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public sealed class CorridorSectionWorkspaceCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            if (uidoc == null || uidoc.Document == null)
            {
                message = "Open a Revit project before generating corridor sections.";
                return Result.Failed;
            }

            try
            {
                Document document = uidoc.Document;
                View view = document.ActiveView;
                var extraction = Revit3DCoordinationExtractionService.Extract(
                    document, view, uidoc.Selection.GetElementIds().ToList());
                if (extraction.Elements == null || extraction.Elements.Count == 0)
                {
                    TaskDialog.Show("ALIGN Corridor Sections",
                        "No supported MEP solids were found in the current selection or active view/loaded links.");
                    return Result.Cancelled;
                }

                new CorridorSectionWorkspaceWindow(document, view, extraction).ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Error("Multi-section corridor workspace failed", ex);
                message = "Multi-section corridor workspace failed: " + ex.Message;
                return Result.Failed;
            }
        }
    }
}