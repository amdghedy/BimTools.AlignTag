using System;
using AlignTool.Helpers;
using AlignTool.Models;
using AlignTool.Services;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AlignTool.Commands
{
    internal static class ClassicAlignRunner
    {
        public static Result Run(ExternalCommandData commandData, ref string message,
            AlignBoxService.AlignMode? alignMode, bool? distributeHorizontal, string title)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            View view = doc?.ActiveView;
            if (doc == null || view == null)
            {
                message = "No active document or view.";
                return Result.Failed;
            }

            try
            {
                var tags = SelectionHelper.GetSelectedTags(uidoc, allowPick: true, "Select tags");
                if (tags.Count == 0)
                {
                    TaskDialog.Show(title, "No tags selected.");
                    return Result.Cancelled;
                }

                var service = new AlignBoxService(doc, view);
                AlignmentResult result;

                if (alignMode.HasValue)
                    result = service.Align(tags, alignMode.Value);
                else
                    result = service.Distribute(tags, distributeHorizontal == true);

                TaskDialog.Show(title, result.Message);
                return result.Success ? Result.Succeeded : Result.Failed;
            }
            catch (Exception ex)
            {
                Logger.Error(title + " failed", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class AlignLeftCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData c, ref string m, ElementSet e)
            => ClassicAlignRunner.Run(c, ref m, AlignBoxService.AlignMode.Left, null, "Align Left");
    }

    [Transaction(TransactionMode.Manual)]
    public class AlignRightCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData c, ref string m, ElementSet e)
            => ClassicAlignRunner.Run(c, ref m, AlignBoxService.AlignMode.Right, null, "Align Right");
    }

    [Transaction(TransactionMode.Manual)]
    public class AlignTopCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData c, ref string m, ElementSet e)
            => ClassicAlignRunner.Run(c, ref m, AlignBoxService.AlignMode.Top, null, "Align Top");
    }

    [Transaction(TransactionMode.Manual)]
    public class AlignBottomCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData c, ref string m, ElementSet e)
            => ClassicAlignRunner.Run(c, ref m, AlignBoxService.AlignMode.Bottom, null, "Align Bottom");
    }

    [Transaction(TransactionMode.Manual)]
    public class AlignCenterCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData c, ref string m, ElementSet e)
            => ClassicAlignRunner.Run(c, ref m, AlignBoxService.AlignMode.Center, null, "Align Center");
    }

    [Transaction(TransactionMode.Manual)]
    public class AlignMiddleCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData c, ref string m, ElementSet e)
            => ClassicAlignRunner.Run(c, ref m, AlignBoxService.AlignMode.Middle, null, "Align Middle");
    }

    [Transaction(TransactionMode.Manual)]
    public class DistributeHorizontalCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData c, ref string m, ElementSet e)
            => ClassicAlignRunner.Run(c, ref m, null, true, "Distribute Horizontal");
    }

    [Transaction(TransactionMode.Manual)]
    public class DistributeVerticalCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData c, ref string m, ElementSet e)
            => ClassicAlignRunner.Run(c, ref m, null, false, "Distribute Vertical");
    }
}
