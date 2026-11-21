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
        HeadLeft, HeadCenterX, HeadRight, HeadTop, HeadMiddle, HeadBottom, HeadDistV, HeadDistH,
        Presets, Reference, FitSize, Group, Report, Clear, Flip0, Flip1, Flip2, Flip3, Audit
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
            T("workflow", "Smart Tag Arrange", Illus.Workflow, "How the tool works",
              "Lines the selected tags up in one tidy column (or several blocks, one per group). Landing lines become horizontal and, in the default shape, all leaders get the same slope. Leader ends stay on their elements; only the tags and elbows move.",
              "1. Set the layout on the left and press Arrange.\n2. Select the tags (or select them BEFORE starting the tool).\n3. Click where the first row of the column should start.\n4. Only if 'Fill the space between two clicks' is on: click where the last row should end.",
              "Esc cancels at any step. The whole operation is one Undo. Hover any option to see what it does; press its ? button for a full-size popup.");

            T("angle", "Leader slope", Illus.Angle, "Slope of every leader (0 = flat, 90 = vertical)",
              "The slope of each leader's diagonal part, measured from the horizontal landing line: 0 = flat, 90 = vertical. A leader is a line, so there is no need to go past 180.",
              "Drag the handle on the gauge, click one of the quick buttons (30 / 45 / 60 / 75 / 90), or type a number. The gauge snaps to 5 degrees; hold Shift for 1-degree steps.",
              "75-90 gives steep, compact leaders. 45 gives the classic dog-leg look. Only used by the 'Parallel slope' shape.");

            T("style0", "Leader shape: Parallel slope", Illus.Parallel, "All diagonals parallel at the chosen slope",
              "Each leader = one horizontal landing line next to the tag + one diagonal at your slope down to the element. All diagonals end up parallel.",
              "Choose the slope on the gauge. The landing length is calculated automatically.",
              "Best when elements lie roughly in a row, such as pipes or fittings along one run.");
            T("style1", "Leader shape: Equal landing", Illus.Landing, "Same landing length, free diagonal slope",
              "Every landing line gets exactly the same length. The diagonal takes whatever slope is needed to reach its element, so diagonals are not parallel.",
              "Choose this shape, then set the landing length (mm on sheet).",
              "Good when elements are scattered and a fixed slope would make some leaders very long.");
            T("style2", "Leader shape: Straight line", Illus.Straight, "Single straight line, no elbow",
              "No elbow at all: every leader is one straight line from the tag to its element.",
              "Choose this shape. The slope and landing settings are ignored.",
              "Use for sparse drawings or tags placed close to their elements.");

            T("landing", "Landing length", Illus.Landing, "Length of the horizontal landing line (mm on sheet)",
              "Length of the short horizontal line next to the tag, in millimetres on the PRINTED sheet. The tool multiplies it by the view scale (1:100 -> 8 mm = 800 mm in the model).",
              "Enabled only with the 'Equal landing' shape. Type a value.",
              "5-10 mm is typical.");

            T("vsep", "Row spacing", Illus.VSep, "Gap between tag rows (mm on sheet)",
              "Distance between the centres of two neighbouring tag rows, in mm on the printed sheet (multiplied by the view scale).",
              "Type a value slightly bigger than your text height. For 2.5 mm text use 4 mm.",
              "If tags overlap, raise this value. Not used when 'Fill the space between two clicks' is on.");

            T("inter", "Staggered two-column layout", Illus.Intermittent, "Alternate tags between two columns",
              "Every second tag is moved sideways by the stagger offset, forming two interleaved columns. Rows can then sit closer together without the text overlapping.",
              "Tick the box, then set the stagger offset.",
              "Great for long tag texts or crowded runs.");
            T("hsep", "Stagger offset", Illus.HSep, "Sideways shift of the 2nd column (mm on sheet)",
              "How far the alternate column is shifted, in mm on the printed sheet. Enabled only with the staggered layout.",
              "Type a value larger than the width of your tag text (20-30 mm for short tags).",
              "Too small = neighbouring tags overlap horizontally.");

            T("order0", "Row order: Fewest crossings", Illus.OrderAuto, "Order that gives the fewest leader crossings",
              "The tool tries several orders and keeps the one with the fewest crossing leaders (shortest total leader length as tie-breaker).",
              "Leave it on for most jobs.", "The result message tells you how many crossings are left.");
            T("order1", "Row order: By position", Illus.OrderColumn, "Order by element position along the column",
              "Tags are ordered by where their elements sit along the column direction.",
              "Choose it when you want a predictable top-to-bottom order matching the plan.", "Crossings are not minimised.");
            T("order2", "Row order: By tag text", Illus.OrderText, "Alphabetical / numeric order of the tag text",
              "Tags are sorted by their text in natural order (WP2 comes before WP10).",
              "Choose it for schedule-like columns.", "Leaders may cross.");
            T("order3", "Row order: Keep current", Illus.OrderKeep, "Keep the present top-to-bottom order of the tags",
              "The vertical order your tags already have is preserved; only the spacing and leaders are tidied.",
              "Choose it when you already placed the tags in the order you want.", "Leaders may cross.");

            T("dist", "Fill the space between two clicks", Illus.Distribute, "Use two clicks and spread the rows between them",
              "Instead of using the row spacing, the rows are spread evenly between a first and a last click. Without this option you click only once and the column grows upward from that point.",
              "Tick it, then click the start and the end of the column. Not available with Auto column.",
              "Handy to fill an exact free zone of the drawing.");

            T("auto", "Auto column (no clicks)", Illus.AutoColumn, "The tool places the column itself",
              "No clicks needed. The tool builds the column beside the elements, starting level with the lowest element, using the row spacing.",
              "Tick it and press Arrange, then select the tags. Combine with 'Use all tags in the view' for a one-step tidy.",
              "Use the next option to choose the side.");
            T("side", "Column side", Illus.Side, "Left or right of the elements",
              "Chooses on which side of the elements the automatic column is built.",
              "Pick Left or Right. Enabled with Auto column.", "Choose the side that is emptier in your drawing.");

            T("notes", "Include text notes", Illus.Notes, "Text notes join the column (moved only)",
              "Text notes are moved into the column too, using the same order logic. Their own leaders are NOT re-angled.",
              "Tick to include them in the selection and the arrangement.", "Untick if you only want to tidy tags.");
            T("spatial", "Include room / space / area tags", Illus.Spatial, "Room, Space and Area tags join in",
              "Room, Space and Area tags are arranged like normal tags. They need a leader; the tool switches it on for them.",
              "Tick to include them.",
              "Tags whose Revit version does not expose leader data are skipped and listed in the report.");
            T("all", "Use all tags in the view", Illus.AllInView, "Ignore the selection, take all tags in the view",
              "Ignores your selection and processes every tag visible in the active view.",
              "Tick it. Combine with Auto column to tidy a whole view in one step.",
              "Without Auto column you are still asked for the start click.");

            T("tuner", "Leader Tuner", Illus.Tuner, "Re-shape leaders without moving tags",
              "Changes only the elbow of each selected leader. Tag heads and leader ends stay exactly where they are.",
              "1. Choose slope / shape.\n2. Select the tags (or tick 'Use all tags in the view').\n3. Press Apply.",
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
            // ---------------- version 2 ----------------
            T("presets", "Presets", Illus.Presets, "Pick a saved layout or save your own",
              "A preset stores the whole layout (slope, shape, spacing, order, smart options) under a name. Five ready-made presets are included. Choosing one fills in every layout option; scope options (notes, rooms, all in view, column placement) are left as you set them.",
              "Choose a preset in the list, or set up a layout and press 'Save as...' to keep it. Pressing 'Delete' removes one of your own presets (built-in ones cannot be deleted).",
              "Presets live in %AppData%\\BIMTools\\AutoArrangePresets.ini. Copy that file to a colleague to share your office standard.");

            T("ref", "Continue an existing column", Illus.Reference, "New tags continue the column of a tag you pick",
              "Instead of clicking a start point, you pick one tag that is already in place. The new tags are stacked directly above it, on the same vertical line, so a second batch of tags joins the first one perfectly.",
              "Tick it, press Arrange, select the new tags, then pick the existing tag. No other clicks are needed.",
              "Great for adding tags to a finished sheet without re-arranging the old ones. The picked tag is not moved.");

            T("fit", "Fit row spacing to the real tag size", Illus.FitSize, "Row spacing follows the measured tag size",
              "The tool measures the true height of every tag (and its width for the staggered layout) and sets the row spacing to the tallest tag plus your padding. Tall two-line tags no longer overlap and small tags no longer waste space.",
              "Tick it and choose the extra padding. The 'Row spacing' and 'Stagger offset' values are then ignored.",
              "If a tag cannot be measured, the tool falls back to your row spacing and tells you in the result message.");
            T("fitpad", "Extra padding", Illus.FitSize, "Extra gap added to the measured tag size (mm on sheet)",
              "The free gap that is added to the measured tag height (and width when staggered), in mm on the printed sheet.",
              "Type a value. 1 mm is a tight, readable stack; 2-3 mm is airy.", "Only used with 'Fit row spacing to the real tag size'.");

            string[] gn = { "No grouping", "Group by tag category", "Group by tag type", "Group by tag text prefix" };
            string[] gw = {
                "All tags form one continuous column.",
                "Tags of the same category (Pipe Tags, Duct Tags, Door Tags ...) form their own block with an empty gap between the blocks.",
                "Tags of the same tag family type form their own block.",
                "Tags whose text starts with the same letters (WP, FP, AHU ...) form their own block, so 'WP1 WP2 FP1 FP2' becomes two blocks." };
            for (int i = 0; i < 4; i++)
                T("group" + i, gn[i], Illus.Group, i == 0 ? "One column for everything" : "Separate blocks inside the column",
                  gw[i], "Choose it in the list and set the gap between the groups. Row order and crossing optimisation are applied inside every group.",
                  "Groups are placed in the order of their elements along the column (or alphabetically with 'By tag text').");
            T("groupgap", "Gap between groups", Illus.Group, "Empty rows between two groups",
              "The number of empty rows left between two blocks. 1 means one free row, 0.5 half a row.",
              "Type a value. Enabled when a grouping is chosen.", "With 'Fill the space between two clicks' the gaps are included in the equal distribution.");

            T("report", "Show before / after numbers", Illus.Report, "Result message with measured improvement",
              "The result message lists leader crossings, overlapping tags and total leader length before and after the run, so you can see what the tool improved.",
              "Tick it (default). Switch it off for a quieter message and slightly faster runs.",
              "Overlap numbers need the tag size measurement; if it is not possible, only the crossing figures are shown.");

            T("clear", "Resolve Overlaps", Illus.Clear, "Nudge only the tags that collide",
              "Looks at the real text boxes of the selected tags. Every pair that overlaps is pushed apart by the smallest possible move. Tags that do not collide stay exactly where they are, leader ends never move, nothing is re-ordered.",
              "1. Set the minimum gap, maximum move and direction.\n2. Select the tags (or tick 'Use all tags in the view').\n3. Press Resolve.",
              "Use it as the last polish after placing tags by hand, or after Smart Tag Arrange with a tight row spacing.");
            T("clearpad", "Minimum gap", Illus.Clear, "Free space left between two tags (mm on sheet)",
              "The empty space that must remain between the boxes of two neighbouring tags, in mm on the printed sheet.",
              "Type a value. 1-2 mm is typical.", "0 only separates tags that really overlap.");
            T("clearmax", "Maximum move", Illus.Clear, "A tag never moves farther than this (mm on sheet)",
              "Safety limit: no tag is moved farther than this distance from where it was. If a crowded group needs more room, the result message says how many overlaps remain.",
              "Type a value. 25 mm keeps tags close to their elements.", "For very crowded areas use Smart Tag Arrange instead of a bigger limit.");
            T("clearaxis", "Push direction", Illus.Clear, "Which way tags may be pushed",
              "Any: every overlap is solved along the shorter way out. Vertical only: tags only move up or down (good for tag columns). Horizontal only: tags only move left or right.",
              "Choose the direction in the list.", "Vertical only keeps the tags of a column in their column.");

            string[] fn = { "Flip left / right", "Flip above / below", "Point mirror (both)", "Whole column to the other side" };
            string[] fw = {
                "Every tag head jumps to the opposite side of its element, level with it. Leader end stays on the element.",
                "Every tag head jumps above / below its element, in line with it.",
                "Every tag head is mirrored through its element: left becomes right and above becomes below.",
                "The whole selection is mirrored about the vertical line through the middle of the leader ends. A column that stands left of the elements moves to the right in one click, keeping its order." };
            for (int i = 0; i < 4; i++)
                T("flip" + i, fn[i], i == 0 ? Illus.Flip0 : i == 1 ? Illus.Flip1 : i == 2 ? Illus.Flip2 : Illus.Flip3, "Move tags to the other side of their elements",
                  fw[i], "Choose the operation, press Flip, then select the tags.",
                  "Run Leader Tuner afterwards if you want all leaders parallel again.");

            T("audit", "Tag Audit", Illus.Audit, "Find untagged elements, duplicate and orphan tags",
              "Checks the active view for model elements without a tag, elements carrying the same tag twice, and tags whose element is gone. You can then select the findings, or delete the duplicate and orphan tags in one Undo step.",
              "1. Tick what to check and choose the categories.\n2. Press 'Check view'.\n3. In the result window pick a command link to select or delete the findings.",
              "After selecting untagged elements, use Revit's Tag by Category, then finish with Smart Tag Arrange.");
            T("audituntagged", "Elements without a tag", Illus.Audit, "List model elements that have no tag in this view",
              "Every element of the chosen categories that is visible in the view and has no tag attached is reported and can be selected.",
              "Tick it and choose the categories below.", "Elements of linked models are not checked.");
            T("auditdup", "Duplicate tags", Illus.Audit, "Same element tagged twice with the same tag type",
              "Finds elements that carry more than one tag of the same tag type in this view. The first tag is kept, the others are reported.",
              "Tick it. In the result window you can select or delete the extra tags.", "Two tags of different types on one element are not duplicates.");
            T("auditorphan", "Orphan tags", Illus.Audit, "Tags that lost their element",
              "Finds tags that no longer point to an element and show '?' or nothing.",
              "Tick it. In the result window you can select or delete them.", "Tags that point into a linked model are not treated as orphans.");
            T("auditcats", "Categories", Illus.Audit, "Which categories are checked for missing tags",
              "Lists the taggable model categories that occur in the active view, with their element counts.",
              "Tick the categories you expect to be tagged on this drawing (for example Pipes, Ducts, Doors). Use All / None for quick changes.",
              "Do not tick categories that are intentionally untagged, such as walls on a services drawing.");

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
        static new void Tag(Graphics g, PointF head, string t, float w, bool hl)
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
                        Dim(g, new PointF(14, h[0].Y), new PointF(14, h[1].Y), "row spacing", true);
                        Caption(g, "Gap between rows"); break;
                    }
                case Illus.Intermittent:
                case Illus.HSep:
                    {
                        PointF[] h = Stack(g, 4, 60, 70, 15, 80, 0, 0, true, 52, false, Wp, false);
                        Dim(g, new PointF(60, h[0].Y - 12), new PointF(112, h[0].Y - 12), "stagger offset", false);
                        Caption(g, "Staggered: alternate tags shift sideways"); break;
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
                        Txt(g, "click 1", 90, h[0].Y - 20, Red, 6.5f, true); Txt(g, "click 2", 90, h[3].Y + 6, Red, 6.5f, true);
                        Caption(g, "Equal gaps between the two clicks"); break;
                    }
                case Illus.AutoColumn:
                    {
                        PointF[] h = Stack(g, 4, 60, 64, 20, 80, 0, 0, false, 0, false, Wp, true);
                        Txt(g, "column built automatically - no clicks", 8, 150, Blue, 6.5f, true); break;
                    }
                case Illus.Side: DrawSides(g); break;
                case Illus.Notes: DrawNotes(g); break;
                case Illus.Spatial: DrawSpatial(g); break;
                case Illus.AllInView: DrawAllInView(g); break;
                case Illus.Tuner: DrawTuner(g); break;
                case Illus.KeepShape: DrawKeep(g); break;
                case Illus.Presets: DrawPresets(g); break;
                case Illus.FitSize: DrawFit(g); break;
                case Illus.Group: DrawGroup(g); break;
                case Illus.Reference: DrawReference(g); break;
                case Illus.Report: DrawReport(g); break;
                case Illus.Clear: DrawClear(g); break;
                case Illus.Flip0: case Illus.Flip1: case Illus.Flip2: case Illus.Flip3: DrawFlip(g, pic); break;
                case Illus.Audit: DrawAudit(g); break;
                default: DrawHeads(g, pic); break;
            }
        }

        static void DrawWorkflow(Graphics g)
        {
            string[] steps = { "1  Set the layout", "2  Select tags", "3  Click the start", "4  (optional) click the end" };
            for (int i = 0; i < 4; i++)
            {
                RectangleF r = new RectangleF(20, 12 + i * 36, 150, 28);
                using (Brush b = new SolidBrush(Color.FromArgb(235, 242, 252))) g.FillRectangle(b, r);
                using (Pen p = new Pen(Blue)) g.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
                Txt(g, steps[i], r.X + 8, r.Y + 6, Ink, 7.5f, true);
                if (i < 3) { using (Pen p = new Pen(Blue, 1.5f)) g.DrawLine(p, 95, r.Bottom, 95, r.Bottom + 8); }
            }
            // result mini
            GraphicsState st = g.Save();
            g.TranslateTransform(165, 55); g.ScaleTransform(0.4f, 0.4f);
            Stack(g, 4, 70, 70, 22, 80, 0, 0, false, 0, false, new[] { "", "", "", "" }, false);
            g.Restore(st);
            Txt(g, "result", 215, 142, Color.Gray, 6.5f, false);
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

        // ---------- version 2 drawings ----------
        static void TagBox(Graphics g, float x, float y, float w, float h, string t, bool hl, bool bad)
        {
            RectangleF r = new RectangleF(x, y, w, h);
            Color fill = bad ? Color.FromArgb(250, 225, 225) : (hl ? Color.FromArgb(215, 230, 250) : Color.FromArgb(240, 240, 240));
            using (Brush b = new SolidBrush(fill)) g.FillRectangle(b, r);
            using (Pen p = new Pen(bad ? Red : (hl ? Blue : Color.Silver))) g.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
            Txt(g, t, r.X + 2, r.Y + Math.Max(0, (h - 12) / 2), Ink, 6.2f, false);
        }

        static void Arrow(Graphics g, float x0, float y0, float x1, float y1)
        {
            using (Pen p = new Pen(Blue, 2f) { EndCap = LineCap.ArrowAnchor }) g.DrawLine(p, x0, y0, x1, y1);
        }

        static void DrawPresets(Graphics g)
        {
            string[] n = { "Classic 45", "Steep 90", "My office" };
            for (int i = 0; i < 3; i++)
            {
                RectangleF r = new RectangleF(12, 16 + i * 44, 110, 34);
                using (Brush b = new SolidBrush(i == 2 ? Color.FromArgb(225, 242, 232) : Color.FromArgb(235, 242, 252))) g.FillRectangle(b, r);
                using (Pen p = new Pen(i == 2 ? Green : Blue)) g.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
                Txt(g, n[i], r.X + 8, r.Y + 9, Ink, 8f, true);
            }
            Arrow(g, 128, 77, 154, 77);
            GraphicsState st = g.Save();
            g.TranslateTransform(150, 40); g.ScaleTransform(0.5f, 0.5f);
            Stack(g, 4, 70, 70, 22, 80, 0, 0, false, 0, false, new[] { "", "", "", "" }, true);
            g.Restore(st);
            Caption(g, "One click fills in the whole layout");
        }

        static void DrawFit(Graphics g)
        {
            float[] h = { 12, 24, 12 };
            for (int s = 0; s < 2; s++)
            {
                float ox = s * 150, y = 30;
                for (int k = 0; k < 3; k++)
                {
                    bool bad = s == 0 && k >= 1;
                    TagBox(g, ox + 20, y, 54, h[k], k == 1 ? "2-line tag" : "WP", s == 1, bad);
                    y += s == 0 ? 14 : h[k] + 4;
                }
                Txt(g, s == 0 ? "fixed spacing: overlap" : "fitted to tag size", ox + 8, 118, s == 0 ? Red : Green, 6.5f, true);
            }
            Arrow(g, 134, 60, 148, 60);
            Caption(g, "Spacing = tallest tag + padding");
        }

        static void DrawGroup(Graphics g)
        {
            for (int k = 0; k < 6; k++)
            {
                float y = 14 + k * 16 + (k >= 3 ? 16 : 0);
                TagTL(g, 40, y, k < 3 ? "PIPE " + (k + 1) : "DUCT " + (k - 2), 46, k < 3);
            }
            Dim(g, new PointF(24, 14 + 2 * 16 + TH), new PointF(24, 14 + 3 * 16 + 16), "gap", true);
            Txt(g, "Pipe tags", 100, 28, Blue, 7f, true);
            Txt(g, "Duct tags", 100, 92, Ink, 7f, true);
            Caption(g, "Separate blocks, one empty row between");
        }

        static void DrawReference(Graphics g)
        {
            for (int k = 0; k < 3; k++) TagTL(g, 60, 100 + k * 16, "OLD " + (k + 1), 46, false);
            for (int k = 0; k < 3; k++) TagTL(g, 60, 36 + k * 16, "NEW " + (k + 1), 46, true);
            Arrow(g, 150, 100, 150, 40);
            Txt(g, "continues upward", 158, 62, Blue, 6.8f, true);
            Txt(g, "picked tag", 118, 100, Red, 6.8f, true);
            using (Pen p = new Pen(Red, 1.5f)) g.DrawRectangle(p, 57, 97, 52, 19);
            Caption(g, "Same vertical line as the tag you pick");
        }

        static void DrawReport(Graphics g)
        {
            string[] a = { "Leader crossings", "Overlapping tags", "Leader length (sheet)" };
            string[] b = { "7  ->  0", "5  ->  0", "412  ->  268 mm" };
            using (Brush br = new SolidBrush(Color.FromArgb(248, 250, 252))) g.FillRectangle(br, 16, 16, 268, 112);
            using (Pen p = new Pen(Blue)) g.DrawRectangle(p, 16, 16, 268, 112);
            Txt(g, "Smart Tag Arrange", 26, 22, Ink, 8f, true);
            for (int i = 0; i < 3; i++)
            {
                Txt(g, a[i], 26, 50 + i * 24, Ink, 8f, false);
                Txt(g, b[i], 170, 50 + i * 24, Green, 8f, true);
            }
            Caption(g, "Measured before and after every run");
        }

        static void DrawClear(Graphics g)
        {
            float[] ox = { 20, 22, 18 }, oy = { 30, 40, 50 };
            for (int k = 0; k < 3; k++) TagTL(g, ox[k], oy[k], "WP " + (k + 1), 46, false);
            using (Pen p = new Pen(Red, 1.5f)) g.DrawEllipse(p, 10, 22, 70, 50);
            Arrow(g, 100, 55, 130, 55);
            for (int k = 0; k < 3; k++) TagTL(g, 170, 24 + k * 22, "WP " + (k + 1), 46, true);
            Elem(g, new PointF(150, 120));
            for (int k = 0; k < 3; k++) Poly(g, Color.Silver, 1f, new PointF(170 + 46, 24 + k * 22 + TH / 2), new PointF(150, 120));
            Txt(g, "overlap", 24, 78, Red, 6.5f, true); Txt(g, "solved, ends stay", 170, 100, Green, 6.5f, true);
            Caption(g, "Only colliding tags move, by the smallest step");
        }

        static void DrawFlip(Graphics g, Illus pic)
        {
            if (pic == Illus.Flip3)
            {
                for (int k = 0; k < 3; k++)
                {
                    PointF A = new PointF(150, 40 + k * 30);
                    Elem(g, A);
                    PointF h0 = new PointF(60, 40 + k * 30), h1 = new PointF(240, 40 + k * 30);
                    Poly(g, Color.Silver, 1f, A, h0); Tag(g, h0, "WP " + (k + 1), 36, false);
                    Poly(g, Ink, 1f, A, h1); TagTL(g, 240, 40 + k * 30 - TH / 2, "WP " + (k + 1), 36, true);
                }
                Caption(g, "The whole column mirrors to the other side");
                return;
            }
            PointF e = new PointF(150, 70);
            PointF o, n;
            switch (pic)
            {
                case Illus.Flip1: o = new PointF(150, 30); n = new PointF(150, 110); break;
                case Illus.Flip2: o = new PointF(90, 36); n = new PointF(210, 104); break;
                default: o = new PointF(90, 70); n = new PointF(210, 70); break;
            }
            Poly(g, Color.Silver, 1f, e, o); Tag(g, new PointF(o.X + 18, o.Y), "WP", 36, false);
            Poly(g, Ink, 1f, e, n); Tag(g, new PointF(n.X + 18, n.Y), "WP", 36, true);
            Elem(g, e);
            Txt(g, "before", o.X - 26, o.Y + 10, Red, 6.5f, true); Txt(g, "after", n.X - 14, n.Y + 10, Green, 6.5f, true);
            Caption(g, "Leader end stays on the element");
        }

        static void DrawAudit(Graphics g)
        {
            PointF[] el = { new PointF(40, 30), new PointF(100, 30), new PointF(160, 30), new PointF(40, 90), new PointF(100, 90), new PointF(160, 90) };
            foreach (PointF p in el) Elem(g, p);
            TagTL(g, 30, 40, "OK", 24, true);
            TagTL(g, 90, 40, "OK", 24, true);
            using (Pen p = new Pen(Red, 1.5f)) { g.DrawEllipse(p, 148, 18, 24, 24); }
            Txt(g, "no tag", 146, 44, Red, 6.5f, true);
            TagTL(g, 30, 100, "OK", 24, true); TagTL(g, 40, 110, "OK", 24, false);
            Txt(g, "x2", 70, 112, Red, 7f, true);
            TagTL(g, 200, 70, "?", 24, false);
            using (Pen p = new Pen(Red, 1.5f)) g.DrawRectangle(p, 198, 68, 28, 17);
            Txt(g, "orphan", 200, 88, Red, 6.5f, true);
            Txt(g, "1 element without tag, 1 duplicate, 1 orphan", 8, 138, Color.FromArgb(120, 120, 120), 6.8f, false);
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
