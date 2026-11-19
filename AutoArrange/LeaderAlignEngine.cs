using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;

namespace BIMTools.TagAlign.Leaders
{
    public enum OrderMode { Auto, AlongColumn, ByText, KeepCurrent }
    public enum LeaderStyle { ParallelAngle, FixedLanding, Straight }
    public enum AutoSide { Left, Right }

    /// <summary>User options (mirrors the "Choose Angle" dialog of the reference tool + extras).</summary>
    public class LeaderAlignSettings
    {
        public static LeaderAlignSettings Last;

        public double AngleDeg = 80.0;                       // protractor: 0 = right, 90 = up, CCW
        public LeaderStyle Style = LeaderStyle.ParallelAngle;
        public double LandingMm = 8.0;                       // landing line length (FixedLanding), mm ON SHEET
        public double VerticalSepMm = 4.0;                   // gap between tag rows, mm ON SHEET
        public double HorizontalSepMm = 30.0;                // stagger offset, mm ON SHEET
        public bool Intermittent;                            // zig-zag two columns
        public OrderMode Order = OrderMode.Auto;
        public bool DistributeBetweenPicks;
        public bool AutoColumn;
        public AutoSide Side = AutoSide.Left;
        public bool IncludeNotes = true;
        public bool IncludeSpatial = true;                   // room / space / area tags
        public bool AllInView;                               // ignore selection, take every tag in the view

        public XYZ LowestHead, HighestHead;                  // not persisted

        public LeaderAlignSettings Clone() { return (LeaderAlignSettings)MemberwiseClone(); }

        static string IniPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BIMTools", "AutoArrange.ini"); }
        }

        public void Save()
        {
            try
            {
                CultureInfo ci = CultureInfo.InvariantCulture;
                Directory.CreateDirectory(Path.GetDirectoryName(IniPath));
                File.WriteAllLines(IniPath, new string[] {
                    "AngleDeg=" + AngleDeg.ToString(ci), "Style=" + Style, "LandingMm=" + LandingMm.ToString(ci),
                    "VerticalSepMm=" + VerticalSepMm.ToString(ci), "HorizontalSepMm=" + HorizontalSepMm.ToString(ci),
                    "Intermittent=" + Intermittent, "Order=" + Order, "Distribute=" + DistributeBetweenPicks,
                    "AutoColumn=" + AutoColumn, "Side=" + Side, "IncludeNotes=" + IncludeNotes,
                    "IncludeSpatial=" + IncludeSpatial, "AllInView=" + AllInView });
            }
            catch { }
            Last = Clone();
        }

        public static LeaderAlignSettings Load()
        {
            if (Last != null) return Last.Clone();
            LeaderAlignSettings s = new LeaderAlignSettings();
            try
            {
                if (File.Exists(IniPath))
                    foreach (string line in File.ReadAllLines(IniPath))
                    {
                        int i = line.IndexOf('=');
                        if (i < 1) continue;
                        try { s.SetKey(line.Substring(0, i), line.Substring(i + 1)); } catch { }
                    }
            }
            catch { }
            Last = s.Clone();
            return s;
        }

        void SetKey(string k, string v)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            switch (k)
            {
                case "AngleDeg": AngleDeg = double.Parse(v, ci); break;
                case "Style": Style = (LeaderStyle)Enum.Parse(typeof(LeaderStyle), v); break;
                case "LandingMm": LandingMm = double.Parse(v, ci); break;
                case "VerticalSepMm": VerticalSepMm = double.Parse(v, ci); break;
                case "HorizontalSepMm": HorizontalSepMm = double.Parse(v, ci); break;
                case "Intermittent": Intermittent = bool.Parse(v); break;
                case "Order": Order = (OrderMode)Enum.Parse(typeof(OrderMode), v); break;
                case "Distribute": DistributeBetweenPicks = bool.Parse(v); break;
                case "AutoColumn": AutoColumn = bool.Parse(v); break;
                case "Side": Side = (AutoSide)Enum.Parse(typeof(AutoSide), v); break;
                case "IncludeNotes": IncludeNotes = bool.Parse(v); break;
                case "IncludeSpatial": IncludeSpatial = bool.Parse(v); break;
                case "AllInView": AllInView = bool.Parse(v); break;
            }
        }
    }

    public class LeaderAlignResult
    {
        public int Aligned, Skipped, ElbowFailures, Crossings;
        public List<string> Notes = new List<string>();
    }

    public struct P2
    {
        public double X, Y;
        public P2(double x, double y) { X = x; Y = y; }
        public static P2 operator +(P2 a, P2 b) { return new P2(a.X + b.X, a.Y + b.Y); }
        public static P2 operator -(P2 a, P2 b) { return new P2(a.X - b.X, a.Y - b.Y); }
        public static P2 operator *(P2 a, double k) { return new P2(a.X * k, a.Y * k); }
        public double Dot(P2 o) { return X * o.X + Y * o.Y; }
        public double Length { get { return Math.Sqrt(X * X + Y * Y); } }
        public P2 Unit() { double l = Length; return l < 1e-12 ? new P2(0, 1) : new P2(X / l, Y / l); }
    }

    internal class ViewFrame
    {
        readonly XYZ _o, _r, _u, _d;
        public ViewFrame(View v) { _o = v.Origin; _r = v.RightDirection; _u = v.UpDirection; _d = v.ViewDirection; }
        public P2 To2D(XYZ p) { XYZ q = p - _o; return new P2(q.DotProduct(_r), q.DotProduct(_u)); }
        public double Depth(XYZ p) { return (p - _o).DotProduct(_d); }
        public XYZ ToModel(P2 p, double depth) { return _o + _r * p.X + _u * p.Y + _d * depth; }
    }

    internal class Item
    {
        public Annot An;
        public P2 A, Head;               // leader end, current head
        public double HeadDepth, EndDepth;
        public P2 Slot, Elbow;
        public double XInt;
    }

    /// <summary>
    /// Stack tags in one column (or a zig-zag of two); landing lines horizontal; leaders parallel,
    /// fixed-landing or straight; row order chosen to avoid crossings (or by text / position).
    /// </summary>
    public static class LeaderAlignEngine
    {
        public static LeaderAlignResult Run(Document doc, View view, IEnumerable<Element> elements, LeaderAlignSettings s)
        {
            LeaderAlignResult res = new LeaderAlignResult();
            ViewFrame f = new ViewFrame(view);
            double scale = view.Scale > 0 ? view.Scale : 100;
            double stepFt = s.VerticalSepMm * scale / 304.8;
            double hsepFt = s.HorizontalSepMm * scale / 304.8;
            double landFt = s.LandingMm * scale / 304.8;

            List<Annot> annots = AnnotFactory.Build(elements, s.IncludeNotes, s.IncludeSpatial);
            foreach (Annot a in annots) a.Prepare(res);
            doc.Regenerate();

            List<Item> items = new List<Item>();
            foreach (Annot a in annots)
            {
                XYZ head, end, elbow;
                if (!a.Read(out head, out end, out elbow)) { res.Skipped++; res.Notes.Add("Skipped " + a.El.Id + " (no leader data)"); continue; }
                items.Add(new Item { An = a, A = f.To2D(end), Head = f.To2D(head), EndDepth = f.Depth(end), HeadDepth = f.Depth(head) });
            }
            if (items.Count == 0) { res.Notes.Add("Nothing to align."); return res; }

            // ---- column axis ----
            P2 low, high;
            if (s.AutoColumn || s.LowestHead == null || s.HighestHead == null)
            {
                double margin = 25.0 * scale / 304.8;
                double minY = items.Min(i => i.A.Y);
                double x = s.Side == AutoSide.Left ? items.Min(i => i.A.X) - margin : items.Max(i => i.A.X) + margin;
                low = new P2(x, minY);
                high = new P2(x, minY + stepFt * (items.Count - 1));
            }
            else { low = f.To2D(s.LowestHead); high = f.To2D(s.HighestHead); }

            P2 span = high - low;
            P2 u = span.Unit();
            P2 nrm = new P2(-u.Y, u.X);
            P2 centroid = new P2(items.Average(i => i.A.X), items.Average(i => i.A.Y));
            if (nrm.Dot(centroid - low) < 0) nrm = nrm * -1.0;
            double sgnX = nrm.X >= 0 ? 1.0 : -1.0;

            double stepEff = stepFt;
            if (s.DistributeBetweenPicks && items.Count > 1 && span.Length > 1e-6) stepEff = span.Length / (items.Count - 1);

            List<P2> slots = new List<P2>();
            for (int k = 0; k < items.Count; k++)
            {
                P2 p = low + u * (k * stepEff);
                if (s.Intermittent && (k % 2 == 1)) p = p + nrm * hsepFt;
                slots.Add(p);
            }

            double phiDeg = ((s.AngleDeg % 180.0) + 180.0) % 180.0;
            phiDeg = Math.Max(1, Math.Min(179, phiDeg));
            double phi = phiDeg * Math.PI / 180.0;
            double cot = Math.Cos(phi) / Math.Sin(phi);
            foreach (Item it in items) it.XInt = it.A.X - it.A.Y * cot;

            // ---- candidate row orders ----
            List<List<Item>> cands = new List<List<Item>>();
            switch (s.Order)
            {
                case OrderMode.AlongColumn: cands.Add(items.OrderBy(i => i.A.Dot(u)).ToList()); break;
                case OrderMode.ByText: cands.Add(items.OrderBy(i => i.An.Key, NaturalComparer.Instance).ToList()); break;
                case OrderMode.KeepCurrent: cands.Add(items.OrderBy(i => i.Head.Dot(u)).ToList()); break;
                default:
                    cands.Add(items.OrderBy(i => i.XInt).ToList());
                    cands.Add(items.OrderByDescending(i => i.XInt).ToList());
                    cands.Add(items.OrderBy(i => i.A.Dot(u)).ToList());
                    cands.Add(items.OrderByDescending(i => i.A.Dot(u)).ToList());
                    break;
            }

            List<Item> best = null; int bestCross = int.MaxValue; double bestLen = double.MaxValue;
            foreach (List<Item> order in cands)
            {
                Assign(order, slots, s.Style, cot, landFt, sgnX);
                int c = CountCrossings(order);
                double len = order.Where(i => i.An.HasLeader).Sum(i => (i.Elbow - i.A).Length + (i.Slot - i.Elbow).Length);
                if (c < bestCross || (c == bestCross && len < bestLen)) { best = order; bestCross = c; bestLen = len; }
            }
            Assign(best, slots, s.Style, cot, landFt, sgnX);
            res.Crossings = bestCross;

            foreach (Item it in best)
            {
                try
                {
                    XYZ head = f.ToModel(it.Slot, it.HeadDepth);
                    XYZ end = it.An.HasLeader ? f.ToModel(it.A, it.EndDepth) : null;
                    XYZ elbow = it.An.HasLeader ? f.ToModel(it.Elbow, it.HeadDepth) : null;
                    it.An.Apply(head, end, elbow, res);
                    res.Aligned++;
                }
                catch (Exception ex) { res.Skipped++; res.Notes.Add("Could not move " + it.An.El.Id + ": " + ex.Message); }
            }
            doc.Regenerate();
            return res;
        }

        static void Assign(List<Item> order, List<P2> slots, LeaderStyle style, double cot, double landFt, double sgnX)
        {
            for (int k = 0; k < order.Count; k++)
            {
                Item it = order[k];
                it.Slot = slots[k];
                switch (style)
                {
                    case LeaderStyle.FixedLanding: it.Elbow = new P2(it.Slot.X + sgnX * landFt, it.Slot.Y); break;
                    case LeaderStyle.Straight: it.Elbow = (it.A + it.Slot) * 0.5; break;
                    default: it.Elbow = new P2(it.A.X + (it.Slot.Y - it.A.Y) * cot, it.Slot.Y); break;
                }
            }
        }

        static int CountCrossings(List<Item> order)
        {
            List<Item> t = order.Where(i => i.An.HasLeader).ToList();
            int c = 0;
            for (int i = 0; i < t.Count; i++)
                for (int j = i + 1; j < t.Count; j++)
                {
                    Item a = t[i], b = t[j];
                    if (Cross2(a.A, a.Elbow, b.A, b.Elbow) || Cross2(a.A, a.Elbow, b.Elbow, b.Slot) ||
                        Cross2(a.Elbow, a.Slot, b.A, b.Elbow) || Cross2(a.Elbow, a.Slot, b.Elbow, b.Slot)) c++;
                }
            return c;
        }

        static double Cr(P2 a, P2 b) { return a.X * b.Y - a.Y * b.X; }

        static bool Cross2(P2 p1, P2 p2, P2 p3, P2 p4)
        {
            const double e = 1e-9;
            double d1 = Cr(p4 - p3, p1 - p3), d2 = Cr(p4 - p3, p2 - p3);
            double d3 = Cr(p2 - p1, p3 - p1), d4 = Cr(p2 - p1, p4 - p1);
            return ((d1 > e && d2 < -e) || (d1 < -e && d2 > e)) && ((d3 > e && d4 < -e) || (d3 < -e && d4 > e));
        }
    }
}
