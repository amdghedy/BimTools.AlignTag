using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;

namespace AlignTool.Services
{
    /// <summary>
    /// Read-only Revit 2020 adapter for complete MEP element geometry, linked models,
    /// attached insulation/lining, native solid intersection checks and connector references.
    /// Must be called from a valid Revit API context.
    /// </summary>
    public static class Revit3DCoordinationExtractionService
    {
        private const double CubicFootToCubicMillimetres = 28316846.592;
        private const double FeetToMm = 304.8;

        public sealed class ElementRecord
        {
            public string Key { get; set; }
            public string UniqueId { get; set; }
            public string Name { get; set; }
            public string Category { get; set; }
            public string Discipline { get; set; }
            public string SystemName { get; set; }
            public string ModelName { get; set; }
            public bool IsLinked { get; set; }
            public IList<Solid> Solids { get; set; }
            public IList<string> GeometryKinds { get; set; }
            public XYZ BoundsMin { get; set; }
            public XYZ BoundsMax { get; set; }
        }

        public sealed class ConnectorRecord
        {
            public string Key { get; set; }
            public string OwnerKey { get; set; }
            public string OwnerUniqueId { get; set; }
            public string ModelName { get; set; }
            public int ConnectorId { get; set; }
            public IList<string> ResolvedReferences { get; set; }
            public IList<string> UnresolvedReferences { get; set; }
            public string ConnectorType { get; set; }
            public string Domain { get; set; }
            public string Shape { get; set; }
            public XYZ OriginHost { get; set; }
            public bool IsConnected { get; set; }
            public IList<string> References { get; set; }
        }

        public sealed class ClashRecord
        {
            public string FirstElementKey { get; set; }
            public string SecondElementKey { get; set; }
            public string FirstName { get; set; }
            public string SecondName { get; set; }
            public string FirstModel { get; set; }
            public string SecondModel { get; set; }
            public double IntersectionVolumeMm3 { get; set; }
            public int SolidPairsTested { get; set; }
            public string FirstGeometryKind { get; set; }
            public string SecondGeometryKind { get; set; }
            public bool IsInsulationOrLiningRelated { get; set; }
            public string GeometryBasis { get; set; }
        }

        public sealed class ExtractionResult
        {
            public IList<ElementRecord> Elements { get; set; }
            public IList<ConnectorRecord> Connectors { get; set; }
            public IList<ClashRecord> Clashes { get; set; }
            public int ElementsScanned { get; set; }
            public int ElementsWithSolids { get; set; }
            public int SolidsExtracted { get; set; }
            public int InsulationElements { get; set; }
            public int LiningElements { get; set; }
            public int GeometryFailures { get; set; }
            public int BroadPhasePairs { get; set; }
            public int SolidPairsTested { get; set; }
            public int BooleanFailures { get; set; }
            public int ConnectorReferences { get; set; }
            public int ResolvedConnectorLinks { get; set; }
            public int UnresolvedConnectorReferences { get; set; }
            public int ConnectorDomainMismatches { get; set; }
            public int ConnectorEndpointGaps { get; set; }
            public int OpenConnectors { get; set; }
            public int LinkedModelsScanned { get; set; }
            public string SourceDescription { get; set; }
        }

        private sealed class Source
        {
            public Element Element;
            public Transform ToHost;
            public string Prefix;
            public string ModelName;
            public bool IsLinked;
        }

        public static ExtractionResult Extract(Document host, View view, IEnumerable<ElementId> selection)
        {
            if (host == null) throw new ArgumentNullException("host");
            if (view == null) throw new ArgumentNullException("view");

            var result = new ExtractionResult
            {
                Elements = new List<ElementRecord>(),
                Connectors = new List<ConnectorRecord>(),
                Clashes = new List<ClashRecord>()
            };
            var sources = new List<Source>();
            var selectedLinks = new List<RevitLinkInstance>();
            var selectedIds = selection == null ? new List<ElementId>() : selection.ToList();

            foreach (ElementId id in selectedIds)
            {
                Element element = host.GetElement(id);
                if (element is RevitLinkInstance) selectedLinks.Add((RevitLinkInstance)element);
                else if (element != null && IsMep(element))
                    sources.Add(new Source { Element = element, ToHost = Transform.Identity,
                        Prefix = "HOST", ModelName = host.Title, IsLinked = false });
            }

            bool selectionMode = sources.Count > 0 || selectedLinks.Count > 0;
            if (!selectionMode)
            {
                foreach (Element element in new FilteredElementCollector(host, view.Id)
                    .WhereElementIsNotElementType().ToElements())
                    if (IsMep(element))
                        sources.Add(new Source { Element = element, ToHost = Transform.Identity,
                            Prefix = "HOST", ModelName = host.Title, IsLinked = false });
            }

            IList<RevitLinkInstance> links = selectedLinks;
            if (links.Count == 0)
            {
                links = new FilteredElementCollector(host, view.Id)
                    .OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>().ToList();
            }

            foreach (RevitLinkInstance link in links)
            {
                try
                {
                    Document linked = link.GetLinkDocument();
                    if (linked == null) continue;
                    Transform transform = link.GetTotalTransform();
                    result.LinkedModelsScanned++;
                    foreach (Element element in new FilteredElementCollector(linked)
                        .WhereElementIsNotElementType().ToElements())
                    {
                        if (!IsMep(element) || !IntersectsViewCrop(view, element, transform)) continue;
                        sources.Add(new Source { Element = element, ToHost = transform,
                            Prefix = "LINK" + link.Id.IntegerValue, ModelName = linked.Title, IsLinked = true });
                    }
                }
                catch { result.GeometryFailures++; }
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Source source in sources)
            {
                Element element = source.Element;
                if (element == null) continue;
                string key = source.Prefix + ":" + element.Id.IntegerValue;
                if (!seen.Add(key)) continue;
                result.ElementsScanned++;
                try
                {
                    var record = new ElementRecord
                    {
                        Key = key, UniqueId = element.UniqueId,
                        Name = SafeName(element), Category = element.Category == null ? "" : element.Category.Name,
                        Discipline = GetDiscipline(element), ModelName = source.ModelName,
                        IsLinked = source.IsLinked, Solids = new List<Solid>(),
                        GeometryKinds = new List<string>()
                    };
                    ExtractSolids(element, source.ToHost, "Element", record, result);
                    AddAttachedGeometry(element, source.ToHost, record, result, true);
                    AddAttachedGeometry(element, source.ToHost, record, result, false);
                    ReadBounds(element, source.ToHost, record);
                    ExpandBoundsFromSolids(record);
                    ReadSystemName(element, out string systemName);
                    record.SystemName = systemName;
                    if (record.Solids.Count > 0) result.ElementsWithSolids++;
                    result.SolidsExtracted += record.Solids.Count;
                    result.Elements.Add(record);
                    ReadConnectors(element, source.ToHost, key, result);
                }
                catch { result.GeometryFailures++; }
            }

            result.OpenConnectors = result.Connectors.Count(x => !x.IsConnected);
            result.ConnectorReferences = result.Connectors.Sum(x => x.References.Count);
            ResolveConnectorReferences(result);
            DetectNativeSolidClashes(result);
            result.SourceDescription = (selectionMode ? "Selected host MEP / selected links" : "Active view MEP") +
                "; loaded linked models scanned: " + result.LinkedModelsScanned;
            return result;
        }

        private static void AddAttachedGeometry(Element element, Transform transform,
            ElementRecord record, ExtractionResult result, bool insulation)
        {
            try
            {
                ICollection<ElementId> ids = insulation
                    ? InsulationLiningBase.GetInsulationIds(element.Document, element.Id)
                    : InsulationLiningBase.GetLiningIds(element.Document, element.Id);
                foreach (ElementId id in ids)
                {
                    Element attached = element.Document.GetElement(id);
                    if (attached == null) continue;
                    if (insulation) result.InsulationElements++; else result.LiningElements++;
                    ExtractSolids(attached, transform, insulation ? "Insulation" : "Lining", record, result);
                }
            }
            catch { result.GeometryFailures++; }
        }

        private static void ExtractSolids(Element element, Transform transform, string kind,
            ElementRecord record, ExtractionResult result)
        {
            try
            {
                var options = new Options { ComputeReferences = true,
                    IncludeNonVisibleObjects = true, DetailLevel = ViewDetailLevel.Fine };
                GeometryElement geometry = element.get_Geometry(options);
                if (geometry != null) CollectGeometry(geometry, transform ?? Transform.Identity, kind, record, result);
            }
            catch { result.GeometryFailures++; }
        }

        private static void CollectGeometry(GeometryElement geometry, Transform transform, string kind,
            ElementRecord record, ExtractionResult result)
        {
            foreach (GeometryObject obj in geometry)
            {
                Solid solid = obj as Solid;
                if (solid != null)
                {
                    try
                    {
                        if (solid.Faces.Size == 0 || solid.Volume <= 1e-12) continue;
                        Solid hostSolid = SolidUtils.CreateTransformed(solid, transform);
                        if (hostSolid != null && hostSolid.Faces.Size > 0 && hostSolid.Volume > 1e-12)
                        {
                            record.Solids.Add(hostSolid);
                            record.GeometryKinds.Add(kind);
                        }
                    }
                    catch { result.GeometryFailures++; }
                    continue;
                }
                GeometryInstance instance = obj as GeometryInstance;
                if (instance != null)
                {
                    try
                    {
                        GeometryElement nested = instance.GetInstanceGeometry();
                        if (nested != null) CollectGeometry(nested, transform, kind, record, result);
                    }
                    catch { result.GeometryFailures++; }
                }
            }
        }

        private static void ReadConnectors(Element element, Transform transform, string ownerKey,
            ExtractionResult result)
        {
            ConnectorSet set = null;
            MEPCurve curve = element as MEPCurve;
            if (curve != null) set = curve.ConnectorManager.Connectors;
            FamilyInstance family = element as FamilyInstance;
            if (set == null && family != null && family.MEPModel != null)
                set = family.MEPModel.ConnectorManager.Connectors;
            if (set == null) return;

            var local = new Dictionary<int, ConnectorRecord>();
            foreach (Connector connector in set)
            {
                if (connector == null) continue;
                try
                {
                    string connectorKey = ownerKey + "/C" + connector.Id;
                    var item = new ConnectorRecord
                    {
                        Key = connectorKey, OwnerKey = ownerKey, OwnerUniqueId = element.UniqueId,
                        ModelName = result.Elements.Last().ModelName, ConnectorId = connector.Id,
                        ResolvedReferences = new List<string>(), UnresolvedReferences = new List<string>(),
                        ConnectorType = connector.ConnectorType.ToString(),
                        Domain = connector.Domain.ToString(), Shape = connector.Shape.ToString(),
                        OriginHost = transform.OfPoint(connector.Origin),
                        IsConnected = connector.IsConnected,
                        References = new List<string>()
                    };
                    local[connector.Id] = item;
                    result.Connectors.Add(item);
                }
                catch { result.GeometryFailures++; }
            }

            foreach (Connector connector in set)
            {
                ConnectorRecord item;
                if (connector == null || !local.TryGetValue(connector.Id, out item)) continue;
                try
                {
                    foreach (Connector reference in connector.AllRefs)
                    {
                        if (reference == null || reference.Owner == null || reference.Owner.Id == element.Id) continue;
                        string refKey = ownerKey + "/C" + reference.Id;
                        // Connector references are only resolved to records in the same source element
                        // document. A linked connector's counterpart is captured while that link is scanned.
                        item.References.Add(result.Elements.Last().ModelName + "|" + reference.Owner.UniqueId + "/C" + reference.Id);
                    }
                }
                catch { result.GeometryFailures++; }
            }
        }

        private static void ResolveConnectorReferences(ExtractionResult result)
        {
            var index = result.Connectors.ToDictionary(
                x => x.ModelName + "|" + x.OwnerUniqueId + "/C" + x.ConnectorId,
                x => x, StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ConnectorRecord connector in result.Connectors)
            {
                foreach (string reference in connector.References)
                {
                    ConnectorRecord target;
                    if (!index.TryGetValue(reference, out target))
                    {
                        connector.UnresolvedReferences.Add(reference);
                        result.UnresolvedConnectorReferences++;
                        continue;
                    }
                    connector.ResolvedReferences.Add(target.Key);
                    string pairKey = string.Compare(connector.Key, target.Key, StringComparison.OrdinalIgnoreCase) < 0
                        ? connector.Key + "|" + target.Key : target.Key + "|" + connector.Key;
                    if (!seen.Add(pairKey)) continue;
                    result.ResolvedConnectorLinks++;
                    if (!string.Equals(connector.Domain, target.Domain, StringComparison.OrdinalIgnoreCase))
                        result.ConnectorDomainMismatches++;
                    if (connector.OriginHost != null && target.OriginHost != null &&
                        connector.OriginHost.DistanceTo(target.OriginHost) * FeetToMm > 2.0)
                        result.ConnectorEndpointGaps++;
                }
            }
        }

        private static void DetectNativeSolidClashes(ExtractionResult result)
        {
            for (int i = 0; i < result.Elements.Count; i++)
            for (int j = i + 1; j < result.Elements.Count; j++)
            {
                ElementRecord a = result.Elements[i], b = result.Elements[j];
                if (!BoundsOverlap(a, b)) continue;
                result.BroadPhasePairs++;
                double maxIntersectionVolume = 0;
                int tested = 0;
                string firstKind = "", secondKind = "";
                for (int ai = 0; ai < a.Solids.Count; ai++)
                for (int bi = 0; bi < b.Solids.Count; bi++)
                {
                    Solid solidA = a.Solids[ai], solidB = b.Solids[bi];
                    if (!SolidBoundsOverlap(solidA, solidB)) continue;
                    tested++;
                    result.SolidPairsTested++;
                    try
                    {
                        Solid intersection = BooleanOperationsUtils.ExecuteBooleanOperation(
                            solidA, solidB, BooleanOperationsType.Intersect);
                        if (intersection != null && intersection.Faces.Size > 0 && intersection.Volume > maxIntersectionVolume)
                        {
                            maxIntersectionVolume = intersection.Volume;
                            firstKind = a.GeometryKinds[ai];
                            secondKind = b.GeometryKinds[bi];
                        }
                    }
                    catch { result.BooleanFailures++; }
                }
                if (maxIntersectionVolume > 1e-12)
                    result.Clashes.Add(new ClashRecord
                    {
                        FirstElementKey = a.Key, SecondElementKey = b.Key,
                        FirstName = a.Name, SecondName = b.Name,
                        FirstModel = a.ModelName, SecondModel = b.ModelName,
                        IntersectionVolumeMm3 = maxIntersectionVolume * CubicFootToCubicMillimetres,
                        SolidPairsTested = tested, FirstGeometryKind = firstKind, SecondGeometryKind = secondKind,
                        IsInsulationOrLiningRelated = firstKind != "Element" || secondKind != "Element",
                        GeometryBasis = "Native Revit solid Boolean intersection; element, attached insulation and lining solids included."
                    });
            }
        }

        private static bool BoundsOverlap(ElementRecord a, ElementRecord b)
        {
            if (a.BoundsMin == null || a.BoundsMax == null || b.BoundsMin == null || b.BoundsMax == null) return true;
            return Overlap(a.BoundsMin.X, a.BoundsMax.X, b.BoundsMin.X, b.BoundsMax.X) &&
                   Overlap(a.BoundsMin.Y, a.BoundsMax.Y, b.BoundsMin.Y, b.BoundsMax.Y) &&
                   Overlap(a.BoundsMin.Z, a.BoundsMax.Z, b.BoundsMin.Z, b.BoundsMax.Z);
        }

        private static bool SolidBoundsOverlap(Solid a, Solid b)
        {
            try
            {
                BoundingBoxXYZ x = a.GetBoundingBox(), y = b.GetBoundingBox();
                if (x == null || y == null) return true;
                XYZ[] xa = BoxCorners(x), ya = BoxCorners(y);
                return AabbOverlap(xa, ya);
            }
            catch { return true; }
        }

        private static void ReadBounds(Element element, Transform transform, ElementRecord record)
        {
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return;
            XYZ min = new XYZ(double.MaxValue, double.MaxValue, double.MaxValue);
            XYZ max = new XYZ(double.MinValue, double.MinValue, double.MinValue);
            XYZ[] corners = BoxCorners(box);
            foreach (XYZ corner in corners)
            {
                XYZ p = transform.OfPoint(corner);
                min = new XYZ(Math.Min(min.X,p.X),Math.Min(min.Y,p.Y),Math.Min(min.Z,p.Z));
                max = new XYZ(Math.Max(max.X,p.X),Math.Max(max.Y,p.Y),Math.Max(max.Z,p.Z));
            }
            record.BoundsMin = min; record.BoundsMax = max;
        }

        private static void ExpandBoundsFromSolids(ElementRecord record)
        {
            foreach (Solid solid in record.Solids)
            {
                try
                {
                    BoundingBoxXYZ box = solid.GetBoundingBox();
                    if (box == null) continue;
                    foreach (XYZ p in BoxCorners(box))
                    {
                        if (record.BoundsMin == null)
                        {
                            record.BoundsMin = p; record.BoundsMax = p; continue;
                        }
                        record.BoundsMin = new XYZ(Math.Min(record.BoundsMin.X,p.X),
                            Math.Min(record.BoundsMin.Y,p.Y),Math.Min(record.BoundsMin.Z,p.Z));
                        record.BoundsMax = new XYZ(Math.Max(record.BoundsMax.X,p.X),
                            Math.Max(record.BoundsMax.Y,p.Y),Math.Max(record.BoundsMax.Z,p.Z));
                    }
                }
                catch { }
            }
        }

        private static XYZ[] BoxCorners(BoundingBoxXYZ box)
        {
            Transform t = box.Transform ?? Transform.Identity;
            return new[]
            {
                t.OfPoint(new XYZ(box.Min.X,box.Min.Y,box.Min.Z)),
                t.OfPoint(new XYZ(box.Min.X,box.Min.Y,box.Max.Z)),
                t.OfPoint(new XYZ(box.Min.X,box.Max.Y,box.Min.Z)),
                t.OfPoint(new XYZ(box.Min.X,box.Max.Y,box.Max.Z)),
                t.OfPoint(new XYZ(box.Max.X,box.Min.Y,box.Min.Z)),
                t.OfPoint(new XYZ(box.Max.X,box.Min.Y,box.Max.Z)),
                t.OfPoint(new XYZ(box.Max.X,box.Max.Y,box.Min.Z)),
                t.OfPoint(new XYZ(box.Max.X,box.Max.Y,box.Max.Z))
            };
        }

        private static bool AabbOverlap(XYZ[] a, XYZ[] b)
        {
            double aminX=a.Min(p=>p.X),amaxX=a.Max(p=>p.X),aminY=a.Min(p=>p.Y),amaxY=a.Max(p=>p.Y),aminZ=a.Min(p=>p.Z),amaxZ=a.Max(p=>p.Z);
            double bminX=b.Min(p=>p.X),bmaxX=b.Max(p=>p.X),bminY=b.Min(p=>p.Y),bmaxY=b.Max(p=>p.Y),bminZ=b.Min(p=>p.Z),bmaxZ=b.Max(p=>p.Z);
            return Overlap(aminX,amaxX,bminX,bmaxX)&&Overlap(aminY,amaxY,bminY,bmaxY)&&Overlap(aminZ,amaxZ,bminZ,bmaxZ);
        }

        private static void ReadSystemName(Element element, out string name)
        {
            name = "";
            try
            {
                MEPCurve curve = element as MEPCurve;
                if (curve != null && curve.MEPSystem != null) { name = curve.MEPSystem.Name; return; }
                FamilyInstance family = element as FamilyInstance;
                if (family != null && family.MEPModel != null)
                {
                    foreach (Connector c in family.MEPModel.ConnectorManager.Connectors)
                        if (c.MEPSystem != null) { name = c.MEPSystem.Name; return; }
                }
            }
            catch { }
        }

        private static bool IntersectsViewCrop(View view, Element element, Transform linkTransform)
        {
            try
            {
                if (!view.CropBoxActive) return true;
                BoundingBoxXYZ crop=view.CropBox, box=element.get_BoundingBox(null);
                if(crop==null||box==null)return true;
                XYZ[] corners=BoxCorners(box).Select(linkTransform.OfPoint).Select((p)=> (crop.Transform??Transform.Identity).Inverse.OfPoint(p)).ToArray();
                double minX=corners.Min(p=>p.X),maxX=corners.Max(p=>p.X),minY=corners.Min(p=>p.Y),maxY=corners.Max(p=>p.Y),minZ=corners.Min(p=>p.Z),maxZ=corners.Max(p=>p.Z);
                const double tol=0.01;
                return maxX>=crop.Min.X-tol&&minX<=crop.Max.X+tol&&maxY>=crop.Min.Y-tol&&minY<=crop.Max.Y+tol&&maxZ>=crop.Min.Z-tol&&minZ<=crop.Max.Z+tol;
            }
            catch { return true; }
        }

        private static bool IsMep(Element e)
        {
            if(e.Category==null)return false;
            int id=e.Category.Id.IntegerValue;
            return id==(int)BuiltInCategory.OST_DuctCurves||id==(int)BuiltInCategory.OST_FlexDuctCurves||
                id==(int)BuiltInCategory.OST_DuctFitting||id==(int)BuiltInCategory.OST_DuctAccessory||
                id==(int)BuiltInCategory.OST_PipeCurves||id==(int)BuiltInCategory.OST_FlexPipeCurves||
                id==(int)BuiltInCategory.OST_PipeFitting||id==(int)BuiltInCategory.OST_PipeAccessory||
                id==(int)BuiltInCategory.OST_CableTray||id==(int)BuiltInCategory.OST_CableTrayFitting||
                id==(int)BuiltInCategory.OST_Conduit||id==(int)BuiltInCategory.OST_ConduitFitting||
                id==(int)BuiltInCategory.OST_MechanicalEquipment||id==(int)BuiltInCategory.OST_ElectricalEquipment;
        }
        private static string GetDiscipline(Element e)
        {
            int id=e.Category==null?-1:e.Category.Id.IntegerValue;
            if(id==(int)BuiltInCategory.OST_DuctCurves||id==(int)BuiltInCategory.OST_FlexDuctCurves||
                id==(int)BuiltInCategory.OST_DuctFitting||id==(int)BuiltInCategory.OST_DuctAccessory||
                id==(int)BuiltInCategory.OST_MechanicalEquipment)return "HVAC";
            if(id==(int)BuiltInCategory.OST_PipeCurves||id==(int)BuiltInCategory.OST_FlexPipeCurves||
                id==(int)BuiltInCategory.OST_PipeFitting||id==(int)BuiltInCategory.OST_PipeAccessory)return "Plumbing";
            return "Electrical";
        }
        private static string SafeName(Element e){try{return e.Name??e.GetType().Name;}catch{return e.GetType().Name;}}
        private static bool Overlap(double a,double b,double c,double d){return a<=d&&b>=c;}
    }
}