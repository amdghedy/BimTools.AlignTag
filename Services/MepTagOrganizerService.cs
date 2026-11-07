using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;

namespace AlignTool.Services
{
    /// <summary>
    /// Drawing-wide MEP tag layout for composite views containing native and linked models.
    /// Uses two perimeter annotation zones with as many vertical columns as required.
    /// </summary>
    public static class MepTagOrganizerService
    {
        private const double GapMm = 8.0;
        private const double SideOffsetMm = 35.0;
        private const double MinimumTagWidthMm = 25.0;
        private const double MinimumTagHeightMm = 12.0;
        private const double CropMarginMm = 12.0;

        public sealed class TagPlan
        {
            public IndependentTag Tag;
            public XYZ Head;
            public XYZ Elbow;
            public Reference LeaderReference;
            public string Side;
            public int Column;
            public XYZ Anchor;
            public bool HasLeader;
        }

        public sealed class ResultSummary
        {
            public int Eligible;
            public int Movable;
            public int Pinned;
            public int Leaderless;
            public int LinkedHostTags;
            public int NativeHostTags;
            public int Moved;
            public int Failed;
            public int ElbowsSet;
            public int ElbowFailures;
            public int ColumnsLeft;
            public int ColumnsRight;
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
            (int)BuiltInCategory.OST_ConduitFitting,
            (int)BuiltInCategory.OST_MechanicalEquipment
        };

        public static bool IsMepHost(Element host)
        {
            return host != null && host.Category != null &&
                MepCategoryIds.Contains(host.Category.Id.IntegerValue);
        }

        /// <summary>Resolve a tag reference to its actual host, including a linked element.</summary>
        public static Element ResolveReferencedHost(Document hostDocument, Reference reference,
            out RevitLinkInstance linkInstance)
        {
            linkInstance = null;
            if (hostDocument == null || reference == null) return null;

            Element direct = hostDocument.GetElement(reference.ElementId);
            if (reference.LinkedElementId != ElementId.InvalidElementId)
            {
                linkInstance = direct as RevitLinkInstance;
                if (linkInstance == null) return null;
                Document linkedDocument = linkInstance.GetLinkDocument();
                return linkedDocument == null ? null : linkedDocument.GetElement(reference.LinkedElementId);
            }

            return direct;
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
                        RevitLinkInstance link;
                        if (reference != null && IsMepHost(ResolveReferencedHost(doc, reference, out link)))
                            return reference;
                    }
                }
            }
            catch { }

            // Legacy single-host fallback. Resolve the tag's reference rather than assuming
            // that the referenced ElementId is always the physical MEP element.
            try
            {
                ElementId hostId = AlignTool.Models.TagInfo.GetHostIdSafe(tag);
                Element host = doc.GetElement(hostId);
                if (IsMepHost(host)) return new Reference(host);
            }
            catch { }
            return null;
        }

        public static IList<IndependentTag> GetMepTagsInView(Document doc, View view)
        {
            var result = new List<IndependentTag>();
            if (doc == null || view == null) return result;
            foreach (IndependentTag tag in new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(IndependentTag)).Cast<IndependentTag>())
            {
                if (GetMepLeaderReference(tag, doc) != null) result.Add(tag);
            }
            return result;
        }

        public static ResultSummary BuildPlan(Document doc, View view, IList<IndependentTag> input)
        {
            var result = new ResultSummary();
            if (doc == null || view == null || input == null) return result;

            XYZ right = view.RightDirection.Normalize();
            XYZ up = view.UpDirection.Normalize();
            XYZ normal = view.ViewDirection.Normalize();
            XYZ origin = view.Origin;
            var records = new List<TagRecord>();

            foreach (IndependentTag tag in input.Where(t => t != null)
                .GroupBy(t => t.Id.IntegerValue).Select(g => g.First()))
            {
                Reference reference = GetMepLeaderReference(tag, doc);
                if (reference == null) continue;

                RevitLinkInstance link;
                Element host = ResolveReferencedHost(doc, reference, out link);
                if (!IsMepHost(host)) continue;

                result.Eligible++;
                if (tag.Pinned) { result.Pinned++; continue; }

                XYZ anchor = GetLeaderEnd(tag, reference);
                if (anchor == null) anchor = GetHostCenter(host, link, view);
                if (anchor == null) anchor = tag.TagHeadPosition;

                bool hasLeader = false;
                try { hasLeader = tag.HasLeader; } catch { }
                if (!hasLeader) result.Leaderless++;
                if (link == null) result.NativeHostTags++; else result.LinkedHostTags++;

                double anchorX = (anchor - origin).DotProduct(right);
                double anchorY = (anchor - origin).DotProduct(up);
                double depth = (tag.TagHeadPosition - origin).DotProduct(normal);
                double headX = (tag.TagHeadPosition - origin).DotProduct(right);
                double headY = (tag.TagHeadPosition - origin).DotProduct(up);
                double width, height, boxOffsetX, boxOffsetY;
                GetProjectedTagSize(tag, view, right, up, headX, headY,
                    out width, out height, out boxOffsetX, out boxOffsetY);

                records.Add(new TagRecord
                {
                    Tag = tag,
                    Reference = reference,
                    Anchor = anchor,
                    AnchorX = anchorX,
                    AnchorY = anchorY,
                    Depth = depth,
                    CurrentHeadX = headX,
                    CurrentHeadY = headY,
                    Width = width,
                    Height = height,
                    BoxOffsetX = boxOffsetX,
                    BoxOffsetY = boxOffsetY,
                    HasLeader = hasLeader
                });
            }

            result.Movable = records.Count;
            if (records.Count == 0) return result;

            double centreX = records.Select(r => r.AnchorX).OrderBy(x => x)
                .ElementAt(records.Count / 2);
            foreach (TagRecord record in records)
                record.Side = record.AnchorX <= centreX ? "Left" : "Right";

            double cropMinY, cropMaxY;
            GetViewVerticalRange(view, origin, up, records, out cropMinY, out cropMaxY);
            double availableHeight = Math.Max(MmToFeet(500), cropMaxY - cropMinY - 2 * MmToFeet(CropMarginMm));
            double topY = cropMaxY - MmToFeet(CropMarginMm);

            double minAnchorX = records.Min(r => r.AnchorX);
            double maxAnchorX = records.Max(r => r.AnchorX);
            double leftBaseX = minAnchorX - MmToFeet(SideOffsetMm);
            double rightBaseX = maxAnchorX + MmToFeet(SideOffsetMm);

            result.ColumnsLeft = PackSide(records.Where(r => r.Side == "Left").ToList(),
                "Left", leftBaseX, topY, availableHeight, right, up, normal, origin, result);
            result.ColumnsRight = PackSide(records.Where(r => r.Side == "Right").ToList(),
                "Right", rightBaseX, topY, availableHeight, right, up, normal, origin, result);

            return result;
        }

        private static int PackSide(List<TagRecord> sideRecords, string side,
            double baseX, double topY, double availableHeight,
            XYZ right, XYZ up, XYZ normal, XYZ origin, ResultSummary result)
        {
            if (sideRecords.Count == 0) return 0;

            // Preserve endpoint elevation order. Greedy column packing uses measured
            // annotation extents so no two planned text boxes in one column overlap.
            List<TagRecord> ordered = sideRecords.OrderByDescending(r => r.AnchorY)
                .ThenBy(r => r.AnchorX)
                .ThenBy(r => r.Tag.Id.IntegerValue).ToList();

            double gap = MmToFeet(GapMm);
            var columns = new List<List<TagRecord>>();
            var columnHeights = new List<double>();
            var columnWidths = new List<double>();

            foreach (TagRecord record in ordered)
            {
                int target = columns.Count - 1;
                double required = record.Height + (target >= 0 && columns[target].Count > 0 ? gap : 0);
                if (target < 0 || columnHeights[target] + required > availableHeight)
                {
                    columns.Add(new List<TagRecord>());
                    columnHeights.Add(0);
                    columnWidths.Add(0);
                    target++;
                    required = record.Height;
                }

                columns[target].Add(record);
                columnHeights[target] += required;
                columnWidths[target] = Math.Max(columnWidths[target], record.Width);
            }

            double xCursor = baseX;
            for (int c = 0; c < columns.Count; c++)
            {
                double colWidth = columnWidths[c];
                double headCenterX = side == "Left"
                    ? xCursor - colWidth * 0.5
                    : xCursor + colWidth * 0.5;

                double usedHeight = columnHeights[c];
                double cursorY = topY - Math.Max(0, (availableHeight - usedHeight) * 0.5);

                foreach (TagRecord record in columns[c])
                {
                    double boxCenterY = cursorY - record.Height * 0.5;
                    cursorY -= record.Height + gap;

                    double targetHeadX = headCenterX - record.BoxOffsetX;
                    double targetHeadY = boxCenterY - record.BoxOffsetY;
                    XYZ head = origin + right * targetHeadX + up * targetHeadY + normal * record.Depth;

                    // Vertical first, then horizontal to the head. Keeping host elevation
                    // order inside each column reduces leader crossings. Each row gets an
                    // individual elbow rather than a shared elbow rail.
                    XYZ elbow = origin + right * record.AnchorX + up * targetHeadY + normal *
                        ((record.Anchor - origin).DotProduct(normal));

                    result.Plan.Add(new TagPlan
                    {
                        Tag = record.Tag,
                        Head = head,
                        Elbow = elbow,
                        LeaderReference = record.Reference,
                        Side = side,
                        Column = c + 1,
                        Anchor = record.Anchor,
                        HasLeader = record.HasLeader
                    });
                }

                xCursor += (side == "Left" ? -1 : 1) * (colWidth + gap);
            }

            return columns.Count;
        }

        public static ResultSummary Apply(Document doc, ResultSummary summary)
        {
            if (doc == null || summary == null || summary.Plan == null) return summary;

            using (Transaction tx = new Transaction(doc, "BIM TOOLS - Composite MEP Tag Layout"))
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
            if (r == null) return "No MEP layout plan was created.";
            return "COMPOSITE MEP DRAWING LAYOUT PREVIEW" +
                "\nEligible MEP tags: " + r.Eligible +
                "\nMovable tags: " + r.Movable +
                "\nNative-model hosts: " + r.NativeHostTags +
                "\nLinked-model hosts: " + r.LinkedHostTags +
                "\nPinned (skipped): " + r.Pinned +
                "\nLeaderless: " + r.Leaderless +
                "\nLeft annotation columns: " + r.ColumnsLeft +
                "\nRight annotation columns: " + r.ColumnsRight +
                "\nPlanned tag moves: " + (r.Plan == null ? 0 : r.Plan.Count) +
                "\n\nTags are packed into measured, non-overlapping vertical columns on both sides of the composite view. Host elevation order is preserved within each column. Leaders are given individual orthogonal elbows where the Revit API allows it. Check the preview and review the final drawing, especially at column transitions.";
        }

        private static void GetProjectedTagSize(IndependentTag tag, View view,
            XYZ right, XYZ up, double headX, double headY,
            out double width, out double height, out double offsetX, out double offsetY)
        {
            width = MmToFeet(MinimumTagWidthMm);
            height = MmToFeet(MinimumTagHeightMm);
            offsetX = 0;
            offsetY = 0;
            try
            {
                BoundingBoxXYZ box = tag.get_BoundingBox(view);
                if (box == null) return;
                Transform transform = box.Transform ?? Transform.Identity;
                double minX = double.MaxValue, maxX = double.MinValue;
                double minY = double.MaxValue, maxY = double.MinValue;
                for (int ix = 0; ix < 2; ix++)
                for (int iy = 0; iy < 2; iy++)
                for (int iz = 0; iz < 2; iz++)
                {
                    XYZ local = new XYZ(ix == 0 ? box.Min.X : box.Max.X,
                        iy == 0 ? box.Min.Y : box.Max.Y,
                        iz == 0 ? box.Min.Z : box.Max.Z);
                    XYZ world = transform.OfPoint(local);
                    double x = world.DotProduct(right);
                    double y = world.DotProduct(up);
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                }
                if (maxX - minX > 1e-5) width = Math.Max(width, maxX - minX);
                if (maxY - minY > 1e-5) height = Math.Max(height, maxY - minY);
                offsetX = (minX + maxX) * 0.5 - headX;
                offsetY = (minY + maxY) * 0.5 - headY;
            }
            catch { }
        }

        private static void GetViewVerticalRange(View view, XYZ origin, XYZ up,
            IList<TagRecord> records, out double minY, out double maxY)
        {
            minY = records.Min(r => r.AnchorY);
            maxY = records.Max(r => r.AnchorY);
            try
            {
                BoundingBoxXYZ crop = view.CropBox;
                if (crop == null) return;
                Transform transform = crop.Transform ?? Transform.Identity;
                double cropMin = double.MaxValue, cropMax = double.MinValue;
                for (int ix = 0; ix < 2; ix++)
                for (int iy = 0; iy < 2; iy++)
                for (int iz = 0; iz < 2; iz++)
                {
                    XYZ local = new XYZ(ix == 0 ? crop.Min.X : crop.Max.X,
                        iy == 0 ? crop.Min.Y : crop.Max.Y,
                        iz == 0 ? crop.Min.Z : crop.Max.Z);
                    double y = (transform.OfPoint(local) - origin).DotProduct(up);
                    cropMin = Math.Min(cropMin, y);
                    cropMax = Math.Max(cropMax, y);
                }
                if (cropMax - cropMin > MmToFeet(500))
                {
                    minY = cropMin;
                    maxY = cropMax;
                }
            }
            catch { }
        }

        private static XYZ GetLeaderEnd(IndependentTag tag, Reference reference)
        {
            try
            {
                MethodInfo method = tag.GetType().GetMethod("GetLeaderEnd", new[] { typeof(Reference) });
                if (method != null) return method.Invoke(tag, new object[] { reference }) as XYZ;
            }
            catch { }
            return null;
        }

        private static bool TrySetLeaderElbow(IndependentTag tag, Reference reference, XYZ point)
        {
            try
            {
                MethodInfo method = tag.GetType().GetMethod("SetLeaderElbow",
                    new[] { typeof(Reference), typeof(XYZ) });
                if (method == null) return false;
                method.Invoke(tag, new object[] { reference, point });
                return true;
            }
            catch { return false; }
        }

        private static XYZ GetHostCenter(Element host, RevitLinkInstance link, View view)
        {
            if (host == null) return null;
            try
            {
                XYZ point = null;
                if (host.Location is LocationPoint lp) point = lp.Point;
                else if (host.Location is LocationCurve lc) point = lc.Curve.Evaluate(0.5, true);
                else
                {
                    BoundingBoxXYZ box = host.get_BoundingBox(null);
                    if (box != null) point = box.Transform.OfPoint((box.Min + box.Max) * 0.5);
                }
                if (point != null && link != null) point = link.GetTotalTransform().OfPoint(point);
                return point;
            }
            catch { return null; }
        }

        private static double MmToFeet(double mm) { return mm / 304.8; }

        private sealed class TagRecord
        {
            public IndependentTag Tag;
            public Reference Reference;
            public XYZ Anchor;
            public double AnchorX, AnchorY, Depth;
            public double CurrentHeadX, CurrentHeadY;
            public double Width, Height, BoxOffsetX, BoxOffsetY;
            public bool HasLeader;
            public string Side;
        }
    }
}
