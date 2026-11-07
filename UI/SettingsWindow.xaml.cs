using System.Windows;
using AlignTool.Services;

namespace AlignTool.UI
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            var s = SettingsService.Instance.Current;
            TxtSpacing.Text = s.StackSpacingMm.ToString("F0");
            TxtCollisionGap.Text = s.AnnotationCollisionGapMm.ToString("F1");
            TxtMaxMove.Text = s.MaxMoveMm.ToString("F0");
            TxtMinimize.Text = s.MinimizeLeaderMm.ToString("F0");
            ChkSkipPinned.IsChecked = s.SkipPinnedTags;
            ChkLogging.IsChecked = s.EnableFileLogging;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var s = SettingsService.Instance.Current;
            if (double.TryParse(TxtSpacing.Text, out double spacing)) s.StackSpacingMm = spacing;
            if (double.TryParse(TxtCollisionGap.Text, out double gap) && gap >= 0 && gap <= 1000) s.AnnotationCollisionGapMm = gap;
            if (double.TryParse(TxtMaxMove.Text, out double max)) s.MaxMoveMm = max;
            if (double.TryParse(TxtMinimize.Text, out double min)) s.MinimizeLeaderMm = min;
            s.SkipPinnedTags = ChkSkipPinned.IsChecked == true;
            s.EnableFileLogging = ChkLogging.IsChecked == true;
            SettingsService.Instance.Save();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
