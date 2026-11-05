using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace AlignTool.Models
{
    public class TagInfo
    {
        public IndependentTag Tag { get; set; }
        public ElementId Id { get; set; }
        public XYZ HeadPosition { get; set; }
        public XYZ LeaderEnd { get; set; }
        public bool HasLeader { get; set; }
        public bool IsPinned { get; set; }
        public ElementId HostId { get; set; }
        public string CategoryName { get; set; }
        public double ApproximateWidth { get; set; }
        public double ApproximateHeight { get; set; }
        public string LeaderEndSource { get; set; }

        public static TagInfo FromTag(IndependentTag tag, Document doc, View view = null)
        {
            if (tag == null) return null;
            var info = new TagInfo
            {
                Tag = tag,
                Id = tag.Id,
                HeadPosition = tag.TagHeadPosition,
                IsPinned = tag.Pinned,
                CategoryName = tag.Category?.Name ?? "Unknown"
            };
            try { info.HasLeader = tag.HasLeader; } catch { info.HasLeader = true; }
            info.HostId = GetHostIdSafe(tag);
            info.LeaderEnd = GetLeaderEndSafe(tag, doc, view, out string source);
            info.LeaderEndSource = source;
            info.ApproximateWidth = 0.30;
            info.ApproximateHeight = 0.15;
            return info;
        }

        public static XYZ GetLeaderEndSafe(IndependentTag tag, Document doc, View view, out string source)
        {
            source = "none";
            if (tag == null || doc == null) return null;

            try
            {
                var prop = tag.GetType().GetProperty("LeaderEnd");
                if (prop != null)
                {
                    var value = prop.GetValue(tag) as XYZ;
                    if (IsValidPoint(value, tag.TagHeadPosition))
                    { source = "LeaderEnd property"; return value; }
                }
            }
            catch { }

            try
            {
                var method = tag.GetType().GetMethod("GetLeaderEnd", Type.EmptyTypes);
                if (method != null)
                {
                    var value = method.Invoke(tag, null) as XYZ;
                    if (IsValidPoint(value, tag.TagHeadPosition))
                    { source = "GetLeaderEnd()"; return value; }
                }
            }
            catch { }

            // Host location / bbox center fallback
            try
            {
                ElementId hostId = GetHostIdSafe(tag);
                if (hostId != null && hostId != ElementId.InvalidElementId)
                {
                    Element host = doc.GetElement(hostId);
                    if (host != null)
                    {
                        if (host.Location is LocationPoint lp && lp.Point != null)
                        { source = "host LocationPoint"; return lp.Point; }
                        BoundingBoxXYZ bb = host.get_BoundingBox(view);
                        if (bb != null)
                        {
                            XYZ c = (bb.Min + bb.Max) * 0.5;
                            source = "host BoundingBox center";
                            return c;
                        }
                    }
                }
            }
            catch { }

            source = "fallback head";
            return tag.TagHeadPosition;
        }

        public static ElementId GetHostIdSafe(IndependentTag tag)
        {
            try
            {
                var prop = tag.GetType().GetProperty("TaggedLocalElementId");
                if (prop != null)
                {
                    var id = prop.GetValue(tag) as ElementId;
                    if (id != null && id != ElementId.InvalidElementId) return id;
                }
            }
            catch { }

            try
            {
                var method = tag.GetType().GetMethod("GetTaggedLocalElementId", Type.EmptyTypes);
                if (method != null)
                {
                    var id = method.Invoke(tag, null) as ElementId;
                    if (id != null && id != ElementId.InvalidElementId) return id;
                }
            }
            catch { }

            try
            {
                var method = tag.GetType().GetMethod("GetTaggedReferences", Type.EmptyTypes);
                if (method != null)
                {
                    var refs = method.Invoke(tag, null) as System.Collections.IEnumerable;
                    if (refs != null)
                    {
                        foreach (var r in refs)
                        {
                            if (r is Reference reference)
                                return reference.ElementId;
                        }
                    }
                }
            }
            catch { }

            return ElementId.InvalidElementId;
        }

        private static bool IsValidPoint(XYZ p, XYZ head)
        {
            if (p == null) return false;
            if (double.IsNaN(p.X) || double.IsNaN(p.Y) || double.IsNaN(p.Z)) return false;
            if (head != null && p.DistanceTo(head) < 1e-9) return false;
            return true;
        }
    }
}
