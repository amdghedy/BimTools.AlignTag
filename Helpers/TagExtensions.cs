using System;
using Autodesk.Revit.DB;

namespace AlignTool.Helpers
{
    public static class TagExtensions
    {
        public static bool TrySetHeadPosition(this IndependentTag tag, XYZ position)
        {
            if (tag == null || position == null) return false;
            try
            {
                tag.TagHeadPosition = position;
                return true;
            }
            catch { return false; }
        }

        public static XYZ TryGetLeaderEndSafe(this IndependentTag tag, Document doc, View view, out string reason)
        {
            reason = null;
            try
            {
                // Revit 2020+ reflection fallbacks handled in TagInfo
                return Models.TagInfo.GetLeaderEndSafe(tag, doc, view, out reason);
            }
            catch (Exception ex)
            {
                reason = ex.Message;
                return null;
            }
        }
    }
}
