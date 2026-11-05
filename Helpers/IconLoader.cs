using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace AlignTool.Helpers
{
    public static class IconLoader
    {
        private static string IconsFolder
        {
            get
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                string[] candidates =
                {
                    Path.Combine(dir, "Resources", "Icons"),
                    Path.Combine(dir, "Icons"),
                    Path.Combine(dir, "..", "Resources", "Icons"),
                };
                foreach (var c in candidates)
                {
                    if (Directory.Exists(c)) return c;
                }
                return Path.Combine(dir, "Resources", "Icons");
            }
        }

        public static BitmapImage GetIcon(string name, bool large)
        {
            try
            {
                string file = Path.Combine(IconsFolder, large ? name + "_32.png" : name + "_16.png");
                if (!File.Exists(file))
                    file = Path.Combine(IconsFolder, name + "_32.png");
                if (!File.Exists(file)) return null;

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(file, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }
    }
}
