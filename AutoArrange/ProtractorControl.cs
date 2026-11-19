using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BIMTools.TagAlign.Leaders
{
    /// <summary>Round 0-360 protractor (0 = right, 90 = up, counter-clockwise) like the reference tool.</summary>
    public class ProtractorControl : Control
    {
        double _angle = 80;
        bool _drag;
        public int SnapDeg = 5;
        public event EventHandler AngleChanged;

        public ProtractorControl()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            Size = new Size(320, 320);
            Cursor = Cursors.Hand;
        }

        public double Angle
        {
            get { return _angle; }
            set
            {
                double a = ((value % 360.0) + 360.0) % 360.0;
                if (Math.Abs(a - _angle) < 1e-9) return;
                _angle = a; Invalidate();
                if (AngleChanged != null) AngleChanged(this, EventArgs.Empty);
            }
        }

        PointF Pt(PointF c, double deg, float r)
        {
            double a = deg * Math.PI / 180.0;
            return new PointF(c.X + (float)(r * Math.Cos(a)), c.Y - (float)(r * Math.Sin(a)));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);
            float R = Math.Min(Width, Height) / 2f - 6f;
            PointF c = new PointF(Width / 2f, Height / 2f);

            using (Pen thin = new Pen(Color.FromArgb(150, 150, 150)))
            using (Pen faint = new Pen(Color.FromArgb(210, 210, 210)))
            using (Font font = new Font("Segoe UI", 6.5f))
            using (Brush txt = new SolidBrush(Color.FromArgb(70, 70, 70)))
            {
                g.DrawEllipse(thin, c.X - R, c.Y - R, 2 * R, 2 * R);
                g.DrawEllipse(faint, c.X - R * 0.72f, c.Y - R * 0.72f, 1.44f * R, 1.44f * R);
                for (int d = 0; d < 360; d += 5)
                {
                    bool major = d % 10 == 0;
                    g.DrawLine(thin, Pt(c, d, R), Pt(c, d, R - (major ? 9 : 5)));
                    if (major)
                    {
                        g.DrawLine(faint, Pt(c, d, R * 0.72f), c);
                        PointF tp = Pt(c, d, R - 20);
                        string s = d.ToString();
                        SizeF sz = g.MeasureString(s, font);
                        g.DrawString(s, font, txt, tp.X - sz.Width / 2, tp.Y - sz.Height / 2);
                    }
                }
                g.DrawLine(thin, new PointF(c.X - 8, c.Y), new PointF(c.X + 8, c.Y));
                g.DrawLine(thin, new PointF(c.X, c.Y - 8), new PointF(c.X, c.Y + 8));
            }

            using (Pen opp = new Pen(Color.FromArgb(120, 200, 30, 30), 1.5f) { DashStyle = DashStyle.Dash })
            using (Pen main = new Pen(Color.FromArgb(200, 30, 30), 2.5f))
            using (Brush dot = new SolidBrush(Color.FromArgb(200, 30, 30)))
            {
                g.DrawLine(opp, c, Pt(c, _angle + 180, R * 0.72f));   // a leader is a line: both directions are equivalent
                g.DrawLine(main, c, Pt(c, _angle, R * 0.72f));
                PointF h = Pt(c, _angle, R - 4);
                g.FillEllipse(dot, h.X - 5, h.Y - 5, 10, 10);
            }
        }

        void SetFromMouse(Point p)
        {
            double dx = p.X - Width / 2.0, dy = Height / 2.0 - p.Y;
            if (Math.Abs(dx) < 2 && Math.Abs(dy) < 2) return;
            double a = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            if (SnapDeg > 1 && (ModifierKeys & Keys.Shift) == 0) a = Math.Round(a / SnapDeg) * SnapDeg;
            else a = Math.Round(a);
            Angle = a;
        }

        protected override void OnMouseDown(MouseEventArgs e) { _drag = true; SetFromMouse(e.Location); base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (_drag) SetFromMouse(e.Location); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _drag = false; base.OnMouseUp(e); }
    }
}
