using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;

namespace BIMTools.TagAlign.Leaders
{
    public enum OrderMode { Auto, AlongColumn, ByText, KeepCurrent }
    public enum LeaderStyle { ParallelAngle, FixedLanding, Straight }
    public enum AutoSide { Left, Right }
    /// <summary>How tags are split into separate blocks inside the column.</summary>
    public enum GroupMode { None, Category, TagType, TextPrefix }
    /// <summary>Direction the overlap resolver may push tags.</summary>
    public enum ClearAxis { Any, Vertical, Horizontal }

    /// <summary>User options of all dialogs. Every public instance field (except XYZ) is saved automatically.</summary>
    public class LeaderAlignSettings
    {
        public static LeaderAlignSettings Last;

        // ---- leaders ----
        public double AngleDeg = 80.0;                       // slope of the diagonal: 0 = flat, 90 = vertical
        public LeaderStyle Style = LeaderStyle.ParallelAngle;
        public double LandingMm = 8.0;                       // landing line length (FixedLanding), mm ON SHEET

        // ---- rows ----
        public double VerticalSepMm = 4.0;                   // gap between tag rows, mm ON SHEET
        public double HorizontalSepMm = 30.0;                // stagger offset, mm ON SHEET
        public bool Intermittent;                            // zig-zag two columns
        public OrderMode Order = OrderMode.Auto;
        public bool DistributeBetweenPicks;

        // ---- placement and scope ----
        public bool AutoColumn;
        public AutoSide Side = AutoSide.Left;
        public bool UseReferenceTag;                         // continue the column of an existing tag
        public bool IncludeNotes = true;
        public bool IncludeSpatial = true;                   // room / space / area tags
        public bool AllInView;                               // ignore selection, take every tag in the view

        // ---- smart options (v2) ----
        public bool AutoRowHeight;                           // row spacing (and stagger offset) measured from the real tag size
        public double RowPaddingMm = 1.0;                    // extra gap added to the measured size, mm ON SHEET
        public GroupMode Group = GroupMode.None;
        public double GroupGapRows = 1.0;                    // empty rows between two groups
        public bool DetailedReport = true;                   // before / after numbers in the result message

        // ---- resolve overlaps (v2) ----
        public double ClearPaddingMm = 1.5;                  // minimum free gap between two tags, mm ON SHEET
        public double ClearMaxMoveMm = 25.0;                 // a tag never moves farther than this, mm ON SHEET
        public ClearAxis ClearDirection = ClearAxis.Any;
        public bool ClearKeepShape = true;
        public int ClearPasses = 80;

        // ---- clicks (not persisted) ----
        public XYZ LowestHead, HighestHead;                  // start (and optional end) of the column
        public XYZ ReferenceHead;                            // head of the picked reference tag

        public LeaderAlignSettings Clone() { return (LeaderAlignSettings)MemberwiseClone(); }

        // ---------- persistence (reflection: new options never need extra save code) ----------
        static IEnumerable<FieldInfo> Persisted()
        {
            return typeof(LeaderAlignSettings).GetFields(BindingFlags.Public | BindingFlags.Instance)
                                              .Where(f => f.FieldType != typeof(XYZ));
        }

        static string Fmt(object v)
        {
            if (v is double) return ((double)v).ToString("R", CultureInfo.InvariantCulture);
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public List<string> ToLines()
        {
            List<string> l = new List<string>();
            foreach (FieldInfo f in Persisted())
            {
                object v = f.GetValue(this);
                l.Add(f.Name + "=" + (f.FieldType.IsEnum ? v.ToString() : Fmt(v)));
            }
            return l;
        }

        public void FromLines(IEnumerable<string> lines)
        {
            Dictionary<string, FieldInfo> map = Persisted().ToDictionary(f => f.Name);
            foreach (string line in lines)
            {
                int i = line.IndexOf('=');
                if (i < 1) continue;
                string k = line.Substring(0, i).Trim(), v = line.Substring(i + 1).Trim();
                if (k == "Distribute") k = "DistributeBetweenPicks";          // key used by version 1
                FieldInfo f;
                if (!map.TryGetValue(k, out f)) continue;
                try
                {
                    CultureInfo ci = CultureInfo.InvariantCulture;
                    if (f.FieldType == typeof(double)) f.SetValue(this, double.Parse(v, ci));
                    else if (f.FieldType == typeof(int)) f.SetValue(this, int.Parse(v, ci));
                    else if (f.FieldType == typeof(bool)) f.SetValue(this, bool.Parse(v));
                    else if (f.FieldType.IsEnum) f.SetValue(this, Enum.Parse(f.FieldType, v));
                }
                catch { }
            }
        }

        static string IniPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BIMTools", "AutoArrange.ini"); }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(IniPath));
                File.WriteAllLines(IniPath, ToLines().ToArray());
            }
            catch { }
            Last = Clone();
        }

        public static LeaderAlignSettings Load()
        {
            if (Last != null) return Last.Clone();
            LeaderAlignSettings s = new LeaderAlignSettings();
            try { if (File.Exists(IniPath)) s.FromLines(File.ReadAllLines(IniPath)); } catch { }
            Last = s.Clone();
            return s;
        }
    }

    public class LeaderAlignResult
    {
        public int Aligned, Skipped, ElbowFailures, Crossings;
        // before / after figures (filled when the engine could measure them)
        public int CrossingsBefore = -1, OverlapsBefore = -1, OverlapsAfter = -1, Groups;
        public double LenBeforeMm = -1, LenAfterMm = -1;
        public bool Measured;
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

    /// <summary>Axis-aligned rectangle in view coordinates (feet).</summary>
    internal struct Box
    {
        public double X0, Y0, X1, Y1;
        public Box(double x0, double y0, double x1, double y1) { X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; }
        public double W { get { return X1 - X0; } }
        public double H { get { return Y1 - Y0; } }
        public Box Shift(P2 p) { return new Box(X0 + p.X, Y0 + p.Y, X1 + p.X, Y1 + p.Y); }
        public Box Inflate(double d) { return new Box(X0 - d, Y0 - d, X1 + d, Y1 + d); }
        public static bool Overlap(Box a, Box b)
        {
            const double e = 1e-6;
            return a.X0 < b.X1 - e && b.X0 < a.X1 - e && a.Y0 < b.Y1 - e && b.Y0 < a.Y1 - e;
        }
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
        public P2 Elbow0; public bool HasElbow0;    // elbow before the run
        public Box Rel;                  // tag box relative to its head
        public double XInt;
    }

    /// <summary>
    /// Measures the real size of every tag (head only, without its leader) so that spacing can follow the
    /// actual text size and overlaps can be detected. Works inside a SubTransaction that is always rolled back.
    /// </summary>
    internal static class TagMetrics
    {
        public static Dictionary<Annot, Box> Measure(Document doc, View view, ViewFrame f, List<Annot> annots)
        {
            Dictionary<Annot, Box> res = new Dictionary<Annot, Box>();
            Dictionary<Annot, P2> heads = new Dictionary<Annot, P2>();
            foreach (Annot a in annots)
            {
                XYZ h, e, el;
                if (a.Read(out h, out e, out el)) heads[a] = f.To2D(h);
            }
            SubTransaction st = new SubTransaction(doc);
            try
            {
                st.Start();
                foreach (Annot a in annots) { try { a.SetLeaderVisible(false); } catch { } }
                doc.Regenerate();
                foreach (Annot a in annots)
                {
                    P2 h;
                    Box b;
                    if (!heads.TryGetValue(a, out h)) continue;
                    if (BoxOf(a.El, view, f, out b))
                        res[a] = new Box(b.X0 - h.X, b.Y0 - h.Y, b.X1 - h.X, b.Y1 - h.Y);
                }
            }
            catch { }
            finally
            {
                try { if (st.GetStatus() == TransactionStatus.Started) st.RollBack(); } catch { }
            }
            return res;
        }

        static bool BoxOf(Element el, View view, ViewFrame f, out Box box)
        {
            box = new Box();
            BoundingBoxXYZ bb = null;
            try { bb = el.get_BoundingBox(view); } catch { }
            if (bb == null) return false;
            Transform t = bb.Transform ?? Transform.Identity;
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            for (int i = 0; i < 8; i++)
            {
                XYZ c = t.OfPoint(new XYZ((i & 1) == 0 ? bb.Min.X : bb.Max.X, (i & 2) == 0 ? bb.Min.Y : bb.Max.Y, (i & 4) == 0 ? bb.Min.Z : bb.Max.Z));
                P2 p = f.To2D(c);
                x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y);
            }
            if (x1 - x0 < 1e-6 || y1 - y0 < 1e-6) return false;
            box = new Box(x0, y0, x1, y1);
            return true;
        }

        /// <summary>Gives every annotation without a measured box the median size of the measured ones (or a default).</summary>
        public static void Fill(Dictionary<Annot, Box> boxes, IEnumerable<Annot> all, double scale)
        {
            double w, h;
            if (boxes.Count > 0)
            {
                List<double> ws = boxes.Values.Select(b => b.W).OrderBy(x => x).ToList();
                List<double> hs = boxes.Values.Select(b => b.H).OrderBy(x => x).ToList();
                w = ws[ws.Count / 2]; h = hs[hs.Count / 2];
            }
            else { w = 12.0 * scale / 304.8; h = 4.0 * scale / 304.8; }
            foreach (Annot a in all)
                if (!boxes.ContainsKey(a)) boxes[a] = new Box(-w / 2, -h / 2, w / 2, h / 2);
        }
    }

    /// <summary>
    /// Stack tags in one column (or a zig-zag of two); landing lines horizontal; leaders parallel,
    /// fixed-landing or straight; row order chosen to avoid crossings (or by text / position).
    /// New in v2: row spacing from the real tag size, separate blocks per group, continue an existing column,
    /// before / after measurements.
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
                Item it = new Item { An = a, A = f.To2D(end), Head = f.To2D(head), EndDepth = f.Depth(end), HeadDepth = f.Depth(head) };
                if (a.HasLeader && elbow != null) { it.Elbow0 = f.To2D(elbow); it.HasElbow0 = true; }
                items.Add(it);
            }
            if (items.Count == 0) { res.Notes.Add("Nothing to align."); return res; }

            // ---- real tag sizes ----
            HashSet<Annot> measured = new HashSet<Annot>();
            Dictionary<Annot, Box> boxes = new Dictionary<Annot, Box>();
            if (s.AutoRowHeight || s.DetailedReport)
            {
                boxes = TagMetrics.Measure(doc, view, f, items.Select(i => i.An).ToList());
                foreach (Annot a in boxes.Keys) measured.Add(a);
            }
            res.Measured = measured.Count > 0;
            TagMetrics.Fill(boxes, items.Select(i => i.An), scale);
            foreach (Item it in items) it.Rel = boxes[it.An];

            if (s.AutoRowHeight && res.Measured)
            {
                double pad = s.RowPaddingMm * scale / 304.8;
                List<Item> real = items.Where(i => measured.Contains(i.An)).ToList();
                stepFt = real.Max(i => i.Rel.H) + pad;
                if (s.Intermittent) hsepFt = real.Max(i => i.Rel.W) + pad;
            }
            else if (s.AutoRowHeight) res.Notes.Add("Tag size could not be measured - the row spacing value was used.");

            // ---- column axis ----
            P2 low, high;
            if (s.UseReferenceTag && s.ReferenceHead != null)
            {
                P2 r = f.To2D(s.ReferenceHead);                    // continue the existing column upward
                low = new P2(r.X, r.Y + stepFt);
                high = new P2(low.X, low.Y + stepFt * (items.Count - 1));
            }
            else if (!s.AutoColumn && s.LowestHead != null && s.HighestHead == null)
            {
                low = f.To2D(s.LowestHead);                        // one click: column grows upward from it
                high = new P2(low.X, low.Y + stepFt * (items.Count - 1));
            }
            else if (s.AutoColumn || s.LowestHead == null)
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
            if (items.Count == 1 && span.Length < 1e-9) u = new P2(0, 1);
            P2 nrm = new P2(-u.Y, u.X);
            P2 centroid = new P2(items.Average(i => i.A.X), items.Average(i => i.A.Y));
            if (nrm.Dot(centroid - low) < 0) nrm = nrm * -1.0;
            double sgnX = nrm.X >= 0 ? 1.0 : -1.0;

            // ---- groups and slots ----
            List<List<Item>> groups = Partition(doc, items, s.Group, s.Order, u);
            res.Groups = groups.Count;
            int n = items.Count;
            double gap = groups.Count > 1 ? Math.Max(0, s.GroupGapRows) : 0;
            double units = (n - 1) + gap * (groups.Count - 1);
            double stepEff = stepFt;
            bool spread = s.DistributeBetweenPicks && !s.UseReferenceTag && !s.AutoColumn && s.HighestHead != null;
            if (spread && n > 1 && span.Length > 1e-6 && units > 1e-9) stepEff = span.Length / units;

            List<List<P2>> groupSlots = new List<List<P2>>();
            double pos = 0; int idx = 0;
            foreach (List<Item> g in groups)
            {
                List<P2> sl = new List<P2>();
                foreach (Item it in g)
                {
                    P2 p = low + u * (pos * stepEff);
                    if (s.Intermittent && (idx % 2 == 1)) p = p + nrm * hsepFt;
                    sl.Add(p);
                    pos += 1; idx++;
                }
                groupSlots.Add(sl);
                pos += gap;
            }

            double phiDeg = ((s.AngleDeg % 180.0) + 180.0) % 180.0;
            phiDeg = Math.Max(1, Math.Min(179, phiDeg));
            double phi = phiDeg * Math.PI / 180.0;
            double cot = Math.Cos(phi) / Math.Sin(phi);
            foreach (Item it in items) it.XInt = it.A.X - it.A.Y * cot;

            // ---- figures before the run ----
            foreach (Item it in items) { it.Slot = it.Head; it.Elbow = it.HasElbow0 ? it.Elbow0 : (it.A + it.Head) * 0.5; }
            res.CrossingsBefore = CountCrossings(items);
            if (res.Measured) res.OverlapsBefore = CountOverlaps(items);
            res.LenBeforeMm = TotalLength(items) * 304.8 / scale;

            // ---- best row order, group by group ----
            List<Item> ordered = new List<Item>();
            for (int gi = 0; gi < groups.Count; gi++)
            {
                List<Item> grp = groups[gi];
                List<P2> slots = groupSlots[gi];
                List<List<Item>> cands = new List<List<Item>>();
                switch (s.Order)
                {
                    case OrderMode.AlongColumn: cands.Add(grp.OrderBy(i => i.A.Dot(u)).ToList()); break;
                    case OrderMode.ByText: cands.Add(grp.OrderBy(i => i.An.Key, NaturalComparer.Instance).ToList()); break;
                    case OrderMode.KeepCurrent: cands.Add(grp.OrderBy(i => i.Head.Dot(u)).ToList()); break;
                    default:
                        cands.Add(grp.OrderBy(i => i.XInt).ToList());
                        cands.Add(grp.OrderByDescending(i => i.XInt).ToList());
                        cands.Add(grp.OrderBy(i => i.A.Dot(u)).ToList());
                        cands.Add(grp.OrderByDescending(i => i.A.Dot(u)).ToList());
                        break;
                }
                List<Item> best = null; int bestCross = int.MaxValue; double bestLen = double.MaxValue;
                foreach (List<Item> order in cands)
                {
                    Assign(order, slots, s.Style, cot, landFt, sgnX);
                    int c = CountCrossings(order);
                    double len = TotalLength(order);
                    if (c < bestCross || (c == bestCross && len < bestLen)) { best = order; bestCross = c; bestLen = len; }
                }
                Assign(best, slots, s.Style, cot, landFt, sgnX);
                ordered.AddRange(best);
            }
            res.Crossings = CountCrossings(ordered);
            if (res.Measured) res.OverlapsAfter = CountOverlaps(ordered);
            res.LenAfterMm = TotalLength(ordered) * 304.8 / scale;

            foreach (Item it in ordered)
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

        // ---------- grouping ----------
        static string KeyOf(Document doc, Annot a, GroupMode m)
        {
            switch (m)
            {
                case GroupMode.Category: return a.CategoryName;
                case GroupMode.TagType:
                    try { Element ty = doc.GetElement(a.El.GetTypeId()); return ty != null ? ty.Name : ""; } catch { return ""; }
                case GroupMode.TextPrefix:
                    {
                        string k = (a.Key ?? "").Trim();
                        int i = 0;
                        while (i < k.Length && !char.IsDigit(k[i]) && !char.IsWhiteSpace(k[i])) i++;
                        return i == 0 ? "#" : k.Substring(0, i).ToUpperInvariant();
                    }
            }
            return "";
        }

        static List<List<Item>> Partition(Document doc, List<Item> items, GroupMode mode, OrderMode order, P2 u)
        {
            if (mode == GroupMode.None) return new List<List<Item>> { items };
            Dictionary<string, List<Item>> d = new Dictionary<string, List<Item>>();
            foreach (Item it in items)
            {
                string k = KeyOf(doc, it.An, mode);
                List<Item> l;
                if (!d.TryGetValue(k, out l)) { l = new List<Item>(); d[k] = l; }
                l.Add(it);
            }
            List<KeyValuePair<string, List<Item>>> list = d.ToList();
            if (order == OrderMode.ByText) list = list.OrderBy(kv => kv.Key, NaturalComparer.Instance).ToList();
            else list = list.OrderBy(kv => kv.Value.Average(i => i.A.Dot(u))).ToList();
            return list.Select(kv => kv.Value).ToList();
        }

        // ---------- leader geometry ----------
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

        static double TotalLength(List<Item> order)
        {
            return order.Where(i => i.An.HasLeader).Sum(i => (i.Elbow - i.A).Length + (i.Slot - i.Elbow).Length);
        }

        static int CountOverlaps(List<Item> list)
        {
            int c = 0;
            Box[] b = list.Select(i => i.Rel.Shift(i.Slot)).ToArray();
            for (int i = 0; i < b.Length; i++)
                for (int j = i + 1; j < b.Length; j++)
                    if (Box.Overlap(b[i], b[j])) c++;
            return c;
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
