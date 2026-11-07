using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;

namespace AlignTool.Services
{
    /// <summary>
    /// Arranges MEP tags in active-view coordinates. Tag heads are stacked in stable
    /// host-endpoint order; each leader elbow is placed at its own tag row to avoid
    /// the shared vertical elbow rail used by the first implementation.
    /// </summary>
    public static class MepTagOrganizerService
    {
        private const double DefaultGapMm = 8.0;
        private const double MinimumHeadHeightMm = 12.0;
        private const double MinimumHeadWidthMm = 25.0;
        private const double DefaultSideOffsetMm = 50.0;

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
            public int ElbowFailures;
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
                if (GetMepLeaderReference(tag, doc) != null)
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

            foreach (IndependentTag tag in input.Where(x => x != null)
                .GroupBy(x => x.Id.IntegerValue).Select(g => g.First()))
            {
                Reference reference = GetMepLeaderReference(tag, doc);
                if (reference == null) continue;

                summary.Eligible++;
                if (tag.Pinned)
                {
                    summary.Pinned++;
                    continue;
                }

                XYZ head = tag.TagHeadPosition;
                XYZ anchor = GetLeaderEnd(tag, reference, doc, view);
                if (anchor == null)
                    anchor = GetHostCenter(doc.GetElement(reference.ElementId), view);
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
                double width, height;
                GetProjectedTagSize(tag, view, right, up, out width, out height);

                records.Add(new Record
                {
                    Tag = tag,
                    Reference = reference,
                    HeadX = hx,
                    HeadY = hy,
                    AnchorX = ax,
                    AnchorY = ay,
                    Depth = depth,
                    HostDepth = hostDepth,
                    Width = width,
                    Height = height,
                    HasLeader = hasLeader,
                    Side = hx <= ax ? "Left" : "Right"
                });
            }

            summary.Movable = records.Count;
            if (records.Count == 0) return summary;

            double gap = MmToFeet(DefaultGapMm);
            double sideOffset = MmToFeet(DefaultSideOffsetMm);
            double leftAnchor = records.Where(x => x.Side == "Left")
                .Select(x => x.AnchorX).DefaultIfEmpty(records.Average(x => x.AnchorX)).Average();
            double rightAnchor = records.Where(x => x.Side == "Right")
                .Select(x => x.AnchorX).DefaultIfEmpty(records.Average(x => x.AnchorX)).Average();

            double leftLane = records.Where(x => x.Side == "Left")
                .Select(x => x.HeadX).DefaultIfEmpty(leftAnchor - sideOffset).Average();
            double rightLane = records.Where(x => x.Side == "Right")
                .Select(x => x.HeadX).DefaultIfEmpty(rightAnchor + sideOffset).Average();

            double leftWidth = records.Where(x => x.Side == "Left")
                .Select(x => x.Width).DefaultIfEmpty(MmToFeet(MinimumHeadWidthMm)).Max();
            double rightWidth = records.Where(x => x.Side == "Right")
                .Select(x => x.Width).DefaultIfEmpty(MmToFeet(MinimumHeadWidthMm)).Max();
            double centreX = records.Average(x => x.AnchorX);
            double requiredHalfGap = (leftWidth + rightWidth) * 0.5 + gap;

            if (leftLane >= rightLane)
            {
                leftLane = centreX - requiredHalfGap * 0.5;
                rightLane = centreX + requiredHalfGap * 0.5;
            }
            else if (rightLane - leftLane < requiredHalfGap)
            {
                double laneCentre = (leftLane + rightLane) * 0.5;
                leftLane = laneCentre - requiredHalfGap * 0.5;
                rightLane = laneCentre + requiredHalfGap * 0.5;
            }

            foreach (string side in new[] { "Left", "Right" })
            {
                List<Record> lane = records.Where(x => x.Side == side)
                    .OrderByDescending(x => x.AnchorY)
                    .ThenBy(x => x.AnchorX)
                    .ThenBy(x => x.Tag.Id.IntegerValue)
                    .ToList();
                if (lane.Count == 0) continue;

                double totalHeight = lane.Sum(x => x.Height) + gap * Math.Max(0, lane.Count - 1);
                double cursorY = lane.Average(x => x.AnchorY) + totalHeight * 0.5;
                double laneX = side == "Left" ? leftLane : rightLane;

                foreach (Record item in lane)
                {
                    double targetY = cursorY - item.Height * 0.5;
                    cursorY = targetY - item.Height * 0.5 - gap;

                    XYZ newHead = origin + right * laneX + up * targetY + normal * item.Depth;

                    // One elbow per leader: keep its Y on the unique tag row and its X
                    // at the actual leader endpoint. Ordered endpoint/row pairs prevent
                    // the avoidable crossings caused by a common elbow column.
                    XYZ elbow = origin + right * item.AnchorX + up * targetY + normal * item.HostDepth;

                    summary.Plan.Add(new TagPlan
                    {
                        Tag = item.Tag,
                        Head = newHead,
                        Elbow = elbow,
                        LeaderReference = item.Reference,
                        Side = side,
                        HostY = item.AnchorY,
                        HasLeader = item.HasLeader
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

                            if (item.HasLeader)
                            {
                                if (TrySetLeaderElbow(item.Tag, item.LeaderReference, item.Elbow))
                                    summary.ElbowsSet++;
                                else
                                    summary.ElbowFailures++;
                            }

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
                "\n\nTag heads are stacked by leader endpoint elevation with measured tag-box spacing. Each leader elbow is assigned to its own row. The routing minimizes avoidable crossings for ordered endpoints; inspect cases with coincident endpoints, multi-reference tags or constrained leaders.";
        }

        private static void GetProjectedTagSize(IndependentTag tag, View view,
            XYZ right, XYZ up, out double width, out double height)
        {
            width = MmToFeet(MinimumHeadWidthMm);
            height = MmToFeet(MinimumHeadHeightMm);

            try
            {
                BoundingBoxXYZ box = tag.get_BoundingBox(view);
                if (box == null) return;

                Transform transform = box.Transform;
                if (transform == null) transform = Transform.Identity;

                double minX = double.MaxValue;
                double maxX = double.MinValue;
                double minY = double.MaxValue;
                double maxY = double.MinValue;

                for (int ix = 0; ix < 2; ix++)
                for (int iy = 0; iy < 2; iy++)
                for (int iz = 0; iz < 2; iz++)
                {
                    XYZ local = new XYZ(
                        ix == 0 ? box.Min.X : box.Max.X,
                        iy == 0 ? box.Min.Y : box.Max.Y,
                        iz == 0 ? box.Min.Z : box.Max.Z);
                    XYZ world = transform.OfPoint(local);
                    double x = world.DotProduct(right);
                    double y = world.DotProduct(up);
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }

                if (maxX - minX > 1e-5) width = Math.Max(width, maxX - minX);
                if (maxY - minY > 1e-5) height = Math.Max(height, maxY - minY);
            }
            catch { }
        }

        private static XYZ GetLeaderEnd(IndependentTag tag, Reference reference, Document doc, View view)
        {
            try
            {
                MethodInfo method = tag.GetType().GetMethod("GetLeaderEnd", new[] { typeof(Reference) });
                if (method != null)
                {
                    XYZ point = method.Invoke(tag, new object[] { reference }) as XYZ;
                    if (point != null) return point;
                }
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
                if (box != null) return box.Transform.OfPoint((box.Min + box.Max) * 0.5);
            }
            catch { }
            return null;
        }

        private static double MmToFeet(double mm) { return mm / 304.8; }

        private sealed class Record
        {
            public IndependentTag Tag;
            public Reference Reference;
            public double HeadX;
            public double HeadY;
            public double AnchorX;
            public double AnchorY;
            public double Depth;
            public double HostDepth;
            public double Width;
            public double Height;
            public bool HasLeader;
            public string Side;
        }
    }
}
