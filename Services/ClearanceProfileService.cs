using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace AlignTool.Services
{
    [DataContract]
    public sealed class ClearanceProfile
    {
        [DataMember(Order = 1)] public string Name { get; set; }
        [DataMember(Order = 2)] public string ZoneType { get; set; }
        [DataMember(Order = 3)] public double LeftMm { get; set; }
        [DataMember(Order = 4)] public double RightMm { get; set; }
        [DataMember(Order = 5)] public double BelowMm { get; set; }
        [DataMember(Order = 6)] public double AboveMm { get; set; }
        [DataMember(Order = 7)] public string Discipline { get; set; }
        [DataMember(Order = 8)] public string Notes { get; set; }

        public ClearanceProfile Clone()
        {
            return new ClearanceProfile
            {
                Name = Name, ZoneType = ZoneType, LeftMm = LeftMm, RightMm = RightMm,
                BelowMm = BelowMm, AboveMm = AboveMm, Discipline = Discipline, Notes = Notes
            };
        }
    }

    [DataContract]
    internal sealed class ClearanceProfileDocument
    {
        [DataMember(Order = 1)] public int SchemaVersion { get; set; }
        [DataMember(Order = 2)] public string ProjectKey { get; set; }
        [DataMember(Order = 3)] public DateTime UpdatedUtc { get; set; }
        [DataMember(Order = 4)] public List<ClearanceProfile> Profiles { get; set; }
    }

    public static class ClearanceProfileService
    {
        public static readonly string[] ZoneTypes =
        {
            "Maintenance Access", "Operational Access", "Side Access - Left",
            "Side Access - Right", "Overhead Access", "Underfloor Access",
            "Equipment Removal", "Electrical Working Space", "Custom"
        };

        public static List<ClearanceProfile> Load(string projectTitle)
        {
            string path = GetPath(projectTitle);
            if (!File.Exists(path)) return CreateDefaults();
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var serializer = new DataContractJsonSerializer(typeof(ClearanceProfileDocument));
                    var document = (ClearanceProfileDocument)serializer.ReadObject(stream);
                    if (document == null || document.SchemaVersion != 1 ||
                        document.Profiles == null) return CreateDefaults();
                    return document.Profiles.Where(IsValid).Select(x => x.Clone()).ToList();
                }
            }
            catch
            {
                // Keep the editor usable if a profile file is damaged; the original file is not overwritten.
                return CreateDefaults();
            }
        }

        public static void Save(string projectTitle, IList<ClearanceProfile> profiles)
        {
            if (profiles == null) throw new ArgumentNullException("profiles");
            foreach (var profile in profiles)
            {
                string error;
                if (!Validate(profile, out error))
                    throw new InvalidOperationException("Profile '" + (profile == null ? "" : profile.Name) + "': " + error);
            }

            string path = GetPath(projectTitle);
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            var document = new ClearanceProfileDocument
            {
                SchemaVersion = 1,
                ProjectKey = MakeProjectKey(projectTitle),
                UpdatedUtc = DateTime.UtcNow,
                Profiles = profiles.Select(x => x.Clone()).ToList()
            };
            string temporary = path + ".tmp";
            var serializer = new DataContractJsonSerializer(typeof(ClearanceProfileDocument));
            using (var stream = File.Create(temporary)) serializer.WriteObject(stream, document);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temporary, path);
        }

        public static bool Validate(ClearanceProfile profile, out string error)
        {
            error = null;
            if (profile == null) { error = "Profile is required."; return false; }
            if (string.IsNullOrWhiteSpace(profile.Name)) { error = "Name is required."; return false; }
            if (string.IsNullOrWhiteSpace(profile.ZoneType) || !ZoneTypes.Contains(profile.ZoneType))
            { error = "Select a supported zone type."; return false; }
            double[] values = { profile.LeftMm, profile.RightMm, profile.BelowMm, profile.AboveMm };
            if (values.Any(x => double.IsNaN(x) || double.IsInfinity(x) || x < 0 || x > 100000))
            { error = "Clearance dimensions must be finite values between 0 and 100000 mm."; return false; }
            if (values.All(x => x <= 0)) { error = "At least one clearance dimension must be greater than zero."; return false; }
            return true;
        }

        private static bool IsValid(ClearanceProfile profile)
        {
            string error;
            return Validate(profile, out error);
        }

        private static List<ClearanceProfile> CreateDefaults()
        {
            return new List<ClearanceProfile>
            {
                new ClearanceProfile { Name = "General maintenance envelope", ZoneType = "Maintenance Access", LeftMm = 600, RightMm = 600, BelowMm = 0, AboveMm = 600, Discipline = "MEP", Notes = "Adjust to equipment manufacturer and project requirements." },
                new ClearanceProfile { Name = "Electrical working space", ZoneType = "Electrical Working Space", LeftMm = 900, RightMm = 900, BelowMm = 0, AboveMm = 2000, Discipline = "Electrical", Notes = "Verify against applicable code and approved project criteria." },
                new ClearanceProfile { Name = "Overhead service access", ZoneType = "Overhead Access", LeftMm = 450, RightMm = 450, BelowMm = 0, AboveMm = 600, Discipline = "MEP", Notes = "Illustrative starting profile; not a compliance determination." }
            };
        }

        private static string GetPath(string projectTitle)
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(root, "BimTools", "ALIGN", "ClearanceProfiles", MakeProjectKey(projectTitle) + ".json");
        }

        private static string MakeProjectKey(string title)
        {
            string value = string.IsNullOrWhiteSpace(title) ? "UntitledProject" : title.Trim();
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            value = value.Replace('/', '_').Replace('\\', '_');
            return value.Length > 80 ? value.Substring(0, 80) : value;
        }
    }
}
