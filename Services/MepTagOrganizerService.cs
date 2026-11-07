using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;

namespace AlignTool.Services
{
    /// <summary>
    /// View-aware arrangement for duct, pipe, cable-tray and conduit IndependentTags.
    /// Only annotation heads and leader elbows are changed; hosts and system data are untouched.
    /// </summary>
    public static class MepTagOrganizerService
    {
        private const double DefaultGapMm = 8.0;
        private const double MinimumHeadHeightMm = 12.0;

        public sealed class TagPlan
        {
            public IndependentTag Tag;
            public XYZ Head;
            public XYZ Elbow;
            public Reference LeaderReference;
            public string Side;
            public double HostY;
            public bool HasLeader;
        }

        public sealed class ResultSummary
        {
            public int Eligible;
            public int Movable;
            public int Pinned;
            public int Leaderless;
            public int Moved;
            public int Failed;
            public int ElbowsSet;
            public IList<TagPlan> Plan = new List<TagPlan>();
        }

        private static readonly int[] MepCategoryIds =
        {
            (int)BuiltInCategory.OST_DuctCurves,
            (int)BuiltInCategory.OST_FlexDuctCurves,
            (int)BuiltInCategory.OST_DuctFitting,
            (int)BuiltInCategory.OST_PipeCurves,
            (int)BuiltInCategory.OST_FlexPipeCurves,
            (int)BuiltInCategory.OST_PipeFitting,
            (int)BuiltInCategory.OST_CableTray,
            (int)BuiltInCategory.OST_CableTrayFitting,
            (int)BuiltInCategory.OST_Conduit,
            (int)BuiltInCategory.OST_ConduitFitting
        };

        public static bool IsMepHost(Element host)
        {
            return host != null && host.Category != null &&
                MepCategoryIds.Contains(host.Category.Id.IntegerValue);
        }

        public static IList<IndependentTag> GetMepTagsInView(Document doc, View view)
        {
            var result = new List<IndependentTag>();
            if (doc == null || view == null) return result;
            foreach (IndependentTag tag in new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(IndependentTag)).Cast<IndependentTag>())
            {
                if (GetMepLeaderReference(tag, doc) != null || IsMepHost(doc.GetElement(AlignTool.Models.TagInfo.GetHostIdSafe(tag))))
                    result.Add(tag);
            }
            return result;
        }

        public static Reference GetMepLeaderReference(IndependentTag tag, Document doc)
        {
            if (tag == null || doc == null) return null;
            try
            {
                MethodInfo method = tag.GetType().GetMethod("GetTaggedReferences", Type.EmptyTypes);
                IEnumerable refs = method == null ? null : method.Invoke(tag, null) as IEnumerable;
                if (refs != null)
                {
                    foreach (object item in refs)
                    {
                        Reference reference = item as Reference;
                        if (reference != null && IsMepHost(doc.GetElement(reference.ElementId)))
                            return reference;
                    }
                }
            }
            catch { }
            try
            {
                Element host = doc.GetElement(AlignTool.Models.TagInfo.GetHostIdSafe(tag));
                if (IsMepHost(host)) return new Reference(host);
            }
            catch { }
            return null;
        }

        public static ResultSummary BuildPlan(Document doc, View view, IList<IndependentTag> input)
        {
            var summary = new ResultSummary();
            if (doc == null || view == null || input == null) return summary;

            XYZ right = view.RightDirection.Normalize();
            XYZ up = view.UpDirection.Normalize();
            XYZ normal = view.ViewDirection.Normalize();
            XYZ origin = view.Origin;
            var records = new List<Record>();

            foreach (IndependentTag tag in input.Where(x => x != null).GroupBy(x => x.Id.IntegerValue).Select(g => g.First()))
            {
                Reference reference = GetMepLeaderReference(tag, doc);
                if (reference == null) continue;
                summary.Eligible++;
                if (tag.Pinned) { summary.Pinned++; continue; }

                XYZ head = tag.TagHeadPosition;
                XYZ anchor = GetLeaderEnd(tag, reference, doc, view);
                if (anchor == null) anchor = GetHostCenter(doc.GetElement(reference.ElementId), view);
                if (anchor == null) anchor = head;

                bool hasLeader = false;
                try { hasLeader = tag.HasLeader; } catch { }
                if (!hasLeader) summary.Leaderless++;

                double hx = (head - origin).DotProduct(right);
                double hy = (head - origin).DotProduct(up);
                double depth = (head - origin).DotProduct(normal);
                double ax = (anchor - origin).DotProduct(right);
                double ay = (anchor - origin).DotProduct(up);
                double hostDepth = (anchor - origin).DotProduct(normal);

                double height = MinimumHeadHeightMm / 304.8;
                try
                {
                    BoundingBoxXYZ box = tag.get_BoundingBox(view);
                    if (box != null)
                    {
                        double projected = Math.Abs((box.Max.Y - box.Min.Y));
                        if (projected > 1e-5) height = Math.Max(height, projected);
                    }
                }
                catch { }

                records.Add(new Record
                {
                    Tag = tag, Reference = reference, HeadX = hx, HeadY = hy,
                    AnchorX = ax, AnchorY = ay, Depth = depth, HostDepth = hostDepth,
                    Height = height, HasLeader = hasLeader,
                    Side = hx <= ax ? "Left" : "Right"
                });
            }

            summary.Movable = records.Count;
            if (records.Count == 0) return summary;

            double minAnchorX = records.Min(x => x.AnchorX);
            double maxAnchorX = records.Max(x => x.AnchorX);
            double minHeadX = records.Min(x => x.HeadX);
            double maxHeadX = records.Max(x => x.HeadX);
            double leftLane = records.Where(x => x.Side == "Left").Select(x => x.HeadX).DefaultIfEmpty(minAnchorX - MmToFeet(50)).Average();
            double rightLane = records.Where(x => x.Side == "Right").Select(x => x.HeadX).DefaultIfEmpty(maxAnchorX + MmToFeet(50)).Average();
            double gap = MmToFeet(DefaultGapMm);
            double maxHeight = records.Max(x => x.Height);
            double rowSpacing = Math.Max(MmToFeet(25), maxHeight + gap);

            foreach (string side in new[] { "Left", "Right" })
            {
                List<Record> lane = records.Where(x => x.Side == side)
                    .OrderByDescending(x => x.AnchorY).ThenBy(x => x.AnchorX).ToList();
                if (lane.Count == 0) continue;

                double centreY = lane.Average(x => x.AnchorY);
                double startY = centreY + (lane.Count - 1) * rowSpacing * 0.5;
                double laneX = side == "Left" ? leftLane : rightLane;

                for (int i = 0; i < lane.Count; i++)
                {
                    Record item = lane[i];
                    double targetY = startY - i * rowSpacing;
                    XYZ newHead = origin + right * laneX + up * targetY + normal * item.Depth;
                    XYZ elbow = origin + right * laneX + up * item.AnchorY + normal * item.HostDepth;
                    summary.Plan.Add(new TagPlan
                    {
                        Tag = item.Tag, Head = newHead, Elbow = elbow,
                        LeaderReference = item.Reference, Side = side,
                        HostY = item.AnchorY, HasLeader = item.HasLeader
                    });
                }
            }
            return summary;
        }

        public static ResultSummary Apply(Document doc, ResultSummary summary)
        {
            if (doc == null || summary == null || summary.Plan == null) return summary;
            using (Transaction tx = new Transaction(doc, "BIM TOOLS - MEP Tag Organizer"))
            {
                tx.Start();
                foreach (TagPlan item in summary.Plan)
                {
                    if (item == null || item.Tag == null || item.Tag.Pinned) continue;
                    using (SubTransaction sub = new SubTransaction(doc))
                    {
                        try
                        {
                            sub.Start();
                            item.Tag.TagHeadPosition = item.Head;
                            summary.Moved++;
                            if (item.HasLeader && TrySetLeaderElbow(item.Tag, item.LeaderReference, item.Elbow))
                                summary.ElbowsSet++;
                            sub.Commit();
                        }
                        catch
                        {
                            if (sub.GetStatus() == TransactionStatus.Started) sub.RollBack();
                            summary.Failed++;
                        }
                    }
                }
                tx.Commit();
            }
            return summary;
        }

        public static string Describe(ResultSummary r)
        {
            if (r == null) return "No MEP tag plan was created.";
            return "Eligible MEP tags: " + r.Eligible +
                "\nMovable tags: " + r.Movable +
                "\nPinned (skipped): " + r.Pinned +
                "\nLeaderless: " + r.Leaderless +
                "\nPlanned head moves: " + (r.Plan == null ? 0 : r.Plan.Count) +
                "\n\nHeads are ordered by host position and arranged in left/right columns. Leader elbows are adjusted where the Revit API permits. This is a best-effort layout; review dense areas and multi-reference tags after applying.";
        }

        private static XYZ GetLeaderEnd(IndependentTag tag, Reference reference, Document doc, View view)
        {
            try
            {
                MethodInfo method = tag.GetType().GetMethod("GetLeaderEnd", new[] { typeof(Reference) });
                if (method != null) return method.Invoke(tag, new object[] { reference }) as XYZ;
            }
            catch { }
            try { return AlignTool.Models.TagInfo.GetLeaderEndSafe(tag, doc, view, out _); }
            catch { return null; }
        }

        private static bool TrySetLeaderElbow(IndependentTag tag, Reference reference, XYZ point)
        {
            if (tag == null || reference == null || point == null) return false;
            try
            {
                MethodInfo method = tag.GetType().GetMethod("SetLeaderElbow", new[] { typeof(Reference), typeof(XYZ) });
                if (method == null) return false;
                method.Invoke(tag, new object[] { reference, point });
                return true;
            }
            catch { return false; }
        }

        private static XYZ GetHostCenter(Element host, View view)
        {
            if (host == null) return null;
            try
            {
                if (host.Location is LocationPoint lp) return lp.Point;
                if (host.Location is LocationCurve lc) return lc.Curve.Evaluate(0.5, true);
                BoundingBoxXYZ box = host.get_BoundingBox(view);
                if (box != null) return (box.Min + box.Max) * 0.5;
            }
            catch { }
            return null;
        }

        private static double MmToFeet(double mm) { return mm / 304.8; }

        private sealed class Record
        {
            public IndependentTag Tag;
            public Reference Reference;
            public double HeadX, HeadY, AnchorX, AnchorY, Depth, HostDepth, Height;
            public bool HasLeader;
            public string Side;
        }
    }
}
