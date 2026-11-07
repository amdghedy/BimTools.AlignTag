using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;

namespace AlignTool.Services
{
    /// <summary>Reads native Revit MEP elements and connector dimensions without modifying the model.</summary>
    public static class RevitCsdExtractionService
    {
        public sealed class ExtractionResult
        {
            public IList<KnowledgeBasedCsdEngine.ServiceItem> Items { get; set; }
            public IList<SectionProjectionItem> SectionItems { get; set; }
            public double MinXmm { get; set; }
            public double MaxXmm { get; set; }
            public double MinYmm { get; set; }
            public double MaxYmm { get; set; }
            public int ConnectorCount { get; set; }
            public int ElementsWithoutConnectors { get; set; }
            public int SkippedElements { get; set; }
            public int ElementsTestedForSection { get; set; }
            public int SectionIntersections { get; set; }
            public int ElementsOutsideSection { get; set; }
            public int SolidIntersections { get; set; }
            public int SolidIntersectionFailures { get; set; }
            public int InsulationLiningElements { get; set; }
            public string SourceDescription { get; set; }
        }

        public sealed class SectionPoint
        {
            public double Xmm { get; set; }
            public double Ymm { get; set; }
        }

        public sealed class SectionContour
        {
            public IList<SectionPoint> Points { get; set; }
            public bool IsHole { get; set; }
        }

        /// <summary>Cross-section contours obtained from a Revit solid intersected with a thin view-plane slab.</summary>
        public sealed class SectionProjectionItem
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Discipline { get; set; }
            public double CenterXmm { get; set; }
            public double CenterYmm { get; set; }
            public double WidthMm { get; set; }
            public double HeightMm { get; set; }
            public double PlaneOffsetMm { get; set; }
            public string ProjectionMethod { get; set; }
            public IList<SectionContour> Contours { get; set; }
        }

        public static ExtractionResult Extract(Document doc, View view, IEnumerable<ElementId> selectedIds)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (view == null) throw new ArgumentNullException("view");
            var ids = selectedIds == null ? new List<ElementId>() : selectedIds.ToList();
            var sources = new List<SourceElement>();
            var selectedLinks = new List<RevitLinkInstance>();
            foreach (ElementId id in ids)
            {
                Element element = doc.GetElement(id);
                if (element is RevitLinkInstance) selectedLinks.Add((RevitLinkInstance)element);
                else if (element != null && IsMepElement(element))
                    sources.Add(new SourceElement { Element = element, ToHost = Transform.Identity, Prefix = "HOST", ModelName = doc.Title });
            }

            bool useSelection = sources.Count > 0 || selectedLinks.Count > 0;
            if (!useSelection)
            {
                foreach (Element element in new FilteredElementCollector(doc, view.Id)
                    .WhereElementIsNotElementType().ToElements())
                    if (IsMepElement(element))
                        sources.Add(new SourceElement { Element = element, ToHost = Transform.Identity, Prefix = "HOST", ModelName = doc.Title });
            }

            IList<RevitLinkInstance> links = selectedLinks;
            if (links.Count == 0)
            {
                try
                {
                    links = new FilteredElementCollector(doc, view.Id)
                        .OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>().ToList();
                }
                catch
                {
                    links = new FilteredElementCollector(doc)
                        .OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>().ToList();
                }
            }

            foreach (RevitLinkInstance link in links)
            {
                try
                {
                    Document linkedDoc = link.GetLinkDocument();
                    if (linkedDoc == null) continue;
                    Transform transform = link.GetTotalTransform();
                    string modelName = linkedDoc.Title;
                    foreach (Element linkedElement in new FilteredElementCollector(linkedDoc)
                        .WhereElementIsNotElementType().ToElements())
                    {
                        if (!IsMepElement(linkedElement)) continue;
                        if (!IntersectsActiveCrop(view, linkedElement, transform)) continue;
                        sources.Add(new SourceElement
                        {
                            Element = linkedElement, ToHost = transform,
                            Prefix = "LINK" + link.Id.IntegerValue, ModelName = modelName
                        });
                    }
                }
                catch { }
            }

            var result = new ExtractionResult { Items = new List<KnowledgeBasedCsdEngine.ServiceItem>(), SectionItems = new List<SectionProjectionItem>() };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            XYZ right = view.RightDirection.Normalize();
            XYZ up = view.UpDirection.Normalize();
            XYZ origin = view.Origin;
            foreach (SourceElement source in sources)
            {
                Element element = source.Element;
                string uniqueId = source.Prefix + ":" + element.Id.IntegerValue;
                if (element == null || !seen.Add(uniqueId)) continue;
                try
                {
                    double width, height;
                    int connectorCount;
                    string systemName;
                    ReadConnectorEnvelope(element, source.ToHost, right, up,
                        out width, out height, out connectorCount, out systemName);
                    result.ConnectorCount += connectorCount;
                    if (connectorCount == 0)
                    {
                        result.ElementsWithoutConnectors++;
                        ReadParameterEnvelope(element, ref width, ref height);
                    }
                    if (width <= 0 || height <= 0)
                    {
                        result.SkippedElements++;
                        continue;
                    }

                    XYZ localCentre = GetElementCentre(element);
                    XYZ centre = localCentre == null ? null : source.ToHost.OfPoint(localCentre);
                    if (centre == null) { result.SkippedElements++; continue; }
                    double x = (centre - origin).DotProduct(right) * 304.8;
                    double y = (centre - origin).DotProduct(up) * 304.8;
                    width = Math.Max(10, width);
                    height = Math.Max(10, height);
                    string discipline = GetDiscipline(element);
                    string name = (element.Name ?? element.GetType().Name) + " [" + source.ModelName + "]";
                    result.ElementsTestedForSection++;
                    IList<SectionProjectionItem> projected = ProjectElementToSection(
                        element, source.ToHost, right, up, view.ViewDirection.Normalize(),
                        origin, uniqueId, name, discipline, ref result.SolidIntersections,
                        ref result.SolidIntersectionFailures, ref result.InsulationLiningElements);
                    if (projected.Count == 0) result.ElementsOutsideSection++;
                    else
                    {
                        foreach (SectionProjectionItem sectionItem in projected)
                            result.SectionItems.Add(sectionItem);
                        result.SectionIntersections += projected.Count;
                    }

                    result.Items.Add(new KnowledgeBasedCsdEngine.ServiceItem
                    {
                        Id = uniqueId,
                        Name = name,
                        SystemName = systemName,
                        Discipline = discipline,
                        WidthMm = width,
                        HeightMm = height,
                        RequiredClearanceMm = 50,
                        PreferredElevationMm = y,
                        MinimumElevationMm = 0,
                        IsFixed = false,
                        Priority = GetPriority(discipline)
                    });
                    result.MinXmm = result.Items.Count == 1 ? x : Math.Min(result.MinXmm, x);
                    result.MaxXmm = result.Items.Count == 1 ? x : Math.Max(result.MaxXmm, x);
                    result.MinYmm = result.Items.Count == 1 ? y : Math.Min(result.MinYmm, y);
                    result.MaxYmm = result.Items.Count == 1 ? y : Math.Max(result.MaxYmm, y);
                }
                catch { result.SkippedElements++; }
            }
            result.SourceDescription = (useSelection ? "Selection" : "Active view") +
                " + linked models (" + links.Count + ")";
            return result;
        }


        private const double SectionSlabThicknessFeet = 0.00656167979; // 2 mm Boolean slice
        private const double SectionExtentPaddingFeet = 1.64041995; // 500 mm around element bounds

        private static IList<SectionProjectionItem> ProjectElementToSection(
            Element element, Transform toHost, XYZ right, XYZ up, XYZ normal, XYZ origin,
            string id, string name, string discipline, ref int successfulIntersections,
            ref int failedIntersections, ref int insulationLiningElements)
        {
            var output = new List<SectionProjectionItem>();
            IList<SolidSource> solids = new List<SolidSource>();
            CollectElementSolids(element, toHost, "Host solid", solids, ref failedIntersections);

            // Revit 2020 exposes duct/pipe insulation and duct lining as separate elements.
            // Include their own solids so the visible outside envelope is not silently omitted.
            try
            {
                ICollection<ElementId> insulationIds =
                    InsulationLiningBase.GetInsulationIds(element.Document, element.Id);
                foreach (ElementId insulationId in insulationIds)
                {
                    Element insulation = element.Document.GetElement(insulationId);
                    if (insulation == null) continue;
                    insulationLiningElements++;
                    CollectElementSolids(insulation, toHost, "Insulation", solids, ref failedIntersections);
                }
            }
            catch { }
            try
            {
                ICollection<ElementId> liningIds =
                    InsulationLiningBase.GetLiningIds(element.Document, element.Id);
                foreach (ElementId liningId in liningIds)
                {
                    Element lining = element.Document.GetElement(liningId);
                    if (lining == null) continue;
                    insulationLiningElements++;
                    CollectElementSolids(lining, toHost, "Lining", solids, ref failedIntersections);
                }
            }
            catch { }

            if (solids.Count == 0) return output;
            BoundingBoxXYZ elementBox = null;
            try { elementBox = element.get_BoundingBox(null); } catch { }
            if (elementBox == null)
            {
                failedIntersections += solids.Count;
                return output;
            }

            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            Transform boxTransform = elementBox.Transform ?? Transform.Identity;
            for (int ix = 0; ix <= 1; ix++)
            for (int iy = 0; iy <= 1; iy++)
            for (int iz = 0; iz <= 1; iz++)
            {
                XYZ local = new XYZ(ix == 0 ? elementBox.Min.X : elementBox.Max.X,
                    iy == 0 ? elementBox.Min.Y : elementBox.Max.Y,
                    iz == 0 ? elementBox.Min.Z : elementBox.Max.Z);
                XYZ host = toHost.OfPoint(boxTransform.OfPoint(local));
                double x = (host - origin).DotProduct(right);
                double y = (host - origin).DotProduct(up);
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }
            minX -= SectionExtentPaddingFeet; maxX += SectionExtentPaddingFeet;
            minY -= SectionExtentPaddingFeet; maxY += SectionExtentPaddingFeet;

            Solid slab = CreateSectionSlab(origin, right, up, normal, minX, maxX, minY, maxY);
            if (slab == null)
            {
                failedIntersections += solids.Count;
                return output;
            }

            foreach (SolidSource source in solids)
            {
                try
                {
                    Solid cut = BooleanOperationsUtils.ExecuteBooleanOperation(
                        source.Geometry, slab, BooleanOperationsType.Intersect);
                    if (cut == null || cut.Faces.Size == 0 || cut.Volume <= 1e-12) continue;

                    IList<SectionContour> contours = ReadSectionContours(
                        cut, right, up, normal, origin);
                    if (contours.Count == 0) continue;

                    double minContourX = double.MaxValue, maxContourX = double.MinValue;
                    double minContourY = double.MaxValue, maxContourY = double.MinValue;
                    int pointCount = 0;
                    foreach (SectionContour contour in contours)
                    foreach (SectionPoint point in contour.Points)
                    {
                        pointCount++;
                        minContourX = Math.Min(minContourX, point.Xmm);
                        maxContourX = Math.Max(maxContourX, point.Xmm);
                        minContourY = Math.Min(minContourY, point.Ymm);
                        maxContourY = Math.Max(maxContourY, point.Ymm);
                    }
                    if (pointCount < 3) continue;

                    output.Add(new SectionProjectionItem
                    {
                        Id = id + ":" + source.Kind,
                        Name = name + " (" + source.Kind + ")",
                        Discipline = discipline,
                        CenterXmm = (minContourX + maxContourX) * 0.5,
                        CenterYmm = (minContourY + maxContourY) * 0.5,
                        WidthMm = Math.Max(0.1, maxContourX - minContourX),
                        HeightMm = Math.Max(0.1, maxContourY - minContourY),
                        PlaneOffsetMm = 0,
                        ProjectionMethod = "Revit solid Boolean intersection / " + source.Kind,
                        Contours = contours
                    });
                    successfulIntersections++;
                }
                catch
                {
                    failedIntersections++;
                }
            }
            return output;
        }

        private sealed class SolidSource
        {
            public Solid Geometry;
            public string Kind;
        }

        private static void CollectElementSolids(Element element, Transform toHost, string kind,
            IList<SolidSource> output, ref int failures)
        {
            try
            {
                var options = new Options
                {
                    ComputeReferences = false,
                    IncludeNonVisibleObjects = true,
                    DetailLevel = ViewDetailLevel.Fine
                };
                GeometryElement geometry = element.get_Geometry(options);
                if (geometry == null) return;
                CollectSolidsRecursive(geometry, toHost, kind, output, ref failures);
            }
            catch { failures++; }
        }

        private static void CollectSolidsRecursive(GeometryElement geometry, Transform toHost,
            string kind, IList<SolidSource> output, ref int failures)
        {
            foreach (GeometryObject obj in geometry)
            {
                Solid solid = obj as Solid;
                if (solid != null)
                {
                    try
                    {
                        if (solid.Faces.Size == 0 || solid.Volume <= 1e-12) continue;
                        Solid hostSolid = SolidUtils.CreateTransformed(solid, toHost ?? Transform.Identity);
                        if (hostSolid != null && hostSolid.Faces.Size > 0 && hostSolid.Volume > 1e-12)
                            output.Add(new SolidSource { Geometry = hostSolid, Kind = kind });
                    }
                    catch { failures++; }
                    continue;
                }

                GeometryInstance instance = obj as GeometryInstance;
                if (instance != null)
                {
                    try
                    {
                        // GetInstanceGeometry returns geometry in the element document's model coordinates.
                        GeometryElement instanceGeometry = instance.GetInstanceGeometry();
                        if (instanceGeometry != null)
                            CollectSolidsRecursive(instanceGeometry, toHost, kind, output, ref failures);
                    }
                    catch { failures++; }
                }
            }
        }

        private static Solid CreateSectionSlab(XYZ origin, XYZ right, XYZ up, XYZ normal,
            double minX, double maxX, double minY, double maxY)
        {
            try
            {
                double half = SectionSlabThicknessFeet * 0.5;
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
                    new List<CurveLoop> { loop }, normal, SectionSlabThicknessFeet);
            }
            catch { return null; }
        }

        private static IList<SectionContour> ReadSectionContours(Solid cut,
            XYZ right, XYZ up, XYZ normal, XYZ origin)
        {
            var result = new List<SectionContour>();
            PlanarFace selectedFace = null;
            double bestOffset = double.MaxValue;
            foreach (Face face in cut.Faces)
            {
                PlanarFace planar = face as PlanarFace;
                if (planar == null) continue;
                if (Math.Abs(planar.FaceNormal.Normalize().DotProduct(normal)) < 0.999) continue;
                double signedOffset = (planar.Origin - origin).DotProduct(normal);
                double offset = Math.Abs(Math.Abs(signedOffset) - SectionSlabThicknessFeet * 0.5);
                if (offset < bestOffset) { bestOffset = offset; selectedFace = planar; }
            }
            if (selectedFace == null) return result;

            foreach (EdgeArray edgeLoop in selectedFace.EdgeLoops)
            {
                var points = new List<SectionPoint>();
                foreach (Edge edge in edgeLoop)
                {
                    IList<XYZ> tessellated;
                    try { tessellated = edge.AsCurve().Tessellate(); }
                    catch { continue; }
                    foreach (XYZ hostPoint in tessellated)
                    {
                        double x = (hostPoint - origin).DotProduct(right) * 304.8;
                        double y = (hostPoint - origin).DotProduct(up) * 304.8;
                        if (points.Count == 0 ||
                            Math.Abs(points[points.Count - 1].Xmm - x) > 0.01 ||
                            Math.Abs(points[points.Count - 1].Ymm - y) > 0.01)
                            points.Add(new SectionPoint { Xmm = x, Ymm = y });
                    }
                }
                if (points.Count >= 3)
                {
                    if (Math.Abs(points[0].Xmm - points[points.Count - 1].Xmm) < 0.01 &&
                        Math.Abs(points[0].Ymm - points[points.Count - 1].Ymm) < 0.01)
                        points.RemoveAt(points.Count - 1);
                    if (points.Count >= 3)
                        result.Add(new SectionContour { Points = points, IsHole = false });
                }
            }
            return result;
        }

        private sealed class SourceElement
        {
            public Element Element;
            public Transform ToHost;
            public string Prefix;
            public string ModelName;
        }

        private static bool IntersectsActiveCrop(View view, Element element, Transform toHost)
        {
            try
            {
                if (!view.CropBoxActive) return true;
                BoundingBoxXYZ crop = view.CropBox;
                BoundingBoxXYZ box = element.get_BoundingBox(null);
                if (crop == null || box == null) return true;

                Transform boxTransform = box.Transform ?? Transform.Identity;
                Transform cropInverse = (crop.Transform ?? Transform.Identity).Inverse;
                double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
                for (int ix = 0; ix <= 1; ix++)
                for (int iy = 0; iy <= 1; iy++)
                for (int iz = 0; iz <= 1; iz++)
                {
                    XYZ local = new XYZ(ix == 0 ? box.Min.X : box.Max.X,
                        iy == 0 ? box.Min.Y : box.Max.Y,
                        iz == 0 ? box.Min.Z : box.Max.Z);
                    XYZ host = toHost.OfPoint(boxTransform.OfPoint(local));
                    XYZ cropLocal = cropInverse.OfPoint(host);
                    minX = Math.Min(minX, cropLocal.X); maxX = Math.Max(maxX, cropLocal.X);
                    minY = Math.Min(minY, cropLocal.Y); maxY = Math.Max(maxY, cropLocal.Y);
                    minZ = Math.Min(minZ, cropLocal.Z); maxZ = Math.Max(maxZ, cropLocal.Z);
                }
                const double tolerance = 0.01;
                return maxX >= crop.Min.X - tolerance && minX <= crop.Max.X + tolerance &&
                       maxY >= crop.Min.Y - tolerance && minY <= crop.Max.Y + tolerance &&
                       maxZ >= crop.Min.Z - tolerance && minZ <= crop.Max.Z + tolerance;
            }
            catch
            {
                // If a crop/bounding-box query fails, retain the item rather than silently
                // discarding a potentially intersecting linked route.
                return true;
            }
        }

        private static bool IsMepElement(Element e)
        {
            if (e.Category == null) return false;
            int id = e.Category.Id.IntegerValue;
            return id == (int)BuiltInCategory.OST_DuctCurves ||
                   id == (int)BuiltInCategory.OST_FlexDuctCurves ||
                   id == (int)BuiltInCategory.OST_DuctFitting ||
                   id == (int)BuiltInCategory.OST_DuctAccessory ||
                   id == (int)BuiltInCategory.OST_PipeCurves ||
                   id == (int)BuiltInCategory.OST_FlexPipeCurves ||
                   id == (int)BuiltInCategory.OST_PipeFitting ||
                   id == (int)BuiltInCategory.OST_PipeAccessory ||
                   id == (int)BuiltInCategory.OST_CableTray ||
                   id == (int)BuiltInCategory.OST_CableTrayFitting ||
                   id == (int)BuiltInCategory.OST_Conduit ||
                   id == (int)BuiltInCategory.OST_ConduitFitting ||
                   id == (int)BuiltInCategory.OST_MechanicalEquipment ||
                   id == (int)BuiltInCategory.OST_ElectricalEquipment;
        }

        private static void ReadConnectorEnvelope(Element element, Transform toHost, XYZ right, XYZ up,
            out double widthMm, out double heightMm, out int count, out string systemName)
        {
            widthMm = 0; heightMm = 0; count = 0; systemName = "";
            ConnectorSet connectors = null;
            MEPCurve curve = element as MEPCurve;
            if (curve != null) connectors = curve.ConnectorManager.Connectors;
            FamilyInstance family = element as FamilyInstance;
            if (connectors == null && family != null && family.MEPModel != null)
                connectors = family.MEPModel.ConnectorManager.Connectors;
            if (connectors == null) return;

            foreach (Connector connector in connectors)
            {
                if (connector == null) continue;
                count++;
                try
                {
                    double w = 0, h = 0;
                    if (connector.Shape == ConnectorProfileType.Rectangular ||
                        connector.Shape == ConnectorProfileType.Oval)
                    {
                        Transform cs = connector.CoordinateSystem;
                        XYZ bx = toHost.OfVector(cs.BasisX).Normalize();
                        XYZ by = toHost.OfVector(cs.BasisY).Normalize();
                        double cw = connector.Width * 304.8;
                        double ch = connector.Height * 304.8;
                        w = Math.Abs(bx.DotProduct(right)) * cw + Math.Abs(by.DotProduct(right)) * ch;
                        h = Math.Abs(bx.DotProduct(up)) * cw + Math.Abs(by.DotProduct(up)) * ch;
                    }
                    else if (connector.Shape == ConnectorProfileType.Round)
                    {
                        w = h = connector.Radius * 2 * 304.8;
                    }
                    widthMm = Math.Max(widthMm, w);
                    heightMm = Math.Max(heightMm, h);
                    if (string.IsNullOrEmpty(systemName) && connector.MEPSystem != null)
                        systemName = connector.MEPSystem.Name;
                }
                catch { }
            }
            if (widthMm <= 0 || heightMm <= 0) { widthMm = 0; heightMm = 0; }
        }

        private static void ReadParameterEnvelope(Element element, ref double width, ref double height)
        {
            width = ReadMm(element, BuiltInParameter.RBS_CURVE_WIDTH_PARAM,
                BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM);
            height = ReadMm(element, BuiltInParameter.RBS_CURVE_HEIGHT_PARAM,
                BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM);
            if (width <= 0 || height <= 0)
            {
                double diameter = ReadMm(element, BuiltInParameter.RBS_PIPE_DIAMETER_PARAM,
                    BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM,
                    BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                if (diameter > 0) { width = diameter; height = diameter; }
            }
            if (width <= 0 || height <= 0)
            {
                BoundingBoxXYZ box = element.get_BoundingBox(null);
                if (box != null)
                {
                    Transform t = box.Transform ?? Transform.Identity;
                    XYZ size = box.Max - box.Min;
                    width = Math.Max(width, Math.Max(size.X, size.Y) * 304.8);
                    height = Math.Max(height, Math.Min(size.X, size.Y) * 304.8);
                }
            }
        }

        private static double ReadMm(Element element, params BuiltInParameter[] parameters)
        {
            foreach (BuiltInParameter bip in parameters)
            {
                Parameter p = element.get_Parameter(bip);
                if (p != null && p.StorageType == StorageType.Double && p.HasValue)
                {
                    double value = p.AsDouble() * 304.8;
                    if (value > 0) return value;
                }
            }
            return 0;
        }

        private static XYZ GetElementCentre(Element element)
        {
            LocationPoint lp = element.Location as LocationPoint;
            if (lp != null) return lp.Point;
            LocationCurve lc = element.Location as LocationCurve;
            if (lc != null) return lc.Curve.Evaluate(0.5, true);
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            return box == null ? null : (box.Transform ?? Transform.Identity).OfPoint((box.Min + box.Max) * 0.5);
        }

        private static string GetDiscipline(Element e)
        {
            int id = e.Category == null ? -1 : e.Category.Id.IntegerValue;
            if (id == (int)BuiltInCategory.OST_DuctCurves || id == (int)BuiltInCategory.OST_FlexDuctCurves ||
                id == (int)BuiltInCategory.OST_DuctFitting || id == (int)BuiltInCategory.OST_DuctAccessory ||
                id == (int)BuiltInCategory.OST_MechanicalEquipment) return "HVAC";
            if (id == (int)BuiltInCategory.OST_PipeCurves || id == (int)BuiltInCategory.OST_FlexPipeCurves ||
                id == (int)BuiltInCategory.OST_PipeFitting || id == (int)BuiltInCategory.OST_PipeAccessory) return "Plumbing";
            return "Electrical";
        }

        private static int GetPriority(string discipline)
        {
            return string.Equals(discipline, "Plumbing", StringComparison.OrdinalIgnoreCase) ? 3 :
                   string.Equals(discipline, "HVAC", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
        }
    }
}