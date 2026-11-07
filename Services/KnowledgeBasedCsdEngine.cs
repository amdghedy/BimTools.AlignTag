using System;
using System.Collections.Generic;
using System.Linq;

namespace AlignTool.Services
{
    /// <summary>Revit-independent 2D section packing engine. Coordinates are millimetres.</summary>
    public static class KnowledgeBasedCsdEngine
    {
        public sealed class ServiceItem
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string SystemName { get; set; }
            public string Discipline { get; set; }
            public double WidthMm { get; set; }
            public double HeightMm { get; set; }
            public double RequiredClearanceMm { get; set; }
            public double PreferredElevationMm { get; set; }
            public double MinimumElevationMm { get; set; }
            public bool IsFixed { get; set; }
            public double FixedXmm { get; set; }
            public double FixedYmm { get; set; }
            public int Priority { get; set; }
        }

        public sealed class SectionConstraints
        {
            public double WidthMm { get; set; }
            public double HeightMm { get; set; }
            public double SideClearanceMm { get; set; }
            public double TopClearanceMm { get; set; }
            public double BottomClearanceMm { get; set; }
            public double DefaultServiceGapMm { get; set; }
            public double GridMm { get; set; }
            public bool ElectricalAboveWetServicesPenalty { get; set; }
            public IList<ForbiddenPair> ForbiddenPairs { get; set; }
            public SectionConstraints()
            {
                SideClearanceMm = 50; TopClearanceMm = 50; BottomClearanceMm = 50;
                DefaultServiceGapMm = 50; GridMm = 25;
                ElectricalAboveWetServicesPenalty = true;
                ForbiddenPairs = new List<ForbiddenPair>();
            }
        }

        public sealed class ForbiddenPair
        {
            public string FirstDiscipline { get; set; }
            public string SecondDiscipline { get; set; }
            public double MinimumDistanceMm { get; set; }
        }

        public sealed class Placement
        {
            public string ServiceId { get; set; }
            public double Xmm { get; set; }
            public double Ymm { get; set; }
            public double WidthMm { get; set; }
            public double HeightMm { get; set; }
            public int Order { get; set; }
        }

        public sealed class Weights
        {
            public double BoundaryViolation { get; set; }
            public double Clash { get; set; }
            public double Clearance { get; set; }
            public double Compactness { get; set; }
            public double PreferredElevation { get; set; }
            public double DisciplineRule { get; set; }
            public double Movement { get; set; }
            public double Access { get; set; }
            public Weights()
            {
                BoundaryViolation = 1000000; Clash = 1000000; Clearance = 10000;
                Compactness = 1; PreferredElevation = 0.35; DisciplineRule = 2500;
                Movement = 0.15; Access = 2;
            }
            public Weights Clone()
            {
                return new Weights { BoundaryViolation = BoundaryViolation, Clash = Clash,
                    Clearance = Clearance, Compactness = Compactness,
                    PreferredElevation = PreferredElevation, DisciplineRule = DisciplineRule,
                    Movement = Movement, Access = Access };
            }
        }

        public sealed class Candidate
        {
            public string Id { get; set; }
            public string Strategy { get; set; }
            public IList<Placement> Placements { get; set; }
            public double Cost { get; set; }
            public double Fitness { get { return 1.0 / (1.0 + Math.Max(0, Cost)); } }
            public int BoundaryViolations { get; set; }
            public int Clashes { get; set; }
            public int ClearanceViolations { get; set; }
            public int RuleViolations { get; set; }
            public double UsedWidthMm { get; set; }
            public double UsedHeightMm { get; set; }
            public string Explanation { get; set; }
        }

        public sealed class SolveResult
        {
            public IList<Candidate> Candidates { get; set; }
            public Candidate Recommended { get; set; }
            public int EvaluatedCandidates { get; set; }
            public string Status { get; set; }
        }

        public sealed class EngineerAssessment
        {
            public string CandidateId { get; set; }
            public double Grade { get; set; }
            public string Comment { get; set; }
            public DateTime AssessedUtc { get; set; }
        }

        public static SolveResult Solve(IList<ServiceItem> input, SectionConstraints constraints, Weights weights)
        {
            Validate(input, constraints, weights);
            var result = new SolveResult { Candidates = new List<Candidate>(), Status = "Completed" };
            if (input.Count == 0) { result.Status = "No service items supplied."; return result; }

            var strategies = new List<KeyValuePair<string, Func<ServiceItem, object>>>
            {
                Pair("Priority / elevation", x => new { A = -x.Priority, B = -x.PreferredElevationMm, C = x.Id }),
                Pair("Largest first", x => new { A = -(x.WidthMm * x.HeightMm), B = -x.Priority, C = x.Id }),
                Pair("Wet services lower", x => new { A = WetRank(x.Discipline), B = -x.Priority, C = x.Id }),
                Pair("Electrical grouping", x => new { A = ElectricalRank(x.Discipline), B = -x.Priority, C = x.Id }),
                Pair("Narrowest first", x => new { A = x.WidthMm, B = -x.HeightMm, C = x.Id }),
                Pair("Preferred elevation", x => new { A = -x.PreferredElevationMm, B = x.WidthMm, C = x.Id })
            };

            foreach (var strategy in strategies)
            {
                Candidate candidate = Pack(input.OrderBy(strategy.Value).ToList(), constraints, weights, strategy.Key);
                candidate.Id = "CSD-" + (result.Candidates.Count + 1).ToString("000");
                result.Candidates.Add(candidate);
            }

            if (input.All(x => x.IsFixed))
            {
                Candidate baseline = Score(input.Select(x => ToPlacement(x, x.FixedXmm, x.FixedYmm, 0)).ToList(),
                    input, constraints, weights, "Existing arrangement");
                baseline.Id = "CSD-BASE";
                result.Candidates.Add(baseline);
            }
            result.Candidates = result.Candidates.OrderBy(x => x.Cost).ToList();
            result.Recommended = result.Candidates.FirstOrDefault();
            result.EvaluatedCandidates = result.Candidates.Count;
            return result;
        }

        private static KeyValuePair<string, Func<ServiceItem, object>> Pair(string name, Func<ServiceItem, object> selector)
        { return new KeyValuePair<string, Func<ServiceItem, object>>(name, selector); }

        private static Candidate Pack(IList<ServiceItem> ordered, SectionConstraints c, Weights w, string strategy)
        {
            var placements = new List<Placement>();
            double x = c.SideClearanceMm, rowY = c.BottomClearanceMm, rowHeight = 0;
            foreach (ServiceItem item in ordered)
            {
                if (item.IsFixed)
                {
                    placements.Add(ToPlacement(item, item.FixedXmm, item.FixedYmm, placements.Count));
                    continue;
                }
                double gap = Math.Max(c.DefaultServiceGapMm, item.RequiredClearanceMm);
                if (x + item.WidthMm > c.WidthMm - c.SideClearanceMm)
                {
                    x = c.SideClearanceMm;
                    rowY += rowHeight + gap;
                    rowHeight = 0;
                }
                double y = Math.Max(rowY, item.MinimumElevationMm);
                if (item.PreferredElevationMm > 0)
                    y = Math.Max(y, Math.Min(item.PreferredElevationMm,
                        c.HeightMm - item.HeightMm - c.TopClearanceMm));
                y = Snap(y, c.GridMm);
                placements.Add(ToPlacement(item, x, y, placements.Count));
                x += item.WidthMm + gap;
                rowHeight = Math.Max(rowHeight, item.HeightMm);
            }
            return Score(placements, ordered, c, w, strategy);
        }

        private static Candidate Score(IList<Placement> placements, IList<ServiceItem> items,
            SectionConstraints c, Weights w, string strategy)
        {
            var byId = items.ToDictionary(x => x.Id, x => x, StringComparer.OrdinalIgnoreCase);
            var result = new Candidate { Strategy = strategy, Placements = placements,
                Explanation = "Boundary, clash, clearance, discipline, compactness, elevation and movement scoring." };
            double cost = 0, maxX = 0, maxY = 0;
            foreach (Placement p in placements)
            {
                ServiceItem item = byId[p.ServiceId];
                maxX = Math.Max(maxX, p.Xmm + p.WidthMm);
                maxY = Math.Max(maxY, p.Ymm + p.HeightMm);
                double boundary = Math.Max(0, c.SideClearanceMm - p.Xmm)
                    + Math.Max(0, p.Xmm + p.WidthMm - (c.WidthMm - c.SideClearanceMm))
                    + Math.Max(0, c.BottomClearanceMm - p.Ymm)
                    + Math.Max(0, p.Ymm + p.HeightMm - (c.HeightMm - c.TopClearanceMm));
                if (boundary > 0) result.BoundaryViolations++;
                cost += boundary * w.BoundaryViolation;
                if (item.PreferredElevationMm > 0)
                    cost += Math.Abs(p.Ymm - item.PreferredElevationMm) * w.PreferredElevation;
                if (item.IsFixed)
                    cost += (Math.Abs(p.Xmm - item.FixedXmm) + Math.Abs(p.Ymm - item.FixedYmm)) * w.Movement;
            }

            for (int i = 0; i < placements.Count; i++)
            for (int j = i + 1; j < placements.Count; j++)
            {
                Placement a = placements[i], b = placements[j];
                ServiceItem ia = byId[a.ServiceId], ib = byId[b.ServiceId];
                double dx = AxisGap(a.Xmm, a.WidthMm, b.Xmm, b.WidthMm);
                double dy = AxisGap(a.Ymm, a.HeightMm, b.Ymm, b.HeightMm);
                bool overlap = dx == 0 && dy == 0;
                if (overlap) { result.Clashes++; cost += w.Clash; }
                else
                {
                    double distance = Math.Sqrt(dx * dx + dy * dy);
                    double clearance = Math.Max(ia.RequiredClearanceMm, ib.RequiredClearanceMm);
                    if (distance < clearance)
                    { result.ClearanceViolations++; cost += (clearance - distance) * w.Clearance; }
                }
                double rule = RuleDistance(ia, ib, c);
                if (rule > 0 && Math.Sqrt(dx * dx + dy * dy) < rule)
                { result.RuleViolations++; cost += w.DisciplineRule; }
                if (c.ElectricalAboveWetServicesPenalty &&
                    ((IsElectrical(ia.Discipline) && IsWet(ib.Discipline) && a.Ymm > b.Ymm) ||
                     (IsElectrical(ib.Discipline) && IsWet(ia.Discipline) && b.Ymm > a.Ymm)))
                    cost += w.DisciplineRule * 0.5;
            }
            cost += ((maxX * maxY) / Math.Max(1, c.WidthMm * c.HeightMm)) * 1000 * w.Compactness;
            cost += Math.Max(0, maxX - c.WidthMm) * w.Access;
            result.Cost = cost; result.UsedWidthMm = maxX; result.UsedHeightMm = maxY;
            return result;
        }

        private static double RuleDistance(ServiceItem a, ServiceItem b, SectionConstraints c)
        {
            if (c.ForbiddenPairs == null) return 0;
            foreach (ForbiddenPair p in c.ForbiddenPairs)
                if ((Same(a.Discipline, p.FirstDiscipline) && Same(b.Discipline, p.SecondDiscipline)) ||
                    (Same(b.Discipline, p.FirstDiscipline) && Same(a.Discipline, p.SecondDiscipline)))
                    return Math.Max(0, p.MinimumDistanceMm);
            return 0;
        }

        public static double CalculateNdcg(IList<Candidate> ranked, IList<EngineerAssessment> assessments, int k)
        {
            if (ranked == null || assessments == null || k <= 0) return 0;
            var grades = assessments.Where(x => x != null).GroupBy(x => x.CandidateId)
                .ToDictionary(g => g.Key, g => g.Max(x => Math.Max(0, x.Grade)));
            double dcg = 0;
            for (int i = 0; i < Math.Min(k, ranked.Count); i++)
            {
                double grade;
                if (!grades.TryGetValue(ranked[i].Id, out grade)) grade = 0;
                dcg += (Math.Pow(2, grade) - 1) / Math.Log(i + 2, 2);
            }
            var ideal = grades.Values.OrderByDescending(x => x).Take(k).ToList();
            double idcg = 0;
            for (int i = 0; i < ideal.Count; i++) idcg += (Math.Pow(2, ideal[i]) - 1) / Math.Log(i + 2, 2);
            return idcg <= 0 ? 0 : dcg / idcg;
        }

        /// <summary>Conservative preference calibration, not neural-network training.</summary>
        public static Weights CalibrateWeights(Weights current, IList<EngineerAssessment> assessments,
            IList<Candidate> candidates, double learningRate)
        {
            Weights next = (current ?? new Weights()).Clone();
            if (assessments == null || candidates == null || assessments.Count < 2) return next;
            double rate = Math.Max(0, Math.Min(0.25, learningRate));
            var map = candidates.ToDictionary(x => x.Id, x => x);
            var valid = assessments.Where(x => x != null && map.ContainsKey(x.CandidateId)).ToList();
            if (valid.Count < 2) return next;
            double mg = valid.Average(x => x.Grade);
            double mc = valid.Average(x => map[x.CandidateId].Cost);
            double covariance = valid.Average(x => (x.Grade - mg) * (map[x.CandidateId].Cost - mc));
            if (covariance < 0) next.Compactness = Math.Max(0.01, next.Compactness * (1 - rate));
            else if (covariance > 0) next.Compactness = Math.Min(100, next.Compactness * (1 + rate));
            return next;
        }

        private static Placement ToPlacement(ServiceItem i, double x, double y, int order)
        { return new Placement { ServiceId = i.Id, Xmm = x, Ymm = y, WidthMm = i.WidthMm, HeightMm = i.HeightMm, Order = order }; }
        private static double AxisGap(double a, double aw, double b, double bw)
        { if (a + aw < b) return b - (a + aw); if (b + bw < a) return a - (b + bw); return 0; }
        private static double Snap(double v, double g)
        { return g <= 0 ? v : Math.Round(v / g, MidpointRounding.AwayFromZero) * g; }
        private static bool IsWet(string d)
        { return Has(d, "pipe") || Has(d, "plumb") || Has(d, "drain") || Has(d, "wet"); }
        private static bool IsElectrical(string d)
        { return Has(d, "elect") || Has(d, "cable") || Has(d, "power") || Has(d, "containment"); }
        private static int WetRank(string d) { return IsWet(d) ? 0 : (IsElectrical(d) ? 2 : 1); }
        private static int ElectricalRank(string d) { return IsElectrical(d) ? 0 : 1; }
        private static bool Has(string a, string b) { return (a ?? "").IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0; }
        private static bool Same(string a, string b) { return string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase); }

        private static void Validate(IList<ServiceItem> items, SectionConstraints c, Weights w)
        {
            if (items == null) throw new ArgumentNullException("items");
            if (c == null) throw new ArgumentNullException("constraints");
            if (w == null) throw new ArgumentNullException("weights");
            if (c.WidthMm <= 0 || c.HeightMm <= 0) throw new ArgumentException("Section dimensions must be positive.");
            if (items.Any(x => x == null || string.IsNullOrWhiteSpace(x.Id) || x.WidthMm <= 0 || x.HeightMm <= 0))
                throw new ArgumentException("Each service needs a unique ID and positive width and height.");
            if (items.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Count)
                throw new ArgumentException("Service IDs must be unique.");
        }
    }
}
