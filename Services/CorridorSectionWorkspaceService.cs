using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace AlignTool.Services
{
    /// <summary>
    /// Creates true, measured cross-sections by intersecting already transformed Revit solids
    /// with finite-thickness station slabs. No model transaction or element modification occurs.
    /// Station offsets are measured along the active view direction from the active view origin.
    /// </summary>
    public static class CorridorSectionWorkspaceService
    {
        private const double FeetToMm = 304.8;
        private const double DefaultMarginMm = 300.0;

        public sealed class StationRequest
        {
            public double StartOffsetMm { get; set; }
            public double SpacingMm { get; set; }
            public int Count { get; set; }
            public double SlabThicknessMm { get; set; }
        }

        public sealed class Contour
        {
            public IList<XYZ> PointsHost { get; set; }
            public bool IsHole { get; set; }
            public double AreaMm2 { get; set; }
        }

        public sealed class SectionItem
        {
            public string ElementKey { get; set; }
            public string ElementName { get; set; }
            public string Category { get; set; }
            public string Discipline { get; set; }
            public string SystemName { get; set; }
            public string ModelName { get; set; }
            public bool IsLinked { get; set; }
            public string GeometryKind { get; set; }
            public int SolidIndex { get; set; }
            public double WidthMm { get; set; }
            public double HeightMm { get; set; }
            public double AreaMm2 { get; set; }
            public IList<Contour> Contours { get; set; }
        }

        public sealed class StationResult
        {
            public int Index { get; set; }
            public string Name { get; set; }
            public double OffsetMm { get; set; }
            public int SolidsTested { get; set; }
            public int SolidIntersections { get; set; }
            public int BooleanFailures { get; set; }
            public IList<SectionItem> Items { get; set; }
            public int ContourCount { get { return Items.Sum(x => x.Contours.Count); } }
            public int ElementCount { get { return Items.Select(x => x.ElementKey).Distinct(StringComparer.OrdinalIgnoreCase).Count(); } }
        }

        public sealed class WorkspaceResult
        {
            public string ModelName { get; set; }
            public string SourceDescription { get; set; }
            public string PlaneDescription { get; set; }
            public double SlabThicknessMm { get; set; }
            public XYZ BaseOrigin { get; set; }
            public XYZ Right { get; set; }
            public XYZ Up { get; set; }
            public XYZ Normal { get; set; }
            public int StationsRequested { get; set; }
            public int StationsWithGeometry { get; set; }
            public int SolidsTested { get; set; }
            public int SolidIntersections { get; set; }
            public int BooleanFailures { get; set; }
            public IList<StationResult> Stations { get; set; }
        }

        public static WorkspaceResult Extract(
            Document document,
            View view,
            Revit3DCoordinationExtractionService.ExtractionResult source,
            StationRequest request)
        {
            if (document == null) throw new ArgumentNullException("document");
            if (view == null) throw new ArgumentNullException("view");
            if (source == null) throw new ArgumentNullException("source");
            if (source.Elements == null) throw new ArgumentException("The source extraction contains no element collection.");
            Validate(request);

            XYZ right = view.RightDirection.Normalize();
            XYZ up = view.UpDirection.Normalize();
            XYZ normal = view.ViewDirection.Normalize();
            XYZ baseOrigin = view.Origin;
            var result = new WorkspaceResult
            {
                ModelName = document.Title,
                SourceDescription = source.SourceDescription,
                PlaneDescription = "Parallel section planes; station axis = active view direction; in-plane axes = active view right/up.",
                SlabThicknessMm = request.SlabThicknessMm,
                StationsRequested = request.Count,
                Stations = new List<StationResult>()
            };

            double minX, maxX, minY, maxY;
            GetPlaneExtents(source.Elements, baseOrigin, right, up, out minX, out maxX, out minY, out maxY);
            minX -= DefaultMarginMm / FeetToMm;
            maxX += DefaultMarginMm / FeetToMm;
            minY -= DefaultMarginMm / FeetToMm;
            maxY += DefaultMarginMm / FeetToMm;

            for (int stationIndex = 0; stationIndex < request.Count; stationIndex++)
            {
                double offsetMm = request.StartOffsetMm + stationIndex * request.SpacingMm;
                XYZ stationOrigin = baseOrigin + normal * (offsetMm / FeetToMm);
                Solid slab = CreateSectionSlab(stationOrigin, right, up, normal,
                    minX, maxX, minY, maxY, request.SlabThicknessMm / FeetToMm);
                var station = new StationResult
                {
                    Index = stationIndex + 1,
                    Name = "S" + (stationIndex + 1).ToString("000"),
                    OffsetMm = offsetMm,
                    Items = new List<SectionItem>()
                };
                if (slab == null)
                {
                    station.BooleanFailures++;
                    result.BooleanFailures++;
                    result.Stations.Add(station);
                    continue;
                }

                foreach (Revit3DCoordinationExtractionService.ElementRecord element in source.Elements)
                {
                    if (element == null || element.Solids == null) continue;
                    for (int solidIndex = 0; solidIndex < element.Solids.Count; solidIndex++)
                    {
                        Solid solid = element.Solids[solidIndex];
                        if (solid == null || solid.Faces.Size == 0 || solid.Volume <= 1e-12) continue;
                        station.SolidsTested++;
                        result.SolidsTested++;
                        if (!SolidCrossesStation(solid, stationOrigin, normal,
                            request.SlabThicknessMm / FeetToMm)) continue;
                        try
                        {
                            Solid cut = BooleanOperationsUtils.ExecuteBooleanOperation(
                                solid, slab, BooleanOperationsType.Intersect);
                            if (cut == null || cut.Faces.Size == 0 || cut.Volume <= 1e-12) continue;
                            IList<Contour> contours = ReadContours(cut, right, up, normal,
                                stationOrigin, request.SlabThicknessMm / FeetToMm);
                            if (contours.Count == 0) continue;

                            var allPoints = contours.SelectMany(x => x.PointsHost).ToList();
                            double minSectionX = allPoints.Min(p => (p - stationOrigin).DotProduct(right));
                            double maxSectionX = allPoints.Max(p => (p - stationOrigin).DotProduct(right));
                            double minSectionY = allPoints.Min(p => (p - stationOrigin).DotProduct(up));
                            double maxSectionY = allPoints.Max(p => (p - stationOrigin).DotProduct(up));
                            double area = contours.Where(x => !x.IsHole).Sum(x => Math.Abs(x.AreaMm2))
                                - contours.Where(x => x.IsHole).Sum(x => Math.Abs(x.AreaMm2));
                            string kind = element.GeometryKinds != null && solidIndex < element.GeometryKinds.Count
                                ? element.GeometryKinds[solidIndex] : "Solid";
                            station.Items.Add(new SectionItem
                            {
                                ElementKey = element.Key,
                                ElementName = element.Name,
                                Category = element.Category,
                                Discipline = element.Discipline,
                                SystemName = element.SystemName,
                                ModelName = element.ModelName,
                                IsLinked = element.IsLinked,
                                GeometryKind = kind,
                                SolidIndex = solidIndex,
                                WidthMm = Math.Max(0, (maxSectionX - minSectionX) * FeetToMm),
                                HeightMm = Math.Max(0, (maxSectionY - minSectionY) * FeetToMm),
                                AreaMm2 = Math.Max(0, area),
                                Contours = contours
                            });
                            station.SolidIntersections++;
                            result.SolidIntersections++;
                        }
                        catch
                        {
                            station.BooleanFailures++;
                            result.BooleanFailures++;
                        }
                    }
                }
                result.Stations.Add(station);
            }

            result.StationsWithGeometry = result.Stations.Count(x => x.Items.Count > 0);
            return result;
        }

        private static void Validate(StationRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            if (request.Count < 1 || request.Count > 100)
                throw new ArgumentOutOfRangeException("request.Count", "Station count must be between 1 and 100.");
            if (request.SpacingMm <= 0 || double.IsNaN(request.SpacingMm) || double.IsInfinity(request.SpacingMm))
                throw new ArgumentOutOfRangeException("request.SpacingMm", "Station spacing must be a positive finite value.");
            if (request.SlabThicknessMm < 0.5 || request.SlabThicknessMm > 100 ||
                double.IsNaN(request.SlabThicknessMm) || double.IsInfinity(request.SlabThicknessMm))
                throw new ArgumentOutOfRangeException("request.SlabThicknessMm", "Slab thickness must be between 0.5 and 100 mm.");
            if (double.IsNaN(request.StartOffsetMm) || double.IsInfinity(request.StartOffsetMm))
                throw new ArgumentOutOfRangeException("request.StartOffsetMm");
        }

        private static void GetPlaneExtents(
            IList<Revit3DCoordinationExtractionService.ElementRecord> elements,
            XYZ origin, XYZ right, XYZ up,
            out double minX, out double maxX, out double minY, out double maxY)
        {
            minX = double.MaxValue; maxX = double.MinValue;
            minY = double.MaxValue; maxY = double.MinValue;
            foreach (var element in elements)
            {
                if (element == null || element.BoundsMin == null || element.BoundsMax == null) continue;
                for (int ix = 0; ix <= 1; ix++)
                for (int iy = 0; iy <= 1; iy++)
                for (int iz = 0; iz <= 1; iz++)
                {
                    XYZ p = new XYZ(ix == 0 ? element.BoundsMin.X : element.BoundsMax.X,
                        iy == 0 ? element.BoundsMin.Y : element.BoundsMax.Y,
                        iz == 0 ? element.BoundsMin.Z : element.BoundsMax.Z);
                    XYZ d = p - origin;
                    double x = d.DotProduct(right), y = d.DotProduct(up);
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                }
            }
            if (minX == double.MaxValue)
            {
                minX = -1000 / FeetToMm; maxX = 1000 / FeetToMm;
                minY = -1000 / FeetToMm; maxY = 1000 / FeetToMm;
            }
        }

        private static bool SolidCrossesStation(Solid solid, XYZ origin, XYZ normal, double thicknessFeet)
        {
            try
            {
                BoundingBoxXYZ box = solid.GetBoundingBox();
                if (box == null) return true;
                Transform transform = box.Transform ?? Transform.Identity;
                double min = double.MaxValue, max = double.MinValue;
                for (int ix = 0; ix <= 1; ix++)
                for (int iy = 0; iy <= 1; iy++)
                for (int iz = 0; iz <= 1; iz++)
                {
                    XYZ local = new XYZ(ix == 0 ? box.Min.X : box.Max.X,
                        iy == 0 ? box.Min.Y : box.Max.Y,
                        iz == 0 ? box.Min.Z : box.Max.Z);
                    double d = (transform.OfPoint(local) - origin).DotProduct(normal);
                    min = Math.Min(min, d); max = Math.Max(max, d);
                }
                double half = thicknessFeet * 0.5 + 1e-6;
                return max >= -half && min <= half;
            }
            catch { return true; }
        }

        private static Solid CreateSectionSlab(
            XYZ origin, XYZ right, XYZ up, XYZ normal,
            double minX, double maxX, double minY, double maxY, double thicknessFeet)
        {
            try
            {
                double half = thicknessFeet * 0.5;
                XYZ p0 = origin + right * minX + up * minY - normal * half;
                XYZ p1 = origin + right * maxX + up * minY - normal * half;
                XYZ p2 = origin + right * maxX + up * maxY - normal * half;
                XYZ p3 = origin + right * minX + up * maxY - normal * half;
                var loop = new CurveLoop();
                if (right.CrossProduct(up).DotProduct(normal) >= 0)
                {
                    loop.Append(Line.CreateBound(p0, p1));
                    loop.Append(Line.CreateBound(p1, p2));
                    loop.Append(Line.CreateBound(p2, p3));
                    loop.Append(Line.CreateBound(p3, p0));
                }
                else
                {
                    loop.Append(Line.CreateBound(p0, p3));
                    loop.Append(Line.CreateBound(p3, p2));
                    loop.Append(Line.CreateBound(p2, p1));
                    loop.Append(Line.CreateBound(p1, p0));
                }
                return GeometryCreationUtilities.CreateExtrusionGeometry(
                    new List<CurveLoop> { loop }, normal, thicknessFeet);
            }
            catch { return null; }
        }

        private static IList<Contour> ReadContours(
            Solid cut, XYZ right, XYZ up, XYZ normal, XYZ origin, double thicknessFeet)
        {
            var result = new List<Contour>();
            PlanarFace selected = null;
            double best = double.MaxValue;
            foreach (Face face in cut.Faces)
            {
                PlanarFace planar = face as PlanarFace;
                if (planar == null) continue;
                if (Math.Abs(planar.FaceNormal.Normalize().DotProduct(normal)) < 0.999) continue;
                double signed = (planar.Origin - origin).DotProduct(normal);
                double distance = Math.Abs(Math.Abs(signed) - thicknessFeet * 0.5);
                if (distance < best) { best = distance; selected = planar; }
            }
            if (selected == null) return result;

            var rawContours = new List<Tuple<IList<XYZ>, double>>();
            foreach (EdgeArray edgeLoop in selected.EdgeLoops)
            {
                var points = new List<XYZ>();
                foreach (Edge edge in edgeLoop)
                {
                    IList<XYZ> tessellated;
                    try { tessellated = edge.AsCurveFollowingFace(selected).Tessellate(); }
                    catch
                    {
                        try { tessellated = edge.AsCurve().Tessellate(); }
                        catch { continue; }
                    }
                    foreach (XYZ point in tessellated)
                    {
                        if (points.Count == 0 || points[points.Count - 1].DistanceTo(point) > 1e-5)
                            points.Add(point);
                    }
                }
                if (points.Count > 1 && points[0].DistanceTo(points[points.Count - 1]) < 1e-5)
                    points.RemoveAt(points.Count - 1);
                if (points.Count >= 3)
                    rawContours.Add(Tuple.Create((IList<XYZ>)points, SignedArea(points, origin, right, up)));
            }

            // Revit edge-loop enumeration order is not a reliable outer/inner-loop contract.
            // Use the largest absolute loop as the outer boundary, then classify opposite winding as holes.
            double outerSign = rawContours.OrderByDescending(x => Math.Abs(x.Item2))
                .Select(x => Math.Sign(x.Item2)).FirstOrDefault();
            foreach (var raw in rawContours)
            {
                bool hole = outerSign != 0 && Math.Sign(raw.Item2) != 0 &&
                    Math.Sign(raw.Item2) != outerSign;
                result.Add(new Contour
                {
                    PointsHost = raw.Item1,
                    IsHole = hole,
                    AreaMm2 = raw.Item2
                });
            }
            return result;
        }

        private static double SignedArea(IList<XYZ> points, XYZ origin, XYZ right, XYZ up)
        {
            double area = 0;
            for (int i = 0; i < points.Count; i++)
            {
                XYZ a = points[i] - origin;
                XYZ b = points[(i + 1) % points.Count] - origin;
                double ax = a.DotProduct(right) * FeetToMm;
                double ay = a.DotProduct(up) * FeetToMm;
                double bx = b.DotProduct(right) * FeetToMm;
                double by = b.DotProduct(up) * FeetToMm;
                area += ax * by - bx * ay;
            }
            return area * 0.5;
        }
    }
}