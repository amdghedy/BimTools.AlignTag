using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AlignTool.Services;

namespace AlignTool.UI
{
    /// <summary>Read-only report for native Revit 3D MEP geometry coordination.</summary>
    public sealed class Revit3DCoordinationWindow : Window
    {
        public Revit3DCoordinationWindow(Revit3DCoordinationExtractionService.ExtractionResult data)
        {
            if (data == null) throw new ArgumentNullException("data");

            Title = "ALIGN | 3D MEP Coordination";
            Width = 1120;
            Height = 760;
            MinWidth = 850;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(245, 247, 251));

            var root = new DockPanel { Margin = new Thickness(18) };
            var header = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            header.Children.Add(new TextBlock
            {
                Text = "3D MEP COORDINATION",
                FontSize = 21,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(32, 45, 64))
            });
            header.Children.Add(new TextBlock
            {
                Text = data.SourceDescription,
                Margin = new Thickness(0, 5, 0, 0),
                Foreground = Brushes.DimGray,
                TextWrapping = TextWrapping.Wrap
            });
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            var close = new Button
            {
                Content = "Close",
                Width = 100,
                Padding = new Thickness(12, 7, 12, 7)
            };
            close.Click += (s, e) => Close();
            footer.Children.Add(close);
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);

            var body = new StackPanel();
            var metrics = new TextBlock
            {
                Text = BuildMetrics(data),
                FontSize = 13,
                LineHeight = 23,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };
            body.Children.Add(metrics);

            body.Children.Add(new TextBlock
            {
                Text = "NATIVE SOLID INTERSECTION FINDINGS",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 5, 0, 8)
            });

            var clashes = new DataGrid
            {
                AutoGenerateColumns = true,
                IsReadOnly = true,
                CanUserAddRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                ItemsSource = data.Clashes.Select(x => new
                {
                    ElementA = x.FirstName,
                    ModelA = x.FirstModel,
                    ElementB = x.SecondName,
                    ModelB = x.SecondModel,
                    IntersectionVolume_mm3 = Math.Round(x.IntersectionVolumeMm3, 1),
                    SolidPairs = x.SolidPairsTested,
                    GeometryA = x.FirstGeometryKind,
                    GeometryB = x.SecondGeometryKind,
                    InsulationRelated = x.IsInsulationOrLiningRelated,
                    x.GeometryBasis
                }).ToList(),
                MinHeight = 180,
                MaxHeight = 360
            };
            body.Children.Add(clashes);

            body.Children.Add(new TextBlock
            {
                Text = "CONNECTOR INVENTORY",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 16, 0, 8)
            });

            var connectors = new DataGrid
            {
                AutoGenerateColumns = true,
                IsReadOnly = true,
                CanUserAddRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                ItemsSource = data.Connectors.Select(x => new
                {
                    x.OwnerKey,
                    x.Key,
                    x.Domain,
                    x.ConnectorType,
                    x.Shape,
                    Connected = x.IsConnected,
                    References = x.References.Count,
                    Resolved = x.ResolvedReferences.Count,
                    Unresolved = x.UnresolvedReferences.Count
                }).ToList(),
                MinHeight = 120,
                MaxHeight = 220
            };
            body.Children.Add(connectors);

            var scroll = new ScrollViewer
            {
                Content = body,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            root.Children.Add(scroll);
            Content = root;
        }

        private static string BuildMetrics(Revit3DCoordinationExtractionService.ExtractionResult x)
        {
            var b = new StringBuilder();
            b.AppendLine("Elements scanned: " + x.ElementsScanned + "   |   With solids: " + x.ElementsWithSolids +
                "   |   Solids extracted: " + x.SolidsExtracted);
            b.AppendLine("Linked models: " + x.LinkedModelsScanned + "   |   Insulation elements: " + x.InsulationElements +
                "   |   Lining elements: " + x.LiningElements);
            b.AppendLine("Broad-phase element pairs: " + x.BroadPhasePairs + "   |   Native solid pairs tested: " +
                x.SolidPairsTested + "   |   Boolean failures: " + x.BooleanFailures);
            b.AppendLine("Native solid clashes: " + x.Clashes.Count + "   |   Connectors: " + x.Connectors.Count +
                "   |   Open connectors: " + x.OpenConnectors + "   |   Connector references: " + x.ConnectorReferences +
                "   |   Resolved links: " + x.ResolvedConnectorLinks +
                "   |   Unresolved references: " + x.UnresolvedConnectorReferences +
                "   |   Domain mismatches: " + x.ConnectorDomainMismatches +
                "   |   Endpoint gaps > 2 mm: " + x.ConnectorEndpointGaps);
            b.AppendLine("Geometry extraction failures: " + x.GeometryFailures);
            b.AppendLine("Safety: read-only analysis. No Revit transactions or model modifications are performed.");
            return b.ToString();
        }
    }
}