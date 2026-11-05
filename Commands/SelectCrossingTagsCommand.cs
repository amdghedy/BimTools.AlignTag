using System;
using System.Collections.Generic;
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
    public class SelectCrossingTagsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            View view = doc?.ActiveView;
            if (doc == null) { message = "No document."; return Result.Failed; }
            try
            {
                var tags = SelectionHelper.GetAllTagsInView(doc, view).ToList();
                var ends = tags.Select(t => new
                {
                    Tag = t,
                    Head = t.TagHeadPosition,
                    End = TagInfo.GetLeaderEndSafe(t, doc, view, out _)
                }).Where(x => x.End != null).ToList();

                var crossing = new HashSet<ElementId>();
                for (int i = 0; i < ends.Count; i++)
                {
                    for (int j = i + 1; j < ends.Count; j++)
                    {
                        if (LeaderGeometryService.SegmentsCross(ends[i].Head, ends[i].End, ends[j].Head, ends[j].End))
                        {
                            crossing.Add(ends[i].Tag.Id);
                            crossing.Add(ends[j].Tag.Id);
                        }
                    }
                }

                if (crossing.Count == 0)
                {
                    TaskDialog.Show("Sel Cross", "No crossing leaders found.");
                    return Result.Succeeded;
                }

                uidoc.Selection.SetElementIds(crossing.ToList());
                TaskDialog.Show("Sel Cross", $"{crossing.Count} tag(s) with crossing leaders selected.");
                return Result.Succeeded;
            }
            catch (Exception ex) { Logger.Error("SelectCrossing failed", ex); message = ex.Message; return Result.Failed; }
        }
    }
}
