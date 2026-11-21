using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace BIMTools.TagAlign.Leaders
{
    public enum HeadAlign { Left, CenterX, Right, Top, MiddleY, Bottom, DistributeVertically, DistributeHorizontally }

    /// <summary>Extra tools: re-shape leaders in place, and align / distribute tag heads.</summary>
    public static class AnnotTools
    {
        /// <summary>Leader Tuner: keep tag heads and leader ends, rebuild only the elbows.</summary>
        public static LeaderAlignResult Retune(Document doc, View view, IEnumerable<Element> elements, LeaderAlignSettings s)
        {
            LeaderAlignResult res = new LeaderAlignResult();
            ViewFrame f = new ViewFrame(view);
            double scale = view.Scale > 0 ? view.Scale : 100;
            double landFt = s.LandingMm * scale / 304.8;
            double phiDeg = Math.Max(1, Math.Min(179, ((s.AngleDeg % 180.0) + 180.0) % 180.0));
            double phi = phiDeg * Math.PI / 180.0;
            double cot = Math.Cos(phi) / Math.Sin(phi);

            List<Annot> annots = AnnotFactory.Build(elements, false, s.IncludeSpatial).Where(a => a.HasLeader).ToList();
            foreach (Annot a in annots) a.Prepare(res);
            doc.Regenerate();

            foreach (Annot a in annots)
            {
                XYZ head, end, elbow;
                if (!a.Read(out head, out end, out elbow)) { res.Skipped++; continue; }
                P2 H = f.To2D(head), A = f.To2D(end), E;
                switch (s.Style)
                {
                    case LeaderStyle.FixedLanding: E = new P2(H.X + (A.X >= H.X ? 1.0 : -1.0) * landFt, H.Y); break;
                    case LeaderStyle.Straight: E = (A + H) * 0.5; break;
                    default: E = new P2(A.X + (H.Y - A.Y) * cot, H.Y); break;
                }
                try { a.Apply(head, end, f.ToModel(E, f.Depth(head)), res); res.Aligned++; }
                catch (Exception ex) { res.Skipped++; res.Notes.Add(a.El.Id + ": " + ex.Message); }
            }
            doc.Regenerate();
            return res;
        }

        class HeadItem
        {
            public Annot An; public XYZ End; public P2 H, Off; public bool HasOff; public double Depth;
        }

        /// <summary>Align / distribute tag heads (like Revit Align, but for tag heads with leaders).</summary>
        public static LeaderAlignResult AlignHeads(Document doc, View view, IEnumerable<Element> elements,
                                                    HeadAlign mode, bool keepLeaderShape, bool notes, bool spatial)
        {
            LeaderAlignResult res = new LeaderAlignResult();
            ViewFrame f = new ViewFrame(view);
            List<HeadItem> items = new List<HeadItem>();
            foreach (Annot a in AnnotFactory.Build(elements, notes, spatial))
            {
                XYZ head, end, elbow;
                if (!a.Read(out head, out end, out elbow)) { res.Skipped++; continue; }
                HeadItem it = new HeadItem { An = a, End = a.HasLeader ? end : null, H = f.To2D(head), Depth = f.Depth(head) };
                if (a.HasLeader && elbow != null) { it.HasOff = true; it.Off = f.To2D(elbow) - it.H; }
                items.Add(it);
            }
            if (items.Count < 2) { res.Notes.Add("Select at least two annotations."); return res; }

            Dictionary<HeadItem, P2> target = new Dictionary<HeadItem, P2>();
            switch (mode)
            {
                case HeadAlign.Left: { double v = items.Min(i => i.H.X); foreach (HeadItem i in items) target[i] = new P2(v, i.H.Y); break; }
                case HeadAlign.Right: { double v = items.Max(i => i.H.X); foreach (HeadItem i in items) target[i] = new P2(v, i.H.Y); break; }
                case HeadAlign.CenterX: { double v = items.Average(i => i.H.X); foreach (HeadItem i in items) target[i] = new P2(v, i.H.Y); break; }
                case HeadAlign.Top: { double v = items.Max(i => i.H.Y); foreach (HeadItem i in items) target[i] = new P2(i.H.X, v); break; }
                case HeadAlign.Bottom: { double v = items.Min(i => i.H.Y); foreach (HeadItem i in items) target[i] = new P2(i.H.X, v); break; }
                case HeadAlign.MiddleY: { double v = items.Average(i => i.H.Y); foreach (HeadItem i in items) target[i] = new P2(i.H.X, v); break; }
                case HeadAlign.DistributeVertically:
                    {
                        List<HeadItem> o = items.OrderBy(i => i.H.Y).ToList();
                        double y0 = o.First().H.Y, y1 = o.Last().H.Y;
                        for (int k = 0; k < o.Count; k++) target[o[k]] = new P2(o[k].H.X, y0 + (y1 - y0) * k / (o.Count - 1));
                        break;
                    }
                default:
                    {
                        List<HeadItem> o = items.OrderBy(i => i.H.X).ToList();
                        double x0 = o.First().H.X, x1 = o.Last().H.X;
                        for (int k = 0; k < o.Count; k++) target[o[k]] = new P2(x0 + (x1 - x0) * k / (o.Count - 1), o[k].H.Y);
                        break;
                    }
            }

            foreach (HeadItem i in items)
            {
                try
                {
                    P2 nh = target[i];
                    XYZ elbow = (keepLeaderShape && i.HasOff) ? f.ToModel(nh + i.Off, i.Depth) : null;
                    i.An.Apply(f.ToModel(nh, i.Depth), i.End, elbow, res);
                    res.Aligned++;
                }
                catch (Exception ex) { res.Skipped++; res.Notes.Add(i.An.El.Id + ": " + ex.Message); }
            }
            doc.Regenerate();
            return res;
        }
    }
}
