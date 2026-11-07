using System;
using System.Collections.Generic;
using System.Linq;
using AlignTool.Services;
using AlignTool.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AlignTool.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public sealed class IntelligentCsdOptimizerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            if (uidoc == null || uidoc.Document == null)
            {
                message = "Open a Revit project before running the CSD optimizer.";
                return Result.Failed;
            }

            try
            {
                Document doc = uidoc.Document;
                IList<ElementId> selection = uidoc.Selection.GetElementIds().ToList();
                RevitCsdExtractionService.ExtractionResult extraction =
                    RevitCsdExtractionService.Extract(doc, doc.ActiveView, selection);
                if (extraction.Items.Count == 0 && selection.Count > 0)
                    extraction = RevitCsdExtractionService.Extract(doc, doc.ActiveView, null);

                if (extraction.Items.Count == 0)
                {
                    TaskDialog.Show("Intelligent CSD Optimizer",
                        "No supported native MEP elements were found in the selection or active view. " +
                        "Supported categories include ducts, pipes, fittings, accessories, cable trays, conduits and MEP equipment. " +
                        "Linked-model extraction is not included in this first adapter.");
                    return Result.Cancelled;
                }

                double minY = extraction.MinYmm;
                foreach (KnowledgeBasedCsdEngine.ServiceItem item in extraction.Items)
                    item.PreferredElevationMm = Math.Max(0, item.PreferredElevationMm - minY);

                var constraints = new KnowledgeBasedCsdEngine.SectionConstraints
                {
                    WidthMm = Math.Max(1000, extraction.MaxXmm - extraction.MinXmm + 600),
                    HeightMm = Math.Max(1000, extraction.MaxYmm - extraction.MinYmm + 600),
                    SideClearanceMm = 100,
                    TopClearanceMm = 100,
                    BottomClearanceMm = 100,
                    DefaultServiceGapMm = 50,
                    GridMm = 25,
                    WetServicesAboveElectricalPenalty = true
                };

                KnowledgeBasedCsdEngine.SolveResult solve = KnowledgeBasedCsdEngine.Solve(
                    extraction.Items, constraints, new KnowledgeBasedCsdEngine.Weights());
                if (solve.Candidates == null || solve.Candidates.Count == 0)
                {
                    TaskDialog.Show("Intelligent CSD Optimizer", "No arrangement candidates could be generated.");
                    return Result.Cancelled;
                }

                var window = new CsdOptimizerWindow(extraction, solve, constraints);
                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                AlignTool.Helpers.Logger.Error("Intelligent CSD Optimizer failed", ex);
                message = "CSD analysis failed: " + ex.Message;
                return Result.Failed;
            }
        }
    }
}