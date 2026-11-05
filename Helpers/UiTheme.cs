using System.Windows;
using System.Windows.Media;

namespace AlignTool.Helpers
{
    public static class UiTheme
    {
        public static readonly Color Accent = Color.FromRgb(0x00, 0x78, 0xD4);
        public static readonly Brush AccentBrush = new SolidColorBrush(Accent);
        public static readonly Brush CardBrush = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));
        public static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(0x32, 0x31, 0x30));

        static UiTheme()
        {
            if (AccentBrush is Freezable f1) f1.Freeze();
            if (CardBrush is Freezable f2) f2.Freeze();
            if (TextBrush is Freezable f3) f3.Freeze();
        }
    }
}
