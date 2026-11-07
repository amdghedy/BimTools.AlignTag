using System;
using System.Collections.Generic;
using System.Linq;

namespace AlignTool.Services
{
    /// <summary>Revit-independent 3D route-envelope coordination core. Units: millimetres.
    /// Swept circular envelopes are a screening model, not native Revit solid Boolean geometry.</summary>
    public static class Csd3DCoordinationEngine
    {
        public sealed class Point3
        {
            public double X { get; set; } public double Y { get; set; } public double Z { get; set; }
            public Point3() { }
            public Point3(double x, double y, double z) { X=x; Y=y; Z=z; }
            public static Point3 operator -(Point3 a, Point3 b) { return new Point3(a.X-b.X,a.Y-b.Y,a.Z-b.Z); }
            public static Point3 operator *(Point3 a, double s) { return new Point3(a.X*s,a.Y*s,a.Z*s); }
            public double Dot(Point3 b) { return X*b.X+Y*b.Y+Z*b.Z; }
            public double Length() { return Math.Sqrt(Dot(this)); }
        }
        public sealed class RouteSegment
        {
            public string ServiceId { get; set; } public string SegmentId { get; set; }
            public string SystemName { get; set; } public string Discipline { get; set; }
            public Point3 Start { get; set; } public Point3 End { get; set; }
            public double OutsideRadiusMm { get; set; } public double InsulationThicknessMm { get; set; }
            public double RequiredClearanceMm { get; set; } public bool IsMeasuredGeometry { get; set; }
        }
        public sealed class ConnectorNode
        {
            public string Id { get; set; } public string ServiceId { get; set; }
            public string Domain { get; set; } public Point3 Origin { get; set; }
            public bool IsConnected { get; set; }
        }
        public sealed class ConnectorLink
        {
            public string FirstConnectorId { get; set; } public string SecondConnectorId { get; set; }
            public double AllowedGapMm { get; set; }
        }
        public sealed class ClearanceVolume
        {
            public string Id { get; set; } public string Description { get; set; }
            public Point3 Min { get; set; } public Point3 Max { get; set; }
            public string ExemptServiceId { get; set; }
        }
        public sealed class SectionStation
        {
            public string Id { get; set; } public double StationMm { get; set; }
            public Point3 Origin { get; set; } public Point3 Normal { get; set; }
            public double HalfThicknessMm { get; set; } public IList<string> RequiredServiceIds { get; set; }
        }
        public sealed class CoordinationInput
        {
            public IList<RouteSegment> Segments { get; set; } = new List<RouteSegment>();
            public IList<ConnectorNode> Connectors { get; set; } = new List<ConnectorNode>();
            public IList<ConnectorLink> ExpectedLinks { get; set; } = new List<ConnectorLink>();
            public IList<ClearanceVolume> ClearanceVolumes { get; set; } = new List<ClearanceVolume>();
            public IList<SectionStation> Sections { get; set; } = new List<SectionStation>();
            public double ClashToleranceMm { get; set; } = 1.0;
        }
        public sealed class ClashFinding
        {
            public string FirstServiceId { get; set; } public string SecondServiceId { get; set; }
            public string FirstSegmentId { get; set; } public string SecondSegmentId { get; set; }
            public double SurfaceGapMm { get; set; } public bool IsHardClash { get; set; }
            public string Description { get; set; }
        }
        public sealed class ConnectorFinding
        {
            public string ConnectorId { get; set; } public string RelatedConnectorId { get; set; }
            public string Severity { get; set; } public string Description { get; set; }
        }
        public sealed class SectionResult
        {
            public string SectionId { get; set; } public double StationMm { get; set; }
            public IList<string> PresentServiceIds { get; set; } public IList<string> MissingServiceIds { get; set; }
            public bool Complete { get { return MissingServiceIds.Count == 0; } }
        }
        public sealed class ClearanceFinding
        {
            public string ServiceId { get; set; } public string SegmentId { get; set; }
            public string ClearanceVolumeId { get; set; } public string Description { get; set; }
        }
        public sealed class CoordinationResult
        {
            public IList<ClashFinding> Clashes { get; set; } = new List<ClashFinding>();
            public IList<ConnectorFinding> ConnectorFindings { get; set; } = new List<ConnectorFinding>();
            public IList<SectionResult> Sections { get; set; } = new List<SectionResult>();
            public IList<ClearanceFinding> ClearanceFindings { get; set; } = new List<ClearanceFinding>();
            public int BroadPhasePairs { get; set; } public int NarrowPhasePairs { get; set; }
            public int MeasuredSegments { get; set; } public int ProxySegments { get; set; }
            public string GeometryBasis { get; set; } public string Status { get; set; }
            public bool HasHardClashes { get { return Clashes.Any(x => x.IsHardClash); } }
            public bool HasBlockingFindings { get { return HasHardClashes ||
                ConnectorFindings.Any(x => x.Severity == "Error") || Sections.Any(x => !x.Complete) ||
                ClearanceFindings.Count > 0; } }
        }

        public static CoordinationResult Analyze(CoordinationInput input)
        {
            Validate(input);
            var result=new CoordinationResult {
                MeasuredSegments=input.Segments.Count(x=>x.IsMeasuredGeometry),
                ProxySegments=input.Segments.Count(x=>!x.IsMeasuredGeometry),
                GeometryBasis="Swept circular envelopes; measured/proxy provenance is retained."
            };
            for(int i=0;i<input.Segments.Count;i++) for(int j=i+1;j<input.Segments.Count;j++)
            {
                RouteSegment a=input.Segments[i], b=input.Segments[j];
                if(Same(a.ServiceId,b.ServiceId) || !BoundsOverlap(a,b,Math.Max(a.RequiredClearanceMm,b.RequiredClearanceMm))) continue;
                result.BroadPhasePairs++;
                double gap=SegmentDistance(a.Start,a.End,b.Start,b.End)
                    - a.OutsideRadiusMm-a.InsulationThicknessMm-b.OutsideRadiusMm-b.InsulationThicknessMm;
                result.NarrowPhasePairs++;
                if(gap < -input.ClashToleranceMm || gap+input.ClashToleranceMm < Math.Max(a.RequiredClearanceMm,b.RequiredClearanceMm))
                    result.Clashes.Add(new ClashFinding {
                        FirstServiceId=a.ServiceId,SecondServiceId=b.ServiceId,FirstSegmentId=a.SegmentId,
                        SecondSegmentId=b.SegmentId,SurfaceGapMm=gap,IsHardClash=gap < -input.ClashToleranceMm,
                        Description=gap < -input.ClashToleranceMm ? "Swept-envelope intersection including insulation." :
                            "Required service-to-service clearance is not achieved."
                    });
            }
            ValidateConnectors(input,result); ValidateSections(input,result); ValidateClearances(input,result);
            result.Status=result.HasBlockingFindings ? "Review required" : "No blocking findings detected";
            return result;
        }
        private static void ValidateConnectors(CoordinationInput input,CoordinationResult result)
        {
            var map=input.Connectors.ToDictionary(x=>x.Id,x=>x,StringComparer.OrdinalIgnoreCase);
            foreach(var c in input.Connectors)
                if(!c.IsConnected && !input.ExpectedLinks.Any(x=>Same(x.FirstConnectorId,c.Id)||Same(x.SecondConnectorId,c.Id)))
                    result.ConnectorFindings.Add(new ConnectorFinding { ConnectorId=c.Id,Severity="Warning",
                        Description="Connector is open and has no declared expected connection." });
            foreach(var link in input.ExpectedLinks)
            {
                ConnectorNode a,b;
                if(!map.TryGetValue(link.FirstConnectorId,out a)||!map.TryGetValue(link.SecondConnectorId,out b))
                { result.ConnectorFindings.Add(new ConnectorFinding { ConnectorId=link.FirstConnectorId,
                    RelatedConnectorId=link.SecondConnectorId,Severity="Error",Description="Expected connection references a missing connector." }); continue; }
                if(!Same(a.Domain,b.Domain)) result.ConnectorFindings.Add(new ConnectorFinding {
                    ConnectorId=a.Id,RelatedConnectorId=b.Id,Severity="Error",Description="Connector domains do not match." });
                double gap=(a.Origin-b.Origin).Length();
                if(gap>Math.Max(0,link.AllowedGapMm)) result.ConnectorFindings.Add(new ConnectorFinding {
                    ConnectorId=a.Id,RelatedConnectorId=b.Id,Severity="Error",
                    Description="Expected connector pair is separated by "+gap.ToString("F1")+" mm." });
            }
        }
        private static void ValidateSections(CoordinationInput input,CoordinationResult result)
        {
            foreach(var section in input.Sections.OrderBy(x=>x.StationMm))
            {
                Point3 n=Normalize(section.Normal); var present=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(var s in input.Segments)
                {
                    double a=(s.Start-section.Origin).Dot(n), b=(s.End-section.Origin).Dot(n);
                    double half=section.HalfThicknessMm+s.OutsideRadiusMm+s.InsulationThicknessMm;
                    if((a<=half&&b>=-half)||(b<=half&&a>=-half)) present.Add(s.ServiceId);
                }
                var required=section.RequiredServiceIds??new List<string>();
                result.Sections.Add(new SectionResult { SectionId=section.Id,StationMm=section.StationMm,
                    PresentServiceIds=present.OrderBy(x=>x).ToList(),
                    MissingServiceIds=required.Where(x=>!present.Contains(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() });
            }
        }
        private static void ValidateClearances(CoordinationInput input,CoordinationResult result)
        {
            foreach(var v in input.ClearanceVolumes) foreach(var s in input.Segments)
            {
                if(!string.IsNullOrEmpty(v.ExemptServiceId)&&Same(v.ExemptServiceId,s.ServiceId)) continue;
                double r=s.OutsideRadiusMm+s.InsulationThicknessMm;
                if(Overlap(Math.Min(s.Start.X,s.End.X)-r,Math.Max(s.Start.X,s.End.X)+r,v.Min.X,v.Max.X)&&
                   Overlap(Math.Min(s.Start.Y,s.End.Y)-r,Math.Max(s.Start.Y,s.End.Y)+r,v.Min.Y,v.Max.Y)&&
                   Overlap(Math.Min(s.Start.Z,s.End.Z)-r,Math.Max(s.Start.Z,s.End.Z)+r,v.Min.Z,v.Max.Z))
                    result.ClearanceFindings.Add(new ClearanceFinding { ServiceId=s.ServiceId,SegmentId=s.SegmentId,
                        ClearanceVolumeId=v.Id,Description=v.Description??"Conservative maintenance/access AABB overlap." });
            }
        }
        private static bool BoundsOverlap(RouteSegment a,RouteSegment b,double extra)
        {
            double ra=a.OutsideRadiusMm+a.InsulationThicknessMm, rb=b.OutsideRadiusMm+b.InsulationThicknessMm+extra;
            return Overlap(Math.Min(a.Start.X,a.End.X)-ra,Math.Max(a.Start.X,a.End.X)+ra,Math.Min(b.Start.X,b.End.X)-rb,Math.Max(b.Start.X,b.End.X)+rb)&&
                Overlap(Math.Min(a.Start.Y,a.End.Y)-ra,Math.Max(a.Start.Y,a.End.Y)+ra,Math.Min(b.Start.Y,b.End.Y)-rb,Math.Max(b.Start.Y,b.End.Y)+rb)&&
                Overlap(Math.Min(a.Start.Z,a.End.Z)-ra,Math.Max(a.Start.Z,a.End.Z)+ra,Math.Min(b.Start.Z,b.End.Z)-rb,Math.Max(b.Start.Z,b.End.Z)+rb);
        }
        private static double SegmentDistance(Point3 p1,Point3 q1,Point3 p2,Point3 q2)
        {
            Point3 d1=q1-p1,d2=q2-p2,r=p1-p2; double a=d1.Dot(d1),e=d2.Dot(d2),f=d2.Dot(r),s,t;
            const double eps=1e-12;
            if(a<=eps&&e<=eps)return (p1-p2).Length();
            if(a<=eps){s=0;t=Clamp(f/e);}
            else { double c=d1.Dot(r); if(e<=eps){t=0;s=Clamp(-c/a);}
                else { double b=d1.Dot(d2),den=a*e-b*b; s=Math.Abs(den)>eps?Clamp((b*f-c*e)/den):0;
                    t=(b*s+f)/e; if(t<0){t=0;s=Clamp(-c/a);} else if(t>1){t=1;s=Clamp((b-c)/a);} } }
            return (r+d1*s-d2*t).Length();
        }
        private static Point3 Normalize(Point3 p){double n=p.Length();if(n<=1e-12)throw new ArgumentException("Section normal must be non-zero.");return p*(1/n);}
        private static bool Overlap(double a,double b,double c,double d){return a<=d&&b>=c;}
        private static double Clamp(double x){return Math.Max(0,Math.Min(1,x));}
        private static bool Same(string a,string b){return string.Equals(a??"",b??"",StringComparison.OrdinalIgnoreCase);}
        private static void Validate(CoordinationInput x)
        {
            if(x==null)throw new ArgumentNullException("input");
            if(x.Segments==null||x.Connectors==null||x.ExpectedLinks==null||x.ClearanceVolumes==null||x.Sections==null)
                throw new ArgumentException("Coordination collections cannot be null.");
            if(x.Segments.Any(s=>s==null||string.IsNullOrWhiteSpace(s.ServiceId)||string.IsNullOrWhiteSpace(s.SegmentId)||
                s.Start==null||s.End==null||s.OutsideRadiusMm<0||s.InsulationThicknessMm<0||s.RequiredClearanceMm<0))
                throw new ArgumentException("Route segments require IDs, endpoints and non-negative dimensions.");
            if(x.Connectors.Any(c=>c==null||string.IsNullOrWhiteSpace(c.Id)||c.Origin==null)||
                x.Connectors.Select(c=>c.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=x.Connectors.Count)
                throw new ArgumentException("Connector records are invalid or contain duplicate IDs.");
            if(x.ClearanceVolumes.Any(v=>v==null||v.Min==null||v.Max==null||v.Min.X>v.Max.X||v.Min.Y>v.Max.Y||v.Min.Z>v.Max.Z))
                throw new ArgumentException("Clearance volume bounds are invalid.");
            if(x.Sections.Any(s=>s==null||s.Origin==null||s.Normal==null))throw new ArgumentException("Section stations need an origin and normal.");
        }
    }

    /// <summary>Human approval gate only; it never opens a Revit transaction.</summary>
    public static class CsdEngineerApprovalGate
    {
        public sealed class Review
        {
            public string CandidateId { get; set; } public string EngineerName { get; set; }
            public string ProjectProfile { get; set; } public bool GeometryReviewed { get; set; }
            public bool ConnectorContinuityReviewed { get; set; } public bool AllSectionsReviewed { get; set; }
            public bool ClearanceReviewed { get; set; } public bool ClashReportReviewed { get; set; }
            public bool Approved { get; set; } public string Comment { get; set; } public DateTime ReviewedUtc { get; set; }
        }
        public sealed class GateResult
        {
            public bool IsApproved { get; set; } public bool CanModifyRevitModel { get; set; }
            public IList<string> MissingRequirements { get; set; } public string Status { get; set; }
        }
        public static GateResult Evaluate(Review r,Csd3DCoordinationEngine.CoordinationResult c)
        {
            var missing=new List<string>();
            if(r==null)missing.Add("Engineer review record is missing.");
            else {
                if(string.IsNullOrWhiteSpace(r.CandidateId))missing.Add("Candidate ID is required.");
                if(string.IsNullOrWhiteSpace(r.EngineerName))missing.Add("Engineer name is required.");
                if(string.IsNullOrWhiteSpace(r.ProjectProfile))missing.Add("Project profile is required.");
                if(!r.GeometryReviewed)missing.Add("Measured geometry review is incomplete.");
                if(!r.ConnectorContinuityReviewed)missing.Add("Connector continuity review is incomplete.");
                if(!r.AllSectionsReviewed)missing.Add("Multi-section review is incomplete.");
                if(!r.ClearanceReviewed)missing.Add("Maintenance-clearance review is incomplete.");
                if(!r.ClashReportReviewed)missing.Add("Clash report review is incomplete.");
                if(!r.Approved)missing.Add("Engineer has not approved the candidate.");
            }
            if(c==null)missing.Add("3D coordination analysis is missing.");
            else if(c.HasBlockingFindings)missing.Add("Blocking 3D coordination findings remain.");
            return new GateResult { IsApproved=missing.Count==0,CanModifyRevitModel=false,MissingRequirements=missing,
                Status=missing.Count==0?"Approved for a separately implemented transaction-controlled application stage; no model changes are made here.":
                "Approval blocked: "+string.Join(" ",missing) };
        }
    }
}