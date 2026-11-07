using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AlignTool.Helpers
{
    /// <summary>Creates crisp 2D vector icons for the ALIGN ribbon without external image assets.</summary>
    public static class IconLoader
    {
        private static readonly Pen MainPen = MakePen(Color.FromRgb(39, 91, 139), 2.15);
        private static readonly Pen AccentPen = MakePen(Color.FromRgb(224, 145, 48), 2.15);

        private static Pen MakePen(Color color, double width)
        {
            var pen = new Pen(new SolidColorBrush(color), width);
            pen.StartLineCap = PenLineCap.Round;
            pen.EndLineCap = PenLineCap.Round;
            pen.LineJoin = PenLineJoin.Round;
            pen.Freeze();
            return pen;
        }

        public static BitmapSource GetIcon(string name, bool large)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name)) return null;
                var visual = new DrawingVisual();
                using (DrawingContext dc = visual.RenderOpen())
                {
                    dc.DrawGeometry(null, MainPen, Geometry.Parse(Artwork(name)));
                    switch (name)
                    {
                        case "align_left": dc.DrawLine(AccentPen, new System.Windows.Point(7, 7), new System.Windows.Point(7, 25)); break;
                        case "align_center": dc.DrawLine(AccentPen, new System.Windows.Point(16, 5), new System.Windows.Point(16, 27)); break;
                        case "align_right": dc.DrawLine(AccentPen, new System.Windows.Point(25, 7), new System.Windows.Point(25, 25)); break;
                        case "align_top": dc.DrawLine(AccentPen, new System.Windows.Point(7, 7), new System.Windows.Point(25, 7)); break;
                        case "align_middle": dc.DrawLine(AccentPen, new System.Windows.Point(5, 16), new System.Windows.Point(27, 16)); break;
                        case "align_bottom": dc.DrawLine(AccentPen, new System.Windows.Point(7, 25), new System.Windows.Point(25, 25)); break;
                        case "dist_h":
                        case "dist_v": dc.DrawLine(AccentPen, new System.Windows.Point(6, 26), new System.Windows.Point(26, 26)); break;
                        case "pin": dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(224, 145, 48)), null, new System.Windows.Point(16, 8), 2.2, 2.2); break;
                        case "unpin": dc.DrawLine(AccentPen, new System.Windows.Point(7, 25), new System.Windows.Point(25, 7)); break;
                        case "audit": dc.DrawEllipse(null, AccentPen, new System.Windows.Point(22, 22), 5, 5); break;
                        case "settings":
                        case "about": dc.DrawEllipse(null, AccentPen, new System.Windows.Point(16, 16), 3, 3); break;
                        case "invert": dc.DrawLine(AccentPen, new System.Windows.Point(16, 5), new System.Windows.Point(16, 27)); break;
                        case "select_all": dc.DrawRectangle(null, AccentPen, new System.Windows.Rect(5, 5, 22, 22)); break;
                    }
                }
                var full = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
                full.Render(visual);
                full.Freeze();
                if (large) return full;

                var smallVisual = new DrawingVisual();
                using (DrawingContext dc = smallVisual.RenderOpen())
                {
                    dc.PushTransform(new ScaleTransform(0.5, 0.5));
                    dc.DrawImage(full, new System.Windows.Rect(0, 0, 32, 32));
                    dc.Pop();
                }
                var small = new RenderTargetBitmap(16, 16, 96, 96, PixelFormats.Pbgra32);
                small.Render(smallVisual);
                small.Freeze();
                return small;
            }
            catch { return null; }
        }

        private static string Artwork(string name)
        {
            switch (name)
            {
                case "align_left": return "M7,5 L7,27 M12,9 L25,9 M12,16 L21,16 M12,23 L25,23";
                case "align_center": return "M16,5 L16,27 M6,9 L13,9 M19,9 L26,9 M9,16 L23,16 M6,23 L13,23 M19,23 L26,23";
                case "align_right": return "M25,5 L25,27 M7,9 L20,9 M11,16 L20,16 M7,23 L20,23";
                case "align_top": return "M5,7 L27,7 M9,12 L9,25 M16,12 L16,21 M23,12 L23,25";
                case "align_middle": return "M5,16 L27,16 M9,6 L9,13 M9,19 L9,26 M23,6 L23,13 M23,19 L23,26";
                case "align_bottom": return "M5,25 L27,25 M9,7 L9,20 M16,11 L16,20 M23,7 L23,20";
                case "dist_h": return "M5,9 L5,23 M27,9 L27,23 M5,16 L27,16 M11,12 L11,20 M21,12 L21,20";
                case "dist_v": return "M9,5 L23,5 M9,27 L23,27 M16,5 L16,27 M12,11 L20,11 M12,21 L20,21";
                case "align_line": return "M5,25 L27,7 M8,21 L12,25 M20,7 L24,11 M8,7 L12,7 L12,11 M20,25 L24,25 L24,21";
                case "stack": return "M7,7 L25,7 L25,12 L7,12 Z M7,15 L25,15 L25,20 L7,20 Z M7,23 L25,23";
                case "arrange_left": return "M7,5 L7,27 M12,8 L25,8 M12,16 L21,16 M12,24 L25,24";
                case "arrange_right": return "M25,5 L25,27 M7,8 L20,8 M11,16 L20,16 M7,24 L20,24";
                case "untangle": return "M6,7 L26,25 M6,25 L26,7 M6,16 L12,16 M20,16 L26,16";
                case "flip_side": return "M16,5 L16,27 M6,10 L12,10 L12,22 L6,22 Z M20,10 L26,10 L26,22 L20,22 Z";
                case "parallel": return "M7,25 L22,7 M13,27 L28,9 M5,20 L10,24 M20,8 L24,12";
                case "equal_offset": return "M7,7 L25,7 L25,25 L7,25 Z M11,11 L21,11 M11,16 L21,16 M11,21 L21,21";
                case "match_len": return "M7,8 L25,8 M7,24 L25,24 M7,5 L7,11 M25,5 L25,11 M7,21 L7,27 M25,21 L25,27";
                case "minimize": return "M6,25 L14,17 L19,17 L26,7 M20,7 L26,7 L26,13";
                case "align_h": return "M5,16 L27,16 M8,7 L24,7 M8,25 L24,25 M8,7 L8,12 M24,20 L24,25";
                case "align_v": return "M16,5 L16,27 M7,8 L7,24 M25,8 L25,24 M7,8 L12,8 M20,24 L25,24";
                case "align_ref": return "M6,8 L17,8 L17,19 L6,19 Z M20,13 L26,13 M23,10 L26,13 L23,16 M11,22 L11,27";
                case "straighten": return "M6,24 L16,16 L26,8 M22,8 L26,8 L26,12 M6,8 L6,12";
                case "orientation": return "M7,7 L25,7 L25,25 L7,25 Z M16,7 L16,25 M7,16 L25,16";
                case "select_cross": return "M6,7 L26,25 M6,25 L26,7 M9,5 L9,27 M23,5 L23,27";
                case "pin": return "M11,6 L21,6 L21,13 L24,17 L8,17 L11,13 Z M16,17 L16,27";
                case "unpin": return "M11,6 L21,6 L21,13 L24,17 L8,17 L11,13 Z M16,17 L16,27 M5,5 L27,27";
                case "settings": return "M16,5 L18,8 L22,8 L23,12 L27,14 L25,18 L27,22 L23,24 L22,28 L18,27 L16,29 L13,27 L9,28 L8,24 L5,22 L7,18 L5,14 L9,12 L10,8 L14,8 Z";
                case "about": return "M10,11 L11,8 L16,7 L21,9 L22,13 L16,17 L16,20 M16,25 L16,26";
                case "select_all": return "M7,7 L25,7 L25,25 L7,25 Z M11,11 L21,11 L21,21 L11,21 Z M4,4 L9,4 M4,4 L4,9 M28,4 L23,4 M28,4 L28,9";
                case "invert": return "M16,5 L16,27 M8,9 L13,9 L13,14 L8,14 Z M19,18 L24,18 L24,23 L19,23 Z M19,9 L24,9";
                case "audit": return "M6,7 L20,7 L20,21 L6,21 Z M10,12 L16,12 M10,16 L16,16 M20,20 L27,27";
                default: return "M6,16 L26,16 M16,6 L16,26 M8,8 L24,24 M24,8 L8,24";
            }
        }
    }
}