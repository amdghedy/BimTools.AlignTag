using System;
using AlignTool.Helpers;
using AlignTool.Services;
using Autodesk.Revit.UI;

namespace AlignTool
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                Logger.Initialise(SettingsService.Instance.Current);
                Logger.Info("AlignTool v2.3 starting...");

                RibbonPanel pAlign = RibbonHelper.GetOrCreatePanel(application, "BIM TOOLS", "ALIGN");
                RibbonHelper.AddStackedTriple(pAlign,
                    RibbonHelper.CreatePushButtonData("AlignLeft", "Left", "AlignTool.Commands.AlignLeftCommand", "Align heads to leftmost X.", null, "align_left"),
                    RibbonHelper.CreatePushButtonData("AlignCenter", "Center", "AlignTool.Commands.AlignCenterCommand", "Align heads to horizontal center.", null, "align_center"),
                    RibbonHelper.CreatePushButtonData("AlignRight", "Right", "AlignTool.Commands.AlignRightCommand", "Align heads to rightmost X.", null, "align_right"));
                RibbonHelper.AddStackedTriple(pAlign,
                    RibbonHelper.CreatePushButtonData("AlignTop", "Top", "AlignTool.Commands.AlignTopCommand", "Align heads to topmost Y.", null, "align_top"),
                    RibbonHelper.CreatePushButtonData("AlignMiddle", "Middle", "AlignTool.Commands.AlignMiddleCommand", "Align heads to vertical middle.", null, "align_middle"),
                    RibbonHelper.CreatePushButtonData("AlignBottom", "Bottom", "AlignTool.Commands.AlignBottomCommand", "Align heads to bottommost Y.", null, "align_bottom"));
                RibbonHelper.AddStackedTriple(pAlign,
                    RibbonHelper.CreatePushButtonData("DistH", "Dist H", "AlignTool.Commands.DistributeHorizontalCommand", "Distribute evenly horizontally.", null, "dist_h"),
                    RibbonHelper.CreatePushButtonData("DistV", "Dist V", "AlignTool.Commands.DistributeVerticalCommand", "Distribute evenly vertically.", null, "dist_v"),
                    RibbonHelper.CreatePushButtonData("AlignToLine", "To Line", "AlignTool.Commands.AlignToLineCommand", "Project tag heads onto a picked line/grid.", null, "align_line"));

                RibbonPanel pArr = RibbonHelper.GetOrCreatePanel(application, "BIM TOOLS", "ALIGN");
                RibbonHelper.AddStackedTriple(pArr,
                    RibbonHelper.CreatePushButtonData("PreventLeaderCrossing", "Stack", "AlignTool.Commands.PreventLeaderCrossingCommand", "Stack by host order, remove crossings.", null, "stack"),
                    RibbonHelper.CreatePushButtonData("ArrangeLeft", "Left Col", "AlignTool.Commands.ArrangeLeftCommand", "Stack selected/all tags on left of view.", null, "arrange_left"),
                    RibbonHelper.CreatePushButtonData("ArrangeRight", "Right Col", "AlignTool.Commands.ArrangeRightCommand", "Stack selected/all tags on right of view.", null, "arrange_right"));
                RibbonHelper.AddStackedTriple(pArr,
                    RibbonHelper.CreatePushButtonData("UntangleTags", "Untangle", "AlignTool.Commands.UntangleTagsCommand", "Push overlapping tag heads apart.", null, "untangle"),
                    RibbonHelper.CreatePushButtonData("FlipTagsSide", "Flip", "AlignTool.Commands.FlipTagsSideCommand", "Mirror tags to other side of host.", null, "flip_side"),
                    RibbonHelper.CreatePushButtonData("ParallelLeaders", "Parallel", "AlignTool.Commands.ParallelLeadersCommand", "Make leaders parallel to first selected.", null, "parallel"));
                RibbonHelper.AddStackedTriple(pArr,
                    RibbonHelper.CreatePushButtonData("EqualOffset", "Eq Offset", "AlignTool.Commands.EqualOffsetCommand", "Same offset distance from host.", null, "equal_offset"),
                    RibbonHelper.CreatePushButtonData("MatchLeaderLengths", "Match Len", "AlignTool.Commands.MatchLeaderLengthsCommand", "Equal leader lengths.", null, "match_len"),
                    RibbonHelper.CreatePushButtonData("MinimizeLeaders", "Minimize", "AlignTool.Commands.MinimizeLeaderCommand", "Shorten leaders ~150 mm.", null, "minimize"));

                RibbonPanel pTag = RibbonHelper.GetOrCreatePanel(application, "BIM TOOLS", "ALIGN");
                RibbonHelper.AddStackedTriple(pTag,
                    RibbonHelper.CreatePushButtonData("AlignTagsHorizontally", "Horiz", "AlignTool.Commands.AlignHorizontallyCommand", "Same Y (first = master).", null, "align_h"),
                    RibbonHelper.CreatePushButtonData("AlignTagsVertically", "Vert", "AlignTool.Commands.AlignVerticallyCommand", "Same X (first = master).", null, "align_v"),
                    RibbonHelper.CreatePushButtonData("AlignToReference", "To Ref", "AlignTool.Commands.AlignToReferenceCommand", "Align to picked reference tag.", null, "align_ref"));
                RibbonHelper.AddStackedTriple(pTag,
                    RibbonHelper.CreatePushButtonData("StraightenLeaders", "Straighten", "AlignTool.Commands.StraightenLeadersCommand", "Single-segment leaders.", null, "straighten"),
                    RibbonHelper.CreatePushButtonData("AlignTagOrientation", "Orient", "AlignTool.Commands.AlignTagOrientationCommand", "Horizontal / Vertical orientation.", null, "orientation"),
                    RibbonHelper.CreatePushButtonData("SelectCrossingTags", "Sel Cross", "AlignTool.Commands.SelectCrossingTagsCommand", "Select crossing-leader tags.", null, "select_cross"));
                RibbonHelper.AddStackedTriple(pTag,
                    RibbonHelper.CreatePushButtonData("PinTags", "Pin", "AlignTool.Commands.PinTagsCommand", "Pin selected tags.", null, "pin"),
                    RibbonHelper.CreatePushButtonData("UnpinTags", "Unpin", "AlignTool.Commands.UnpinTagsCommand", "Unpin selected tags.", null, "unpin"),
                    RibbonHelper.CreatePushButtonData("AlignToolSettings", "Settings", "AlignTool.Commands.ShowSettingsCommand", "Settings.", null, "settings"));


                RibbonPanel pUniversal = RibbonHelper.GetOrCreatePanel(application, "BIM TOOLS", "ALIGN");
                RibbonHelper.AddStackedTriple(pUniversal,
                    RibbonHelper.CreatePushButtonData("UniversalAlignLeft", "Left", "AlignTool.Commands.UniversalAlignLeftCommand", "Align any supported tag head to the left in the active view.", null, "align_left"),
                    RibbonHelper.CreatePushButtonData("UniversalAlignCenter", "Center", "AlignTool.Commands.UniversalAlignCenterCommand", "Center supported tag heads in the active view.", null, "align_center"),
                    RibbonHelper.CreatePushButtonData("UniversalAlignRight", "Right", "AlignTool.Commands.UniversalAlignRightCommand", "Align any supported tag head to the right in the active view.", null, "align_right"));
                RibbonHelper.AddStackedTriple(pUniversal,
                    RibbonHelper.CreatePushButtonData("UniversalAlignTop", "Top", "AlignTool.Commands.UniversalAlignTopCommand", "Align supported tag heads to the top in the active view.", null, "align_top"),
                    RibbonHelper.CreatePushButtonData("UniversalAlignMiddle", "Middle", "AlignTool.Commands.UniversalAlignMiddleCommand", "Align supported tag heads to the vertical middle.", null, "align_middle"),
                    RibbonHelper.CreatePushButtonData("UniversalAlignBottom", "Bottom", "AlignTool.Commands.UniversalAlignBottomCommand", "Align supported tag heads to the bottom.", null, "align_bottom"));
                RibbonHelper.AddStackedTriple(pUniversal,
                    RibbonHelper.CreatePushButtonData("AlignToolAbout", "About", "AlignTool.Commands.AboutCommand", "About BIM TOOLS ALIGN.", null, "about"),
                    RibbonHelper.CreatePushButtonData("UniversalDistH", "Dist H", "AlignTool.Commands.UniversalDistributeHorizontalCommand", "Evenly distribute supported tags along the view horizontal axis.", null, "dist_h"),
                    RibbonHelper.CreatePushButtonData("UniversalDistV", "Dist V", "AlignTool.Commands.UniversalDistributeVerticalCommand", "Evenly distribute supported tags along the view vertical axis.", null, "dist_v"));
                RibbonHelper.AddStackedTriple(pUniversal,
                    RibbonHelper.CreatePushButtonData("UniversalSelectAll", "All View", "AlignTool.Commands.UniversalSelectAllTagsInViewCommand", "Select every supported annotation tag visible in the active view.", null, "select_all"),
                    RibbonHelper.CreatePushButtonData("UniversalMatchH", "Match H", "AlignTool.Commands.UniversalMatchHorizontalToFirstCommand", "Match selected tags to the first selected tag on the same view-horizontal line.", null, "align_h"),
                    RibbonHelper.CreatePushButtonData("UniversalMatchV", "Match V", "AlignTool.Commands.UniversalMatchVerticalToFirstCommand", "Match selected tags to the first selected tag on the same view-vertical line.", null, "align_v"));

                RibbonPanel pSelect = RibbonHelper.GetOrCreatePanel(application, "BIM TOOLS", "ALIGN");
                RibbonHelper.AddStackedTriple(pSelect,
                    RibbonHelper.CreatePushButtonData("UniversalSelectPinned", "Pinned", "AlignTool.Commands.UniversalSelectPinnedTagsCommand", "Select supported pinned annotation tags in the active view.", null, "pin"),
                    RibbonHelper.CreatePushButtonData("UniversalSelectUnpinned", "Unpinned", "AlignTool.Commands.UniversalSelectUnpinnedTagsCommand", "Select supported unpinned annotation tags in the active view.", null, "unpin"),
                    RibbonHelper.CreatePushButtonData("UniversalInvertSelection", "Invert", "AlignTool.Commands.UniversalInvertTagSelectionCommand", "Invert the current selection among supported annotation tags in the active view.", null, "invert"));

                RibbonPanel pManage = RibbonHelper.GetOrCreatePanel(application, "BIM TOOLS", "ALIGN");
                RibbonHelper.AddStackedTriple(pManage,
                    RibbonHelper.CreatePushButtonData("AnnotationAlignH", "Align H", "AlignTool.Commands.AnnotationAlignHorizontalCommand", "Align selected text notes, dimensions and tags to a common horizontal centre.", null, "align_h"),
                    RibbonHelper.CreatePushButtonData("AnnotationAlignV", "Align V", "AlignTool.Commands.AnnotationAlignVerticalCommand", "Align selected text notes, dimensions and tags to a common vertical centre.", null, "align_v"),
                    RibbonHelper.CreatePushButtonData("AnnotationAudit", "Audit", "AlignTool.Commands.AnnotationQualityAuditCommand", "Audit selected annotations for visible-bound overlaps.", null, "audit"));
                RibbonHelper.AddStackedTriple(pManage,
                    RibbonHelper.CreatePushButtonData("AnnotationDistH", "Dist H", "AlignTool.Commands.AnnotationDistributeHorizontalCommand", "Evenly distribute selected annotation centres horizontally.", null, "dist_h"),
                    RibbonHelper.CreatePushButtonData("AnnotationDistV", "Dist V", "AlignTool.Commands.AnnotationDistributeVerticalCommand", "Evenly distribute selected annotation centres vertically.", null, "dist_v"),
                    RibbonHelper.CreatePushButtonData("AnnotationResolve", "Resolve", "AlignTool.Commands.AnnotationCollisionResolveCommand", "Preview and optionally resolve annotation overlaps; pinned items are skipped.", null, "untangle"));

                RibbonPanel pMep = RibbonHelper.GetOrCreatePanel(application, "BIM TOOLS", "ALIGN");
                RibbonHelper.AddStackedTriple(pMep,
                    RibbonHelper.CreatePushButtonData("MepTagOrganizer", "MEP Organize", "AlignTool.Commands.MepTagOrganizerCommand", "Arrange selected or visible duct, pipe and cable-tray tags with ordered heads and orthogonal leader elbows.", null, "stack"),
                    RibbonHelper.CreatePushButtonData("MepSelectTags", "MEP Select", "AlignTool.Commands.MepSelectTagsCommand", "Select duct, pipe and cable-tray tags in the active view.", null, "select_all"),
                    RibbonHelper.CreatePushButtonData("MepTagAudit", "MEP Audit", "AlignTool.Commands.MepTagAuditCommand", "Report MEP tag counts, pinned tags and tags without leaders.", null, "audit"));
                RibbonHelper.AddStackedTriple(pMep,
                    RibbonHelper.CreatePushButtonData("IntelligentCsdOptimizer", "CSD Optimize", "AlignTool.Commands.IntelligentCsdOptimizerCommand", "Extract native MEP connector envelopes, generate ranked section arrangements and review/export candidates without modifying the model.", null, "stack"),
                    RibbonHelper.CreatePushButtonData("MepCsdHelp", "CSD Guide", "AlignTool.Commands.AboutCommand", "Open BIM TOOLS ALIGN information and usage guidance.", null, "about"),
                    RibbonHelper.CreatePushButtonData("MepCsdAudit", "CSD Audit", "AlignTool.Commands.MepTagAuditCommand", "Audit supported MEP tags in the active view.", null, "audit"));

                Logger.Info("Ribbon registration completed.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Error("OnStartup failed", ex);
                TaskDialog.Show("AlignTool – Startup Error", ex.ToString());
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            Logger.Info("AlignTool shutting down.");
            return Result.Succeeded;
        }
    }
}
