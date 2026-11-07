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
            IEnumerable<Element> source = ids.Count > 0
                ? ids.Select(doc.GetElement).Where(e => e != null)
                : new FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType().ToElements();

            var result = new ExtractionResult { Items = new List<KnowledgeBasedCsdEngine.ServiceItem>() };
            var seen = new HashSet<int>();
            XYZ right = view.RightDirection.Normalize();
            XYZ up = view.UpDirection.Normalize();
            XYZ origin = view.Origin;
            foreach (Element element in source)
            {
                if (element == null || !IsMepElement(element) || !seen.Add(element.Id.IntegerValue)) continue;
                try
                {
                    double width, height;
                    int connectorCount;
                    string systemName;
                    ReadConnectorEnvelope(element, right, up, out width, out height, out connectorCount, out systemName);
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

                    XYZ centre = GetElementCentre(element);
                    if (centre == null) { result.SkippedElements++; continue; }
                    double x = (centre - origin).DotProduct(right) * 304.8;
                    double y = (centre - origin).DotProduct(up) * 304.8;
                    width = Math.Max(10, width);
                    height = Math.Max(10, height);
                    string discipline = GetDiscipline(element);
                    string name = element.Name ?? element.GetType().Name;
                    result.Items.Add(new KnowledgeBasedCsdEngine.ServiceItem
                    {
                        Id = element.Id.IntegerValue.ToString(),
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
                catch
                {
                    result.SkippedElements++;
                }
            }
            result.SourceDescription = ids.Count > 0 ? "Current selection" : "Active view";
            return result;
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

        private static void ReadConnectorEnvelope(Element element, XYZ right, XYZ up,
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
                double w = 0, h = 0;
                try
                {
                    if (connector.Shape == ConnectorProfileType.Rectangular ||
                        connector.Shape == ConnectorProfileType.Oval)
                    {
                        w = connector.Width * 304.8;
                        h = connector.Height * 304.8;
                    }
                    else if (connector.Shape == ConnectorProfileType.Round)
                    {
                        w = h = connector.Radius * 2 * 304.8;
                    }
                    if (string.IsNullOrEmpty(systemName) && connector.MEPSystem != null)
                        systemName = connector.MEPSystem.Name;
                }
                catch { }
                widthMm = Math.Max(widthMm, w);
                heightMm = Math.Max(heightMm, h);
            }
            if (widthMm > 0 && heightMm > 0) return;
            widthMm = 0; heightMm = 0;
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