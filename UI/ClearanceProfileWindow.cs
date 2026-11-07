using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AlignTool.Services;

namespace AlignTool.UI
{
    public sealed class ClearanceProfileWindow : Window
    {
        private readonly string _projectTitle;
        private readonly List<ClearanceProfile> _profiles;
        private readonly DataGrid _grid = new DataGrid();
        private readonly TextBox _name = new TextBox { Width = 210, Margin = new Thickness(4) };
        private readonly ComboBox _type = new ComboBox { Width = 190, Margin = new Thickness(4) };
        private readonly TextBox _left = NumberBox("600");
        private readonly TextBox _right = NumberBox("600");
        private readonly TextBox _below = NumberBox("0");
        private readonly TextBox _above = NumberBox("600");
        private readonly TextBox _discipline = new TextBox { Width = 110, Margin = new Thickness(4) };
        private readonly TextBox _notes = new TextBox { Width = 380, Margin = new Thickness(4) };
        private readonly TextBlock _status = new TextBlock { Margin = new Thickness(4), Foreground = Brushes.DimGray };

        public ClearanceProfileWindow(string projectTitle)
        {
            _projectTitle = projectTitle ?? "UntitledProject";
            _profiles = ClearanceProfileService.Load(_projectTitle);
            Title = "ALIGN - Project Clearance Profiles";
            Width = 1050;
            Height = 650;
            MinWidth = 850;
            MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(245, 247, 251));

            var root = new DockPanel { Margin = new Thickness(16) };
            var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            heading.Children.Add(new TextBlock { Text = "PROJECT MAINTENANCE & ACCESS PROFILES", FontSize = 19, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(32, 45, 64)) });
            heading.Children.Add(new TextBlock { Text = "Project: " + _projectTitle + "  |  Reusable clearance envelopes  |  Stored locally per project", Margin = new Thickness(0, 4, 0, 0), Foreground = Brushes.DimGray });
            DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);

            var editor = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            AddField(editor, "Profile name", _name);
            _type.ItemsSource = ClearanceProfileService.ZoneTypes; _type.SelectedIndex = 0;
            AddField(editor, "Zone type", _type);
            AddField(editor, "Left (mm)", _left); AddField(editor, "Right (mm)", _right);
            AddField(editor, "Below (mm)", _below); AddField(editor, "Above (mm)", _above);
            AddField(editor, "Discipline", _discipline); AddField(editor, "Notes", _notes);
            var add = new Button { Content = "Add / Update", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(4) };
            add.Click += (s, e) => AddOrUpdate(); editor.Children.Add(add);
            var remove = new Button { Content = "Remove Selected", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(4) };
            remove.Click += (s, e) => RemoveSelected(); editor.Children.Add(remove);
            DockPanel.SetDock(editor, Dock.Top); root.Children.Add(editor);

            _grid.AutoGenerateColumns = false; _grid.IsReadOnly = true; _grid.CanUserAddRows = false;
            _grid.SelectionMode = DataGridSelectionMode.Single; _grid.Background = Brushes.White;
            _grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new System.Windows.Data.Binding("Name"), Width = 190 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Zone Type", Binding = new System.Windows.Data.Binding("ZoneType"), Width = 160 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Left", Binding = new System.Windows.Data.Binding("LeftMm"), Width = 65 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Right", Binding = new System.Windows.Data.Binding("RightMm"), Width = 65 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Below", Binding = new System.Windows.Data.Binding("BelowMm"), Width = 65 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Above", Binding = new System.Windows.Data.Binding("AboveMm"), Width = 65 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Discipline", Binding = new System.Windows.Data.Binding("Discipline"), Width = 100 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Notes", Binding = new System.Windows.Data.Binding("Notes"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _grid.SelectionChanged += (s, e) => LoadSelected();
            DockPanel.SetDock(_grid, Dock.Top); root.Children.Add(_grid);

            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var save = new Button { Content = "Save Profiles", Width = 130, Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(4) };
            save.Click += (s, e) => SaveProfiles(); footer.Children.Add(save);
            var close = new Button { Content = "Close", Width = 90, Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(4) };
            close.Click += (s, e) => Close(); footer.Children.Add(close);
            DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
            DockPanel.SetDock(_status, Dock.Bottom); root.Children.Add(_status);
            Content = root;
            RefreshGrid();
        }

        private static TextBox NumberBox(string value) { return new TextBox { Text = value, Width = 72, Margin = new Thickness(4) }; }
        private static void AddField(Panel panel, string label, Control control)
        {
            var group = new StackPanel { Margin = new Thickness(2) };
            group.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = Brushes.DimGray });
            group.Children.Add(control); panel.Children.Add(group);
        }

        private void AddOrUpdate()
        {
            try
            {
                var profile = new ClearanceProfile
                {
                    Name = _name.Text.Trim(), ZoneType = _type.SelectedItem as string,
                    LeftMm = Parse(_left), RightMm = Parse(_right), BelowMm = Parse(_below), AboveMm = Parse(_above),
                    Discipline = _discipline.Text.Trim(), Notes = _notes.Text.Trim()
                };
                string error;
                if (!ClearanceProfileService.Validate(profile, out error)) throw new InvalidOperationException(error);
                var selected = _grid.SelectedItem as ClearanceProfile;
                if (selected != null)
                {
                    int index = _profiles.IndexOf(selected);
                    _profiles[index] = profile;
                }
                else _profiles.Add(profile);
                RefreshGrid(); _status.Text = "Profile ready. Select Save Profiles to persist changes.";
                _name.Clear(); _notes.Clear();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Clearance profile", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void RemoveSelected()
        {
            var selected = _grid.SelectedItem as ClearanceProfile;
            if (selected == null) return;
            _profiles.Remove(selected); RefreshGrid();
            _status.Text = "Profile removed from the working list. Save to persist the change.";
        }

        private void LoadSelected()
        {
            var p = _grid.SelectedItem as ClearanceProfile;
            if (p == null) return;
            _name.Text = p.Name; _type.SelectedItem = p.ZoneType;
            _left.Text = p.LeftMm.ToString(CultureInfo.InvariantCulture);
            _right.Text = p.RightMm.ToString(CultureInfo.InvariantCulture);
            _below.Text = p.BelowMm.ToString(CultureInfo.InvariantCulture);
            _above.Text = p.AboveMm.ToString(CultureInfo.InvariantCulture);
            _discipline.Text = p.Discipline ?? ""; _notes.Text = p.Notes ?? "";
        }

        private void SaveProfiles()
        {
            try
            {
                ClearanceProfileService.Save(_projectTitle, _profiles);
                _status.Text = "Saved " + _profiles.Count + " clearance profile(s) for this project.";
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Save clearance profiles", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void RefreshGrid()
        {
            _grid.ItemsSource = null; _grid.ItemsSource = _profiles.ToList();
        }

        private static double Parse(TextBox box)
        {
            double value;
            if (!double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw new ArgumentException("Clearance dimensions must be valid numbers in millimetres.");
            return value;
        }
    }
}
