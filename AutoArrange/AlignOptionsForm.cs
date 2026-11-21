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
        public static GroupBox Group(string t, int x, int y, int w, int h)
        {
            return new GroupBox { Text = t, Left = x, Top = y, Width = w, Height = h, ForeColor = AngleDialControl.Accent };
        }
        public static Button Primary(string t, int x, int y, int w)
        {
            Button b = new Button { Text = t, Left = x, Top = y, Width = w, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = AngleDialControl.Accent, ForeColor = Color.White, DialogResult = DialogResult.OK };
            b.FlatAppearance.BorderSize = 0; return b;
        }
        public static Button Secondary(string t, int x, int y, int w)
        {
            Button b = new Button { Text = t, Left = x, Top = y, Width = w, Height = 28, FlatStyle = FlatStyle.Flat };
            b.FlatAppearance.BorderColor = Color.Silver; return b;
        }
        /// <summary>Row of quick-pick slope buttons under the gauge.</summary>
        public static void Presets(Control parent, int x, int y, AngleDialControl dial)
        {
            int[] v = { 30, 45, 60, 75, 90 };
            for (int i = 0; i < v.Length; i++)
            {
                int val = v[i];
                Button b = Secondary(val + "\u00B0", x + i * 54, y, 50);
                b.Height = 24;
                b.Click += delegate { dial.Angle = val; };
                parent.Controls.Add(b);
            }
        }
    }

    /// <summary>Base for the option dialogs: right-hand help panel, hover/focus help, "?" popup buttons, tooltips.</summary>
    public abstract class OptionsFormBase : Form
    {
        protected HelpPanel Help;
        protected ToolTip Tips = new ToolTip { AutoPopDelay = 8000, InitialDelay = 400 };

        protected void InitLayout(int leftWidth, int height, string title)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.FromArgb(250, 251, 252);
            ClientSize = new Size(leftWidth + 322, height);
            Help = new HelpPanel { Left = leftWidth, Top = 8, Width = 312, Height = height - 16 };
            Controls.Add(Help);
        }

        /// <summary>Hover / focus help and tooltip only (no "?" button) for controls that share a row with other buttons.</summary>
        protected void Hover(Control c, Func<string> key)
        {
            Action show = delegate { Help.ShowTopic(HelpCatalog.Get(key())); };
            c.Enter += delegate { show(); };
            c.MouseEnter += delegate { show(); };
            Tips.SetToolTip(c, HelpCatalog.Get(key()).Short);
        }

        /// <summary>Bind a control (already added to its parent) to a help key: hover/focus help, tooltip and a "?" button.</summary>
        protected void Reg(Control c, Control label, Func<string> key)
        {
            Action show = delegate { Help.ShowTopic(HelpCatalog.Get(key())); };
            c.Enter += delegate { show(); };
            c.MouseEnter += delegate { show(); };
            if (label != null) label.MouseEnter += delegate { show(); };
            ComboBox cb = c as ComboBox;
            if (cb != null) cb.SelectedIndexChanged += delegate { show(); };
            Tips.SetToolTip(c, HelpCatalog.Get(key()).Short);

            Control parent = c.Parent ?? this;
            Button q = new Button { Text = "?", Left = (parent == this ? 350 : parent.ClientSize.Width - 30), Top = c.Top - 1, Width = 22, Height = 22, FlatStyle = FlatStyle.Flat, ForeColor = Color.Gray, TabStop = false };
            q.FlatAppearance.BorderColor = Color.Silver;
            q.Click += delegate { using (HelpForm f = new HelpForm(HelpCatalog.Get(key()))) f.ShowDialog(this); };
            Tips.SetToolTip(q, "Open a full explanation with a picture");
            parent.Controls.Add(q);
        }
    }

    /// <summary>Main dialog "Smart Tag Arrange" (two tabs: Layout / Placement and smart options).</summary>
    public class AlignOptionsForm : OptionsFormBase
    {
        readonly AngleDialControl _dial;
        readonly NumericUpDown _angle, _spacing, _offset, _land, _pad, _gap;
        readonly ComboBox _style, _order, _side, _group, _preset;
        readonly CheckBox _stagger, _fill, _auto, _notes, _spatial, _all, _fit, _ref, _report;
        readonly Button _savePreset, _delPreset;
        bool _sync;
        const string Custom = "(current settings)";

        public LeaderAlignSettings Settings { get; private set; }

        static decimal Cl(double v, decimal min, decimal max)
        {
            decimal d = (decimal)v;
            return d < min ? min : (d > max ? max : d);
        }

        public AlignOptionsForm(LeaderAlignSettings s)
        {
            Settings = s.Clone();
            InitLayout(380, 684, "Smart Tag Arrange");
            double startAngle = s.AngleDeg % 180.0;

            TabControl tabs = new TabControl { Left = 6, Top = 6, Width = 368, Height = 630 };
            TabPage t1 = new TabPage("Layout") { UseVisualStyleBackColor = false, BackColor = BackColor };
            TabPage t2 = new TabPage("Placement and smart options") { UseVisualStyleBackColor = false, BackColor = BackColor };
            tabs.TabPages.Add(t1); tabs.TabPages.Add(t2);
            Controls.Add(tabs);

            // ================= TAB 1: layout =================
            _dial = new AngleDialControl { Left = 35, Top = 6, Width = 290, Height = 170, BackColor = BackColor, Angle = startAngle };
            t1.Controls.Add(_dial); Reg(_dial, null, () => "angle");
            Ui.Presets(t1, 45, 180, _dial);

            GroupBox g1 = Ui.Group("Leaders", 6, 212, 346, 118); t1.Controls.Add(g1);
            Label l1 = Ui.Lbl("Slope (degrees)", 12, 22, 150); g1.Controls.Add(l1);
            _angle = Ui.Num(170, 22, 80, 0, 180, 0, 1, (decimal)Math.Round(startAngle)); g1.Controls.Add(_angle);
            Reg(_angle, l1, () => "angle");
            Label l2 = Ui.Lbl("Shape", 12, 52, 150); g1.Controls.Add(l2);
            _style = Ui.Combo(170, 52, 140, (int)s.Style, "Parallel slope", "Equal landing", "Straight line"); g1.Controls.Add(_style);
            Reg(_style, l2, () => "style" + _style.SelectedIndex);
            Label l3 = Ui.Lbl("Landing length (mm)", 12, 82, 150); g1.Controls.Add(l3);
            _land = Ui.Num(170, 82, 80, 1, 200, 1, 1, (decimal)s.LandingMm); g1.Controls.Add(_land);
            Reg(_land, l3, () => "landing");

            GroupBox g2 = Ui.Group("Rows", 6, 336, 346, 176); t1.Controls.Add(g2);
            Label l4 = Ui.Lbl("Row spacing (mm)", 12, 22, 150); g2.Controls.Add(l4);
            _spacing = Ui.Num(170, 22, 80, 0.5m, 200, 1, 0.5m, (decimal)s.VerticalSepMm); g2.Controls.Add(_spacing);
            Reg(_spacing, l4, () => "vsep");
            _stagger = Ui.Chk("Staggered two-column layout", 12, 52, 290, s.Intermittent); g2.Controls.Add(_stagger);
            Reg(_stagger, null, () => "inter");
            Label l6 = Ui.Lbl("Stagger offset (mm)", 12, 80, 150); g2.Controls.Add(l6);
            _offset = Ui.Num(170, 80, 80, 1, 500, 1, 1, (decimal)s.HorizontalSepMm); g2.Controls.Add(_offset);
            Reg(_offset, l6, () => "hsep");
            Label l7 = Ui.Lbl("Row order", 12, 110, 150); g2.Controls.Add(l7);
            _order = Ui.Combo(170, 110, 140, (int)s.Order, "Fewest crossings", "By position", "By tag text", "Keep current"); g2.Controls.Add(_order);
            Reg(_order, l7, () => "order" + _order.SelectedIndex);
            _fill = Ui.Chk("Fill the space between two clicks", 12, 142, 290, s.DistributeBetweenPicks); g2.Controls.Add(_fill);
            Reg(_fill, null, () => "dist");

            // presets
            Label lp = Ui.Lbl("Preset", 8, 524, 50); t1.Controls.Add(lp);
            _preset = new ComboBox { Left = 62, Top = 524, Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            t1.Controls.Add(_preset);
            _savePreset = Ui.Secondary("Save as...", 228, 522, 66); _savePreset.Height = 26; t1.Controls.Add(_savePreset);
            _delPreset = Ui.Secondary("Delete", 298, 522, 54); _delPreset.Height = 26; t1.Controls.Add(_delPreset);
            Hover(_preset, () => "presets"); Hover(lp, () => "presets"); Hover(_savePreset, () => "presets"); Hover(_delPreset, () => "presets");
            FillPresets(Custom);

            // ================= TAB 2: placement, scope, smart options =================
            GroupBox g3 = Ui.Group("Placement and scope", 6, 8, 346, 194); t2.Controls.Add(g3);
            _auto = Ui.Chk("Auto column (no clicks)", 12, 22, 290, s.AutoColumn); g3.Controls.Add(_auto);
            Reg(_auto, null, () => "auto");
            Label l10 = Ui.Lbl("Column side", 12, 48, 150); g3.Controls.Add(l10);
            _side = Ui.Combo(170, 48, 140, (int)s.Side, "Left of elements", "Right of elements"); g3.Controls.Add(_side);
            Reg(_side, l10, () => "side");
            _ref = Ui.Chk("Continue an existing column (pick a tag)", 12, 78, 290, s.UseReferenceTag); g3.Controls.Add(_ref);
            Reg(_ref, null, () => "ref");
            _notes = Ui.Chk("Include text notes (moved only)", 12, 106, 290, s.IncludeNotes); g3.Controls.Add(_notes);
            Reg(_notes, null, () => "notes");
            _spatial = Ui.Chk("Include room / space / area tags", 12, 132, 290, s.IncludeSpatial); g3.Controls.Add(_spatial);
            Reg(_spatial, null, () => "spatial");
            _all = Ui.Chk("Use all tags in the view", 12, 158, 290, s.AllInView); g3.Controls.Add(_all);
            Reg(_all, null, () => "all");

            GroupBox g4 = Ui.Group("Smart options", 6, 210, 346, 190); t2.Controls.Add(g4);
            _fit = Ui.Chk("Fit row spacing to the real tag size", 12, 22, 290, s.AutoRowHeight); g4.Controls.Add(_fit);
            Reg(_fit, null, () => "fit");
            Label l11 = Ui.Lbl("Extra padding (mm)", 12, 50, 150); g4.Controls.Add(l11);
            _pad = Ui.Num(170, 50, 80, 0, 50, 1, 0.5m, (decimal)s.RowPaddingMm); g4.Controls.Add(_pad);
            Reg(_pad, l11, () => "fitpad");
            Label l12 = Ui.Lbl("Group tags by", 12, 82, 150); g4.Controls.Add(l12);
            _group = Ui.Combo(170, 82, 140, (int)s.Group, "No grouping", "Tag category", "Tag type", "Tag text prefix"); g4.Controls.Add(_group);
            Reg(_group, l12, () => "group" + _group.SelectedIndex);
            Label l13 = Ui.Lbl("Gap between groups (rows)", 12, 112, 156); g4.Controls.Add(l13);
            _gap = Ui.Num(170, 112, 80, 0, 10, 1, 0.5m, (decimal)s.GroupGapRows); g4.Controls.Add(_gap);
            Reg(_gap, l13, () => "groupgap");
            _report = Ui.Chk("Show before / after numbers", 12, 146, 290, s.DetailedReport); g4.Controls.Add(_report);
            Reg(_report, null, () => "report");

            // ================= buttons =================
            Button ok = Ui.Primary("Arrange", 190, 644, 85);
            Button cancel = Ui.Secondary("Cancel", 285, 644, 85); cancel.DialogResult = DialogResult.Cancel;
            Button how = Ui.Secondary("How it works", 10, 644, 110);
            how.Click += delegate { using (HelpForm f = new HelpForm(HelpCatalog.Get("workflow"))) f.ShowDialog(this); };
            Controls.Add(ok); Controls.Add(cancel); Controls.Add(how);
            AcceptButton = ok; CancelButton = cancel;

            Action refresh = delegate
            {
                _offset.Enabled = _stagger.Checked && !_fit.Checked;
                _spacing.Enabled = !_fit.Checked;
                _pad.Enabled = _fit.Checked;
                _gap.Enabled = _group.SelectedIndex > 0;
                _land.Enabled = _style.SelectedIndex == 1;
                _angle.Enabled = _style.SelectedIndex == 0;
                _dial.Enabled = _style.SelectedIndex == 0;
                _auto.Enabled = !_ref.Checked;
                _side.Enabled = _auto.Checked && !_ref.Checked;
                _fill.Enabled = !_auto.Checked && !_ref.Checked;
                _delPreset.Enabled = !PresetStore.IsBuiltIn(_preset.Text) && _preset.Text != Custom;
            };
            _stagger.CheckedChanged += delegate { refresh(); };
            _style.SelectedIndexChanged += delegate { refresh(); };
            _auto.CheckedChanged += delegate { refresh(); };
            _ref.CheckedChanged += delegate { refresh(); };
            _fit.CheckedChanged += delegate { refresh(); };
            _group.SelectedIndexChanged += delegate { refresh(); };
            refresh();

            _dial.AngleChanged += delegate { if (_sync) return; _sync = true; _angle.Value = (decimal)Math.Round(_dial.Angle); _sync = false; };
            _angle.ValueChanged += delegate { if (_sync) return; _sync = true; _dial.Angle = (double)_angle.Value; _sync = false; };

            _preset.SelectedIndexChanged += delegate
            {
                refresh();
                if (_sync || _preset.SelectedIndex <= 0) return;
                LeaderAlignSettings p = PresetStore.Get(_preset.Text);
                p.IncludeNotes = _notes.Checked; p.IncludeSpatial = _spatial.Checked; p.AllInView = _all.Checked;   // scope is not part of a layout
                p.AutoColumn = _auto.Checked; p.Side = (AutoSide)_side.SelectedIndex; p.UseReferenceTag = _ref.Checked;
                p.DetailedReport = _report.Checked;
                LoadFrom(p);
                refresh();
            };
            _savePreset.Click += delegate
            {
                string name = Prompt.Ask(this, "Save preset", "Name of the preset (an existing name is overwritten):",
                                         _preset.SelectedIndex > 0 && !PresetStore.IsBuiltIn(_preset.Text) ? _preset.Text : "");
                if (string.IsNullOrEmpty(name)) return;
                if (!PresetStore.Save(name, Collect()))
                { MessageBox.Show(this, "This name cannot be used (empty, contains [ ], or is a built-in preset name).", "Save preset"); return; }
                FillPresets(name);
            };
            _delPreset.Click += delegate
            {
                if (PresetStore.IsBuiltIn(_preset.Text) || _preset.SelectedIndex <= 0) return;
                if (MessageBox.Show(this, "Delete the preset \"" + _preset.Text + "\"?", "Delete preset", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                PresetStore.Delete(_preset.Text);
                FillPresets(Custom);
            };

            ok.Click += delegate { Settings = Collect(); };
            Help.ShowTopic(HelpCatalog.Get("workflow"));
        }

        void FillPresets(string select)
        {
            _preset.Items.Clear();
            _preset.Items.Add(Custom);
            foreach (string n in PresetStore.Names()) _preset.Items.Add(n);
            int i = _preset.Items.IndexOf(select);
            _sync = true;
            _preset.SelectedIndex = i < 0 ? 0 : i;
            _sync = false;
            _delPreset.Enabled = _preset.SelectedIndex > 0 && !PresetStore.IsBuiltIn(_preset.Text);
        }

        /// <summary>Copies every control value into a settings object.</summary>
        LeaderAlignSettings Collect()
        {
            LeaderAlignSettings r = Settings.Clone();
            r.AngleDeg = _dial.Angle;
            r.Style = (LeaderStyle)_style.SelectedIndex;
            r.LandingMm = (double)_land.Value;
            r.VerticalSepMm = (double)_spacing.Value;
            r.HorizontalSepMm = (double)_offset.Value;
            r.Intermittent = _stagger.Checked;
            r.Order = (OrderMode)_order.SelectedIndex;
            r.DistributeBetweenPicks = _fill.Checked;
            r.AutoColumn = _auto.Checked;
            r.Side = (AutoSide)_side.SelectedIndex;
            r.UseReferenceTag = _ref.Checked;
            r.IncludeNotes = _notes.Checked;
            r.IncludeSpatial = _spatial.Checked;
            r.AllInView = _all.Checked;
            r.AutoRowHeight = _fit.Checked;
            r.RowPaddingMm = (double)_pad.Value;
            r.Group = (GroupMode)_group.SelectedIndex;
            r.GroupGapRows = (double)_gap.Value;
            r.DetailedReport = _report.Checked;
            return r;
        }

        /// <summary>Puts a settings object into the controls (used when a preset is chosen).</summary>
        void LoadFrom(LeaderAlignSettings p)
        {
            double a = p.AngleDeg % 180.0;
            _sync = true;
            _dial.Angle = a;
            _angle.Value = Cl(Math.Round(a), 0, 180);
            _sync = false;
            _style.SelectedIndex = (int)p.Style;
            _land.Value = Cl(p.LandingMm, 1, 200);
            _spacing.Value = Cl(p.VerticalSepMm, 0.5m, 200);
            _stagger.Checked = p.Intermittent;
            _offset.Value = Cl(p.HorizontalSepMm, 1, 500);
            _order.SelectedIndex = (int)p.Order;
            _fill.Checked = p.DistributeBetweenPicks;
            _fit.Checked = p.AutoRowHeight;
            _pad.Value = Cl(p.RowPaddingMm, 0, 50);
            _group.SelectedIndex = (int)p.Group;
            _gap.Value = Cl(p.GroupGapRows, 0, 10);
        }
    }

    /// <summary>Leader Tuner dialog.</summary>
    public class TunerForm : OptionsFormBase
    {
        readonly AngleDialControl _dial;
        readonly NumericUpDown _angle, _land;
        readonly ComboBox _style;
        readonly CheckBox _spatial, _all;
        bool _sync;
        public LeaderAlignSettings Settings { get; private set; }

        public TunerForm(LeaderAlignSettings s)
        {
            Settings = s.Clone();
            InitLayout(380, 480, "Leader Tuner");
            double startAngle = s.AngleDeg % 180.0;

            _dial = new AngleDialControl { Left = 45, Top = 6, Width = 290, Height = 170, BackColor = BackColor, Angle = startAngle };
            Controls.Add(_dial); Reg(_dial, null, () => "angle");
            Ui.Presets(this, 55, 180, _dial);

            GroupBox g1 = Ui.Group("Leaders", 10, 212, 360, 118); Controls.Add(g1);
            Label l1 = Ui.Lbl("Slope (degrees)", 12, 22, 150); g1.Controls.Add(l1);
            _angle = Ui.Num(170, 22, 80, 0, 180, 0, 1, (decimal)Math.Round(startAngle)); g1.Controls.Add(_angle);
            Reg(_angle, l1, () => "angle");
            Label l2 = Ui.Lbl("Shape", 12, 52, 150); g1.Controls.Add(l2);
            _style = Ui.Combo(170, 52, 150, (int)s.Style, "Parallel slope", "Equal landing", "Straight line"); g1.Controls.Add(_style);
            Reg(_style, l2, () => "style" + _style.SelectedIndex);
            Label l3 = Ui.Lbl("Landing length (mm)", 12, 82, 150); g1.Controls.Add(l3);
            _land = Ui.Num(170, 82, 80, 1, 200, 1, 1, (decimal)s.LandingMm); g1.Controls.Add(_land);
            Reg(_land, l3, () => "landing");

            GroupBox g2 = Ui.Group("Scope", 10, 336, 360, 84); Controls.Add(g2);
            _spatial = Ui.Chk("Include room / space / area tags", 12, 22, 300, s.IncludeSpatial); g2.Controls.Add(_spatial);
            Reg(_spatial, null, () => "spatial");
            _all = Ui.Chk("Use all tags in the view", 12, 50, 300, s.AllInView); g2.Controls.Add(_all);
            Reg(_all, null, () => "all");

            Button ok = Ui.Primary("Apply", 190, 436, 85);
            Button cancel = Ui.Secondary("Cancel", 285, 436, 85); cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(ok); Controls.Add(cancel); AcceptButton = ok; CancelButton = cancel;

            Action refresh = delegate
            {
                _land.Enabled = _style.SelectedIndex == 1;
                _angle.Enabled = _style.SelectedIndex == 0;
                _dial.Enabled = _style.SelectedIndex == 0;
            };
            _style.SelectedIndexChanged += delegate { refresh(); };
            refresh();
            _dial.AngleChanged += delegate { if (_sync) return; _sync = true; _angle.Value = (decimal)Math.Round(_dial.Angle); _sync = false; };
            _angle.ValueChanged += delegate { if (_sync) return; _sync = true; _dial.Angle = (double)_angle.Value; _sync = false; };
            ok.Click += delegate
            {
                Settings.AngleDeg = _dial.Angle;
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
            InitLayout(380, 440, "Align Tag Heads");
            GroupBox g = Ui.Group("Operation", 10, 12, 360, 170); Controls.Add(g);
            Label l1 = Ui.Lbl("Operation", 12, 26, 70); g.Controls.Add(l1);
            _mode = Ui.Combo(90, 26, 230, 0, "Align left", "Align centre (vertical axis)", "Align right", "Align top",
                             "Align middle (horizontal axis)", "Align bottom", "Distribute vertically", "Distribute horizontally");
            g.Controls.Add(_mode); Reg(_mode, l1, () => "head" + _mode.SelectedIndex);
            _keep = Ui.Chk("Keep leader shape (elbow moves with the head)", 12, 70, 310, true); g.Controls.Add(_keep);
            Reg(_keep, null, () => "keep");
            _notes = Ui.Chk("Include text notes", 12, 100, 310, true); g.Controls.Add(_notes);
            Reg(_notes, null, () => "notes");
            _spatial = Ui.Chk("Include room / space / area tags", 12, 130, 310, true); g.Controls.Add(_spatial);
            Reg(_spatial, null, () => "spatial");
            Button ok = Ui.Primary("Apply", 190, 398, 85);
            Button cancel = Ui.Secondary("Cancel", 285, 398, 85); cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(ok); Controls.Add(cancel); AcceptButton = ok; CancelButton = cancel;
            Help.ShowTopic(HelpCatalog.Get("head0"));
        }
    }
}
