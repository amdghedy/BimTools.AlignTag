using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using AlignTool.Services;
using Microsoft.Win32;

namespace AlignTool.UI
{
    /// <summary>Interactive candidate review. This window never edits Revit elements.</summary>
    public sealed class CsdOptimizerWindow : Window
    {
        private readonly RevitCsdExtractionService.ExtractionResult _extraction;
        private readonly KnowledgeBasedCsdEngine.SolveResult _solve;
        private readonly KnowledgeBasedCsdEngine.SectionConstraints _constraints;
        private readonly DataGrid _candidateGrid;
        private readonly DataGrid _placementGrid;
        private readonly Canvas _sectionCanvas;
        private readonly Canvas _actualSectionCanvas;
        private readonly ComboBox _grade;
        private readonly TextBox _comment;
        private readonly TextBlock _status;
        private readonly List<KnowledgeBasedCsdEngine.EngineerAssessment> _assessments =
            new List<KnowledgeBasedCsdEngine.EngineerAssessment>();

        public CsdOptimizerWindow(RevitCsdExtractionService.ExtractionResult extraction,
            KnowledgeBasedCsdEngine.SolveResult solve,
            KnowledgeBasedCsdEngine.SectionConstraints constraints)
        {
            _extraction = extraction;
            _solve = solve;
            _constraints = constraints;
            Title = "BIM TOOLS ALIGN - Intelligent CSD Optimizer";
            Width = 1120; Height = 720; MinWidth = 900; MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = System.Windows.Media.Brushes.White;

            var root = new DockPanel { Margin = new Thickness(14) };
            Content = root;
            var header = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            header.Children.Add(new TextBlock
            {
                Text = "INTELLIGENT MEP COMPOSITE SECTION",
                FontSize = 19, FontWeight = FontWeights.SemiBold,
                Foreground = System.Windows.Media.Brushes.DarkSlateBlue
            });
            header.Children.Add(new TextBlock
            {
                Text = extraction.SourceDescription + "  |  " + extraction.Items.Count +
                    " MEP elements  |  " + extraction.ConnectorCount + " connectors  |  " +
                    extraction.ElementsWithoutConnectors + " without connectors  |  " +
                    extraction.SkippedElements + " skipped  |  " +
                    extraction.SolidIntersections + " solid sections / " +
                    extraction.SolidIntersectionFailures + " geometry failures / " +
                    extraction.InsulationLiningElements + " insulation/lining objects",
                Margin = new Thickness(0, 4, 0, 0), Foreground = System.Windows.Media.Brushes.DimGray
            });
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            var footer = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 10, 0, 0) };
            _status = new TextBlock { Text = "Select a candidate to inspect its proposed section positions.", Margin = new Thickness(0, 0, 0, 8) };
            footer.Children.Add(_status);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            _grade = new ComboBox { Width = 100, Margin = new Thickness(0, 0, 8, 0), ItemsSource = new[] { "1 - Poor", "2 - Fair", "3 - Acceptable", "4 - Good", "5 - Excellent" }, SelectedIndex = 2 };
            _comment = new TextBox { Width = 260, Height = 26, Margin = new Thickness(0, 0, 8, 0), ToolTip = "Engineering review comment" };
            var assess = new Button { Content = "Record engineer grade", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 8, 0) };
            assess.Click += RecordAssessment;
            var export = new Button { Content = "Export candidate CSV", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 8, 0) };
            export.Click += ExportCsv;
            var close = new Button { Content = "Close", Padding = new Thickness(18, 5, 18, 5) };
            close.Click += (s, e) => Close();
            actions.Children.Add(_grade); actions.Children.Add(_comment);
            actions.Children.Add(assess); actions.Children.Add(export); actions.Children.Add(close);
            footer.Children.Add(actions);
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);

            var split = new Grid();
            split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
            split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
            root.Children.Add(split);

            var left = new DockPanel { Margin = new Thickness(0, 0, 8, 0) };
            left.Children.Add(new TextBlock { Text = "RANKED ARRANGEMENT OPTIONS", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
            _candidateGrid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, SelectionMode = DataGridSelectionMode.Single, HeadersVisibility = DataGridHeadersVisibility.Column };
            AddColumn(_candidateGrid, "Option", "Id", 90);
            AddColumn(_candidateGrid, "Strategy", "Strategy", 150);
            AddColumn(_candidateGrid, "Cost", "CostText", 90);
            AddColumn(_candidateGrid, "Clashes", "Clashes", 65);
            AddColumn(_candidateGrid, "Clearance", "ClearanceViolations", 75);
            AddColumn(_candidateGrid, "Rules", "RuleViolations", 55);
            AddColumn(_candidateGrid, "Solid clashes", "SolidProfileClashes", 75);
            AddColumn(_candidateGrid, "Solid boundary", "GeometryBoundaryViolations", 75);
            AddColumn(_candidateGrid, "Profiles", "GeometryCoverage", 70);
            AddColumn(_candidateGrid, "Geometry check", "GeometryStatus", 160);
            _candidateGrid.SelectionChanged += CandidateChanged;
            IList<CsdSectionGeometryValidator.CandidateCheck> geometryChecks =
                CsdSectionGeometryValidator.Validate(solve.Candidates, extraction, constraints);
            _candidateGrid.ItemsSource = solve.Candidates.Select(candidate =>
                new CandidateRow(candidate, geometryChecks.FirstOrDefault(x =>
                    string.Equals(x.CandidateId, candidate.Id, StringComparison.OrdinalIgnoreCase)))).ToList();
            left.Children.Add(_candidateGrid);
            Grid.SetColumn(left, 0); split.Children.Add(left);

            var right = new DockPanel { Margin = new Thickness(8, 0, 0, 0) };
            right.Children.Add(new TextBlock { Text = "ACTUAL MODEL SECTION - ACTIVE VIEW PLANE", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
            _actualSectionCanvas = new Canvas { Width = 520, Height = 190, Background = Brushes.WhiteSmoke, ClipToBounds = true };
            var actualBorder = new Border { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(1), Child = _actualSectionCanvas, Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(actualBorder, Dock.Top);
            right.Children.Add(actualBorder);
            right.Children.Add(new TextBlock { Text = "PROPOSED ARRANGEMENT - SOLVER PREVIEW", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
            _sectionCanvas = new Canvas { Width = 520, Height = 190, Background = Brushes.WhiteSmoke, ClipToBounds = true };
            var previewBorder = new Border { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(1), Child = _sectionCanvas, Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(previewBorder, Dock.Top);
            right.Children.Add(previewBorder);
            var placementHeader = new TextBlock { Text = "PROPOSED SERVICE POSITIONS (mm)", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) };
            DockPanel.SetDock(placementHeader, Dock.Top);
            right.Children.Add(placementHeader);
            _placementGrid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, HeadersVisibility = DataGridHeadersVisibility.Column };
            AddColumn(_placementGrid, "Element", "ElementName", 180);
            AddColumn(_placementGrid, "System", "SystemName", 110);
            AddColumn(_placementGrid, "Discipline", "Discipline", 85);
            AddColumn(_placementGrid, "Width", "WidthText", 65);
            AddColumn(_placementGrid, "Height", "HeightText", 65);
            AddColumn(_placementGrid, "X", "XText", 65);
            AddColumn(_placementGrid, "Y", "YText", 65);
            right.Children.Add(_placementGrid);
            Grid.SetColumn(right, 1); split.Children.Add(right);

            if (_candidateGrid.Items.Count > 0) _candidateGrid.SelectedIndex = 0;
        }

        private static void AddColumn(DataGrid grid, string title, string path, double width)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = title, Binding = new Binding(path),
                Width = new DataGridLength(width)
            });
        }

        private void CandidateChanged(object sender, SelectionChangedEventArgs e)
        {
            CandidateRow row = _candidateGrid.SelectedItem as CandidateRow;
            if (row == null) return;
            var byId = _extraction.Items.ToDictionary(x => x.Id, x => x);
            DrawActualSectionProjection();
            DrawSectionPreview(row.Candidate);
            _placementGrid.ItemsSource = row.Candidate.Placements.Select(p =>
            {
                KnowledgeBasedCsdEngine.ServiceItem item;
                byId.TryGetValue(p.ServiceId, out item);
                return new PlacementRow
                {
                    ElementName = item == null ? p.ServiceId : item.Name,
                    SystemName = item == null ? "" : item.SystemName,
                    Discipline = item == null ? "" : item.Discipline,
                    WidthText = p.WidthMm.ToString("F0"),
                    HeightText = p.HeightMm.ToString("F0"),
                    XText = p.Xmm.ToString("F0"),
                    YText = p.Ymm.ToString("F0")
                };
            }).ToList();
            _status.Text = row.Candidate.Id + " - " + row.Candidate.Strategy +
                " | Fitness: " + row.Candidate.Fitness.ToString("P2") +
                " | Boundary: " + row.Candidate.BoundaryViolations +
                " | Clashes: " + row.Candidate.Clashes +
                " | Rule violations: " + row.Candidate.RuleViolations +
                " | NDCG@5: " + KnowledgeBasedCsdEngine.CalculateNdcg(
                    _solve.Candidates, _assessments, 5).ToString("F3");
        }


        private void DrawActualSectionProjection()
        {
            _actualSectionCanvas.Children.Clear();
            IList<RevitCsdExtractionService.SectionProjectionItem> items = _extraction.SectionItems;
            if (items == null || items.Count == 0)
            {
                _actualSectionCanvas.Children.Add(new TextBlock
                {
                    Text = "No solid section contours were returned. Check the geometry failure count and confirm that the active view plane crosses the selected MEP solids.",
                    FontSize = 11, Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap,
                    Width = _actualSectionCanvas.Width - 24
                });
                return;
            }

            double minX = items.Min(x => x.CenterXmm - x.WidthMm * 0.5);
            double maxX = items.Max(x => x.CenterXmm + x.WidthMm * 0.5);
            double minY = items.Min(x => x.CenterYmm - x.HeightMm * 0.5);
            double maxY = items.Max(x => x.CenterYmm + x.HeightMm * 0.5);
            double spanX = Math.Max(100, maxX - minX);
            double spanY = Math.Max(100, maxY - minY);
            const double margin = 12;
            double scale = Math.Min((_actualSectionCanvas.Width - margin * 2) / spanX,
                (_actualSectionCanvas.Height - margin * 2 - 15) / spanY);
            double usedWidth = spanX * scale;
            double usedHeight = spanY * scale;
            double left = (_actualSectionCanvas.Width - usedWidth) * 0.5;
            double top = (_actualSectionCanvas.Height - usedHeight - 15) * 0.5;

            foreach (RevitCsdExtractionService.SectionProjectionItem item in items)
            {
                if (item.Contours == null) continue;
                Brush disciplineBrush = GetDisciplineBrush(item.Discipline);
                foreach (RevitCsdExtractionService.SectionContour contour in item.Contours)
                {
                    if (contour.Points == null || contour.Points.Count < 3) continue;
                    var points = new PointCollection();
                    foreach (RevitCsdExtractionService.SectionPoint point in contour.Points)
                    {
                        double px = left + (point.Xmm - minX) * scale;
                        double py = top + (maxY - point.Ymm) * scale;
                        points.Add(new Point(px, py));
                    }
                    var polygon = new Polygon
                    {
                        Points = points,
                        Fill = contour.IsHole ? Brushes.WhiteSmoke : Brushes.Transparent,
                        Stroke = contour.IsHole ? Brushes.DimGray : disciplineBrush,
                        StrokeThickness = contour.IsHole ? 0.9 : 1.5,
                        ToolTip = item.Name + Environment.NewLine + item.Discipline +
                            Environment.NewLine + item.WidthMm.ToString("F1") + " x " +
                            item.HeightMm.ToString("F1") + " mm" + Environment.NewLine +
                            item.ProjectionMethod
                    };
                    _actualSectionCanvas.Children.Add(polygon);
                }

                if (item.WidthMm * scale >= 42 && item.HeightMm * scale >= 15)
                {
                    var label = new TextBlock
                    {
                        Text = ShortLabel(item.Name), FontSize = 8, Foreground = Brushes.Black,
                        Width = Math.Max(24, item.WidthMm * scale - 4),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(label, left + (item.CenterXmm - minX) * scale - item.WidthMm * scale * 0.5 + 2);
                    Canvas.SetTop(label, top + (maxY - item.CenterYmm) * scale - 6);
                    _actualSectionCanvas.Children.Add(label);
                }
            }

            var note = new TextBlock
            {
                Text = items.Count + " solid section profiles | Boolean slab 2 mm | geometry failures: " +
                    _extraction.SolidIntersectionFailures,
                FontSize = 8, Foreground = Brushes.DimGray
            };
            Canvas.SetLeft(note, 8);
            Canvas.SetTop(note, _actualSectionCanvas.Height - 13);
            _actualSectionCanvas.Children.Add(note);
        }

        private void DrawSectionPreview(KnowledgeBasedCsdEngine.Candidate candidate)
        {
            _sectionCanvas.Children.Clear();
            const double margin = 12;
            double availableWidth = _sectionCanvas.Width - margin * 2;
            double availableHeight = _sectionCanvas.Height - margin * 2;
            double scale = Math.Min(availableWidth / Math.Max(1, _constraints.WidthMm),
                availableHeight / Math.Max(1, _constraints.HeightMm));
            double sectionWidth = _constraints.WidthMm * scale;
            double sectionHeight = _constraints.HeightMm * scale;
            double left = (_sectionCanvas.Width - sectionWidth) / 2;
            double top = (_sectionCanvas.Height - sectionHeight) / 2;

            var boundary = new Rectangle
            {
                Width = sectionWidth, Height = sectionHeight,
                Stroke = Brushes.DimGray, StrokeThickness = 1.5,
                Fill = Brushes.White
            };
            Canvas.SetLeft(boundary, left);
            Canvas.SetTop(boundary, top);
            _sectionCanvas.Children.Add(boundary);

            foreach (var placement in candidate.Placements)
            {
                var item = _extraction.Items.FirstOrDefault(x =>
                    string.Equals(x.Id, placement.ServiceId, StringComparison.OrdinalIgnoreCase));
                string discipline = item == null ? "" : item.Discipline;
                Brush fill = GetDisciplineBrush(discipline);
                double w = Math.Max(3, placement.WidthMm * scale);
                double h = Math.Max(3, placement.HeightMm * scale);
                double x = left + placement.Xmm * scale;
                double y = top + sectionHeight - (placement.Ymm + placement.HeightMm) * scale;

                var shape = new Rectangle
                {
                    Width = w, Height = h, Fill = fill,
                    Stroke = Brushes.Black, StrokeThickness = 0.7,
                    ToolTip = (item == null ? placement.ServiceId : item.Name) +
                        Environment.NewLine + discipline +
                        Environment.NewLine + placement.WidthMm.ToString("F0") + " x " +
                        placement.HeightMm.ToString("F0") + " mm"
                };
                Canvas.SetLeft(shape, x);
                Canvas.SetTop(shape, y);
                _sectionCanvas.Children.Add(shape);

                if (w >= 32 && h >= 13)
                {
                    var label = new TextBlock
                    {
                        Text = item == null ? placement.ServiceId : ShortLabel(item.Name),
                        FontSize = 8, Foreground = Brushes.Black,
                        Width = Math.Max(20, w - 4),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(label, x + 2);
                    Canvas.SetTop(label, y + Math.Max(0, (h - 12) / 2));
                    _sectionCanvas.Children.Add(label);
                }
            }

            AddLegend("HVAC", Brushes.LightSkyBlue, 12);
            AddLegend("Plumbing", Brushes.LightGreen, 95);
            AddLegend("Electrical", Brushes.Khaki, 205);
            var note = new TextBlock
            {
                Text = "Schematic solver section - not a true-to-scale route projection",
                FontSize = 8, Foreground = Brushes.DimGray
            };
            Canvas.SetLeft(note, 12);
            Canvas.SetTop(note, _sectionCanvas.Height - 13);
            _sectionCanvas.Children.Add(note);
        }

        private void AddLegend(string text, Brush brush, double x)
        {
            var swatch = new Rectangle { Width = 9, Height = 9, Fill = brush, Stroke = Brushes.Gray, StrokeThickness = 0.5 };
            Canvas.SetLeft(swatch, x);
            Canvas.SetTop(swatch, 4);
            _sectionCanvas.Children.Add(swatch);
            var label = new TextBlock { Text = text, FontSize = 9, Foreground = Brushes.Black };
            Canvas.SetLeft(label, x + 12);
            Canvas.SetTop(label, 2);
            _sectionCanvas.Children.Add(label);
        }

        private static Brush GetDisciplineBrush(string discipline)
        {
            if ((discipline ?? "").IndexOf("plumb", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (discipline ?? "").IndexOf("pipe", StringComparison.OrdinalIgnoreCase) >= 0)
                return Brushes.LightGreen;
            if ((discipline ?? "").IndexOf("elect", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (discipline ?? "").IndexOf("cable", StringComparison.OrdinalIgnoreCase) >= 0)
                return Brushes.Khaki;
            return Brushes.LightSkyBlue;
        }

        private static string ShortLabel(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Length <= 18 ? value : value.Substring(0, 18);
        }

        private void RecordAssessment(object sender, RoutedEventArgs e)
        {
            CandidateRow row = _candidateGrid.SelectedItem as CandidateRow;
            if (row == null || _grade.SelectedIndex < 0) return;
            _assessments.RemoveAll(x => x.CandidateId == row.Candidate.Id);
            _assessments.Add(new KnowledgeBasedCsdEngine.EngineerAssessment
            {
                CandidateId = row.Candidate.Id,
                Grade = _grade.SelectedIndex + 1,
                Comment = _comment.Text,
                AssessedUtc = DateTime.UtcNow
            });
            _status.Text = "Grade recorded for " + row.Candidate.Id + ". NDCG@5: " +
                KnowledgeBasedCsdEngine.CalculateNdcg(_solve.Candidates, _assessments, 5).ToString("F3") +
                ". Assessment remains in this session; no model changes were made.";
        }

        private void ExportCsv(object sender, RoutedEventArgs e)
        {
            CandidateRow row = _candidateGrid.SelectedItem as CandidateRow;
            if (row == null) return;
            var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = row.Candidate.Id + "_CSD.csv" };
            if (dialog.ShowDialog(this) != true) return;
            var byId = _extraction.Items.ToDictionary(x => x.Id, x => x);
            using (var writer = new StreamWriter(dialog.FileName, false, System.Text.Encoding.UTF8))
            {
                writer.WriteLine("ElementId,Element,System,Discipline,WidthMm,HeightMm,Xmm,Ymm");
                foreach (var p in row.Candidate.Placements)
                {
                    KnowledgeBasedCsdEngine.ServiceItem item;
                    byId.TryGetValue(p.ServiceId, out item);
                    writer.WriteLine(string.Join(",", new[]
                    {
                        Csv(p.ServiceId), Csv(item == null ? "" : item.Name),
                        Csv(item == null ? "" : item.SystemName), Csv(item == null ? "" : item.Discipline),
                        p.WidthMm.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                        p.HeightMm.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                        p.Xmm.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                        p.Ymm.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                    }));
                }
            }
            _status.Text = "Candidate exported. This is an analysis report, not a construction-approved layout.";
        }

        private static string Csv(string value)
        {
            value = value ?? "";
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private sealed class CandidateRow
        {
            public KnowledgeBasedCsdEngine.Candidate Candidate { get; private set; }
            public string Id { get { return Candidate.Id; } }
            public string Strategy { get { return Candidate.Strategy; } }
            public string CostText { get { return Candidate.Cost.ToString("F1"); } }
            public int Clashes { get { return Candidate.Clashes; } }
            public int ClearanceViolations { get { return Candidate.ClearanceViolations; } }
            public int RuleViolations { get { return Candidate.RuleViolations; } }
            private readonly CsdSectionGeometryValidator.CandidateCheck _geometry;
            public int SolidProfileClashes { get { return _geometry == null ? 0 : _geometry.SolidProfileClashes; } }
            public int GeometryBoundaryViolations { get { return _geometry == null ? 0 : _geometry.GeometryBoundaryViolations; } }
            public string GeometryCoverage { get { return _geometry == null ? "N/A" : _geometry.GeometryItemsMatched + "/" + (_geometry.GeometryItemsMatched + _geometry.GeometryItemsMissing); } }
            public string GeometryStatus { get { return _geometry == null ? "Not checked" : _geometry.Status; } }
            public CandidateRow(KnowledgeBasedCsdEngine.Candidate candidate,
                CsdSectionGeometryValidator.CandidateCheck geometry)
            {
                Candidate = candidate;
                _geometry = geometry;
            }
        }

        private sealed class PlacementRow
        {
            public string ElementName { get; set; }
            public string SystemName { get; set; }
            public string Discipline { get; set; }
            public string WidthText { get; set; }
            public string HeightText { get; set; }
            public string XText { get; set; }
            public string YText { get; set; }
        }
    }
}