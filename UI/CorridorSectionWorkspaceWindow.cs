using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using Autodesk.Revit.DB;
using AlignTool.Services;

namespace AlignTool.UI
{
    /// <summary>Read-only multi-station review of native Revit solid cross-sections.</summary>
    public sealed class CorridorSectionWorkspaceWindow : Window
    {
        private readonly Revit3DCoordinationExtractionService.ExtractionResult _source;
        private readonly Document _document;
        private readonly View _view;
        private readonly TextBox _start = new TextBox { Text = "0", Width = 78, Margin = new Thickness(4, 0, 12, 0) };
        private readonly TextBox _spacing = new TextBox { Text = "3000", Width = 78, Margin = new Thickness(4, 0, 12, 0) };
        private readonly TextBox _count = new TextBox { Text = "5", Width = 55, Margin = new Thickness(4, 0, 12, 0) };
        private readonly TextBox _thickness = new TextBox { Text = "2", Width = 55, Margin = new Thickness(4, 0, 12, 0) };
        private readonly DataGrid _stations = new DataGrid();
        private readonly DataGrid _items = new DataGrid();
        private readonly Canvas _preview = new Canvas();
        private readonly TextBlock _previewInfo = new TextBlock { Margin = new Thickness(0, 0, 0, 8), Foreground = Brushes.DimGray };
        private readonly TextBlock _status = new TextBlock { Margin = new Thickness(0, 8, 0, 0), Foreground = Brushes.DimGray };
        private CorridorSectionWorkspaceService.WorkspaceResult _result;
        private CorridorSectionWorkspaceService.StationResult _selectedStation;

        public CorridorSectionWorkspaceWindow(
            Document document,
            View view,
            Revit3DCoordinationExtractionService.ExtractionResult source)
        {
            _document = document ?? throw new ArgumentNullException("document");
            _view = view ?? throw new ArgumentNullException("view");
            _source = source ?? throw new ArgumentNullException("source");

            Title = "ALIGN | Multi-Section Corridor Workspace";
            Width = 1420;
            Height = 850;
            MinWidth = 1050;
            MinHeight = 650;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(245, 247, 251));

            var root = new DockPanel { Margin = new Thickness(16) };
            var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            header.Children.Add(new TextBlock
            {
                Text = "MULTI-SECTION CORRIDOR WORKSPACE",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(32, 45, 64))
            });
            header.Children.Add(new TextBlock
            {
                Text = "Measured Revit solids • host and transformed linked models • insulation and lining • read-only review",
                Margin = new Thickness(0, 4, 0, 0),
                Foreground = Brushes.DimGray
            });
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            var controls = new WrapPanel { Margin = new Thickness(0, 0, 0, 10), VerticalAlignment = VerticalAlignment.Center };
            AddControl(controls, "Start offset (mm)", _start);
            AddControl(controls, "Station spacing (mm)", _spacing);
            AddControl(controls, "Station count", _count);
            AddControl(controls, "Slab thickness (mm)", _thickness);
            var generate = new Button
            {
                Content = "Generate Sections",
                Padding = new Thickness(16, 7, 16, 7),
                Margin = new Thickness(8, 0, 0, 0),
                FontWeight = FontWeights.SemiBold
            };
            generate.Click += (s, e) => Generate();
            controls.Children.Add(generate);
            DockPanel.SetDock(controls, Dock.Top);
            root.Children.Add(controls);

            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            var close = new Button { Content = "Close", Width = 100, Padding = new Thickness(12, 7, 12, 7) };
            close.Click += (s, e) => Close();
            footer.Children.Add(close);
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);

            var body = new System.Windows.Controls.Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(245) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(430) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var stationPanel = new DockPanel { Margin = new Thickness(0, 0, 10, 0) };
            var stationTitle = new TextBlock
            {
                Text = "CORRIDOR STATIONS",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(stationTitle, Dock.Top);
            stationPanel.Children.Add(stationTitle);
            ConfigureGrid(_stations);
            _stations.Columns.Add(Column("Station", "Name", 65));
            _stations.Columns.Add(Column("Offset mm", "OffsetMm", 82));
            _stations.Columns.Add(Column("Elements", "ElementCount", 72));
            _stations.SelectionChanged += (s, e) => SelectStation(_stations.SelectedItem as CorridorSectionWorkspaceService.StationResult);
            stationPanel.Children.Add(_stations);
            System.Windows.Controls.Grid.SetColumn(stationPanel, 0);
            body.Children.Add(stationPanel);

            var itemPanel = new DockPanel { Margin = new Thickness(0, 0, 10, 0) };
            var itemTitle = new TextBlock
            {
                Text = "MEASURED SOLID SECTIONS",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(itemTitle, Dock.Top);
            itemPanel.Children.Add(itemTitle);
            ConfigureGrid(_items);
            itemPanel.Children.Add(_items);
            Grid.SetColumn(itemPanel, 1);
            body.Children.Add(itemPanel);

            var previewPanel = new DockPanel();
            DockPanel.SetDock(_previewInfo, Dock.Top);
            previewPanel.Children.Add(_previewInfo);
            _preview.Background = Brushes.White;
            _preview.Margin = new Thickness(0, 0, 0, 0);
            _preview.SizeChanged += (s, e) => DrawPreview();
            previewPanel.Children.Add(_preview);
            Grid.SetColumn(previewPanel, 2);
            body.Children.Add(previewPanel);

            DockPanel.SetDock(_status, Dock.Bottom);
            root.Children.Add(_status);
            root.Children.Add(body);
            Content = root;

            Generate();
        }

        private static void AddControl(System.Windows.Controls.Panel panel, string label, TextBox input)
        {
            var group = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 4, 4) };
            group.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            group.Children.Add(input);
            panel.Children.Add(group);
        }

        private static void ConfigureGrid(DataGrid grid)
        {
            grid.AutoGenerateColumns = false;
            grid.IsReadOnly = true;
            grid.CanUserAddRows = false;
            grid.CanUserDeleteRows = false;
            grid.SelectionMode = DataGridSelectionMode.Single;
            grid.HeadersVisibility = DataGridHeadersVisibility.Column;
            grid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
            grid.Background = Brushes.White;
        }

        private static DataGridTextColumn Column(string title, string path, double width)
        {
            return new DataGridTextColumn
            {
                Header = title,
                Binding = new Binding(path) { StringFormat = "F1" },
                Width = new DataGridLength(width)
            };
        }

        private void Generate()
        {
            try
            {
                double start = Parse(_start, "Start offset");
                double spacing = Parse(_spacing, "Station spacing");
                int count = int.Parse(_count.Text, CultureInfo.InvariantCulture);
                double thickness = Parse(_thickness, "Slab thickness");
                var request = new CorridorSectionWorkspaceService.StationRequest
                {
                    StartOffsetMm = start,
                    SpacingMm = spacing,
                    Count = count,
                    SlabThicknessMm = thickness
                };
                _status.Text = "Intersecting measured Revit solids at corridor stations...";
                _result = CorridorSectionWorkspaceService.Extract(_document, _view, _source, request);
                _stations.ItemsSource = _result.Stations;
                _items.ItemsSource = null;
                _selectedStation = null;
                if (_result.Stations.Count > 0) _stations.SelectedIndex = 0;
                _status.Text = "Stations: " + _result.StationsRequested +
                    " | Stations with measured geometry: " + _result.StationsWithGeometry +
                    " | Solid intersections: " + _result.SolidIntersections +
                    " | Solids tested: " + _result.SolidsTested +
                    " | Boolean failures: " + _result.BooleanFailures +
                    " | Source: " + _result.SourceDescription;
                if (_selectedStation != null) SelectStation(_selectedStation);
                else
                {
                    _previewInfo.Text = _result.PlaneDescription +
                        "  |  Slab thickness: " + _result.SlabThicknessMm.ToString("F1") + " mm";
                    DrawPreview();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Corridor section extraction", MessageBoxButton.OK, MessageBoxImage.Warning);
                _status.Text = "Section generation did not complete.";
            }
        }

        private static double Parse(TextBox input, string label)
        {
            double value;
            if (!double.TryParse(input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentException(label + " must be a valid number.");
            return value;
        }

        private void SelectStation(CorridorSectionWorkspaceService.StationResult station)
        {
            _selectedStation = station;
            if (station == null)
            {
                _items.ItemsSource = null;
                DrawPreview();
                return;
            }
            _items.ItemsSource = station.Items.Select(x => new
            {
                x.ElementName,
                x.Category,
                x.Discipline,
                x.SystemName,
                x.ModelName,
                x.GeometryKind,
                Width_mm = Math.Round(x.WidthMm, 1),
                Height_mm = Math.Round(x.HeightMm, 1),
                Area_mm2 = Math.Round(x.AreaMm2, 1),
                Loops = x.Contours.Count
            }).ToList();
            _previewInfo.Text = station.Name + "  |  Offset: " + station.OffsetMm.ToString("F1") +
                " mm  |  Elements: " + station.ElementCount + "  |  Contours: " +
                station.ContourCount + "  |  Solid intersections: " + station.SolidIntersections +
                "  |  Boolean failures: " + station.BooleanFailures;
            DrawPreview();
        }

        private void DrawPreview()
        {
            _preview.Children.Clear();
            if (_result == null || _selectedStation == null || _selectedStation.Items.Count == 0 ||
                _preview.ActualWidth < 20 || _preview.ActualHeight < 20)
            {
                AddPreviewMessage(_result == null ? "Generate corridor sections to begin." :
                    "No measured solid intersects this station.");
                return;
            }

            XYZ stationOrigin = _result.BaseOrigin +
                _result.Normal * (_selectedStation.OffsetMm / 304.8);
            var projected = _selectedStation.Items.SelectMany(item => item.Contours.SelectMany(contour =>
                contour.PointsHost.Select(point => new
                {
                    Item = item,
                    Contour = contour,
                    X = (point - stationOrigin).DotProduct(_result.Right) * 304.8,
                    Y = (point - stationOrigin).DotProduct(_result.Up) * 304.8
                }))).ToList();
            if (projected.Count < 3)
            {
                AddPreviewMessage("No closed measured contours are available.");
                return;
            }

            double minX = projected.Min(p => p.X), maxX = projected.Max(p => p.X);
            double minY = projected.Min(p => p.Y), maxY = projected.Max(p => p.Y);
            double rangeX = Math.Max(1, maxX - minX), rangeY = Math.Max(1, maxY - minY);
            double scale = Math.Min((_preview.ActualWidth - 50) / rangeX, (_preview.ActualHeight - 50) / rangeY);
            scale = Math.Max(0.001, Math.Min(scale, 10));
            double drawWidth = rangeX * scale, drawHeight = rangeY * scale;
            double offsetX = (_preview.ActualWidth - drawWidth) * 0.5;
            double offsetY = (_preview.ActualHeight - drawHeight) * 0.5;

            foreach (var item in _selectedStation.Items)
            foreach (var contour in item.Contours)
            {
                var polygon = new Polygon
                {
                    Stroke = contour.IsHole ? Brushes.DarkOrange : Brushes.SteelBlue,
                    StrokeThickness = 1.6,
                    Fill = contour.IsHole ? Brushes.Transparent : new SolidColorBrush(Color.FromArgb(35, 70, 130, 180)),
                    Points = new PointCollection(contour.PointsHost.Select(point =>
                    {
                        double x = (point - stationOrigin).DotProduct(_result.Right) * 304.8;
                        double y = (point - stationOrigin).DotProduct(_result.Up) * 304.8;
                        return new System.Windows.Point(offsetX + (x - minX) * scale,
                            offsetY + (maxY - y) * scale);
                    }))
                };
                _preview.Children.Add(polygon);
            }
        }

        private void AddPreviewMessage(string message)
        {
            var label = new TextBlock
            {
                Text = message,
                Foreground = Brushes.Gray,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Width = Math.Max(180, _preview.ActualWidth - 30)
            };
            Canvas.SetLeft(label, 15);
            Canvas.SetTop(label, 15);
            _preview.Children.Add(label);
        }
    }
}