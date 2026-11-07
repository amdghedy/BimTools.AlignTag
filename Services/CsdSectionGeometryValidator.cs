using System;
using System.Collections.Generic;
using System.Linq;
using AlignTool.Services;

namespace AlignTool.Services
{
    /// <summary>
    /// Repositions measured section contours by each candidate's proposed centre and checks
    /// the resulting true section profiles pairwise. This is a 2D section check, not a 3D route clash test.
    /// </summary>
    public static class CsdSectionGeometryValidator
    {
        public sealed class CandidateCheck
        {
            public string CandidateId { get; set; }
            public int GeometryItemsMatched { get; set; }
            public int GeometryItemsMissing { get; set; }
            public int PairsChecked { get; set; }
            public int SolidProfileClashes { get; set; }
            public string Status { get; set; }
        }

        private sealed class PolygonData
        {
            public string ServiceId;
            public IList<Point2> Points;
            public bool IsHole;
        }

        private sealed class Point2
        {
            public double X;
            public double Y;
        }

        private sealed class ServiceShape
        {
            public string Id;
            public IList<PolygonData> Polygons = new List<PolygonData>();
            public double MinX = double.MaxValue, MinY = double.MaxValue;
            public double MaxX = double.MinValue, MaxY = double.MinValue;
            public double CenterX { get { return (MinX + MaxX) * 0.5; } }
            public double CenterY { get { return (MinY + MaxY) * 0.5; } }
        }

        public static IList<CandidateCheck> Validate(
            IList<KnowledgeBasedCsdEngine.Candidate> candidates,
            RevitCsdExtractionService.ExtractionResult extraction)
        {
            var results = new List<CandidateCheck>();
            if (candidates == null || extraction == null) return results;

            var shapes = BuildServiceShapes(extraction);
            var byId = shapes.ToDictionary(x => x.Id, x => x, StringComparer.OrdinalIgnoreCase);
            // The optimizer section adds 300 mm of working margin around the extracted centre range.
            double originX = extraction.MinXmm - 300.0;
            double originY = extraction.MinYmm - 300.0;

            foreach (KnowledgeBasedCsdEngine.Candidate candidate in candidates)
            {
                var check = new CandidateCheck
                {
                    CandidateId = candidate.Id,
                    GeometryItemsMatched = 0,
                    GeometryItemsMissing = 0,
                    PairsChecked = 0,
                    SolidProfileClashes = 0
                };
                var proposed = new List<ServiceShape>();
                foreach (KnowledgeBasedCsdEngine.Placement placement in candidate.Placements)
                {
                    ServiceShape source;
                    if (!byId.TryGetValue(placement.ServiceId, out source))
                    {
                        check.GeometryItemsMissing++;
                        continue;
                    }

                    check.GeometryItemsMatched++;
                    double targetX = placement.Xmm + placement.WidthMm * 0.5;
                    double targetY = placement.Ymm + placement.HeightMm * 0.5;
                    double dx = targetX - (source.CenterX - originX);
                    double dy = targetY - (source.CenterY - originY);
                    var moved = new ServiceShape { Id = source.Id };
                    foreach (PolygonData polygon in source.Polygons)
                    {
                        var movedPolygon = new PolygonData { ServiceId = polygon.ServiceId, IsHole = polygon.IsHole, Points = new List<Point2>() };
                        foreach (Point2 point in polygon.Points)
                        {
                            double movedX = point.X - originX + dx;
                            double movedY = point.Y - originY + dy;
                            movedPolygon.Points.Add(new Point2 { X = movedX, Y = movedY });
                            moved.MinX = Math.Min(moved.MinX, movedX);
                            moved.MaxX = Math.Max(moved.MaxX, movedX);
                            moved.MinY = Math.Min(moved.MinY, movedY);
                            moved.MaxY = Math.Max(moved.MaxY, movedY);
                        }
                        moved.Polygons.Add(movedPolygon);
                    }
                    proposed.Add(moved);
                }

                for (int i = 0; i < proposed.Count; i++)
                for (int j = i + 1; j < proposed.Count; j++)
                {
                    check.PairsChecked++;
                    if (ShapesOverlap(proposed[i], proposed[j])) check.SolidProfileClashes++;
                }

                check.Status = check.GeometryItemsMatched == 0
                    ? "No measured section profiles"
                    : check.GeometryItemsMissing > 0
                        ? "Partial section coverage"
                        : check.SolidProfileClashes > 0
                            ? "Measured profile overlap"
                            : "No 2D profile overlap detected";
                results.Add(check);
            }
            return results;
        }

        private static IList<ServiceShape> BuildServiceShapes(
            RevitCsdExtractionService.ExtractionResult extraction)
        {
            var map = new Dictionary<string, ServiceShape>(StringComparer.OrdinalIgnoreCase);
            foreach (RevitCsdExtractionService.SectionProjectionItem item in extraction.SectionItems ?? new List<RevitCsdExtractionService.SectionProjectionItem>())
            {
                string serviceId = item.ServiceId;
                if (string.IsNullOrEmpty(serviceId) || item.Contours == null) continue;
                ServiceShape shape;
                if (!map.TryGetValue(serviceId, out shape))
                {
                    shape = new ServiceShape { Id = serviceId };
                    map.Add(serviceId, shape);
                }

                foreach (RevitCsdExtractionService.SectionContour contour in item.Contours)
                {
                    if (contour.Points == null || contour.Points.Count < 3) continue;
                    var polygon = new PolygonData { ServiceId = serviceId, IsHole = contour.IsHole, Points = new List<Point2>() };
                    foreach (RevitCsdExtractionService.SectionPoint point in contour.Points)
                    {
                        polygon.Points.Add(new Point2 { X = point.Xmm, Y = point.Ymm });
                        shape.MinX = Math.Min(shape.MinX, point.Xmm);
                        shape.MaxX = Math.Max(shape.MaxX, point.Xmm);
                        shape.MinY = Math.Min(shape.MinY, point.Ymm);
                        shape.MaxY = Math.Max(shape.MaxY, point.Ymm);
                    }
                    if (polygon.Points.Count >= 3) shape.Polygons.Add(polygon);
                }
            }
            return map.Values.Where(x => x.Polygons.Count > 0).ToList();
        }

        private static bool ShapesOverlap(ServiceShape a, ServiceShape b)
        {
            if (a.MaxX <= b.MinX || b.MaxX <= a.MinX || a.MaxY <= b.MinY || b.MaxY <= a.MinY)
                return false;
            foreach (PolygonData pa in a.Polygons)
            foreach (PolygonData pb in b.Polygons)
                if (BoundariesIntersect(pa.Points, pb.Points)) return true;

            foreach (PolygonData polygon in a.Polygons.Where(x => !x.IsHole))
                if (polygon.Points.Any(point => PointInMaterial(point, b))) return true;
            foreach (PolygonData polygon in b.Polygons.Where(x => !x.IsHole))
                if (polygon.Points.Any(point => PointInMaterial(point, a))) return true;
            return false;
        }

        private static bool BoundariesIntersect(IList<Point2> a, IList<Point2> b)
        {
            for (int i = 0; i < a.Count; i++)
            for (int j = 0; j < b.Count; j++)
                if (SegmentsIntersect(a[i], a[(i + 1) % a.Count], b[j], b[(j + 1) % b.Count]))
                    return true;
            return false;
        }

        private static bool PointInMaterial(Point2 point, ServiceShape shape)
        {
            bool insideOuter = shape.Polygons.Any(p => !p.IsHole && PointInside(point, p.Points));
            if (!insideOuter) return false;
            return !shape.Polygons.Any(p => p.IsHole && PointInside(point, p.Points));
        }

        private static bool PointInside(Point2 point, IList<Point2> polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Point2 a = polygon[i], b = polygon[j];
                bool crosses = ((a.Y > point.Y) != (b.Y > point.Y)) &&
                    (point.X < (b.X - a.X) * (point.Y - a.Y) / ((b.Y - a.Y) + 1e-15) + a.X);
                if (crosses) inside = !inside;
            }
            return inside;
        }

        private static bool SegmentsIntersect(Point2 a, Point2 b, Point2 c, Point2 d)
        {
            double o1 = Orientation(a, b, c), o2 = Orientation(a, b, d);
            double o3 = Orientation(c, d, a), o4 = Orientation(c, d, b);
            const double epsilon = 1e-7;
            if (((o1 > epsilon && o2 < -epsilon) || (o1 < -epsilon && o2 > epsilon)) &&
                ((o3 > epsilon && o4 < -epsilon) || (o3 < -epsilon && o4 > epsilon))) return true;
            return Math.Abs(o1) <= epsilon && OnSegment(a, b, c) ||
                   Math.Abs(o2) <= epsilon && OnSegment(a, b, d) ||
                   Math.Abs(o3) <= epsilon && OnSegment(c, d, a) ||
                   Math.Abs(o4) <= epsilon && OnSegment(c, d, b);
        }

        private static double Orientation(Point2 a, Point2 b, Point2 c)
        {
            return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        }

        private static bool OnSegment(Point2 a, Point2 b, Point2 p)
        {
            const double epsilon = 1e-7;
            return p.X >= Math.Min(a.X, b.X) - epsilon && p.X <= Math.Max(a.X, b.X) + epsilon &&
                   p.Y >= Math.Min(a.Y, b.Y) - epsilon && p.Y <= Math.Max(a.Y, b.Y) + epsilon;
        }
    }
}
