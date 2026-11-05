using System;
using System.IO;
using System.Xml.Serialization;
using AlignTool.Models;

namespace AlignTool.Services
{
    public class SettingsService
    {
        private static SettingsService _instance;
        public static SettingsService Instance => _instance ?? (_instance = new SettingsService());

        public AppSettings Current { get; private set; } = new AppSettings();

        private SettingsService()
        {
            try
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AlignTool", "settings.xml");
                if (File.Exists(path))
                {
                    var ser = new XmlSerializer(typeof(AppSettings));
                    using (var fs = File.OpenRead(path))
                        Current = (AppSettings)ser.Deserialize(fs) ?? new AppSettings();
                }
            }
            catch { Current = new AppSettings(); }
        }

        public void Save()
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AlignTool");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "settings.xml");
                var ser = new XmlSerializer(typeof(AppSettings));
                using (var fs = File.Create(path))
                    ser.Serialize(fs, Current);
            }
            catch { }
        }
    }
}
