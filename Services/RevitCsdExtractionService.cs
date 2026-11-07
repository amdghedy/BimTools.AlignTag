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
            public int ElementsTestedForSection { get; set; }
            public int SectionIntersections { get; set; }
            public int ElementsOutsideSection { get; set; }
        }

        /// <summary>Measured view-plane projection; route centreline/profile or conservative fitting/equipment bounding envelope.</summary>
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
        }
            public double MinXmm { get; set; }
            public double MaxXmm { get; set; }
            public double MinYmm { get; set; }
            public double MaxYmm { get; set; }
            public int ConnectorCount { get; set; }
            public int ElementsWithoutConnectors { get; set; }
            public int SkippedElements { get; set; }
            public string SourceDescription { get; set; }
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
                        XYZ localCentre = GetElementCentre(linkedElement);
                        XYZ hostCentre = localCentre == null ? null : transform.OfPoint(localCentre);
                        if (hostCentre == null || !IsInsideActiveCrop(view, hostCentre)) continue;
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
                        origin, uniqueId, name, discipline, width, height);
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


        private const double SectionPlaneToleranceFeet = 0.0164041995; // 5 mm

        private static IList<SectionProjectionItem> ProjectElementToSection(
            Element element, Transform toHost, XYZ right, XYZ up, XYZ normal, XYZ origin,
            string id, string name, string discipline, double profileWidthMm, double profileHeightMm)
        {
            var output = new List<SectionProjectionItem>();
            MEPCurve mepCurve = element as MEPCurve;
            LocationCurve location = element.Location as LocationCurve;
            if (mepCurve != null && location != null && location.Curve != null)
            {
                IList<XYZ> points;
                try { points = location.Curve.Tessellate(); }
                catch { points = new List<XYZ>(); }
                var hits = new List<XYZ>();
                for (int i = 0; i < points.Count - 1; i++)
                {
                    XYZ a = toHost.OfPoint(points[i]);
                    XYZ b = toHost.OfPoint(points[i + 1]);
                    double da = (a - origin).DotProduct(normal);
                    double db = (b - origin).DotProduct(normal);
                    if (Math.Abs(da) <= SectionPlaneToleranceFeet && Math.Abs(db) <= SectionPlaneToleranceFeet)
                    {
                        AddUniqueHit(hits, a);
                        AddUniqueHit(hits, b);
                    }
                    else if ((da <= SectionPlaneToleranceFeet && db >= -SectionPlaneToleranceFeet) ||
                             (db <= SectionPlaneToleranceFeet && da >= -SectionPlaneToleranceFeet))
                    {
                        double denominator = da - db;
                        if (Math.Abs(denominator) < 1e-9) continue;
                        double t = da / denominator;
                        if (t >= -0.000001 && t <= 1.000001)
                            AddUniqueHit(hits, a + (b - a) * Math.Max(0, Math.Min(1, t)));
                    }
                }
                foreach (XYZ hit in hits)
                {
                    output.Add(new SectionProjectionItem
                    {
                        Id = id, Name = name, Discipline = discipline,
                        CenterXmm = (hit - origin).DotProduct(right) * 304.8,
                        CenterYmm = (hit - origin).DotProduct(up) * 304.8,
                        WidthMm = Math.Max(10, profileWidthMm),
                        HeightMm = Math.Max(10, profileHeightMm),
                        PlaneOffsetMm = (hit - origin).DotProduct(normal) * 304.8,
                        ProjectionMethod = "Route centreline / connector profile"
                    });
                }
                if (output.Count > 0) return output;
            }

            // Fittings/accessories/equipment use a conservative transformed 3D box envelope.
            BoundingBoxXYZ box = null;
            try { box = element.get_BoundingBox(null); } catch { }
            if (box == null) return output;
            Transform boxTransform = box.Transform ?? Transform.Identity;
            double minDistance = double.MaxValue, maxDistance = double.MinValue;
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            for (int ix = 0; ix <= 1; ix++)
            for (int iy = 0; iy <= 1; iy++)
            for (int iz = 0; iz <= 1; iz++)
            {
                XYZ local = new XYZ(ix == 0 ? box.Min.X : box.Max.X,
                    iy == 0 ? box.Min.Y : box.Max.Y, iz == 0 ? box.Min.Z : box.Max.Z);
                XYZ host = toHost.OfPoint(boxTransform.OfPoint(local));
                double distance = (host - origin).DotProduct(normal);
                double x = (host - origin).DotProduct(right) * 304.8;
                double y = (host - origin).DotProduct(up) * 304.8;
                minDistance = Math.Min(minDistance, distance);
                maxDistance = Math.Max(maxDistance, distance);
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }
            if (minDistance > SectionPlaneToleranceFeet || maxDistance < -SectionPlaneToleranceFeet)
                return output;
            output.Add(new SectionProjectionItem
            {
                Id = id, Name = name, Discipline = discipline,
                CenterXmm = (minX + maxX) * 0.5,
                CenterYmm = (minY + maxY) * 0.5,
                WidthMm = Math.Max(10, maxX - minX),
                HeightMm = Math.Max(10, maxY - minY),
                PlaneOffsetMm = 0,
                ProjectionMethod = "Conservative 3D bounding-box envelope"
            });
            return output;
        }

        private static void AddUniqueHit(IList<XYZ> hits, XYZ point)
        {
            foreach (XYZ existing in hits)
                if (existing.DistanceTo(point) < 0.00656168) return; // 2 mm
            hits.Add(point);
        }

        private sealed class SourceElement
        {
            public Element Element;
            public Transform ToHost;
            public string Prefix;
            public string ModelName;
        }

        private static bool IsInsideActiveCrop(View view, XYZ hostPoint)
        {
            try
            {
                if (!view.CropBoxActive) return true;
                BoundingBoxXYZ crop = view.CropBox;
                if (crop == null) return true;
                XYZ local = (crop.Transform ?? Transform.Identity).Inverse.OfPoint(hostPoint);
                const double tolerance = 0.01;
                return local.X >= crop.Min.X - tolerance && local.X <= crop.Max.X + tolerance &&
                       local.Y >= crop.Min.Y - tolerance && local.Y <= crop.Max.Y + tolerance &&
                       local.Z >= crop.Min.Z - tolerance && local.Z <= crop.Max.Z + tolerance;
            }
            catch { return true; }
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