using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BIMTools.TagAlign.Leaders
{
    /// <summary>
    /// Named layouts. Five ready-made presets plus any number of the user's own, stored in
    /// %AppData%\BIMTools\AutoArrangePresets.ini (plain text, easy to share with the office:
    /// just copy the file to a colleague).
    /// </summary>
    public static class PresetStore
    {
        static string PathFile
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BIMTools", "AutoArrangePresets.ini"); }
        }

        static readonly string[] BuiltInNames =
        {
            "Classic 45",
            "Steep 90 (compact)",
            "Staggered (long tags)",
            "Schedule order (by text)",
            "Fit to tag size + groups"
        };

        public static bool IsBuiltIn(string name) { return BuiltInNames.Contains(name); }

        static LeaderAlignSettings MakeBuiltIn(string name)
        {
            LeaderAlignSettings s = new LeaderAlignSettings();
            switch (name)
            {
                case "Classic 45":
                    s.AngleDeg = 45; s.Style = LeaderStyle.ParallelAngle; s.VerticalSepMm = 4; s.Order = OrderMode.Auto; break;
                case "Steep 90 (compact)":
                    s.AngleDeg = 90; s.Style = LeaderStyle.ParallelAngle; s.VerticalSepMm = 3.5; s.Order = OrderMode.Auto; break;
                case "Staggered (long tags)":
                    s.AngleDeg = 60; s.Intermittent = true; s.HorizontalSepMm = 35; s.VerticalSepMm = 3; s.Order = OrderMode.Auto; break;
                case "Schedule order (by text)":
                    s.Style = LeaderStyle.FixedLanding; s.LandingMm = 8; s.VerticalSepMm = 4; s.Order = OrderMode.ByText; break;
                case "Fit to tag size + groups":
                    s.AngleDeg = 75; s.AutoRowHeight = true; s.RowPaddingMm = 1; s.Group = GroupMode.Category; s.GroupGapRows = 1; break;
            }
            return s;
        }

        static Dictionary<string, List<string>> ReadUser()
        {
            Dictionary<string, List<string>> d = new Dictionary<string, List<string>>();
            try
            {
                if (!File.Exists(PathFile)) return d;
                List<string> cur = null;
                foreach (string raw in File.ReadAllLines(PathFile))
                {
                    string line = raw.Trim();
                    if (line.Length > 2 && line[0] == '[' && line[line.Length - 1] == ']')
                    {
                        cur = new List<string>();
                        d[line.Substring(1, line.Length - 2)] = cur;
                    }
                    else if (cur != null && line.Length > 0) cur.Add(line);
                }
            }
            catch { }
            return d;
        }

        static void WriteUser(Dictionary<string, List<string>> d)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PathFile));
                List<string> o = new List<string>();
                foreach (KeyValuePair<string, List<string>> kv in d)
                {
                    o.Add("[" + kv.Key + "]");
                    o.AddRange(kv.Value);
                    o.Add("");
                }
                File.WriteAllLines(PathFile, o.ToArray());
            }
            catch { }
        }

        /// <summary>Built-in names first, then the user's own in alphabetical order.</summary>
        public static List<string> Names()
        {
            List<string> l = new List<string>(BuiltInNames);
            l.AddRange(ReadUser().Keys.Where(k => !IsBuiltIn(k)).OrderBy(k => k, StringComparer.CurrentCultureIgnoreCase));
            return l;
        }

        public static LeaderAlignSettings Get(string name)
        {
            if (IsBuiltIn(name)) return MakeBuiltIn(name);
            List<string> lines;
            LeaderAlignSettings s = new LeaderAlignSettings();
            if (ReadUser().TryGetValue(name, out lines)) s.FromLines(lines);
            return s;
        }

        /// <summary>Returns false when the name is not allowed (empty, brackets, or the name of a built-in preset).</summary>
        public static bool Save(string name, LeaderAlignSettings s)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0 || name.IndexOfAny(new[] { '[', ']', '\r', '\n' }) >= 0 || IsBuiltIn(name)) return false;
            Dictionary<string, List<string>> d = ReadUser();
            d[name] = s.ToLines();
            WriteUser(d);
            return true;
        }

        public static void Delete(string name)
        {
            if (IsBuiltIn(name)) return;
            Dictionary<string, List<string>> d = ReadUser();
            if (d.Remove(name)) WriteUser(d);
        }
    }
}
