using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace BIMTools.TagAlign.Leaders
{
    public enum FlipMode { Horizontal, Vertical, Both, WholeColumn }

    /// <summary>
    /// "Resolve Overlaps": finds tags whose real text boxes touch and pushes them apart by the smallest
    /// possible move. Leader ends never move, nothing is re-ordered, tags that do not collide stay where they are.
    /// </summary>
    public static class OverlapEngine
    {
        class Node
        {
            public Annot An; public P2 H0, H, Off; public bool HasOff; public XYZ End; public double Depth; public Box Rel;
        }

        public static LeaderAlignResult Run(Document doc, View view, IEnumerable<Element> elements, LeaderAlignSettings s)
        {
            LeaderAlignResult res = new LeaderAlignResult();
            ViewFrame f = new ViewFrame(view);
            double scale = view.Scale > 0 ? view.Scale : 100;
            double pad = s.ClearPaddingMm * scale / 304.8;
            double maxMove = s.ClearMaxMoveMm * scale / 304.8;

            List<Annot> annots = AnnotFactory.Build(elements, s.IncludeNotes, s.IncludeSpatial);
            List<Node> nodes = new List<Node>();
            foreach (Annot a in annots)
            {
                XYZ head, end, elbow;
                if (!a.Read(out head, out end, out elbow)) { res.Skipped++; continue; }
                Node n = new Node { An = a, H0 = f.To2D(head), Depth = f.Depth(head), End = a.HasLeader ? end : null };
                n.H = n.H0;
                if (a.HasLeader && elbow != null) { n.HasOff = true; n.Off = f.To2D(elbow) - n.H0; }
                nodes.Add(n);
            }
            if (nodes.Count < 2) { res.Notes.Add("Select at least two tags."); return res; }

            Dictionary<Annot, Box> boxes = TagMetrics.Measure(doc, view, f, nodes.Select(n => n.An).ToList());
            res.Measured = boxes.Count > 0;
            if (!res.Measured) { res.Notes.Add("The tag sizes could not be measured in this view, so nothing was moved."); return res; }
            TagMetrics.Fill(boxes, nodes.Select(n => n.An), scale);
            foreach (Node n in nodes) n.Rel = boxes[n.An];

            res.OverlapsBefore = Count(nodes, pad);

            // ---- relaxation: push every overlapping pair apart along the cheapest axis ----
            for (int pass = 0; pass < Math.Max(1, s.ClearPasses); pass++)
            {
                bool moved = false;
                for (int i = 0; i < nodes.Count; i++)
                    for (int j = i + 1; j < nodes.Count; j++)
                    {
                        Box a = nodes[i].Rel.Shift(nodes[i].H).Inflate(pad / 2);
                        Box b = nodes[j].Rel.Shift(nodes[j].H).Inflate(pad / 2);
                        if (!Box.Overlap(a, b)) continue;
                        double ox = Math.Min(a.X1, b.X1) - Math.Max(a.X0, b.X0);
                        double oy = Math.Min(a.Y1, b.Y1) - Math.Max(a.Y0, b.Y0);
                        bool vertical;
                        switch (s.ClearDirection)
                        {
                            case ClearAxis.Vertical: vertical = true; break;
                            case ClearAxis.Horizontal: vertical = false; break;
                            default: vertical = oy <= ox; break;
                        }
                        double d = (vertical ? oy : ox) / 2 + 1e-4;
                        double ci = vertical ? (a.Y0 + a.Y1) / 2 : (a.X0 + a.X1) / 2;
                        double cj = vertical ? (b.Y0 + b.Y1) / 2 : (b.X0 + b.X1) / 2;
                        double dir = Math.Abs(ci - cj) < 1e-9 ? 1.0 : (ci > cj ? 1.0 : -1.0);
                        P2 shift = vertical ? new P2(0, d * dir) : new P2(d * dir, 0);
                        nodes[i].H = Clamp(nodes[i], nodes[i].H + shift, maxMove);
                        nodes[j].H = Clamp(nodes[j], nodes[j].H - shift, maxMove);
                        moved = true;
                    }
                if (!moved) break;
            }
            res.OverlapsAfter = Count(nodes, pad);

            foreach (Node n in nodes)
            {
                if ((n.H - n.H0).Length < 1e-6) continue;
                try
                {
                    XYZ head = f.ToModel(n.H, n.Depth);
                    XYZ elbow = (s.ClearKeepShape && n.HasOff) ? f.ToModel(n.H + n.Off, n.Depth) : null;
                    n.An.Apply(head, n.An.HasLeader ? n.End : null, elbow, res);
                    res.Aligned++;
                }
                catch (Exception ex) { res.Skipped++; res.Notes.Add(n.An.El.Id + ": " + ex.Message); }
            }
            doc.Regenerate();
            return res;
        }

        static P2 Clamp(Node n, P2 p, double maxMove)
        {
            P2 d = p - n.H0;
            return d.Length > maxMove ? n.H0 + d.Unit() * maxMove : p;
        }

        static int Count(List<Node> nodes, double pad)
        {
            int c = 0;
            Box[] b = nodes.Select(n => n.Rel.Shift(n.H).Inflate(pad / 2)).ToArray();
            for (int i = 0; i < b.Length; i++)
                for (int j = i + 1; j < b.Length; j++)
                    if (Box.Overlap(b[i], b[j])) c++;
            return c;
        }
    }

    /// <summary>"Flip Tag Side": move tag heads to the other side of their elements (leader ends stay).</summary>
    public static class FlipTool
    {
        public static LeaderAlignResult Run(Document doc, View view, IEnumerable<Element> elements, FlipMode mode, bool spatial)
        {
            LeaderAlignResult res = new LeaderAlignResult();
            ViewFrame f = new ViewFrame(view);
            List<Annot> annots = AnnotFactory.Build(elements, false, spatial).Where(a => a.HasLeader).ToList();

            List<Annot> ok = new List<Annot>();
            List<P2> H = new List<P2>(), A = new List<P2>(), E = new List<P2>();
            List<bool> hasE = new List<bool>();
            List<double> depth = new List<double>(), endDepth = new List<double>();
            foreach (Annot a in annots)
            {
                XYZ head, end, elbow;
                if (!a.Read(out head, out end, out elbow)) { res.Skipped++; continue; }
                ok.Add(a); H.Add(f.To2D(head)); A.Add(f.To2D(end)); depth.Add(f.Depth(head)); endDepth.Add(f.Depth(end));
                hasE.Add(elbow != null); E.Add(elbow != null ? f.To2D(elbow) : new P2());
            }
            if (ok.Count == 0) { res.Notes.Add("No tags with a leader were found."); return res; }

            double cx = A.Average(p => p.X);                       // mirror line for "whole column"
            for (int i = 0; i < ok.Count; i++)
            {
                P2 nh, ne;
                switch (mode)
                {
                    case FlipMode.Vertical: nh = new P2(H[i].X, 2 * A[i].Y - H[i].Y); ne = new P2(E[i].X, 2 * A[i].Y - E[i].Y); break;
                    case FlipMode.Both: nh = A[i] * 2.0 - H[i]; ne = A[i] * 2.0 - E[i]; break;
                    case FlipMode.WholeColumn: nh = new P2(2 * cx - H[i].X, H[i].Y); ne = new P2(2 * cx - E[i].X, E[i].Y); break;
                    default: nh = new P2(2 * A[i].X - H[i].X, H[i].Y); ne = new P2(2 * A[i].X - E[i].X, E[i].Y); break;
                }
                try
                {
                    ok[i].Apply(f.ToModel(nh, depth[i]), f.ToModel(A[i], endDepth[i]), hasE[i] ? f.ToModel(ne, depth[i]) : null, res);
                    res.Aligned++;
                }
                catch (Exception ex) { res.Skipped++; res.Notes.Add(ok[i].El.Id + ": " + ex.Message); }
            }
            doc.Regenerate();
            return res;
        }
    }
}
