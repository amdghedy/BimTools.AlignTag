using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BIMTools.TagAlign.Leaders
{
    /// <summary>
    /// Half-circle gauge for the leader slope (0 = flat, 90 = vertical, 180 = flat again).
    /// Drag the handle or click anywhere on the arc. Shift = 1 degree steps, otherwise 5.
    /// </summary>
    public class AngleDialControl : Control
    {
        public static readonly Color Accent = Color.FromArgb(0, 128, 140);
        double _angle = 80;
        bool _drag;
        public event EventHandler AngleChanged;

        public AngleDialControl()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            Size = new Size(290, 170);
            Cursor = Cursors.Hand;
        }

        public double Angle
        {
            get { return _angle; }
            set
            {
                double a = Math.Max(0, Math.Min(180, value));
                if (Math.Abs(a - _angle) < 1e-9) return;
                _angle = a; Invalidate();
                if (AngleChanged != null) AngleChanged(this, EventArgs.Empty);
            }
        }

        float Radius { get { return Math.Min(Width / 2f - 22f, Height - 52f); } }
        PointF Center { get { return new PointF(Width / 2f, Height - 36f); } }

        static PointF At(PointF c, double deg, float r)
        {
            double a = deg * Math.PI / 180.0;
            return new PointF(c.X + (float)(r * Math.Cos(a)), c.Y - (float)(r * Math.Sin(a)));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            float R = Radius; PointF c = Center;
            RectangleF box = new RectangleF(c.X - R, c.Y - R, 2 * R, 2 * R);

            using (Pen track = new Pen(Color.FromArgb(225, 230, 232), 16f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(track, box, 180, 180);
            using (Pen val = new Pen(Accent, 16f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                if (_angle > 0.5) g.DrawArc(val, box, (float)(360 - _angle), (float)_angle);

            using (Pen tick = new Pen(Color.FromArgb(150, 150, 150)))
            using (Font f = new Font("Segoe UI", 7f))
            using (Brush tb = new SolidBrush(Color.FromArgb(120, 120, 120)))
            {
                for (int d = 0; d <= 180; d += 15)
                {
                    g.DrawLine(tick, At(c, d, R + 12), At(c, d, R + (d % 45 == 0 ? 19 : 16)));
                    if (d % 45 == 0)
                    {
                        PointF tp = At(c, d, R + 31);
                        string s = d + "\u00B0";
                        SizeF sz = g.MeasureString(s, f);
                        g.DrawString(s, f, tb, tp.X - sz.Width / 2, tp.Y - sz.Height / 2);
                    }
                }
            }

            // thin guide line from the centre to the handle
            using (Pen line = new Pen(Color.FromArgb(160, 160, 160), 1.5f) { DashStyle = DashStyle.Dash })
                g.DrawLine(line, c, At(c, _angle, R - 12));

            PointF h = At(c, _angle, R);
            using (Brush w = new SolidBrush(Color.White)) g.FillEllipse(w, h.X - 9, h.Y - 9, 18, 18);
            using (Pen p = new Pen(Accent, 3f)) g.DrawEllipse(p, h.X - 9, h.Y - 9, 18, 18);

            using (Font big = new Font("Segoe UI Semibold", 20f))
            using (Font small = new Font("Segoe UI", 8f))
            using (Brush dark = new SolidBrush(Color.FromArgb(50, 50, 50)))
            using (Brush grey = new SolidBrush(Color.FromArgb(130, 130, 130)))
            {
                string t = Math.Round(_angle) + "\u00B0";
                SizeF sz = g.MeasureString(t, big);
                g.DrawString(t, big, dark, c.X - sz.Width / 2, c.Y - sz.Height - 2);
                SizeF s2 = g.MeasureString("leader slope", small);
                g.DrawString("leader slope", small, grey, c.X - s2.Width / 2, c.Y + 2);
            }
        }

        void SetFromMouse(Point p)
        {
            PointF c = Center;
            double dx = p.X - c.X, dy = c.Y - p.Y;
            double a = Math.Atan2(dy, dx) * 180.0 / Math.PI;       // -180..180
            if (a < 0) a = dx >= 0 ? 0 : 180;                       // below the centre: snap to nearest end
            a = (ModifierKeys & Keys.Shift) == 0 ? Math.Round(a / 5.0) * 5.0 : Math.Round(a);
            Angle = a;
        }

        protected override void OnMouseDown(MouseEventArgs e) { _drag = true; SetFromMouse(e.Location); base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (_drag) SetFromMouse(e.Location); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _drag = false; base.OnMouseUp(e); }
    }
}
