using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace BIMTools.TagAlign.Leaders
{
    /// <summary>Creates the "BIM TOOLS" ribbon tab with a "Tag Align" panel holding all six commands.</summary>
    public class App : IExternalApplication
    {
        public const string TabName = "BIM TOOLS";
        public const string PanelName = "Tag Align";
        const string NS = "BIMTools.TagAlign.Leaders.";

        public Result OnStartup(UIControlledApplication app)
        {
            try { app.CreateRibbonTab(TabName); } catch { }          // tab already exists (another BIM TOOLS add-in): reuse it

            RibbonPanel panel = null;
            try
            {
                foreach (RibbonPanel p in app.GetRibbonPanels(TabName))
                    if (p.Name == PanelName) { panel = p; break; }
            }
            catch { }
            if (panel == null) panel = app.CreateRibbonPanel(TabName, PanelName);

            string asm = Assembly.GetExecutingAssembly().Location;

            PushButtonData smart = Make("SmartTagArrange", "Smart Tag\nArrange", asm, "AlignTagsAutoCommand", "TA",
                "Arrange tags in tidy columns with parallel, equal-landing or straight leaders. Presets, grouping, size-aware spacing, before/after report.");
            PushButtonData tuner = Make("LeaderTuner", "Leader Tuner", asm, "LeaderTunerCommand", "LT",
                "Re-shape leaders in place; tag heads and leader ends stay.");
            PushButtonData heads = Make("AlignHeads", "Align Tag Heads", asm, "AlignHeadsCommand", "AH",
                "Align or distribute tag heads.");
            PushButtonData clear = Make("ResolveOverlaps", "Resolve Overlaps", asm, "ResolveOverlapsCommand", "RO",
                "Nudge only the tags whose text boxes collide.");
            PushButtonData flip = Make("FlipTags", "Flip Tag Side", asm, "FlipTagsCommand", "FL",
                "Move tags to the other side of their elements, or a whole column across.");
            PushButtonData audit = Make("TagAudit", "Tag Audit", asm, "TagAuditCommand", "AU",
                "Find untagged elements, duplicate tags and orphan tags in the active view.");

            panel.AddItem(smart);
            panel.AddSeparator();
            panel.AddStackedItems(tuner, heads, clear);
            panel.AddSeparator();
            panel.AddStackedItems(flip, audit);
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app) { return Result.Succeeded; }

        static PushButtonData Make(string name, string text, string asm, string cls, string glyph, string tip)
        {
            PushButtonData b = new PushButtonData("BIMT_" + name, text, asm, NS + cls);
            b.ToolTip = tip;
            b.LargeImage = Icon(32, glyph);
            b.Image = Icon(16, glyph);
            return b;
        }

        /// <summary>Simple generated icon: teal rounded square with white letters (replace with your own artwork any time).</summary>
        static System.Windows.Media.ImageSource Icon(int size, string glyph)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.Clear(System.Drawing.Color.Transparent);
                float r = size / 5f;
                using (GraphicsPath path = new GraphicsPath())
                {
                    RectangleF rc = new RectangleF(1, 1, size - 2, size - 2);
                    path.AddArc(rc.X, rc.Y, r * 2, r * 2, 180, 90);
                    path.AddArc(rc.Right - r * 2, rc.Y, r * 2, r * 2, 270, 90);
                    path.AddArc(rc.Right - r * 2, rc.Bottom - r * 2, r * 2, r * 2, 0, 90);
                    path.AddArc(rc.X, rc.Bottom - r * 2, r * 2, r * 2, 90, 90);
                    path.CloseFigure();
                    using (Brush br = new SolidBrush(AngleDialControl.Accent)) g.FillPath(br, path);
                }
                using (Font f = new Font("Segoe UI", size * 0.36f, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    SizeF sz = g.MeasureString(glyph, f);
                    g.DrawString(glyph, f, Brushes.White, (size - sz.Width) / 2f, (size - sz.Height) / 2f);
                }
                IntPtr h = bmp.GetHbitmap(System.Drawing.Color.Transparent);
                try
                {
                    return Imaging.CreateBitmapSourceFromHBitmap(h, IntPtr.Zero, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                }
                finally { DeleteObject(h); }
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr hObject);
    }
}
