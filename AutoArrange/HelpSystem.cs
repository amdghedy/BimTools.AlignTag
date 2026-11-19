using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BIMTools.TagAlign.Leaders
{
    public enum Illus
    {
        Workflow, Angle, Parallel, Landing, Straight, VSep, Intermittent, HSep,
        OrderAuto, OrderColumn, OrderText, OrderKeep, Distribute, AutoColumn, Side,
        Notes, Spatial, AllInView, Tuner, KeepShape,
        HeadLeft, HeadCenterX, HeadRight, HeadTop, HeadMiddle, HeadBottom, HeadDistV, HeadDistH
    }

    public class HelpTopic
    {
        public string Title, Short, What, How, Tip;
        public Illus Pic;
    }

    /// <summary>All help texts. Keys: option name (+ selected index for drop-downs, e.g. "style1").</summary>
    public static class HelpCatalog
    {
        static readonly Dictionary<string, HelpTopic> D = new Dictionary<string, HelpTopic>();

        static void T(string key, string title, Illus pic, string shortTip, string what, string how, string tip)
        {
            D[key] = new HelpTopic { Title = title, Pic = pic, Short = shortTip, What = what, How = how, Tip = tip };
        }

        public static HelpTopic Get(string key)
        {
            HelpTopic t;
            return D.TryGetValue(key, out t) ? t : D["workflow"];
        }

        static HelpCatalog()
        {
            T("workflow", "Align Tags Automatically", Illus.Workflow, "How the tool works",
              "Stacks the selected tags into one tidy column, makes every landing line horizontal and (in the default style) every leader parallel. Leader ends stay on their elements; only the tags and elbows move.",
              "1. Choose the options on the left and press Proceed.\n2. Select the tags (or select them BEFORE clicking the button).\n3. Click where the LOWEST tag head should be.\n4. Click where the HIGHEST tag head should be. The column runs between the two clicks.",
              "Esc cancels at any step. The whole operation is one Undo. Hover any option to see what it does; press its ? button for a full-size popup.");

            T("angle", "Leader angle", Illus.Angle, "Slope of every leader diagonal (0 = right, 90 = up)",
              "The slope of each leader's diagonal segment, measured like a protractor: 0 degrees = pointing right, 90 = straight up, counter-clockwise. A leader is a line, so 80 and 260 look identical.",
              "Drag the red dot on the protractor, or type a number in the box. Dragging snaps to 5 degrees; hold Shift for 1-degree steps.",
              "80-90 gives steep, compact leaders. 45 gives the classic dog-leg look. Only used by the 'Parallel angle' style.");

            T("style0", "Leader style: Parallel angle", Illus.Parallel, "All diagonals parallel at the chosen angle",
              "Each leader = one horizontal landing line next to the tag + one diagonal at the protractor angle down to the element. All diagonals end up parallel - the clean look of the reference tool.",
              "Pick the angle on the protractor. The landing length is calculated automatically.",
              "Best when elements lie roughly in a row, such as pipes or fittings along one run.");
            T("style1", "Leader style: Fixed landing", Illus.Landing, "Same landing length for all, free diagonal angle",
              "Every landing line gets exactly the same length. The diagonal takes whatever angle is needed to reach its element, so diagonals are not parallel.",
              "Choose this style, then set the landing length (mm on sheet) below.",
              "Good when elements are scattered and a fixed angle would make some leaders very long.");
            T("style2", "Leader style: Straight", Illus.Straight, "Single straight line, no elbow",
              "No elbow at all: every leader is one straight line from the tag to its element.",
              "Choose this style. The angle and landing settings are ignored.",
              "Use for sparse drawings or tags placed close to their elements.");

            T("landing", "Landing length", Illus.Landing, "Horizontal landing line length (mm on sheet)",
              "Length of the short horizontal line next to the tag, in millimetres on the PRINTED sheet. The tool multiplies it by the view scale (1:100 -> 8 mm = 800 mm in the model).",
              "Enabled only with the 'Fixed landing' style. Type a value.",
              "5-10 mm is typical.");

            T("vsep", "Vertical separation", Illus.VSep, "Gap between tag rows (mm on sheet)",
              "Distance between the centres of two neighbouring tag rows, in mm on the printed sheet (multiplied by the view scale).",
              "Type a value slightly bigger than your text height. For 2.5 mm text use 4 mm.",
              "If tags overlap, raise this value. Ignored when 'Spread rows evenly between the two picks' is ticked.");

            T("inter", "Intermittent alignment (zig-zag)", Illus.Intermittent, "Alternate tags between two columns",
              "Every second tag is shifted sideways by the horizontal separation, forming a zig-zag of two columns. Rows can then sit closer together without the text overlapping.",
              "Tick the box, then set the horizontal separation. Each row is still one tag high.",
              "Great for long tag texts or crowded runs.");
            T("hsep", "Horizontal separation", Illus.HSep, "Sideways shift of the 2nd column (mm on sheet)",
              "How far the alternate column is shifted, in mm on the printed sheet. Enabled only with Intermittent alignment.",
              "Type a value larger than the width of your tag text (20-30 mm for 'WP 100' style tags).",
              "Too small = neighbouring tags overlap horizontally.");

            T("order0", "Row order: Auto", Illus.OrderAuto, "Order that gives the fewest leader crossings",
              "The tool tries several orders and keeps the one with the fewest crossing leaders (shortest total leader length as tie-breaker).",
              "Leave on Auto for most jobs.", "The result message tells you how many crossings are left.");
            T("order1", "Row order: Along column", Illus.OrderColumn, "Order by element position along the column",
              "Tags are ordered by where their elements sit along the column direction.",
              "Choose it when you want a predictable top-to-bottom order matching the plan.", "Crossings are not minimised.");
            T("order2", "Row order: By tag text", Illus.OrderText, "Alphabetical / numeric order of the tag text",
              "Tags are sorted by their text in natural order (WP2 comes before WP10).",
              "Choose it for schedules-like columns.", "Leaders may cross.");
            T("order3", "Row order: Keep current", Illus.OrderKeep, "Keep the present top-to-bottom order of the tags",
              "The vertical order your tags already have is preserved; only the spacing and leaders are tidied.",
              "Choose it when you already placed the tags in the order you want.", "Leaders may cross.");

            T("dist", "Spread rows evenly between the two picks", Illus.Distribute, "Ignore the vertical separation",
              "Instead of using the vertical separation, the rows are spread evenly between your Lowest and Highest picks.",
              "Tick it, then pick the two points as usual. Disabled with Auto column.",
              "Handy to fill an exact free zone of the drawing.");

            T("auto", "Auto column (skip the two picks)", Illus.AutoColumn, "Tool builds the column itself",
              "No picks needed. The tool builds the column beside the elements starting at the lowest element, using the vertical separation.",
              "Tick it and press Proceed, then select the tags. Combine with 'Use every tag in the view' for a one-click tidy.",
              "Use the next option to choose the side.");
            T("side", "Auto column side", Illus.Side, "Left or right of the elements",
              "Chooses on which side of the elements the automatic column is built.",
              "Pick Left or Right. Enabled with Auto column.", "Choose the side that is emptier in your drawing.");

            T("notes", "Include text notes", Illus.Notes, "Text notes join the column (moved only)",
              "Text notes are moved into the column too, in the same order logic. Their own leaders are NOT re-angled.",
              "Tick to include them in the selection and the arrangement.", "Untick if you only want to tidy tags.");
            T("spatial", "Include room / space / area tags", Illus.Spatial, "Room, Space and Area tags join in",
              "Room, Space and Area tags are arranged like normal tags. They need a leader; the tool switches it on for them.",
              "Tick to include them.",
              "Tags whose Revit version does not expose leader data are skipped and listed in the report.");
            T("all", "Use every tag in the view", Illus.AllInView, "Ignore selection, take all tags in the view",
              "Ignores your selection and processes every tag visible in the active view.",
              "Tick it. Combine with Auto column to tidy a whole view with one click.",
              "Without Auto column you are still asked for the two picks.");

            T("tuner", "Leader Tuner", Illus.Tuner, "Re-shape leaders without moving tags",
              "Changes only the elbow of each selected leader. Tag heads and leader ends stay exactly where they are.",
              "1. Choose angle / style.\n2. Select the tags (or tick 'Use every tag in the view').\n3. Press Apply.",
              "Use it after moving tags by hand.");

            string[] heads = { "Align left", "Align centre (vertical axis)", "Align right", "Align top", "Align middle (horizontal axis)", "Align bottom", "Distribute vertically", "Distribute horizontally" };
            Illus[] hp = { Illus.HeadLeft, Illus.HeadCenterX, Illus.HeadRight, Illus.HeadTop, Illus.HeadMiddle, Illus.HeadBottom, Illus.HeadDistV, Illus.HeadDistH };
            string[] hw = {
                "Moves all selected tag heads to the same X as the leftmost one.",
                "Moves all heads to the average X (a common vertical centre line).",
                "Moves all heads to the same X as the rightmost one.",
                "Moves all heads to the same Y as the highest one.",
                "Moves all heads to the average Y (a common horizontal centre line).",
                "Moves all heads to the same Y as the lowest one.",
                "Keeps the highest and lowest head, spreads the others with equal vertical gaps.",
                "Keeps the leftmost and rightmost head, spreads the others with equal horizontal gaps." };
            for (int i = 0; i < heads.Length; i++)
                T("head" + i, heads[i], hp[i], hw[i], hw[i],
                  "Choose the operation, press Apply, then select two or more tags.",
                  "Distribute needs at least three tags to show a visible effect.");

            T("keep", "Keep leader shape", Illus.KeepShape, "Elbow moves with the head",
              "ON: each elbow keeps its offset from the tag head, so the leader shape travels with the tag. OFF: Revit decides the elbow.",
              "Tick the box before pressing Apply.", "Run Leader Tuner afterwards to make all leaders parallel again.");
        }
    }

    /// <summary>Draws a simple diagram for every help topic (no image files needed).</summary>
    public class IllustrationControl : Control
    {
        Illus _pic = Illus.Workflow;
        public Illus Pic { get { return _pic; } set { _pic = value; Invalidate(); } }

        static readonly Color Green = Color.FromArgb(50, 170, 80);
        static readonly Color Red = Color.FromArgb(205, 45, 45);
        static readonly Color Ink = Color.FromArgb(60, 60, 60);
        static readonly Color Blue = Color.FromArgb(40, 110, 200);

        public IllustrationControl()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            Size = new Size(300, 170);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.White);
            float k = Math.Min(Width / 300f, Height / 170f);
            g.ScaleTransform(k, k);
            using (Pen b = new Pen(Color.Gainsboro)) g.DrawRectangle(b, 0, 0, 299, 169);
            try { Draw(g, _pic); } catch { }
        }

        // ---------- primitives ----------
        const float TW = 44f, TH = 13f;

        static void Txt(Graphics g, string s, float x, float y, Color c, float size, bool bold)
        {
            using (Font f = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular))
            using (Brush br = new SolidBrush(c)) g.DrawString(s, f, br, x, y);
        }

        static void Elem(Graphics g, PointF p) { using (Brush b = new SolidBrush(Green)) g.FillEllipse(b, p.X - 4, p.Y - 4, 8, 8); }

        // tag box whose right-middle is at head
        static void Tag(Graphics g, PointF head, string t, float w, bool hl)
        {
            RectangleF r = new RectangleF(head.X - w, head.Y - TH / 2, w, TH);
            using (Brush b = new SolidBrush(hl ? Color.FromArgb(215, 230, 250) : Color.FromArgb(240, 240, 240))) g.FillRectangle(b, r);
            using (Pen p = new Pen(hl ? Blue : Color.Silver)) g.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
            Txt(g, t, r.X + 2, r.Y, Ink, 6.2f, false);
        }

        // tag box by top-left
        static void TagTL(Graphics g, float x, float y, string t, float w, bool hl)
        {
            Tag(g, new PointF(x + w, y + TH / 2), t, w, hl);
        }

        static void Poly(Graphics g, Color c, float w, params PointF[] pts)
        {
            using (Pen p = new Pen(c, w)) g.DrawLines(p, pts);
        }

        static void Dim(Graphics g, PointF a, PointF b, string label, bool vertical)
        {
            using (Pen p = new Pen(Red, 1.2f))
            {
                g.DrawLine(p, a, b);
                if (vertical) { g.DrawLine(p, a.X - 3, a.Y, a.X + 3, a.Y); g.DrawLine(p, b.X - 3, b.Y, b.X + 3, b.Y); }
                else { g.DrawLine(p, a.X, a.Y - 3, a.X, a.Y + 3); g.DrawLine(p, b.X, b.Y - 3, b.X, b.Y + 3); }
            }
            if (vertical) Txt(g, label, a.X + 4, (a.Y + b.Y) / 2 - 6, Red, 6.5f, true);
            else Txt(g, label, (a.X + b.X) / 2 - label.Length * 2.2f, a.Y - 13, Red, 6.5f, true);
        }

        static void Caption(Graphics g, string s) { Txt(g, s, 8, 152, Color.FromArgb(120, 120, 120), 7f, false); }

        /// <summary>Elements in a row at the top, tags in a column; returns the head points (row order top to bottom).</summary>
        static PointF[] Stack(Graphics g, int n, float x0, float y0, float step, double angleDeg, int style,
                              float land, bool zig, float hsep, bool crossed, string[] labels, bool hl)
        {
            float[] ax = { 150, 185, 220, 255, 280 };
            float ay = 22;
            double phi = angleDeg * Math.PI / 180.0;
            double cot = Math.Cos(phi) / Math.Sin(phi);
            PointF[] heads = new PointF[n];
            for (int k = 0; k < n; k++)
            {
                int ei = crossed ? (n - 1 - k) : k;
                PointF A = new PointF(ax[ei], ay);
                PointF H = new PointF(x0 + ((zig && k % 2 == 1) ? hsep : 0), y0 + k * step);
                PointF E;
                if (style == 1) E = new PointF(H.X + land, H.Y);
                else if (style == 2) E = new PointF((A.X + H.X) / 2, (A.Y + H.Y) / 2);
                else E = new PointF((float)(A.X - (H.Y - A.Y) * cot), H.Y);
                Poly(g, Ink, 1f, A, E, H);
                Elem(g, A);
                Tag(g, H, labels != null ? labels[k] : "WP", TW, hl);
                heads[k] = H;
            }
            return heads;
        }

        static readonly string[] Wp = { "100 WP", "100 WP", "150 WP", "100 WP" };

        // ---------- drawings ----------
        void Draw(Graphics g, Illus pic)
        {
            switch (pic)
            {
                case Illus.Workflow: DrawWorkflow(g); break;
                case Illus.Angle: DrawAngle(g); break;
                case Illus.Parallel:
                    Stack(g, 4, 70, 70, 22, 80, 0, 0, false, 0, false, Wp, false);
                    Caption(g, "Landing lines horizontal, diagonals parallel"); break;
                case Illus.Landing:
                    {
                        PointF[] h = Stack(g, 4, 70, 70, 22, 80, 1, 34, false, 0, false, Wp, false);
                        Dim(g, new PointF(70, h[1].Y + 11), new PointF(104, h[1].Y + 11), "landing", false);
                        Caption(g, "Same landing length on every leader"); break;
                    }
                case Illus.Straight:
                    Stack(g, 4, 70, 70, 22, 80, 2, 0, false, 0, false, Wp, false);
                    Caption(g, "One straight line per tag"); break;
                case Illus.VSep:
                    {
                        PointF[] h = Stack(g, 4, 70, 70, 22, 80, 0, 0, false, 0, false, Wp, false);
                        Dim(g, new PointF(14, h[0].Y), new PointF(14, h[1].Y), "vertical sep.", true);
                        Caption(g, "Gap between rows"); break;
                    }
                case Illus.Intermittent:
                case Illus.HSep:
                    {
                        PointF[] h = Stack(g, 4, 60, 70, 15, 80, 0, 0, true, 52, false, Wp, false);
                        Dim(g, new PointF(60, h[0].Y - 12), new PointF(112, h[0].Y - 12), "horizontal sep.", false);
                        Caption(g, "Zig-zag: alternate tags shift sideways"); break;
                    }
                case Illus.OrderAuto: DrawTwoStacks(g, "before: leaders cross", "after: fewest crossings"); break;
                case Illus.OrderColumn:
                    Stack(g, 4, 70, 70, 22, 80, 0, 0, false, 0, false, Wp, false);
                    Poly(g, Red, 1.5f, new PointF(14, 140), new PointF(14, 66)); Txt(g, "order along column", 18, 100, Red, 6.5f, true);
                    Caption(g, "Ordered by element position"); break;
                case Illus.OrderText:
                    Stack(g, 4, 70, 70, 22, 80, 0, 0, false, 0, false, new[] { "WP1", "WP2", "WP10", "WP11" }, true);
                    Caption(g, "Sorted by tag text (natural order)"); break;
                case Illus.OrderKeep:
                    Stack(g, 4, 70, 70, 22, 80, 0, 0, false, 0, false, new[] { "A", "B", "C", "D" }, true);
                    Poly(g, Red, 1.5f, new PointF(14, 66), new PointF(14, 140)); Txt(g, "your order", 18, 100, Red, 6.5f, true);
                    Caption(g, "Existing top-to-bottom order is kept"); break;
                case Illus.Distribute:
                    {
                        PointF[] h = Stack(g, 4, 70, 60, 26, 80, 0, 0, false, 0, false, Wp, false);
                        using (Brush b = new SolidBrush(Red))
                        {
                            g.FillEllipse(b, 74, h[0].Y - 4, 8, 8); g.FillEllipse(b, 74, h[3].Y - 4, 8, 8);
                        }
                        Txt(g, "pick 1", 90, h[0].Y - 20, Red, 6.5f, true); Txt(g, "pick 2", 90, h[3].Y + 6, Red, 6.5f, true);
                        Caption(g, "Equal gaps between the two picks"); break;
                    }
                case Illus.AutoColumn:
                    {
                        PointF[] h = Stack(g, 4, 60, 64, 20, 80, 0, 0, false, 0, false, Wp, true);
                        Txt(g, "column built automatically - no picks", 8, 150, Blue, 6.5f, true); break;
                    }
                case Illus.Side: DrawSides(g); break;
                case Illus.Notes: DrawNotes(g); break;
                case Illus.Spatial: DrawSpatial(g); break;
                case Illus.AllInView: DrawAllInView(g); break;
                case Illus.Tuner: DrawTuner(g); break;
                case Illus.KeepShape: DrawKeep(g); break;
                default: DrawHeads(g, pic); break;
            }
        }

        static void DrawWorkflow(Graphics g)
        {
            string[] steps = { "1  Choose options", "2  Select tags", "3  Pick LOWEST head", "4  Pick HIGHEST head" };
            for (int i = 0; i < 4; i++)
            {
                RectangleF r = new RectangleF(20, 12 + i * 36, 150, 28);
                using (Brush b = new SolidBrush(Color.FromArgb(235, 242, 252))) g.FillRectangle(b, r);
                using (Pen p = new Pen(Blue)) g.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
                Txt(g, steps[i], r.X + 8, r.Y + 6, Ink, 7.5f, true);
                if (i < 3) { using (Pen p = new Pen(Blue, 1.5f)) g.DrawLine(p, 95, r.Bottom, 95, r.Bottom + 8); }
            }
            // result mini
            PointF[] h = Stack(g, 4, 215, 90, 11, 80, 0, 0, false, 0, false, new[] { "", "", "", "" }, false);
            Txt(g, "result", 232, 142, Color.Gray, 6.5f, false);
        }

        static void DrawAngle(Graphics g)
        {
            double[] ang = { 80, 45 };
            for (int s = 0; s < 2; s++)
            {
                float ox = s * 150;
                double phi = ang[s] * Math.PI / 180.0;
                PointF A = new PointF(ox + 120, 30);
                PointF E = new PointF((float)(A.X - 100 * Math.Cos(phi)), (float)(A.Y + 100 * Math.Sin(phi)));
                PointF H = new PointF(E.X - 20, E.Y);
                if (s == 1) { E = new PointF((float)(A.X - 70 * Math.Cos(phi)), (float)(A.Y + 70 * Math.Sin(phi))); H = new PointF(E.X - 20, E.Y); }
                Poly(g, Ink, 1.2f, A, E, H);
                Elem(g, A);
                Tag(g, H, "WP", 28, false);
                using (Pen dash = new Pen(Color.Gray) { DashStyle = DashStyle.Dash }) g.DrawLine(dash, E.X, E.Y, E.X + 40, E.Y);
                using (Pen arc = new Pen(Red, 1.5f)) g.DrawArc(arc, E.X - 18, E.Y - 18, 36, 36, (float)(-ang[s]), (float)ang[s]);
                Txt(g, ang[s] + "\u00B0", E.X + 14, E.Y - 22, Red, 8f, true);
            }
            Caption(g, "Angle measured from the horizontal landing line");
        }

        void DrawTwoStacks(Graphics g, string c1, string c2)
        {
            GraphicsState st = g.Save();
            g.ScaleTransform(0.5f, 0.5f); g.TranslateTransform(0, 18);
            Stack(g, 4, 70, 70, 22, 80, 0, 0, false, 0, true, new[] { "", "", "", "" }, false);
            g.Restore(st);
            st = g.Save();
            g.TranslateTransform(150, 0); g.ScaleTransform(0.5f, 0.5f); g.TranslateTransform(0, 18);
            Stack(g, 4, 70, 70, 22, 80, 0, 0, false, 0, false, new[] { "", "", "", "" }, false);
            g.Restore(st);
            Txt(g, c1, 6, 128, Red, 6.5f, true); Txt(g, c2, 156, 128, Green, 6.5f, true);
            using (Pen p = new Pen(Color.Silver)) g.DrawLine(p, 150, 10, 150, 125);
        }

        static void DrawSides(Graphics g)
        {
            for (int s = 0; s < 2; s++)
            {
                float ox = s * 150;
                using (Brush b = new SolidBrush(Green))
                    for (int i = 0; i < 3; i++) g.FillEllipse(b, ox + 70 + i * 10, 40 + i * 6, 8, 8);
                float cx = s == 0 ? ox + 14 : ox + 110;
                for (int i = 0; i < 3; i++)
                {
                    RectangleF r = new RectangleF(cx, 80 + i * 18, 28, 12);
                    using (Brush b = new SolidBrush(Color.FromArgb(215, 230, 250))) g.FillRectangle(b, r);
                    using (Pen p = new Pen(Blue)) g.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
                    PointF from = new PointF(s == 0 ? r.Right : r.X, r.Y + 6);
                    Poly(g, Ink, 1f, from, new PointF(75 + ox + i * 10, 48 + i * 6));
                }
                Txt(g, s == 0 ? "Left" : "Right", ox + 60, 128, Blue, 7.5f, true);
            }
            Caption(g, "Side of the elements for the automatic column");
        }

        static void DrawNotes(Graphics g)
        {
            PointF[] h = Stack(g, 3, 70, 66, 22, 80, 0, 0, false, 0, false, new[] { "100 WP", "100 WP", "NOTE" }, false);
            RectangleF r = new RectangleF(26, h[2].Y - 7, 44, 14);
            using (Brush b = new SolidBrush(Color.FromArgb(255, 248, 200))) g.FillRectangle(b, r);
            using (Pen p = new Pen(Color.Goldenrod)) g.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
            Txt(g, "TEXT NOTE", r.X + 2, r.Y, Ink, 6f, true);
            Caption(g, "Text notes are moved into the column (leader not re-angled)");
        }

        static void DrawSpatial(Graphics g)
        {
            using (Pen p = new Pen(Color.Gray, 1.5f))
                g.DrawPolygon(p, new[] { new PointF(150, 20), new PointF(280, 20), new PointF(280, 80), new PointF(150, 80) });
            Txt(g, "Room 101", 190, 40, Color.Gray, 7f, false);
            PointF A = new PointF(215, 60);
            Elem(g, A);
            PointF H = new PointF(70, 130);
            Poly(g, Ink, 1f, A, new PointF(215 - 0.176f * 70, 130), H);
            Tag(g, H, "101 Office", 54, true);
            Caption(g, "Room / Space / Area tags move out of the room");
        }

        static void DrawAllInView(Graphics g)
        {
            using (Pen p = new Pen(Color.Gray, 1.5f)) g.DrawRectangle(p, 14, 14, 140, 110);
            Random rnd = new Random(7);
            for (int i = 0; i < 8; i++) TagTL(g, 20 + rnd.Next(0, 80), 20 + rnd.Next(0, 90), "WP", 28, true);
            using (Pen p = new Pen(Blue, 2f) { EndCap = LineCap.ArrowAnchor }) g.DrawLine(p, 160, 70, 190, 70);
            using (Pen p = new Pen(Color.Gray, 1.5f)) g.DrawRectangle(p, 196, 14, 90, 110);
            for (int i = 0; i < 8; i++) TagTL(g, 202, 20 + i * 13, "WP", 28, true);
            Caption(g, "Every tag in the view is arranged, no selection needed");
        }

        static void DrawTuner(Graphics g)
        {
            double[] ang = { 50, 110, 70, 130 };
            for (int s = 0; s < 2; s++)
            {
                float ox = s * 150;
                for (int k = 0; k < 4; k++)
                {
                    PointF H = new PointF(ox + 60, 40 + k * 22);
                    PointF A = new PointF(ox + 105 + k * 11, 20);
                    double phi = (s == 0 ? ang[k] : 80) * Math.PI / 180.0;
                    PointF E = new PointF((float)(A.X - (H.Y - A.Y) * Math.Cos(phi) / Math.Sin(phi)), H.Y);
                    if (E.X < H.X + 8) E = new PointF(H.X + 8, H.Y);
                    Poly(g, Ink, 1f, A, E, H);
                    Elem(g, A);
                    Tag(g, H, "WP", 30, false);
                }
                Txt(g, s == 0 ? "before: mixed angles" : "after: parallel", ox + 14, 130, s == 0 ? Red : Green, 6.5f, true);
            }
            Caption(g, "Heads and leader ends stay; only elbows change");
        }

        static void DrawKeep(Graphics g)
        {
            for (int s = 0; s < 2; s++)
            {
                float ox = s * 150;
                PointF A = new PointF(ox + 120, 130);
                Elem(g, A);
                PointF H0 = new PointF(ox + 40, 40);
                Tag(g, H0, "WP", 30, false);
                Poly(g, Color.Silver, 1f, H0, new PointF(ox + 78, 40), A);
                PointF H = new PointF(ox + 40, 80);
                Tag(g, H, "WP", 30, true);
                if (s == 0) Poly(g, Ink, 1.2f, H, new PointF(ox + 78, 80), A);
                else Poly(g, Ink, 1.2f, H, A);
                Txt(g, s == 0 ? "ON: shape moves" : "OFF: Revit decides", ox + 14, 140, s == 0 ? Green : Red, 6.5f, true);
            }
            Caption(g, "Grey = old position, black = new position");
        }

        static void DrawHeads(Graphics g, Illus pic)
        {
            float[] bx = { 4, 34, 66, 98 };
            float[] by = { 22, 70, 44, 100 };
            const float W = 28f;
            PointF[] after = new PointF[4];
            for (int i = 0; i < 4; i++) after[i] = new PointF(bx[i], by[i]);
            float minX = 4, maxX = 98, minY = 22, maxY = 100;
            switch (pic)
            {
                case Illus.HeadLeft: for (int i = 0; i < 4; i++) after[i].X = minX; break;
                case Illus.HeadRight: for (int i = 0; i < 4; i++) after[i].X = maxX; break;
                case Illus.HeadCenterX: for (int i = 0; i < 4; i++) after[i].X = 51; break;
                case Illus.HeadTop: for (int i = 0; i < 4; i++) after[i].Y = minY; break;
                case Illus.HeadBottom: for (int i = 0; i < 4; i++) after[i].Y = maxY; break;
                case Illus.HeadMiddle: for (int i = 0; i < 4; i++) after[i].Y = 61; break;
                case Illus.HeadDistV:
                    { int[] o = { 0, 2, 1, 3 }; for (int k = 0; k < 4; k++) after[o[k]].Y = minY + (maxY - minY) * k / 3f; break; }
                case Illus.HeadDistH:
                    { for (int k = 0; k < 4; k++) after[k].X = minX + (maxX - minX) * k / 3f; break; }
            }
            for (int i = 0; i < 4; i++)
            {
                TagTL(g, bx[i], by[i], "WP", W, false);
                TagTL(g, 150 + after[i].X, after[i].Y, "WP", W, true);
            }
            using (Pen p = new Pen(Blue, 2f) { EndCap = LineCap.ArrowAnchor }) g.DrawLine(p, 138, 70, 150, 70);
            using (Pen p = new Pen(Color.Silver)) g.DrawLine(p, 148, 8, 148, 140);
            Txt(g, "before", 50, 132, Red, 6.5f, true); Txt(g, "after", 205, 132, Green, 6.5f, true);
        }
    }

    /// <summary>Right-hand help panel: illustration + grey explanatory text.</summary>
    public class HelpPanel : Panel
    {
        readonly Label _title;
        readonly IllustrationControl _pic;
        readonly RichTextBox _txt;

        public HelpPanel()
        {
            BackColor = Color.FromArgb(248, 248, 248);
            BorderStyle = BorderStyle.FixedSingle;
            _title = new Label { Left = 8, Top = 8, Width = 290, Height = 36, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Color.FromArgb(40, 40, 40) };
            _pic = new IllustrationControl { Left = 8, Top = 48, Width = 290, Height = 164 };
            _txt = new RichTextBox
            {
                Left = 8, Top = 220, Width = 290, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = BackColor,
                ScrollBars = RichTextBoxScrollBars.Vertical, TabStop = false, DetectUrls = false
            };
            Controls.Add(_title); Controls.Add(_pic); Controls.Add(_txt);
            Resize += delegate { _txt.Height = Math.Max(40, Height - 228); };
            ShowTopic(HelpCatalog.Get("workflow"));
        }

        void Add(string text, bool header)
        {
            _txt.SelectionStart = _txt.TextLength; _txt.SelectionLength = 0;
            _txt.SelectionFont = new Font("Segoe UI", header ? 8f : 9f, header ? FontStyle.Bold : FontStyle.Regular);
            _txt.SelectionColor = header ? Color.FromArgb(70, 70, 70) : Color.FromArgb(125, 125, 125);   // grey explanatory text
            _txt.AppendText(text);
        }

        public void ShowTopic(HelpTopic t)
        {
            _title.Text = t.Title;
            _pic.Pic = t.Pic;
            _txt.Clear();
            Add("WHAT IT DOES\n", true); Add(t.What + "\n\n", false);
            Add("HOW TO USE IT\n", true); Add(t.How + "\n\n", false);
            Add("TIP\n", true); Add(t.Tip, false);
        }
    }

    /// <summary>Big popup shown by the "?" buttons.</summary>
    public class HelpForm : Form
    {
        public HelpForm(HelpTopic t)
        {
            Text = "Help - " + t.Title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            ClientSize = new Size(330, 610);
            HelpPanel p = new HelpPanel { Left = 8, Top = 8, Width = 314, Height = 552 };
            p.ShowTopic(t);
            Controls.Add(p);
            Button close = new Button { Text = "Close", Left = 235, Top = 570, Width = 85, DialogResult = DialogResult.OK };
            Controls.Add(close); AcceptButton = close; CancelButton = close;
        }
    }
}
