using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace BIMTools.TagAlign.Leaders
{
    public class RevitWindow : IWin32Window
    {
        public IntPtr Handle { get; private set; }
        public RevitWindow() { Handle = Process.GetCurrentProcess().MainWindowHandle; }
    }

    static class Ui
    {
        public static Label Lbl(string t, int x, int y, int w) { return new Label { Text = t, Left = x, Top = y + 3, Width = w }; }
        public static NumericUpDown Num(int x, int y, int w, decimal min, decimal max, int dec, decimal inc, decimal val)
        {
            if (val < min) val = min;
            if (val > max) val = max;
            return new NumericUpDown { Left = x, Top = y, Width = w, Minimum = min, Maximum = max, DecimalPlaces = dec, Increment = inc, Value = val };
        }
        public static ComboBox Combo(int x, int y, int w, int sel, params string[] items)
        {
            ComboBox c = new ComboBox { Left = x, Top = y, Width = w, DropDownStyle = ComboBoxStyle.DropDownList };
            c.Items.AddRange(items); c.SelectedIndex = sel; return c;
        }
        public static CheckBox Chk(string t, int x, int y, int w, bool v) { return new CheckBox { Text = t, Left = x, Top = y, Width = w, Checked = v }; }
    }

    /// <summary>Base for the option dialogs: right-hand help panel, hover/focus help, "?" popup buttons, tooltips.</summary>
    public abstract class OptionsFormBase : Form
    {
        protected HelpPanel Help;
        protected ToolTip Tips = new ToolTip { AutoPopDelay = 8000, InitialDelay = 400 };
        protected const int QX = 346;   // x of the "?" buttons

        protected void InitLayout(int leftWidth, int height, string title)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(leftWidth + 322, height);
            Help = new HelpPanel { Left = leftWidth, Top = 8, Width = 312, Height = height - 16 };
            Controls.Add(Help);
        }

        /// <summary>Bind a control (and its label) to a help key; adds hover/focus help, tooltip and a "?" button.</summary>
        protected void Reg(Control c, Control label, Func<string> key, int rowY)
        {
            Action show = delegate { Help.ShowTopic(HelpCatalog.Get(key())); };
            c.Enter += delegate { show(); };
            c.MouseEnter += delegate { show(); };
            if (label != null) label.MouseEnter += delegate { show(); };
            ComboBox cb = c as ComboBox;
            if (cb != null) cb.SelectedIndexChanged += delegate { show(); };
            Tips.SetToolTip(c, HelpCatalog.Get(key()).Short);

            Button q = new Button { Text = "?", Left = QX, Top = rowY - 1, Width = 24, Height = 24, FlatStyle = FlatStyle.Flat, ForeColor = Color.Gray, TabStop = false };
            q.FlatAppearance.BorderColor = Color.Silver;
            q.Click += delegate { using (HelpForm f = new HelpForm(HelpCatalog.Get(key()))) f.ShowDialog(this); };
            Tips.SetToolTip(q, "Open a full explanation with a picture");
            Controls.Add(q);
        }
    }

    /// <summary>"Choose Angle" dialog: protractor + separations + all options + live help.</summary>
    public class AlignOptionsForm : OptionsFormBase
    {
        readonly ProtractorControl _prot;
        readonly NumericUpDown _angle, _vsep, _hsep, _land;
        readonly ComboBox _style, _order, _side;
        readonly CheckBox _inter, _dist, _auto, _notes, _spatial, _all;
        bool _sync;

        public LeaderAlignSettings Settings { get; private set; }

        public AlignOptionsForm(LeaderAlignSettings s)
        {
            Settings = s.Clone();
            InitLayout(380, 750, "Choose Angle");

            _prot = new ProtractorControl { Left = 50, Top = 8, Width = 280, Height = 280, Angle = s.AngleDeg };
            Controls.Add(_prot);
            Reg(_prot, null, () => "angle", 8);

            int y = 298, L = 20, C = 200;
            Label l1 = Ui.Lbl("Leader angle (deg)", L, y, 175); Controls.Add(l1);
            _angle = Ui.Num(C, y, 80, 0, 360, 0, 1, (decimal)Math.Round(s.AngleDeg)); Controls.Add(_angle);
            Reg(_angle, l1, () => "angle", y);

            y += 30; Label l2 = Ui.Lbl("Leader style", L, y, 175); Controls.Add(l2);
            _style = Ui.Combo(C, y, 140, (int)s.Style, "Parallel angle", "Fixed landing", "Straight"); Controls.Add(_style);
            Reg(_style, l2, () => "style" + _style.SelectedIndex, y);

            y += 30; Label l3 = Ui.Lbl("Landing length (mm)", L, y, 175); Controls.Add(l3);
            _land = Ui.Num(C, y, 80, 1, 200, 1, 1, (decimal)s.LandingMm); Controls.Add(_land);
            Reg(_land, l3, () => "landing", y);

            y += 30; Label l4 = Ui.Lbl("Vertical separation (mm)", L, y, 175); Controls.Add(l4);
            _vsep = Ui.Num(C, y, 80, 0.5m, 200, 1, 0.5m, (decimal)s.VerticalSepMm); Controls.Add(_vsep);
            Reg(_vsep, l4, () => "vsep", y);

            y += 30; _inter = Ui.Chk("Intermittent alignment (zig-zag)", L, y, 320, s.Intermittent); Controls.Add(_inter);
            Reg(_inter, null, () => "inter", y);

            y += 28; Label l6 = Ui.Lbl("Horizontal separation (mm)", L, y, 175); Controls.Add(l6);
            _hsep = Ui.Num(C, y, 80, 1, 500, 1, 1, (decimal)s.HorizontalSepMm); Controls.Add(_hsep);
            Reg(_hsep, l6, () => "hsep", y);

            y += 30; Label l7 = Ui.Lbl("Row order", L, y, 175); Controls.Add(l7);
            _order = Ui.Combo(C, y, 140, (int)s.Order, "Auto (fewest crossings)", "Along column", "By tag text", "Keep current"); Controls.Add(_order);
            Reg(_order, l7, () => "order" + _order.SelectedIndex, y);

            y += 30; _dist = Ui.Chk("Spread rows evenly between the two picks", L, y, 320, s.DistributeBetweenPicks); Controls.Add(_dist);
            Reg(_dist, null, () => "dist", y);

            y += 28; _auto = Ui.Chk("Auto column (skip the two picks)", L, y, 320, s.AutoColumn); Controls.Add(_auto);
            Reg(_auto, null, () => "auto", y);

            y += 28; Label l10 = Ui.Lbl("Auto column side", L, y, 175); Controls.Add(l10);
            _side = Ui.Combo(C, y, 140, (int)s.Side, "Left of elements", "Right of elements"); Controls.Add(_side);
            Reg(_side, l10, () => "side", y);

            y += 30; _notes = Ui.Chk("Include text notes (moved only)", L, y, 320, s.IncludeNotes); Controls.Add(_notes);
            Reg(_notes, null, () => "notes", y);
            y += 28; _spatial = Ui.Chk("Include room / space / area tags", L, y, 320, s.IncludeSpatial); Controls.Add(_spatial);
            Reg(_spatial, null, () => "spatial", y);
            y += 28; _all = Ui.Chk("Use every tag in the view (no selection)", L, y, 320, s.AllInView); Controls.Add(_all);
            Reg(_all, null, () => "all", y);

            y += 40;
            Button ok = new Button { Text = "Proceed", Left = 190, Top = y, Width = 85, DialogResult = DialogResult.OK };
            Button cancel = new Button { Text = "Cancel", Left = 285, Top = y, Width = 85, DialogResult = DialogResult.Cancel };
            Button how = new Button { Text = "How it works", Left = 20, Top = y, Width = 110 };
            how.Click += delegate { using (HelpForm f = new HelpForm(HelpCatalog.Get("workflow"))) f.ShowDialog(this); };
            Controls.Add(ok); Controls.Add(cancel); Controls.Add(how);
            AcceptButton = ok; CancelButton = cancel;

            Action refresh = delegate
            {
                _hsep.Enabled = _inter.Checked;
                _land.Enabled = _style.SelectedIndex == 1;
                _angle.Enabled = _style.SelectedIndex == 0;
                _prot.Enabled = _style.SelectedIndex == 0;
                _side.Enabled = _auto.Checked;
                _dist.Enabled = !_auto.Checked;
            };
            _inter.CheckedChanged += delegate { refresh(); };
            _style.SelectedIndexChanged += delegate { refresh(); };
            _auto.CheckedChanged += delegate { refresh(); };
            refresh();

            _prot.AngleChanged += delegate { if (_sync) return; _sync = true; _angle.Value = (decimal)Math.Round(_prot.Angle); _sync = false; };
            _angle.ValueChanged += delegate { if (_sync) return; _sync = true; _prot.Angle = (double)_angle.Value; _sync = false; };

            ok.Click += delegate
            {
                Settings.AngleDeg = _prot.Angle;
                Settings.Style = (LeaderStyle)_style.SelectedIndex;
                Settings.LandingMm = (double)_land.Value;
                Settings.VerticalSepMm = (double)_vsep.Value;
                Settings.HorizontalSepMm = (double)_hsep.Value;
                Settings.Intermittent = _inter.Checked;
                Settings.Order = (OrderMode)_order.SelectedIndex;
                Settings.DistributeBetweenPicks = _dist.Checked;
                Settings.AutoColumn = _auto.Checked;
                Settings.Side = (AutoSide)_side.SelectedIndex;
                Settings.IncludeNotes = _notes.Checked;
                Settings.IncludeSpatial = _spatial.Checked;
                Settings.AllInView = _all.Checked;
            };
            Help.ShowTopic(HelpCatalog.Get("workflow"));
        }
    }

    /// <summary>Leader Tuner dialog.</summary>
    public class TunerForm : OptionsFormBase
    {
        readonly ProtractorControl _prot;
        readonly NumericUpDown _angle, _land;
        readonly ComboBox _style;
        readonly CheckBox _spatial, _all;
        bool _sync;
        public LeaderAlignSettings Settings { get; private set; }

        public TunerForm(LeaderAlignSettings s)
        {
            Settings = s.Clone();
            InitLayout(380, 540, "Leader Tuner");

            _prot = new ProtractorControl { Left = 50, Top = 8, Width = 280, Height = 280, Angle = s.AngleDeg };
            Controls.Add(_prot); Reg(_prot, null, () => "angle", 8);

            int y = 298, L = 20, C = 200;
            Label l1 = Ui.Lbl("Leader angle (deg)", L, y, 175); Controls.Add(l1);
            _angle = Ui.Num(C, y, 80, 0, 360, 0, 1, (decimal)Math.Round(s.AngleDeg)); Controls.Add(_angle);
            Reg(_angle, l1, () => "angle", y);
            y += 30; Label l2 = Ui.Lbl("Leader style", L, y, 175); Controls.Add(l2);
            _style = Ui.Combo(C, y, 140, (int)s.Style, "Parallel angle", "Fixed landing", "Straight"); Controls.Add(_style);
            Reg(_style, l2, () => "style" + _style.SelectedIndex, y);
            y += 30; Label l3 = Ui.Lbl("Landing length (mm)", L, y, 175); Controls.Add(l3);
            _land = Ui.Num(C, y, 80, 1, 200, 1, 1, (decimal)s.LandingMm); Controls.Add(_land);
            Reg(_land, l3, () => "landing", y);
            y += 32; _spatial = Ui.Chk("Include room / space / area tags", L, y, 320, s.IncludeSpatial); Controls.Add(_spatial);
            Reg(_spatial, null, () => "spatial", y);
            y += 28; _all = Ui.Chk("Use every tag in the view (no selection)", L, y, 320, s.AllInView); Controls.Add(_all);
            Reg(_all, null, () => "all", y);
            y += 40;
            Button ok = new Button { Text = "Apply", Left = 190, Top = y, Width = 85, DialogResult = DialogResult.OK };
            Button cancel = new Button { Text = "Cancel", Left = 285, Top = y, Width = 85, DialogResult = DialogResult.Cancel };
            Controls.Add(ok); Controls.Add(cancel); AcceptButton = ok; CancelButton = cancel;

            Action refresh = delegate
            {
                _land.Enabled = _style.SelectedIndex == 1;
                _angle.Enabled = _style.SelectedIndex == 0;
                _prot.Enabled = _style.SelectedIndex == 0;
            };
            _style.SelectedIndexChanged += delegate { refresh(); };
            refresh();
            _prot.AngleChanged += delegate { if (_sync) return; _sync = true; _angle.Value = (decimal)Math.Round(_prot.Angle); _sync = false; };
            _angle.ValueChanged += delegate { if (_sync) return; _sync = true; _prot.Angle = (double)_angle.Value; _sync = false; };
            ok.Click += delegate
            {
                Settings.AngleDeg = _prot.Angle;
                Settings.Style = (LeaderStyle)_style.SelectedIndex;
                Settings.LandingMm = (double)_land.Value;
                Settings.IncludeSpatial = _spatial.Checked;
                Settings.AllInView = _all.Checked;
            };
            Help.ShowTopic(HelpCatalog.Get("tuner"));
        }
    }

    /// <summary>Align / distribute tag heads dialog.</summary>
    public class HeadAlignForm : OptionsFormBase
    {
        readonly ComboBox _mode;
        readonly CheckBox _keep, _notes, _spatial;
        public HeadAlign Mode { get { return (HeadAlign)_mode.SelectedIndex; } }
        public bool KeepLeaderShape { get { return _keep.Checked; } }
        public bool IncludeNotes { get { return _notes.Checked; } }
        public bool IncludeSpatial { get { return _spatial.Checked; } }

        public HeadAlignForm()
        {
            InitLayout(380, 480, "Align Tag Heads");
            int y = 18, L = 20;
            Label l1 = Ui.Lbl("Operation", L, y, 70); Controls.Add(l1);
            _mode = Ui.Combo(95, y, 245, 0, "Align left", "Align centre (vertical axis)", "Align right", "Align top",
                             "Align middle (horizontal axis)", "Align bottom", "Distribute vertically", "Distribute horizontally");
            Controls.Add(_mode);
            Reg(_mode, l1, () => "head" + _mode.SelectedIndex, y);
            y += 40; _keep = Ui.Chk("Keep leader shape (elbow moves with the head)", L, y, 320, true); Controls.Add(_keep);
            Reg(_keep, null, () => "keep", y);
            y += 30; _notes = Ui.Chk("Include text notes", L, y, 320, true); Controls.Add(_notes);
            Reg(_notes, null, () => "notes", y);
            y += 30; _spatial = Ui.Chk("Include room / space / area tags", L, y, 320, true); Controls.Add(_spatial);
            Reg(_spatial, null, () => "spatial", y);
            Button ok = new Button { Text = "Apply", Left = 190, Top = 420, Width = 85, DialogResult = DialogResult.OK };
            Button cancel = new Button { Text = "Cancel", Left = 285, Top = 420, Width = 85, DialogResult = DialogResult.Cancel };
            Controls.Add(ok); Controls.Add(cancel); AcceptButton = ok; CancelButton = cancel;
            Help.ShowTopic(HelpCatalog.Get("head0"));
        }
    }
}
