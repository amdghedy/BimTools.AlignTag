using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BIMTools.TagAlign.Leaders
{
    /// <summary>Small "type a name" dialog.</summary>
    public static class Prompt
    {
        public static string Ask(IWin32Window owner, string title, string label, string initial)
        {
            using (Form f = new Form())
            {
                f.Text = title; f.FormBorderStyle = FormBorderStyle.FixedDialog; f.StartPosition = FormStartPosition.CenterParent;
                f.MaximizeBox = false; f.MinimizeBox = false; f.ShowInTaskbar = false; f.Font = new Font("Segoe UI", 9f);
                f.ClientSize = new Size(330, 110);
                Label l = new Label { Text = label, Left = 12, Top = 14, Width = 306 };
                TextBox t = new TextBox { Left = 12, Top = 38, Width = 306, Text = initial ?? "" };
                Button ok = Ui.Primary("Save", 148, 72, 80);
                Button cancel = Ui.Secondary("Cancel", 238, 72, 80); cancel.DialogResult = DialogResult.Cancel;
                f.Controls.Add(l); f.Controls.Add(t); f.Controls.Add(ok); f.Controls.Add(cancel);
                f.AcceptButton = ok; f.CancelButton = cancel;
                return f.ShowDialog(owner) == DialogResult.OK ? t.Text.Trim() : null;
            }
        }
    }

    /// <summary>"Resolve Overlaps" dialog.</summary>
    public class ResolveForm : OptionsFormBase
    {
        readonly NumericUpDown _pad, _max;
        readonly ComboBox _axis;
        readonly CheckBox _keep, _notes, _spatial, _all;
        public LeaderAlignSettings Settings { get; private set; }

        public ResolveForm(LeaderAlignSettings s)
        {
            Settings = s.Clone();
            InitLayout(380, 430, "Resolve Overlaps");

            GroupBox g1 = Ui.Group("Separation", 10, 12, 360, 120); Controls.Add(g1);
            Label l1 = Ui.Lbl("Minimum gap (mm)", 12, 24, 150); g1.Controls.Add(l1);
            _pad = Ui.Num(170, 24, 80, 0, 50, 1, 0.5m, (decimal)s.ClearPaddingMm); g1.Controls.Add(_pad); Reg(_pad, l1, () => "clearpad");
            Label l2 = Ui.Lbl("Never move farther than (mm)", 12, 54, 160); g1.Controls.Add(l2);
            _max = Ui.Num(170, 54, 80, 1, 500, 0, 1, (decimal)s.ClearMaxMoveMm); g1.Controls.Add(_max); Reg(_max, l2, () => "clearmax");
            Label l3 = Ui.Lbl("Push direction", 12, 84, 150); g1.Controls.Add(l3);
            _axis = Ui.Combo(170, 84, 150, (int)s.ClearDirection, "Any (shortest move)", "Vertical only", "Horizontal only"); g1.Controls.Add(_axis);
            Reg(_axis, l3, () => "clearaxis");

            GroupBox g2 = Ui.Group("Scope", 10, 140, 360, 148); Controls.Add(g2);
            _keep = Ui.Chk("Keep leader shape (elbow moves with the tag)", 12, 22, 320, s.ClearKeepShape); g2.Controls.Add(_keep); Reg(_keep, null, () => "keep");
            _notes = Ui.Chk("Include text notes", 12, 50, 320, s.IncludeNotes); g2.Controls.Add(_notes); Reg(_notes, null, () => "notes");
            _spatial = Ui.Chk("Include room / space / area tags", 12, 78, 320, s.IncludeSpatial); g2.Controls.Add(_spatial); Reg(_spatial, null, () => "spatial");
            _all = Ui.Chk("Use all tags in the view", 12, 106, 320, s.AllInView); g2.Controls.Add(_all); Reg(_all, null, () => "all");

            Button ok = Ui.Primary("Resolve", 190, 388, 85);
            Button cancel = Ui.Secondary("Cancel", 285, 388, 85); cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(ok); Controls.Add(cancel); AcceptButton = ok; CancelButton = cancel;
            ok.Click += delegate
            {
                Settings.ClearPaddingMm = (double)_pad.Value;
                Settings.ClearMaxMoveMm = (double)_max.Value;
                Settings.ClearDirection = (ClearAxis)_axis.SelectedIndex;
                Settings.ClearKeepShape = _keep.Checked;
                Settings.IncludeNotes = _notes.Checked;
                Settings.IncludeSpatial = _spatial.Checked;
                Settings.AllInView = _all.Checked;
            };
            Help.ShowTopic(HelpCatalog.Get("clear"));
        }
    }

    /// <summary>"Flip Tag Side" dialog.</summary>
    public class FlipForm : OptionsFormBase
    {
        readonly ComboBox _mode;
        readonly CheckBox _spatial;
        public FlipMode Mode { get { return (FlipMode)_mode.SelectedIndex; } }
        public bool IncludeSpatial { get { return _spatial.Checked; } }

        public FlipForm()
        {
            InitLayout(380, 300, "Flip Tag Side");
            GroupBox g = Ui.Group("Operation", 10, 12, 360, 120); Controls.Add(g);
            Label l1 = Ui.Lbl("Flip", 12, 28, 50); g.Controls.Add(l1);
            _mode = Ui.Combo(70, 28, 250, 0, "Left <-> right of the element", "Above <-> below the element",
                             "Both (point mirror)", "Whole column to the other side");
            g.Controls.Add(_mode); Reg(_mode, l1, () => "flip" + _mode.SelectedIndex);
            _spatial = Ui.Chk("Include room / space / area tags", 12, 74, 320, true); g.Controls.Add(_spatial); Reg(_spatial, null, () => "spatial");

            Button ok = Ui.Primary("Flip", 190, 258, 85);
            Button cancel = Ui.Secondary("Cancel", 285, 258, 85); cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(ok); Controls.Add(cancel); AcceptButton = ok; CancelButton = cancel;
            Help.ShowTopic(HelpCatalog.Get("flip0"));
        }
    }

    /// <summary>"Tag Audit" dialog.</summary>
    public class AuditForm : OptionsFormBase
    {
        readonly CheckedListBox _cats;
        readonly CheckBox _untagged, _dups, _orphans;
        public bool CheckUntagged { get { return _untagged.Checked; } }
        public bool CheckDuplicates { get { return _dups.Checked; } }
        public bool CheckOrphans { get { return _orphans.Checked; } }
        public List<string> SelectedCategories
        {
            get { return _cats.CheckedItems.Cast<AuditCategory>().Select(c => c.Name).ToList(); }
        }

        public AuditForm(List<AuditCategory> cats)
        {
            InitLayout(380, 520, "Tag Audit");

            GroupBox g1 = Ui.Group("What to check", 10, 12, 360, 118); Controls.Add(g1);
            _untagged = Ui.Chk("Elements without a tag", 12, 22, 310, true); g1.Controls.Add(_untagged); Reg(_untagged, null, () => "audituntagged");
            _dups = Ui.Chk("Elements tagged twice (duplicate tags)", 12, 52, 310, true); g1.Controls.Add(_dups); Reg(_dups, null, () => "auditdup");
            _orphans = Ui.Chk("Tags that lost their element (orphans)", 12, 82, 310, true); g1.Controls.Add(_orphans); Reg(_orphans, null, () => "auditorphan");

            GroupBox g2 = Ui.Group("Categories to check for missing tags", 10, 138, 360, 320); Controls.Add(g2);
            _cats = new CheckedListBox { Left = 12, Top = 22, Width = 334, Height = 252, CheckOnClick = true, IntegralHeight = false };
            foreach (AuditCategory c in cats) _cats.Items.Add(c, true);
            g2.Controls.Add(_cats);
            Button all = Ui.Secondary("All", 12, 282, 70); all.Height = 24;
            Button none = Ui.Secondary("None", 90, 282, 70); none.Height = 24;
            all.Click += delegate { for (int i = 0; i < _cats.Items.Count; i++) _cats.SetItemChecked(i, true); };
            none.Click += delegate { for (int i = 0; i < _cats.Items.Count; i++) _cats.SetItemChecked(i, false); };
            g2.Controls.Add(all); g2.Controls.Add(none);
            _cats.Enter += delegate { Help.ShowTopic(HelpCatalog.Get("auditcats")); };
            _cats.MouseEnter += delegate { Help.ShowTopic(HelpCatalog.Get("auditcats")); };
            _untagged.CheckedChanged += delegate { _cats.Enabled = all.Enabled = none.Enabled = _untagged.Checked; };

            Button ok = Ui.Primary("Check view", 170, 478, 100);
            Button cancel = Ui.Secondary("Cancel", 285, 478, 85); cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(ok); Controls.Add(cancel); AcceptButton = ok; CancelButton = cancel;
            Help.ShowTopic(HelpCatalog.Get("audit"));
        }
    }
}
